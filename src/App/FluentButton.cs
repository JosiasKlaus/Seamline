using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace Seamline.App;

/// <summary>Owner-painted rounded button in the Fluent style: accent fill for the
/// primary action, bordered surface for secondaries, with hover/press/disabled
/// states and theme-aware colors.</summary>
[SupportedOSPlatform("windows10.0.14393")]
internal sealed class FluentButton : Button
{
    private bool _hover;
    private bool _pressed;

    public FluentButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(96, 32);
    }

    /// <summary>Accent-filled primary action.</summary>
    [System.ComponentModel.DefaultValue(false)]
    public bool Primary { get; init; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ModernTheme.EffectiveBackColor(Parent));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill;
        Color text;
        if (!Enabled)
        {
            fill = ModernTheme.SurfaceDisabled;
            text = ModernTheme.TextSecondary;
        }
        else if (Primary)
        {
            fill = _pressed ? ModernTheme.AccentPressed : _hover ? ModernTheme.AccentHover : ModernTheme.Accent;
            text = Color.White;
        }
        else
        {
            fill = _pressed ? ModernTheme.SurfacePressed
                : _hover ? ModernTheme.SurfaceHover
                : ModernTheme.CardBackground;
            text = ModernTheme.TextPrimary;
        }

        var rect = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
        using GraphicsPath path = ModernTheme.RoundedPath(rect, 5f);
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);
        if (!Primary || !Enabled)
        {
            // Inset keeps the full stroke inside the control — a centered stroke
            // loses half a pixel at the right/bottom edges.
            using var border = new Pen(Focused ? ModernTheme.AccentForeground : ModernTheme.CardBorder)
            {
                Alignment = System.Drawing.Drawing2D.PenAlignment.Inset,
            };
            g.DrawPath(border, path);
        }
        else if (Focused)
        {
            // Win11-style inner keyline so a focused primary button has a cue too.
            using GraphicsPath inner = ModernTheme.RoundedPath(
                new RectangleF(2.5f, 2.5f, Width - 5f, Height - 5f), 3.5f);
            using var keyline = new Pen(Color.White, 1.5f);
            g.DrawPath(keyline, inner);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
