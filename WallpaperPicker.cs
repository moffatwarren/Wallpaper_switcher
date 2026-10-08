// Wallpaper Picker - a tray app with a horizontal, slanted-card carousel for choosing a wallpaper.
// Build with build.bat (uses the C# compiler that ships with Windows; no SDK needed).
//
// Runs in the background with a tray icon. Ctrl+Alt+W (or clicking the tray icon) toggles the picker.
//
//   <- / ->, A / D, H / L, mouse wheel   browse
//   Home / End, PageUp / PageDown        jump
//   R                                    random
//   Enter or double-click                set Desktop wallpaper + Lock Screen, then close
//   Shift+Enter                          set Desktop wallpaper only, then close
//   Esc / click elsewhere                hide
//
// Command line:
//   WallpaperPicker.exe [folder] [--tray]
//   --tray   start hidden in the tray (used by "Start with Windows")

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Drawing = System.Drawing;
using IOPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;
using WinForms = System.Windows.Forms;

namespace WallpaperPicker
{
    public static class Program
    {
        const string MutexName = "WallpaperPicker.SingleInstance";
        const string ShowEventName = "WallpaperPicker.Show";

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        [STAThread]
        public static void Main(string[] args)
        {
            bool trayOnly = args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
            // Default: <Pictures>\Wallpapers (GetFolderPath handles OneDrive redirection)
            string folder = args.FirstOrDefault(a => !a.StartsWith("--"))
                ?? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Wallpapers");

            bool firstInstance;
            var mutex = new Mutex(true, MutexName, out firstInstance);
            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            if (!firstInstance)
            {
                // Already running in the tray: ask it to open the picker instead
                if (!trayOnly)
                {
                    AllowSetForegroundWindow(-1); // ASFW_ANY
                    showEvent.Set();
                }
                return;
            }

            WinForms.Application.EnableVisualStyles();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var tray = new TrayApp(folder, showEvent);
            if (!trayOnly) app.Dispatcher.BeginInvoke(new Action(tray.ShowPicker));
            app.Run();

            GC.KeepAlive(mutex);
        }
    }

    // ---------- Tray icon, global hotkey, single-instance signalling ----------

    class TrayApp
    {
        const int HotkeyId = 1;
        const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        const uint VK_W = 0x57;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "WallpaperPicker";

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        readonly PickerWindow picker;
        readonly WinForms.NotifyIcon icon;
        readonly WinForms.ToolStripMenuItem startupItem;
        readonly IntPtr hwnd;

