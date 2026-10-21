using Calculator.Controls;
using Calculator.Infrastructure;

namespace Calculator.Views;

public sealed class SettingsView : ModeView
{
    private readonly Panel content = new() { Name = "settings-content", TabStop = false };
    private readonly SettingsSection appearance = new("Appearance", "settings-appearance", expandable: false);
    private readonly SettingsSection shortcuts = new("Keyboard shortcuts", "settings-shortcuts");
    private readonly SettingsSection about = new("About Calculator", "settings-about");
    private readonly Label themeLabel = Ui.Label("Theme", 14, "settings-theme-label");
    private readonly Panel themes = new() { Name = "settings-themes", TabStop = false };
    private readonly Label accentLabel = Ui.Label("Accent color", 14, "settings-accent-label");
    private readonly Panel accents = new() { Name = "settings-accents", TabStop = false };
    private readonly List<(Label Key, Label Action)> shortcutRows = [];
    private readonly Label shortcutNote = Ui.Label("Text fields keep their native editing shortcuts.", 13, "settings-shortcut-note");
    private readonly Label aboutName = Ui.Label("Calculator", 16, "settings-about-name");
    private readonly Label aboutDetails = Ui.Label("18 modes · 34-digit decimal precision", 13, "settings-about-details");
    private readonly Label aboutStorage = Ui.Label("Preferences and cached rates stay on this device.", 13, "settings-about-storage");
    private bool arranging;

    public SettingsView(SettingsStore settings, Action changed)
    {
        Name = "settings-view";
        AutoScroll = true;
        themeLabel.Font = Ui.Font(14, FontStyle.Bold);
        accentLabel.Font = Ui.Font(14, FontStyle.Bold);
        aboutName.Font = Ui.Font(16, FontStyle.Bold);
        string selectedTheme = settings.Get("theme", "light"), selectedAccent = settings.Get("accent", "BLUE");
        foreach (string theme in new[] { "light", "dark" })
        {
            var choice = new AppearanceChoice(theme == "light" ? "Light" : "Dark")
            {
                Name = "settings-theme-" + theme,
                Checked = theme == selectedTheme,
                TabIndex = themes.Controls.Count
            };
            choice.CheckedChanged += (_, _) =>
            {
                if (!choice.Checked) return;
                settings.Put("theme", theme);
                changed();
            };
            themes.Controls.Add(choice);
        }
        foreach (string accent in ThemePalette.AccentNames)
        {
            var choice = new AppearanceChoice(accent[0] + accent[1..].ToLowerInvariant(), accent)
            {
                Name = "accent-" + accent,
                Checked = accent == selectedAccent,
                TabIndex = accents.Controls.Count
            };
            choice.CheckedChanged += (_, _) =>
            {
                if (!choice.Checked) return;
                settings.Put("accent", accent);
                changed();
            };
            accents.Controls.Add(choice);
        }
        themeLabel.TabIndex = 0; themes.TabIndex = 1; accentLabel.TabIndex = 2; accents.TabIndex = 3;
        appearance.Body.Controls.AddRange([themeLabel, themes, accentLabel, accents]);
        appearance.MeasureBody = LayoutAppearance;
        (string Key, string Action)[] shortcutsText =
        [
            ("Enter", "Calculate / plot"), ("Backspace", "Delete a digit"),
            ("Esc", "Clear calculation"), ("Delete", "Clear entry"),
            ("Ctrl+C / Ctrl+V", "Copy / paste"), ("Tab / Shift+Tab", "Move between controls")
        ];
        foreach (var row in shortcutsText)
        {
            var key = Ui.Label(row.Key, 13); key.Font = Ui.Font(13, FontStyle.Bold);
            var action = Ui.Label(row.Action, 13); action.Tag = "muted";
            key.AutoEllipsis = action.AutoEllipsis = false;
            key.TextAlign = action.TextAlign = ContentAlignment.TopLeft;
            shortcutRows.Add((key, action));
            shortcuts.Body.Controls.AddRange([key, action]);
        }
        shortcutNote.Tag = aboutDetails.Tag = aboutStorage.Tag = "muted";
        foreach (var label in new[] { shortcutNote, aboutName, aboutDetails, aboutStorage })
        {
            label.AutoEllipsis = false;
            label.TextAlign = ContentAlignment.TopLeft;
        }
        shortcuts.Body.Controls.Add(shortcutNote);
        shortcuts.MeasureBody = LayoutShortcuts;
        about.Body.Controls.AddRange([aboutName, aboutDetails, aboutStorage]);
        about.MeasureBody = LayoutAbout;
        appearance.TabIndex = 0; shortcuts.TabIndex = 1; about.TabIndex = 2;
        shortcuts.ExpandedChanged += (_, _) => PerformLayout();
        about.ExpandedChanged += (_, _) => PerformLayout();
        content.Controls.AddRange([appearance, shortcuts, about]);
        Controls.Add(content);
    }

