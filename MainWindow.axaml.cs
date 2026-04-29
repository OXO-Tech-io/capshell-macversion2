using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Threading.Tasks;
using Avalonia;

namespace CGPShell;

public partial class MainWindow : Window
{
    private Window? _exitWindow;
    private System.Timers.Timer? _exitTopTimer;
    private IntPtr _overlayNsHandle = IntPtr.Zero;

    private bool _monitoring = true;

    private bool _overlayLevelApplied = false;

    public MainWindow()
    {
        InitializeComponent();

        this.Closing += PreventClosing;
        this.Opened += OnWindowOpened;

        this.SystemDecorations = SystemDecorations.None;
        this.Topmost = true;
        this.WindowState = WindowState.FullScreen;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {

        // KeyBlocker.Start();
        // try
        // {
        //     var handle = this.TryGetPlatformHandle();
        //     if (handle != null)
        //     {
        //         await Task.Delay(500);
        //         KioskMode.Enable(handle.Handle);
        //         this.Activate();
        //         this.Focus();
        //     }
        // }
        // catch (Exception ex) { Console.WriteLine($"❌ KioskMode: {ex.Message}"); }

        // try { MultiMonitorBlocker.BlockSecondaryScreens(this); }
        // catch (Exception ex) { Console.WriteLine($"❌ MultiMonitor: {ex.Message}"); }

        // try { ExamWebView.Url = new Uri("https://exam-app-ashy.vercel.app"); }
        // catch (Exception ex) { Console.WriteLine($"❌ WebView: {ex.Message}"); }

        // await Task.Delay(3000);
        // CreateExitButtonOverlay();
        
        // // ✅ Give focus back to main window so WebView can receive input
        // await Task.Delay(500);
        // this.Activate();
        // this.Focus();
        // Console.WriteLine("✅ Focus returned to main window");

        Console.WriteLine("✅ MainWindow opened successfully");

        var isSecure = await InitializeSecurityAsync();

        if (!isSecure)
            return;

        Console.WriteLine("✅ Security passed -- loading exam");

        await LoadExamAsync();
        await Task.Delay(1000);
        CreateExitButtonOverlay();
        _monitoring = true;
        StartDisplayMonitoring();
        this.Activate();
        this.Focus();
        
    }

    private async Task<bool> InitializeSecurityAsync()
    {
        try
        {
            bool keyboardOk = KeyBlocker.Start();
            if (!keyboardOk)
            {
                ShowBlockingError("Enable Accessibility permission to continue.");
                return false;
            }

            // 2. Native window handle
            var handle = this.TryGetPlatformHandle();
            if (handle == null)
            {
                ShowBlockingError("Unable to access system window.");
                return false;
            }

            // 3. Enable kiosk
            KioskMode.Enable(handle.Handle);
            await Task.Delay(500);

            // 4. Fullscreen enforcement
            this.WindowState = WindowState.FullScreen;
            await Task.Delay(500);

            if (this.WindowState != WindowState.FullScreen)
            {
                ShowBlockingError("Failed to enter fullscreen mode.");
                return false;
            }

            // 5. Display check
            if (this.Screens.All.Count != 1)
            {
                MultiMonitorBlocker.BlockSecondaryScreens(this);
                return false;
            }

            // 6. Start monitoring (non-blocking)
            StartDisplayMonitoring();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Security init error: {ex.Message}");
            ShowBlockingError("Security initialization failed.");
            return false;
        }
    }

    private async Task LoadExamAsync()
    {
        try
        {
            // 🔒 Later replace with backend URL
            var url = new Uri("https://exam-app-ashy.vercel.app");

            ExamWebView.Url = url;

            Console.WriteLine("🌐 Exam loaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WebView error: {ex.Message}");
            ShowBlockingError("Failed to load exam.");
        }
    }

    private void StartDisplayMonitoring()
    {
        _ = Task.Run(async () =>
        {
            while (_monitoring)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (this.Screens.All.Count != 1)
                    {
                        Console.WriteLine("🚨 Secondary display detected");
                        ShowBlockingError("External display detected.");
                    }
                });

                await Task.Delay(2000);
            }
        });
    }

    private void ShowBlockingError(string message)
    {
        this.Content = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            Background = Brushes.Black,
            FontSize = 20,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
    }

    private void CreateExitButtonOverlay()
    {
        if (_exitWindow != null) return;

        Console.WriteLine("🔧 Creating exit button overlay...");

        _exitWindow = new Window
        {
            Width = 120,
            Height = 50,
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur },
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Focusable = false, // ✅ CRITICAL: Prevent overlay from stealing focus
        };

        var button = new Button
        {
            Content = "⏻ Exit",
            Width = 110,
            Height = 40,
            Background = new SolidColorBrush(Color.FromArgb(220, 170, 34, 34)),
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Focusable = true, // ✅ Button itself should be focusable when clicked
        };

        button.Click += (_, _) => ExitApp();
        
        // ✅ After button click, don't let overlay steal focus
        button.PointerPressed += (_, _) => 
        {
            // Click is handled, immediately return focus to main window
            Task.Delay(100).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    this.Activate();
                    this.Focus();

                    
                });
            });
        };
        
        _exitWindow.Content = button;

        PositionExitOverlay();
        _exitWindow.Show();
        
        Console.WriteLine("✅ Overlay window shown");

        Task.Delay(200).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _overlayNsHandle = _exitWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

                if (_overlayNsHandle != IntPtr.Zero)
                {
                    Console.WriteLine($"✅ Got overlay handle: {_overlayNsHandle}");
                    KioskMode.SetOverlayLevel(_overlayNsHandle);
                    KioskMode.ForceRenderOverlay(_overlayNsHandle);
                    _overlayLevelApplied = true;
                    // ✅ Make overlay non-activating (won't steal focus)
                    KioskMode.MakeOverlayClickable(_overlayNsHandle);
                    
                    Console.WriteLine("✅ Overlay configured with native macOS APIs");
                }
                else
                {
                    Console.WriteLine("❌ Failed to get overlay NSWindow handle");
                }
            });
        });

        this.PositionChanged += (_, _) => PositionExitOverlay();
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty || e.Property == WindowStateProperty)
                PositionExitOverlay();
        };

        _exitTopTimer = new System.Timers.Timer(2000);
        _exitTopTimer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_overlayNsHandle == IntPtr.Zero || _exitWindow == null)
                    return;

                if (!_exitWindow.IsVisible)
                {
                    Console.WriteLine("⚠️ Overlay missing, restoring...");
                    _exitWindow.Show();

                    KioskMode.SetOverlayLevel(_overlayNsHandle);
                }
            });
        };
        _exitTopTimer.AutoReset = true;
        _exitTopTimer.Start();

        Console.WriteLine("✅ Exit button overlay created successfully");
    }

    private void PositionExitOverlay()
    {
        if (_exitWindow == null) return;

        var screen = this.Screens.ScreenFromVisual(this);
        if (screen == null) return;

        var scaling = screen.Scaling;
        var bounds = screen.Bounds;

        var x = bounds.Right - (int)(_exitWindow.Width * scaling) - 20;
        var y = bounds.Y + 20;

        _exitWindow.Position = new PixelPoint(x, y);
        
        Console.WriteLine($"📍 Overlay positioned at ({x}, {y})");
    }

    private void ExitApp()
    {
        Console.WriteLine("🚪 Exit requested...");

        try
        {
            _monitoring = false;
            _exitTopTimer?.Stop();
            _exitTopTimer?.Dispose();
            _exitTopTimer = null;

            _exitWindow?.Close();
            _exitWindow = null;
            _overlayNsHandle = IntPtr.Zero;

            KioskMode.Disable();
            MultiMonitorBlocker.UnblockAllScreens();
            this.Closing -= PreventClosing;
        }
        catch (Exception ex) { Console.WriteLine($"ExitApp error: {ex.Message}"); }
        KeyBlocker.Stop();
        Environment.Exit(0);
    }

    private void PreventClosing(object? sender, WindowClosingEventArgs e)
        => e.Cancel = true;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool block = false;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.Q) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.W) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.Tab) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.H) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.M) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key == Key.Space) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.W) block = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.F4) block = true;
        if (e.Key == Key.Escape) block = true;
        if (e.Key >= Key.F1 && e.Key <= Key.F12) block = true;

        if (block)
        {
            e.Handled = true;
            Console.WriteLine($"🚫 Blocked: {e.Key}");
        }
    }
}