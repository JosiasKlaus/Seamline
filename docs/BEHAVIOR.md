# Behavioral contract

Seamline is a from-scratch, behavior-compatible rewrite of
[HwMonitorAlignment](https://github.com/Chrisg2000/HwMonitorAlignment) (GPL).
It shares no code or assets with the original; its behavior was derived from a
written specification and verified on real hardware. This document records that
contract — what the app guarantees, and where it deliberately differs from the
original. Changes to anything below are breaking changes and should be
deliberate, documented decisions.

## Model & data

- Positions are physical-pixel virtual-screen coordinates; the primary
  monitor's top-left corner is (0,0) and negative values are valid and written
  verbatim.
- A monitor's identity is its Win32 adapter device name (`\\.\DISPLAY1`);
  duplicate names cannot enter the layout.
- Orientation values are numerically identical to Win32
  `DMDO_DEFAULT/90/180/270` (Landscape, Portrait, Landscape (flipped),
  Portrait (flipped)).
- Setting a property to its current value is a silent no-op; every effective
  change raises change notification; snapshot restore fires notifications so
  the UI visibly snaps back.
- Hit-testing is inclusive on all four edges; a point on a shared edge
  resolves to the first monitor in insertion order.
- The virtual-screen rectangle is always queried live from the OS
  (`GetSystemMetrics`), never derived from the model.

## Interop protocol

- Geometry is read **and** written via registry display settings
  (`ENUM_REGISTRY_SETTINGS`), matching the original. Known sharp edge: a tool
  that changes the live mode without a registry write leaves stale geometry.
- Staging sets `dmFields = DM_POSITION` by **assignment** (or-ing would
  re-apply stale mode fields) and uses
  `CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_NORESET`; one final
  `ChangeDisplaySettingsEx(NULL, NULL, NULL, 0, NULL)` applies all staged
  monitors in a single atomic modeset. Monitors are never applied one at a
  time. The primary is never moved; `CDS_SET_PRIMARY` is never used.
- Every `DISP_CHANGE_*` result is checked and surfaced. Windows silently
  normalizes overlap-creating layouts while returning success — the app never
  treats success codes as proof of position and re-reads the configuration.

## Alignment session

- **Adjust** opens one fullscreen, borderless, white, always-on-top overlay per
  monitor at its exact physical rectangle (no taskbar entry, excluded from
  Alt-Tab, never DWM-rounded).
- Line layers and defaults: diagonals (hidden), horizontal lines (visible),
  vertical lines (hidden; for stacked layouts), info box (visible), control box
  (primary overlay only). Toggles act on all overlays simultaneously.
- Line math: reference monitor center ± spacing (defaults: spacing 100,
  thickness 3); mid line spans the full width/height, the offset lines are
  edge-anchored stubs of a fifth of the span. The reference follows the
  selected screen: the primary when it shares the row/column, else the nearest
  static neighbor. A screen none of whose lines are visible shows an
  explanatory hint instead of silent white.
- **Keys** (selection = OS focus; movement keys are refused for the primary
  and pass through to its control-box fields):
  - <kbd>↑</kbd>/<kbd>↓</kbd>: ±1 px vertical; <kbd>PageUp</kbd>/<kbd>PageDown</kbd>: ±10 px.
    Sign convention: Up *increases* `PositionY` and the lines move **up** on
    screen (`local = reference − position`) — the line follows the key.
  - <kbd>←</kbd>/<kbd>→</kbd>: ±1 px horizontal (<kbd>Shift</kbd> for 10 px); Left increases
    `PositionX`, lines move **left** — same convention.
  - Modifier keys don't block the vertical keys (Ctrl+Up still moves);
    <kbd>Esc</kbd> aborts the session; the mouse wheel is swallowed.
- Session snapshots are captured at start. Escape/Close/Reset restore them
  **in memory only** — no OS write. Reset keeps the session open;
  Escape/Close end it. Closing an overlay window directly (Alt+F4) aborts the
  session instead of orphaning it.
- **Apply**: stage every session monitor → single commit → views refresh → the
  rollback dialog. On any stage/commit failure the registry is auto-restored
  from the snapshots (best effort), the rollback countdown is skipped in favor
  of a warning describing the failure, and the session stays open with the
  user's edits intact.
- **Rollback dialog**: German strings (deliberate), 15-second countdown,
  Yes/No with **No as the default** (and the accent button), Esc = No, no
  close button, auto-No at zero. No/timeout performs a full OS revert
  (restore snapshots → re-stage → re-commit). Overlays close afterwards
  regardless of the answer.
- App shutdown during a session: no-throw teardown — in-memory abort while
  aligning, full OS revert while the countdown is showing.
- Optional cursor-position tooltip shows global physical coordinates.
- Session settings are recreated with defaults per session, then overlaid with
  the persisted values from `%APPDATA%\Seamline\settings.json` (best-effort).

## Main window

- Overview miniature of the virtual layout (non-interactive, live-updating on
  membership changes and applies; primary tile in the accent color) and one
  live info card per monitor in (x, y) order showing: device name, monitor
  name, friendly name, adapter, resolution, virtual position, orientation,
  primary flag.
- The window is resizable (maximize included). Extra height goes to the
  overview; the monitor cards re-wrap into as many equal-width columns as fit
  and stretch to share the row. The launch size is derived from the content
  (up to three card columns); the minimum size always keeps the full card
  wrap, the buttons and an overview strip visible.
- Adjust is disabled while a session is active. Closing the window triggers
  the shutdown teardown above.
- While idle, the window re-enumerates on display-settings changes; changes
  arriving mid-session are caught up after the session ends. Sessions
  themselves run on a frozen monitor set — a membership change mid-session
  invalidates the session (in-memory restore, no OS writes).

## Deliberate deviations from HwMonitorAlignment

Faithfulness was the default; every difference below was an explicit decision.

**Fixes of original defects**
- `DISP_CHANGE` results are checked and surfaced (original ignored them).
- Enumeration ends on a `FALSE` return instead of exception-driven
  termination; the mirroring-driver filter checks the adapter's flags.
- Session state is cleaned up on every exit path (original leaked stale
  models after Apply); Adjust is disabled during a session (original could
  double-start and leak overlay sets).
- Overview fit transform is computed from scratch each paint; two-way
  control-box binding; embedded resources instead of CWD-relative paths.

**Robustness additions**
- Enumeration degrades instead of aborting (unreadable registry modes, clone
  mode, missing friendly names); `QueryDisplayConfig` retries on
  `ERROR_INSUFFICIENT_BUFFER`.
- Apply-failure policy (original had none): auto-restore the registry, skip
  the dialog, keep the session open.
- Mid-session topology changes invalidate the session safely; a no-throw
  shutdown path exists for app close.

**Extensions beyond the original's envelope**
- Row-following line reference: rows that don't contain the primary (e.g. two
  screens stacked above it) can be mutually aligned.
- Horizontal alignment for stacked layouts: vertical lines from a column
  reference, Left/Right keys (Shift for 10 px).
- Settings persistence across runs; Idle-time re-enumeration on display
  changes; explicit Per-Monitor-V2 DPI awareness (the original relied on Qt's
  plugin as a side effect); overlays are always-on-top (original had this
  commented out) and excluded from Alt-Tab.
- Selection badge + accented info-box title on the focused overlay; offscreen
  hint text (original showed silent white).

**Visual redesign (main window only)**
- Fluent/Windows 11 styling replaces the original look: the main window and
  About dialog are WPF with the in-box Fluent theme — system dark/light mode
  with live switching, Windows accent color (luminance-gated, Seamline-blue
  fallback), display-settings-style overview tiles, monitor cards instead of
  group boxes. The window is resizable with a responsive card grid.
  The overlay session visuals intentionally stay close to the original's
  functional white surfaces (and stay WinForms, in physical pixels).
- German rollback-dialog captions „Ja"/„Nein" (the original showed untranslated
  English buttons next to its German body text).
