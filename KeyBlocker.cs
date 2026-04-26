using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace CGPShell;

public static class KeyBlocker
{
    // =========================
    // Native Imports
    // =========================

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, int encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopRun();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr tap, int order);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        CGEventTapCallBack callback,
        IntPtr userInfo);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern ulong CGEventGetIntegerValueField(IntPtr evnt, int field);

    // =========================
    // Types
    // =========================

    private delegate IntPtr CGEventTapCallBack(IntPtr proxy, uint type, IntPtr evnt, IntPtr userInfo);

    private static readonly CGEventTapCallBack _callback = OnEvent;

    private static IntPtr _eventTap = IntPtr.Zero;
    private static Thread? _thread;
    private static bool _running = false;

    private static readonly HashSet<ulong> BlockedKeys = new()
    {
        53,  // ESC
        122, // F1
        120, // F2
        99,  // F3
        118, // F4
        36,  // Enter
        48   // Tab
    };

    // =========================
    // START
    // =========================

    public static void Start()
    {
        if (_running) return;

        _running = true;

        _thread = new Thread(() =>
        {
            try
            {
                ulong mask =
                    (1UL << 10) | // key down
                    (1UL << 11);  // key up

                _eventTap = CGEventTapCreate(
                    0, // HID event tap
                    0, // head insert
                    0,
                    mask,
                    _callback,
                    IntPtr.Zero
                );

                if (_eventTap == IntPtr.Zero)
                {
                    Console.WriteLine("❌ Event tap failed (check Accessibility permissions)");
                    _running = false;
                    return;
                }

                Console.WriteLine("✅ Event tap created");

                IntPtr runLoopSource = CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
                IntPtr mode = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", 0);

                CFRunLoopAddSource(CFRunLoopGetCurrent(), runLoopSource, mode);

                CGEventTapEnable(_eventTap, true);

                Console.WriteLine("🎯 KeyBlocker running...");

                // 🔥 THIS MUST BLOCK THREAD (correct behavior)
                CFRunLoopRun();

                Console.WriteLine("⚠️ RunLoop exited (unexpected)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ KeyBlocker crash: {ex.Message}");
            }
        });

        _thread.IsBackground = false; // IMPORTANT: do NOT use background thread
        _thread.Start();
    }

    // =========================
    // STOP
    // =========================

    public static void Stop()
    {
        Console.WriteLine("🛑 Stopping KeyBlocker...");

        _running = false;

        try
        {
            if (_eventTap != IntPtr.Zero)
            {
                CGEventTapEnable(_eventTap, false);
                _eventTap = IntPtr.Zero;
            }
        }
        catch { }
    }

    // =========================
    // CALLBACK
    // =========================

    private static IntPtr OnEvent(IntPtr proxy, uint type, IntPtr evnt, IntPtr userInfo)
{
    // ⚠️ CRITICAL: Recovery Logic
    // If type is 14, the system disabled your tap because of 'spamming'
    if (type == 14) 
    {
        Console.WriteLine("⚠️ Tap Timed Out. Re-enabling...");
        CGEventTapEnable(_eventTap, true); // This fixes the '2nd/3rd time failure'
        return evnt;
    }

    if (type == 10 || type == 11) // KeyDown or KeyUp
    {
        try
        {
            ulong keyCode = CGEventGetIntegerValueField(evnt, 9);
            
            // 🛑 REMOVE ALL CONSOLE.WRITELINE FROM HERE
            // Writing to the console is SLOW. It is the #1 reason 
            // why the tap fails after 3 clicks.

            if (BlockedKeys.Contains(keyCode)) return IntPtr.Zero;

            ulong flags = CGEventGetIntegerValueField(evnt, 7);
            bool isCmd = (flags & 0x00100000) != 0;
            bool isFn = (flags & 0x00800000) != 0;

            // Block Cmd+Q or Fn+Q
            if ((isCmd || isFn) && keyCode == 12) return IntPtr.Zero;
        }
        catch { }
    }
    return evnt;
}
}