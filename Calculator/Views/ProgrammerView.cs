using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;

namespace Calculator.Views;

public sealed class ProgrammerView : ModeView
{
    private readonly ProgrammerEngine engine=new();
    private readonly ComboBox radix=new(){Name="programmer-base",AccessibleName="Number base",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly ComboBox word=new(){Name="programmer-word",AccessibleName="Word size",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly Label expression=Ui.Label("",11,"programmer-expression");
    private readonly TextBox display=Ui.TextField("programmer-display",true,34);
    private readonly Label bases=Ui.Label("",11,"programmer-bases");
    private readonly Button expand;
    private readonly Panel bits=new(){Name="programmer-bit-scroll",AutoScroll=true,TabStop=false};
    private readonly Panel bitGrid=new(){Name="programmer-bit-editor"};
    private readonly CommandMenu bitwise;
    private readonly KeypadControl keypad;
    private bool expanded;
    public ProgrammerView(SessionStore session,SettingsStore settings)
    {
        Name="programmer-view";
        expression.TextAlign=ContentAlignment.MiddleRight;expression.Tag="muted";expression.AccessibleName="Programmer expression";
        radix.Items.AddRange(Enum.GetNames<NumberBase>());word.Items.AddRange(Enum.GetNames<WordSize>());
        radix.SelectedItem=settings.Get("base","DEC");if(radix.SelectedIndex<0)radix.SelectedItem="DEC";
        word.SelectedItem=settings.Get("wordSize","QWORD");if(word.SelectedIndex<0)word.SelectedItem="QWORD";
        void Configure()
        {
            engine.Configure(Enum.Parse<NumberBase>(radix.Text),Enum.Parse<WordSize>(word.Text));
            settings.Put("base",radix.Text);settings.Put("wordSize",word.Text);RefreshDisplay();
        }
        engine.Configure(Enum.Parse<NumberBase>(radix.Text),Enum.Parse<WordSize>(word.Text));
        radix.SelectedIndexChanged+=(_,_)=>Configure();word.SelectedIndexChanged+=(_,_)=>Configure();
        engine.Calculated+=(expression,result)=>session.AddHistory(ModeId.PROGRAMMER,expression,result);
        bitwise=new("Bitwise","programmer-bitwise",["AND","OR","XOR","NOT","<<",">>",">>>"],Press,FocusInput);
        expand=Ui.Button("▸ Bit editor",()=>{expanded=!expanded;expand!.Text=expanded?"▾ Bit editor":"▸ Bit editor";PerformLayout();},"programmer-expand-bits");
        bits.Controls.Add(bitGrid);bits.Visible=false;
        for(int i=0;i<64;i++)
        {
            int bit=i;var button=Ui.Button("0",()=>{engine.ToggleBit(bit);RefreshDisplay();},"bit-"+bit);
            button.AccessibleName="Bit "+bit;button.Tag=bit;Ui.SetFont(button,12);bitGrid.Controls.Add(button);
        }
        keypad=new([
            ["A","CE","AC","⌫","mod"],["B","BIN","DEC","HEX","÷"],["C","7","8","9","×"],
            ["D","4","5","6","−"],["E","1","2","3","+"],["F","±","0","","="]],Press);
        Controls.AddRange([radix,word,bitwise,expression,display,bases,expand,bits,keypad]);RefreshDisplay();
    }
    private void Press(string key)
    {
        if(key is "BIN" or "DEC" or "HEX") {radix.SelectedItem=key;return;}
        engine.Press(key=="AC"?"Clear":key);RefreshDisplay();
    }
    private void RefreshDisplay()
    {
        expression.Text=engine.Expression;expression.Visible=expression.Text.Length>0;
        display.Text=engine.Display;display.ForeColor=engine.Error is null?Palette.Text:Palette.Error;
        float displaySize=display.TextLength>22?20:display.TextLength>14?26:34;
        Ui.SetFont(display,expression.Text.Length>0?Math.Min(displaySize,22):displaySize,FontStyle.Bold);
        bases.Text=$"HEX  {engine.Format(NumberBase.HEX)}    OCT  {engine.Format(NumberBase.OCT)}\nDEC  {engine.Format(NumberBase.DEC)}    BIN  {engine.Format(NumberBase.BIN)}";
        if(keypad is null)return;
        foreach(Button key in keypad.Buttons)
        {
            if(key.Text.Length==1&&"0123456789ABCDEF".Contains(key.Text))
            {
                key.Enabled="0123456789ABCDEF".IndexOf(key.Text,StringComparison.Ordinal)<(int)engine.Base;
                if("ABCDEF".Contains(key.Text))key.AccessibleName="Hexadecimal "+key.Text;
            }
        }
        foreach(Button button in bitGrid.Controls)
        {
            int bit=(int)button.Tag!;bool on=(engine.Unsigned(engine.Value)&(System.Numerics.BigInteger.One<<bit))!=0;
            button.Visible=bit<(int)engine.Size;button.Text=on?"1":"0";
            button.BackColor=on?Palette.SoftAccent:Palette.Secondary;button.ForeColor=Palette.Text;
            button.AccessibleName=$"Bit {bit}, {(on?1:0)}";
        }
        PerformLayout();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(keypad is null)return;
        int gap=ScaleLogical(6),y=0;
        radix.SetBounds(0,0,ScaleLogical(66),ScaleLogical(28));word.SetBounds(ScaleLogical(72),0,ScaleLogical(82),ScaleLogical(28));
        bitwise.SetBounds(ScaleLogical(160),0,Math.Max(0,Width-ScaleLogical(160)),ScaleLogical(28));y+=ScaleLogical(34);
        int expressionHeight=expression.Text.Length>0?ScaleLogical(14):0;
        expression.SetBounds(0,y,Width,expressionHeight);
        display.SetBounds(0,y+expressionHeight,Width,ScaleLogical(42)-expressionHeight);y+=ScaleLogical(44);
        bases.Visible=!expanded;bits.Visible=expanded;
        if(!expanded){bases.SetBounds(0,y,Width,ScaleLogical(32));y+=ScaleLogical(34);}
        expand.SetBounds(0,y,Width,ScaleLogical(24));y+=ScaleLogical(28);
        if(expanded)
        {
            int minimumKeys=ScaleLogical(234),height=Math.Clamp(Height-y-minimumKeys-gap,ScaleLogical(28),ScaleLogical(112));
            bits.SetBounds(0,y,Width,height);y+=height+gap;
            int cellHeight=ScaleLogical(30),width=Math.Max(0,bits.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);
            bitGrid.Size=new(width,((int)engine.Size/8)*cellHeight);
            bits.AutoScrollMinSize=new(0,bitGrid.Height);
            bitGrid.Location=bits.AutoScrollPosition;
            for(int i=0;i<(int)engine.Size;i++)
            {
                var b=bitGrid.Controls[(int)engine.Size-1-i];int col=i%8,row=i/8;
                b.SetBounds(col*width/8,row*cellHeight,(col+1)*width/8-col*width/8-ScaleLogical(2),cellHeight-ScaleLogical(2));
            }
        }
        keypad.SetBounds(0,y,Width,Math.Max(0,Height-y));
    }
    public override bool HandleKey(Keys key)
    {
        Keys code=key&Keys.KeyCode;
        if((key&Keys.Modifiers)==Keys.None&&code>=Keys.A&&code<=Keys.F){Press(((char)code).ToString());return true;}
        if(KeyCommands.FromKey(key) is not {} command)return false;
        Press(command=="C"?"AC":command);return true;
    }
    public override string CopyValue()=>engine.Error is null?engine.Format(engine.Base):"";
    public override void Paste(string text){engine.Paste(text);RefreshDisplay();}
    public void Recall(string text){engine.Recall(text);RefreshDisplay();}
    public override void ApplyTheme(ThemePalette p){base.ApplyTheme(p);bitwise.ApplyTheme(p);RefreshDisplay();}
}