        public TrayApp(string folder, EventWaitHandle showEvent)
        {
            picker = new PickerWindow(folder);

            // The picker's (hidden) window receives the global hotkey messages
            hwnd = new WindowInteropHelper(picker).EnsureHandle();
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
            bool hotkeyOk = RegisterHotKey(hwnd, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_W);

            var menu = new WinForms.ContextMenuStrip();
            var openItem = new WinForms.ToolStripMenuItem("Open picker", null, delegate { ShowPicker(); })
            {
                ShortcutKeyDisplayString = "Ctrl+Alt+W",
                Font = new Drawing.Font(WinForms.SystemInformation.MenuFont, Drawing.FontStyle.Bold),
            };
            menu.Items.Add(openItem);
            menu.Items.Add("Random wallpaper", null, delegate { RandomWallpaper(); });
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Open wallpapers folder", null, delegate { OpenFolder(); });
            startupItem = new WinForms.ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); })
            {
                Checked = IsStartupEnabled(),
            };
            menu.Items.Add(startupItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Exit(); });

            icon = new WinForms.NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "Wallpaper Picker (Ctrl+Alt+W)",
                ContextMenuStrip = menu,
                Visible = true,
            };
            icon.MouseClick += delegate(object s, WinForms.MouseEventArgs e)
            {
                if (e.Button == WinForms.MouseButtons.Left) TogglePicker();
            };

            if (!hotkeyOk)
                icon.ShowBalloonTip(5000, "Wallpaper Picker",
                    "Ctrl+Alt+W is already used by another app. Use the tray icon instead.", WinForms.ToolTipIcon.Warning);

            // A second launch of the exe signals this event to open the picker
            var waiter = new Thread(() =>
            {
                while (showEvent.WaitOne())
                    picker.Dispatcher.BeginInvoke(new Action(ShowPicker));
            }) { IsBackground = true };
            waiter.Start();
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                TogglePicker();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void ShowPicker()
        {
            string problem = picker.ShowPicker();
            if (problem != null)
                icon.ShowBalloonTip(5000, "Wallpaper Picker", problem, WinForms.ToolTipIcon.Warning);
        }

        void TogglePicker()
        {
            // Clicking the tray icon deactivates the picker (which hides it) before this runs,
            // so a recent hide counts as "was open"
            if (picker.IsVisible || picker.JustHidden) picker.Hide();
            else ShowPicker();
        }

        void RandomWallpaper()
        {
            var files = PickerWindow.ScanFolder(picker.Folder);
            if (files.Count == 0) { ShowPicker(); return; }
            string file = files[new Random().Next(files.Count)];
            PickerWindow.ApplyWallpaper(file, true, null);
        }

        void OpenFolder()
        {
            if (Directory.Exists(picker.Folder)) Process.Start("explorer.exe", "\"" + picker.Folder + "\"");
            else ShowPicker(); // shows the "folder not found" message
        }

        static bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(RunValue) != null;
        }

        void ToggleStartup()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (IsStartupEnabled()) key.DeleteValue(RunValue, false);
                else key.SetValue(RunValue, "\"" + Process.GetCurrentProcess().MainModule.FileName + "\" --tray");
            }
            startupItem.Checked = IsStartupEnabled();
        }

        void Exit()
        {
            UnregisterHotKey(hwnd, HotkeyId);
            icon.Visible = false;
            icon.Dispose();
            Application.Current.Shutdown();
        }

        // Same .ico as the exe (embedded by build.bat), at the tray's size for the current DPI
        static Drawing.Icon MakeIcon()
        {
            using (var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("WallpaperPicker.ico"))
                return new Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
        }
    }

    // ---------- The picker window ----------

    class Card
    {
        public string File;
        public Grid Root;
        public Image Image;
        public ShapePath Outline;
        public ScaleTransform Scale;
        public UIElement Shade;
        public Grid Face;
        public double Width;
    }

    // One set of cards plus its loading state; replaced whenever the folder contents change
    class Batch
    {
        public List<Card> Cards;
        public bool[] Requested;
        public readonly object Lock = new object();
    }

    public class PickerWindow : Window
    {
        // Card geometry (device-independent pixels)
        const double CardW = 300;
        const double CardH = 440;
        const double Slant = 110;
        const double Gap = 14;
        const double Step = CardW - Slant + Gap;
        const double SelectedExtra = 220;   // how much wider the selected card gets

        static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        static readonly Color Accent = Color.FromRgb(0xF0, 0x87, 0x6A);
        static readonly Brush AccentBrush = Frozen(new SolidColorBrush(Accent));
        static readonly Brush IdleStroke = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
        static readonly Brush HoverStroke = Frozen(new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)));
        static readonly Brush DimText = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)));
        static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI");

        public readonly string Folder;

        Batch batch = new Batch { Cards = new List<Card>(), Requested = new bool[0] };
        List<Card> cards { get { return batch.Cards; } }

        readonly Canvas canvas = new Canvas { ClipToBounds = true };
        readonly TextBlock counter = new TextBlock();
        readonly TextBlock nameText = new TextBlock();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly double dpiScale;
        readonly int decodeHeight;

        int selected;
        int hovered = -1;
        double offset;          // animated scroll position, in card units
        double lastFrame;
        bool animating;
        int wheelAccum;
        volatile int focusIndex;
        DateTime hiddenAt = DateTime.MinValue;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public PickerWindow(string folder)
        {
            Folder = folder;
            Title = "Wallpaper Picker";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Height = 600;
            FontFamily = UiFont;

            using (var g = Drawing.Graphics.FromHwnd(IntPtr.Zero)) dpiScale = g.DpiY / 96.0;
            decodeHeight = (int)(CardH * dpiScale);

            Content = BuildChrome();

            KeyDown += OnKeyDown;
            PreviewMouseWheel += OnWheel;
            Deactivated += delegate { Hide(); };
            IsVisibleChanged += delegate { if (!IsVisible) hiddenAt = DateTime.UtcNow; };
            canvas.SizeChanged += delegate { Layout(); };
        }

        public bool JustHidden { get { return (DateTime.UtcNow - hiddenAt).TotalMilliseconds < 300; } }

        // Closing (Alt+F4) just hides; the tray app owns the lifetime
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

        public static List<string> ScanFolder(string folder)
        {
            if (!Directory.Exists(folder)) return new List<string>();
            return Directory.GetFiles(folder)
                .Where(f => Extensions.Contains(IOPath.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => IOPath.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Shows the picker on the monitor under the mouse. Returns an error message, or null.
        public string ShowPicker()
        {
            if (!Directory.Exists(Folder)) return "Wallpaper folder not found:\n" + Folder;

            var files = ScanFolder(Folder);
            if (files.Count == 0) return "No images (.jpg, .png, .bmp, .webp) found in:\n" + Folder;
            if (!files.SequenceEqual(cards.Select(c => c.File))) Rebuild(files);

            selected = FindCurrentWallpaper(files);
            offset = selected;
            focusIndex = selected;
            hovered = -1;
            UpdateSelectionVisuals();

            var area = WinForms.Screen.FromPoint(WinForms.Cursor.Position).WorkingArea;
            Width = Math.Min(1700, area.Width / dpiScale * 0.9);
            Left = area.Left / dpiScale + (area.Width / dpiScale - Width) / 2;
            Top = area.Top / dpiScale + (area.Height / dpiScale - Height) / 2;

            Show();
            Activate();
            Focus();
            Layout();
            return null;
        }

        // ---------- UI construction ----------

        UIElement BuildChrome()
        {
            var header = new DockPanel { Margin = new Thickness(26, 18, 26, 6) };
            var title = new TextBlock
            {
                Text = "WALLPAPERS",
                Foreground = DimText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
            };
            counter.Foreground = DimText;
            counter.FontSize = 12;
            counter.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);
            header.Children.Add(counter);

            var footer = new Grid { Margin = new Thickness(26, 4, 26, 18) };
            nameText.Foreground = Brushes.White;
            nameText.FontSize = 16;
            nameText.HorizontalAlignment = HorizontalAlignment.Center;
            nameText.TextTrimming = TextTrimming.CharacterEllipsis;
            // Keeps text readable now that the background is see-through
            var textShadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 };
            nameText.Effect = textShadow;
            counter.Effect = textShadow;
            var hints = new TextBlock
            {
                Text = "← → browse    Enter apply    Shift+Enter desktop only    R random    Esc close",
                Foreground = DimText,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            footer.Children.Add(nameText);
            footer.Children.Add(hints);

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(header, 0);
            Grid.SetRow(canvas, 1);
            Grid.SetRow(footer, 2);
            layout.Children.Add(header);
            layout.Children.Add(canvas);
            layout.Children.Add(footer);

            var shell = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x11, 0x13, 0x18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Child = layout,
            };
            // Drag the window by its empty background
            shell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (!e.Handled) DragMove(); };
            return shell;
        }

        void Rebuild(List<string> files)
        {
            canvas.Children.Clear();
            var next = new Batch { Cards = new List<Card>(), Requested = new bool[files.Count] };
            for (int i = 0; i < files.Count; i++)
            {
                var c = BuildCard(files[i], i);
                next.Cards.Add(c);
                canvas.Children.Add(c.Root);
            }
            batch = next;

            for (int t = 0; t < 2; t++)
            {
                var b = next;
                new Thread(() => LoadLoop(b)) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
            }
        }

        static Geometry CardShape(double width)
        {
            var shape = new StreamGeometry();
            using (var ctx = shape.Open())
            {
                ctx.BeginFigure(new Point(Slant, 0), true, true);
                ctx.PolyLineTo(new[] { new Point(width, 0), new Point(width - Slant, CardH), new Point(0, CardH) }, true, true);
            }
            shape.Freeze();
            return shape;
        }

        Card BuildCard(string file, int index)
        {
            var shape = CardShape(CardW);

            var placeholder = new TextBlock
            {
                Text = IOPath.GetFileNameWithoutExtension(file),
                Foreground = DimText,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Width = CardW - Slant - 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // Centered alignment makes UniformToFill crop around the middle instead of the top-left
            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var face = new Grid
            {
                Clip = shape,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1F, 0x26)),
            };
            face.Children.Add(placeholder);
            face.Children.Add(image);
            // Dims distant cards. Fading the card's own opacity would let the desktop show through.
            var shade = new System.Windows.Shapes.Rectangle { Fill = Brushes.Black, Opacity = 0, IsHitTestVisible = false };
            face.Children.Add(shade);

            var outline = new ShapePath
            {
                Data = shape,
                Stroke = IdleStroke,
                StrokeThickness = 1,
                StrokeLineJoin = PenLineJoin.Round,
            };

            var scale = new ScaleTransform(1, 1, CardW / 2, CardH / 2);
            var root = new Grid
            {
                Width = CardW,
                Height = CardH,
                RenderTransform = scale,
                Cursor = Cursors.Hand,
            };
            root.Children.Add(face);
            root.Children.Add(outline);

            root.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (e.ClickCount >= 2) Apply(index, (Keyboard.Modifiers & ModifierKeys.Shift) == 0);
                else Select(index);
            };
            root.MouseEnter += delegate { hovered = index; UpdateSelectionVisuals(); };
            root.MouseLeave += delegate { if (hovered == index) hovered = -1; UpdateSelectionVisuals(); };

            return new Card { File = file, Root = root, Image = image, Outline = outline, Scale = scale, Shade = shade, Face = face, Width = CardW };
        }

        // ---------- Selection & animation ----------

        void Select(int index)
        {
            index = Math.Max(0, Math.Min(cards.Count - 1, index));
            if (index == selected) return;
            selected = index;
            focusIndex = index;
            UpdateSelectionVisuals();
            StartAnimation();
        }

        void UpdateSelectionVisuals()
        {
            if (cards.Count == 0) return;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (i == selected)
                {
                    c.Outline.Stroke = AccentBrush;
                    c.Outline.StrokeThickness = 3;
                    c.Outline.Effect = new DropShadowEffect { Color = Accent, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.7 };
                    Panel.SetZIndex(c.Root, 2);
                }
                else
                {
                    c.Outline.Stroke = i == hovered ? HoverStroke : IdleStroke;
                    c.Outline.StrokeThickness = i == hovered ? 2 : 1;
                    c.Outline.Effect = null;
                    Panel.SetZIndex(c.Root, i == hovered ? 1 : 0);
                }
            }
            counter.Text = (selected + 1) + " / " + cards.Count;
            nameText.Text = IOPath.GetFileNameWithoutExtension(cards[selected].File);
        }

        void StartAnimation()
        {
            if (animating) return;
            animating = true;
            lastFrame = clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += OnFrame;
        }

        void OnFrame(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, now - lastFrame);
            lastFrame = now;

            // Exponential ease toward the selected card
            offset += (selected - offset) * (1 - Math.Exp(-dt * 13));
            if (Math.Abs(selected - offset) < 0.001)
            {
                offset = selected;
                animating = false;
                CompositionTarget.Rendering -= OnFrame;
            }
            Layout();
        }

        void Layout()
        {
            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w <= 0) return;
            double top = (h - CardH) / 2;
            double center = w / 2;

            // The two cards around the scroll position share SelectedExtra (their weights sum to 1),
            // so the widening slides smoothly from card to card and the centered one stays centered.
            double pushed = -SelectedExtra / 2;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                double d = Math.Abs(i - offset);
                double cw = CardW + SelectedExtra * Math.Max(0, 1 - d);
                double x = center + (i - offset) * Step - CardW / 2 + pushed;
                pushed += cw - CardW;
                if (x > w + 40 || x + cw < -40)
                {
                    c.Root.Visibility = Visibility.Collapsed;
                    continue;
                }
                c.Root.Visibility = Visibility.Visible;
                Canvas.SetLeft(c.Root, x);
                Canvas.SetTop(c.Root, top);

                if (Math.Abs(cw - c.Width) > 0.01)
                {
                    c.Width = cw;
                    c.Root.Width = cw;
                    var shape = CardShape(cw);
                    c.Face.Clip = shape;
                    c.Outline.Data = shape;
                    c.Scale.CenterX = cw / 2;
                }

                double s = 0.93 + 0.07 * Math.Max(0, 1 - d);
                c.Scale.ScaleX = s;
                c.Scale.ScaleY = s;
                c.Shade.Opacity = Math.Min(0.45, Math.Max(0, d - 0.6) * 0.09);
            }
        }

        // ---------- Input ----------

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left: case Key.A: case Key.H: Select(selected - 1); break;
                case Key.Right: case Key.D: case Key.L: Select(selected + 1); break;
                case Key.PageUp: Select(selected - 5); break;
                case Key.PageDown: Select(selected + 5); break;
                case Key.Home: Select(0); break;
                case Key.End: Select(cards.Count - 1); break;
                case Key.R:
                    if (cards.Count > 1)
                    {
                        var rnd = new Random();
                        int r;
                        do { r = rnd.Next(cards.Count); } while (r == selected);
                        Select(r);
                    }
                    break;
                case Key.Enter: Apply(selected, (Keyboard.Modifiers & ModifierKeys.Shift) == 0); break;
                case Key.Escape: Hide(); break;
                default: return;
            }
            e.Handled = true;
        }

        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            // Accumulate so precision touchpads (small deltas) scroll at a sane rate
            wheelAccum += e.Delta;
            while (wheelAccum >= 120) { Select(selected - 1); wheelAccum -= 120; }
            while (wheelAccum <= -120) { Select(selected + 1); wheelAccum += 120; }
            e.Handled = true;
        }

        // ---------- Thumbnails ----------

        // Always decode the not-yet-loaded image nearest the current selection.
        // Stops when everything is loaded or the batch has been replaced by a rescan.
        void LoadLoop(Batch b)
        {
            while (b == batch)
            {
                int idx = -1;
                lock (b.Lock)
                {
                    int f = Math.Min(focusIndex, b.Cards.Count - 1);
                    for (int r = 0; r < b.Cards.Count && idx < 0; r++)
                    {
                        if (f + r < b.Cards.Count && !b.Requested[f + r]) idx = f + r;
                        else if (f - r >= 0 && !b.Requested[f - r]) idx = f - r;
                    }
                    if (idx < 0) return;
                    b.Requested[idx] = true;
                }

                BitmapImage bmp = null;
                try
                {
                    bmp = new BitmapImage();
                    using (var fs = new FileStream(b.Cards[idx].File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bmp.DecodePixelHeight = decodeHeight;
                        bmp.StreamSource = fs;
                        bmp.EndInit();
                    }
                    bmp.Freeze();
                }
                catch
                {
                    bmp = null; // unsupported/corrupt file: placeholder with the file name stays visible
                }

                if (bmp != null)
                {
                    var card = b.Cards[idx];
                    var img = bmp;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        card.Image.Source = img;
                        card.Image.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                    }));
                }
            }
        }

        // ---------- Applying ----------

        void Apply(int index, bool includeLockScreen)
        {
            Select(index);
            Hide();
            // The picker is gone by the time this finishes, so only failures are worth reporting
            ApplyWallpaper(cards[index].File, includeLockScreen, msg =>
            {
                if (msg.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0)
                    Dispatcher.BeginInvoke(new Action(() =>
                        MessageBox.Show(msg, "Wallpaper Picker", MessageBoxButton.OK, MessageBoxImage.Warning)));
            });
        }

        // Sets the wallpaper on a background thread, then reports a status message
        public static void ApplyWallpaper(string file, bool includeLockScreen, Action<string> done)
        {
            Task.Run(() =>
            {
                // SPI_SETDESKWALLPAPER, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
                bool ok = SystemParametersInfo(0x0014, 0, file, 0x01 | 0x02);
                string msg = ok ? "Desktop wallpaper set" : "Failed to set wallpaper";

                if (ok && includeLockScreen)
                    msg = SetLockScreen(file) ? "Desktop + Lock Screen set" : "Desktop set (Lock Screen failed)";

                if (done != null) done(msg);
            });
        }

        // The Lock Screen API is WinRT, which is easiest to reach from Windows PowerShell.
        static bool SetLockScreen(string file)
        {
            string script = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Set-LockScreen.ps1");
            if (!System.IO.File.Exists(script)) return false;

            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script +
                "\" -ImagePath \"" + file + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            try
            {
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(30000)) return false;
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        // Start on whatever is currently the desktop wallpaper, if it lives in this folder
        static int FindCurrentWallpaper(List<string> files)
        {
            foreach (var current in CurrentWallpaperCandidates())
            {
                if (string.IsNullOrEmpty(current)) continue;
                int i = files.FindIndex(f => string.Equals(f, current, StringComparison.OrdinalIgnoreCase));
                if (i < 0)
                    i = files.FindIndex(f => string.Equals(IOPath.GetFileName(f), IOPath.GetFileName(current),
                        StringComparison.OrdinalIgnoreCase));
                if (i >= 0) return i;
            }
            return 0;
        }

        // Most reliable source first. The "WallPaper" registry value goes stale when the
        // wallpaper is set through Settings, so it's only the last resort.
        static IEnumerable<string> CurrentWallpaperCandidates()
        {
            yield return WallpaperOfMonitorUnderCursor();

            byte[] transcoded = null;
            string legacy = null;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    if (key != null)
                    {
                        transcoded = key.GetValue("TranscodedImageCache") as byte[];
                        legacy = key.GetValue("WallPaper") as string;
                    }
                }
            }
            catch { }

            // TranscodedImageCache: 24-byte header followed by the source path (UTF-16, null-terminated)
            if (transcoded != null && transcoded.Length > 24)
                yield return System.Text.Encoding.Unicode.GetString(transcoded, 24, transcoded.Length - 24).Split('\0')[0];

            yield return legacy;
        }

        static string WallpaperOfMonitorUnderCursor()
        {
            try
            {
                var dw = (IDesktopWallpaper)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")));
                try
                {
                    var pt = WinForms.Cursor.Position;
                    uint count = dw.GetMonitorDevicePathCount();
                    string first = null;
                    for (uint i = 0; i < count; i++)
                    {
                        string id = dw.GetMonitorDevicePathAt(i);
                        string path = dw.GetWallpaper(id);
                        if (first == null) first = path;
                        var r = dw.GetMonitorRECT(id);
                        if (pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom) return path;
                    }
                    return first;
                }
                finally { Marshal.ReleaseComObject(dw); }
            }
            catch { return null; }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    // Windows 8+ desktop wallpaper API (only the methods we call need real signatures, but
    // every method must be declared in vtable order)
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        NativeRect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
    }
}