    private int LayoutAppearance(int width)
    {
        int gap = ScaleLogical(UiStyle.ControlGap), labelHeight = Measure(themeLabel, width), y = 0;
        themeLabel.SetBounds(0, y, width, labelHeight); y += labelHeight + gap;
        int choiceHeight = Math.Max(ScaleLogical(40), themes.Controls.Cast<Control>().Max(choice => choice.Font.Height) + ScaleLogical(16));
        themes.SetBounds(0, y, width, choiceHeight);
        LayoutChoices(themes, 2, gap, choiceHeight); y += choiceHeight + ScaleLogical(20);
        labelHeight = Measure(accentLabel, width);
        accentLabel.SetBounds(0, y, width, labelHeight); y += labelHeight + gap;
        int columns = width >= ScaleLogical(300) ? 3 : 2;
        int rows = (accents.Controls.Count + columns - 1) / columns;
        choiceHeight = Math.Max(ScaleLogical(38), accents.Controls.Cast<Control>().Max(choice => choice.Font.Height) + ScaleLogical(16));
        accents.SetBounds(0, y, width, rows * choiceHeight + (rows - 1) * gap);
        LayoutChoices(accents, columns, gap, choiceHeight);
        return accents.Bottom;
    }

    private static void LayoutChoices(Panel panel, int columns, int gap, int height)
    {
        int available = Math.Max(0, panel.ClientSize.Width - (columns - 1) * gap);
        for (int i = 0; i < panel.Controls.Count; i++)
        {
            int column = i % columns, row = i / columns;
            int left = available * column / columns + column * gap;
            int right = available * (column + 1) / columns + column * gap;
            panel.Controls[i].SetBounds(left, row * (height + gap), right - left, height);
        }
    }

    private int LayoutShortcuts(int width)
    {
        int gap = ScaleLogical(12), keyWidth = Math.Min(ScaleLogical(112), Math.Max(0, (width - gap) / 2)), y = 0;
        int actionWidth = Math.Max(1, width - keyWidth - gap);
        foreach (var row in shortcutRows)
        {
            int height = Math.Max(Measure(row.Key, keyWidth), Measure(row.Action, actionWidth));
            row.Key.SetBounds(0, y, keyWidth, height);
            row.Action.SetBounds(keyWidth + gap, y, actionWidth, height);
            y += height + ScaleLogical(12);
        }
        y += ScaleLogical(4);
        shortcutNote.SetBounds(0, y, width, Measure(shortcutNote, width));
        return shortcutNote.Bottom;
    }

    private int LayoutAbout(int width)
    {
        int y = 0;
        foreach (var label in new[] { aboutName, aboutDetails, aboutStorage })
        {
            label.SetBounds(0, y, width, Measure(label, width));
            y = label.Bottom + ScaleLogical(8);
        }
        return aboutStorage.Bottom;
    }

    private static int Measure(Label label, int width) => TextRenderer.MeasureText(label.Text, label.Font,
        new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging || content is null) return;
        arranging = true;
        try
        {
            base.OnLayout(e);
            // Scrollbars can change ClientSize after content grows or shrinks.
            // Re-measure the viewport without assigning the content's scrolled location.
            for (int pass = 0; pass < 3; pass++)
            {
                int width = Math.Max(0, ClientSize.Width - ScaleLogical(2)), y = 0;
                foreach (var section in new[] { appearance, shortcuts, about })
                {
                    int height = section.MeasureHeight(width);
                    section.SetBounds(0, y, width, height);
                    y += height + ScaleLogical(UiStyle.SectionGap);
                }
                int contentHeight = about.Bottom + ScaleLogical(8);
                content.Size = new Size(width, contentHeight);
                AutoScrollMinSize = new Size(0, contentHeight);
                base.OnLayout(e);
                if (width == Math.Max(0, ClientSize.Width - ScaleLogical(2))) break;
            }
        }
        finally { arranging = false; }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); PerformLayout(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (content is not null) PerformLayout(); }
}
