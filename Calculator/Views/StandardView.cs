using Calculator.Controls;
using Calculator.Core;

namespace Calculator.Views;

public sealed class StandardView : ModeView
{
    private readonly StandardEngine engine=new();
    private readonly SessionStore session;
    private readonly Label expression=Ui.Label("",12,"standard-expression");
    private readonly TextBox display=Ui.TextField("standard-display",true,48);
    private readonly MemoryBar memory;
    private readonly KeypadControl keypad;
    public StandardView(SessionStore session)
    {
        this.session=session;Name="standard-view";
        expression.TextAlign=ContentAlignment.MiddleRight;expression.Tag="muted";
        memory=new(session,()=>engine.Error?null:engine.Value,v=>{engine.Recall(v);RefreshDisplay();},m=>display.Text=m);
        keypad=new([
            ["%","CE","C","⌫"],["1/x","x²","√","÷"],["7","8","9","×"],
            ["4","5","6","−"],["1","2","3","+"],["±","0",".","="]],Press);
        Controls.AddRange([expression,display,memory,keypad]);RefreshDisplay();
    }
    private void Press(string key)
    {
        engine.Press(key);RefreshDisplay();
        if(engine.Completed&&!engine.Error)session.AddHistory(ModeId.STANDARD,engine.Expression,Numbers.Format(engine.Value));
    }
    private void RefreshDisplay()
    {
        display.Text=engine.Display;expression.Text=engine.Expression;
        Ui.SetFont(display,engine.Display.Length>22?24:engine.Display.Length>14?32:46,FontStyle.Bold);
        display.ForeColor=engine.Error?Palette.Error:Palette.Text;
        display.Select(display.TextLength,0);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(keypad is null)return;
        int gap=ScaleLogical(6),y=0;
        expression.SetBounds(0,y,Width,ScaleLogical(24));y+=ScaleLogical(26);
        display.SetBounds(0,y,Width,ScaleLogical(60));y+=ScaleLogical(62);
        memory.SetBounds(0,y,Width,ScaleLogical(28));y+=ScaleLogical(28)+gap;
        keypad.SetBounds(0,y,Width,Math.Max(0,Height-y));
    }
    public override bool HandleKey(Keys key) {if(KeyCommands.FromKey(key) is not { } command)return false;Press(command);return true;}
    public override string CopyValue()=>engine.Error?"":Numbers.Format(engine.Value);
    public override void Paste(string text)
    {
        try {engine.Recall(Numbers.Parse(text.Trim()));RefreshDisplay();}
        catch(CalculationException e){display.Text=e.Message;display.ForeColor=Palette.Error;}
    }
    public override void ApplyTheme(ThemePalette p){base.ApplyTheme(p);RefreshDisplay();}
}
