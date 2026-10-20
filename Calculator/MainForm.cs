using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;

namespace Calculator;

public sealed class MainForm : Form
{
    private readonly SettingsStore settings;
    private readonly SessionStore session;
    private readonly ICurrencyRateProvider? currencyRateProvider;
    private readonly Dictionary<ModeId,ModeView> views=[];
    private readonly Panel workspace=new(){Name="workspace",TabStop=false};
    private readonly Panel header=new(){Name="header"};
    private readonly Panel drawer=new(){Name="navigation",Visible=false};
    private readonly Panel history=new(){Name="history",Visible=false};
    private readonly Panel historyItems=new(){Name="history-items",AutoScroll=true};
    private readonly Label title=Ui.Label("Standard",22,"mode-title");
    private readonly Label footer=Ui.Label("",11,"status");
    private readonly Button navigation,historyButton;
    private readonly List<Button> modeButtons=[];
    private readonly Button settingsButton,clearHistory;
    private readonly Label historyTitle=Ui.Label("History",20);
    private ThemePalette palette;
    private ModeId currentMode;
    private ModeView? active;
    private SettingsView? settingsView;
    private bool showingSettings;
    private bool initialized;
    private readonly ToolTip tooltips=new();

    public MainForm(SettingsStore? settings=null,SessionStore? session=null,ICurrencyRateProvider? currencyRateProvider=null)
    {
        this.settings=settings??new SettingsStore(Environment.GetEnvironmentVariable("CALCULATOR_DATA_DIR"),Environment.GetEnvironmentVariable("CALCULATOR_SKIP_LEGACY_IMPORT")!="1");
        this.session=session??new SessionStore();
        this.currencyRateProvider=currencyRateProvider;
        palette=ThemePalette.Create(this.settings.Get("theme","light"),this.settings.Get("accent","BLUE"));
        Name="CalculatorMainWindow";Text="Calculator";StartPosition=FormStartPosition.CenterScreen;
        using (var iconStream=typeof(MainForm).Assembly.GetManifestResourceStream("Calculator.Assets.Calculator.ico"))
            if(iconStream is not null)Icon=new Icon(iconStream);
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font(14);KeyPreview=true;
        Size=new Size(Scale(400),Scale(600));MinimumSize=new Size(Scale(320),Scale(500));DoubleBuffered=true;
        navigation=Ui.Button("≡",ToggleNavigation,"navigation-toggle");navigation.AccessibleName="Open navigation";Ui.SetFont(navigation,28);
        historyButton=Ui.Button("◷",ToggleHistory,"history-toggle");historyButton.AccessibleName="History";Ui.SetFont(historyButton,24);
        title.Font=Ui.Font(22,FontStyle.Bold);tooltips.SetToolTip(historyButton,"History");tooltips.SetToolTip(navigation,"Navigation");
        header.Controls.AddRange([navigation,title,historyButton]);
        var modeList=new Panel(){Name="navigation-modes",AutoScroll=true,Dock=DockStyle.Fill};
        foreach(ModeId mode in Enum.GetValues<ModeId>())
        {
            var button=Ui.Button(mode.Title(),()=>SelectMode(mode),"navigation-"+mode);
            button.TextAlign=ContentAlignment.MiddleLeft;button.Tag=mode;modeButtons.Add(button);modeList.Controls.Add(button);
        }
        modeList.Resize+=(_,_)=>
        {
            int h=Scale(36),width=modeList.ClientSize.Width-SystemInformation.VerticalScrollBarWidth;
            for(int i=0;i<modeButtons.Count;i++)modeButtons[i].SetBounds(0,i*h,Math.Max(0,width),h);
            modeList.AutoScrollMinSize=new Size(0,modeButtons.Count*h);
        };
        settingsButton=Ui.Button("Settings",ShowSettings,"navigation-settings");settingsButton.Dock=DockStyle.Bottom;settingsButton.Height=Scale(42);
        drawer.Controls.Add(modeList);drawer.Controls.Add(settingsButton);
        clearHistory=Ui.Button("Clear",this.session.ClearHistory,"history-clear");
        history.Controls.AddRange([historyTitle,clearHistory,historyItems]);
        footer.Tag="muted";footer.Text="Ready";
        Controls.AddRange([header,workspace,footer,drawer,history]);
        this.session.Changed+=RefreshHistory;
        if(!Enum.TryParse(this.settings.Get("mode","STANDARD"),out currentMode)||!Enum.IsDefined(currentMode))currentMode=ModeId.STANDARD;
        initialized=true;SelectMode(currentMode);
        DpiChanged+=(_,_)=>{ApplyWindowRequirements();PerformLayout();};
        Shown+=(_,_)=>{ApplyWindowRequirements();active?.FocusInput();};
        FormClosing+=(_,_)=>this.settings.Flush();
    }
    private int Scale(int logical)=>(int)Math.Round(logical*DeviceDpi/96d);
    public ModeId CurrentMode=>currentMode;
    public ModeView? ActiveMode=>active;
    public void SelectMode(ModeId mode)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        showingSettings=false;currentMode=mode;settings.Put("mode",mode.ToString());
        if(!views.TryGetValue(mode,out var view))
        {
            view=mode switch
            {
                ModeId.STANDARD=>new StandardView(session),ModeId.SCIENTIFIC=>new ScientificView(session,settings),
                ModeId.PROGRAMMER=>new ProgrammerView(session,settings),ModeId.DATE=>new DateView(),
                ModeId.GRAPHING=>new GraphView(session),_=>new ConverterView(mode,settings,session,currencyRateProvider)
            };
            views.Add(mode,view);
        }
        ShowView(view,mode.Title());ApplyWindowRequirements();view.ActivateMode();
        footer.Text=mode==ModeId.GRAPHING?"Plot or Enter to apply expressions":mode.IsConverter()?"Conversions update as you type":"Ready";
        historyButton.Enabled=mode is ModeId.STANDARD or ModeId.SCIENTIFIC or ModeId.PROGRAMMER;
    }
    private void ShowView(ModeView view,string label)
    {
        active?.DeactivateMode();
        workspace.Controls.Clear();active=view;workspace.Controls.Add(view);title.Text=label;tooltips.SetToolTip(title,label);
        drawer.Visible=history.Visible=false;ApplyTheme();PerformLayout();view.FocusInput();
    }
    private void ApplyWindowRequirements()
    {
        bool graph=currentMode==ModeId.GRAPHING&&!showingSettings;
        var minimum=new Size(Scale(graph?800:320),Scale(graph?640:500));
        MinimumSize=minimum;
        if(Width<minimum.Width||Height<minimum.Height)Size=new Size(Math.Max(Width,minimum.Width),Math.Max(Height,minimum.Height));
    }
    private void ToggleNavigation(){drawer.Visible=!drawer.Visible;history.Visible=false;if(drawer.Visible){drawer.BringToFront();modeButtons[(int)currentMode].Focus();}}
    private void ToggleHistory(){history.Visible=!history.Visible;drawer.Visible=false;if(history.Visible){RefreshHistory();history.BringToFront();clearHistory.Focus();}}
    private void ShowSettings()
    {
        settingsView??=new SettingsView(settings,ApplyTheme);showingSettings=true;ShowView(settingsView,"Settings");
        historyButton.Enabled=false;footer.Text="Preferences are saved automatically";ApplyWindowRequirements();
    }
    private void ApplyTheme()
    {
        palette=ThemePalette.Create(settings.Get("theme","light"),settings.Get("accent","BLUE"));palette.Apply(this);
        active?.ApplyTheme(palette);
        foreach(var pair in views)if(pair.Value!=active)pair.Value.ApplyTheme(palette);
        foreach(Button b in modeButtons)
        {
            b.BackColor=(ModeId)b.Tag! == currentMode&&!showingSettings?palette.SoftAccent:palette.Surface;
            b.FlatAppearance.BorderSize=0;
        }
        navigation.FlatAppearance.BorderSize=historyButton.FlatAppearance.BorderSize=0;
        navigation.BackColor=historyButton.BackColor=palette.Background;
    }
    private void RefreshHistory()
    {
        if(IsDisposed)return;
        foreach(Control c in historyItems.Controls.Cast<Control>().ToArray())c.Dispose();
        int y=0;
        if(session.History.Count==0)
        {
            var empty=Ui.Label("Your calculations will appear here.",14);empty.SetBounds(0,0,historyItems.Width,Scale(52));historyItems.Controls.Add(empty);
        }
        foreach(var item in session.History)
        {
            var b=Ui.Button(item.Expression+"\n"+item.Result,()=>Recall(item),"history-item");
            b.TextAlign=ContentAlignment.MiddleRight;b.AutoEllipsis=true;b.SetBounds(0,y,Math.Max(0,historyItems.Width-SystemInformation.VerticalScrollBarWidth),Scale(66));
            historyItems.Controls.Add(b);y+=Scale(72);
        }
        historyItems.AutoScrollMinSize=new Size(0,y);palette.Apply(historyItems);
    }
    private void Recall(HistoryItem item)
    {
        SelectMode(item.Mode);
        if(active is ProgrammerView programmer)programmer.Recall(item.Result);else active?.Paste(item.Result);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(!initialized)return;
        int margin=Scale(10),headerHeight=Scale(44),footerHeight=Scale(22);
        header.SetBounds(margin,0,ClientSize.Width-2*margin,headerHeight);
        navigation.SetBounds(0,Scale(4),Scale(36),Scale(36));historyButton.SetBounds(header.Width-Scale(36),Scale(4),Scale(36),Scale(36));
        title.SetBounds(Scale(46),0,Math.Max(0,header.Width-Scale(88)),headerHeight);
        Ui.SetFont(title,ClientSize.Width<Scale(360)?18:22,FontStyle.Bold);
        workspace.SetBounds(margin,headerHeight+Scale(4),Math.Max(0,ClientSize.Width-2*margin),Math.Max(0,ClientSize.Height-headerHeight-footerHeight-Scale(8)));
        if(active is not null)
        {
            int? cap=showingSettings?560:currentMode.WorkspaceLimit();int width=cap.HasValue?Math.Min(workspace.Width,Scale(cap.Value)):workspace.Width;
            active.SetBounds((workspace.Width-width)/2,0,width,workspace.Height);
        }
        footer.SetBounds(margin,ClientSize.Height-footerHeight,ClientSize.Width-2*margin,footerHeight);
        int drawerWidth=Math.Min(Scale(270),ClientSize.Width-Scale(24));
        drawer.SetBounds(margin,headerHeight,drawerWidth,Math.Max(0,ClientSize.Height-headerHeight-margin));
        settingsButton.Height=Scale(42);
        history.SetBounds(ClientSize.Width-drawerWidth-margin,headerHeight,drawerWidth,Math.Max(0,ClientSize.Height-headerHeight-margin));
        historyTitle.SetBounds(Scale(10),0,drawerWidth-Scale(90),Scale(42));clearHistory.SetBounds(drawerWidth-Scale(70),Scale(6),Scale(62),Scale(30));
        historyItems.SetBounds(Scale(8),Scale(48),drawerWidth-Scale(16),history.Height-Scale(56));
    }
    private Control? FocusOwner()
    {
        Control? c=ActiveControl;
        while(c is ContainerControl container&&container.ActiveControl is not null)c=container.ActiveControl;
        return c;
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(Ui.PopupDepth>0)return base.ProcessCmdKey(ref msg,keyData);
        if(keyData==Keys.Escape&&(drawer.Visible||history.Visible)){drawer.Visible=history.Visible=false;active?.FocusInput();return true;}
        if(drawer.Visible||history.Visible||showingSettings)return base.ProcessCmdKey(ref msg,keyData);
        var focus=FocusOwner();
        if(focus is TextBoxBase {ReadOnly:false} or ComboBox or NumericUpDown or MonthCalendar)return base.ProcessCmdKey(ref msg,keyData);
        if(focus is CalendarField)return base.ProcessCmdKey(ref msg,keyData);
        if(keyData==(Keys.Control|Keys.C))
        {
            string text=active?.CopyValue()??"";if(text.Length>0)try{Clipboard.SetText(text);}catch(System.Runtime.InteropServices.ExternalException){footer.Text="Clipboard is busy. Try again.";}
            return true;
        }
        if(keyData==(Keys.Control|Keys.V))
        {
            try{if(Clipboard.ContainsText())active?.Paste(Clipboard.GetText());}catch(System.Runtime.InteropServices.ExternalException){footer.Text="Clipboard is busy. Try again.";}
            return true;
        }
        if(keyData is Keys.Space or Keys.Tab or (Keys.Shift|Keys.Tab))return base.ProcessCmdKey(ref msg,keyData);
        if(active?.HandleKey(keyData)==true)return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            session.Changed-=RefreshHistory;
            foreach(var view in views.Values)view.Dispose();settingsView?.Dispose();tooltips.Dispose();settings.Flush();
        }
        base.Dispose(disposing);
    }
}
