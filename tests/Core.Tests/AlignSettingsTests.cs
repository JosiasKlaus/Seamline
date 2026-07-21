namespace Seamline.Core.Tests;

public class AlignSettingsTests
{
    [Fact]
    public void Defaults_match_the_behavioral_spec()
    {
        var settings = new AlignSettings();

        Assert.False(settings.ShowCursorPosition);
        Assert.False(settings.ShowDiagonalLines);
        Assert.True(settings.ShowHorizontalLines);
        Assert.False(settings.Antialiasing);
        Assert.True(settings.ShowInfoBox);
        Assert.Equal(100, settings.LineSpacing);
        Assert.Equal(3f, settings.LineThickness);
    }

    [Fact]
    public void Changes_notify_with_equality_suppression()
    {
        var settings = new AlignSettings();
        var names = new List<string?>();
        settings.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        settings.LineSpacing = 100;   // no-op
        settings.LineSpacing = 150;
        settings.ShowDiagonalLines = true;

        Assert.Equal([nameof(AlignSettings.LineSpacing), nameof(AlignSettings.ShowDiagonalLines)], names);
    }
}
