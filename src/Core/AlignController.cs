namespace Seamline.Core;

/// <summary>
/// Alignment-session state machine: Idle → Aligning → Applying → Confirming.
/// The participant set and their snapshots are frozen at session start; Escape/Close/
/// Reset restore them purely in memory — only Apply (and the post-apply revert)
/// touch the OS. Any layout membership change (hot-plug re-enumeration) during a
/// session invalidates it: in-memory restore, session ends, no OS writes. A change
/// arriving while Apply is mid-backend-call (ChangeDisplaySettingsEx dispatches
/// WM_DISPLAYCHANGE synchronously) is deferred and processed when the backend returns.
/// Lifetime: exactly one controller per layout, both living for the app's lifetime —
/// the constructor's event subscriptions are never detached.
/// </summary>
public sealed class AlignController
{
    private readonly IDisplayBackend _backend;
    private readonly MonitorLayout _layout;
    private readonly List<(MonitorInfo Monitor, MonitorSnapshot Snapshot)> _session = [];

    // Identifies the live session; bumped when one ends. Teardown paths capture it
    // and EndSession no-ops on mismatch, so a re-entrant teardown (an observer
    // mutating the layout mid-restore) can neither end a session twice nor end a
    // successor session it never belonged to.
    private int _generation;
    private bool _invalidatePending;
    private bool _invalidating;

    public AlignController(IDisplayBackend backend, MonitorLayout layout)
    {
        _backend = backend;
        _layout = layout;
        layout.MonitorAdded += _ => InvalidateSession();
        layout.MonitorRemoved += _ => InvalidateSession();
        layout.Reset += InvalidateSession;
    }

    public AlignSessionState State { get; private set; } = AlignSessionState.Idle;

    /// <summary>Session-scoped settings; created fresh with defaults on StartSession
    /// (matching the original), null while Idle.</summary>
    public AlignSettings? Settings { get; private set; }

    public event Action? SessionStarted;

    /// <summary>Raised exactly once per session, after teardown, with the ended
    /// session's settings (Settings is already null when this fires).</summary>
    public event Action<AlignSettings>? SessionEnded;

    public void StartSession()
    {
        if (State != AlignSessionState.Idle)
            throw new InvalidOperationException($"Cannot start a session while {State}.");
        foreach (MonitorInfo monitor in _layout.Monitors)
            _session.Add((monitor, monitor.CreateSnapshot()));
        Settings = new AlignSettings();
        _invalidatePending = false;
        State = AlignSessionState.Aligning;
        SessionStarted?.Invoke();
    }

    /// <summary>
    /// Moves a monitor's Y position by a delta. The UI maps keys to deltas
    /// (Up=+1, Down=−1, PageUp=+10, PageDown=−10 — Up increases PositionY yet moves
    /// the drawn lines up on screen; copy exactly, do not "fix").
    /// Refused for the primary monitor, outside an aligning session, and for
    /// monitors that are not part of the frozen session set.
    /// </summary>
    public bool TryMoveBy(string deviceName, int deltaY)
    {
        if (State != AlignSessionState.Aligning)
            return false;
        MonitorInfo? monitor = _session.FirstOrDefault(e => e.Monitor.DeviceName == deviceName).Monitor;
        if (monitor is null || monitor.IsPrimary)
            return false;
        monitor.PositionY += deltaY;
        return true;
    }

    /// <summary>Reset button: restore session snapshots in memory; the session stays open.</summary>
    public void ResetPositions()
    {
        if (State != AlignSessionState.Aligning)
            throw new InvalidOperationException($"Cannot reset while {State}.");
        RestoreSnapshots();
    }

    /// <summary>Escape/Close: restore in memory and end the session. No OS write.
    /// The session ends even when a change observer throws mid-restore.</summary>
    public void AbortSession()
    {
        if (State != AlignSessionState.Aligning)
            throw new InvalidOperationException($"Cannot abort while {State}.");
        int generation = _generation;
        try
        {
            RestoreSnapshots();
        }
        finally
        {
            EndSession(generation);
        }
    }

    /// <summary>
    /// Stages every session monitor's current position, then commits once (atomic
    /// modeset). On success the session moves to Confirming (the UI shows the rollback
    /// countdown). On any failure the registry is restored from the session snapshots
    /// (best effort — the revert commit is skipped if a re-stage fails) and the session
    /// stays in Aligning with the user's edits intact; no rollback dialog is shown.
    /// If the layout was invalidated while the backend ran, the session ends instead
    /// of entering Confirming; the caller should re-enumerate to resync the model.
    /// </summary>
    public ApplyOutcome Apply()
    {
        if (State != AlignSessionState.Aligning)
            throw new InvalidOperationException($"Cannot apply while {State}.");
        int generation = _generation;
        State = AlignSessionState.Applying;

        StageCommitOutcome applied;
        try
        {
            applied = StageAllAndCommit(e => (e.Monitor.PositionX, e.Monitor.PositionY));
        }
        catch
        {
            if (!ProcessPendingInvalidation(generation))
            {
                // A stage may already have written the registry before the throw;
                // best-effort restore, surfacing only the primary exception.
                try { StageAllAndCommit(e => (e.Snapshot.PositionX, e.Snapshot.PositionY)); }
                catch { }
                State = AlignSessionState.Aligning;
            }
            throw;
        }

        if (ProcessPendingInvalidation(generation))
            return new ApplyOutcome(applied, AutoRevert: null);

        if (applied.Succeeded)
        {
            // State first: a throwing LayoutChanged observer must not strand the
            // machine between the successful OS commit and Confirming.
            State = AlignSessionState.Confirming;
            _layout.NotifyLayoutChanged();
            return new ApplyOutcome(applied, AutoRevert: null);
        }

        StageCommitOutcome autoRevert;
        try
        {
            autoRevert = StageAllAndCommit(e => (e.Snapshot.PositionX, e.Snapshot.PositionY));
        }
        finally
        {
            State = AlignSessionState.Aligning;
        }
        return new ApplyOutcome(applied, autoRevert);
    }

