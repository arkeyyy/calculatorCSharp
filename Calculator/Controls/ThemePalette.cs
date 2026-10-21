namespace Calculator.Controls;

public sealed record ThemePalette(bool IsDark, Color Background, Color Surface, Color Secondary,
    Color Text, Color Muted, Color Accent, Color AccentText, Color SoftAccent, Color Border, Color Error)
{
    public static readonly string[] AccentNames = ["BLUE", "PURPLE", "PINK", "TEAL", "YELLOW", "CORAL"];
    public static ThemePalette Create(string theme, string accent)
    {
        Color seed = ColorTranslator.FromHtml(accent switch
        {
            "PURPLE" => "#A25BFF", "PINK" => "#FA70AB", "TEAL" => "#04BB9F",
            "YELLOW" => "#FFCC38", "CORAL" => "#FF8066", _ => "#3566F0"
        });
        bool dark = theme == "dark";
        Color bg = ColorTranslator.FromHtml(dark ? "#202024" : "#F3F4F8");
        return new(dark, bg, ColorTranslator.FromHtml(dark ? "#34343B" : "#FFFFFF"),
            ColorTranslator.FromHtml(dark ? "#2B2B31" : "#E9EBF2"),
            ColorTranslator.FromHtml(dark ? "#F5F5FA" : "#1B1E2B"),
            ColorTranslator.FromHtml(dark ? "#B8BBCB" : "#585E73"), seed,
            ContrastInk(seed), Blend(bg, seed, dark ? .24 : .14),
            ColorTranslator.FromHtml(dark ? "#555560" : "#CDD1DE"),
            ColorTranslator.FromHtml(dark ? "#FFB0B0" : "#A82132"));
    }
    public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R * (1-t) + b.R*t), (int)(a.G*(1-t)+b.G*t), (int)(a.B*(1-t)+b.B*t));
    public static Color ContrastInk(Color c)
    {
        static double Linear(byte v) { double d = v/255d; return d <= .04045 ? d/12.92 : Math.Pow((d+.055)/1.055,2.4); }
        double l = .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        return (l+.05)/.05 > 1.05/(l+.05) ? Color.Black : Color.White;
    }
    public void Apply(Control root)
    {
        root.BackColor = Background; root.ForeColor = Text;
        foreach (Control child in root.Controls) Apply(child);
        if (root is TextBoxBase) { root.BackColor = Surface; root.ForeColor = Text; }
        if (root is ComboBox or ListBox or NumericUpDown) root.BackColor = Surface;
        if (root is Button button)
        {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = SoftAccent;
            button.FlatAppearance.MouseDownBackColor = Accent;
            button.BackColor = Surface; button.ForeColor = Text;
        }
        if (root.Tag as string == "muted") root.ForeColor = Muted;
        if (root.Tag as string == "error") root.ForeColor = Error;
        if (root.Tag as string == "primary") { root.BackColor = Accent; root.ForeColor = AccentText; }
        if (root is IThemedControl themed) themed.ApplyPalette(this);
    }
}

public sealed class PaletteRenderer(ThemePalette palette) : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(palette.Surface);
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.Item.Selected ? palette.SoftAccent : palette.Surface);
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = palette.Text; base.OnRenderItemText(e);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
}
