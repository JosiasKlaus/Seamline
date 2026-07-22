namespace Seamline.App;

/// <summary>
/// The post-apply countdown: keep the new configuration or auto-revert.
/// Original German strings preserved; "Nein" is the default and is auto-invoked
/// when the countdown reaches zero. No close button, always on top.
/// </summary>
internal sealed class RollbackDialog : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Label _countdownLabel;
    private readonly Button _noButton;
    private int _remaining;

    public RollbackDialog(int timeoutSeconds = 15)
    {
        _remaining = timeoutSeconds;

        Text = "Änderung übernehmen?";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F); // baseline so PMv2 Dpi scaling has an anchor
        ClientSize = new Size(380, 130);

        var icon = new PictureBox
        {
            Image = SystemIcons.Warning.ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(16, 16),
        };
        var message = new Label
        {
            Text = "Ihre Desktop-Konfiguration hat sich geändert.\nMöchten Sie diese Änderung beibehalten?",
            AutoSize = true,
            Location = new Point(64, 16),
        };
        _countdownLabel = new Label
        {
            Text = CountdownText(),
            AutoSize = true,
            Location = new Point(64, 60),
        };
        var yesButton = new FluentButton
        {
            Text = "Ja",
            DialogResult = DialogResult.Yes,
            Size = new Size(90, 30),
            Location = new Point(174, 90),
        };
        _noButton = new FluentButton
        {
            Text = "Nein",
            DialogResult = DialogResult.No,
            Primary = true, // the default (and safe) answer gets the accent
            Size = new Size(90, 30),
            Location = new Point(272, 90),
        };

        Controls.AddRange([icon, message, _countdownLabel, yesButton, _noButton]);
        AcceptButton = _noButton; // "No" is the default answer
        CancelButton = _noButton; // Esc answers No (revert), like the original QMessageBox

        _timer.Tick += OnTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _noButton.Focus();
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _timer.Stop();
            _noButton.PerformClick();
        }
        _countdownLabel.Text = CountdownText();
    }

    private string CountdownText() => $"Rücksetzung in {_remaining} Sekunden";
}
