using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using PeterO.Numbers;

namespace Calculator.Views;

public sealed class ScientificView : ModeView
{
    private readonly ScientificEngine engine=new();
    private readonly SessionStore session;
    private readonly TextBox expression=Ui.TextField("scientific-expression",false,18);
    private readonly TextBox display=Ui.TextField("scientific-display",true,36);
    private readonly ComboBox angle=new(){Name="scientific-angle",AccessibleName="Angle unit",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ExpressionEditor editor;
    private readonly MemoryBar memory;
    private readonly CommandMenu trigonometry,functions;
    private readonly KeypadControl keypad;
    private EDecimal? value;
    public ScientificView(SessionStore session,SettingsStore settings)
    {
        this.session=session;Name="scientific-view";expression.TextAlign=HorizontalAlignment.Left;
        expression.PlaceholderText="Try sin(30) + 2^3";editor=new(expression);
        angle.Items.AddRange(["DEG","RAD"]);angle.SelectedItem=settings.Get("angle","DEG");
        if(angle.SelectedIndex<0)angle.SelectedIndex=0;
        angle.SelectedIndexChanged+=(_,_)=>settings.Put("angle",angle.Text);
        expression.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){Evaluate();e.SuppressKeyPress=true;}};
        memory=new(session,()=>value,Recall,m=>{display.Text=m;display.ForeColor=Palette.Error;});
        trigonometry=new("Trigonometry","scientific-trigonometry",["sin","cos","tan","asin","acos","atan"],Press,FocusInput);
        functions=new("Functions","scientific-functions",["x²","x³","1/x","abs","%","eˣ"],Press,FocusInput);
        keypad=new([
            ["π","e","CE","C","⌫"],["√","(",")","n!","÷"],["^","7","8","9","×"],
            ["10ˣ","4","5","6","−"],["log","1","2","3","+"],["ln","±","0",".","="]],Press);
        Controls.AddRange([expression,angle,display,memory,trigonometry,functions,keypad]);display.Text="0";
    }
    private void Press(string key)
    {
        if(key=="="){Evaluate();return;}
        editor.Insert(key);
        if(key is "C" or "CE"){value=null;display.Text="0";display.ForeColor=Palette.Text;}
    }
    private void Evaluate()
    {
        try
        {
            value=engine.Evaluate(expression.Text,angle.Text=="RAD"?AngleUnit.RAD:AngleUnit.DEG);
            display.Text=Numbers.Format(value);display.ForeColor=Palette.Text;
            session.AddHistory(ModeId.SCIENTIFIC,expression.Text+" =",display.Text);
        }
        catch(CalculationException e){value=null;display.Text=e.Message;display.ForeColor=Palette.Error;}
        Ui.SetFont(display,display.TextLength>22?18:display.TextLength>14?26:36,FontStyle.Bold);
    }
    private void Recall(EDecimal number)
    {
        value=number;expression.Text=Numbers.Format(number);expression.Select(expression.TextLength,0);editor.Remember();
        display.Text=Numbers.Format(number);display.ForeColor=Palette.Text;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(keypad is null)return;
        int d=ScaleLogical(6),selector=ScaleLogical(66),y=0;
        expression.SetBounds(0,0,Math.Max(0,Width-selector-d),ScaleLogical(28));
        angle.SetBounds(Width-selector,0,selector,ScaleLogical(28));y+=ScaleLogical(32);
        display.SetBounds(0,y,Width,ScaleLogical(44));y+=ScaleLogical(46);
        memory.SetBounds(0,y,Width,ScaleLogical(26));y+=ScaleLogical(28);
        trigonometry.SetBounds(0,y,ScaleLogical(124),ScaleLogical(26));
        functions.SetBounds(ScaleLogical(130),y,ScaleLogical(96),ScaleLogical(26));y+=ScaleLogical(32);
        keypad.SetBounds(0,y,Width,Math.Max(0,Height-y));
    }
    public override bool HandleKey(Keys key){if(KeyCommands.FromKey(key) is not { } command)return false;Press(command);return true;}
    public override string CopyValue()=>value is null?"":Numbers.Format(value);
    public override void Paste(string text)
    {
        try{Recall(Numbers.Parse(text.Trim()));}
        catch(CalculationException e){display.Text=e.Message;display.ForeColor=Palette.Error;}
    }
    public override void ApplyTheme(ThemePalette p){base.ApplyTheme(p);trigonometry.ApplyTheme(p);functions.ApplyTheme(p);}
}
