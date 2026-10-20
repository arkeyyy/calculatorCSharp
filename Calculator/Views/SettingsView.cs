using Calculator.Controls;
using Calculator.Infrastructure;

namespace Calculator.Views;

public sealed class SettingsView : ModeView
{
    private bool arranging;
    private readonly Panel content=new(){Name="settings-content"};
    private readonly Label appearance=Ui.Label("Appearance",22);
    private readonly Label themeLabel=Ui.Label("Theme",14);
    private readonly ComboBox themes=new(){Name="settings-theme",AccessibleName="Theme",DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly Label accentLabel=Ui.Label("Accent color",14);
    private readonly FlowLayoutPanel accents=new(){Name="settings-accents",WrapContents=true};
    private readonly Label help=Ui.Label("Keyboard shortcuts\n\nEnter   Calculate / plot\nBackspace   Delete a digit\nEsc   Clear calculation\nDelete   Clear entry\nCtrl+C / Ctrl+V   Copy / paste\nTab / Shift+Tab   Move between controls\n\nText fields keep their native editing shortcuts.",14);
    private readonly Label about=Ui.Label("Calculator\n18 modes · 34-digit decimal precision\nPreferences and cached rates stay on this device.",12);
    public SettingsView(SettingsStore settings,Action changed)
    {
        Name="settings-view";AutoScroll=true;
        themes.Items.AddRange(["Light","Dark"]);themes.SelectedIndex=settings.Get("theme","light")=="dark"?1:0;
        themes.SelectedIndexChanged+=(_,_)=>{settings.Put("theme",themes.SelectedIndex==1?"dark":"light");changed();};
        foreach(string accent in ThemePalette.AccentNames)
        {
            var b=Ui.Button(accent[0]+accent[1..].ToLowerInvariant(),()=>{settings.Put("accent",accent);changed();},"accent-"+accent);
            b.Tag=accent;b.Margin=new Padding(0,0,6,6);accents.Controls.Add(b);
        }
        about.Tag="muted";help.AutoEllipsis=false;about.AutoEllipsis=false;
        content.Controls.AddRange([appearance,themeLabel,themes,accentLabel,accents,help,about]);
        Controls.Add(content);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if(arranging)return;
        arranging=true;
        try
        {
        base.OnLayout(e);if(appearance is null)return;
        int width=Math.Max(0,ClientSize.Width-ScaleLogical(6));
        appearance.SetBounds(0,0,width,ScaleLogical(36));
        themeLabel.SetBounds(0,ScaleLogical(48),width,ScaleLogical(24));
        themes.SetBounds(0,ScaleLogical(76),Math.Min(width,ScaleLogical(240)),ScaleLogical(30));
        accentLabel.SetBounds(0,ScaleLogical(122),width,ScaleLogical(24));
        int gap=ScaleLogical(6),buttonWidth=ScaleLogical(84),buttonHeight=ScaleLogical(34);
        int columns=Math.Max(1,width/(buttonWidth+gap));
        int rows=(accents.Controls.Count+columns-1)/columns;
        int accentHeight=rows*(buttonHeight+gap);
        accents.SetBounds(0,ScaleLogical(152),width,accentHeight);
        foreach(Control b in accents.Controls)
        {
            b.Size=new Size(buttonWidth,buttonHeight);
            b.Margin=new Padding(0,0,gap,gap);
        }
        int helpTop=accents.Bottom+ScaleLogical(16);
        help.SetBounds(0,helpTop,width,ScaleLogical(242));
        about.SetBounds(0,help.Bottom+ScaleLogical(14),width,ScaleLogical(74));
        int contentHeight=about.Bottom+ScaleLogical(8);
        // AutoScroll owns the content's position; assigning it during layout can
        // count the existing scroll offset twice when the scroll range changes.
        content.Size=new Size(width,contentHeight);
        AutoScrollMinSize=new Size(0,contentHeight);
        base.OnLayout(e);
        }
        finally { arranging=false; }
    }
    public override void ApplyTheme(ThemePalette p)
    {
        base.ApplyTheme(p);
        foreach(Button b in accents.Controls)
        {
            var a=ThemePalette.Create(p.IsDark?"dark":"light",(string)b.Tag!);
            b.BackColor=a.Accent;b.ForeColor=a.AccentText;
            b.FlatAppearance.BorderSize=a.Accent==p.Accent?3:1;
            b.FlatAppearance.BorderColor=a.Accent==p.Accent?p.Text:p.Border;
        }
    }
}
