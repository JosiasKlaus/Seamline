using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Media.Imaging;

namespace Seamline.App;

/// <summary>WPF port of the About dialog (M8): logo, version, description,
/// third-party notices (the acknowledgement of the original lives in the
/// README). Close is both the default and the Escape action.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed partial class AboutWindow : System.Windows.Window
{
    public AboutWindow()
    {
        InitializeComponent();
        // Palette brushes resolve from the app-level dictionary (Program.cs),
        // which MainWindow refreshes on theme switches — including while this
        // dialog is open.
        VersionText.Text = $"Version {FormatVersion(Application.ProductVersion)}";
        if (LoadLogo() is { } logo)
            LogoImage.Source = logo;
    }

    // InformationalVersion carries "+<40-char git sha>"; show "0.9.0 (3df9238)"
    // instead — the full sha stays in the file's VERSIONINFO for diagnostics.
    private static string FormatVersion(string productVersion)
    {
        int plus = productVersion.IndexOf('+');
        if (plus < 0)
            return productVersion;
        string sha = productVersion[(plus + 1)..];
        return $"{productVersion[..plus]} ({sha[..Math.Min(7, sha.Length)]})";
    }

    private static BitmapImage? LoadLogo()
    {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Seamline.App.Resources.seamline-256.png");
        if (stream is null)
            return null;
        // OnLoad decodes eagerly so the stream may be disposed right here.
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
