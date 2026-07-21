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
        ClientSize = new Size(500, 260);

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
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(162, 20),
        };
        var version = new Label
        {
            Text = $"Version {FormatVersion(Application.ProductVersion)}",
            AutoSize = true,
            Location = new Point(166, 66),
            ForeColor = Color.FromArgb(96, 96, 96),
        };
        var description = new Label
        {
            Text = "Align your physical multi-monitor setup with Windows'\nvirtual screen layout — pixel by pixel.",
            AutoSize = true,
            Location = new Point(166, 92),
        };
        const string acknowledgementText = "A from-scratch, behavior-compatible rewrite of HwMonitorAlignment.";
        const string linkTarget = "HwMonitorAlignment";
        var acknowledgement = new LinkLabel
        {
            Text = acknowledgementText,
            AutoSize = true,
            Location = new Point(166, 136),
            LinkArea = new LinkArea(acknowledgementText.IndexOf(linkTarget, StringComparison.Ordinal), linkTarget.Length),
        };
        acknowledgement.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(
            "https://github.com/Chrisg2000/HwMonitorAlignment") { UseShellExecute = true });
        var components = new Label
        {
            Text = "Third-party components:\n.NET Runtime (MIT) — Microsoft\nCsWin32 (MIT) — Microsoft",
            AutoSize = true,
            Location = new Point(166, 162),
        };
        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 28),
            Location = new Point(390, 218),
        };

        Controls.AddRange([iconBox, heading, version, description, acknowledgement, components, closeButton]);
        AcceptButton = closeButton;
        CancelButton = closeButton;
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
        return stream is null ? null : Image.FromStream(stream);
    }
}
