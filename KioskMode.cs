using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;
using System.Diagnostics;

namespace CGPShell
{
    public static class KioskMode
    {
        private const ulong NSApplicationPresentationHideDock                   = 1 << 1;
        // private const ulong NSApplicationPresentationAutoHideDock               = 1 << 2;  // ✅ Prevents Dock hover
        private const ulong NSApplicationPresentationHideMenuBar                = 1 << 3;
        private const ulong NSApplicationPresentationDisableProcessSwitching    = 1 << 5;
        private const ulong NSApplicationPresentationDisableForceQuit           = 1 << 6;
        private const ulong NSApplicationPresentationDisableHideApplication     = 1 << 8;
        // private const ulong NSApplicationPresentationDisableMenuBarTransparency = 1 << 9;  // ✅ Extra lockdown
        private const ulong NSApplicationPresentationFullScreen                 = 1 << 10;

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_rect(IntPtr receiver, IntPtr selector, 
            double x, double y, double width, double height, ulong styleMask, ulong backing, bool defer);

        public static IntPtr CreateNativeOverlayWindow(double x, double y, double width, double height)
        {
            // Get NSWindow class
            IntPtr nsWindowClass = objc_getClass("NSWindow");
            IntPtr allocSel = sel_registerName("alloc");
            IntPtr initSel = sel_registerName("initWithContentRect:styleMask:backing:defer:");
            
            // Create NSWindow instance
            IntPtr window = objc_msgSend(nsWindowClass, allocSel);
            
            // Style: Borderless
            ulong styleMask = 0; // NSWindowStyleMaskBorderless
            ulong backing = 2; // NSBackingStoreBuffered
            
            // Initialize window
            window = objc_msgSend_rect(window, initSel, x, y, width, height, styleMask, backing, false);
            
            // Set window level to floating (above everything)
            objc_msgSend_int(window, sel_registerName("setLevel:"), 3); // NSFloatingWindowLevel
            
            // Make it opaque with a background color
            objc_msgSend_bool(window, sel_registerName("setOpaque:"), true);
            objc_msgSend_bool(window, sel_registerName("setBackgroundColor:"), true);
            
            // Collection behavior - can join all spaces, ignore cycle
            ulong behavior = (1UL << 0) | (1UL << 4) | (1UL << 9);
            objc_msgSend_ulong(window, sel_registerName("setCollectionBehavior:"), behavior);
            
            // Show the window
            objc_msgSend(window, sel_registerName("orderFrontRegardless"));
            
            Console.WriteLine($"✅ Native overlay window created at ({x}, {y})");
            return window;
        }

        [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr objc_getClass(string name);
        [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr sel_registerName(string name);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool value);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_int(IntPtr receiver, IntPtr selector, int value);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_ulong(IntPtr receiver, IntPtr selector, ulong value);
        

        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        private static extern void NSApplicationSetPresentationOptions(ulong options);

        [DllImport("libc")]
        private static extern int system(string command);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]

        
    private static extern void objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg);

    public static void ForceRenderOverlay(IntPtr nsWindow)
    {
        objc_msgSend(nsWindow, sel_registerName("display"));
        objc_msgSend(nsWindow, sel_registerName("displayIfNeeded"));
        objc_msgSend(nsWindow, sel_registerName("orderFrontRegardless"));
    }
        private static bool _isEnabled = false;
        private static Thread? _watchdog;

        public static void Enable(IntPtr nsWindow)
        {
            _isEnabled = true;
            Console.WriteLine("🚀 Kiosk Mode ON");

            Dispatcher.UIThread.Post(() =>
            {
                SetWindowLevel(nsWindow);
                SetCollectionBehavior(nsWindow);
            });

            // Delay fullscreen → CRITICAL for macOS
            new Thread(() =>
            {
                Thread.Sleep(500);

                Dispatcher.UIThread.Post(() =>
                {
                    if (_isEnabled)
                    {
                        ForceFullscreen(nsWindow);
                        ApplyPresentation();
                        ForceActivate();
                    }
                });
            }).Start();

            DisableGestures();
            StartWatchdog();
        }

        public static void Disable()
        {
            _isEnabled = false;
            Console.WriteLine("🔓 Kiosk Mode OFF");

            _watchdog?.Interrupt();

            IntPtr nsApp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            objc_msgSend_ulong(nsApp, sel_registerName("setPresentationOptions:"), 0);

            RestoreGestures();
            RestoreDock(); // ✅ Add this
        }

    // ✅ Add this new method
    private static void RestoreDock()
{
    new Thread(() =>
    {
        try
        {
            Run("defaults", "delete com.apple.dock autohide");
            Run("defaults", "delete com.apple.dock autohide-delay");
            Run("defaults", "delete com.apple.dock autohide-time-modifier");
            Run("defaults", "delete com.apple.dock no-bouncing");
            Run("defaults", "delete com.apple.dock show-recents");
            Run("defaults", "delete com.apple.dock show-process-indicators");
            Run("killall", "Dock");
            
            Console.WriteLine("✅ Dock settings restored");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Dock restore warning: {ex.Message}");
        }
    })
    { IsBackground = true }.Start();
}

        

