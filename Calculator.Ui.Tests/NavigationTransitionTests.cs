using System.Drawing.Imaging;
using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class NavigationTransitionTests
{
    [TestMethod]
    public void CubicProgressReversesContinuouslyWithDistanceProportionalDuration()
    {
        double now = 0;
        int frames = 0;
        using var slide = new NavigationTransition(() => frames++, () => now, () => true);
        slide.Request(true);
        Assert.IsTrue(slide.IsRunning);
        Assert.AreEqual(0d, slide.Position);
        now = 90;
        slide.Advance();
        Assert.AreEqual(.875, slide.Position, 1e-10);
        slide.Request(false);
        Assert.AreEqual(.875, slide.Position, 1e-10, "Reversing cannot jump.");
        now += 180 * .875 / 2;
        slide.Advance();
        Assert.AreEqual(.875 / 8, slide.Position, 1e-10);
        slide.Request(true);
        double start = slide.Position;
        now += 180 * (1 - start) / 2;
        slide.Advance();
        Assert.AreEqual(start + (1 - start) * .875, slide.Position, 1e-10);
        now += 180 * (1 - start) / 2;
        slide.Advance();
        Assert.AreEqual(1d, slide.Position);
        Assert.IsFalse(slide.IsRunning);
        slide.Request(false);
        now += 180;
        slide.Advance();
        Assert.AreEqual(0d, slide.Position);
        Assert.IsFalse(slide.IsRunning);
        slide.Request(true);
        slide.Dispose();
        int before = frames;
        now += 1000;
        slide.Advance();
        slide.Request(false);
        Assert.AreEqual(before, frames, "Disposed transitions must never notify the shell.");
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(null)]
    public void ControllerHonorsReducedMotionAndReadFailureWithoutStartingATimer(bool? preference)
    {
        using var slide = new NavigationTransition(() => { }, () => 0, () => preference);
        slide.Request(true);
        Assert.AreEqual(1d, slide.Position);
        Assert.IsFalse(slide.IsRunning);
        slide.Request(false);
        Assert.AreEqual(0d, slide.Position);
        Assert.IsFalse(slide.IsRunning);
    }

    [TestMethod]
    public void BackgroundLayoutPreservesGeometryAndLogicalFocusDuringTransitions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "calculator-navigation-" + Guid.NewGuid().ToString("N"));
        double now = 0;
        try
        {
            using var ui = new UiThreadHarness(() => new MainForm(new SettingsStore(directory, false), null, null, () => now, () => true), acquireForeground: false);
            ui.Do(host =>
            {
                host.Size = host.MinimumSize;
                var form = (MainForm)host;
                var slide = form.NavigationTransition;
                var toggle = UiThreadHarness.Find<Button>(host, "navigation-toggle");
                var drawer = UiThreadHarness.Find<Panel>(host, "navigation");
                var viewport = UiThreadHarness.Find<Panel>(host, "navigation-modes");
                toggle.PerformClick();
                Assert.AreSame(UiThreadHarness.Find<Button>(host, "navigation-STANDARD"), form.ActiveControl);
                int width = (int)Math.Round(202.5 * host.DeviceDpi / 96d);
                Assert.AreEqual(width, drawer.Width);
                Assert.AreEqual((int)Math.Round(270 * host.DeviceDpi / 96d), UiThreadHarness.Find<Panel>(host, "history").Width);
                Assert.IsTrue(viewport.VerticalScroll.Visible);
                Button[] rows = ModeView.Descendants(viewport).OfType<Button>().ToArray();
                int rowWidth = rows[0].Width;
                foreach (Button row in rows)
                {
                    Size text = TextRenderer.MeasureText(row.Text, row.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    Assert.IsTrue(text.Width <= row.ClientSize.Width - row.Padding.Horizontal, $"{row.Text} clips at minimum size.");
                }
                now = 45;
                slide.Advance();
                viewport.AutoScrollPosition = new Point(0, 180);
                int scroll = viewport.AutoScrollPosition.Y;
                host.Width += 40;
                Assert.AreEqual(.578125, slide.Position, 1e-10);
                Assert.AreEqual(scroll, viewport.AutoScrollPosition.Y);
                Assert.AreEqual(rowWidth, rows[0].Width);
                toggle.PerformClick();
                Assert.AreSame(form.ActiveMode, form.ActiveControl, "Closing must synchronously select the workspace.");
                foreach (Button row in rows) row.PerformClick();
                Assert.AreEqual(ModeId.STANDARD, form.CurrentMode);
                Assert.AreSame(form.ActiveMode, form.ActiveControl, "Ignored commands must retain the destination.");
                now += 180;
                slide.Advance();
                Assert.IsFalse(drawer.Visible);
                Assert.AreEqual(scroll, viewport.AutoScrollPosition.Y);
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(null)]
    public void DisabledOrUnavailableAnimationPreferenceSwitchesImmediately(bool? preference)
    {
        using var shell = new ShellFixture(ModeId.STANDARD, animationsEnabled: () => preference);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host =>
        {
            var slide = ((MainForm)host).NavigationTransition;
            Assert.AreEqual(1d, slide.Position);
            Assert.IsFalse(slide.IsRunning);
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "navigation-STANDARD").Focused);
        });
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host =>
        {
            Assert.AreEqual(0d, ((MainForm)host).NavigationTransition.Position);
            Assert.IsFalse(UiThreadHarness.Find<Panel>(host, "navigation").Visible);
            Assert.IsTrue(((MainForm)host).ActiveMode!.ContainsFocus);
        });
    }

    [TestMethod]
    public void PreferenceChangeFinishesAnActiveSlide()
    {
        double now = 0;
        bool? preference = true;
        using var shell = new ShellFixture(ModeId.STANDARD, animationClock: () => now, animationsEnabled: () => preference);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host =>
        {
            var slide = ((MainForm)host).NavigationTransition;
            now = 45;
            slide.Advance();
            Assert.IsTrue(slide.Position is > 0 and < 1);
            preference = false;
            slide.Advance();
            Assert.AreEqual(1d, slide.Position);
            Assert.IsFalse(slide.IsRunning);
        });
    }

    [DataTestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void NativeFramesKeepWidthLabelsAndScrollStableThroughResizeAndReversal(string theme)
    {
        double now = 0;
        using var shell = new ShellFixture(ModeId.STANDARD, theme, animationClock: () => now, animationsEnabled: () => true);
        shell.Ui.Do(host => host.Size = host.MinimumSize);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host =>
        {
            var slide = ((MainForm)host).NavigationTransition;
            var drawer = UiThreadHarness.Find<Panel>(host, "navigation");
            var clip = UiThreadHarness.Find<Panel>(host, "navigation-clip");
            var viewport = UiThreadHarness.Find<Panel>(host, "navigation-modes");
            int width = (int)Math.Round(202.5 * host.DeviceDpi / 96d);
            Assert.AreEqual(width, drawer.Width);
            Assert.AreEqual((int)Math.Round(270 * host.DeviceDpi / 96d), UiThreadHarness.Find<Panel>(host, "history").Width);
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "navigation-STANDARD").Focused,
                "Selected item must receive focus even before the first frame. " + FocusDetails(host));
            Assert.IsTrue(viewport.VerticalScroll.Visible);
            Button[] rows = ModeView.Descendants(viewport).OfType<Button>().ToArray();
            int rowWidth = rows[0].Width;
            foreach (Button row in rows)
            {
                Size text = TextRenderer.MeasureText(row.Text, row.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                Assert.IsTrue(text.Width <= row.ClientSize.Width - row.Padding.Horizontal, $"{row.Text} clips at minimum size.");
                Assert.AreEqual((int)Math.Round(36 * host.DeviceDpi / 96d), row.Height);
                Assert.AreEqual(10.5f, row.Font.SizeInPoints, .01f);
            }
            now = 45;
            slide.Advance();
            Assert.AreEqual(.578125, slide.Position, 1e-10);
            Assert.AreEqual((int)Math.Round(width * slide.Position) - width, drawer.Left);
            Assert.AreEqual(rowWidth, rows[0].Width);
            AssertClip(clip, (int)Math.Round(width * slide.Position));
            Capture(host, $"navigation-{theme}-opening");
            viewport.AutoScrollPosition = new Point(0, (int)Math.Round(180 * host.DeviceDpi / 96d));
            int scroll = viewport.AutoScrollPosition.Y;
            host.Width += 40;
            host.PerformLayout();
            Assert.AreEqual(.578125, slide.Position, 1e-10);
            Assert.AreEqual(scroll, viewport.AutoScrollPosition.Y);
            Assert.AreEqual(rowWidth, rows[0].Width);
            UiThreadHarness.Find<Button>(host, "navigation-toggle").PerformClick();
            Assert.AreEqual(.578125, slide.Position, 1e-10);
            now += 20;
            slide.Advance();
            Assert.IsTrue(slide.Position is > 0 and < .578125);
            Assert.AreEqual(scroll, viewport.AutoScrollPosition.Y);
            Capture(host, $"navigation-{theme}-closing");
            UiThreadHarness.Find<Button>(host, "navigation-toggle").PerformClick();
            now += 180;
            slide.Advance();
            Assert.AreEqual(1d, slide.Position);
            Assert.AreEqual(0, drawer.Left);
            Assert.AreEqual(rowWidth, rows[0].Width);
            viewport.AutoScrollPosition = Point.Empty;
            Capture(host, $"navigation-{theme}-open");
        });
        shell.Ui.Key(VirtualKeyShort.TAB);
        Assert.AreEqual("navigation-SCIENTIFIC", shell.Ui.FocusedName);
    }

    [DataTestMethod]
    [DataRow("hamburger")]
    [DataRow("escape")]
    [DataRow("mode")]
    [DataRow("settings")]
    [DataRow("history")]
    public void EveryClosePathUpdatesFocusAndRoutingBeforeTheAnimationCompletes(string path)
    {
        double now = 0;
        using var shell = new ShellFixture(ModeId.STANDARD, animationClock: () => now, animationsEnabled: () => true);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Do(host => { now = 180; ((MainForm)host).NavigationTransition.Advance(); });
        switch (path)
        {
            case "hamburger": shell.Ui.Click("navigation-toggle"); break;
            case "escape": shell.Ui.Key(VirtualKeyShort.ESCAPE); break;
            case "mode": shell.Ui.Click("navigation-SCIENTIFIC"); break;
            case "settings": shell.Ui.Click("navigation-settings"); break;
            case "history": shell.Ui.Click("history-toggle"); break;
        }
        shell.Ui.Do(host =>
        {
            var form = (MainForm)host;
            Assert.IsFalse(form.NavigationTransition.RequestedOpen);
            Assert.IsTrue(form.NavigationTransition.IsRunning);
            Assert.IsTrue(UiThreadHarness.Find<Panel>(host, "navigation").Visible, "The exit frame remains visible.");
            Assert.AreEqual("Open navigation", UiThreadHarness.Find<Button>(host, "navigation-toggle").AccessibleName);
            var buttons = ModeView.Descendants(UiThreadHarness.Find<Panel>(host, "navigation")).OfType<Button>();
            Assert.IsTrue(buttons.All(button => !button.TabStop));
            ModeId mode = form.CurrentMode;
            ModeView destination = form.ActiveMode!;
            foreach (Button button in buttons) button.PerformClick();
            Assert.AreEqual(mode, form.CurrentMode, "Closing items must ignore commands.");
            Assert.AreSame(destination, form.ActiveMode);
            if (path == "history") Assert.IsTrue(UiThreadHarness.Find<Button>(host, "history-clear").Focused);
            else Assert.IsTrue(destination.ContainsFocus, "Closing must synchronously focus the destination. " + FocusDetails(host));
        });
        if (path is "hamburger" or "escape")
        {
            shell.Ui.Key(VirtualKeyShort.KEY_7);
            Assert.AreEqual("7", shell.Ui.Text("standard-display"), "Typing must work while the closing frame is still visible.");
            shell.Ui.Key(VirtualKeyShort.TAB);
            Assert.IsFalse(shell.Ui.FocusedName.StartsWith("navigation-", StringComparison.Ordinal));
        }
        // Move focus deliberately before completion: a timer callback must never steal it.
        shell.Ui.Do(host => UiThreadHarness.Find<Button>(host, "navigation-toggle").Focus());
        shell.Ui.Do(host =>
        {
            now += 180;
            ((MainForm)host).NavigationTransition.Advance();
            Assert.IsFalse(UiThreadHarness.Find<Panel>(host, "navigation").Visible);
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "navigation-toggle").Focused);
        });
    }

    [TestMethod]
    public void NativeTimerFinishesAndWindowDisposalStopsAnActiveTransition()
    {
        using (var shell = new ShellFixture(ModeId.STANDARD, animationsEnabled: () => true))
        {
            shell.Ui.Click("navigation-toggle");
            shell.Ui.WaitForNavigation();
            shell.Ui.Do(host => Assert.AreEqual(1d, ((MainForm)host).NavigationTransition.Position));
            shell.Ui.Click("navigation-toggle");
            shell.Ui.WaitForNavigation();
            shell.Ui.Do(host => Assert.AreEqual(0d, ((MainForm)host).NavigationTransition.Position));
        }
        var closing = new ShellFixture(ModeId.STANDARD, animationClock: () => 0, animationsEnabled: () => true);
        closing.Ui.Click("navigation-toggle");
        NavigationTransition slide = closing.Ui.Read(host => ((MainForm)host).NavigationTransition);
        closing.Dispose();
        Assert.IsFalse(slide.IsRunning);
        slide.Advance();
    }

    private static void AssertClip(Panel clip, int reveal)
    {
        using var graphics = clip.CreateGraphics();
        Assert.AreEqual(new RectangleF(0, 0, reveal, clip.Height), clip.Region!.GetBounds(graphics));
    }

    private static string FocusDetails(Form form) => $"Active control: {form.ActiveControl?.Name}; focused: {string.Join(",", ModeView.Descendants(form).Where(control => control.Focused).Select(control => control.Name))}";

    private static void Capture(Form form, string name)
    {
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "screenshots"));
        Directory.CreateDirectory(directory);
        form.Refresh();
        using var image = new Bitmap(form.Width, form.Height);
        using (var graphics = Graphics.FromImage(image)) graphics.CopyFromScreen(form.Location, Point.Empty, form.Size);
        image.Save(Path.Combine(directory, $"{name}-{form.DeviceDpi}dpi.png"), ImageFormat.Png);
    }
}
