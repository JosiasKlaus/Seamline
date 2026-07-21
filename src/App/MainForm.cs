using System.Runtime.Versioning;
using Microsoft.Win32;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// The main window: virtual-screen overview on top, one "Monitor Information"
/// box per monitor in (x, y) order, and About / Adjust / Close. DPI scaling is
/// entirely WinForms' (AutoScaleMode.Dpi) — child controls are built at 96-DPI
/// metrics, unlike the AutoScaleMode.None overlays which scale manually.
/// Re-enumerates on display-settings changes while no session is active, so the
/// overview and info boxes track external layout changes.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MainForm : Form
{
    private readonly IDisplayBackend _backend;
    private readonly MonitorLayout _layout;
    private readonly AlignController _controller;
    private readonly FlowLayoutPanel _infoFlow;
    private bool _displayChangedDuringSession;

    public MainForm(IDisplayBackend backend, MonitorLayout layout, AlignController controller,
        AlignSessionCoordinator coordinator)
    {
        _backend = backend;
        _layout = layout;
        _controller = controller;

        Text = "Seamline";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        var overview = new OverviewPanel(layout)
        {
            Height = 200,
            Dock = DockStyle.Fill,
            MinimumSize = new Size(500, 200),
        };

        _infoFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MaximumSize = new Size(1100, 0), // wrap instead of outgrowing the screen
            Padding = new Padding(4),
        };
        BuildInfoPanels();

        var infoGroup = new GroupBox
        {
            Text = "Monitor Setup Information",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
        };
        _infoFlow.Dock = DockStyle.Fill;
        infoGroup.Controls.Add(_infoFlow);

        var aboutButton = new Button { Text = "About", Size = new Size(90, 28) };
        aboutButton.Click += (_, _) =>
        {
            using var dialog = new AboutDialog();
            dialog.ShowDialog(this);
        };
        var adjustButton = new Button { Text = "Adjust", Size = new Size(90, 28) };
        adjustButton.Click += (_, _) => coordinator.StartSession();
        var closeButton = new Button { Text = "Close", Size = new Size(90, 28) };
        closeButton.Click += (_, _) => Close();

        var buttonRow = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(4),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var rightButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, // wrapping stacked Close under Adjust and clipped it
        };
        rightButtons.Controls.Add(adjustButton);
        rightButtons.Controls.Add(closeButton);
        buttonRow.Controls.Add(aboutButton, 0, 0);
        buttonRow.Controls.Add(new Panel { Height = 1 }, 1, 0);
        buttonRow.Controls.Add(rightButtons, 2, 0);

        root.Controls.Add(overview, 0, 0);
        root.Controls.Add(infoGroup, 0, 1);
        root.Controls.Add(buttonRow, 0, 2);
        Controls.Add(root);

        // One session at a time: Adjust re-enables on every end path
        // (fixes the original's double-start overlay leak).
        controller.SessionStarted += () => adjustButton.Enabled = false;
        controller.SessionEnded += _ =>
        {
            adjustButton.Enabled = true;
            if (_displayChangedDuringSession)
                BeginInvoke(RefreshFromSystem); // after teardown fully unwinds
        };

        // Membership changes replace MonitorInfo instances — rebuild the bound panels.
        layout.Reset += BuildInfoPanels;
        layout.MonitorAdded += _ => BuildInfoPanels();
        layout.MonitorRemoved += _ => BuildInfoPanels();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // App shutdown while a session is live: no-throw teardown (abort while
        // aligning; full OS revert while the countdown is showing).
        _controller.Shutdown();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; // static event
        base.OnFormClosed(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Our own Apply commits also raise this; sessions run on a frozen set, so
        // only refresh while Idle and catch up after the session ends.
        if (_controller.State == AlignSessionState.Idle)
            RefreshFromSystem();
        else
            _displayChangedDuringSession = true;
    }

    private void RefreshFromSystem()
    {
        _displayChangedDuringSession = false;
        try
        {
            _layout.ResetTo(_backend.EnumerateMonitors());
        }
        catch
        {
            // A transient enumeration failure must not take the window down;
            // the next display-change event retries.
        }
    }

    private void BuildInfoPanels()
    {
        _infoFlow.SuspendLayout();
        foreach (Control old in _infoFlow.Controls.Cast<Control>().ToArray())
            old.Dispose();
        _infoFlow.Controls.Clear();
        foreach (MonitorInfo monitor in _layout.MonitorsInOrder)
        {
            var group = new GroupBox
            {
                Text = "Monitor Information",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6),
            };
            group.Controls.Add(new MonitorInfoRows(monitor) { Location = new Point(8, 24) });
            _infoFlow.Controls.Add(group);
        }
        _infoFlow.ResumeLayout();
    }
}
