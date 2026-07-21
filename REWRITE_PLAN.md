# Seamline — Rewrite Plan

Seamline is a from-scratch, behavior-compatible rewrite of [HwMonitorAlignment](https://github.com/Chrisg2000/HwMonitorAlignment) (Python 3.9 / PySide2, GPL, abandoned). This document is the behavioral spec and implementation plan; Seamline shares no code with the original.

**Stack:** C# 13 / .NET 10 (LTS) · WinForms · CsWin32 source-generated P/Invoke
**Audience:** solo hobbyist developer, implementing with AI assistance. Windows-first.
**Status:** working document — check off items as they land.

---

## 1. Goals & Non-Goals

### Goals (priority order)
1. **Longevity.** No dependency that can rot the way PySide2 did. Total third-party surface: one Microsoft build-time source generator, which can be vendored into the repo if it ever dies. A self-contained exe keeps running on future Windows even if never rebuilt.
2. **Behavioral fidelity.** Preserve the original's observable behavior (Section 4 checklist), with a short, explicitly signed-off list of deliberate deviations (Section 8).
3. **Simple distribution.** One exe, download-and-run, no installer, no runtime install, no admin elevation.
4. **Clean licensing.** Clean-room rewrite: no code derived from the GPL-encumbered `has_properties.py`/`signals.py` (Linux Show Player lineage). Note in the repo README that *old* Python releases remain GPLv3-encumbered.

### Non-Goals
- Linux support in v1 (architecture keeps the door open — Section 9).
- Moving/changing the primary monitor, resolution/orientation changes, hot-plug monitoring. Near-original feature envelope (plus the signed-off §8 extensions).
- Horizontal alignment (vertical lines + Left/Right keys for stacked layouts) in v1 — user-requested, planned as M6 after the v1 release (see §5).
- Minimal exe size. ~60–70 MB self-contained is accepted; sanctioned shrink paths documented in Section 7 but not built.
- Native AOT. Unsupported for WinForms (COM-marshalling dependency per Microsoft's trimming-incompatibility docs). Do not plan around it appearing.

---

## 2. Stack Choice

**C# 13 / .NET 10 LTS + WinForms + CsWin32.** Under Per-Monitor-V2 DPI awareness, WinForms is the only mainstream .NET UI stack that works natively in *physical device pixels* — `Screen` bounds, form `Location/Size`, `Cursor.Position`, and GDI+ painting are 1:1 with the Win32 virtual-screen coordinates the entire domain model uses, so the original's line math and overlay placement port verbatim with no coordinate-translation layer. CsWin32 generates SDK-accurate `DEVMODEW`/`DISPLAYCONFIG_*` structs from Win32 metadata at compile time, deleting the hand-rolled ctypes-layout risk class entirely while adding zero runtime dependencies.

**Rejected:**
- **WPF** — lays out in 1/96" DIPs; on mixed-DPI setups (this tool's core audience) every coordinate needs explicit conversion and pixel-snapping fights. Exactly the bug class this tool exists to fix. Also untrimmable and heavier.
- **C++ / raw Win32** (the judges' longevity favorite) — genuinely the most rot-proof, but 2–3x UI effort, manual GDI resource lifetimes, and the worst pick-up-after-two-years story for a hobbyist. The Core/Win32 split below keeps this as a documented fallback for the App layer without rework.
- **Rust + windows-sys** — same substrate as C++ but every call is `unsafe` over a wndproc; hardest re-entry, no gain on any axis that matters here.
- **FPC/Lazarus** — best Linux path, but volunteer bus factor, LCL PMv2 rough edges, one hand-declared FFI unit (a mini-ctypes), thin AI/community support.

**Known cost, accepted:** a recompile against a new LTS every ≤3 years (edit one TFM digit) to keep a patched bundled runtime. A never-recompiled exe keeps *running* but its bundled runtime stops getting security fixes.

---

## 3. Architecture

### Solution layout

```
Seamline.slnx
├─ src/Core/    Seamline.Core.csproj    net10.0          zero deps, no Windows refs
├─ src/Win32/   Seamline.Win32.csproj   net10.0-windows  CsWin32 only
├─ src/App/     Seamline.App.csproj     net10.0-windows  WinForms exe
└─ tests/Core.Tests/                              xUnit
```

### Core (clean-room replacement for HasProperties/Signal/Memento)

- **`MonitorInfo`** — `INotifyPropertyChanged` class, 10 fields: `DeviceName` (init-only, write-once by construction — stronger than the original's default-comparison hack), `MonitorName`, `FriendlyMonitorName`, `DisplayAdapter`, `ScreenWidth`, `ScreenHeight`, `PositionX`, `PositionY`, `Orientation`, `IsPrimary`. Setters: `if (value == field) return;` (equality suppression — preserves the no-op/no-loop guarantee), then raise `PropertyChanged`. Per-property subscription = a small `WhenChanged(nameof(...), handler)` helper returning an `IDisposable`. **Drop entirely:** the weak-reference signal system (lambda-collection footgun), `MonitorSyncState` (dead weight, never read), `NestedProperties`, `register_property`, the unused exception types, `from_memento`.
- **`MonitorOrientation`** — enum 0–3 = `DMDO_DEFAULT/90/180/270` (Landscape, Portrait, FlippedLandscape, FlippedPortrait). Values must stay numerically identical to Win32.
- **`MonitorSnapshot`** — `readonly record struct` of all 10 values. `MonitorInfo.CreateSnapshot()` / `Restore(snapshot)`; restore goes *through the property setters* so `PropertyChanged` fires and the UI repaints — this is what makes rollback visually snap back. Rollback is purely in-memory; only Apply touches the OS.
- **`MonitorLayout`** (merges the original `MonitorModel` + `VScreen`) — ordered dictionary keyed by `DeviceName` (duplicate add throws `ArgumentException`); events `MonitorAdded`, `MonitorRemoved`, `Reset`, `LayoutChanged`; `PrimaryMonitor`; `MonitorsInOrder` (sort by `(PositionX, PositionY)`); `bool TryHitTest(int x, int y, out MonitorInfo m)` with inclusive edges on all four sides, first match in insertion order wins on shared edges (deliberate deviation: Try-pattern instead of `LookupError`).
- **`AlignSettings`** — shared observable settings object: `ShowCursorPosition=false`, `ShowDiagonalLines=false`, `ShowHorizontalLines=true`, `Antialiasing=false`, `ShowInfoBox=true`, `LineSpacing=100` (range 50–960), `LineThickness=3` (range 0–100). Change events invalidate every overlay. Two-way bound to control-box widgets (fixes the original's one-way binding trap).
- **`IDisplayBackend`** — the single OS seam:
  ```csharp
  IReadOnlyList<MonitorInfo> EnumerateMonitors();
  Rectangle GetVirtualScreenBounds();              // live OS query, never derived from model
  DisplayChangeResult StagePosition(string deviceName, int x, int y);
  DisplayChangeResult CommitStaged();
  ```
  The two-phase protocol is *typed into the interface* (no `reset`/`id_=None` convention to misuse). `DisplayChangeResult` carries the raw `DISP_CHANGE_*` code + name; results are surfaced, never swallowed (fixes the ignored-return-code bug).
- **`AlignController`** — session state machine: `Idle → Aligning → Applying → Confirming`. Snapshots captured at session start; `Apply` = stage-all + commit + `LayoutChanged`; Escape/Close/Reset semantics per Section 4. Clears its per-session dictionaries on **both** exit paths (fixes the original's stale-model leak after Apply).

### Win32 backend (CsWin32)

Package: `Microsoft.Windows.CsWin32`, **pinned** to an exact version (0.3.x at time of writing). It is a compile-time source generator — zero runtime footprint. **Insurance:** after M1 stabilizes, snapshot the generated interop `.cs` files into `src/Win32/Generated.snapshot/` (excluded from build) so the dependency can be deleted entirely if the project ever dies or generated identifier shapes drift.

`NativeMethods.txt`:
```
ChangeDisplaySettingsEx
EnumDisplayDevices
EnumDisplaySettings
GetDisplayConfigBufferSizes
QueryDisplayConfig
DisplayConfigGetDeviceInfo
GetSystemMetrics
GetThreadDpiAwarenessContext
```

**Enumeration** (faithful port of the original's two-pass approach):
1. **Friendly names:** `GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS)` → `QueryDisplayConfig` → per path `DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, adapterId = path.sourceInfo.adapterId, id = path.targetInfo.id)` → dict `monitorDevicePath → monitorFriendlyDeviceName`. A missing join entry degrades to empty friendly name, never crashes discovery (fixes the KeyError risk).
2. **GDI pass:** outer loop `EnumDisplayDevices(null, i, ref dd, 0)` over adapters; inner loop `EnumDisplayDevices(adapter.DeviceName, j, ref mon, EDD_GET_DEVICE_INTERFACE_NAME)` — `mon.DeviceID` is then the device-interface path, the join key. **`FALSE` return = normal end of enumeration**, not an error (fixes exception-driven termination). Filter: monitor `StateFlags & DISPLAY_DEVICE_ACTIVE`; skip `DISPLAY_DEVICE_MIRRORING_DRIVER` **checked on the adapter's flags** (deliberate fix of the original's misplaced check — document in code).
3. **Geometry:** `EnumDisplaySettings(adapter.DeviceName, ENUM_REGISTRY_SETTINGS, ref dm)` — registry settings on both read and write paths, matching the original. Add a code comment: if another tool changed the live mode without registry write, we see stale geometry — faithful, known sharp edge.
4. Field mapping: `DeviceName = adapter.DeviceName` (`\\.\DISPLAY1`, the repositioning identity), `MonitorName = mon.DeviceString`, `DisplayAdapter = adapter.DeviceString`, `ScreenWidth/Height = dmPelsWidth/Height`, `PositionX/Y = dmPosition.x/y` (signed, negatives valid), `Orientation = dmDisplayOrientation`, `IsPrimary = adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE`.

**Repositioning — the exact staged dance, with checked results:**

```csharp
public unsafe DisplayChangeResult StagePosition(string deviceName, int x, int y)
{
    DEVMODEW dm = default;
    dm.dmSize = (ushort)sizeof(DEVMODEW);
    PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_REGISTRY_SETTINGS, ref dm);
    dm.Anonymous1.Anonymous2.dmPosition = new POINTL { x = x, y = y };  // signed; negatives fine
    dm.dmFields = DEVMODE_FIELD_FLAGS.DM_POSITION;   // ASSIGNMENT, not OR — only position is declared valid
    var r = PInvoke.ChangeDisplaySettingsEx(deviceName, &dm, HWND.Null,
        CDS_TYPE.CDS_UPDATEREGISTRY | CDS_TYPE.CDS_GLOBAL | CDS_TYPE.CDS_NORESET, null);
    return Map(r);
}

public DisplayChangeResult CommitStaged()
    => Map(PInvoke.ChangeDisplaySettingsEx((PCWSTR)null, null, HWND.Null, 0, null)); // NULL/NULL commit
```

Rules carried over verbatim: primary monitor is never moved; `CDS_SET_PRIMARY` never used; coordinates written as-is (Windows keeps primary at origin); `hwnd`/`lParam` always NULL. **M1 decision (resolved 2026-07):** `CDS_GLOBAL` kept, faithful to the original — verified working on real hardware; revisit only if failures are reported on locked-down machines (return codes are checked, so they'd be visible).

**Sharp edge (observed in M1):** Windows silently *normalizes* staged layouts that would overlap or disconnect monitors — every `ChangeDisplaySettingsEx` call still returns `DISP_CHANGE_SUCCESSFUL`, but the position is snapped back. Alignment moves slide along shared edges, so normal use is unaffected; still, never interpret success codes as proof the exact position was applied — re-enumerate to confirm (the `--move` harness does).

Virtual screen: `GetSystemMetrics(SM_XVIRTUALSCREEN=76, SM_YVIRTUALSCREEN=77, SM_CXVIRTUALSCREEN=78, SM_CYVIRTUALSCREEN=79)` — always live, never derived from the model. Between edit and apply, model (pending) and virtual-screen metrics (actual) intentionally disagree.

### Per-monitor DPI (the make-or-break requirement)

The original had *zero* DPI code and worked only because Qt's platform plugin declared awareness. We declare it explicitly, three ways (belt and braces):

1. `app.manifest`: `<dpiAware>true/pm</dpiAware>` + `<dpiAwareness>PerMonitorV2</dpiAwareness>` — authoritative, earliest-possible declaration.
2. csproj: `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` → generated `ApplicationConfiguration.Initialize()`.
3. `Debug.Assert` that `GetThreadDpiAwarenessContext()` is PMv2 before any window is created.

**Overlay strategy that makes DPI a non-issue:** `OverlayForm` uses `AutoScaleMode.None`, `StartPosition = Manual`, and `SetBounds(mon.PositionX, mon.PositionY, mon.ScreenWidth, mon.ScreenHeight)` before `Show()` — WinForms' PMv2 rescaling machinery never touches it, and a borderless window exactly covering one monitor never straddles a DPI boundary, so `WM_DPICHANGED` needs no handling on the drawing surface. Only child panels (info box, control box) opt into font scaling via `Form.DeviceDpi`.

### UI layer (WinForms)

- **MainForm** — `FormBorderStyle.FixedSingle`, `MaximizeBox = false`, title "Seamline". Top to bottom: `OverviewPanel`, a "Monitor Setup Information" group with one `MonitorInfoPanel` per monitor in `(x, y)` order, buttons Close / Adjust / About.
- **OverviewPanel** — double-buffered, owner-drawn, non-interactive. Each paint computes the fit transform **from scratch** (fixes the original's stateful multiplicative-scale convergence): scale = `min(fit)` with 10 px margin, centered, aspect-preserved, monitors at true virtual-screen coordinates. Per monitor: `LinearGradientBrush` top-right RGB(15,125,193) → bottom-left RGB(119,196,122); 20-unit flat-cap border, drawn correctly inset on all four edges (fixes the original's bottom-edge `+offset` bug); centered white Arial monitor index (device name minus literal `\\.\DISPLAY` prefix; empty on mismatch), font auto-scaled to fill the rect. Repaints on `LayoutChanged`.
- **OverlayForm** (one per monitor) — `FormBorderStyle.None`, `BackColor = White`, `ShowInTaskbar = false`, `TopMost = true` (**deliberate deviation** — original had stay-on-top commented out; trivially revertible, see Section 8). Owner-drawn in `OnPaint` from current state (no cached-glob pattern):
  - Diagonal layer (hidden by default): two 1 px black lines, `(0,0)→(w,h)` and `(w,0)→(0,h)`.
  - Horizontal layer (visible by default): black pen, width = `LineThickness`. Reference heights from the **primary** monitor: `mid = primaryHeight/2`, `over = mid − LineSpacing`, `under = mid + LineSpacing`; local `y = reference − monitor.PositionY`. Mid line spans full width; over/under lines are stubs of length `ScreenWidth/5` from left and right edges (4 stubs).
  - Antialiasing toggle maps to `Graphics.SmoothingMode`.
- **Info box** (every overlay) / **Control box** (primary only) — plain child `Panel`s with a fake title bar made draggable via `WM_NCHITTEST → HTCAPTION`. Info box titled "Display Information"; centered on non-primary monitors; on the primary, info box + control box centered as a unit, control box to the right, vertically centered. Control box titled "Control Box": form rows in order — Show cursor position, Show diagonal lines, Show horizontal lines, Antialiasing, Line spacing (50–960), Line thickness (0–100), Show info box — plus vertical Apply / Close / Reset buttons.
- **Input** — selection = OS focus (click an overlay). `ProcessCmdKey`: Up/Down = `PositionY ± 1`, PageUp/PageDown = `± 10` (**sign convention: Up INCREASES PositionY and the lines move up on screen** — copy exactly, do not "fix"), all movement keys ignored on the primary, Escape aborts session + in-memory rollback, everything else unhandled. `OnMouseWheel` swallowed. `OnMouseMove` with cursor-position enabled: `ToolTip.Show($"{Cursor.Position.X}, {Cursor.Position.Y}")` — physical global coordinates.
- **RollbackDialog** — custom fixed-size topmost `Form`, no close button, warning icon, the original German strings: title `"Änderung übernehmen?"`, body `"Ihre Desktop-Konfiguration hat sich geändert.\nMöchten Sie diese Änderung beibehalten?"`, informative line `"Rücksetzung in {n} Sekunden"`. Yes/No, **No is default**. `System.Windows.Forms.Timer` at 1000 ms counting down from **15**; at zero, programmatically invoke No.
- **AboutDialog** — fixed size, modal; icon (embedded resource, 125×125), heading with name+version, project-website link, a third-party-components list (now just ".NET / MIT" and "CsWin32 / MIT"), Close button. Drop "About Qt".
- **Resources** — icon and version info embedded via csproj properties; all assets as embedded resources (fixes CWD-relative `hwmonitor/res/` fragility).

---

## 4. Behavioral Spec Checklist

Port these exactly. Items marked ⚠ are the non-obvious ones AI assistants will "helpfully" get wrong.

### Model & data
- [x] 10 monitor fields: DeviceName (write-once, unique key), MonitorName, FriendlyMonitorName, DisplayAdapter, ScreenWidth, ScreenHeight, PositionX, PositionY, Orientation, IsPrimary.
- [x] Orientation enum values 0/1/2/3 numerically = `DMDO_DEFAULT/90/180/270`.
- [x] Positions are physical-pixel virtual-screen coordinates; primary top-left = (0,0); negatives valid and written verbatim.
- [x] Setting a property to its current value is a silent no-op (equality suppression).
- [x] Every effective change raises change notification; snapshot restore goes through setters so the UI repaints.
- [x] Duplicate `DeviceName` add throws; layout ordering = sort by `(PositionX, PositionY)`.
- [x] ⚠ Hit-test: inclusive on **all four** edges; shared-edge point resolves to first monitor in insertion order.
- [x] Virtual-screen offset/size always queried live via `GetSystemMetrics(76/77/78/79)`, never computed from the model.

### Interop
- [x] Enumeration: `EnumDisplayDevices(NULL, i, 0)` adapters → `EnumDisplayDevices(name, j, EDD_GET_DEVICE_INTERFACE_NAME)` monitors; include only ACTIVE; skip mirroring drivers (adapter flags); FALSE = end, not error; cap loops at 1000.
- [x] Friendly names: `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` + `DisplayConfigGetDeviceInfo(GET_TARGET_NAME)`, joined on `monitorDevicePath == DISPLAY_DEVICE.DeviceID`.
- [x] ⚠ Geometry from `ENUM_REGISTRY_SETTINGS (-2)` on **both** read and write paths.
- [x] ⚠ Stage: read registry DEVMODE, set `dmPosition = (x,y)`, `dmFields = DM_POSITION` (**assignment, not OR** — OR-ing re-applies stale mode fields), call with `CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_NORESET` (0x10000009), hwnd/lParam NULL.
- [x] Commit: single `ChangeDisplaySettingsEx(NULL, NULL, NULL, 0, NULL)` applies all staged monitors in one atomic modeset. **Never apply one-at-a-time non-deferred** — transient invalid layouts get rejected by Windows.
- [x] Primary never moved; `CDS_SET_PRIMARY` never used. *(AlignController.TryMoveBy refuses the primary, the CLI harness refuses `--move` on it, and CDS_SET_PRIMARY is never used.)*
- [x] `DISP_CHANGE_SUCCESSFUL == 0` checked on every stage and the commit (deviation: original ignored them).

### Alignment session
- [ ] Adjust opens one fullscreen borderless overlay per monitor at `(PositionX, PositionY)` size `(ScreenWidth, ScreenHeight)`, white background, no taskbar entry.
- [ ] Layer z-order and defaults: diagonals z1 hidden, horizontal lines z2 visible, info box z3 visible, control box z4; checkboxes toggle across **all** overlays simultaneously.
- [ ] Line math: refs from primary height — `mid = h/2`, `over = mid − spacing`, `under = mid + spacing`; local y = ref − PositionY; mid full-width; over/under stubs of `width/5` from each edge; defaults spacing **100**, thickness **3**.
- [ ] ⚠ Keys: Up `+1`, Down `−1`, PageUp `+10`, PageDown `−10` on PositionY; **Up increases PositionY yet moves lines up on screen** (because local y = ref − PositionY). Ignored on primary. Escape = abort + in-memory rollback. Wheel swallowed. Other keys no-ops.
- [x] Session snapshots captured at session start; Escape/Close/Reset all restore from those snapshots **in memory only** — no OS write.
- [ ] Reset keeps overlays open; Close/Escape end the session.
- [ ] Apply: stage every monitor → commit → `LayoutChanged` (overview + overlay geometry update) → modal rollback dialog.
- [ ] Rollback dialog: 15 s countdown, 1000 ms tick, Yes/No, **No default**, no close button, topmost, auto-No at zero. On No/timeout: restore every snapshot, re-stage, re-commit (full OS revert). Overlays close afterwards regardless of answer.
- [ ] Cursor-position tooltip shows `"{globalX}, {globalY}"` in physical virtual-screen coordinates.
- [ ] App shutdown while a session is live = abort + in-memory rollback (controller stop on main-form close).

### Main window & dialogs
- [x] Fixed-size main window, minimize+close only; overview (10 px margin fit, gradient, index labels, non-interactive, live-updates on `LayoutChanged`); per-monitor info boxes in `(x, y)` order; Close/Adjust/About.
- [x] Info form rows in order: DEVICE NAME, MONITOR NAME, FRIENDLY MONITOR NAME, DISPLAY ADAPTER, SCREEN RESOLUTION (`WxH`), VIRTUAL SCREEN POSITION (`(x, y)`), ORIENTATION (Landscape / Portrait / Landscape (flipped) / Portrait (flipped)), IS PRIMARY — labels above bold values, live-updating.
- [x] Windowed exe (no console), no admin elevation, embedded icon.

---

## 5. Milestones

Ordered so the riskiest interop is validated first. Each has a testable exit criterion.

**M0 — Skeleton (½ d).** Solution + 3 projects + test project; CI on `windows-latest` (build + test); PMv2 manifest + csproj setting + debug assert in place from day one.
*Exit:* CI green; a blank form runs and the DPI-awareness assert passes on a scaled display.

**M1 — Interop spike (2–3 d). 90% of the technical risk lives here.**
CsWin32 pinned; enumeration + friendly-name join; `StagePosition`/`CommitStaged`; a `--dump` CLI mode printing the full enumeration and a `--move <device> <dx> <dy>` test mode.
*Exit:* on real hardware, `--dump` matches Settings app data (names, positions incl. negatives, primary flag, orientation); `--move` shifts a secondary monitor ±10 px and reverts, with DISP_CHANGE codes logged as 0. **Decide here:** keep or drop `CDS_GLOBAL`. **Then:** snapshot generated interop files into the repo.

**M2 — Core model + tests (1–2 d).**
`MonitorInfo`, `MonitorSnapshot`, `MonitorLayout`, `AlignSettings`, `AlignController` state machine; xUnit against a `FakeBackend` that records calls.
*Exit:* tests cover equality-suppressed notification, snapshot/restore-fires-events, duplicate-key throw, ordering, inclusive-edge hit-test, and the exact stage-all-then-commit call sequence (including the rollback re-apply sequence). CI green.

**M3 — Overlays + DPI validation (3–4 d). The hardest milestone.**
`OverlayForm` rendering (all layers, exact line math), keyboard/mouse handling, shared-settings live propagation, **RollbackDialog built early in this milestone** so every experiment on real hardware is auto-reverting and safe.
*Exit:* on a physical mixed-DPI setup (100% + 150%, not a VM): each overlay covers its monitor exactly edge-to-edge; a 3 px line is 3 physical pixels on both monitors; the mid line is visually continuous across the bezel when monitors are aligned; Up/PageUp move lines up; Escape restores everything.

**M4 — Main window + dialogs + wiring (2–3 d).**
OverviewPanel, info panels, control box, About dialog, full session wiring through `AlignController`, apply → rollback-dialog flow against the real backend.
*Exit:* complete end-to-end run on real hardware: adjust, apply, let the 15 s countdown expire → layout reverts via the OS; run again, answer Yes → layout persists across a reboot (registry write verified).

**M6 — Horizontal alignment for stacked layouts (post-v1, user-requested).**
Vertical lines (mid/left/right columns from a reference monitor's center X, mirroring the row logic as columns), Left/Right = `PositionX ± 1`, Ctrl-free key mapping decided then; `TryMoveBy` generalized to 2D deltas; column-reference resolution analogous to `AlignmentReference`. Windows constraint (verified in M1): stacked monitors slide along shared horizontal edges, so X-moves are exactly the legal degree of freedom in stacked layouts.
*Exit:* two stacked monitors mutually aligned horizontally on real hardware, applied and persisted.
*Design notes (implemented):* Left/Right = ±1 px, Shift+Left/Right = ±10 px; the lines follow the key on both axes. Row and column references resolve independently per selection — in L-shaped layouts the horizontal and vertical line sets may anchor to different neighbors, which is intentional (each axis judges only its own seam). When the selected screen has no column neighbor, the vertical set falls back to the primary's column (same fallback the row axis has always had); the offscreen hint fires whenever no enabled line reaches a panel.

**M7 — Modern look & feel (post-v1, user-requested).**
Restyle away from the faithful-original look toward a Windows 11 / WinUI-like appearance — while STAYING on WinForms (the framework choice is a longevity decision; this is styling, not a rewrite): dark/light mode via WinForms' `Application.SetColorMode` (experimental since .NET 9), DWM rounded corners where applicable, Segoe UI Variable, flat accent-styled buttons/checkboxes, restyled group boxes and overview, refreshed overlay boxes and rollback dialog. Behavioral spec unchanged — §4 stays the contract; only §3's visual details are superseded.
*Exit:* user sign-off on the new look on real hardware, light and dark, 100% and 125% DPI.

**M5 — Packaging + release (1–2 d).**
Publish profile (`-r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`), icon/version resources, README (incl. SmartScreen "More info → Run anyway" note and old-releases GPL note), GitHub Actions release workflow producing the exe + SHA-256, run the manual test matrix (Section 6).
*Exit:* a downloaded release exe runs on a clean Windows 11 machine with no .NET installed, no admin, and passes the smoke matrix.

---

## 6. Testing Strategy

**Automated (CI, every push):** Core has no UI or OS dependency — xUnit on `windows-latest` covers all model semantics and the stage/commit sequencing via `FakeBackend`. The Win32 project compiles in CI (validates the CsWin32 generation) but its behavior is hardware-tested.

**Manual hardware harness:** the `--dump`/`--move` CLI flags from M1 stay in the shipped exe (hidden). `--dump` output is the first thing to request in any future bug report.

**Manual test matrix (M3/M5, on real hardware — VMs lie about DPI and modesets):**

| Scenario | Verify |
|---|---|
| 2 monitors, both 100% | baseline: overlay coverage, line continuity, apply/revert |
| Mixed DPI: 100% + 150% (e.g. laptop + external) | overlays exact; 3 px = 3 physical px on both; tooltip coords match between monitors; info-panel fonts legible on both |
| 3 monitors | control box only on primary; per-overlay focus/keys; commit is atomic |
| Monitor left of / above primary (negative coords) | enumeration shows negatives; move + apply + revert works |
| Portrait monitor | orientation label; width/height correct; line math sane |
| Rollback countdown expiry | full OS revert; also verify Yes-path persists across reboot |
| Overlay vs. other windows | TopMost behavior acceptable; Alt-Tab doesn't strand a hidden overlay |
| DPI change mid-session (move scaling slider) | app doesn't crash; documented as "restart the session" if visuals shift |

---

## 7. Distribution & Packaging

- **Artifact:** one self-contained single-file exe — measured 49.4 MB compressed vs 111 MB uncompressed, with launch time ~1.0 s for both (M5 measurement on real hardware; compression kept — smaller *and* no slower, since less is read from disk). Windows 10 1607+ / Windows 11 x64; `win-arm64` is one extra publish line. No installer, no runtime install, no admin (`ChangeDisplaySettingsEx` position changes need none — no UAC manifest entry).
- **Release:** GitHub Actions workflow: tag → build → publish exe + SHA-256 as release assets.
- **SmartScreen:** unsigned exe triggers the interstitial; ship unsigned with documented "More info → Run anyway" + published hash (reputation accrues per file hash). Code-signing cert (~€200–400/yr) is disproportionate; revisit only if AV false-positives become a real support burden.
- **Size escape hatches (documented, not built):** (a) multi-target the same source to .NET Framework 4.8.1 → ~1 MB exe, zero runtime distribution, at the cost of C# 7.3-era constraints; (b) port only the App layer to raw Win32 + CsWin32, which is Native-AOT-clean (~3–6 MB). Both reuse Core/Win32 unchanged — that's why the split exists.

---

## 8. Deliberate Improvements (each is a signed-off deviation — no silent scope creep)

1. **Explicit PMv2 DPI awareness** (original relied on Qt's plugin as a side effect). *The* modernization.
2. **DISP_CHANGE results surfaced**: stage/commit failures shown to the user with code + name instead of silently swallowed.
3. **Overlays TopMost** (original had stay-on-top commented out). Behavior change vs. shipped original; one-line revert if it annoys in practice.
4. **Mirroring-driver filter moved to adapter flags** (fixes a misplaced check that happened not to matter).
5. **Hit-test Try-pattern** instead of exception-driven control flow; enumeration ends on FALSE instead of a raised error.
6. **Two-way control-box binding** (Reset/rollback refreshes widgets — closes the original's one-way trap).
7. **Session cleanup on both exit paths** (fixes stale mementos/models after Apply).
8. **Overview transform computed from scratch each paint**; border-rect bottom-edge bug fixed.
9. **Embedded resources** instead of CWD-relative paths.
10. **Enumeration robustness** (from M1 review): `QueryDisplayConfig` retried on `ERROR_INSUFFICIENT_BUFFER`; friendly-name join case-insensitive (device-interface paths are case-insensitive identifiers); one `MonitorInfo` per adapter so GDI clone mode can't produce duplicate `DeviceName` keys; a device without a readable registry mode skips that adapter's remaining monitors instead of aborting discovery (original semantics).
11. **Pending-edit vs applied-layout events split** (from M2 review): the original fired `layout_changed` only after an OS apply. `MonitorLayout.LayoutChanged` keeps that meaning (membership changes + post-apply notify); per-monitor in-memory edits raise `MonitorChanged` instead. Overlay/overview *geometry* must bind to `LayoutChanged` only, so windows never move on mere keypresses; line *repaints* bind to `MonitorChanged`.
12. **Apply failure policy** (original ignored all `DISP_CHANGE` results and always showed the dialog): on any stage/commit failure the rollback dialog is skipped, the registry is auto-restored from the session snapshots (best effort — the revert commit is skipped if a re-stage fails, surfacing residue in `ApplyOutcome.AutoRevert`), and the session stays in Aligning with the user's edits intact.
13. **Defensive session invalidation + shutdown**: any layout membership change during a session (hot-plug re-enumeration) restores known monitor instances in memory and ends the session with no OS writes; sessions are generation-tagged so re-entrant teardown can never end one twice. A membership change arriving while Apply is mid-backend-call (`ChangeDisplaySettingsEx` dispatches `WM_DISPLAYCHANGE` synchronously) is deferred and processed when the backend returns — the session ends instead of entering Confirming. `AlignController.Shutdown()` is a no-throw teardown: no-op when Idle, in-memory abort when Aligning, full OS revert when Confirming (the dialog's default answer); in `RevertApplied` the OS revert is staged from the snapshots *before* the in-memory restore so a throwing UI observer cannot skip it.
14. **M3 UI deltas** (each deliberate, from the M3 review): line coordinates are integer physical pixels (original used float subpixel positions — invisible on even-height primaries, and integer is the more honest unit for GDI+); rollback-dialog buttons say "Ja"/"Nein" (the original showed untranslated English "Yes"/"No" because no Qt translator was installed — German matches the body text); when antialiasing is on, `PixelOffsetMode.Half` keeps odd-width lines exactly N physical pixels instead of blurring one row wider; the mouse wheel still adjusts a focused control-box numeric field (the original suppressed wheel everywhere); cursor tooltip is offset (+16,+16) and auto-hides after 1.5 s; the first secondary overlay gets deterministic initial focus so arrow keys work immediately; an externally closed overlay (Alt+F4) aborts the session instead of orphaning it. From hardware feedback: the focused overlay shows a top-center "Selected" badge and an accented info-box title bar (a first-iteration edge frame sat exactly where line continuity is judged at the bezels and was replaced), and a monitor whose reference lines all fall outside its panel shows a gray explanatory hint instead of silent white.
15. **Row-following alignment reference** (user-requested extension beyond the original's envelope): the line reference resolves when an overlay is selected — the primary whenever it shares the selected screen's row (which reproduces the original exactly), otherwise the nearest static row neighbor, with the primary as fallback when the selected screen has no row neighbor. This lets rows that don't contain the primary (e.g. two screens stacked above it) be mutually aligned. The reference is always a monitor that stays still during the adjustment, preserving the original's feedback (lines move on the screen being moved).
16. **M4 deltas** (from the M4 review): Adjust is disabled while a session is active — fixes the original's double-start overlay leak; the main window re-enumerates on `DisplaySettingsChanged` while Idle (sessions stay frozen; changes arriving mid-session are caught up after it ends) — the trigger the §8.13 invalidation machinery needed; the About dialog's 125×125 icon and project link land with the M5 resources.
17. **Small config persistence** (only if it costs <½ day in M4): remember `AlignSettings` in `%APPDATA%\Seamline\settings.json` via `System.Text.Json`. Everything else — localization, horizontal alignment, hot-plug, tray icon — is explicitly *out*.

Not carried over (dead weight): sync-state machinery, `from_memento`, `NestedProperties`, unused exception types, unused `GetMonitorInfo`/`EnumDisplayMonitors` bindings, the three-mode signal-connection enum.

---

## 9. Linux Outlook (brief)

Not a recompile — a bounded port, deliberately deferred. `Core` is pure `net10.0` with zero Windows references and moves as-is (.NET is first-class on Linux); it holds all the fiddly semantics. A later port needs: (1) `LinuxDisplayBackend : IDisplayBackend` — X11/XRandR first (RandR's atomic multi-output apply maps cleanly onto Stage/Commit); Wayland has no cross-compositor output-positioning protocol, so realistically per-compositor adapters later; (2) a UI rewrite of MainForm/OverlayForm in Avalonia. Estimated 1–2 additional weeks when wanted. Deliberately **not** starting on Avalonia today: it works in scaled logical units, which would reintroduce the mixed-DPI coordinate-translation problem PMv2 WinForms eliminates.

---

## 10. Top Risks & Mitigations

| # | Risk | Mitigation |
|---|---|---|
| 1 | **Mixed-DPI rendering wrong anyway** (WinForms PMv2 quirks, e.g. anchored-control drift) | Overlays use `AutoScaleMode.None` + absolute physical bounds so the rescaling machinery never touches them; M3 exit criterion runs on real 100%+150% hardware, not a VM; only child panels use DPI scaling. |
| 2 | **CsWin32 pre-1.0 churn** (generated identifier shapes like `Anonymous1.Anonymous2.dmPosition` can shift) | Pin exact package version; after M1, snapshot generated files into the repo — worst case, delete the dependency and build from the snapshot forever. |
| 3 | **Behavioral drift during AI-assisted porting** (sign conventions, `dmFields` assignment-not-OR, registry-vs-current settings, inclusive hit-test) | Section 4 checklist is the contract; the ⚠ items get dedicated unit tests in M2; deviations only via the Section 8 list. |
| 4 | **`ChangeDisplaySettingsEx` failures on real machines** (`CDS_GLOBAL` on locked-down boxes, odd drivers) | Return codes checked and shown (new); `CDS_GLOBAL` keep/drop decided in M1 on evidence; rollback dialog guarantees a bad apply auto-reverts in 15 s. |
| 5 | **Stale registry geometry** (another tool changed live mode without registry write) | Faithful to original by design; code comment at the read site; possible future toggle to `ENUM_CURRENT_SETTINGS` — not in v1. |
| 6 | **SmartScreen/AV friction** on an unsigned single-file exe | README run-anyway instructions + published SHA-256; signing deferred until it's a demonstrated problem. |
| 7 | **LTS treadmill forgotten** | Calendar reminder + README note: retarget/recompile by Nov 2028 (one TFM digit). A stale exe still *runs*; it just stops receiving runtime security fixes. |
| 8 | **Exe size complaints** | Accepted trade; documented escape hatches (net48 multi-target ~1 MB, or raw-Win32 App layer + AOT ~3–6 MB) reuse Core/Win32 unchanged. |
| 9 | **TopMost overlays interact badly with other apps** (deviation #3) | Single-line revert to non-topmost; decide from real usage in M3/M4. |
| 10 | **Old-release licensing confusion** | README states: this rewrite is clean-room (MIT or similar); pre-rewrite Python binaries remain GPLv3-encumbered and their obligations stand. |