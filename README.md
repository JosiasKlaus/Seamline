# Seamline

Align your physical multi-monitor setup with Windows' virtual screen layout — pixel by pixel.

Windows lets you drag monitors around in the display settings, but getting the virtual arrangement to *exactly* match how your monitors sit on the desk is nearly impossible by eye. The result: the mouse cursor and windows "jump" vertically when crossing between screens. Seamline fixes this by drawing reference lines across all monitors; you nudge each screen's position with the arrow keys (1 px) or PageUp/PageDown (10 px) until the lines meet seamlessly across the bezels, then apply the new layout to Windows — with an automatic 15-second rollback in case something goes wrong.

## Status

In development, not yet usable. See [REWRITE_PLAN.md](REWRITE_PLAN.md) for the architecture and roadmap.

- Target platform: Windows 10/11
- Stack: C# / .NET 10 (LTS), WinForms, CsWin32
- Ships as a single self-contained exe — no installer, no runtime to install, no admin rights

## Acknowledgements

Seamline is a from-scratch, behavior-compatible rewrite of [HwMonitorAlignment](https://github.com/Chrisg2000/HwMonitorAlignment) by Chrisg2000 — a great tool whose Python 3.9 / PySide2 foundation made it increasingly hard to keep running. Seamline replicates its behavior but shares no code with it; the original project remains available under its own GPL license.

## License

[MIT](LICENSE)
