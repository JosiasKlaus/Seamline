using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// UI glue for one alignment session: creates an overlay per monitor on start,
/// routes overlay input to the controller, runs the apply → rollback-dialog flow,
/// and closes every overlay when the session ends (regardless of how it ends).
/// Lifetime: exactly one coordinator per controller/layout, all app-lifetime —
/// the constructor's subscriptions are never detached (mirrors AlignController).
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class AlignSessionCoordinator
{
    private readonly AlignController _controller;
    private readonly MonitorLayout _layout;
    private readonly List<OverlayForm> _overlays = [];
    private MonitorInfo? _referenceMonitor;
    private bool _closingOverlays;

    public AlignSessionCoordinator(AlignController controller, MonitorLayout layout, bool persistSettings = true)
    {
        _controller = controller;
        _layout = layout;
        if (persistSettings)
        {
            // Applied before SessionStarted is observable, saved on every end path.
            controller.SettingsInitializer = SettingsStore.ApplyTo;
            controller.SessionEnded += SettingsStore.Save;
        }
        controller.SessionEnded += _ => CloseOverlays();
        layout.LayoutChanged += OnLayoutChanged;
        // Pending in-memory edits move lines everywhere (any monitor may be the
        // reference), so every overlay repaints on any member change.
        layout.MonitorChanged += _ => InvalidateOverlays();
    }

    /// <summary>Raised after the session ended and every overlay is closed.</summary>
    public event Action? SessionClosed;

    public IReadOnlyList<OverlayForm> Overlays => _overlays;

    internal bool IsClosingOverlays => _closingOverlays;

    /// <summary>Virtual-screen center of the current alignment reference
    /// (see AlignmentReference — resolved on overlay selection).</summary>
    internal int ReferenceCenterY => AlignmentReference.CenterY(
        _referenceMonitor ?? _layout.PrimaryMonitor ?? _layout.Monitors[0]);

    /// <summary>An overlay was clicked/focused: re-resolve the line reference for it.</summary>
    internal void NotifyOverlaySelected(MonitorInfo monitor)
    {
        if (_controller.State == AlignSessionState.Idle || !_layout.TryGet(monitor.DeviceName, out _))
            return;
        MonitorInfo resolved = AlignmentReference.Resolve(monitor, _layout.Monitors);
        if (!ReferenceEquals(resolved, _referenceMonitor))
        {
            _referenceMonitor = resolved;
            InvalidateOverlays();
        }
    }

    public void StartSession()
    {
        if (_overlays.Count > 0)
            CloseOverlays(); // defensive: no session may start over leftover windows

        _controller.StartSession();
        _referenceMonitor = _layout.PrimaryMonitor;
        foreach (MonitorInfo monitor in _layout.Monitors)
        {
            var overlay = new OverlayForm(this, monitor, _controller.Settings!);
            _overlays.Add(overlay);
            overlay.Show();
        }

        // Deterministic initial focus: the first secondary, so arrow keys work
        // immediately (the primary never moves anyway).
        OverlayForm? firstSecondary = _overlays.FirstOrDefault(o =>
            _layout.TryGet(o.DeviceName, out MonitorInfo? m) && !m.IsPrimary);
        (firstSecondary ?? _overlays.FirstOrDefault())?.Activate();
    }

    public void MoveMonitor(MonitorInfo monitor, int deltaY)
        => _controller.TryMoveBy(monitor.DeviceName, deltaY);

    /// <summary>Escape or the Close button: abort with in-memory rollback.</summary>
    public void RequestClose()
    {
        if (_controller.State == AlignSessionState.Aligning)
            _controller.AbortSession();
    }

    public void RequestReset()
    {
        if (_controller.State == AlignSessionState.Aligning)
            _controller.ResetPositions();
    }

    /// <summary>Apply → 15 s rollback dialog → confirm or full OS revert.
    /// Overlays close afterwards regardless of the answer (via SessionEnded).</summary>
    public void RequestApply()
    {
        if (_controller.State != AlignSessionState.Aligning)
            return;

        ApplyOutcome outcome = _controller.Apply();
        if (_controller.State != AlignSessionState.Confirming)
        {
            ShowApplyNotConfirming(outcome);
            return;
        }

        using var dialog = new RollbackDialog(timeoutSeconds: 15);
        DialogResult answer = dialog.ShowDialog();
        // The modal pump can dispatch a WM_CLOSE that runs controller.Shutdown()
        // (full revert) while the countdown shows — the session may already be over.
        if (_controller.State != AlignSessionState.Confirming)
            return;
        if (answer == DialogResult.Yes)
            _controller.ConfirmApplied();
        else
            _controller.RevertApplied();
    }

    /// <summary>An overlay finished closing (coordinator-driven or external, e.g. OS
    /// shutdown). Prunes the list; an externally closed overlay aborts the session so
    /// the remaining overlays and the app never end up stranded.</summary>
    internal void HandleOverlayClosed(OverlayForm overlay)
    {
        _overlays.Remove(overlay);
        if (!_closingOverlays && _controller.State == AlignSessionState.Aligning)
            _controller.AbortSession();
    }

    private void OnLayoutChanged()
    {
        // Post-apply geometry update; pending keypress edits never reach this
        // (they raise MonitorChanged only).
        foreach (OverlayForm overlay in _overlays.ToArray())
            overlay.RefreshBoundsFromMonitor();
    }

    private void InvalidateOverlays()
    {
        foreach (OverlayForm overlay in _overlays.ToArray())
            overlay.Invalidate();
    }

    private void CloseOverlays()
    {
        if (_closingOverlays)
            return;
        _closingOverlays = true;
        try
        {
            foreach (OverlayForm overlay in _overlays.ToArray())
            {
                try
                {
                    overlay.Close();
                }
                catch
                {
                    // Best effort: one failing teardown must not strand the rest.
                }
            }
        }
        finally
        {
            _overlays.Clear();
            _closingOverlays = false;
            SessionClosed?.Invoke();
        }
    }

    private void ShowApplyNotConfirming(ApplyOutcome outcome)
    {
        // A mid-apply invalidation (topology changed while committing) is not a
        // failure: the commit may have been applied and kept.
        string message = outcome.Succeeded
            ? "Die Anordnung wurde übernommen, aber die Monitorkonfiguration hat sich währenddessen geändert.\n"
              + "Bitte prüfen Sie das Ergebnis."
            : DescribeFailure(outcome);

        // Owner must be topmost, or the box opens BEHIND the fullscreen overlays.
        using var topmostOwner = new Form { TopMost = true };
        MessageBox.Show(topmostOwner, message, "Seamline", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string DescribeFailure(ApplyOutcome outcome)
    {
        var lines = new List<string> { "Die neue Anordnung konnte nicht übernommen werden." };
        foreach ((string device, DisplayChangeResult result) in outcome.Applied.StageResults)
            if (!result.Succeeded)
                lines.Add($"{device}: {result}");
        if (outcome.Applied.CommitResult is { Succeeded: false } commit)
            lines.Add($"Übernehmen: {commit}");
        if (outcome.AutoRevert is not null)
            lines.Add(outcome.AutoRevert.Succeeded
                ? "Die vorherige Konfiguration wurde wiederhergestellt."
                : "Achtung: Auch die Wiederherstellung war nicht vollständig erfolgreich.");
        return string.Join(Environment.NewLine, lines);
    }
}
