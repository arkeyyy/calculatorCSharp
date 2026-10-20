namespace Calculator.Controls;

public sealed class CommandMenu : Button
{
    private readonly ContextMenuStrip menu = new() { ShowImageMargin=false };
    private bool commandCommitted;
    private readonly Action restoreFocus;
    public CommandMenu(string text,string name,IEnumerable<string> commands,Action<string> command,Action restoreFocus)
    {
        Text=text+" ▾";Name=name;AccessibleName=text;Font=Ui.Font(12);FlatStyle=FlatStyle.Flat;
        this.restoreFocus=restoreFocus;
        foreach(string token in commands)
        {
            var item=new ToolStripMenuItem(token) {Name=name+"-"+token};
            item.Click+=(_,_)=>
            {
                // Close before executing so native popup teardown cannot reclaim calculator focus.
                commandCommitted=true;menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                command(token);restoreFocus();
            };
            menu.Items.Add(item);
        }
        Ui.WatchPopup(menu);
        menu.Closed+=(_,_)=>{if(commandCommitted){commandCommitted=false;restoreFocus();}};
        Click+=(_,_)=>Open();
    }
    public void Open() {commandCommitted=false;menu.Show(this,new Point(0,Height));menu.Items[0].Select();}
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if(keyData is Keys.Down or (Keys.Alt|Keys.Down)) {Open();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    public void ApplyTheme(ThemePalette palette) {menu.Renderer=new PaletteRenderer(palette);palette.Apply(this);}
    protected override void Dispose(bool disposing) {if(disposing)menu.Dispose();base.Dispose(disposing);}
}
