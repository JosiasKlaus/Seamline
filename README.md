# Seamline

Align your physical multi-monitor setup with Windows' virtual screen layout — pixel by pixel.

Windows lets you drag monitors around in the display settings, but getting the virtual arrangement to *exactly* match how your monitors sit on the desk is nearly impossible by eye. The result: the mouse cursor and windows "jump" vertically when crossing between screens. Seamline fixes this by drawing reference lines across your monitors; you nudge each screen's position with the arrow keys (1 px) or PageUp/PageDown (10 px) until the lines meet seamlessly across the bezels, then apply the new layout to Windows — with an automatic 15-second rollback in case something goes wrong.

- Target platform: Windows 10 (1607+) / Windows 11, x64 (runs on Windows-on-ARM via the built-in x64 emulation)
- One self-contained exe — no installer, no .NET to install, no admin rights
- Per-Monitor-V2 DPI aware: works correctly on mixed-scaling setups (e.g. a 125% laptop next to 100% externals)

## Usage

1. Download `seamline.exe` from the Releases page and run it.
   - *SmartScreen note:* the exe is not code-signed, so Windows may show "Windows protected your PC" — click **More info → Run anyway**. Compare the published SHA-256 hash if you want to verify the download.
2. Make sure all monitors you want to align are connected and visible in the overview.
3. Click **Adjust**. White overlays with alignment lines appear on your screens.
4. Click the screen you want to move (a "Selected" badge marks it), then nudge it with <kbd>↑</kbd>/<kbd>↓</kbd> (1 px) or <kbd>PageUp</kbd>/<kbd>PageDown</kbd> (10 px) until the lines continue seamlessly across the bezel.
   - The lines anchor to the primary monitor's row, or — when you select a screen in a different row — to its nearest row neighbor.
5. Click **Apply**. A dialog with a 15-second countdown appears; confirm to keep the new layout, or let it expire to automatically restore the previous one.
   - The confirmation dialog is currently German (faithful to the original tool): **Ja** keeps the layout, **Nein** or the timeout reverts it.
6. Enjoy windows and your cursor no longer jumping at monitor borders.

The primary monitor never moves (everything aligns relative to it), and <kbd>Esc</kbd> always exits the alignment session without changing anything.

## Reporting issues

Please attach a dump of your monitor configuration exactly as Seamline sees it. From the folder containing `seamline.exe`, run:

- **cmd:** `seamline.exe --dump > dump.txt`
- **PowerShell:** `.\seamline.exe --dump | Out-File dump.txt`

(In PowerShell, a plain `>` redirect does not capture output from a windowed exe — it leaves `dump.txt` empty, so use the pipe form above.)

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```
dotnet build Seamline.slnx -c Release        # build
dotnet run --project src/App/Seamline.App.csproj -c Release   # run from source
dotnet test  Seamline.slnx -c Release        # run the test suite
dotnet publish src/App/Seamline.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

Maintenance note: the self-contained exe bundles the .NET runtime, so releases should be rebuilt against the then-current LTS every ~3 years (next: retarget from .NET 10 by November 2028 — a one-line `TargetFramework` change) to keep shipping a security-patched runtime. A stale exe keeps *running*; it just stops receiving runtime fixes.

See [REWRITE_PLAN.md](REWRITE_PLAN.md) for the architecture, behavioral spec, and roadmap (upcoming: horizontal alignment for stacked layouts, a modernized look).

## Acknowledgements

Seamline is a from-scratch, behavior-compatible rewrite of [HwMonitorAlignment](https://github.com/Chrisg2000/HwMonitorAlignment) by Chrisg2000 — a great tool whose Python 3.9 / PySide2 foundation made it increasingly hard to keep running. Seamline replicates its behavior but shares no code with it; the original project and its releases remain under their own GPL license.

## License

[MIT](LICENSE)
