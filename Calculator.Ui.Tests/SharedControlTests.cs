using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using ControlsUi = Calculator.Controls.Ui;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class SharedControlTests
{
    [TestMethod]
    public void KeypadIntegerCellsStayWithinOddRenderedSizes()
    {
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form { ClientSize = new Size(641, 503) };
            host.Controls.Add(new KeypadControl([["", "CE", "⌫"], ["7", "8", "9"], ["4", "5", "6"], ["1", "2", "3"], ["±", "0", "."]], _ => { })
            { Dock = DockStyle.Fill, Expanding = true });
            return host;
        });
        ui.Do(host =>
        {
            foreach (Size size in new[] { new Size(641, 503), new Size(799, 607), new Size(1001, 719) })
            {
                host.ClientSize = size;
                host.PerformLayout();
                UiThreadHarness.AssertKeypadBounds(host);
                var keypad = host.Controls.OfType<KeypadControl>().Single();
                Assert.AreEqual(keypad.ClientSize.Height, keypad.Buttons.Max(button => button.Bottom));
                Assert.AreEqual(14, keypad.Buttons.Count, "The intentional blank must not become an interactive button.");
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("CALCULATOR_EXPECT_DPI"), out int expected))
                Assert.AreEqual(expected, host.DeviceDpi, "Run this verification on the requested actual Windows display scale.");
        });
    }

    [TestMethod]
    public void ExpressionSelectionSurvivesFocusMovingToAButton()
    {
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form();
            var field = ControlsUi.TextField("expression");
            field.SetBounds(10, 10, 250, 50);
            var editor = new ExpressionEditor(field);
            var button = ControlsUi.Button("Insert", () => editor.Insert("sin"), "insert");
            button.SetBounds(10, 80, 100, 35);
            host.Controls.AddRange([field, button]);
            return host;
        });
        ui.Do(host =>
        {
            var field = UiThreadHarness.Find<TextBox>(host, "expression");
            field.Text = "12+34";
            field.Focus();
            field.Select(3, 2);
        });
        ui.Click("insert");
        Assert.AreEqual("12+sin(", ui.Text("expression"));
    }

    [TestMethod]
    public void SettingsRoundTripPreservesIdentifiersAndIsolatesEphemeralState()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CalculatorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new SettingsStore(directory, importLegacy: false);
            settings.Put("mode", "STORAGE");
            settings.Put("theme", "dark");
            settings.Put("accent", "TEAL");
            settings.Put("currency.from", "JPY");
            settings.Put("currency.to", "PHP");
            settings.Flush();
            Assert.IsNull(settings.LastError);
            var reopened = new SettingsStore(directory, importLegacy: false);
            Assert.AreEqual("STORAGE", reopened.Get("mode", "STANDARD"));
            Assert.AreEqual("TEAL", reopened.Get("accent", "BLUE"));
            Assert.AreEqual("JPY", reopened.Get("currency.from", "EUR"));
            var session = new SessionStore();
            session.StoreMemory(Numbers.Parse("12.5"));
            session.AddHistory(ModeId.STANDARD, "2 + 3 =", "5");
            Assert.IsNull(new SessionStore().Memory);
            Assert.AreEqual(0, new SessionStore().History.Count);
            Assert.IsFalse(File.ReadAllText(settings.StoragePath).Contains("12.5", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
