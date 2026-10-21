using System.Drawing.Imaging;
using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class ShellAppearanceTests
{
    [TestMethod]
    public void NavigationSelectionAndScrolledRowsSurviveResize()
    {
        using var shell=new ShellFixture(ModeId.STANDARD);
        shell.Ui.Do(host=>host.Size=host.MinimumSize);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.WaitForNavigation();
        shell.Ui.Do(host=>
        {
            Assert.AreEqual("Close navigation",UiThreadHarness.Find<Button>(host,"navigation-toggle").AccessibleName);
            Assert.IsTrue(UiThreadHarness.Find<StyledButton>(host,"navigation-STANDARD").Selected);
            Assert.IsFalse(UiThreadHarness.Find<StyledButton>(host,"navigation-settings").Selected);
            var viewport=UiThreadHarness.Find<Panel>(host,"navigation-modes");
            StyledButton[] rows=ModeView.Descendants(viewport).OfType<StyledButton>().ToArray();
            CollectionAssert.AreEqual(Enum.GetValues<ModeId>(),rows.Select(row=>(ModeId)row.Tag!).ToArray());
            Assert.IsTrue(rows.All(row=>row.SelectionIndicator));
            Assert.IsTrue(viewport.VerticalScroll.Visible);
            Capture(host,"navigation-minimum");
            viewport.AutoScrollPosition=new Point(0,Scale(host,180));
            int offset=viewport.AutoScrollPosition.Y;
            Assert.IsTrue(offset<0);
            host.Width=Scale(host,400);host.PerformLayout();
            Assert.AreEqual(offset,viewport.AutoScrollPosition.Y,"Resizing must retain the native navigation scroll offset.");
            Panel content=UiThreadHarness.Find<Panel>(viewport,"overlay-list-content");
            Assert.AreEqual(offset,content.Top);
            Assert.IsFalse(viewport.HorizontalScroll.Visible);
            Capture(host,"navigation-scrolled");
        });
        shell.Ui.Key(VirtualKeyShort.TAB);
        shell.Ui.Do(host=>
        {
            var viewport=UiThreadHarness.Find<Panel>(host,"navigation-modes");
            var next=UiThreadHarness.Find<Button>(host,"navigation-SCIENTIFIC");
            Assert.IsTrue(next.Focused,"Tab must still move to the next mode after preserving a scrolled layout.");
            Assert.IsTrue(viewport.RectangleToScreen(viewport.ClientRectangle).Contains(next.RectangleToScreen(next.ClientRectangle)),
                "Native keyboard navigation must reveal the newly focused mode.");
        });
        shell.Ui.Click("navigation-settings");
        shell.Ui.Click("navigation-toggle");
        shell.Ui.WaitForNavigation();
        shell.Ui.Do(host=>
        {
            Assert.IsTrue(UiThreadHarness.Find<StyledButton>(host,"navigation-settings").Selected);
            var viewport=UiThreadHarness.Find<Panel>(host,"navigation-modes");
            Assert.IsTrue(ModeView.Descendants(viewport).OfType<StyledButton>().All(row=>!row.Selected));
            Assert.IsTrue(UiThreadHarness.Find<Button>(host,"navigation-settings").Focused,"Settings should receive focus when its selected navigation item is opened.");
            Capture(host,"navigation-settings-selected");
        });
        shell.Ui.Key(VirtualKeyShort.ESCAPE);
        shell.Ui.WaitForNavigation();
        shell.Ui.Do(host=>
        {
            Assert.IsFalse(UiThreadHarness.Find<Panel>(host,"navigation").Visible);
            Assert.AreEqual("Open navigation",UiThreadHarness.Find<Button>(host,"navigation-toggle").AccessibleName);
            Assert.IsTrue(((MainForm)host).ActiveMode!.ContainsFocus);
        });
    }

    [TestMethod]
    public void HeaderTabAndEnterOpenNavigationAndClosingRestoresInput()
    {
        using var shell=new ShellFixture(ModeId.STANDARD);
        shell.Ui.Do(host=>UiThreadHarness.Find<Button>(host,"history-toggle").Focus());
        shell.Ui.Chord(VirtualKeyShort.SHIFT,VirtualKeyShort.TAB);
        Assert.AreEqual("navigation-toggle",shell.Ui.FocusedName);
        shell.Ui.Key(VirtualKeyShort.RETURN);
        shell.Ui.Do(host=>Assert.IsTrue(UiThreadHarness.Find<Panel>(host,"navigation").Visible,"Enter on the hamburger must open navigation instead of evaluating the calculator."));
        shell.Ui.Key(VirtualKeyShort.ESCAPE);
        shell.Ui.Key(VirtualKeyShort.KEY_7);
        Assert.AreEqual("7",shell.Ui.Text("standard-display"));
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host=>Assert.IsTrue(((MainForm)host).ActiveMode!.ContainsFocus));
        shell.Ui.Key(VirtualKeyShort.KEY_8);
        Assert.AreEqual("78",shell.Ui.Text("standard-display"));
    }

    [TestMethod]
    public void HistoryRowsReflowPreserveScrollAndRecallTheirResult()
    {
        string directory=Path.Combine(Path.GetTempPath(),"calculator-history-"+Guid.NewGuid().ToString("N"));
        try
        {
            var settings=new SettingsStore(directory,false);
            var session=new SessionStore();
            for(int index=0;index<20;index++)session.AddHistory(ModeId.STANDARD,$"{index} + 100",(index+100).ToString());
            using var ui=new UiThreadHarness(()=>new MainForm(settings,session));
            ui.Do(host=>host.Size=host.MinimumSize);
            ui.Click("history-toggle");
            ui.Do(host=>
            {
                var viewport=UiThreadHarness.Find<Panel>(host,"history-items");
                var content=UiThreadHarness.Find<Panel>(viewport,"overlay-list-content");
                HistoryItemButton[] rows=ModeView.Descendants(viewport).OfType<HistoryItemButton>().ToArray();
                Assert.AreEqual(20,rows.Length);
                Assert.IsTrue(rows.All(row=>row.AccessibleName==row.Expression+" equals "+row.Result));
                Capture(host,"history-minimum");
                viewport.AutoScrollPosition=new Point(0,Scale(host,180));
                int offset=viewport.AutoScrollPosition.Y;
                Assert.IsTrue(offset<0);
                int originalWidth=rows[0].Width;
                viewport.Width-=Scale(host,30);
                viewport.PerformLayout();
                Assert.IsTrue(rows[0].Width<originalWidth,"History rows must follow their viewport width.");
                foreach(HistoryItemButton row in rows)
                {
                    Assert.AreEqual(content.ClientSize.Width,row.Width);
                    Assert.IsTrue(content.ClientRectangle.Contains(row.Bounds));
                }
                Assert.AreEqual(offset,viewport.AutoScrollPosition.Y);
                host.Width=Scale(host,400);host.PerformLayout();
                Assert.AreEqual(offset,viewport.AutoScrollPosition.Y,"Shell layout must retain the history scroll offset.");
                Assert.AreEqual(offset,content.Top);
                Assert.IsFalse(viewport.HorizontalScroll.Visible);
                Capture(host,"history-scrolled");
                HistoryItemButton recall=rows.First(row=>row.Result=="117");
                viewport.ScrollControlIntoView(recall);
                recall.Focus();recall.PerformClick();
            });
            Assert.AreEqual("117",ui.Text("standard-display"));
            ui.Do(host=>Assert.IsFalse(UiThreadHarness.Find<Panel>(host,"history").Visible));
            ui.Click("history-toggle");
            ui.Click("history-clear");
            ui.Do(host=>
            {
                var viewport=UiThreadHarness.Find<Panel>(host,"history-items");
                Assert.AreEqual(0,ModeView.Descendants(viewport).OfType<HistoryItemButton>().Count());
                Label empty=UiThreadHarness.Find<Label>(viewport,"history-empty");
                Assert.IsTrue(empty.Width>0&&empty.Height>0,"Clearing history must immediately lay out its empty state.");
                Assert.AreEqual(0,viewport.AutoScrollPosition.Y);
                Assert.IsFalse(viewport.VerticalScroll.Visible);
            });
        }
        finally
        {
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }

    private static int Scale(Control control,int value)=>(int)Math.Round(value*control.DeviceDpi/96d);

    private static void Capture(Form form,string name)
    {
        string directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","artifacts","screenshots"));
        Directory.CreateDirectory(directory);
        using var image=new Bitmap(form.Width,form.Height);
        // DrawToBitmap reverses the z-order of overlapping native child windows.
        // Capture the real desktop so drawer coverage is reviewed as users see it.
        form.Refresh();
        using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(form.Location,Point.Empty,form.Size);
        image.Save(Path.Combine(directory,$"{name}-{form.DeviceDpi}dpi.png"),ImageFormat.Png);
    }
}
