namespace Calculator.Controls;

public sealed class KeypadControl : Panel
{
    private readonly int rows, columns;
    private readonly Dictionary<(int Row, int Column), Control> cells = [];
    public bool Expanding { get; set; }
    public IReadOnlyList<Button> Buttons => cells.Values.OfType<Button>().ToList();
    public int RowCount => rows;
    public KeypadControl(string[][] labels, Action<string> onKey)
    {
        DoubleBuffered = true; rows = labels.Length; columns = labels.Max(r => r.Length);
        Name = "keypad"; AccessibleName = "Calculator keypad"; TabStop = false;
        for (int r=0;r<rows;r++) for(int c=0;c<labels[r].Length;c++)
        {
            string label=labels[r][c]; if (string.IsNullOrEmpty(label)) continue;
            var button=Ui.Button(label, () => onKey(label), "key-"+label);
            button.AccessibleName = label switch { "⌫"=>"Backspace", "±"=>"Change sign", "CE"=>"Clear entry", "C" or "AC"=>"Clear all", "="=>"Equals", "√"=>"Square root", _=>label };
            button.TabIndex=r*columns+c;
            cells[(r,c)]=button; Controls.Add(button);
        }
    }
    public void SetCell(int row,int column,Control control)
    {
        if(cells.Remove((row,column), out var old)) { Controls.Remove(old); old.Dispose(); }
        cells[(row,column)]=control; Controls.Add(control); PerformLayout();
    }
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent); if(rows==0 || columns==0) return;
        int gap=(int)Math.Round(6*DeviceDpi/96d), max=(int)Math.Round(64*DeviceDpi/96d);
        int h=Expanding ? ClientSize.Height : Math.Min(ClientSize.Height, rows*max+(rows-1)*gap);
        int availableWidth=Math.Max(0,ClientSize.Width-(columns-1)*gap), availableHeight=Math.Max(0,h-(rows-1)*gap);
        foreach(var (cell,control) in cells)
        {
            int x1=cell.Column*availableWidth/columns+cell.Column*gap, x2=(cell.Column+1)*availableWidth/columns+cell.Column*gap;
            int y1=cell.Row*availableHeight/rows+cell.Row*gap, y2=(cell.Row+1)*availableHeight/rows+cell.Row*gap;
            control.SetBounds(x1,y1,Math.Max(0,x2-x1),Math.Max(0,y2-y1));
        }
    }
    public void ApplyTheme(ThemePalette palette)
    {
        palette.Apply(this);
        foreach (Button key in Buttons)
        {
            bool number=key.Text.Length==1 && "0123456789.ABCDEF".Contains(key.Text);
            key.BackColor=number ? palette.Surface : palette.Secondary;
            if("+−×÷".Contains(key.Text) && key.Text.Length==1) key.BackColor=palette.SoftAccent;
            if(key.Text is "=" or "Plot") {key.BackColor=palette.Accent;key.ForeColor=palette.AccentText;}
            Ui.SetFont(key, number || key.Text is "+" or "−" or "×" or "÷" or "=" ? 22 : 14);
        }
    }
}
