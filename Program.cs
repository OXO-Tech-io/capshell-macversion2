using Avalonia;
using Avalonia.WebView.Desktop;
using System;
using System.Runtime.InteropServices;

namespace CGPShell;

class Program
{
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_SetPresentation(IntPtr receiver, IntPtr selector, ulong options);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool value);

    private const ulong NSApplicationPresentationHideDock                   = 1 << 1;
    private const ulong NSApplicationPresentationHideMenuBar                = 1 << 3;
    private const ulong NSApplicationPresentationDisableAppleMenu           = 1 << 4;
    private const ulong NSApplicationPresentationDisableProcessSwitching    = 1 << 5;
    private const ulong NSApplicationPresentationDisableSessionTermination  = 1 << 6;
    private const ulong NSApplicationPresentationDisableHideApplication     = 1 << 8;
    private const ulong NSApplicationPresentationDisableMenuBarTransparency = 1 << 9;

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Console.WriteLine($"💥 FATAL: {e.ExceptionObject}");
        };

        try
        {
            // ✅ STEP 1 — Activate app immediately
            ActivateApp();

            // ✅ STEP 2 — Apply kiosk BEFORE Avalonia window even shows
            ApplyKioskImmediately();

            // ✅ STEP 3 — Disable gestures in background immediately
            DisableGesturesEarly();

            // ✅ STEP 4 — Start Avalonia (window appears ALREADY in kiosk mode)
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 CRASH: {ex}");
            Console.ReadLine();
        }
    }

    private static void ActivateApp()
    {
        try
        {
            IntPtr nsAppClass = objc_getClass("NSApplication");
            IntPtr sharedAppSel = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend(nsAppClass, sharedAppSel);
            IntPtr activateSel = sel_registerName("activateIgnoringOtherApps:");
            objc_msgSend_bool(nsApp, activateSel, true);
            Console.WriteLine("✅ NSApp activated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ActivateApp error: {ex.Message}");
        }
    }

    private static void ApplyKioskImmediately()
    {
        try
        {
            IntPtr nsAppClass = objc_getClass("NSApplication");
            IntPtr sharedAppSel = sel_registerName("sharedApplication");
            IntPtr nsApp = objc_msgSend(nsAppClass, sharedAppSel);
            IntPtr setPresentationSel = sel_registerName("setPresentationOptions:");

            ulong options =
                NSApplicationPresentationHideDock |
                NSApplicationPresentationHideMenuBar |
                NSApplicationPresentationDisableAppleMenu |
                NSApplicationPresentationDisableProcessSwitching |
                NSApplicationPresentationDisableSessionTermination |
                NSApplicationPresentationDisableHideApplication |
                NSApplicationPresentationDisableMenuBarTransparency;

            objc_msgSend_SetPresentation(nsApp, setPresentationSel, options);
            Console.WriteLine("✅ Kiosk applied before window shown");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ApplyKiosk error: {ex.Message}");
        }
    }

    private static void DisableGesturesEarly()
    {
        new System.Threading.Thread(() =>
        {
            try
            {
                void Run(string cmd, string a)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = cmd, Arguments = a,
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    })?.WaitForExit();
                }

                Run("defaults", "write com.apple.dock showLaunchpadGestureEnabled -bool false");
                Run("defaults", "write com.apple.dock mcx-expose-disabled -bool true");
                Run("defaults", "write com.apple.dock showAppExposeGestureEnabled -bool false");
                Run("defaults", "write com.apple.dock showDesktopGestureEnabled -bool false");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadThreeFingerHorizSwipeGesture -int 0");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerHorizSwipeGesture -int 0");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerVertSwipeGesture -int 0");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerPinchGesture -int 0");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFiveFingerPinchGesture -int 0");
                Run("killall", "Dock");
                Console.WriteLine("🔒 Gestures disabled early");
            }
            catch { }
        })
        { IsBackground = true }.Start();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseDesktopWebView()
            .LogToTrace();
}