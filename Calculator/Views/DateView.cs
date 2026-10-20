using Calculator.Controls;
using Calculator.Core;

namespace Calculator.Views;

public sealed class DateView : ModeView
{
    private readonly DateCalculator calculator=new();
    private readonly ComboBox operation=new(){Name="date-operation",AccessibleName="Date calculation",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly CalendarField from=new("Start date"),to=new("End date");
    private readonly Label fromLabel=Ui.Label("Start date",12),toLabel=Ui.Label("End date",12),resultCaption=Ui.Label("RESULT",12);
    private readonly Label result=Ui.Label("",23,"date-result"),error=Ui.Label("",12,"date-error");
    private readonly NumericUpDown[] adjustments=[new(){Maximum=9999},new(){Maximum=1200},new(){Maximum=100000}];
    private readonly Label[] adjustmentLabels=[Ui.Label("Years",12),Ui.Label("Months",12),Ui.Label("Days",12)];
    public DateView()
    {
        Name="date-view";
        operation.Items.AddRange(["Difference between dates","Add to a date","Subtract from a date"]);operation.SelectedIndex=0;
        operation.SelectedIndexChanged+=(_,_)=>{PerformLayout();UpdateResult();};
        from.ValueChanged+=(_,_)=>UpdateResult();to.ValueChanged+=(_,_)=>UpdateResult();
        for(int i=0;i<3;i++){adjustments[i].Name="date-"+adjustmentLabels[i].Text.ToLowerInvariant();adjustments[i].AccessibleName=adjustmentLabels[i].Text;adjustments[i].ValueChanged+=(_,_)=>UpdateResult();}
        error.Tag="error";resultCaption.Tag="muted";result.AutoEllipsis=false;
        Controls.AddRange([operation,fromLabel,from,toLabel,to,resultCaption,result,error]);
        Controls.AddRange(adjustments);Controls.AddRange(adjustmentLabels);UpdateResult();
    }
    private void UpdateResult()
    {
        try
        {
            var start=CalendarDate.Parse(from.DateText);
            if(operation.SelectedIndex==0)
            {
                var d=calculator.Between(start,CalendarDate.Parse(to.DateText));
                result.Text=$"{d.Days} days\n{d.Weeks} weeks, {d.RemainingDays} days";
            }
            else result.Text=calculator.Adjust(start,(int)adjustments[0].Value,(int)adjustments[1].Value,(int)adjustments[2].Value,operation.SelectedIndex==2).ToString();
            error.Text="";
        }
        catch(Exception e) when(e is FormatException or ArgumentOutOfRangeException or OverflowException)
        {result.Text="—";error.Text=e is FormatException?"Enter a date as YYYY-MM-DD":"Date is outside the supported range";}
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(operation is null)return;
        int y=0;operation.SetBounds(0,y,Width,ScaleLogical(30));y+=ScaleLogical(46);
        fromLabel.SetBounds(0,y,Width,ScaleLogical(20));y+=ScaleLogical(24);
        from.SetBounds(0,y,Width,ScaleLogical(34));y+=ScaleLogical(48);
        bool difference=operation.SelectedIndex==0;toLabel.Visible=to.Visible=difference;
        if(difference)
        {
            toLabel.SetBounds(0,y,Width,ScaleLogical(20));to.SetBounds(0,y+ScaleLogical(24),Width,ScaleLogical(34));
        }
        int gap=ScaleLogical(8),w=(Width-2*gap)/3;
        for(int i=0;i<3;i++)
        {
            adjustmentLabels[i].Visible=adjustments[i].Visible=!difference;
            adjustmentLabels[i].SetBounds(i*(w+gap),y,w,ScaleLogical(20));
            adjustments[i].SetBounds(i*(w+gap),y+ScaleLogical(24),w,ScaleLogical(34));
        }
        y+=ScaleLogical(80);resultCaption.SetBounds(0,y,Width,ScaleLogical(20));y+=ScaleLogical(24);
        result.SetBounds(0,y,Width,ScaleLogical(84));y+=ScaleLogical(88);
        error.SetBounds(0,y,Width,ScaleLogical(42));
    }
    public override string CopyValue()=>error.Text.Length==0?result.Text:"";
}
