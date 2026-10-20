using Calculator.Core;

namespace Calculator.Controls;

public sealed class CalendarField : UserControl
{
    private readonly TextBox field=Ui.TextField("date-input",false,16);
    private readonly Button open;
    private readonly MonthCalendar calendar=new(){MaxSelectionCount=1};
    private readonly ToolStripDropDown popup=new(){Padding=Padding.Empty};
    public event EventHandler? ValueChanged;
    public string DateText {get=>field.Text;set=>field.Text=value;}
    public CalendarField(string name)
    {
        Name=name;AccessibleName=name;field.Name=name+"-input";field.AccessibleName=name;field.TextAlign=HorizontalAlignment.Left;
        field.Text=CalendarDate.Today.ToString();field.MaxLength=16;
        field.TextChanged+=(_,_)=>ValueChanged?.Invoke(this,EventArgs.Empty);
        open=Ui.Button("▦",Open,name+"-calendar");open.AccessibleName="Open "+name+" calendar";
        popup.Items.Add(new ToolStripControlHost(calendar){Margin=Padding.Empty,Padding=Padding.Empty});Ui.WatchPopup(popup);
        calendar.DateSelected+=(_,e)=>{field.Text=CalendarDate.FromDateTime(e.Start).ToString();popup.Close();};
        Controls.AddRange([field,open]);
    }
    private void Open()
    {
        try {var date=CalendarDate.Parse(field.Text).ToDateTime();calendar.SetDate(date<calendar.MinDate?calendar.MinDate:date>calendar.MaxDate?calendar.MaxDate:date);}
        catch(Exception e) when(e is FormatException or ArgumentOutOfRangeException) {calendar.SetDate(DateTime.Today);}
        popup.Show(this,new Point(0,Height));calendar.Focus();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);int size=(int)Math.Round(34*DeviceDpi/96d);
        field.SetBounds(0,Math.Max(0,(Height-field.PreferredHeight)/2),Math.Max(0,Width-size-4),field.PreferredHeight);
        open.SetBounds(Width-size,0,size,Height);
    }
    protected override void Dispose(bool disposing){if(disposing){popup.Dispose();calendar.Dispose();}base.Dispose(disposing);}
}
