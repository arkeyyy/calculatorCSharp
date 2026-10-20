using System.Drawing.Imaging;
using Calculator.Controls;
using Calculator.Core;
using Calculator.Views;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class ModeLayoutTests
{
    [TestMethod]
    public void OrdinaryLaunchUsesDefaultOuterSize()
    {
        using var shell=new ShellFixture(ModeId.STANDARD);
        shell.Ui.Do(host=>
        {
            double scale=host.DeviceDpi/96d;
            Assert.AreEqual(new Size((int)Math.Round(400*scale),(int)Math.Round(600*scale)),host.Size);
        });
    }

    [DataTestMethod]
    [DataRow(ModeId.LENGTH)]
    [DataRow(ModeId.CURRENCY)]
    public void ConvertersMaximizeAndRestoreTheirCompactLayout(ModeId mode)
    {
        using var shell=new ShellFixture(mode,currencyRateProvider:new LayoutRateProvider());
        shell.Ui.Do(host=>host.Size=host.MinimumSize);
        shell.Ui.Do(host=>
        {
            var view=(ConverterView)((MainForm)host).ActiveMode!;
            Assert.IsFalse(view.IsWideLayout);
            AssertFlatConverterBounds(view);
            UiThreadHarness.AssertKeypadBounds(view);
            Capture(host,$"{mode.ToString().ToLowerInvariant()}-minimum");
            host.WindowState=FormWindowState.Maximized;
        });
        shell.Ui.WaitUntil(host=>host.WindowState==FormWindowState.Maximized,"Window did not maximize.");
        shell.Ui.Do(host=>
        {
            var view=(ConverterView)((MainForm)host).ActiveMode!;
            Assert.IsTrue(view.IsWideLayout);
            Assert.AreEqual(view.Parent!.ClientSize.Width,view.Width);
            AssertFlatConverterBounds(view);
            UiThreadHarness.AssertKeypadBounds(view);
            Capture(host,$"{mode.ToString().ToLowerInvariant()}-maximized");
            host.WindowState=FormWindowState.Normal;
        });
        shell.Ui.WaitUntil(host=>!((ConverterView)((MainForm)host).ActiveMode!).IsWideLayout,"Restoring must return to the compact converter layout.");
        shell.Ui.Do(host=>UiThreadHarness.AssertKeypadBounds(host));
    }

    private sealed class LayoutRateProvider:ICurrencyRateProvider
    {
        public Task<CurrencyRateSnapshot> FetchAsync(CancellationToken cancellationToken=default)=>
            Task.FromResult(CurrencyRateSnapshot.Decode("2020-01-02\nEUR=1\nUSD=1.1\nJPY=123\nCNY=7.5"));
    }

    [TestMethod]
    public void CaptureRepresentativeModeScreens()
    {
        using var shell = new ShellFixture(ModeId.STANDARD);
        foreach (ModeId mode in new[] { ModeId.STANDARD, ModeId.SCIENTIFIC, ModeId.PROGRAMMER, ModeId.LENGTH, ModeId.GRAPHING })
        {
            shell.Select(mode);
            shell.Ui.Do(host =>
            {
                Size[] sizes = mode == ModeId.GRAPHING ? [new(800, 640)] : mode == ModeId.LENGTH ? [new(320, 500), new(400, 600), new(1600, 900)] : [new(320, 500), new(400, 600)];
                foreach (Size logical in sizes)
                {
                    host.Size = new Size((int)Math.Round(logical.Width * host.DeviceDpi / 96d), (int)Math.Round(logical.Height * host.DeviceDpi / 96d));
                    host.PerformLayout();
                    Capture(host, $"{mode.ToString().ToLowerInvariant()}-{logical.Width}x{logical.Height}");
                }
            });
        }
        shell.Select(ModeId.STANDARD);
        shell.Ui.Do(host => host.Size = new Size((int)Math.Round(400 * host.DeviceDpi / 96d), (int)Math.Round(600 * host.DeviceDpi / 96d)));
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Click("navigation-settings");
        foreach (string theme in new[] { "light", "dark" })
            shell.Ui.Do(host =>
            {
                UiThreadHarness.Find<ComboBox>(host, "settings-theme").SelectedIndex = theme == "dark" ? 1 : 0;
                Capture(host, $"settings-{theme}");
            });
    }

    [DataTestMethod]
    [DataRow(ModeId.STANDARD)]
    [DataRow(ModeId.SCIENTIFIC)]
    [DataRow(ModeId.PROGRAMMER)]
    [DataRow(ModeId.DATE)]
    [DataRow(ModeId.GRAPHING)]
    public void OrdinaryModeKeypadsFitMinimumDefaultAndLargeWindows(ModeId mode)
    {
        using var shell = new ShellFixture(mode);
        shell.Ui.Do(host =>
        {
            double scale = host.DeviceDpi / 96d;
            Size[] sizes = mode == ModeId.GRAPHING ? [new(800, 640), new(1600, 900)] : [new(320, 500), new(400, 600), new(1600, 900)];
            foreach (Size logical in sizes)
            {
                host.Size = new Size((int)Math.Round(logical.Width * scale), (int)Math.Round(logical.Height * scale));
                host.PerformLayout();
                UiThreadHarness.AssertKeypadBounds(host);
                ModeView view = ((MainForm)host).ActiveMode!;
                Assert.IsTrue(view.Width <= Math.Round(mode.WorkspaceLimit()!.Value * scale));
                Assert.IsTrue(view.Height > 0 && view.Right <= view.Parent!.ClientSize.Width);
                if (logical.Width == 1600) Assert.AreEqual((int)Math.Round(mode.WorkspaceLimit()!.Value * scale), view.Width);
                if (logical.Width < 1600) Capture(host, $"{mode.ToString().ToLowerInvariant()}-{logical.Width}x{logical.Height}");
            }
        });
    }

    [TestMethod]
    public void AllStaticConvertersFillWideWorkspaceAndKeepRenderedControlsReachable()
    {
        using var shell = new ShellFixture(ModeId.LENGTH);
        foreach (ModeId mode in Enum.GetValues<ModeId>().Where(mode => mode.IsConverter() && mode != ModeId.CURRENCY))
        {
            shell.Select(mode);
            shell.Ui.Do(host =>
            {
                double scale = host.DeviceDpi / 96d;
                foreach (Size logical in new[] { new Size(320, 500), new Size(400, 600), new Size(1600, 900) })
                {
                    host.Size = new Size((int)Math.Round(logical.Width * scale), (int)Math.Round(logical.Height * scale));
                    host.PerformLayout();
                    var view = (ConverterView)((MainForm)host).ActiveMode!;
                    Assert.AreEqual(view.Parent!.ClientSize.Width, view.Width, "Converters must not retain a workspace cap.");
                    UiThreadHarness.AssertKeypadBounds(view);
                    AssertFlatConverterBounds(view);
                    if (logical.Width == 1600)
                    {
                        Assert.IsTrue(view.IsWideLayout);
                        KeypadControl keypad = UiThreadHarness.Find<KeypadControl>(view, "converter-keypad");
                        Assert.IsTrue(keypad.Height >= view.Height - Math.Round(20 * scale));
                        Assert.IsTrue(Math.Abs(keypad.Width - view.Width / 2d) < Math.Round(24 * scale));
                    }
                    if (mode == ModeId.LENGTH) Capture(host, $"converter-{logical.Width}x{logical.Height}");
                }
            });
        }
    }

    [TestMethod]
    public void ConverterBreakpointUsesAvailableWorkspaceInsteadOfOuterWindowWidth()
    {
        using var shell = new ShellFixture(ModeId.LENGTH);
        shell.Ui.Do(host =>
        {
            var view = (ConverterView)((MainForm)host).ActiveMode!;
            int scale(int pixels) => (int)Math.Round(pixels * host.DeviceDpi / 96d);
            // The shell's outer frame and horizontal margin are deliberately excluded.
            host.ClientSize = new Size(scale(639) + 2 * scale(10), scale(700));
            host.PerformLayout();
            Assert.AreEqual(scale(639), view.ClientSize.Width);
            Assert.IsFalse(view.IsWideLayout);
            host.ClientSize = new Size(scale(640) + 2 * scale(10), scale(700));
            host.PerformLayout();
            Assert.AreEqual(scale(640), view.ClientSize.Width);
            Assert.IsTrue(view.IsWideLayout, "A 640-logical-pixel workspace must use equal left/right columns.");
        });
    }

    [TestMethod]
    public void GraphingMinimumAppliesOnStartupAndDoesNotShrinkOnExit()
    {
        using var shell = new ShellFixture(ModeId.GRAPHING);
        shell.Ui.Do(host =>
        {
            int width = (int)Math.Round(800 * host.DeviceDpi / 96d), height = (int)Math.Round(640 * host.DeviceDpi / 96d);
            Assert.AreEqual(new Size(width, height), host.MinimumSize);
            Assert.IsTrue(host.Width >= width && host.Height >= height);
            Size graphSize = host.Size;
            ((MainForm)host).SelectMode(ModeId.STANDARD);
            Assert.AreEqual(graphSize, host.Size);
            Assert.AreEqual(new Size((int)Math.Round(320 * host.DeviceDpi / 96d), (int)Math.Round(500 * host.DeviceDpi / 96d)), host.MinimumSize);
        });
    }

    [TestMethod]
    public void NavigationOrderAndSharedMemorySurviveModeChanges()
    {
        using var shell = new ShellFixture(ModeId.STANDARD);
        shell.Ui.Do(host =>
        {
            Button[] navigation = ModeView.Descendants(host).OfType<Button>().Where(button => button.Name.StartsWith("navigation-", StringComparison.Ordinal) && button.Tag is ModeId).ToArray();
            CollectionAssert.AreEqual(Enum.GetValues<ModeId>(), navigation.Select(button => (ModeId)button.Tag!).ToArray());
        });
        shell.Ui.Click("key-7");
        shell.Ui.Click("memory-store");
        shell.Select(ModeId.SCIENTIFIC);
        shell.Ui.Click("memory-recall");
        Assert.AreEqual("7", shell.Ui.Text("scientific-display"));
        shell.Ui.Click("memory-add");
        shell.Select(ModeId.STANDARD);
        shell.Ui.Click("memory-recall");
        Assert.AreEqual("14", shell.Ui.Text("standard-display"));
    }

    [TestMethod]
    public void BothThemesAndEveryAccentKeepSettingsAndKeypadsVisible()
    {
        using var shell = new ShellFixture(ModeId.STANDARD);
        ModeView calculator = shell.Ui.Read(host => ((MainForm)host).ActiveMode!);
        foreach (string theme in new[] { "light", "dark" })
        {
            shell.Ui.Click("navigation-toggle");
            shell.Ui.Click("navigation-settings");
            shell.Ui.Do(host =>
            {
                UiThreadHarness.Find<ComboBox>(host, "settings-theme").SelectedIndex = theme == "dark" ? 1 : 0;
                Capture(host, $"settings-{theme}");
            });
            foreach (string accent in ThemePalette.AccentNames)
            {
                shell.Ui.Click("accent-" + accent);
                Assert.AreEqual(theme, shell.Settings.Get("theme", ""));
                Assert.AreEqual(accent, shell.Settings.Get("accent", ""));
                shell.Ui.Do(_ => Assert.AreEqual(ThemePalette.Create(theme, accent).Accent,
                    UiThreadHarness.Find<Button>(calculator, "key-=").BackColor));
            }
            shell.Select(ModeId.STANDARD);
            shell.Ui.Do(UiThreadHarness.AssertKeypadBounds);
        }
    }

    [TestMethod]
    public void SettingsContentRemainsScrolledAfterLayout()
    {
        using var shell = new ShellFixture(ModeId.STANDARD);
        shell.Ui.Do(host => host.Size = host.MinimumSize);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Click("navigation-settings");
        shell.Ui.Do(host =>
        {
            var settings = (SettingsView)((MainForm)host).ActiveMode!;
            Assert.IsTrue(settings.VerticalScroll.Visible);
            var accents=UiThreadHarness.Find<FlowLayoutPanel>(settings,"settings-accents");
            Assert.AreEqual(6,accents.Controls.Count);
            foreach(Control button in accents.Controls)
                Assert.IsTrue(accents.ClientRectangle.Contains(button.Bounds),"Every accent must fit within the scrolling content.");
            settings.AutoScrollPosition = new Point(0, settings.AutoScrollMinSize.Height);
            int offset = settings.AutoScrollPosition.Y;
            Assert.IsTrue(offset < 0);
            settings.PerformLayout();
            var content = UiThreadHarness.Find<Panel>(settings, "settings-content");
            Assert.AreEqual(offset, settings.AutoScrollPosition.Y, "Layout must not reset the native scroll offset.");
            Assert.AreEqual(offset, content.Top, "Settings content must move with the scroll viewport.");
            Label about = ModeView.Descendants(settings).OfType<Label>().Single(label => label.Text.StartsWith("Calculator\n", StringComparison.Ordinal));
            Assert.IsTrue(settings.RectangleToScreen(settings.ClientRectangle).Contains(about.RectangleToScreen(about.ClientRectangle)), "The final settings text must be reachable by scrolling.");
            Capture(host, "settings-scrolled");
        });
    }

    private static void AssertFlatConverterBounds(ConverterView view)
    {
        Control[] controls = view.Controls.Cast<Control>().Where(control => control.Visible && control.Height > 0).ToArray();
        foreach (Control control in controls)
            Assert.IsTrue(control.Left >= 0 && control.Top >= 0 && control.Right <= view.ClientSize.Width && control.Bottom <= view.ClientSize.Height,
                $"{control.Name} {control.Bounds} escapes converter {view.ClientSize} at DPI {view.DeviceDpi}.");
        for (int i = 0; i < controls.Length; i++)
            for (int j = i + 1; j < controls.Length; j++)
                Assert.IsFalse(controls[i].Bounds.IntersectsWith(controls[j].Bounds),
                    $"Converter controls overlap: {controls[i].Name} {controls[i].Bounds}, {controls[j].Name} {controls[j].Bounds}.");
    }

    private static void Capture(Form form, string name)
    {
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string directory = Path.Combine(repository, "artifacts", "screenshots");
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, $"{name}-{form.DeviceDpi}dpi.png"), ImageFormat.Png);
    }
}
