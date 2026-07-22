# Development

## Build, test, run

Requires the .NET 10 SDK.

```
dotnet build Seamline.slnx -c Release
dotnet test  Seamline.slnx -c Release
dotnet run --project src/App/Seamline.App.csproj -c Release
dotnet publish src/App/Seamline.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

Builds are kept at **zero warnings**. `Core` is fully unit-tested against a
`FakeBackend` that records the exact backend call sequence — tests assert the
literal stage/commit order, not just outcomes.

## Hidden dev/test flags

`seamline.exe` accepts headless modes (output goes to stdout — from PowerShell
use the pipe form, a plain `>` captures nothing from a windowed exe):

| Flag | Purpose |
|---|---|
| `--dump` | Print the monitor configuration as Seamline sees it (first thing to request in bug reports) |
| `--dpicheck` | Exit 0 if the process runs Per-Monitor-V2 DPI aware |
| `--overlaycheck` | Start a session, verify every overlay covers its monitor exactly (bounds; per-monitor DPI printed for inspection), abort, exit 0/4 |
| `--move <device> <dx> <dy>` | Stage+commit a move, verify, revert (e.g. `--move \\.\DISPLAY2 10 0`) |
| `--align` | Run an alignment session without the main window |

## Hardware testing

VMs lie about DPI and modesets — the interesting behavior only exists on real
monitors. The matrix that matters:

| Scenario | Verify |
|---|---|
| Mixed DPI (e.g. 125 % laptop + 100 % externals) | overlays exactly edge-to-edge; a 3 px line is 3 physical px on both; `--overlaycheck` green |
| Monitors left of / above the primary (negative coordinates) | enumeration matches Settings; move + apply + revert |
| Row without the primary / stacked pairs | row/column reference resolution; vertical lines align the stacked seam |
| Rollback countdown expiry | full OS revert; Yes-path persists across a reboot |
| 3+ monitors | control box only on the primary; atomic commit |
| Display change while the app is idle / mid-session | overview refreshes / session invalidates cleanly |

Remember the Windows constraint: monitors only slide **along shared edges**;
staged positions that would overlap are silently normalized back (with success
return codes) — re-enumerate to see what actually happened.

## Release pipeline

Pushing a tag `v*` runs `.github/workflows/release.yml`: test → publish
(single-file, self-contained, **version derived from the tag**) → SHA-256
(LF-only, `sha256sum -c` compatible) → GitHub release with both assets.
CI (`ci.yml`) builds and tests on pushes to `main` and PRs.

The exe is unsigned; SmartScreen shows its interstitial until the file hash
accrues reputation. Revisit code-signing only if AV false positives become a
real support burden.

## Maintenance

- **LTS treadmill**: the self-contained exe bundles the runtime. Retarget the
  `TargetFramework` to the next LTS (one-line change) by **November 2028** and
  rebuild, so releases keep shipping a security-patched runtime. A stale exe
  keeps running; it just stops receiving runtime fixes.
- **CsWin32**: the package is version-pinned. After changing
  `src/Win32/NativeMethods.txt`, refresh the vendored generated-code snapshot —
  instructions in `src/Win32/Generated.snapshot/README.md`. If the generator
  ever dies, delete the package reference and build from the snapshot.
- **Licensing discipline**: Seamline (MIT) is a behavior-derived rewrite of the
  GPL HwMonitorAlignment. Never copy or translate code/assets from the original
  repository; reading it to verify behavior is fine. All deliberate behavioral
  differences are recorded in [BEHAVIOR.md](BEHAVIOR.md).

## Backlog & outlook

- **WPF-Fluent shell (optional)** — WPF ships in the same in-box .NET desktop
  framework (zero new dependencies) and has an official Fluent theme since
  .NET 9: real full-bleed Mica and smooth theming for the main window, with the
  physical-pixel WinForms overlays kept as-is. ~3–5 days if ever wanted.
- **Linux port (outline)** — `Core` moves as-is (.NET is first-class on Linux).
  Needed: a `LinuxDisplayBackend` (X11/XRandR first; RandR's atomic
  multi-output apply maps cleanly onto the stage/commit protocol; Wayland has
  no cross-compositor output-positioning protocol) and a UI rebuild. The
  overlay coordinate model must be re-validated against the chosen stack's DPI
  handling — the physical-pixel guarantee is the hard requirement.
