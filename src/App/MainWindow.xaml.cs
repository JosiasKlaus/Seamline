using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows.Interop;
using Microsoft.Win32;
using Seamline.Core;
using WpfSize = System.Windows.Size;

namespace Seamline.App;

/// <summary>
/// The main window, M8: a WPF-Fluent shell over the unchanged Core/Win32 stack.
/// Win11-style overview on top (absorbs extra height), one live monitor card per
/// monitor in (x, y) order (MonitorInfo's INPC drives the bindings directly),
/// About / Adjust / Close below. The physical-pixel WinForms session overlays
/// run unchanged on the same STA thread under this window's dispatcher pump.
/// Launch height comes from SizeToContent; the minimum size is content-derived
/// (the card wrap at minimum width stays fully visible — see RefreshMinimumSize).
/// Re-enumerates on display-settings changes while no session is active.
/// </summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed partial class MainWindow : System.Windows.Window
{
    private const double MinWindowWidth = 620;
    private const double OverviewMinHeight = 120;

    private readonly IDisplayBackend _backend;
    private readonly MonitorLayout _layout;
    private readonly AlignController _controller;
    private bool _displayChangedDuringSession;

    public MainWindow(IDisplayBackend backend, MonitorLayout layout, AlignController controller,
        AlignSessionCoordinator coordinator)
    {
        _backend = backend;
        _layout = layout;
        _controller = controller;

        InitializeComponent();
        Overview.Bind(layout);
        RebuildCards();

        // Launch width fits up to three card columns (the height then follows
        // via SizeToContent="Height" and the overview's initial explicit 220).
        int columns = Math.Clamp(layout.Count, 1, 3);
        Width = Math.Min(
            40 + 24 + columns * 270 + (columns - 1) * 10,
            System.Windows.SystemParameters.WorkArea.Width * 0.9);

        // One session at a time: Adjust re-enables on every end path
        // (fixes the original's double-start overlay leak).
        controller.SessionStarted += () => AdjustButton.IsEnabled = false;
        controller.SessionEnded += _ =>
        {
            AdjustButton.IsEnabled = true;
            if (_displayChangedDuringSession)
                Dispatcher.BeginInvoke(RefreshFromSystem); // after teardown fully unwinds
        };
        AdjustButton.Click += (_, _) => coordinator.StartSession();

        // Membership changes replace MonitorInfo instances — rebuild the bound cards.
        layout.Reset += RebuildCards;
        layout.MonitorAdded += _ => RebuildCards();
        layout.MonitorRemoved += _ => RebuildCards();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Windows 11 Mica on the frame/title bar (the Fluent theme may already
        // provide a backdrop; this is a silent no-op then and elsewhere).
        Seamline.Win32.DwmBackdrop.TryApplyMica(new WindowInteropHelper(this).Handle);
    }

    private void OnLoaded(object? sender, System.Windows.RoutedEventArgs e)
    {
        // The launch height is now fixed — hand the overview to the star row so
        // resizing stretches it instead of the initial explicit height.
        SizeToContent = System.Windows.SizeToContent.Manual;
        // SizeToContent knows no screen: clamp the derived height to the work
        // area like the WinForms shell did (5+-monitor wraps on short screens
        // otherwise open with the button row below the taskbar).
        double workHeight = System.Windows.SystemParameters.WorkArea.Height;
        if (Height > workHeight * 0.9)
            Height = workHeight * 0.9;
        Overview.ClearValue(HeightProperty);
        RefreshMinimumSize();
    }

    /// <summary>Content-derived minimum: at minimum width the full card wrap,
    /// the buttons and an overview strip must stay visible (a fixed floor would
    /// let 5+-monitor wraps push the buttons out of the client area).</summary>
    private void RefreshMinimumSize()
    {
        // Chrome + root margins: window size minus arranged content size.
        double chromeWidth = ActualWidth - RootGrid.ActualWidth;
        double chromeHeight = ActualHeight - RootGrid.ActualHeight;
        if (chromeWidth <= 0 || chromeHeight <= 0)
            return; // not arranged yet; the next call recomputes

        double minCardsWidth = Math.Max(50, MinWindowWidth - chromeWidth);
        Cards.Measure(new WpfSize(minCardsWidth, double.PositiveInfinity));
        double cardsMinHeight = Cards.DesiredSize.Height;
        Cards.InvalidateMeasure(); // restore the real constraint next layout pass

        MinWidth = MinWindowWidth;
        MinHeight = Math.Min(
            chromeHeight + OverviewMinHeight + Overview.Margin.Bottom
                + SectionLabel.ActualHeight + SectionLabel.Margin.Bottom
                + cardsMinHeight + Cards.Margin.Bottom
                + ButtonsRow.ActualHeight + ButtonsRow.Margin.Top,
            System.Windows.SystemParameters.WorkArea.Height);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // App shutdown while a session is live: no-throw teardown (abort while
        // aligning; full OS revert while the countdown is showing).
        _controller.Shutdown();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Static events — must detach.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Overview.Unbind();
        base.OnClosed(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // The Fluent theme redoes the standard controls on its own
        // (ThemeMode.System); our palette-bridged brushes and the owner-rendered
        // overview must be refreshed by hand.
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle)
            Dispatcher.BeginInvoke(() =>
            {
                // App-level palette: every window's DynamicResource lookups
                // (this one AND an open About dialog) follow the same refresh.
                WpfTheme.ApplyTo(System.Windows.Application.Current.Resources);
                Overview.InvalidateVisual();
            });
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

    private void RebuildCards()
    {
        Cards.ItemsSource = _layout.MonitorsInOrder.ToList();
        if (IsLoaded)
            Dispatcher.BeginInvoke(RefreshMinimumSize); // after the wrap re-measures
    }

    private void OnAboutClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void OnCloseClick(object sender, System.Windows.RoutedEventArgs e) => Close();
}
