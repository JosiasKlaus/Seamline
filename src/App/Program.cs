using System.Diagnostics;
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

        Application.Run(new MainForm());
        return 0;
    }
}
