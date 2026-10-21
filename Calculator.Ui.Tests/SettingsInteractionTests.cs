using System.Drawing.Imaging;
using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;
using Calculator.Views;
using FlaUI.Core.WindowsAPI;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class SettingsInteractionTests
{
    [DataTestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void SettingsCardsAndLabelsFitNarrowDefaultAndLargeWindows(string theme)
    {
        using var shell = OpenSettings();
        shell.Ui.Click("settings-theme-" + theme);
        shell.Ui.Do(host =>
        {
            var settings = (SettingsView)((MainForm)host).ActiveMode!;
            var shortcuts = UiThreadHarness.Find<SettingsSection>(settings, "settings-shortcuts");
            var about = UiThreadHarness.Find<SettingsSection>(settings, "settings-about");
            Assert.IsFalse(shortcuts.Expanded);
            Assert.IsFalse(about.Expanded);
            Assert.IsInstanceOfType<Label>(UiThreadHarness.Find<Control>(settings, "settings-appearance-title"));
            foreach (Size size in new[] { new Size(320, 500), new Size(400, 600), new Size(1000, 800) })
            {
                host.Size = new Size(Scale(host, size.Width), Scale(host, size.Height));
                settings.AutoScrollPosition = Point.Empty;
                shortcuts.Expanded = about.Expanded = false;
                host.PerformLayout();
                AssertSettingsBounds(settings);
                Assert.IsTrue(settings.Width <= Scale(host, 560));
                var accents = UiThreadHarness.Find<Panel>(settings, "settings-accents");
                int expectedColumns = accents.Width >= Scale(host, 300) ? 3 : 2;
                Assert.AreEqual(expectedColumns, accents.Controls.Cast<Control>().Count(choice => choice.Top == 0));
                foreach (AppearanceChoice choice in ModeView.Descendants(settings).OfType<AppearanceChoice>())
                {
                    int labelWidth = TextRenderer.MeasureText(choice.Text, choice.Font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                    Assert.IsTrue(labelWidth <= choice.Width - Scale(choice, 42), $"{choice.Name} label is clipped at {size}.");
                }
                Capture(host, $"settings-{theme}-{size.Width}x{size.Height}");
                shortcuts.Expanded = about.Expanded = true;
                settings.PerformLayout();
                AssertSettingsBounds(settings);
                foreach (Label label in ModeView.Descendants(settings).OfType<Label>().Where(label => label.Visible))
                {
                    int height = TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, label.Width), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    Assert.IsTrue(label.Height >= height, $"{label.Name}: {label.Text} is clipped at {size}.");
                }
                settings.ScrollControlIntoView(UiThreadHarness.Find<Label>(settings, "settings-about-storage"));
                Capture(host, $"settings-{theme}-{size.Width}x{size.Height}-expanded");
            }
        });
    }

    [TestMethod]
    public void NativeRadioNavigationSavesPreferencesAndKeepsFocus()
    {
        using var shell = OpenSettings();
        shell.Ui.Do(host => UiThreadHarness.Find<RadioButton>(host, "settings-theme-light").Focus());
        shell.Ui.Key(VirtualKeyShort.RIGHT);
        Assert.AreEqual("dark", shell.Settings.Get("theme", ""));
        Assert.AreEqual("settings-theme-dark", shell.Ui.FocusedName);
        shell.Ui.Do(host =>
        {
            var choice = UiThreadHarness.Find<RadioButton>(host, "settings-theme-dark");
            Assert.IsTrue(choice.Checked);
            Assert.IsTrue(choice.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));
            Assert.AreEqual(AccessibleRole.RadioButton, choice.AccessibilityObject.Role);
            UiThreadHarness.Find<RadioButton>(host, "accent-BLUE").Focus();
        });
        shell.Ui.Key(VirtualKeyShort.RIGHT);
        Assert.AreEqual("PURPLE", shell.Settings.Get("accent", ""));
        Assert.AreEqual("accent-PURPLE", shell.Ui.FocusedName);
        shell.Ui.Do(host =>
        {
            var choices = UiThreadHarness.Find<Panel>(host, "settings-accents").Controls.OfType<RadioButton>();
            Assert.AreEqual(1, choices.Count(choice => choice.Checked));
        });
        var reopened = new SettingsStore(Path.GetDirectoryName(shell.Settings.StoragePath)!, false);
        Assert.AreEqual("dark", reopened.Get("theme", ""));
        Assert.AreEqual("PURPLE", reopened.Get("accent", ""));
    }

    [TestMethod]
    public void ExpansionAndScrollSurviveThemeChangesResizeAndReturningToSettings()
    {
        using var shell = OpenSettings();
        shell.Ui.Do(host => host.Size = host.MinimumSize);
        shell.Ui.Click("settings-shortcuts-toggle");
        shell.Ui.Click("settings-about-toggle");
        shell.Ui.Do(host =>
        {
            var settings = (SettingsView)((MainForm)host).ActiveMode!;
            var theme = UiThreadHarness.Find<RadioButton>(settings, "settings-theme-light");
            theme.Focus();
            settings.AutoScrollPosition = new Point(0, Scale(host, 150));
            int offset = settings.AutoScrollPosition.Y;
            Assert.IsTrue(offset < 0);
            UiThreadHarness.Find<RadioButton>(settings, "settings-theme-dark").PerformClick();
            Assert.IsTrue(theme.Focused, "Applying a theme must not move keyboard focus.");
            Assert.AreEqual(offset, settings.AutoScrollPosition.Y);
            host.Width = Scale(host, 400);
            host.PerformLayout();
            Assert.AreEqual(offset, settings.AutoScrollPosition.Y, "Resizing must not snap to the previously focused radio button.");
            Assert.IsTrue(UiThreadHarness.Find<SettingsSection>(settings, "settings-shortcuts").Expanded);
            Assert.IsTrue(UiThreadHarness.Find<SettingsSection>(settings, "settings-about").Expanded);
        });
        shell.Select(ModeId.STANDARD);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Click("navigation-settings");
        shell.Ui.Do(host =>
        {
            var shortcuts = UiThreadHarness.Find<SettingsSection>(host, "settings-shortcuts");
            var about = UiThreadHarness.Find<SettingsSection>(host, "settings-about");
            Assert.IsTrue(shortcuts.Expanded && about.Expanded, "Both independent expansion states must survive returning to Settings.");
            UiThreadHarness.Find<Button>(host, "settings-shortcuts-toggle").Focus();
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "settings-shortcuts-toggle").AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        });
        shell.Ui.Key(VirtualKeyShort.SPACE);
        shell.Ui.Do(host =>
        {
            var shortcuts = UiThreadHarness.Find<SettingsSection>(host, "settings-shortcuts");
            Assert.IsFalse(shortcuts.Expanded || shortcuts.Body.Visible);
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "settings-shortcuts-toggle").AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
            Assert.IsTrue(UiThreadHarness.Find<SettingsSection>(host, "settings-about").Expanded);
        });
        shell.Ui.Key(VirtualKeyShort.TAB);
        Assert.AreEqual("settings-about-toggle", shell.Ui.FocusedName, "Collapsed help contents must not create hidden tab stops.");
    }

    [TestMethod]
    public void CollapsingASectionReturnsFocusFromItsBodyToItsHeader()
    {
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form { ClientSize = new Size(350, 250) };
            var section = new SettingsSection("Details", "details") { Expanded = true };
            var field = new TextBox { Name = "details-input" };
            section.Body.Controls.Add(field);
            section.MeasureBody = width => { field.SetBounds(0, 0, width, field.PreferredHeight); return field.Height; };
            section.SetBounds(10, 10, 300, section.MeasureHeight(300));
            host.Controls.Add(section);
            ThemePalette.Create("light", "BLUE").Apply(host);
            return host;
        });
        ui.Do(host =>
        {
            var section = UiThreadHarness.Find<SettingsSection>(host, "details");
            UiThreadHarness.Find<TextBox>(host, "details-input").Focus();
            Assert.IsTrue(section.Body.ContainsFocus);
            section.Expanded = false;
            Assert.IsTrue(UiThreadHarness.Find<Button>(host, "details-toggle").Focused);
            Assert.IsFalse(section.Body.Visible);
        });
    }

    private static ShellFixture OpenSettings()
    {
        var shell = new ShellFixture(ModeId.STANDARD);
        shell.Ui.Click("navigation-toggle");
        shell.Ui.Click("navigation-settings");
        shell.Ui.WaitForNavigation();
        return shell;
    }

    private static int Scale(Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96d);

    private static void AssertSettingsBounds(SettingsView settings)
    {
        Assert.IsFalse(settings.HorizontalScroll.Visible, "Settings must not need horizontal scrolling.");
        var content = UiThreadHarness.Find<Panel>(settings, "settings-content");
        var cards = content.Controls.OfType<SettingsSection>().ToArray();
        foreach (SettingsSection card in cards)
        {
            Assert.IsTrue(content.ClientRectangle.Contains(card.Bounds));
            foreach (Control child in card.Body.Controls)
                Assert.IsTrue(card.Body.ClientRectangle.Contains(child.Bounds), $"{child.Name} escapes {card.Name}.");
        }
        for (int i = 1; i < cards.Length; i++) Assert.IsTrue(cards[i - 1].Bottom < cards[i].Top);
        foreach (Control panel in new[] { UiThreadHarness.Find<Panel>(settings, "settings-themes"), UiThreadHarness.Find<Panel>(settings, "settings-accents") })
            foreach (Control choice in panel.Controls) Assert.IsTrue(panel.ClientRectangle.Contains(choice.Bounds));
    }

    private static void Capture(Form host, string name)
    {
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "screenshots"));
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.Size));
        bitmap.Save(Path.Combine(directory, $"{name}-{host.DeviceDpi}dpi.png"), ImageFormat.Png);
    }
}
