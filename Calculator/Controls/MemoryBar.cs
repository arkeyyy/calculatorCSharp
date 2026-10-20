using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Controls;

public sealed class MemoryBar : Panel
{
    private readonly SessionStore session;
    private readonly Button clear, recall;
    public MemoryBar(SessionStore session, Func<EDecimal?> value, Action<EDecimal> restore, Action<string> error)
    {
        this.session=session;Name="memory-bar";
        void Run(Action action) {try {action();}catch(CalculationException e){error(e.Message);}}
        clear=Ui.Button("MC",session.ClearMemory,"memory-clear");
        recall=Ui.Button("MR",()=> {if(session.Memory is not null) restore(session.Memory);},"memory-recall");
        Controls.AddRange([clear,recall,
            Ui.Button("MS",()=>Run(()=>{if(value() is { } v)session.StoreMemory(v);}),"memory-store"),
            Ui.Button("M+",()=>Run(()=>{if(value() is { } v)session.AddMemory(v);}),"memory-add"),
            Ui.Button("M−",()=>Run(()=>{if(value() is { } v)session.SubtractMemory(v);}),"memory-subtract")]);
        foreach(Control c in Controls) Ui.SetFont(c,12);
        session.Changed+=RefreshMemory; RefreshMemory();
    }
    private void RefreshMemory() => clear.Enabled=recall.Enabled=session.Memory is not null;
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);int count=Controls.Count;
        for(int i=0;i<count;i++) Controls[i].SetBounds(i*Width/count,0,(i+1)*Width/count-i*Width/count,Height);
    }
    protected override void Dispose(bool disposing) {if(disposing)session.Changed-=RefreshMemory;base.Dispose(disposing);}
}
