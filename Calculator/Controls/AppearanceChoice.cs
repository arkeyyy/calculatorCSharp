using System.Drawing.Drawing2D;

namespace Calculator.Controls;

/// <summary>A native radio choice with a labeled swatch and an explicit checked mark.</summary>
public sealed class AppearanceChoice : RadioButton, IThemedControl
{
    private readonly string? accent;
    private ThemePalette palette = ThemePalette.Create("light", "BLUE");
    private bool hovering, pressed;

    public AppearanceChoice(string label, string? accent = null)
    {
        this.accent = accent;
        Text = AccessibleName = label;
        AutoSize = false;
        Font = Ui.Font(14);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void ApplyPalette(ThemePalette value)
    {
        palette = value;
        BackColor = palette.Surface;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        float scale = DeviceDpi / 96f;
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? palette.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color background = Checked ? palette.SoftAccent : palette.Surface;
        if (Enabled && (hovering || pressed)) background = ThemePalette.Blend(background, palette.Text, pressed ? .12 : .06);
        using var path = UiStyle.RoundedRectangle(new RectangleF(.5f, .5f, Width - 1, Height - 1), UiStyle.CornerRadius * scale);
        using (var brush = new SolidBrush(background)) g.FillPath(brush, path);
        using (var pen = new Pen(Checked ? palette.Accent : palette.Border)) g.DrawPath(pen, path);

        float diameter = 16 * scale, x = 10 * scale, y = (Height - diameter) / 2;
        Color circle = accent is null ? (Checked ? palette.Accent : background) : ThemePalette.Create(palette.IsDark ? "dark" : "light", accent).Accent;
        using (var brush = new SolidBrush(circle)) g.FillEllipse(brush, x, y, diameter, diameter);
        using (var pen = new Pen(accent is null && !Checked ? palette.Muted : circle, Math.Max(1, scale))) g.DrawEllipse(pen, x, y, diameter, diameter);
        if (Checked)
        {
            using var check = new Pen(ThemePalette.ContrastInk(circle), Math.Max(1.5f, 1.5f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(check, [new PointF(x + 4 * scale, y + 8 * scale), new PointF(x + 7 * scale, y + 11 * scale), new PointF(x + 12 * scale, y + 5 * scale)]);
        }
        int textLeft = (int)Math.Ceiling(x + diameter + 8 * scale);
        var textBounds = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft - (int)Math.Round(8 * scale)), Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? palette.Text : palette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
        {
            float inset = 2 * scale;
            using var focus = UiStyle.RoundedRectangle(new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset), 4 * scale);
            using var pen = new Pen(palette.Text, Math.Max(1, 1.5f * scale));
            g.DrawPath(pen, focus);
        }
    }

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) pressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; base.OnKeyDown(e); Invalidate(); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; base.OnKeyUp(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { hovering = pressed = false; base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnChangeUICues(UICuesEventArgs e) { base.OnChangeUICues(e); Invalidate(); }
}
