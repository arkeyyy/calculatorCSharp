namespace Calculator.Controls;

public static class Ui
{
    public static int PopupDepth { get; set; }
    public static Font Font(float logicalPixels, FontStyle style = FontStyle.Regular) => new("Segoe UI", logicalPixels * .75f, style, GraphicsUnit.Point);
    public static Label Label(string text, float size = 14, string? name = null) => new()
    {
        Text = text, Font = Font(size), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
        Name = name ?? "", AccessibleName = text, AutoEllipsis = true, Margin = Padding.Empty
    };
    public static Button Button(string text, Action click, string? name = null)
    {
        var button = new Button { Text = text, Name = name ?? text, AccessibleName = text,
            Font = Font(14), FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
            Margin = Padding.Empty, Cursor = Cursors.Hand };
        button.Click += (_, _) => click(); return button;
    }
    public static TextBox TextField(string name, bool readOnly = false, float size = 24) => new()
    {
        Name = name, AccessibleName = name.Replace('-', ' '), ReadOnly = readOnly, BorderStyle = BorderStyle.None,
        Font = Font(size), TextAlign = HorizontalAlignment.Right, HideSelection = false,
        ShortcutsEnabled = true, Margin = Padding.Empty
    };
    public static void SetFont(Control c, float pixels, FontStyle style = FontStyle.Regular)
    {
        float points = pixels*.75f;
        if (Math.Abs(c.Font.SizeInPoints - points) < .1 && c.Font.Style == style) return;
        Font old = c.Font; c.Font = Font(pixels, style); old.Dispose();
    }
    public static void WatchPopup(ToolStripDropDown popup)
    {
        bool counted = false;
        popup.Opened += (_, _) => { if (!counted) { counted = true; PopupDepth++; } };
        void Closed(object? sender, EventArgs args)
        {
            if (!counted) return;
            counted = false;
            PopupDepth = Math.Max(0, PopupDepth - 1);
        }
        popup.Closed += Closed;
        popup.Disposed += Closed;
    }
}

public static class KeyCommands
{
    public static string? FromKey(Keys data)
    {
        Keys code = data & Keys.KeyCode, mods = data & Keys.Modifiers;
        if ((mods & (Keys.Control | Keys.Alt)) != 0) return null;
        bool shift = (mods & Keys.Shift) != 0;
        if (code is >= Keys.NumPad0 and <= Keys.NumPad9) return ((int)code-(int)Keys.NumPad0).ToString();
        if (!shift && code is >= Keys.D0 and <= Keys.D9) return ((int)code-(int)Keys.D0).ToString();
        return code switch
        {
            Keys.Add => "+", Keys.Subtract => "−", Keys.Multiply => "×", Keys.Divide => "÷",
            Keys.Decimal or Keys.OemPeriod => ".", Keys.Enter => "=", Keys.Back => "⌫",
            Keys.Escape => "C", Keys.Delete => "CE", Keys.Oemplus => shift ? "+" : "=",
            Keys.OemMinus => "−", Keys.OemQuestion when !shift => "÷",
            Keys.D9 when shift => "(", Keys.D0 when shift => ")", Keys.D8 when shift => "×",
            Keys.D6 when shift => "^", Keys.D5 when shift => "%", Keys.D1 when shift => "!", _ => null
        };
    }
}
