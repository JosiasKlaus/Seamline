<p align="center">
  <!-- BRANDING: swap for docs/logo.png (wide logo + wordmark) when the new branding lands -->
  <img src="src/App/Resources/seamline-256.png" width="96" alt="Seamline icon">
</p>

<h1 align="center">Seamline</h1>

<p align="center">
  Align your physical multi-monitor setup with Windows' virtual screen layout — pixel by pixel.
</p>

<p align="center">
  <a href="https://github.com/JosiasKlaus/Seamline/releases/latest"><img src="https://img.shields.io/github/v/release/JosiasKlaus/Seamline?label=download&color=0f7dc1" alt="Latest release"></a>
  <a href="https://github.com/JosiasKlaus/Seamline/actions/workflows/ci.yml"><img src="https://github.com/JosiasKlaus/Seamline/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/JosiasKlaus/Seamline/releases"><img src="https://img.shields.io/github/downloads/JosiasKlaus/Seamline/total" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/.NET-10%20LTS-512bd4" alt=".NET 10">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/JosiasKlaus/Seamline" alt="MIT license"></a>
</p>

---

Windows lets you drag monitors around in the display settings, but getting the virtual arrangement to *exactly* match how your monitors sit on the desk is nearly impossible by eye. The result: the mouse cursor and windows "jump" when crossing between screens.

Seamline fixes this by drawing reference lines across your monitors. You nudge each screen's position with the keyboard until the lines continue seamlessly across the bezels, then apply the layout to Windows — with an automatic 15-second rollback in case something goes wrong.

<!-- SCREENSHOT (hero): docs/alignment-photo.jpg — a photo of two monitors where the
     alignment line continues across the physical bezel; photos beat screenshots here
     because the bezel is the whole point. Uncomment when the file exists:
<p align="center">
  <img src="docs/alignment-photo.jpg" width="720" alt="Alignment lines continuing across the bezel of two monitors">
</p>
-->

## How it works

<!-- SCREENSHOT: docs/main-window.png — the main window (dark mode looks best) -->
<!-- SCREENSHOT: docs/overlay-session.png — full-desktop capture (Win+Shift+S) of an
     alignment session: white overlays, lines, info box and control box visible -->

1. **Adjust** opens a white overlay with alignment lines on every screen.
2. Click the screen you want to move — a "Selected" badge marks it. The lines anchor to the primary monitor's row, or to the nearest neighbor when you select a screen in a different row.
3. Nudge with <kbd>↑</kbd>/<kbd>↓</kbd> (1 px) or <kbd>PageUp</kbd>/<kbd>PageDown</kbd> (10 px) until the lines meet across the bezel.
4. For screens stacked **above/below** each other, enable **vertical lines** in the Control Box and nudge with <kbd>←</kbd>/<kbd>→</kbd> (hold <kbd>Shift</kbd> for 10 px).
5. **Apply**. A 15-second countdown appears — confirm to keep the layout, or let it expire to restore the previous one automatically. *(The dialog is currently German, faithful to the original tool: „Ja" keeps the layout, „Nein" or the timeout reverts.)*
6. Windows and your cursor stop jumping at monitor borders.

The primary monitor never moves (everything aligns relative to it), and <kbd>Esc</kbd> always exits without changing anything.

## Download & run

Grab `seamline.exe` from the [latest release](https://github.com/JosiasKlaus/Seamline/releases/latest) and run it — one self-contained exe, no installer, no .NET to install, no admin rights. Windows 10 (1607+) / Windows 11, x64 (runs on Windows-on-ARM via the built-in emulation). Per-Monitor-V2 DPI aware, so mixed-scaling setups (e.g. a 125 % laptop next to 100 % externals) work correctly.

> **SmartScreen note:** the exe is not code-signed, so Windows may show "Windows protected your PC" — click **More info → Run anyway**. Each release ships a SHA-256 checksum you can verify against.

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

See [REWRITE_PLAN.md](REWRITE_PLAN.md) for the architecture, behavioral spec, and roadmap.

## Acknowledgements

Seamline is a from-scratch, behavior-compatible rewrite of [HwMonitorAlignment](https://github.com/Chrisg2000/HwMonitorAlignment) by Chrisg2000 — a great tool whose Python 3.9 / PySide2 foundation made it increasingly hard to keep running. Seamline replicates its behavior but shares no code with it; the original project and its releases remain under their own GPL license.

## License

[MIT](LICENSE)
