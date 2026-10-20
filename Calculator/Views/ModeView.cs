using Calculator.Controls;

namespace Calculator.Views;

public class ModeView : ContainerControl
{
    protected ThemePalette Palette { get; private set; } = ThemePalette.Create("light", "BLUE");
    public ModeView()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        AutoScaleMode=AutoScaleMode.None; TabStop=true; Font=Ui.Font(14); Margin=Padding.Empty;
    }
    public virtual bool HandleKey(Keys key) => false;
    public virtual string CopyValue() => "";
    public virtual void Paste(string text) { }
    public virtual void ActivateMode() { }
    public virtual void DeactivateMode() { }
    public virtual void ApplyTheme(ThemePalette palette)
    {
        Palette=palette;palette.Apply(this);
        foreach(var keypad in Descendants(this).OfType<KeypadControl>()) keypad.ApplyTheme(palette);
    }
    protected int ScaleLogical(int pixels) => (int)Math.Round(pixels*DeviceDpi/96d);
    public void FocusInput()
    {
        // Use ContainerControl rather than UserControl: UserControl handles WM_SETFOCUS
        // by selecting its first child, which would change expression/clipboard routing.
        ActiveControl = null;
        Select();
        ActiveControl = null;
        Focus();
    }
    public static IEnumerable<Control> Descendants(Control root)
    {
        foreach(Control c in root.Controls) {yield return c; foreach(var nested in Descendants(c))yield return nested;}
    }
}
