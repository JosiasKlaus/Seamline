using System.Runtime.Versioning;
using Microsoft.Win32;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// The main window (M7.5 look): Win11-style overview card on top, a "Monitors"
/// section with one live monitor card per monitor in (x, y) order, and
/// About / Adjust / Close as Fluent buttons on a Mica-topped window. Static
/// chrome scales via AutoScaleMode.Dpi; monitor cards are built AFTER the scale
/// pass (OnLoad and rebuilds) and therefore scale themselves from DeviceDpi.
/// Re-enumerates on display-settings changes while no session is active, so the
/// overview and cards track external layout changes.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MainForm : Form
{
    private readonly IDisplayBackend _backend;
    private readonly MonitorLayout _layout;
    private readonly AlignController _controller;
    private readonly FlowLayoutPanel _cardsTable;
    private readonly OverviewPanel _overview;
    private readonly Label _sectionLabel;
    private bool _displayChangedDuringSession;

    public MainForm(IDisplayBackend backend, MonitorLayout layout, AlignController controller,
        AlignSessionCoordinator coordinator)
    {
        _backend = backend;
        _layout = layout;
        _controller = controller;

        // Fixed-size window: the resizable/responsive layout is deferred (it needs
        // proper resize-repaint and stretch-card work — tracked as a backlog item).
        Text = "Seamline";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = ModernTheme.UiFont(9.5f);
        BackColor = ModernTheme.WindowBackground;
        if (Environment.ProcessPath is { } exePath)
            Icon = Icon.ExtractAssociatedIcon(exePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
        };

        var overview = _overview = new OverviewPanel(layout)
        {
            Height = 210,
            Dock = DockStyle.Fill,
            MinimumSize = new Size(520, 210),
            Margin = new Padding(0, 0, 0, 10),
        };

        var sectionLabel = _sectionLabel = new Label
        {
            Text = "Monitors",
            AutoSize = true,
            Font = ModernTheme.UiFont(11f, FontStyle.Bold),
            ForeColor = ModernTheme.TextPrimary,
            Margin = new Padding(2, 0, 0, 6),
        };

        _cardsTable = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MaximumSize = new Size(1100, 0), // wrap instead of outgrowing the screen
            Margin = new Padding(0, 0, 0, 8),
        };
        // Cards are built in OnLoad, after the Dpi auto-scale pass, so startup and
        // rebuild cards get identical (self-scaled) geometry.

        // Zero side margins so the buttons align flush with the overview's edges.
        var aboutButton = new FluentButton { Text = "About", Margin = new Padding(0, 3, 0, 0) };
        aboutButton.Click += (_, _) =>
        {
            using var dialog = new AboutDialog();
            dialog.ShowDialog(this);
        };
        var adjustButton = new FluentButton { Text = "Adjust", Primary = true, Margin = new Padding(0, 3, 8, 0) };
        adjustButton.Click += (_, _) => coordinator.StartSession();
        var closeButton = new FluentButton { Text = "Close", Margin = new Padding(0, 3, 0, 0) };
        closeButton.Click += (_, _) => Close();

        var buttonRow = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var rightButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, // wrapping stacked Close under Adjust and clipped it
            Margin = new Padding(0),
        };
        rightButtons.Controls.Add(adjustButton);
        rightButtons.Controls.Add(closeButton);
        buttonRow.Controls.Add(aboutButton, 0, 0);
        buttonRow.Controls.Add(new Panel { Height = 1 }, 1, 0);
        buttonRow.Controls.Add(rightButtons, 2, 0);

        root.Controls.Add(overview, 0, 0);
        root.Controls.Add(sectionLabel, 0, 1);
        root.Controls.Add(_cardsTable, 0, 2);
        root.Controls.Add(buttonRow, 0, 3);
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
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 11 Mica on the frame/title bar; a silent no-op elsewhere.
        Seamline.Win32.DwmBackdrop.TryApplyMica(Handle);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildInfoPanels(); // after the Dpi auto-scale pass — cards self-scale
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
        // Static events — must detach.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnFormClosed(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // A light/dark switch arrives as a General preference change. WinForms'
        // color mode retheming covers standard controls; everything painted from
        // ModernTheme was captured at construction and must be reapplied.
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle)
            BeginInvoke(ApplyTheme);
    }

    private void ApplyTheme()
    {
        BackColor = ModernTheme.WindowBackground;
        _overview.BackColor = ModernTheme.WindowBackground;
        _sectionLabel.ForeColor = ModernTheme.TextPrimary;
        BuildInfoPanels(); // cards re-capture the palette
        Invalidate(true);
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
        _cardsTable.SuspendLayout();
        foreach (Control old in _cardsTable.Controls.Cast<Control>().ToArray())
            old.Dispose();
        _cardsTable.Controls.Clear();
        foreach (MonitorInfo monitor in _layout.MonitorsInOrder)
            _cardsTable.Controls.Add(new MonitorCard(monitor, DeviceDpi) { Margin = new Padding(0, 0, 10, 8) });
        _cardsTable.ResumeLayout();
    }
}
