namespace Seamline.Core.Tests;

public class AlignControllerTests
{
    private readonly FakeBackend _backend = new();
    private readonly MonitorLayout _layout = new();
    private readonly AlignController _controller;

    private const string Primary = @"\\.\DISPLAY1";
    private const string Left = @"\\.\DISPLAY2";
    private const string Right = @"\\.\DISPLAY3";

    public AlignControllerTests()
    {
        _layout.Add(new MonitorInfo
        {
            DeviceName = Primary, ScreenWidth = 1920, ScreenHeight = 1200,
            PositionX = 0, PositionY = 0, IsPrimary = true,
        });
        _layout.Add(new MonitorInfo
        {
            DeviceName = Left, ScreenWidth = 1920, ScreenHeight = 1080,
            PositionX = 0, PositionY = -1080,
        });
        _layout.Add(new MonitorInfo
        {
            DeviceName = Right, ScreenWidth = 1920, ScreenHeight = 1080,
            PositionX = 1920, PositionY = -1080,
        });
        _controller = new AlignController(_backend, _layout);
    }

    private MonitorInfo Get(string device)
    {
        Assert.True(_layout.TryGet(device, out MonitorInfo? m));
        return m!;
    }

    [Fact]
    public void StartSession_creates_default_settings_and_enters_Aligning()
    {
        bool started = false;
        _controller.SessionStarted += () => started = true;

        _controller.StartSession();

        Assert.True(started);
        Assert.Equal(AlignSessionState.Aligning, _controller.State);
        Assert.NotNull(_controller.Settings);
        Assert.Equal(100, _controller.Settings!.LineSpacing);
    }

    [Fact]
    public void TryMoveBy_moves_secondary_and_refuses_primary_and_idle()
    {
        Assert.False(_controller.TryMoveBy(Right, 1)); // Idle: refused

        _controller.StartSession();
        Assert.True(_controller.TryMoveBy(Right, 1));
        Assert.True(_controller.TryMoveBy(Right, 10));
        Assert.Equal(-1069, Get(Right).PositionY);

        Assert.False(_controller.TryMoveBy(Primary, 1));
        Assert.Equal(0, Get(Primary).PositionY);

        Assert.False(_controller.TryMoveBy(@"\\.\DISPLAY9", 1));
    }

