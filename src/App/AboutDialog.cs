using System.Diagnostics;

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
        ClientSize = new Size(420, 250);

        var heading = new Label
        {
            Text = "Seamline",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 16),
        };
        var version = new Label
        {
            Text = $"Version {Application.ProductVersion}",
            AutoSize = true,
            Location = new Point(24, 62),
            ForeColor = Color.FromArgb(96, 96, 96),
        };
        var description = new Label
        {
            Text = "Align your physical multi-monitor setup with Windows'\nvirtual screen layout — pixel by pixel.",
            AutoSize = true,
            Location = new Point(24, 88),
        };
        const string acknowledgementText = "A from-scratch, behavior-compatible rewrite of HwMonitorAlignment.";
        const string linkTarget = "HwMonitorAlignment";
        var acknowledgement = new LinkLabel
        {
            Text = acknowledgementText,
            AutoSize = true,
            Location = new Point(24, 130),
            LinkArea = new LinkArea(acknowledgementText.IndexOf(linkTarget, StringComparison.Ordinal), linkTarget.Length),
        };
        acknowledgement.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(
            "https://github.com/Chrisg2000/HwMonitorAlignment") { UseShellExecute = true });
        var components = new Label
        {
            Text = "Third-party components:\n.NET Runtime (MIT) — Microsoft\nCsWin32 (MIT) — Microsoft",
            AutoSize = true,
            Location = new Point(24, 156),
        };
        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 28),
            Location = new Point(310, 208),
        };

        Controls.AddRange([heading, version, description, acknowledgement, components, closeButton]);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }
}
