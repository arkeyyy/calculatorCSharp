namespace Calculator.Controls;

public sealed class ExpressionEditor
{
    private readonly TextBox field;
    private int start,length;
    private bool updating;
    public ExpressionEditor(TextBox field)
    {
        this.field=field;field.MaxLength=4096;
        field.KeyUp+=(_,_)=>Remember();field.MouseUp+=(_,_)=>Remember();field.Leave+=(_,_)=>Remember();
        field.TextChanged+=(_,_)=> {if(!updating) {start=field.SelectionStart;length=field.SelectionLength;}};
    }
    public void Remember() {if(updating)return;start=field.SelectionStart;length=field.SelectionLength;}
    public void Insert(string key)
    {
        start=Math.Clamp(start,0,field.TextLength);length=Math.Clamp(length,0,field.TextLength-start);
        string token=Token(key); string current=field.Text;
        if(key is "C" or "CE") {current="";start=length=0;token="";}
        else if(key=="⌫") {token="";if(length==0&&start>0){start--;length=1;}}
        else if(key=="±") {start=0;length=current.Length;token="-("+(current.Length==0?"0":current)+")";}
        string next=current.Remove(start,length).Insert(start,token);
        if(next.Length>4096) return;
        updating=true;
        try {field.Text=next;start+=token.Length;length=0;field.Select(start,0);}
        finally {updating=false;}
    }
    public static string Token(string key) => key switch
    {
        "×"=>"*", "÷"=>"/", "−"=>"-", "π"=>"pi", "√"=>"sqrt(", "n!"=>"!",
        "x²"=>"^2", "x³"=>"^3", "1/x"=>"1/(", "10ˣ"=>"10^", "eˣ"=>"e^",
        "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or "ln" or "log" or "abs" => key+"(", _=>key
    };
}
