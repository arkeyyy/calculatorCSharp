using Calculator.Core;
using Calculator.Infrastructure;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using ControlsUi = Calculator.Controls.Ui;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class FocusRegressionTests
{
    [DataTestMethod]
    [DataRow("scientific-trigonometry", 0, "12+sin(", "12.5")]
    [DataRow("scientific-functions", 3, "12+abs(", "16")]
    public void ScientificPopupCommitRestoresRoutingAndSavedSelection(string menu, int index, string inserted, string expected)
    {
        using var shell = new ShellFixture(ModeId.SCIENTIFIC);
        UiThreadHarness ui = shell.Ui;
        using var clipboard = new ClipboardRestoreScope(ui);
        ui.Do(host =>
        {
            TextBox expression = UiThreadHarness.Find<TextBox>(host, "scientific-expression");
            expression.Text = "12+999";
            expression.Focus();
            expression.Select(3, 3);
            Assert.IsTrue(expression.Focused, "The expression must have native focus before establishing a selection.");
        });
        ui.Click(menu);
        ui.WaitUntil(_ => ControlsUi.PopupDepth > 0, "Scientific menu did not open.");
        ui.Key(VirtualKeyShort.HOME);
        for (int i = 0; i < index; i++) ui.Key(VirtualKeyShort.DOWN);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(_ => ControlsUi.PopupDepth == 0, "Scientific menu did not commit and close.");
        Assert.AreEqual(inserted, ui.Text("scientific-expression"), "The real menu handler must replace the saved selection.");
        Assert.AreEqual("scientific-view", ui.FocusedName, "A committed command must synchronously focus the calculator root.");
        if (index == 0)
        {
            ui.Key(VirtualKeyShort.KEY_3);
            ui.Key(VirtualKeyShort.KEY_0);
        }
        else ui.Key(VirtualKeyShort.KEY_4);
        ui.Key(VirtualKeyShort.KEY_2);
        ui.Key(VirtualKeyShort.BACK);
        ui.Chord(VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_0);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "scientific-display").Text == expected,
            "Digits, Backspace and Enter were not routed after the actual menu selection.");
        ui.Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
        ui.WaitUntil(_ => Clipboard.ContainsText() && Clipboard.GetText() == expected, "Copy stopped routing after the Scientific menu.");
    }

    [TestMethod]
    public void ProgrammerNativeMenuNavigationPreservesKeyboardAndClipboardRouting()
    {
        using var shell = new ShellFixture(ModeId.PROGRAMMER);
        UiThreadHarness ui = shell.Ui;
        using var clipboard = new ClipboardRestoreScope(ui);
        ui.Click("key-6");
        ui.Click("programmer-bitwise");
        ui.WaitUntil(_ => ControlsUi.PopupDepth > 0, "Bitwise popup did not open.");
        ui.Key(VirtualKeyShort.HOME);
        ui.Key(VirtualKeyShort.DOWN);
        ui.Key(VirtualKeyShort.UP);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(_ => ControlsUi.PopupDepth == 0, "Native popup selection did not commit AND.");
        Assert.AreEqual("programmer-view", ui.FocusedName);
        ui.Key(VirtualKeyShort.KEY_3);
        ui.Key(VirtualKeyShort.KEY_1);
        ui.Key(VirtualKeyShort.BACK);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "programmer-display").Text == "2",
            "6 AND 3 did not evaluate after native popup selection and keyboard editing.");
        ui.Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);
        ui.WaitUntil(_ => Clipboard.ContainsText() && Clipboard.GetText() == "2", "Programmer Copy did not route.");
        ui.Do(_ => Clipboard.SetText("7"));
        ui.Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
        ui.WaitUntil(host => UiThreadHarness.Find<TextBox>(host, "programmer-display").Text == "7", "Programmer Paste did not route.");
    }

    [TestMethod]
    public void AutomationCanDiscoverNamedNativeCalculatorControls()
    {
        using var shell = new ShellFixture(ModeId.SCIENTIFIC);
        using var automation = new UIA2Automation();
        var window = automation.FromHandle(shell.Ui.Handle);
        Assert.IsNotNull(window.FindFirstDescendant(condition => condition.ByAutomationId("scientific-expression")));
        Assert.IsNotNull(window.FindFirstDescendant(condition => condition.ByAutomationId("scientific-functions")));
    }
}

internal sealed class ShellFixture : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CalculatorTests", Guid.NewGuid().ToString("N"));
    public UiThreadHarness Ui { get; }
    public SessionStore Session { get; } = new();
    public SettingsStore Settings { get; }

    public ShellFixture(ModeId mode, string theme = "light", string accent = "BLUE", ICurrencyRateProvider? currencyRateProvider = null,
        Func<double>? animationClock = null, Func<bool?>? animationsEnabled = null)
    {
        Settings = new SettingsStore(directory, importLegacy: false);
        Settings.Put("mode", mode.ToString());
        Settings.Put("theme", theme);
        Settings.Put("accent", accent);
        Ui = new UiThreadHarness(() => new MainForm(Settings, Session, currencyRateProvider, animationClock, animationsEnabled));
    }

    public void Select(ModeId mode) => Ui.Do(host => ((MainForm)host).SelectMode(mode));

    public void Dispose()
    {
        Ui.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

internal sealed class ClipboardRestoreScope : IDisposable
{
    private readonly UiThreadHarness ui;
    private readonly IDataObject? original;
    public ClipboardRestoreScope(UiThreadHarness ui)
    {
        this.ui = ui;
        original = ui.Read(_ => Clipboard.GetDataObject());
    }
    public void Dispose() => ui.Do(_ =>
    {
        if (original is null) Clipboard.Clear();
        else Clipboard.SetDataObject(original, copy: true);
    });
}
