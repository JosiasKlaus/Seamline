using System.Runtime.Versioning;
using Microsoft.Win32;
using Seamline.Core;

namespace Seamline.App;

/// <summary>
/// The main window (M7.5 look): Win11-style overview card on top, a "Monitors"
/// section with one live monitor card per monitor in (x, y) order, and
/// About / Adjust / Close as Fluent buttons on a Mica-topped window. Resizable:
/// the overview absorbs extra height, the card grid re-wraps its columns with
/// the width (§backlog #9).
///
/// DPI model (verified): there is NO startup scale pass over the chrome — the
/// AutoScaleDimensions setter consumes AutoScaleMode.Dpi's initial pass while
/// the form is still empty, so static chrome is built in raw device pixels at
/// the launch DPI (point-size fonts render DPI-scaled on their own, and the
/// sizing code below scales its constants by DeviceDpi explicitly). Only a DPI
/// change while running rescales existing chrome (framework); the monitor cards
/// are instead rebuilt and always self-scale from DeviceDpi.
///
/// Re-enumerates on display-settings changes while no session is active, so the
/// overview and cards track external layout changes.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class MainForm : Form
{
    private readonly IDisplayBackend _backend;
    private readonly MonitorLayout _layout;
    private readonly AlignController _controller;
    private readonly TableLayoutPanel _root;
    private readonly MonitorCardGrid _cardsGrid;
    private readonly OverviewPanel _overview;
    private readonly Label _sectionLabel;
    private readonly TableLayoutPanel _buttonRow;
    private bool _displayChangedDuringSession;

    public MainForm(IDisplayBackend backend, MonitorLayout layout, AlignController controller,
        AlignSessionCoordinator coordinator)
    {
        _backend = backend;
        _layout = layout;
        _controller = controller;

        Text = "Seamline";
        FormBorderStyle = FormBorderStyle.Sizable;
        Font = ModernTheme.UiFont(9.5f);
        BackColor = ModernTheme.WindowBackground;
        DoubleBuffered = true;
        if (Environment.ProcessPath is { } exePath)
            Icon = Icon.ExtractAssociatedIcon(exePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        // The initial size and MinimumSize are computed in OnLoad from the actual
        // content; that code scales by DeviceDpi explicitly — no framework pass
        // scales this chrome at startup (see the class doc).

        var root = _root = new BufferedTableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
        };
        // The overview absorbs all extra height; the other rows fit their content.
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var overview = _overview = new OverviewPanel(layout)
        {
            Dock = DockStyle.Fill,
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

        _cardsGrid = new MonitorCardGrid
        {
            // Left|Right follows the cell width; Top (no Bottom) keeps the height
            // the grid computes for itself, which the autosize row then fits.
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, 8),
        };
        // Cards are built in OnLoad and on rebuilds, always scaling themselves
        // from the current DeviceDpi — startup and rebuild cards get identical
        // geometry.

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

        var buttonRow = _buttonRow = new BufferedTableLayoutPanel
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
        root.Controls.Add(_cardsGrid, 0, 2);
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
        BuildInfoPanels(); // cards self-scale from DeviceDpi
        ApplyInitialWindowSize();
    }

    /// <summary>Initial window size, computed from the content (runs pre-Show):
    /// wide enough for up to three card columns, tall enough for the resulting
    /// card wrap plus a comfortable overview.</summary>
    private void ApplyInitialWindowSize()
    {
        float s = DeviceDpi / 96f;
        Rectangle work = Screen.FromControl(this).WorkingArea;

        // Mirror the grid's own column math (card 270 + gap 10 logical), plus a
        // few px slack so integer rounding can never force an unintended wrap.
        int columns = Math.Clamp(_layout.Count, 1, 3);
        int desiredWidth = _root.Padding.Horizontal
            + columns * (int)(270 * s) + (columns - 1) * (int)(10 * s) + (int)(4 * s);
        desiredWidth = Math.Max(desiredWidth, (int)(560 * s));
        desiredWidth = Math.Min(desiredWidth, work.Width * 9 / 10);

        // Width first, so the grid wraps at the real width before height is read.
        ClientSize = new Size(desiredWidth, ClientSize.Height);
        _root.PerformLayout();
        UpdateMinimumSize(); // before the height set below, which it may clamp up

        int desiredHeight = _root.Padding.Vertical
            + (int)(220 * s) + _overview.Margin.Vertical
            + _sectionLabel.Height + _sectionLabel.Margin.Vertical
            + _cardsGrid.Height + _cardsGrid.Margin.Vertical
            + _buttonRow.Height + _buttonRow.Margin.Vertical;
        desiredHeight = Math.Min(desiredHeight, work.Height * 9 / 10);

        ClientSize = new Size(desiredWidth, desiredHeight);
    }

    /// <summary>Minimum window size: 600 logical wide, and tall enough that at
    /// that width the full card wrap, the button row and a usable overview strip
    /// stay visible (a fixed floor let 5+-monitor wraps push the buttons off the
    /// client area and the overview's percent row negative). Clamped to the
    /// working area — the framework clamps too, but only against the screen the
    /// form is on at assignment time.</summary>
    private void UpdateMinimumSize()
    {
        float s = DeviceDpi / 96f;
        Rectangle work = Screen.FromControl(this).WorkingArea;

        int minClientWidth = (int)(600 * s);
        int minClientHeight = Math.Max((int)(480 * s),
            _root.Padding.Vertical
            + (int)(120 * s) + _overview.Margin.Vertical
            + _sectionLabel.Height + _sectionLabel.Margin.Vertical
            + _cardsGrid.MeasureHeightAt(minClientWidth - _root.Padding.Horizontal)
            + _cardsGrid.Margin.Vertical
            + _buttonRow.Height + _buttonRow.Margin.Vertical);

        Size minimum = SizeFromClientSize(new Size(minClientWidth, minClientHeight));
        MinimumSize = new Size(Math.Min(minimum.Width, work.Width), Math.Min(minimum.Height, work.Height));
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        // AutoScaleMode.Dpi rescales the static chrome; the cards captured the
        // old DeviceDpi at construction and must be rebuilt. Deferred so the
        // framework's own scale pass fully unwinds first.
        BeginInvoke(() =>
        {
            BuildInfoPanels();
            UpdateMinimumSize();
        });
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
        _cardsGrid.SuspendLayout();
        foreach (Control old in _cardsGrid.Controls.Cast<Control>().ToArray())
            old.Dispose();
        _cardsGrid.Controls.Clear();
        foreach (MonitorInfo monitor in _layout.MonitorsInOrder)
            _cardsGrid.Controls.Add(new MonitorCard(monitor, DeviceDpi));
        _cardsGrid.ResumeLayout();
        _cardsGrid.LayoutCards(force: true);
        // Membership changes alter the wrap the minimum height must accommodate.
        // Not during OnLoad — ApplyInitialWindowSize computes it at the real width.
        if (Visible)
            UpdateMinimumSize();
    }
}
