namespace Seamline.Core;

/// <summary>Outcome of a display-configuration call (Win32 DISP_CHANGE_* code plus its name).</summary>
public readonly record struct DisplayChangeResult(int Code, string Name)
{
    /// <summary>True for DISP_CHANGE_SUCCESSFUL (0).</summary>
    public bool Succeeded => Code == 0;

    public override string ToString() => $"{Name} ({Code})";
}
