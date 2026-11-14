using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using Seamline.Core;
using Seamline.Win32;

namespace Seamline.App;

[SupportedOSPlatform("windows10.0.14393")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001 // experimental WinForms dark-mode API, accepted for M7
        Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

        // The entire coordinate model assumes physical device pixels; anything other
        // than Per-Monitor-V2 awareness silently breaks line math on mixed-DPI setups.
        Debug.Assert(DpiAwareness.IsPerMonitorV2,
            "Process is not Per-Monitor-V2 DPI aware — check app.manifest and ApplicationHighDpiMode.");

        // Dev harness: headless modes for scripts/CI and hardware bring-up.
        if (args.Contains("--dpicheck"))
            return DpiAwareness.IsPerMonitorV2 ? 0 : 2;
        IDisplayBackend backend = new Win32DisplayBackend();
        if (args.Contains("--dump"))
            return CliHarness.Dump(backend);
        if (args.Length == 4 && args[0] == "--move"
            && int.TryParse(args[2], out int dx) && int.TryParse(args[3], out int dy))
            return CliHarness.Move(backend, args[1], dx, dy);
        if (args.Contains("--align") || args.Contains("--overlaycheck"))
            return RunAlignSession(backend, verifyOnly: args.Contains("--overlaycheck"));

        // Packaging smoke test: the normal GUI launch, closed again as soon as the
        // main window has rendered its first frame. Exists to catch a published exe
        // that cannot start (v1.0.0 shipped without WPF's native libraries) - every
        // other headless flag returns before WPF loads and cannot see that.
        bool smokeTest = args.Contains("--smoketest");

        var layout = new MonitorLayout();
        layout.ResetTo(backend.EnumerateMonitors());
        if (layout.Count == 0)
        {
            if (smokeTest)
                Console.WriteLine("no monitors found"); // a modal box would hang CI
            else
                MessageBox.Show("Seamline could not find any monitors.", "Seamline",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 3;
        }
        var controller = new AlignController(backend, layout);
        // A smoke test must not touch %APPDATA% as a side effect.
        var coordinator = new AlignSessionCoordinator(controller, layout, persistSettings: !smokeTest);

        // M8: WPF-Fluent shell for the main window. The WinForms session overlays
        // and dialogs run unchanged on this same STA thread — the WPF dispatcher
        // pumps the one message loop both frameworks share.
        //
        // Required for that sharing: the WPF pump does not run WinForms keyboard
        // preprocessing for modeless forms, so without this call the overlays
        // never see ProcessCmdKey — every movement key and Escape is dead
        // (--align is unaffected and masks it: that path keeps the WinForms loop).
        System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();
        var app = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose,
        };
        WpfTheme.ApplyTo(app.Resources); // app-level: one palette every window follows
#pragma warning disable WPF0001 // experimental Fluent ThemeMode API, accepted for M8 (like WFO5001 for M7)
        app.ThemeMode = System.Windows.ThemeMode.System;
#pragma warning restore WPF0001
        var window = new MainWindow(backend, layout, controller, coordinator);
        if (smokeTest)
        {
            // ContentRendered = HWND created, layout done, first frame composed:
            // the whole managed + native WPF stack has loaded by then.
            window.ContentRendered += (_, _) => window.Close();
            // Never hang a pipeline: a window that does not render is a failure.
            var watchdog = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30),
            };
            watchdog.Tick += (_, _) => app.Shutdown(5);
            watchdog.Start();
        }
        return app.Run(window);
    }

    private static int RunAlignSession(IDisplayBackend backend, bool verifyOnly)
    {
        var layout = new MonitorLayout();
        layout.ResetTo(backend.EnumerateMonitors());
        if (layout.Count == 0)
        {
            Console.WriteLine("no monitors found");
            return 3;
        }

        // Note: nothing re-enumerates on WM_DISPLAYCHANGE here, so the controller's
        // session-invalidation machinery has no trigger in this driver (hot-plug is
        // an explicit non-goal). M4 wires re-enumeration and must handle the
        // applied-but-invalidated outcome knowingly.
        var controller = new AlignController(backend, layout);
        // A verify-only run must not touch %APPDATA% as a side effect.
        var coordinator = new AlignSessionCoordinator(controller, layout, persistSettings: !verifyOnly);
        coordinator.StartSession();

        if (verifyOnly)
        {
            // Self-asserting M3 exit criterion: every overlay must cover its monitor
            // exactly edge-to-edge in physical pixels.
            int failures = 0;
            foreach (OverlayForm overlay in coordinator.Overlays)
            {
                layout.TryGet(overlay.DeviceName, out MonitorInfo? monitor);
                var expected = new Rectangle(
                    monitor!.PositionX, monitor.PositionY, monitor.ScreenWidth, monitor.ScreenHeight);
                bool ok = overlay.Bounds == expected;
                if (!ok)
                    failures++;
                Console.WriteLine(
                    $"overlay {overlay.DeviceName} bounds={overlay.Bounds} expected={expected} " +
                    $"dpi={overlay.DeviceDpi} {(ok ? "OK" : "MISMATCH")}");
            }
            controller.AbortSession();
            return failures == 0 ? 0 : 4;
        }

        Application.Run(new AlignApplicationContext(coordinator));
        return 0;
    }
}
