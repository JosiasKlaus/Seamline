# Architecture

Seamline is a Windows multi-monitor alignment utility: C# on .NET 10 (LTS),
WinForms, and [CsWin32](https://github.com/microsoft/CsWin32) source-generated
interop, shipped as one self-contained exe. This document describes how it works
inside. The exact user-visible behavior is specified in [BEHAVIOR.md](BEHAVIOR.md);
build/release/maintenance topics live in [DEVELOPMENT.md](DEVELOPMENT.md).

## Why this stack

Longevity is the project's #1 goal — Seamline replaces a Python/PySide2 tool that
died of dependency rot. Every piece of the stack is Microsoft-shipped and in-box:
the runtime, the UI framework, and the interop generator (a build-time-only
dependency whose generated output is additionally snapshotted into the repo).
WinForms specifically was chosen because, under Per-Monitor-V2 DPI awareness, it
is the only mainstream .NET UI stack that works natively in **physical device
pixels** — `Screen` bounds, form bounds, cursor position, and GDI+ painting map
1:1 onto the Win32 virtual-screen coordinates the whole domain model uses. WPF
and WinUI lay out in scaled logical units, which is exactly the coordinate
translation this tool exists to avoid.

## Solution layout

```
Seamline.slnx
├─ src/Core/    net10.0          zero dependencies, no Windows references
├─ src/Win32/   net10.0-windows  CsWin32 interop only
├─ src/App/     net10.0-windows  WinForms exe (seamline.exe)
└─ tests/Core.Tests/             xUnit against a FakeBackend
```

`Core` holds every fiddly semantic and is fully unit-tested; `Win32` implements
the single OS seam; `App` is UI glue. A future Linux port (or UI-stack swap)
replaces only the outer layers.

## Core

- **`MonitorInfo`** — observable monitor state (10 fields; `DeviceName` is the
  write-once identity, e.g. `\\.\DISPLAY1`). Property setters are
  equality-suppressed; snapshot restore goes **through the setters** so the UI
  repaints on rollback.
- **`MonitorSnapshot`** — immutable capture used for session rollback. Rollback
  is purely in-memory; only Apply touches the OS.
- **`MonitorLayout`** — the monitor set, keyed by device name, insertion-ordered.
  **Event contract:** `LayoutChanged` fires on membership changes and — via
  `NotifyLayoutChanged` — after an OS-level apply; a member's pending in-memory
  edit raises `MonitorChanged` only. Overlay/overview *geometry* binds to
  `LayoutChanged`, never to `MonitorChanged` (otherwise overlays would physically
  move on every keypress).
- **`AlignController`** — the session state machine
  (`Idle → Aligning → Applying → Confirming`). The participant set and snapshots
  freeze at session start. It is hardened against re-entrancy: a session
  generation counter makes teardown idempotent, layout membership changes
  mid-session invalidate the session (in-memory restore, no OS writes), an
  invalidation arriving while the backend runs (ChangeDisplaySettingsEx
  dispatches `WM_DISPLAYCHANGE` synchronously) is deferred and processed after
  the call returns, and `Shutdown()` is a no-throw teardown for app close
  (aborts while aligning; full OS revert while the countdown shows).
- **`AlignmentReference`** — resolves which monitor anchors the alignment lines
  for a selected screen: the primary whenever it shares the selected screen's
  row/column, otherwise the nearest static neighbor. Rows (vertical overlap)
  anchor the horizontal lines; columns (horizontal overlap) anchor the vertical
  lines used for stacked layouts. The reference is always a monitor that stays
  still during the adjustment, so the lines move on the screen being moved.
- **`LineGeometry`** — pure line math in physical pixels, local to each overlay:
  `local = referenceCenter − monitorPosition`. Mid line spans the full
  width/height; the two offset lines (± spacing) are stubs of a fifth of the
  span, anchored at the edges.
- **`IDisplayBackend`** — the OS seam, with the two-phase repositioning protocol
  typed into the interface: `StagePosition(...)` per monitor, then one
  `CommitStaged()`. Results carry the raw `DISP_CHANGE_*` codes and are always
  surfaced.

## Win32

CsWin32 (pinned version) generates SDK-accurate structs and P/Invoke signatures
from `NativeMethods.txt` at build time. As rot insurance, the generated sources
are snapshotted into `src/Win32/Generated.snapshot/` (excluded from the build;
refresh instructions in that folder's README) — if the generator ever dies, the
snapshot builds the project forever.

**Enumeration** is a faithful port of the original tool's two-pass approach:

1. `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` + `DisplayConfigGetDeviceInfo`
   build a device-path → friendly-name map (case-insensitive join; failures
   degrade to empty names, never break discovery).
2. `EnumDisplayDevices` enumerates adapters, then each adapter's monitors with
   `EDD_GET_DEVICE_INTERFACE_NAME` (the join key). A `FALSE` return is the
   normal end of enumeration; mirroring pseudo-adapters are filtered on the
   adapter's flags; one entry per adapter (GDI clone mode cannot produce
   duplicate keys); a device without a readable registry mode skips that
   adapter's remaining monitors instead of aborting discovery.
3. Geometry comes from `EnumDisplaySettings(ENUM_REGISTRY_SETTINGS)` — the
   registry settings, on **both** the read and the write path.

**Repositioning** is the staged dance, verified on hardware:

```
per monitor: EnumDisplaySettings(REGISTRY) → dmPosition = (x, y)
             dmFields = DM_POSITION            // assignment, NOT or-ing
             ChangeDisplaySettingsEx(dev, &dm, NULL,
                 CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_NORESET, NULL)
finally:     ChangeDisplaySettingsEx(NULL, NULL, NULL, 0, NULL)   // atomic commit
```

Monitors are never applied one at a time (transient invalid layouts get
rejected); the primary is never moved and `CDS_SET_PRIMARY` is never used.

**Sharp edges** (observed on real hardware):

- Windows **silently normalizes** staged layouts that would overlap or
  disconnect monitors — every call still returns `DISP_CHANGE_SUCCESSFUL`.
  Success codes are never proof a position was applied; re-enumerate to confirm.
- Registry-settings reads see stale geometry if another tool changed the live
  mode without a registry write. Faithful to the original by design.

Small DWM/UX helpers also live here: corner-preference control (overlays are
explicitly never rounded on Windows 11 — they must cover their monitor to the
exact corner pixel), Mica backdrop (best-effort), the Windows accent color, and
the Per-Monitor-V2 awareness check backing the startup assert.

## App

**Per-monitor DPI has two regimes.** The main window declares WinForms
`AutoScaleMode.Dpi`, whose initial pass runs while the form is still empty — so
startup chrome is laid out in raw device pixels at the launch DPI (the sizing
code scales its constants by `DeviceDpi` explicitly, and point-size fonts render
DPI-scaled on their own); the framework rescales the chrome only on a live DPI
change, upon which the monitor cards are rebuilt — cards always self-scale from
`DeviceDpi`. The overlays opt out entirely: `AutoScaleMode.None`, manual bounds
set to the monitor's physical rectangle before `Show()` — WinForms' rescaling
machinery never touches the drawing surface, and a borderless window exactly
covering one monitor never straddles a DPI boundary. PMv2 awareness is declared
three ways: app.manifest, `ApplicationHighDpiMode`, and a runtime assert.

**Main-window layout.** The resizable window is one `TableLayoutPanel` with a
percent row for the overview (absorbs all extra height) above autosize rows for
the section label, the card grid and the buttons. `MonitorCardGrid` lays the
monitor cards out manually — columns from the available width (270–480 logical
px per card), cards stretched to share the row at equal heights — because
TableLayoutPanel's autosize negotiation with self-sizing children is not
deterministic under live resize. Sharp edge (empirically verified): a child that
sets its own `Height` *during* its parent's layout pass is silently dropped —
WinForms marks the mid-layout container deferred and never re-runs it, so the
autosize row keeps the stale height until an unrelated event. The grid heals
this by posting a follow-up `PerformLayout`; posted messages run before
`WM_PAINT`, so the stale intermediate layout is never painted. Launch size and
the content-derived minimum size are computed in `OnLoad` from `DeviceDpi` and
the measured card wrap.

**Alignment session UI.** `AlignSessionCoordinator` creates one `OverlayForm`
per monitor, routes keys to the controller, runs Apply → rollback-countdown →
confirm/revert, and closes every overlay when the session ends — however it
ends (Escape, Close, Alt+F4 on an overlay, invalidation, app shutdown).
Selection = OS focus; the focused overlay shows a badge and re-resolves the
row/column line references. Overlay surfaces stay white with pinned colors in
every theme: they are measurement surfaces, not chrome.

**Theming (main window only).** `ModernTheme` provides Fluent/Win11 styling on
plain WinForms: system dark/light with live switching, the user's Windows accent
color (gated to a usable luminance band, with a foreground variant for
links/focus in dark mode), Segoe UI Variable, owner-drawn rounded buttons
(`FluentButton`) and cards (`CardPanel`/`MonitorCard`), and a Win11-display-
settings-style overview. Owner-drawn controls resolve their backdrop via the
nearest *opaque* ancestor color — `Color.Transparent` parents blit as opaque
black/white in GDI+ double buffers.