    /// <summary>Rollback dialog answered Yes: keep the applied layout, end the session.</summary>
    public void ConfirmApplied()
    {
        if (State != AlignSessionState.Confirming)
            throw new InvalidOperationException($"Cannot confirm while {State}.");
        EndSession(_generation);
    }

    /// <summary>Rollback dialog answered No or timed out: full OS revert, then in-memory
    /// restore, then end the session. The OS revert is staged from the snapshots first,
    /// so neither a failing backend nor a throwing change observer can skip it; the
    /// session ends in every case and the outcome surfaces what happened.</summary>
    public StageCommitOutcome RevertApplied()
    {
        if (State != AlignSessionState.Confirming)
            throw new InvalidOperationException($"Cannot revert while {State}.");
        int generation = _generation;
        try
        {
            StageCommitOutcome reverted = StageAllAndCommit(e => (e.Snapshot.PositionX, e.Snapshot.PositionY));
            RestoreSnapshots();
            _layout.NotifyLayoutChanged();
            return reverted;
        }
        finally
        {
            EndSession(generation);
        }
    }

    /// <summary>
    /// No-throw teardown for app shutdown: no-op while Idle, aborts an aligning
    /// session (in-memory rollback), and reverts an applied-but-unconfirmed layout
    /// (the rollback dialog's default answer is No).
    /// </summary>
    public void Shutdown()
    {
        try
        {
            switch (State)
            {
                case AlignSessionState.Aligning:
                    AbortSession();
                    break;
                case AlignSessionState.Confirming:
                    RevertApplied();
                    break;
                // Idle: nothing to do. Applying only exists inside Apply's own frame.
            }
        }
        catch
        {
            // Best effort — the process is going away; a throwing backend or
            // observer must not block shutdown.
        }
    }

    private StageCommitOutcome StageAllAndCommit(
        Func<(MonitorInfo Monitor, MonitorSnapshot Snapshot), (int X, int Y)> position)
    {
        var stageResults = new List<(string, DisplayChangeResult)>();
        bool allStaged = true;
        foreach ((MonitorInfo Monitor, MonitorSnapshot Snapshot) entry in _session.ToArray())
        {
            (int x, int y) = position(entry);
            DisplayChangeResult result = _backend.StagePosition(entry.Monitor.DeviceName, x, y);
            stageResults.Add((entry.Monitor.DeviceName, result));
            allStaged &= result.Succeeded;
        }
        // Committing after a failed stage would apply a half-staged layout, and a
        // commit with nothing staged is a stray OS write — skip both.
        DisplayChangeResult? commit = allStaged && stageResults.Count > 0 ? _backend.CommitStaged() : null;
        return new StageCommitOutcome(stageResults, commit);
    }

    private void RestoreSnapshots()
    {
        // Restore only instances still present in the layout — never stomp a fresh
        // MonitorInfo that re-enumeration created under the same device name.
        foreach ((MonitorInfo monitor, MonitorSnapshot snapshot) in _session.ToArray())
        {
            if (_layout.TryGet(monitor.DeviceName, out MonitorInfo? current)
                && ReferenceEquals(current, monitor))
                monitor.Restore(snapshot);
        }
    }

    /// <summary>Layout membership changed mid-session (hot-plug): restore what we still
    /// know in memory and end the session. No OS writes — the snapshots describe a
    /// topology that no longer exists. While Apply is mid-backend-call the teardown is
    /// deferred to Apply via a flag.</summary>
    private void InvalidateSession()
    {
        if (State == AlignSessionState.Idle)
            return;
        if (State == AlignSessionState.Applying)
        {
            _invalidatePending = true;
            return;
        }
        if (_invalidating)
            return;
        _invalidating = true;
        int generation = _generation;
        try
        {
            RestoreSnapshots();
        }
        finally
        {
            _invalidating = false;
            EndSession(generation);
        }
    }

    /// <summary>Runs a deferred invalidation after a backend call; true if the session
    /// was torn down. The committed layout (if any) targeted a topology that changed
    /// mid-flight, so no stale snapshot positions are written on top; the model is
    /// expected to be resynced by re-enumeration.</summary>
    private bool ProcessPendingInvalidation(int generation)
    {
        if (!_invalidatePending)
            return false;
        _invalidatePending = false;
        try
        {
            RestoreSnapshots();
        }
        finally
        {
            EndSession(generation);
        }
        return true;
    }

    // Session teardown on EVERY exit path (fixes the original's stale-model leak).
    private void EndSession(int generation)
    {
        if (generation != _generation)
            return; // that session was already ended by a re-entrant teardown
        _generation++;
        AlignSettings endedSettings = Settings!;
        _session.Clear();
        Settings = null;
        State = AlignSessionState.Idle;
        SessionEnded?.Invoke(endedSettings);
    }
}
