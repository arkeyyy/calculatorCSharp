namespace Calculator.Controls;

/// <summary>A history action with a quiet expression and a prominent result.</summary>
public sealed class HistoryItemButton : StyledButton
{
    private readonly Font expressionFont=Ui.Font(12);
    private readonly Font resultFont=Ui.Font(20,FontStyle.Bold);
    private ThemePalette colors=ThemePalette.Create("light","BLUE");

    public HistoryItemButton(string expression,string result,Action recall)
    {
        Expression=expression;Result=result;Name="history-item";Text=expression+"\n"+result;
        AccessibleName=expression+" equals "+result;AccessibleDescription="Recall this result";
        TextAlign=ContentAlignment.MiddleRight;Margin=Padding.Empty;Cursor=Cursors.Hand;
        Click+=(_,_)=>recall();
    }

    public string Expression { get; }
    public string Result { get; }

    public override void ApplyPalette(ThemePalette palette)
    {
        colors=palette;base.ApplyPalette(palette);
    }

    protected override void DrawContent(PaintEventArgs e,Rectangle bounds,Color foreground)
    {
        int scale(int value)=>(int)Math.Round(value*DeviceDpi/96d);
        Rectangle content=Rectangle.Inflate(bounds,-scale(12),-scale(6));
        int expressionHeight=scale(20);
        const TextFormatFlags flags=TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics,Expression,expressionFont,new Rectangle(content.X,content.Y,Math.Max(0,content.Width),expressionHeight),Enabled?colors.Muted:foreground,flags);
        TextRenderer.DrawText(e.Graphics,Result,resultFont,new Rectangle(content.X,content.Y+expressionHeight,Math.Max(0,content.Width),Math.Max(0,content.Height-expressionHeight)),foreground,flags);
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing){expressionFont.Dispose();resultFont.Dispose();}
        base.Dispose(disposing);
    }
}

/// <summary>Keeps item coordinates separate from the native scroll offset.</summary>
internal sealed class OverlayListPanel : Panel
{
    private readonly int itemHeight,gap,inset;
    private bool arranging;
    public Panel Content { get; }=new(){Name="overlay-list-content",TabStop=false};
    public ControlCollection Items=>Content.Controls;

    public OverlayListPanel(int itemHeight,int gap,int inset)
    {
        this.itemHeight=itemHeight;this.gap=gap;this.inset=inset;
        AutoScroll=true;Controls.Add(Content);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if(arranging||Content is null||Content.Parent!=this)return;
        arranging=true;
        try
        {
            base.OnLayout(e);
            int scale(int value)=>(int)Math.Round(value*DeviceDpi/96d);
            int padding=scale(inset),rowHeight=scale(itemHeight),rowGap=scale(gap);
            for(int pass=0;pass<3;pass++)
            {
                int y=padding,clientWidth=ClientSize.Width,width=Math.Max(0,clientWidth-2*padding);
                foreach(Control item in Items)
                {
                    item.SetBounds(padding,y,width,rowHeight);y+=rowHeight+rowGap;
                }
                int height=y-(Items.Count>0?rowGap:0)+padding;
                // AutoScroll owns the content panel's location. Reassigning it here
                // would combine logical item coordinates with a native scroll offset.
                Content.Size=new Size(clientWidth,height);
                AutoScrollMinSize=new Size(0,height);
                base.OnLayout(e);
                // A newly visible native scrollbar changes the actual client width.
                if(ClientSize.Width==clientWidth)break;
            }
        }
        finally{arranging=false;}
    }

    protected override Point ScrollToControl(Control activeControl) =>
        arranging ? DisplayRectangle.Location : base.ScrollToControl(activeControl);
}
