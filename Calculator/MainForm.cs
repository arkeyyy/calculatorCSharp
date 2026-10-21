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
    private readonly Panel navigationClip=new(){Name="navigation-clip",Visible=false,TabStop=false};
    internal NavigationTransition NavigationTransition { get; }
    private Region? navigationRegion;
    private readonly Panel history=new(){Name="history",Visible=false};
    private readonly OverlayListPanel modeList=new(36,0,8){Name="navigation-modes",Dock=DockStyle.Fill};
    private readonly OverlayListPanel historyItems=new(66,8,0){Name="history-items"};
    private readonly Panel navigationFooter=new(){Name="navigation-footer",Dock=DockStyle.Bottom};
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
        :this(settings,session,currencyRateProvider,null,null) { }

    internal MainForm(SettingsStore? settings,SessionStore? session,ICurrencyRateProvider? currencyRateProvider,
        Func<double>? animationClock,Func<bool?>? animationsEnabled)
    {
        NavigationTransition=new NavigationTransition(ApplyNavigationFrame,animationClock,animationsEnabled);
        this.settings=settings??new SettingsStore(Environment.GetEnvironmentVariable("CALCULATOR_DATA_DIR"),Environment.GetEnvironmentVariable("CALCULATOR_SKIP_LEGACY_IMPORT")!="1");
        this.session=session??new SessionStore();
        this.currencyRateProvider=currencyRateProvider;
        palette=ThemePalette.Create(this.settings.Get("theme","light"),this.settings.Get("accent","BLUE"));
        Name="CalculatorMainWindow";Text="Calculator";StartPosition=FormStartPosition.CenterScreen;
        using (var iconStream=typeof(MainForm).Assembly.GetManifestResourceStream("Calculator.Assets.Calculator.ico"))
            if(iconStream is not null)Icon=new Icon(iconStream);
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Font=Ui.Font(14);KeyPreview=true;
        Size=new Size(Scale(400),Scale(600));MinimumSize=new Size(Scale(320),Scale(500));DoubleBuffered=true;
        navigation=new IconButton(HeaderIcon.Menu,"Open navigation",ToggleNavigation,"navigation-toggle");
        historyButton=new IconButton(HeaderIcon.History,"History",ToggleHistory,"history-toggle");
        title.Font=Ui.Font(22,FontStyle.Bold);tooltips.SetToolTip(historyButton,"History");tooltips.SetToolTip(navigation,"Navigation");
        header.Controls.AddRange([navigation,title,historyButton]);
        foreach(ModeId mode in Enum.GetValues<ModeId>())
        {
            var button=Ui.Button(mode.Title(),()=>{if(NavigationTransition.RequestedOpen)SelectMode(mode);},"navigation-"+mode);
            button.TextAlign=ContentAlignment.MiddleLeft;button.Tag=mode;modeButtons.Add(button);modeList.Items.Add(button);
        }
        settingsButton=Ui.Button("Settings",()=>{if(NavigationTransition.RequestedOpen)ShowSettings();},"navigation-settings");settingsButton.TextAlign=ContentAlignment.MiddleLeft;
        navigationFooter.Controls.Add(settingsButton);
        drawer.Padding=new Padding(1);drawer.Controls.Add(modeList);drawer.Controls.Add(navigationFooter);
        navigationClip.Controls.Add(drawer);
        navigationFooter.Paint+=(_,e)=>{using var pen=new Pen(palette.Border);e.Graphics.DrawLine(pen,Scale(8),0,navigationFooter.Width-Scale(8),0);};
        drawer.Paint+=PaintOverlayBorder;history.Paint+=PaintOverlayBorder;
        clearHistory=Ui.Button("Clear",this.session.ClearHistory,"history-clear");
        history.Controls.AddRange([historyTitle,clearHistory,historyItems]);
        footer.Tag="muted";footer.Text="Ready";
        Controls.AddRange([header,workspace,footer,navigationClip,history]);
        this.session.Changed+=RefreshHistory;
        if(!Enum.TryParse(this.settings.Get("mode","STANDARD"),out currentMode)||!Enum.IsDefined(currentMode))currentMode=ModeId.STANDARD;
        initialized=true;SelectMode(currentMode);
        Shown+=(_,_)=>{ApplyWindowRequirements();active?.FocusInput();};
        FormClosing+=(_,_)=>this.settings.Flush();
    }
    private int Scale(double logical)=>(int)Math.Round(logical*DeviceDpi/96d);
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
        history.Visible=false;SetNavigationOpen(false);ApplyTheme();PerformLayout();view.FocusInput();
    }
    private void ApplyWindowRequirements()
    {
        bool graph=currentMode==ModeId.GRAPHING&&!showingSettings;
        var minimum=new Size(Scale(graph?800:320),Scale(graph?640:500));
        MinimumSize=minimum;
        if(Width<minimum.Width||Height<minimum.Height)Size=new Size(Math.Max(Width,minimum.Width),Math.Max(Height,minimum.Height));
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // Restore the same logical scroll offset after WinForms rescales children.
        double scroll=-modeList.AutoScrollPosition.Y*96d/e.DeviceDpiOld;
        base.OnDpiChanged(e);
        ApplyWindowRequirements();PerformLayout();
        modeList.AutoScrollPosition=new Point(0,Scale(scroll));
    }
    private void ToggleNavigation()
    {
        history.Visible=false;SetNavigationOpen(!NavigationTransition.RequestedOpen);
    }
    private void SetNavigationOpen(bool open)
    {
        NavigationTransition.Request(open);
        navigation.AccessibleName=open?"Close navigation":"Open navigation";
        foreach(Button button in modeButtons)button.TabStop=open;
        settingsButton.TabStop=open;
        if(open)
        {
            navigationClip.BringToFront();
            (showingSettings?settingsButton:modeButtons[(int)currentMode]).Focus();
        }
        else active?.FocusInput();
    }
    private void ApplyNavigationFrame()
    {
        if(IsDisposed||Disposing)return;
        // The contents keep their final width. Only their position and the overlay's
        // native clip change, so neither text nor scrollbars reflow during a slide.
        int reveal=(int)Math.Round(navigationClip.Width*NavigationTransition.Position);
        Point scroll=new(-modeList.AutoScrollPosition.X,-modeList.AutoScrollPosition.Y);
        drawer.Left=reveal-navigationClip.Width;
        var oldRegion=navigationRegion;
        navigationRegion=new Region(new Rectangle(0,0,reveal,navigationClip.Height));
        navigationClip.Region=navigationRegion;
        oldRegion?.Dispose();
        bool visible=NavigationTransition.RequestedOpen||NavigationTransition.Position>0;
        drawer.Visible=visible;navigationClip.Visible=visible;
        modeList.AutoScrollPosition=scroll;
    }
    private void ToggleHistory()
    {
        history.Visible=!history.Visible;SetNavigationOpen(false);
        if(history.Visible){RefreshHistory();history.BringToFront();clearHistory.Focus();}
        else active?.FocusInput();
    }
    private void PaintOverlayBorder(object? sender,PaintEventArgs e)
    {
        if(sender is not Control control||control.Width<1||control.Height<1)return;
        using var pen=new Pen(palette.Border);e.Graphics.DrawRectangle(pen,0,0,control.Width-1,control.Height-1);
    }
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
            if(b is StyledButton styled){styled.Selected=(ModeId)b.Tag! == currentMode&&!showingSettings;styled.SelectionIndicator=true;}
            b.Padding=new Padding(Scale(12),0,Scale(12),0);
            b.FlatAppearance.BorderSize=0;
        }
        if(settingsButton is StyledButton selectedSettings){selectedSettings.Selected=showingSettings;selectedSettings.SelectionIndicator=true;}
        settingsButton.Padding=new Padding(Scale(12),0,Scale(12),0);settingsButton.FlatAppearance.BorderSize=0;
        navigationClip.BackColor=drawer.BackColor=history.BackColor=navigationFooter.BackColor=historyTitle.BackColor=palette.Surface;
        modeList.BackColor=modeList.Content.BackColor=historyItems.BackColor=historyItems.Content.BackColor=palette.Surface;
        foreach(Label label in historyItems.Items.OfType<Label>())label.BackColor=palette.Surface;
        navigation.FlatAppearance.BorderSize=historyButton.FlatAppearance.BorderSize=0;
        navigation.BackColor=historyButton.BackColor=palette.Background;
        drawer.Invalidate();history.Invalidate();navigationFooter.Invalidate();
    }
    private void RefreshHistory()
    {
        if(IsDisposed)return;
        int offset=-historyItems.AutoScrollPosition.Y;
        historyItems.SuspendLayout();
        foreach(Control c in historyItems.Items.Cast<Control>().ToArray())c.Dispose();
        if(session.History.Count==0)
        {
            var empty=Ui.Label("Your calculations will appear here.",14,"history-empty");empty.Tag="muted";historyItems.Items.Add(empty);
        }
        foreach(var item in session.History)
        {
            historyItems.Items.Add(new HistoryItemButton(item.Expression,item.Result,()=>Recall(item)));
        }
        historyItems.ResumeLayout(true);historyItems.PerformLayout();palette.Apply(historyItems);
        historyItems.BackColor=historyItems.Content.BackColor=palette.Surface;
        foreach(Label label in historyItems.Items.OfType<Label>())label.BackColor=palette.Surface;
        historyItems.AutoScrollPosition=new Point(0,offset);
    }
    private void Recall(HistoryItem item)
    {
        SelectMode(item.Mode);
        if(active is ProgrammerView programmer)programmer.Recall(item.Result);else active?.Paste(item.Result);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if(!initialized){base.OnLayout(e);return;}
        // Form layout tries to reveal the currently focused descendant, even when
        // the user has deliberately scrolled away from it. Keep viewport position
        // through layout; native focus changes outside layout still reveal controls.
        Point navigationScroll=new(-modeList.AutoScrollPosition.X,-modeList.AutoScrollPosition.Y);
        Point historyScroll=new(-historyItems.AutoScrollPosition.X,-historyItems.AutoScrollPosition.Y);
        Point? settingsScroll=active is SettingsView settingsPage
            ?new Point(-settingsPage.AutoScrollPosition.X,-settingsPage.AutoScrollPosition.Y):null;
        base.OnLayout(e);
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
        int drawerWidth=Math.Min(Scale(202.5),ClientSize.Width-Scale(24));
        navigationClip.SetBounds(margin,headerHeight,drawerWidth,Math.Max(0,ClientSize.Height-headerHeight-margin));
        drawer.Size=navigationClip.Size;
        ApplyNavigationFrame();
        navigationFooter.Height=Scale(54);
        settingsButton.SetBounds(Scale(8),Scale(8),Math.Max(0,navigationFooter.Width-Scale(16)),Scale(38));
        foreach(Button button in modeButtons)button.Padding=new Padding(Scale(12),0,Scale(12),0);
        settingsButton.Padding=new Padding(Scale(12),0,Scale(12),0);
        int historyWidth=Math.Min(Scale(270),ClientSize.Width-Scale(24));
        history.SetBounds(ClientSize.Width-historyWidth-margin,headerHeight,historyWidth,Math.Max(0,ClientSize.Height-headerHeight-margin));
        historyTitle.SetBounds(Scale(10),0,historyWidth-Scale(90),Scale(42));clearHistory.SetBounds(historyWidth-Scale(70),Scale(6),Scale(62),Scale(30));
        historyItems.SetBounds(Scale(8),Scale(48),historyWidth-Scale(16),history.Height-Scale(56));
        modeList.AutoScrollPosition=navigationScroll;
        historyItems.AutoScrollPosition=historyScroll;
        if(settingsScroll is Point scroll&&active is SettingsView page)page.AutoScrollPosition=scroll;
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
        if(keyData==Keys.Escape&&(NavigationTransition.RequestedOpen||history.Visible)){history.Visible=false;SetNavigationOpen(false);return true;}
        if(NavigationTransition.RequestedOpen||history.Visible||showingSettings)return base.ProcessCmdKey(ref msg,keyData);
        var focus=FocusOwner();
        if(keyData==Keys.Enter&&(focus==navigation||focus==historyButton))return base.ProcessCmdKey(ref msg,keyData);
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
            NavigationTransition.Dispose();
            navigationClip.Region=null;navigationRegion?.Dispose();navigationRegion=null;
            session.Changed-=RefreshHistory;
            foreach(var view in views.Values)view.Dispose();settingsView?.Dispose();tooltips.Dispose();settings.Flush();
        }
        base.Dispose(disposing);
    }
}
