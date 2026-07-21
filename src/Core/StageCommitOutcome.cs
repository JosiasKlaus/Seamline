namespace Seamline.Core;

/// <summary>Per-call results of one stage-all-then-commit pass. Results are surfaced,
/// never swallowed (the original ignored them).</summary>
public sealed record StageCommitOutcome(
    IReadOnlyList<(string DeviceName, DisplayChangeResult Result)> StageResults,
    DisplayChangeResult? CommitResult)
{
    /// <summary>True when every stage succeeded and the commit ran and succeeded.</summary>
    public bool Succeeded =>
        StageResults.All(r => r.Result.Succeeded) && CommitResult is { Succeeded: true };
}

/// <summary>Outcome of AlignController.Apply. When the apply pass fails, the controller
/// automatically restores the registry from the session snapshots (no residue);
/// AutoRevert carries those results.</summary>
public sealed record ApplyOutcome(StageCommitOutcome Applied, StageCommitOutcome? AutoRevert)
{
    public bool Succeeded => Applied.Succeeded;
}
