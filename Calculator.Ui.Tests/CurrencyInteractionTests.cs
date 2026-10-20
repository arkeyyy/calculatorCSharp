using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class CurrencyInteractionTests
{
    [TestMethod]
    public void BothPopupsPreviewCycleCommitCancelAndKeepLabelsAfterRefresh()
    {
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form { ClientSize = new Size(500, 200) };
            foreach (string name in new[] { "first", "second" })
            {
                var selector = new CurrencySelector { Name = name, Bounds = new Rectangle(12, name == "first" ? 12 : 80, 450, 40) };
                selector.SetCodes(["CAD", "CNY", "CZK", "JPY", "EUR", "USD"], "USD");
                host.Controls.Add(selector);
            }
            return host;
        });
        foreach (string name in new[] { "first", "second" })
        {
            ui.Click(name);
            ui.Key(VirtualKeyShort.KEY_C);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "CAD", "C must preview Canada.");
            ui.Key(VirtualKeyShort.KEY_C);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "CNY", "Repeated C must preview China.");
            Assert.AreEqual("USD", ui.Read(host => UiThreadHarness.Find<CurrencySelector>(host, name).CommittedCode));
            ui.Key(VirtualKeyShort.ESCAPE);
            ui.WaitUntil(host => !UiThreadHarness.Find<CurrencySelector>(host, name).PopupOpen, "Escape must close the popup.");
            Assert.AreEqual("USD", ui.Read(host => UiThreadHarness.Find<CurrencySelector>(host, name).CommittedCode));
            ui.Click(name);
            ui.Key(VirtualKeyShort.KEY_J);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "JPY", "J must preview Japan after the search buffer resets.");
            ui.Key(VirtualKeyShort.RETURN);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).CommittedCode == "JPY", "Enter must commit the actual popup selection.");
            Assert.AreEqual("Japan — Japanese yen (JPY)", ui.Text(name));
            ui.Do(host => UiThreadHarness.Find<CurrencySelector>(host, name).SetCodes(["JPY", "EUR", "USD", "CNY"], "JPY"));
            Assert.AreEqual("Japan — Japanese yen (JPY)", ui.Text(name));
            ui.Click(name);
            ui.Key(VirtualKeyShort.KEY_C);
            ui.Key(VirtualKeyShort.KEY_H);
            ui.Key(VirtualKeyShort.KEY_I);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "CNY", "A rapid country prefix must preview China.");
            ui.Key(VirtualKeyShort.KEY_Z);
            Assert.AreEqual("CNY", ui.Read(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode), "A failed prefix must preserve the highlight.");
            Assert.IsTrue(ui.Read(host => UiThreadHarness.Find<CurrencySelector>(host, name).PopupOpen));
            ui.Key(VirtualKeyShort.HOME);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "CNY", "Home must keep native list navigation.");
            ui.Key(VirtualKeyShort.END);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, name).PreviewCode == "USD", "End must keep native list navigation.");
            ui.Key(VirtualKeyShort.ESCAPE);
            Assert.AreEqual("JPY", ui.Read(host => UiThreadHarness.Find<CurrencySelector>(host, name).CommittedCode));
        }
    }

    [TestMethod]
    public void ConverterNativeSelectionAndRejectedEditsKeepAuthority()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CalculatorTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var ui = new UiThreadHarness(() =>
            {
                var host = new Form { ClientSize = new Size(400, 500) };
                host.Controls.Add(new ConverterView(ModeId.LENGTH, new SettingsStore(directory, false), new SessionStore()) { Dock = DockStyle.Fill });
                return host;
            });
            ui.Do(host =>
            {
                var view = host.Controls.OfType<ConverterView>().Single();
                var first = UiThreadHarness.Find<TextBox>(host, "conversion-input");
                var second = UiThreadHarness.Find<TextBox>(host, "conversion-result");
                first.Focus(); first.Text = "12345"; first.Select(1, 3);
                UiThreadHarness.Find<Button>(host, "key-9").Focus();
                UiThreadHarness.Find<Button>(host, "key-9").PerformClick();
                Assert.AreEqual("195", first.Text);
                string before = view.EntryState.RawSource;
                second.Focus();
                Assert.AreEqual(0, view.EntryState.SourceSide);
                second.Text = "invalid";
                Assert.AreEqual(before, view.EntryState.RawSource);
                Assert.AreEqual(0, view.EntryState.SourceSide);
                Assert.AreEqual(view.EntryState.Text(1), second.Text);
                second.SelectAll();
            });
            ui.Key(VirtualKeyShort.KEY_2);
            ui.WaitUntil(host => host.Controls.OfType<ConverterView>().Single().EntryState.SourceSide == 1, "Accepted native typing must reverse conversion.");
            Assert.AreEqual("20", ui.Text("conversion-input"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public async Task RefreshKeepsPreviewByCodeAndPreservesCommittedLabelsRawQuantityAndOfflineRates()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CalculatorTests", Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new SettingsStore(directory, false);
            settings.Put("currency.snapshot", "2026-10-15\nCNY=7.5\nEUR=1\nJPY=177.06\nUSD=1.1208");
            settings.Put("currency.from", "CNY");
            settings.Put("currency.to", "USD");
            var provider = new ControlledProvider();
            using var ui = new UiThreadHarness(() =>
            {
                var host = new Form { ClientSize = new Size(500, 550) };
                host.Controls.Add(new ConverterView(ModeId.CURRENCY, settings, new SessionStore(), provider) { Dock = DockStyle.Fill });
                return host;
            });
            ui.Do(host => UiThreadHarness.Find<TextBox>(host, "currency-input").Text = "1.23456789012345678901234567890123456789");
            string raw = ui.Text("currency-input");
            ui.Click("currency-from");
            ui.Key(VirtualKeyShort.KEY_J);
            ui.WaitUntil(host => UiThreadHarness.Find<CurrencySelector>(host, "currency-from").PreviewCode == "JPY", "J must preview Japan before refresh.");
            Task refresh = ui.Read(host => host.Controls.OfType<ConverterView>().Single().RefreshRatesAsync());
            provider.Pending.SetResult(CurrencyRateSnapshot.Decode("2026-10-16\nCAD=1.5\nCNY=7.6\nEUR=1\nJPY=178\nUSD=1.13"));
            await refresh;
            ui.Do(host =>
            {
                var selector = UiThreadHarness.Find<CurrencySelector>(host, "currency-from");
                Assert.IsTrue(selector.PopupOpen);
                Assert.AreEqual("JPY", selector.PreviewCode, "Insertion before Japan must not move preview to a different ISO code.");
                Assert.AreEqual("CNY", selector.CommittedCode);
                Assert.AreEqual("China — Chinese yuan (CNY)", selector.Text);
                Assert.AreEqual(raw, UiThreadHarness.Find<TextBox>(host, "currency-input").Text);
                Assert.AreEqual(0, host.Controls.OfType<ConverterView>().Single().EntryState.SourceSide);
            });
            ui.Key(VirtualKeyShort.ESCAPE);
            string result = ui.Text("currency-result");
            refresh = ui.Read(host => host.Controls.OfType<ConverterView>().Single().RefreshRatesAsync());
            provider.Pending.SetException(new HttpRequestException("Offline test"));
            await refresh;
            Assert.AreEqual(result, ui.Text("currency-result"));
            Assert.AreEqual("China — Chinese yuan (CNY)", ui.Text("currency-from"));
            StringAssert.Contains(ui.Text("conversion-status"), "using cached rates");
            StringAssert.Contains(ui.Text("conversion-status"), "2026-10-16");
            Assert.AreEqual("CNY", settings.Get("currency.from", ""));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class ControlledProvider : ICurrencyRateProvider
    {
        public TaskCompletionSource<CurrencyRateSnapshot> Pending { get; private set; } = new();
        public Task<CurrencyRateSnapshot> FetchAsync(CancellationToken cancellationToken = default)
        {
            Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return Pending.Task;
        }
    }
}
