using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class GraphInteractionTests
{
    [TestMethod]
    public void GraphKeypadReplacesSavedSelectionAndContinuesThroughCurrentFocusOwner()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host =>
        {
            TextBox field = UiThreadHarness.Find<TextBox>(host, "graph-expression-1");
            field.Text = "12+34";
            field.Focus(); field.Select(3, 2);
        });
        ui.Click("key-x");
        Assert.AreEqual("12+x", ui.Text("graph-expression-1"));
        ui.Key(VirtualKeyShort.ADD);
        ui.Key(VirtualKeyShort.KEY_2);
        ui.Key(VirtualKeyShort.BACK);
        ui.Key(VirtualKeyShort.KEY_3);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text == "12+x+3", "Typing after keypad use did not reach the saved expression target.");
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(host => !UiThreadHarness.Find<Label>(host, "graph-status").Text.StartsWith("Plotting", StringComparison.Ordinal), "Plot did not complete.");
        Assert.AreEqual("", ui.Text("graph-error-1"));
    }

    [TestMethod]
    public void GraphFunctionPopupPreservesSelectionAndRestoresKeyboardRouting()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host =>
        {
            TextBox field = UiThreadHarness.Find<TextBox>(host, "graph-expression-1");
            field.Text = "4+12"; field.Focus(); field.Select(2, 2);
        });
        ui.Click("graph-functions");
        ui.Key(VirtualKeyShort.DOWN);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text == "4+asin(", "Native popup selection did not replace the saved range.");
        ui.Key(VirtualKeyShort.KEY_0);
        ui.Chord(VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_0);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text == "4+asin(0)", "Keyboard focus was not restored after choosing a graph function.");
    }

    [TestMethod]
    public void GraphRowsRetainTargetsAndDisableInputWhenAllAreRemoved()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Click("key-1");
        ui.Click("graph-add");
        ui.Click("key-2");
        Assert.AreEqual("1", ui.Text("graph-expression-1"));
        Assert.AreEqual("2", ui.Text("graph-expression-2"));
        ui.Click("graph-remove-2");
        ui.Click("key-3");
        Assert.AreEqual("13", ui.Text("graph-expression-1"));
        ui.Click("graph-remove-1");
        ui.Do(host => Assert.IsFalse(UiThreadHarness.Find<KeypadControl>(host, "graph-keypad").Enabled));
        ui.Click("graph-add");
        ui.Click("key-x");
        Assert.AreEqual("x", ui.Text("graph-expression-1"));
        for (int i = 1; i < 6; i++) ui.Click("graph-add");
        ui.Do(host =>
        {
            Assert.IsFalse(UiThreadHarness.Find<Button>(host, "graph-add").Enabled);
            Assert.AreEqual(6, ModeView.Descendants(host).OfType<TextBox>().Count(field => field.Name.StartsWith("graph-expression-", StringComparison.Ordinal)));
        });
    }

    [TestMethod]
    public void GraphTraceUsesCommittedExpressionsAndReportsInvalidRanges()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text = "x^2");
        ui.Click("graph-plot");
        ui.Click("graph-advanced");
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "graph-trace-x").Text = "3");
        Assert.AreEqual("9", ui.Text("graph-trace-y"));
        ui.Do(host =>
        {
            var graph = UiThreadHarness.Find<GraphView>(host, "graph-view");
            Assert.AreEqual("f1: x=3, y=9", graph.CopyValue());
            UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text = "x^3";
        });
        Assert.AreEqual("", ui.Text("graph-trace-y"));
        ui.Click("graph-plot");
        Assert.AreEqual("27", ui.Text("graph-trace-y"));
        ui.Do(host =>
        {
            UiThreadHarness.Find<TextBox>(host, "graph-range-0").Text = "10";
            UiThreadHarness.Find<TextBox>(host, "graph-range-1").Text = "-10";
            UiThreadHarness.Find<Button>(host, "graph-apply-ranges").PerformClick();
        });
        Assert.IsTrue(ui.Text("graph-range-error").Contains("increase", StringComparison.Ordinal));
        ui.Click("graph-reset");
        Assert.AreEqual("", ui.Text("graph-range-error"));
        Assert.AreEqual("-10", ui.Text("graph-range-0"));
    }

    [TestMethod]
    public void GraphExpressionLimitAndNativeSelectionArePreserved()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host =>
        {
            TextBox field = UiThreadHarness.Find<TextBox>(host, "graph-expression-1");
            field.Text = new string('1', 4096); field.Focus(); field.Select(4096, 0);
        });
        ui.Click("key-2");
        Assert.AreEqual(4096, ui.Text("graph-expression-1").Length);
        ui.Do(host =>
        {
            TextBox field = UiThreadHarness.Find<TextBox>(host, "graph-expression-1");
            field.Focus(); field.Select(100, 10);
        });
        ui.Click("key-x");
        Assert.AreEqual(4087, ui.Text("graph-expression-1").Length);
        Assert.AreEqual('x', ui.Text("graph-expression-1")[100]);
    }

    [TestMethod]
    public void GraphTracePreservesDecimalValuesBelowTheDoubleRange()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text = "x");
        ui.Click("graph-plot");
        ui.Click("graph-advanced");
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "graph-trace-x").Text = "1E-500");
        Assert.AreEqual("1E-500", ui.Text("graph-trace-y"));
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "graph-trace-x").Text = "1000001");
        Assert.AreEqual("Undefined / invalid X", ui.Text("graph-trace-y"));
    }

    [TestMethod]
    public void GraphScrollingKeepsKeypadAndPlotInTheirOwnColumnsAtMinimumSize()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        for (int i = 1; i < 6; i++) ui.Click("graph-add");
        ui.Click("graph-advanced");
        ui.Do(host =>
        {
            host.Size = host.MinimumSize;
            host.PerformLayout();
            UiThreadHarness.AssertKeypadBounds(host);
            var graph = UiThreadHarness.Find<GraphView>(host, "graph-view");
            var plot = UiThreadHarness.Find<Control>(host, "graph-canvas");
            var keypad = UiThreadHarness.Find<KeypadControl>(host, "graph-keypad");
            var expressions = UiThreadHarness.Find<Panel>(host, "graph-expressions-scroll");
            var advanced = UiThreadHarness.Find<Panel>(host, "graph-advanced-scroll");
            Assert.IsTrue(expressions.VerticalScroll.Visible);
            Assert.IsTrue(advanced.VerticalScroll.Visible);
            Rectangle before = keypad.RectangleToScreen(keypad.ClientRectangle);
            expressions.AutoScrollPosition = new Point(0, expressions.AutoScrollMinSize.Height);
            advanced.AutoScrollPosition = new Point(0, advanced.AutoScrollMinSize.Height);
            Assert.AreEqual(before, keypad.RectangleToScreen(keypad.ClientRectangle));
            Assert.IsTrue(plot.RectangleToScreen(plot.ClientRectangle).Right < before.Left);
            Assert.IsTrue(graph.RectangleToScreen(graph.ClientRectangle).Contains(before));
        });
    }

    [TestMethod]
    public void GraphModeTransitionsKeepExpressionsAndEnforceWindowRequirements()
    {
        using var fixture = new GraphFixture();
        fixture.Ui.Do(host =>
        {
            var main = (MainForm)host;
            var graph = (GraphView)main.ActiveMode!;
            UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text = "sin(x)";
            UiThreadHarness.Find<Button>(host, "graph-plot").PerformClick();
            Size graphSize = main.Size;
            main.SelectMode(ModeId.STANDARD);
            Assert.AreEqual(graphSize, main.Size, "Leaving Graphing must not shrink the window.");
            main.SelectMode(ModeId.GRAPHING);
            Assert.AreSame(graph, main.ActiveMode);
            Assert.AreEqual("sin(x)", UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text);
            Assert.IsTrue(main.MinimumSize.Width >= 800 * main.DeviceDpi / 96d);
            Assert.IsTrue(main.MinimumSize.Height >= 640 * main.DeviceDpi / 96d);
        });
    }

    [TestMethod]
    public void GraphPlotShortcutsPanAndZoomWhileRangeFieldsKeepNativeEditing()
    {
        using var fixture = new GraphFixture();
        var ui = fixture.Ui;
        ui.Do(host =>
        {
            UiThreadHarness.Find<TextBox>(host, "graph-expression-1").Text = "x^2";
            UiThreadHarness.Find<Control>(host, "graph-canvas").Focus();
        });
        ui.Key(VirtualKeyShort.RIGHT);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-range-0").Text == "-8", "The focused plot did not pan with the Right key.");
        ui.Key(VirtualKeyShort.ADD);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-range-0").Text == "-6", "The focused plot did not zoom with the Plus key.");
        ui.Key(VirtualKeyShort.HOME);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-range-0").Text == "-10", "The focused plot did not reset with Home.");
        ui.Click("graph-advanced");
        ui.Do(host =>
        {
            TextBox range = UiThreadHarness.Find<TextBox>(host, "graph-range-0");
            range.Focus();
            range.SelectAll();
        });
        ui.Key(VirtualKeyShort.SUBTRACT);
        ui.Key(VirtualKeyShort.KEY_5);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "graph-range-0").Text == "-5", "Native range editing did not apply.");
        Assert.AreEqual("x^2", ui.Text("graph-expression-1"), "Range input must not modify the active expression.");
        Assert.AreEqual("", ui.Text("graph-range-error"));
    }

    private sealed class GraphFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "CalculatorGraphTests", Guid.NewGuid().ToString("N"));
        public UiThreadHarness Ui { get; }
        public GraphFixture()
        {
            Ui = new UiThreadHarness(() =>
            {
                var settings = new SettingsStore(directory, importLegacy: false);
                settings.Put("mode", "GRAPHING");
                return new MainForm(settings);
            });
        }
        public void Dispose()
        {
            Ui.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
