namespace Seamline.Core;

/// <summary>
/// Shared observable settings of one alignment session; bound two-way to the
/// control box and invalidating every overlay on change. A new instance with
/// these defaults is created per session, matching the original.
/// UI enforces the ranges: LineSpacing 50–960, LineThickness 0–100.
/// </summary>
public sealed class AlignSettings : ObservableObject
{
    private bool _showCursorPosition;
    private bool _showDiagonalLines;
    private bool _showHorizontalLines = true;
    private bool _showVerticalLines;
    private bool _antialiasing;
    private bool _showInfoBox = true;
    private int _lineSpacing = 100;
    private float _lineThickness = 3f;

    public bool ShowCursorPosition { get => _showCursorPosition; set => Set(ref _showCursorPosition, value); }
    public bool ShowDiagonalLines { get => _showDiagonalLines; set => Set(ref _showDiagonalLines, value); }
    public bool ShowHorizontalLines { get => _showHorizontalLines; set => Set(ref _showHorizontalLines, value); }

    /// <summary>Vertical lines for stacked layouts (M6 extension; off by default).</summary>
    public bool ShowVerticalLines { get => _showVerticalLines; set => Set(ref _showVerticalLines, value); }
    public bool Antialiasing { get => _antialiasing; set => Set(ref _antialiasing, value); }
    public bool ShowInfoBox { get => _showInfoBox; set => Set(ref _showInfoBox, value); }
    public int LineSpacing { get => _lineSpacing; set => Set(ref _lineSpacing, value); }
    public float LineThickness { get => _lineThickness; set => Set(ref _lineThickness, value); }
}
