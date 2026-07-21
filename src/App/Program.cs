using System.Diagnostics;
using System.Runtime.Versioning;
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

        // Dev harness: lets scripts/CI verify DPI awareness without showing UI.
        if (args.Contains("--dpicheck"))
            return DpiAwareness.IsPerMonitorV2 ? 0 : 2;

        Application.Run(new MainForm());
        return 0;
    }
}
