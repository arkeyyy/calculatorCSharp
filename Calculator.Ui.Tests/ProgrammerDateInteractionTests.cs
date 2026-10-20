using Calculator.Controls;
using Calculator.Core;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;
using ControlsUi = Calculator.Controls.Ui;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class ProgrammerDateInteractionTests
{
    [TestMethod]
    public void ProgrammerShowsPendingAndCompletedExpressionsWithoutMovingTheKeypad()
    {
        using var shell = new ShellFixture(ModeId.PROGRAMMER);
        shell.Ui.Do(host =>
        {
            host.Size = host.MinimumSize;
            var view = (ProgrammerView)((MainForm)host).ActiveMode!;
            var keypad = ModeView.Descendants(view).OfType<KeypadControl>().Single();
            var caption = UiThreadHarness.Find<Label>(host, "programmer-expression");
            var display = UiThreadHarness.Find<TextBox>(host, "programmer-display");
            Rectangle keypadBounds = keypad.Bounds;
            Assert.IsFalse(caption.Visible);
            view.HandleKey(Keys.D6);
            view.HandleKey(Keys.Add);
            Assert.AreEqual("6 ADD", caption.Text);
            Assert.IsTrue(caption.Visible);
            Assert.IsTrue(caption.Bottom <= display.Top, "The expression must not overlap the current value.");
            Assert.AreEqual(keypadBounds, keypad.Bounds);
            UiThreadHarness.AssertKeypadBounds(host);
            view.HandleKey(Keys.D3);
            view.HandleKey(Keys.Enter);
            Assert.AreEqual("6 ADD 3 = (DEC, 64-bit)", caption.Text);
            Assert.AreEqual("9", display.Text);
            Assert.AreEqual(keypadBounds, keypad.Bounds);
            view.HandleKey(Keys.Escape);
            Assert.AreEqual("", caption.Text);
            Assert.IsFalse(caption.Visible);
            Assert.AreEqual("0", display.Text);
            Assert.AreEqual(keypadBounds, keypad.Bounds);
        });
    }

    [TestMethod]
    public void EditingAScrolledProgrammerBitKeepsItVisibleAndDoesNotMoveTheKeypad()
    {
        using var shell = new ShellFixture(ModeId.PROGRAMMER);
        shell.Ui.Do(host => host.Size = host.MinimumSize);
        shell.Ui.Click("programmer-expand-bits");
        shell.Ui.Do(host =>
        {
            var bits = UiThreadHarness.Find<Panel>(host, "programmer-bit-scroll");
            var grid = UiThreadHarness.Find<Panel>(host, "programmer-bit-editor");
            var view = (ProgrammerView)((MainForm)host).ActiveMode!;
            var keypad = ModeView.Descendants(view).OfType<KeypadControl>().Single();
            UiThreadHarness.AssertKeypadBounds(host);
            Assert.IsTrue(bits.VerticalScroll.Visible);
            bits.AutoScrollPosition = new Point(0, grid.Height);
            int offset = bits.AutoScrollPosition.Y;
            Assert.IsTrue(offset < 0);
            Rectangle keypadBounds = keypad.RectangleToScreen(keypad.ClientRectangle);
            Button bit = UiThreadHarness.Find<Button>(host, "bit-0");
            Assert.IsTrue(bits.RectangleToScreen(bits.ClientRectangle).IntersectsWith(bit.RectangleToScreen(bit.ClientRectangle)));
            bit.PerformClick();
            Assert.AreEqual("1", UiThreadHarness.Find<TextBox>(host, "programmer-display").Text);
            Assert.AreEqual(offset, bits.AutoScrollPosition.Y, "Editing a visible bit must preserve the independent scroll offset.");
            Assert.AreEqual(offset, grid.Top, "The bit grid must retain its scrolled location during layout.");
            Assert.AreEqual(keypadBounds, keypad.RectangleToScreen(keypad.ClientRectangle));
            UiThreadHarness.Find<ComboBox>(host, "programmer-word").SelectedItem = "BYTE";
            Assert.AreEqual(8, grid.Controls.Cast<Control>().Count(control => control.Visible));
            UiThreadHarness.Find<ComboBox>(host, "programmer-base").SelectedItem = "BIN";
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "key-1").Enabled);
            Assert.IsFalse(UiThreadHarness.Find<Button>(host, "key-2").Enabled);
            Assert.IsFalse(UiThreadHarness.Find<Button>(host, "key-A").Enabled);
        });
    }

    [TestMethod]
    public void DateAdjustmentsRetainSignedDifferencesAndExtendedIsoDates()
    {
        using var shell = new ShellFixture(ModeId.DATE);
        shell.Ui.Do(host =>
        {
            UiThreadHarness.Find<TextBox>(host, "Start date-input").Text = "2024-01-10";
            UiThreadHarness.Find<TextBox>(host, "End date-input").Text = "2024-01-01";
            Assert.AreEqual("-9 days\n-1 weeks, -2 days", UiThreadHarness.Find<Label>(host, "date-result").Text);
            UiThreadHarness.Find<ComboBox>(host, "date-operation").SelectedIndex = 1;
            UiThreadHarness.Find<TextBox>(host, "Start date-input").Text = "9999-12-31";
            UiThreadHarness.Find<NumericUpDown>(host, "date-days").Value = 1;
            Assert.AreEqual("+10000-01-01", UiThreadHarness.Find<Label>(host, "date-result").Text);
            UiThreadHarness.Find<TextBox>(host, "Start date-input").Text = "2024-02-29";
            UiThreadHarness.Find<NumericUpDown>(host, "date-days").Value = 0;
            UiThreadHarness.Find<NumericUpDown>(host, "date-years").Value = 1;
            UiThreadHarness.Find<NumericUpDown>(host, "date-months").Value = 1;
            Assert.AreEqual("2025-03-28", UiThreadHarness.Find<Label>(host, "date-result").Text);
            UiThreadHarness.Find<TextBox>(host, "Start date-input").Text = "2024-02-30";
            Assert.AreEqual("—", UiThreadHarness.Find<Label>(host, "date-result").Text);
            Assert.AreNotEqual("", UiThreadHarness.Find<Label>(host, "date-error").Text);
        });
    }

    [TestMethod]
    public void DateCalendarAllowsNativeKeyboardSelectionAndCancellation()
    {
        using var shell = new ShellFixture(ModeId.DATE);
        var ui = shell.Ui;
        ui.Do(host => UiThreadHarness.Find<TextBox>(host, "Start date-input").Text = "2024-04-15");
        ui.Click("Start date-calendar");
        ui.WaitUntil(_ => ControlsUi.PopupDepth > 0, "The native date calendar did not open.");
        ui.Key(VirtualKeyShort.RIGHT);
        ui.Key(VirtualKeyShort.RETURN);
        ui.WaitUntil(_ => ControlsUi.PopupDepth == 0, "The native date selection did not close the popup.");
        Assert.AreEqual("2024-04-16", ui.Text("Start date-input"));
        ui.Click("Start date-calendar");
        ui.Key(VirtualKeyShort.RIGHT);
        ui.Key(VirtualKeyShort.ESCAPE);
        ui.WaitUntil(_ => ControlsUi.PopupDepth == 0, "Escape did not close the date calendar.");
        Assert.AreEqual("2024-04-16", ui.Text("Start date-input"));
    }
}
