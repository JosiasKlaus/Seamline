# CLAUDE.md

Seamline: Windows multi-monitor alignment utility. C# / .NET 10 LTS,
WinForms, CsWin32 interop, one self-contained exe. MIT-licensed,
behavior-derived rewrite of the GPL HwMonitorAlignment.

Docs: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (how it works),
[docs/BEHAVIOR.md](docs/BEHAVIOR.md) (the behavioral contract + deliberate
deviations), [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) (build/test/release/
maintenance/backlog).

## Commands

```
dotnet build Seamline.slnx -c Release        # keep builds at ZERO warnings
dotnet test  Seamline.slnx -c Release
dotnet run --project src/App/Seamline.App.csproj -c Release
```

Headless verification on real hardware (PowerShell: pipe output, a plain `>`
captures nothing from a WinExe): `--dump`, `--dpicheck`, `--overlaycheck`,
`--move <device> <dx> <dy>`, `--align`. Run `--overlaycheck` after touching
anything DPI- or overlay-related; run `--dump` before assuming a monitor
layout — it changes between sessions.

## Invariants — do not "fix" these

- **Physical pixels everywhere.** PMv2 DPI declared in app.manifest +
  `ApplicationHighDpiMode` + runtime assert. Overlays use `AutoScaleMode.None`
  with manual physical bounds — WinForms must never rescale them, and DWM must
  never round their corners. The main window declares `AutoScaleMode.Dpi`, but
  the verified reality is: the initial scale pass runs while the form is still
  empty, so startup chrome is laid out in raw device pixels at launch DPI (the
  sizing code scales its constants by `DeviceDpi` explicitly; point-size fonts
  render DPI-scaled on their own). The framework rescales chrome only on a DPI
  *change*, upon which MainForm rebuilds the monitor cards — cards always
  self-scale from `DeviceDpi`, never both regimes on one control.
- **Interop protocol** (`src/Win32/Win32DisplayBackend.cs`): geometry read AND
  written via `ENUM_REGISTRY_SETTINGS`; `dmFields = DM_POSITION` by assignment,
  never OR; stage all monitors (`CDS_UPDATEREGISTRY|CDS_GLOBAL|CDS_NORESET`)
  then ONE `ChangeDisplaySettingsEx(NULL, NULL, NULL, 0, NULL)` commit — never
  apply monitors individually. Check every `DISP_CHANGE` result. Windows
  silently normalizes overlapping layouts while returning success — always
  re-enumerate to confirm a position stuck.
- **Sign conventions**: Up = `PositionY + 1` and the lines move UP; Left =
  `PositionX + 1` and the lines move LEFT (`local = reference − position`;
  the line follows the key). Verified on hardware; inverting these is the
  historical bug, not the fix.
- **The primary monitor never moves**; `CDS_SET_PRIMARY` is never used.
- **Event contract** (`MonitorLayout`): `LayoutChanged` = membership changes +
  post-OS-apply only — overlay/overview GEOMETRY binds to it. Pending in-memory
  edits raise `MonitorChanged` — line REPAINTS bind to that. Mixing these makes
  overlays physically move on every keypress.
- **Overlays stay white with pinned colors in every theme** (they are
  measurement surfaces). Only the main window themes. Owner-drawn controls must
  resolve their backdrop via `ModernTheme.EffectiveBackColor` (transparent
  parents blit as opaque black/white).
- **Rollback dialog is deliberately German**; „Nein" is default, accent, and
  the Esc/timeout answer.

## Licensing rule (hard)

Never copy or translate code/assets from the original GPL repository
(HwMonitorAlignment). Reading it to verify behavior is allowed. Implement from
docs/BEHAVIOR.md. New behavioral differences must be recorded there.

## Working conventions

- Zero-warning builds; fix analyzer findings properly (platform attributes,
  designer-serialization) rather than suppressing — existing suppressions
  (WFO0003, WFO5001) carry justifying comments.
- Core semantics get xUnit tests against `FakeBackend`, asserting the exact
  backend call sequence, not just outcomes.
- Hardware-verify anything touching DPI, overlays, or interop; VMs lie.
- After changing `src/Win32/NativeMethods.txt`, refresh
  `src/Win32/Generated.snapshot/` (instructions in its README).
- The user commits at feature boundaries; releases are tag-driven (`v*` tags —
  the tag is the version). Maintenance duties and the backlog are in
  docs/DEVELOPMENT.md.
