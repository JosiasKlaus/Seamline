using System.Runtime.Versioning;
using Seamline.Core;

namespace Seamline.App;

/// <summary>"Control Box" on the primary overlay: session settings (two-way bound)
/// plus the Apply / Close / Reset buttons.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class ControlBoxPanel : DraggablePanel
{
    private readonly List<IDisposable> _subscriptions = [];

    public ControlBoxPanel(AlignSessionCoordinator coordinator, AlignSettings settings, int deviceDpi = 96)
        : base("Control Box", deviceDpi)
    {
        float scale = deviceDpi / 96f;
        var font = ModernTheme.UiFont(9f * scale);
        int x = (int)(10 * scale);
        int y = TitleBar.Height + (int)(10 * scale);
        int rowGap = (int)(6 * scale);
        int controlColumn = (int)(150 * scale);

        void AddCheckRow(string caption, Func<bool> get, Action<bool> set, string propertyName)
        {
            var box = new CheckBox
            {
                Text = caption, Font = font, AutoSize = true,
                Location = new Point(x, y), Checked = get(),
                // Flat = owner-drawn glyph in the pinned colors; the visual-style
                // glyph would render with dark-theme assets on this light panel.
                FlatStyle = FlatStyle.Flat,
            };
            box.CheckedChanged += (_, _) => set(box.Checked);
            _subscriptions.Add(settings.WhenChanged(propertyName, () => box.Checked = get()));
            Controls.Add(box);
            y += box.PreferredSize.Height + rowGap;
        }

        void AddNumericRow(string caption, decimal min, decimal max, int decimals,
            Func<decimal> get, Action<decimal> set, string propertyName)
        {
            var label = new Label
            {
                Text = caption, Font = font, AutoSize = true, Location = new Point(x, y + 3),
            };
            var numeric = new NumericUpDown
            {
                Minimum = min, Maximum = max, DecimalPlaces = decimals, Font = font,
                Width = (int)(70 * scale), Location = new Point(x + controlColumn, y),
                Value = Math.Clamp(get(), min, max),
                // Pinned: lives on the always-white overlay, independent of the theme.
                BackColor = Color.White, ForeColor = Color.Black,
            };
            numeric.ValueChanged += (_, _) => set(numeric.Value);
            _subscriptions.Add(settings.WhenChanged(propertyName, () =>
            {
                decimal clamped = Math.Clamp(get(), min, max);
                if (numeric.Value != clamped)
                    numeric.Value = clamped;
            }));
            Controls.Add(label);
            Controls.Add(numeric);
            y += numeric.PreferredSize.Height + rowGap;
        }

        AddCheckRow("Show cursor position", () => settings.ShowCursorPosition,
            v => settings.ShowCursorPosition = v, nameof(AlignSettings.ShowCursorPosition));
        AddCheckRow("Show diagonal lines", () => settings.ShowDiagonalLines,
            v => settings.ShowDiagonalLines = v, nameof(AlignSettings.ShowDiagonalLines));
        AddCheckRow("Show horizontal lines", () => settings.ShowHorizontalLines,
            v => settings.ShowHorizontalLines = v, nameof(AlignSettings.ShowHorizontalLines));
        AddCheckRow("Show vertical lines", () => settings.ShowVerticalLines,
            v => settings.ShowVerticalLines = v, nameof(AlignSettings.ShowVerticalLines));
        AddCheckRow("Antialiasing", () => settings.Antialiasing,
            v => settings.Antialiasing = v, nameof(AlignSettings.Antialiasing));
        AddNumericRow("Line spacing", 50, 960, 0,
            () => settings.LineSpacing, v => settings.LineSpacing = (int)v, nameof(AlignSettings.LineSpacing));
        AddNumericRow("Line thickness", 0, 100, 0,
            () => (decimal)settings.LineThickness, v => settings.LineThickness = (float)v,
            nameof(AlignSettings.LineThickness));
        AddCheckRow("Show info box", () => settings.ShowInfoBox,
            v => settings.ShowInfoBox = v, nameof(AlignSettings.ShowInfoBox));

        y += (int)(8 * scale);
        Button AddButton(string caption, Action onClick)
        {
            var button = new Button
            {
                Text = caption, Font = font,
                Size = new Size((int)(220 * scale), (int)(30 * scale)),
                Location = new Point(x, y),
            };
            ModernTheme.StyleOverlayButton(button);
            button.Click += (_, _) => onClick();
            Controls.Add(button);
            y += button.Height + rowGap;
            return button;
        }

        AddButton("Apply", coordinator.RequestApply);
        AddButton("Close", coordinator.RequestClose);
        AddButton("Reset", coordinator.RequestReset);

        Size = new Size((int)(244 * scale), y + (int)(8 * scale));
        Disposed += (_, _) =>
        {
            foreach (IDisposable s in _subscriptions)
                s.Dispose();
            _subscriptions.Clear();
            font.Dispose();
        };
    }
}
