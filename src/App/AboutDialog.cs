using System.Diagnostics;
using System.Reflection;

namespace Seamline.App;

internal sealed class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = "About Seamline";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(560, 290);
        // Text column: wraps instead of clipping at the dialog edge.
        const int textLeft = 166;
        var textMax = new Size(560 - textLeft - 20, 0);

        var iconBox = new PictureBox
        {
            Size = new Size(125, 125),
            Location = new Point(20, 20),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadLogo(),
        };
        var heading = new Label
        {
            Text = "Seamline",
            Font = ModernTheme.UiFont(20f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(162, 20),
        };
        var version = new Label
        {
            Text = $"Version {FormatVersion(Application.ProductVersion)}",
            AutoSize = true,
            Location = new Point(166, 66),
            ForeColor = SystemColors.GrayText, // remapped by dark mode
        };
        var description = new Label
        {
            Text = "Align your physical multi-monitor setup with Windows' virtual screen layout — pixel by pixel.",
            AutoSize = true,
            MaximumSize = textMax,
            Location = new Point(textLeft, 92),
        };
        const string acknowledgementText = "A from-scratch, behavior-compatible rewrite of HwMonitorAlignment.";
        const string linkTarget = "HwMonitorAlignment";
        var acknowledgement = new LinkLabel
        {
            Text = acknowledgementText,
            AutoSize = true,
            MaximumSize = textMax,
            Location = new Point(textLeft, 140),
            LinkArea = new LinkArea(acknowledgementText.IndexOf(linkTarget, StringComparison.Ordinal), linkTarget.Length),
            LinkColor = ModernTheme.AccentForeground, // lightened in dark mode
        };
        acknowledgement.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(
            "https://github.com/Chrisg2000/HwMonitorAlignment") { UseShellExecute = true });
        var components = new Label
        {
            Text = "Third-party components:\n.NET Runtime (MIT) — Microsoft\nCsWin32 (MIT) — Microsoft",
            AutoSize = true,
            MaximumSize = textMax,
            Location = new Point(textLeft, 180),
        };
        var closeButton = new FluentButton
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(444, 242),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };

        Controls.AddRange([iconBox, heading, version, description, acknowledgement, components, closeButton]);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        Disposed += (_, _) => heading.Font.Dispose();
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

    private static Image? LoadLogo()
    {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Seamline.App.Resources.seamline-256.png");
        if (stream is null)
            return null;
        // Copy the pixels: GDI+ requires the source stream to outlive an Image
        // created with FromStream, and this stream is disposed right here.
        using var decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }
}
