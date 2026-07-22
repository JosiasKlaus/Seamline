using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>
/// Manual-layout host for the monitor cards: wraps them into as many equal-width
/// columns as fit, stretches the cards in a row to share its full width, gives
/// them a uniform height (the row's tallest), and derives its own height from the
/// result. Manual because the wrap math must stay deterministic under live
/// resize — TableLayoutPanel's autosize negotiation with self-sizing children is
/// not. All geometry is scaled from DeviceDpi at layout time, matching the
/// cards' own scaling regime.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MonitorCardGrid : Panel
{
    private int _laidOutWidth = -1;

    public MonitorCardGrid()
    {
        DoubleBuffered = true;
    }

    /// <summary>Reflows all cards for the current width. Guarded on width so the
    /// height assignment cannot recurse; <paramref name="force"/> is for
    /// relayouts after membership/theme rebuilds at an unchanged width.</summary>
    public void LayoutCards(bool force = false)
    {
        if (Width <= 0 || (!force && Width == _laidOutWidth))
            return;
        _laidOutWidth = Width;

        MonitorCard[] cards = Controls.OfType<MonitorCard>().ToArray();
        if (cards.Length == 0)
        {
            SetHeightAndHealParent(0);
            return;
        }

        (int hGap, int vGap, int minCardWidth, int maxCardWidth) = Metrics();
        int columns = Math.Clamp((Width + hGap) / (minCardWidth + hGap), 1, cards.Length);
        int cardWidth = Math.Min((Width - hGap * (columns - 1)) / columns, maxCardWidth);

        SuspendLayout();
        int y = 0;
        for (int start = 0; start < cards.Length; start += columns)
        {
            int rowCount = Math.Min(columns, cards.Length - start);

            // First pass sets the width (the card reflows its lines and derives
            // its content height), second pass stretches the row to equal height.
            int rowHeight = 0;
            for (int i = 0; i < rowCount; i++)
            {
                cards[start + i].Width = cardWidth;
                rowHeight = Math.Max(rowHeight, cards[start + i].Height);
            }
            for (int i = 0; i < rowCount; i++)
                cards[start + i].SetBounds(i * (cardWidth + hGap), y, cardWidth, rowHeight);

            y += rowHeight + vGap;
        }
        SetHeightAndHealParent(y - vGap);
        ResumeLayout(false);
    }

    /// <summary>Pure measurement: the height the grid would need at the given
    /// width, estimating rows from the cards' current heights (card height only
    /// varies with title wrapping, which changes little across the widths this
    /// is asked about). Feeds the window's minimum-height computation.</summary>
    public int MeasureHeightAt(int width)
    {
        MonitorCard[] cards = Controls.OfType<MonitorCard>().ToArray();
        if (cards.Length == 0 || width <= 0)
            return 0;

        (int hGap, int vGap, int minCardWidth, _) = Metrics();
        int columns = Math.Clamp((width + hGap) / (minCardWidth + hGap), 1, cards.Length);
        int y = 0;
        for (int start = 0; start < cards.Length; start += columns)
        {
            int rowHeight = 0;
            for (int i = start; i < Math.Min(start + columns, cards.Length); i++)
                rowHeight = Math.Max(rowHeight, cards[i].Height);
            y += rowHeight + vGap;
        }
        return y - vGap;
    }

    /// <summary>Applies the computed height and, when it changed, schedules a
    /// follow-up layout of the parent: the change usually happens DURING the
    /// parent TableLayoutPanel's own layout pass, and WinForms drops layout
    /// requests against a mid-layout container — the autosize row would keep the
    /// stale height (a dead gap, or cards covering the button row) until some
    /// unrelated event. The posted callback runs before WM_PAINT, so the stale
    /// intermediate layout is never painted.</summary>
    private void SetHeightAndHealParent(int newHeight)
    {
        if (Height == newHeight)
            return;
        Height = newHeight;
        if (IsHandleCreated)
            BeginInvoke(() =>
            {
                if (!IsDisposed && Parent is { } parent)
                    parent.PerformLayout(this, nameof(Height));
            });
    }

    private (int HGap, int VGap, int MinCardWidth, int MaxCardWidth) Metrics()
    {
        float scale = DeviceDpi / 96f;
        // Max width: sparse text on huge cards reads badly.
        return ((int)(10 * scale), (int)(8 * scale), (int)(270 * scale), (int)(480 * scale));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutCards();
    }
}
