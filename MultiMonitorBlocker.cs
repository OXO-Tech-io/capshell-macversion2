using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using System;
using System.Collections.Generic;

namespace CGPShell
{
    public static class MultiMonitorBlocker
    {
        private static readonly List<Window> _blockerWindows = new();

        public static void BlockSecondaryScreens(Window mainWindow)
        {
            try
            {
                var screens = mainWindow.Screens.All;
                Console.WriteLine($"🖥️ Total screens detected: {screens.Count}");

                if (screens.Count <= 1)
                {
                    Console.WriteLine("✅ Single monitor — no blocking needed");
                    return;
                }

                // Get primary screen bounds to skip it
                var primary = mainWindow.Screens.Primary;

                foreach (var screen in screens)
                {
                    // Skip primary screen
                    if (screen.IsPrimary) continue;

                    Console.WriteLine($"🚫 Blocking secondary screen at {screen.Bounds}");
                    CreateBlockerWindow(screen);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MultiMonitorBlocker error: {ex.Message}");
            }
        }

        private static void CreateBlockerWindow(Avalonia.Platform.Screen screen)
        {
            var blocker = new Window
            {
                WindowState = WindowState.Normal,
                SystemDecorations = SystemDecorations.None,
                CanResize = false,
                Topmost = true,
                ShowInTaskbar = false,
                Background = Brushes.Black,
                Title = "CGPShell Blocker",
                // Size to match the secondary screen exactly
                Width = screen.Bounds.Width / screen.Scaling,
                Height = screen.Bounds.Height / screen.Scaling,
            };

            // Position exactly on the secondary screen
            blocker.Position = new Avalonia.PixelPoint(
                screen.Bounds.X,
                screen.Bounds.Y
            );

            // Content — warning message
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 20,
            };

            var icon = new TextBlock
            {
                Text = "🖥️",
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var title = new TextBlock
            {
                Text = "Secondary Display Blocked",
                FontSize = 28,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var subtitle = new TextBlock
            {
                Text = "This display is disabled during the exam.\nPlease use the primary screen only.",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
            };

            // Red warning bar at bottom
            var warningBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(180, 30, 30)),
                Padding = new Avalonia.Thickness(20, 10),
                CornerRadius = new Avalonia.CornerRadius(8),
                Child = new TextBlock
                {
                    Text = "⚠️  Unauthorized display usage will be flagged",
                    FontSize = 13,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                }
            };

            panel.Children.Add(icon);
            panel.Children.Add(title);
            panel.Children.Add(subtitle);
            panel.Children.Add(warningBar);

            blocker.Content = panel;

            // Prevent closing
            blocker.Closing += (s, e) => e.Cancel = true;

            blocker.Show();
            _blockerWindows.Add(blocker);

            Console.WriteLine($"✅ Blocker window shown on secondary screen");
        }

        public static void UnblockAllScreens()
        {
            foreach (var w in _blockerWindows)
            {
                try
                {
                    w.Closing -= null;
                    w.Closing += (s, e) => e.Cancel = false;
                    w.Close();
                }
                catch { }
            }
            _blockerWindows.Clear();
            Console.WriteLine("✅ All secondary screens unblocked");
        }
    }
}