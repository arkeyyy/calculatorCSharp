using System.Drawing.Drawing2D;

namespace Calculator.Controls;

/// <summary>A measured settings card with a native, keyboard-operable disclosure button.</summary>
public sealed class SettingsSection : Panel, IThemedControl
{
    private readonly bool expandable;
    private readonly Control header;
    private ThemePalette palette = ThemePalette.Create("light", "BLUE");
    private bool expanded;
    private int bodyHeight;

    public SettingsSection(string title, string name, bool expandable = true)
    {
        Name = name;
        this.expandable = expandable;
        expanded = !expandable;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        if (expandable)
        {
            header = new SectionHeader(this)
            {
                Text = title, AccessibleName = title, Name = name + "-toggle", TabStop = true,
                Font = Ui.Font(16, FontStyle.Bold)
            };
            header.Click += (_, _) => Expanded = !Expanded;
        }
        else
        {
            header = Ui.Label(title, 16, name + "-title");
            header.Font = Ui.Font(16, FontStyle.Bold);
        }
        Body = new Panel { Name = name + "-body", TabStop = false, Visible = expanded };
        Controls.AddRange([header, Body]);
    }

    public Panel Body { get; }
    public Func<int, int>? MeasureBody { get; set; }
    public event EventHandler? ExpandedChanged;
    public bool Expanded
    {
        get => expanded;
        set
        {
            if (!expandable || expanded == value) return;
            if (!value && Body.ContainsFocus) header.Focus();
            expanded = value;
            Body.Visible = value;
            header.Invalidate();
            if (header is SectionHeader toggle && header.IsHandleCreated) toggle.NotifyExpandedChanged();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int Scale(int logical) => (int)Math.Round(logical * DeviceDpi / 96d);
    public int MeasureHeight(int width)
    {
        int inner = Math.Max(0, width - 2 * Scale(UiStyle.CardPadding));
        bodyHeight = Math.Max(0, MeasureBody?.Invoke(inner) ?? 0);
        return Scale(52) + (expanded ? bodyHeight + Scale(UiStyle.CardPadding) : 0);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (header is null || Body is null) return;
        int headerInset = Scale(expandable ? 4 : UiStyle.CardPadding);
        header.SetBounds(headerInset, Scale(4), Math.Max(0, Width - 2 * headerInset), Scale(44));
        Body.SetBounds(Scale(UiStyle.CardPadding), Scale(52), Math.Max(0, Width - 2 * Scale(UiStyle.CardPadding)), bodyHeight);
    }

    public void ApplyPalette(ThemePalette value)
    {
        palette = value;
        BackColor = value.Surface;
        Body.BackColor = value.Surface;
        foreach (Control child in Body.Controls)
        {
            child.BackColor = value.Surface;
            if (child is Panel panel)
                foreach (Control grandchild in panel.Controls) grandchild.BackColor = value.Surface;
        }
        header.BackColor = value.Surface;
        if (header is SectionHeader toggle)
        {
            toggle.FlatAppearance.BorderSize = 0;
            toggle.ApplyPalette(value);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        e.Graphics.Clear(Parent?.BackColor ?? palette.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = UiStyle.RoundedRectangle(new RectangleF(.5f, .5f, Width - 1, Height - 1), UiStyle.CornerRadius * DeviceDpi / 96f);
        using (var brush = new SolidBrush(palette.Surface)) e.Graphics.FillPath(brush, path);
        using var pen = new Pen(palette.Border);
        e.Graphics.DrawPath(pen, path);
    }

    private sealed class SectionHeader(SettingsSection section) : StyledButton
    {
        protected override void DrawContent(PaintEventArgs e, Rectangle bounds, Color foreground)
        {
            int padding = section.Scale(12), trailing = section.expandable ? section.Scale(28) : padding;
            var textBounds = new Rectangle(padding, 0, Math.Max(0, Width - padding - trailing), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (!section.expandable) return;
            float scale = DeviceDpi / 96f, x = Width - 16 * scale, y = Height / 2f;
            using var pen = new Pen(foreground, Math.Max(1.5f, 1.5f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float direction = section.Expanded ? -1 : 1;
            e.Graphics.DrawLines(pen, [new PointF(x - 4 * scale, y - direction * 2 * scale), new PointF(x, y + direction * 2 * scale), new PointF(x + 4 * scale, y - direction * 2 * scale)]);
        }

        public void NotifyExpandedChanged() => AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessibleObject(this, section);
        private sealed class HeaderAccessibleObject(SectionHeader owner, SettingsSection section) : ButtonBaseAccessibleObject(owner)
        {
            public override AccessibleRole Role => section.expandable ? AccessibleRole.PushButton : AccessibleRole.StaticText;
            public override string DefaultAction => section.expandable ? (section.Expanded ? "Collapse" : "Expand") : "";
            public override void DoDefaultAction() { if (section.expandable) owner.PerformClick(); }
            public override AccessibleStates State => base.State | (section.expandable ? section.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed : AccessibleStates.None);
        }
    }
}