        public static void MakeOverlayClickable(IntPtr nsWindow)
        {
            // Collection behavior - stay above everything but accept clicks
            ulong behavior =
                (1UL << 0) | // All Spaces
                (1UL << 4) | // CanJoinAllApplications
                (1UL << 7) | // Fullscreen support
                (1UL << 9);  // Ignore cycle
            objc_msgSend_ulong(nsWindow, sel_registerName("setCollectionBehavior:"), behavior);
            
            // ✅ CRITICAL: Accept mouse events
            objc_msgSend_bool(nsWindow, sel_registerName("setIgnoresMouseEvents:"), false);
            
            // ✅ Accept first mouse (clicks work even when window is inactive)
            objc_msgSend_bool(nsWindow, sel_registerName("setAcceptsMouseMovedEvents:"), true);
            
            // Window can become key ONLY when clicked (not automatically)
            objc_msgSend_bool(nsWindow, sel_registerName("setCanBecomeKeyWindow:"), true);
            
            Console.WriteLine("✅ Overlay configured to accept clicks");
        }

        private static void StartWatchdog()
        {
            _watchdog = new Thread(() =>
            {
                while (_isEnabled)
                {
                    Thread.Sleep(300);

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_isEnabled)
                        {
                            ApplyPresentation();
                            ForceActivate();
                        }
                    });
                }
            })
            { IsBackground = true };

            _watchdog.Start();
        }

    private const ulong HideDock = 1 << 1;
    private const ulong HideMenuBar = 1 << 2;
    private const ulong DisableProcessSwitching = 1 << 5;
    private const ulong DisableForceQuit = 1 << 3;
    private const ulong DisableSessionTermination = 1 << 4;

    
    

        private static void SetWindowLevel(IntPtr nsWindow)
        {
            objc_msgSend_int(nsWindow, sel_registerName("setLevel:"), 25);
        }

        private static void SetCollectionBehavior(IntPtr nsWindow)
        {
            ulong behavior = (1 << 7) | (1 << 0);
            objc_msgSend_ulong(nsWindow, sel_registerName("setCollectionBehavior:"), behavior);
        }

        private static void ForceFullscreen(IntPtr nsWindow)
        {
            objc_msgSend(nsWindow, sel_registerName("toggleFullScreen:"));
        }

        public static void ApplyPresentation()
        {
            IntPtr nsApp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));

            ulong options =
                NSApplicationPresentationHideDock |
                NSApplicationPresentationHideMenuBar |
                // NSApplicationPresentationDisableMenuBarTransparency | // ✅ Extra menu bar lockdown
                NSApplicationPresentationDisableProcessSwitching |
                NSApplicationPresentationDisableForceQuit |
                NSApplicationPresentationDisableHideApplication ;
                // NSApplicationPresentationFullScreen;

            objc_msgSend_ulong(nsApp, sel_registerName("setPresentationOptions:"), options);
            
            // ✅ Force Dock to completely hide via shell command (nuclear option)
            // HideDockCompletely();
        }

private static void HideDockCompletely()
{
    new Thread(() =>
    {
        try
        {
            // Step 1: Enable autohide
            Run("defaults", "write com.apple.dock autohide -bool true");
            
            // Step 2: Set EXTREME delay (effectively disables hover)
            Run("defaults", "write com.apple.dock autohide-delay -float 1000");
            
            // Step 3: Disable animations
            Run("defaults", "write com.apple.dock autohide-time-modifier -float 0");
            
            // Step 4: Prevent bouncing icons
            Run("defaults", "write com.apple.dock no-bouncing -bool true");
            
            // Step 5: Minimize all Dock features
            Run("defaults", "write com.apple.dock show-recents -bool false");
            Run("defaults", "write com.apple.dock show-process-indicators -bool false");
            
            // Step 6: Restart Dock to apply
            Run("killall", "Dock");
            
            Console.WriteLine("✅ Dock completely suppressed via defaults");
        }
        catch (Exception ex) 
        { 
            Console.WriteLine($"⚠️ Dock suppression warning: {ex.Message}"); 
        }
    })
    { IsBackground = true }.Start();
}

        public static void ForceActivate()
        {
            IntPtr nsApp = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            objc_msgSend_bool(nsApp, sel_registerName("activateIgnoringOtherApps:"), true);
        }

        

        private static void Run(string cmd, string args)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                UseShellExecute = false
            })?.WaitForExit();
        }

        private static void DisableGestures()
        {
            new Thread(() =>
            {
                Run("defaults", "write com.apple.dock mcx-expose-disabled -bool true");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerVertSwipeGesture -int 0");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerHorizSwipeGesture -int 0");
            })
            { IsBackground = true }.Start();
        }

        private static void RestoreGestures()
        {
            new Thread(() =>
            {
                Run("defaults", "write com.apple.dock mcx-expose-disabled -bool false");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerVertSwipeGesture -int 2");
                Run("defaults", "write com.apple.AppleMultitouchTrackpad TrackpadFourFingerHorizSwipeGesture -int 2");
            })
            { IsBackground = true }.Start();
        }

        public static void SetOverlayLevel(IntPtr nsWindow)
        {
            objc_msgSend_int(nsWindow, sel_registerName("setLevel:"), 25); 
            
            ulong behavior =
                (1UL << 0) |
                (1UL << 7) | 
                (1UL << 4) |
                (1UL << 9);
            objc_msgSend_ulong(nsWindow, sel_registerName("setCollectionBehavior:"), behavior);
            
            // Make window opaque
            objc_msgSend_bool(nsWindow, sel_registerName("setOpaque:"), true);
            objc_msgSend_bool(nsWindow, sel_registerName("setHasShadow:"), true);
            objc_msgSend_int(nsWindow, sel_registerName("setLevel:"), 2147483647);
            
            Console.WriteLine($"✅ Overlay level set to NSStatusWindowLevel (25)");
        }


        public static void BringOverlayToFront(IntPtr nsWindow)
        {
            // Forces window to front regardless of app focus/activation state
            objc_msgSend(nsWindow, sel_registerName("orderFrontRegardless"));
        }

        
    }
}