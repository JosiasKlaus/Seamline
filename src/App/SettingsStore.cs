using System.Text.Json;
using Seamline.Core;

namespace Seamline.App;

/// <summary>Persists the align-session settings across runs (REWRITE_PLAN §8.16).
/// Best-effort: any I/O or parse failure silently falls back to defaults.</summary>
internal static class SettingsStore
{
    private sealed record PersistedSettings(
        bool ShowCursorPosition,
        bool ShowDiagonalLines,
        bool ShowHorizontalLines,
        bool Antialiasing,
        bool ShowInfoBox,
        int LineSpacing,
        float LineThickness);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Seamline", "settings.json");

    public static void ApplyTo(AlignSettings settings)
    {
        try
        {
            if (!File.Exists(FilePath))
                return;
            PersistedSettings? persisted =
                JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(FilePath));
            if (persisted is null)
                return;
            settings.ShowCursorPosition = persisted.ShowCursorPosition;
            settings.ShowDiagonalLines = persisted.ShowDiagonalLines;
            settings.ShowHorizontalLines = persisted.ShowHorizontalLines;
            settings.Antialiasing = persisted.Antialiasing;
            settings.ShowInfoBox = persisted.ShowInfoBox;
            settings.LineSpacing = Math.Clamp(persisted.LineSpacing, 50, 960);
            settings.LineThickness = Math.Clamp(persisted.LineThickness, 0f, 100f);
        }
        catch
        {
            // Corrupt or unreadable settings are not worth failing a session over.
        }
    }

    public static void Save(AlignSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var persisted = new PersistedSettings(
                settings.ShowCursorPosition, settings.ShowDiagonalLines, settings.ShowHorizontalLines,
                settings.Antialiasing, settings.ShowInfoBox, settings.LineSpacing, settings.LineThickness);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best effort.
        }
    }
}
