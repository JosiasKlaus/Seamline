using System.Runtime.Versioning;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace Seamline.App;

/// <summary>Wrap layout for the monitor cards, in logical units: as many
/// equal-width columns as fit (cards 270–480 wide), cards stretched to share
/// the row, every row as tall as its tallest card. The WPF counterpart of the
/// former WinForms MonitorCardGrid — WPF's two-pass Measure/Arrange makes the
/// stale-parent-layout hazard that grid had to heal around structurally
/// impossible.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class CardWrapPanel : System.Windows.Controls.Panel
{
    private const double Gap = 10;
    private const double VGap = 8;
    private const double MinCardWidth = 270;
    private const double MaxCardWidth = 480; // sparse text on huge cards reads badly

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        int count = InternalChildren.Count;
        if (count == 0)
            return new WpfSize(0, 0);

        double width = double.IsPositiveInfinity(availableSize.Width)
            ? MinCardWidth * count + Gap * (count - 1)
            : availableSize.Width;
        (int columns, double cardWidth) = Wrap(width, count);

        double total = 0;
        for (int start = 0; start < count; start += columns)
        {
            double rowHeight = 0;
            for (int i = start; i < Math.Min(start + columns, count); i++)
            {
                InternalChildren[i].Measure(new WpfSize(cardWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);
            }
            total += rowHeight + VGap;
        }
        return new WpfSize(
            Math.Min(width, columns * cardWidth + (columns - 1) * Gap),
            total - VGap);
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        int count = InternalChildren.Count;
        if (count == 0)
            return finalSize;

        (int columns, double cardWidth) = Wrap(finalSize.Width, count);
        double y = 0;
        for (int start = 0; start < count; start += columns)
        {
            int rowCount = Math.Min(columns, count - start);
            double rowHeight = 0;
            for (int i = 0; i < rowCount; i++)
                rowHeight = Math.Max(rowHeight, InternalChildren[start + i].DesiredSize.Height);
            for (int i = 0; i < rowCount; i++)
                InternalChildren[start + i].Arrange(
                    new WpfRect(i * (cardWidth + Gap), y, cardWidth, rowHeight));
            y += rowHeight + VGap;
        }
        return finalSize;
    }

    private static (int Columns, double CardWidth) Wrap(double width, int count)
    {
        int columns = Math.Clamp((int)((width + Gap) / (MinCardWidth + Gap)), 1, count);
        double cardWidth = Math.Min((width - Gap * (columns - 1)) / columns, MaxCardWidth);
        return (columns, Math.Max(cardWidth, 50));
    }
}