    [Fact]
    public void ResetPositions_restores_in_memory_keeps_session_open_no_backend_calls()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 25);

        _controller.ResetPositions();

        Assert.Equal(-1080, Get(Right).PositionY);
        Assert.Equal(AlignSessionState.Aligning, _controller.State);
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void AbortSession_restores_in_memory_ends_session_no_backend_calls()
    {
        bool ended = false;
        _controller.SessionEnded += _ => ended = true;
        _controller.StartSession();
        _controller.TryMoveBy(Left, -7);

        _controller.AbortSession();

        Assert.Equal(-1080, Get(Left).PositionY);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Null(_controller.Settings);
        Assert.True(ended);
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void Apply_stages_all_in_layout_order_then_commits_once_and_enters_Confirming()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        ApplyOutcome outcome = _controller.Apply();

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.AutoRevert);
        Assert.Equal(AlignSessionState.Confirming, _controller.State);
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1070)",
            "commit",
        ], _backend.Calls);
    }

    [Fact]
    public void Apply_raises_LayoutChanged_on_success()
    {
        _controller.StartSession();
        int raised = 0;
        _layout.LayoutChanged += () => raised++;

        _controller.Apply();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ConfirmApplied_keeps_positions_ends_session_and_raises_SessionEnded()
    {
        AlignSettings? endedWith = null;
        _controller.SessionEnded += s => endedWith = s;
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();

        _controller.ConfirmApplied();

        Assert.Equal(-1070, Get(Right).PositionY);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Null(_controller.Settings);
        Assert.NotNull(endedWith);
    }

    [Fact]
    public void RevertApplied_restores_restages_recommits_and_ends_session()
    {
        AlignSettings? endedWith = null;
        _controller.SessionEnded += s => endedWith = s;
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();
        _backend.Calls.Clear();

        StageCommitOutcome outcome = _controller.RevertApplied();

        Assert.True(outcome.Succeeded);
        Assert.Equal(-1080, Get(Right).PositionY);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Null(_controller.Settings);
        Assert.NotNull(endedWith);
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1080)",
            "commit",
        ], _backend.Calls);
    }

    [Fact]
    public void RevertApplied_with_failing_commit_still_ends_session_and_surfaces_result()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();
        _backend.CommitResult = new DisplayChangeResult(-1, "DISP_CHANGE_FAILED");

        StageCommitOutcome outcome = _controller.RevertApplied();

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.CommitResult!.Value.Succeeded);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
    }

    [Fact]
    public void Apply_with_failed_stage_skips_commit_auto_reverts_registry_and_stays_Aligning()
    {
        _backend.StageResultOverrides[Right] = new DisplayChangeResult(-2, "DISP_CHANGE_BADMODE");
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        ApplyOutcome outcome = _controller.Apply();

        Assert.False(outcome.Succeeded);
        Assert.Null(outcome.Applied.CommitResult); // half-staged layout must not be committed
        Assert.NotNull(outcome.AutoRevert);
        Assert.Equal(AlignSessionState.Aligning, _controller.State);
        // User's in-memory edit survives for a retry; only the registry was restored.
        Assert.Equal(-1070, Get(Right).PositionY);

        // The auto-revert staged snapshot positions. Right's stage fails there too
        // (per-device override), so the revert commit is also skipped — committing
        // after a partial re-stage could apply a half-reverted layout.
        Assert.Null(outcome.AutoRevert!.CommitResult);
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1070)",
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1080)",
        ], _backend.Calls);
    }

    [Fact]
    public void Apply_with_failed_commit_auto_reverts_with_exact_sequence_and_stays_Aligning()
    {
        _backend.CommitResult = new DisplayChangeResult(-1, "DISP_CHANGE_FAILED");
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        ApplyOutcome outcome = _controller.Apply();

        Assert.False(outcome.Succeeded);
        Assert.NotNull(outcome.AutoRevert);
        Assert.Equal(AlignSessionState.Aligning, _controller.State);
        Assert.Equal(-1070, Get(Right).PositionY);
        // All re-stages succeed, so the revert commit ran (and reports the same
        // configured failure — surfaced, not swallowed).
        Assert.NotNull(outcome.AutoRevert!.CommitResult);
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1070)",
            "commit",
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1080)",
            "commit",
        ], _backend.Calls);
    }

    [Fact]
    public void Second_session_after_confirm_captures_fresh_snapshots()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();
        _controller.ConfirmApplied();

        // New session: the applied position is the new baseline for rollback.
        _controller.StartSession();
        _controller.TryMoveBy(Right, 5);
        _controller.AbortSession();

        Assert.Equal(-1070, Get(Right).PositionY);
    }

    [Fact]
    public void Settings_are_recreated_with_defaults_each_session()
    {
        _controller.StartSession();
        AlignSettings first = _controller.Settings!;
        first.LineSpacing = 500;
        _controller.Apply();
        _controller.ConfirmApplied();

        _controller.StartSession();

        Assert.NotSame(first, _controller.Settings);
        Assert.Equal(100, _controller.Settings!.LineSpacing);
    }

    [Fact]
    public void Membership_change_mid_session_invalidates_it_without_backend_calls()
    {
        bool ended = false;
        _controller.SessionEnded += _ => ended = true;
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        _layout.Add(new MonitorInfo { DeviceName = @"\\.\DISPLAY4", ScreenWidth = 800, ScreenHeight = 600 });

        Assert.True(ended);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Equal(-1080, Get(Right).PositionY); // in-memory restore happened
        Assert.Empty(_backend.Calls);              // no OS writes
    }

    [Fact]
    public void Reset_population_mid_session_invalidates_without_stomping_fresh_instances()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        var freshRight = new MonitorInfo
        {
            DeviceName = Right, ScreenWidth = 1920, ScreenHeight = 1080,
            PositionX = 1920, PositionY = -500, // new topology position
        };
        _layout.ResetTo([freshRight]);

        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Equal(-500, freshRight.PositionY); // never overwritten by a stale snapshot
    }

    [Fact]
    public void Shutdown_is_a_safe_noop_when_idle_aborts_when_aligning_reverts_when_confirming()
    {
        _controller.Shutdown(); // Idle: no-op
        Assert.Empty(_backend.Calls);

        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Shutdown(); // Aligning: in-memory abort
        Assert.Equal(-1080, Get(Right).PositionY);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Empty(_backend.Calls);

        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();
        _backend.Calls.Clear();
        _controller.Shutdown(); // Confirming: full OS revert (dialog default is No)
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Contains("commit", _backend.Calls);
        Assert.Equal(-1080, Get(Right).PositionY);
    }

    [Fact]
    public void Membership_change_during_commit_is_deferred_and_ends_session_without_stale_writes()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        int ended = 0;
        _controller.SessionEnded += _ => ended++;
        // Simulates a WM_DISPLAYCHANGE-driven re-enumeration arriving synchronously
        // inside ChangeDisplaySettingsEx's commit.
        _backend.BeforeCommit = () => _layout.Add(
            new MonitorInfo { DeviceName = @"\\.\DISPLAY4", ScreenWidth = 800, ScreenHeight = 600 });

        ApplyOutcome outcome = _controller.Apply();

        Assert.Equal(1, ended);
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Null(outcome.AutoRevert);
        // Exactly one commit: no stale snapshot re-stage after the invalidation.
        Assert.Equal(1, _backend.Calls.Count(c => c == "commit"));
        Assert.Equal(-1080, Get(Right).PositionY); // in-memory restored
    }

    [Fact]
    public void Observer_mutating_layout_during_abort_restore_ends_session_exactly_once()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        AlignSettings live = _controller.Settings!;
        var endedWith = new List<AlignSettings>();
        _controller.SessionEnded += s => endedWith.Add(s);
        bool added = false;
        _layout.MonitorChanged += _ =>
        {
            if (!added)
            {
                added = true;
                _layout.Add(new MonitorInfo { DeviceName = @"\\.\DISPLAY4", ScreenWidth = 800, ScreenHeight = 600 });
            }
        };

        _controller.AbortSession();

        Assert.Single(endedWith);
        Assert.Same(live, endedWith[0]); // the real session settings, not a fabricated instance
        Assert.Equal(AlignSessionState.Idle, _controller.State);
        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void Throwing_observer_during_revert_restore_still_reverts_os_and_ends_session()
    {
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);
        _controller.Apply();
        _backend.Calls.Clear();
        _layout.MonitorChanged += _ => throw new InvalidOperationException("disposed overlay handler");

        Assert.Throws<InvalidOperationException>(() => _controller.RevertApplied());

        Assert.Equal(AlignSessionState.Idle, _controller.State); // session ended regardless
        // The OS revert ran first, staged from the snapshots.
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1080)",
            "commit",
        ], _backend.Calls);
    }

    [Fact]
    public void Apply_with_throwing_backend_best_effort_restores_registry_and_stays_Aligning()
    {
        _backend.ThrowOnStageDevice = Right;
        _controller.StartSession();
        _controller.TryMoveBy(Right, 10);

        Assert.Throws<InvalidOperationException>(() => _controller.Apply());

        Assert.Equal(AlignSessionState.Aligning, _controller.State); // not stranded in Applying
        Assert.Equal(
        [
            $"stage {Primary} (0, 0)",
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1070)",  // throws after being recorded
            $"stage {Primary} (0, 0)",       // best-effort snapshot re-stage...
            $"stage {Left} (0, -1080)",
            $"stage {Right} (1920, -1080)",  // ...throws again, swallowed
        ], _backend.Calls);

        // The session is still fully usable afterwards.
        _controller.AbortSession();
        Assert.Equal(AlignSessionState.Idle, _controller.State);
    }

    [Fact]
    public void State_guards_throw_on_invalid_transitions()
    {
        Assert.Throws<InvalidOperationException>(_controller.Apply);
        Assert.Throws<InvalidOperationException>(_controller.AbortSession);
        Assert.Throws<InvalidOperationException>(_controller.ResetPositions);
        Assert.Throws<InvalidOperationException>(_controller.ConfirmApplied);
        Assert.Throws<InvalidOperationException>(() => _controller.RevertApplied());

        _controller.StartSession();
        Assert.Throws<InvalidOperationException>(_controller.StartSession);
        Assert.Throws<InvalidOperationException>(_controller.ConfirmApplied);

        _controller.Apply();
        Assert.Equal(AlignSessionState.Confirming, _controller.State);
        Assert.Throws<InvalidOperationException>(_controller.Apply);
        Assert.Throws<InvalidOperationException>(_controller.StartSession);
        Assert.Throws<InvalidOperationException>(_controller.ResetPositions);
        Assert.Throws<InvalidOperationException>(_controller.AbortSession);
        Assert.False(_controller.TryMoveBy(Right, 1));
    }
}
