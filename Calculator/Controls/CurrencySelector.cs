using Calculator.Core;

namespace Calculator.Controls;

/// <summary>A native list popup with independent highlighted and committed values.</summary>
public sealed class CurrencySelector : StyledButton
{
    private readonly ToolStripDropDown popup = new() { AutoClose = true, Padding = new Padding(1), Margin = Padding.Empty };
    private readonly ListBox list = new() { BorderStyle = BorderStyle.None, IntegralHeight = false, Font = Ui.Font(12), DrawMode = DrawMode.OwnerDrawFixed };
    private readonly ToolStripControlHost host;
    private readonly CurrencyTypeAhead search = new();
    private List<Option> options = [];
    private bool currencyNames = true;
    private bool populating;
    private ThemePalette palette = ThemePalette.Create("light", "BLUE");
    public string? CommittedCode { get; private set; }
    public string? PreviewCode { get; private set; }
    public bool PopupOpen => popup.Visible;
    public event EventHandler? SelectionCommitted;

    public CurrencySelector()
    {
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleLeft;
        Font = Ui.Font(14);
        Cursor = Cursors.Hand;
        Padding = new Padding(5, 0, 20, 0);
        AccessibleRole = AccessibleRole.ComboBox;
        host = new ToolStripControlHost(list) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty };
        popup.Items.Add(host);
        Ui.WatchPopup(popup);
        Click += (_, _) => OpenPopup();
        popup.Closed += (_, _) => { search.Reset(); PreviewCode = CommittedCode; Invalidate(); };
        list.SelectedIndexChanged += (_, _) =>
        {
            if (!populating && list.SelectedItem is Option selected) PreviewCode = selected.Code;
        };
        list.MouseClick += (_, e) =>
        {
            int index = list.IndexFromPoint(e.Location);
            if (index >= 0) { list.SelectedIndex = index; CommitPreview(); }
        };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitPreview(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; popup.Close(ToolStripDropDownCloseReason.Keyboard); Select(); }
        };
        list.PreviewKeyDown += (_, e) => { if (e.KeyCode is Keys.Enter or Keys.Escape) e.IsInputKey = true; };
        list.KeyPress += (_, e) =>
        {
            if (currencyNames && char.IsLetter(e.KeyChar)) { TypeCharacter(e.KeyChar); e.Handled = true; }
        };
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= options.Count) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? palette.SoftAccent : palette.Surface);
            e.Graphics.FillRectangle(background, e.Bounds);
            var bounds = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(e.Graphics, options[e.Index].Label, list.Font, bounds, palette.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        };
    }

    public void SetCodes(IEnumerable<string> codes, string? selected) =>
        SetOptions(CurrencyPresentation.SortedCodes(codes).Select(code => (code, CurrencyPresentation.ForCode(code).Label)), selected, true);

    public void SetOptions(IEnumerable<(string Code, string Label)> values, string? selected, bool useCurrencyNames = false)
    {
        string? oldPreview = PreviewCode;
        currencyNames = useCurrencyNames;
        options = values.Select(value => new Option(value.Code, value.Label)).ToList();
        CommittedCode = options.Any(option => option.Code == selected) ? selected : options.FirstOrDefault()?.Code;
        PreviewCode = popup.Visible && options.Any(option => option.Code == oldPreview) ? oldPreview : CommittedCode;
        populating = true;
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            list.Items.AddRange(options.Cast<object>().ToArray());
            list.SelectedIndex = options.FindIndex(option => option.Code == PreviewCode);
        }
        finally { list.EndUpdate(); populating = false; }
        RenderCommitted();
    }

    public void SetCommitted(string? code)
    {
        if (!options.Any(option => option.Code == code)) return;
        CommittedCode = code;
        if (!popup.Visible) PreviewCode = code;
        RenderCommitted();
    }

    private void RenderCommitted()
    {
        Text = options.FirstOrDefault(option => option.Code == CommittedCode)?.Label ?? "";
        AccessibleDescription = Text;
        Invalidate();
    }

    public void OpenPopup()
    {
        if (popup.Visible || options.Count == 0) return;
        PreviewCode = CommittedCode;
        search.Reset();
        populating = true;
        list.SelectedIndex = options.FindIndex(option => option.Code == PreviewCode);
        populating = false;
        int rowHeight = Scale(29);
        int measured = options.Max(option => TextRenderer.MeasureText(option.Label, list.Font).Width) + Scale(36);
        int width = Math.Min(Math.Max(Width, measured), Screen.FromControl(this).WorkingArea.Width - Scale(24));
        int height = Math.Min(options.Count, 10) * rowHeight;
        list.ItemHeight = rowHeight;
        list.Size = new Size(width, Math.Max(rowHeight, height));
        host.Size = list.Size;
        popup.Show(this, new Point(0, Height));
        list.Focus();
    }

    private void TypeCharacter(char character)
    {
        string? next = search.Find(options.Select(option => option.Code).ToArray(), PreviewCode, character.ToString(), Environment.TickCount64);
        int index = options.FindIndex(option => option.Code == next);
        if (index >= 0) list.SelectedIndex = index;
    }

    private void CommitPreview()
    {
        if (list.SelectedItem is not Option selected) return;
        bool changed = CommittedCode != selected.Code;
        CommittedCode = PreviewCode = selected.Code;
        RenderCommitted();
        popup.Close(ToolStripDropDownCloseReason.ItemClicked);
        Select();
        if (changed) SelectionCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is Keys.F4 or Keys.Down or (Keys.Alt | Keys.Down)) { OpenPopup(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (currencyNames && char.IsLetter(e.KeyChar)) { OpenPopup(); TypeCharacter(e.KeyChar); e.Handled = true; }
        base.OnKeyPress(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int x = Width - Scale(13), y = Height / 2;
        using var pen = new Pen(ForeColor, Math.Max(1, DeviceDpi / 96f));
        e.Graphics.DrawLines(pen, [new Point(x - Scale(4), y - Scale(2)), new Point(x, y + Scale(2)), new Point(x + Scale(4), y - Scale(2))]);
    }

    public void ApplyTheme(ThemePalette value)
    {
        palette = value;
        value.Apply(this);
        BackColor = value.Background;
        FlatAppearance.BorderSize = 0;
        list.BackColor = value.Surface;
        list.ForeColor = value.Text;
        popup.Renderer = new PaletteRenderer(value);
        popup.BackColor = value.Surface;
        list.Invalidate();
    }

    private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    protected override void Dispose(bool disposing)
    {
        if (disposing) popup.Dispose();
        base.Dispose(disposing);
    }
    private sealed record Option(string Code, string Label) { public override string ToString() => Label; }
}
