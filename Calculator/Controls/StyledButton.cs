using System.Drawing.Drawing2D;

namespace Calculator.Controls;

public interface IThemedControl
{
    void ApplyPalette(ThemePalette palette);
}

/// <summary>Shared visual metrics, expressed in logical pixels.</summary>
public static class UiStyle
{
    public const int CornerRadius = 6;
    public const int ControlGap = 8;
    public const int CardPadding = 16;
    public const int SectionGap = 12;

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) return path;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Owner-painted appearance with the native Button input and accessibility contract.</summary>
public class StyledButton : Button, IThemedControl
{
    private bool hovering, mousePressed, keyboardPressed, selected, selectionIndicator;
    protected ThemePalette Palette { get; private set; } = ThemePalette.Create("light", "BLUE");

    public StyledButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Margin = Padding.Empty;
        Font = Ui.Font(14);
        Cursor = Cursors.Hand;
    }

    public bool Selected
    {
        get => selected;
        set
        {
            if (selected == value) return;
            selected = value;
            Invalidate();
            if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
    }

    public bool SelectionIndicator
    {
        get => selectionIndicator;
        set { selectionIndicator = value; Invalidate(); }
    }

    public virtual void ApplyPalette(ThemePalette palette)
    {
        Palette = palette;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        Graphics graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? Palette.Background);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        bool primary = BackColor.ToArgb() == Palette.Accent.ToArgb();
        Color background = Selected ? Palette.SoftAccent : BackColor;
        Color foreground = Enabled ? ForeColor : Palette.Muted;
        if (Enabled && (mousePressed || keyboardPressed))
        {
            background = ThemePalette.Blend(background, primary ? Palette.AccentText : Palette.Text, Palette.IsDark ? .18 : .12);
        }
        else if (Enabled && hovering)
        {
            background = ThemePalette.Blend(background, primary ? Palette.AccentText : Palette.Text, Palette.IsDark ? .10 : .06);
        }
        if (primary && Enabled) foreground = ThemePalette.ContrastInk(background);
        if (!Enabled) background = ThemePalette.Blend(background, Parent?.BackColor ?? Palette.Background, .45);

        var outline = new RectangleF(.5f, .5f, Width - 1f, Height - 1f);
        using var path = UiStyle.RoundedRectangle(outline, UiStyle.CornerRadius * scale);
        using (var brush = new SolidBrush(background)) graphics.FillPath(brush, path);
        if (FlatAppearance.BorderSize > 0)
        {
            using var border = new Pen(FlatAppearance.BorderColor.IsEmpty ? Palette.Border : FlatAppearance.BorderColor);
            graphics.DrawPath(border, path);
        }
        if (Selected && SelectionIndicator)
        {
            float height = Math.Min(18 * scale, Height - 8 * scale);
            using var indicator = UiStyle.RoundedRectangle(new RectangleF(3 * scale, (Height - height) / 2f, 3 * scale, height), 1.5f * scale);
            using var brush = new SolidBrush(Palette.Accent);
            graphics.FillPath(brush, indicator);
        }

        var content = new Rectangle(Padding.Left, Padding.Top,
            Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        DrawContent(e, content, foreground);

        if (Focused && ShowFocusCues)
        {
            float inset = Math.Max(2, 2 * scale);
            using var focus = UiStyle.RoundedRectangle(new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset), 4 * scale);
            using var pen = new Pen(primary ? foreground : Palette.Text, Math.Max(1, 1.5f * scale));
            graphics.DrawPath(pen, focus);
        }
    }

    protected virtual void DrawContent(PaintEventArgs e, Rectangle bounds, Color foreground)
    {
        TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;
        flags |= UseMnemonic ? (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix) : TextFormatFlags.NoPrefix;
        flags |= Text.Contains('\n') ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine;
        if (AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
        flags |= TextAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };
        flags |= TextAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => TextFormatFlags.Top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
            _ => TextFormatFlags.VerticalCenter
        };
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, foreground, flags);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; mousePressed = Capture && (MouseButtons & MouseButtons.Left) != 0; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; mousePressed = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) mousePressed = true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { mousePressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) mousePressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) keyboardPressed = true; base.OnKeyDown(e); Invalidate(); }
    protected override void OnKeyUp(KeyEventArgs e) { keyboardPressed = false; base.OnKeyUp(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { keyboardPressed = false; base.OnLostFocus(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { hovering = mousePressed = keyboardPressed = false; base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnChangeUICues(UICuesEventArgs e) { base.OnChangeUICues(e); Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance() => new StyledButtonAccessibleObject(this);
    private sealed class StyledButtonAccessibleObject(StyledButton owner) : ButtonBaseAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.PushButton : owner.AccessibleRole;
        public override string DefaultAction => "Press";
        public override void DoDefaultAction() => owner.PerformClick();
        public override AccessibleStates State => base.State | (owner.Selected ? AccessibleStates.Pressed : AccessibleStates.None);
    }
}
