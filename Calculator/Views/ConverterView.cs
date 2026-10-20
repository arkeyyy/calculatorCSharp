using System.Globalization;
using Calculator.Controls;
using Calculator.Core;
using Calculator.Infrastructure;

namespace Calculator.Views;

public sealed class ConverterView : ModeView
{
    private readonly ModeId mode;
    private readonly SettingsStore settings;
    private readonly ConversionCategory? category;
    private readonly CurrencyRates? rates;
    private readonly TextBox[] amounts = [CreateAmount("conversion-input"), CreateAmount("conversion-result")];
    private readonly CurrencySelector[] selectors = [new() { Name = "conversion-from", AccessibleName = "Source unit" }, new() { Name = "conversion-to", AccessibleName = "Destination unit" }];
    private readonly KeypadControl keypad;
    private readonly Button swap, copy, refresh;
    private readonly Label status = Ui.Label("", 12, "conversion-status");
    private readonly ToolTip tips = new();
    private string rateStatus = "";
    private bool rendering, visited, refreshing, layoutReady;
    public ConverterSession EntryState { get; }
    public CurrencySelector FromSelector => selectors[0];
    public CurrencySelector ToSelector => selectors[1];
    public bool IsWideLayout { get; private set; }

    public ConverterView(ModeId mode, SettingsStore settings, SessionStore session, ICurrencyRateProvider? rateProvider = null)
    {
        this.mode = mode;
        this.settings = settings;
        Name = mode == ModeId.CURRENCY ? "currency-view" : "conversion-view";
        if (mode == ModeId.CURRENCY)
        {
            CurrencyRateSnapshot? cached = null;
            try { cached = CurrencyRateSnapshot.Decode(settings.Get("currency.snapshot", "")); }
            catch (Exception error) when (error is ArgumentException or FormatException or ArithmeticException) { }
            rates = new CurrencyRates(rateProvider ?? new EcbRateProvider(), cached);
            selectors[0].Name = "currency-from";
            selectors[1].Name = "currency-to";
            amounts[0].Name = "currency-input";
            amounts[1].Name = "currency-result";
            PopulateCurrencies(settings.Get("currency.from", "EUR"), settings.Get("currency.to", "USD"));
            rateStatus = cached is null ? "ECB rates unavailable. Refresh to begin." : DescribeRates("Cached rates");
        }
        else
        {
            category = UnitCatalog.Get(mode);
            var units = category.Units.Select(unit => (unit.Id, unit.Label)).ToArray();
            selectors[0].SetOptions(units, category.Units[0].Id);
            selectors[1].SetOptions(units, category.Units[1].Id);
        }
        EntryState = new ConverterSession((value, reverse) =>
        {
            string from = selectors[reverse ? 1 : 0].CommittedCode ?? "";
            string to = selectors[reverse ? 0 : 1].CommittedCode ?? "";
            return category is not null ? category.Convert(value, from, to) : rates?.Snapshot?.Convert(value, from, to);
        });
        keypad = new KeypadControl([["", "CE", "⌫"], ["7", "8", "9"], ["4", "5", "6"], ["1", "2", "3"], ["±", "0", "."]], PressKey)
        { Expanding = true, Name = "converter-keypad" };
        swap = Ui.Button("⇅ Swap", Swap, "converter-swap");
        copy = Ui.Button("Copy", CopyResult, "converter-copy");
        refresh = Ui.Button("Refresh", () => _ = RefreshRatesAsync(), "currency-refresh");
        refresh.Visible = rates is not null;
        status.Tag = "muted";
        status.AutoEllipsis = false;
        status.TextAlign = ContentAlignment.TopLeft;
        for (int side = 0; side < 2; side++) WireAmount(side);
        foreach (var selector in selectors)
            selector.SelectionCommitted += (_, _) =>
            {
                if (mode == ModeId.CURRENCY) SaveCurrencyPair();
                EntryState.Update();
                Render();
            };
        Controls.AddRange([.. amounts, .. selectors, keypad, swap, copy, refresh, status]);
        tips.SetToolTip(swap, "Exchange the units and amounts");
        tips.SetToolTip(copy, "Copy the converted amount");
        tips.SetToolTip(refresh, "Download the latest European Central Bank reference rates");
        EntryState.Update();
        layoutReady = true;
        ApplyTheme(Palette);
        Render();
    }

    private static TextBox CreateAmount(string name) => new AmountTextBox
    {
        Name = name, AccessibleName = name.EndsWith("input", StringComparison.Ordinal) ? "First amount, click to edit" : "Second amount, click to edit",
        BorderStyle = BorderStyle.None, Font = Ui.Font(32), TextAlign = HorizontalAlignment.Left,
        HideSelection = false, ShortcutsEnabled = true, AutoSize = false, MaxLength = NumericEntry.Limit
    };

    private void WireAmount(int side)
    {
        var field = amounts[side];
        Point pressedAt = Point.Empty;
        bool dragged = false;
        int clicks = 0;
        bool hasBeenFocused = false, selectAfterClick = false;
        field.Enter += (_, _) =>
        {
            selectAfterClick = !hasBeenFocused || EntryState.ActiveSide != side;
            hasBeenFocused = true;
            EntryState.Activate(side, selectAfterClick);
            if (selectAfterClick) field.SelectAll();
            Invalidate();
        };
        field.MouseDown += (_, e) => { pressedAt = e.Location; dragged = false; clicks = e.Clicks; };
        field.MouseMove += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && (Math.Abs(e.X - pressedAt.X) > ScaleLogical(3) || Math.Abs(e.Y - pressedAt.Y) > ScaleLogical(3))) dragged = true;
        };
        field.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && !dragged && clicks == 1 && selectAfterClick) { EntryState.Activate(side); field.SelectAll(); }
            else EntryState.Activate(side, selectAll: false);
            selectAfterClick = false;
            RememberSelection(side);
            Invalidate();
        };
        field.KeyUp += (_, _) => RememberSelection(side);
        field.KeyDown += (_, _) => selectAfterClick = false;
        field.Leave += (_, _) => RememberSelection(side);
        field.TextChanged += (_, _) =>
        {
            if (rendering) return;
            selectAfterClick = false;
            int start = field.SelectionStart, length = field.SelectionLength;
            if (EntryState.TrySetText(side, field.Text))
            {
                EntryState.Entry(side).Select(start, start + length);
                Render();
            }
            else if (field.Text != EntryState.Text(side)) Render();
        };
    }

    private void RememberSelection(int side)
    {
        if (rendering) return;
        var field = amounts[side];
        EntryState.Entry(side).Select(field.SelectionStart, field.SelectionStart + field.SelectionLength);
    }

    private void PressKey(string key)
    {
        for (int side = 0; side < 2; side++) if (amounts[side].Focused) RememberSelection(side);
        EntryState.Press(key);
        Render();
        FocusInput();
    }

    private void Swap()
    {
        string? first = selectors[0].CommittedCode;
        selectors[0].SetCommitted(selectors[1].CommittedCode);
        selectors[1].SetCommitted(first);
        EntryState.Swap();
        if (mode == ModeId.CURRENCY) SaveCurrencyPair();
        Render();
        FocusInput();
    }

    private void SaveCurrencyPair()
    {
        if (selectors[0].CommittedCode is string first) settings.Put("currency.from", first);
        if (selectors[1].CommittedCode is string second) settings.Put("currency.to", second);
    }

    private void CopyResult()
    {
        string result = CopyValue();
        if (result.Length != 0)
        {
            try { Clipboard.SetText(result); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }
    }

    public override string CopyValue() => EntryState.Result;
    public override void Paste(string text) { EntryState.Paste(text); Render(); }

    public override bool HandleKey(Keys key)
    {
        if (selectors.Any(selector => selector.ContainsFocus || selector.PopupOpen) || amounts.Any(amount => amount.Focused)) return false;
        string? token = KeyCommands.FromKey(key);
        if (token is null || !(token.Length == 1 && (char.IsAsciiDigit(token[0]) || token is "." or "±" or "−") || token is "CE" or "C" or "⌫")) return false;
        PressKey(token);
        return true;
    }

    public override void ActivateMode()
    {
        if (rates is not null && !visited) { visited = true; _ = RefreshRatesAsync(); }
    }

    public async Task RefreshRatesAsync()
    {
        if (rates is null || refreshing || IsDisposed) return;
        refreshing = true;
        refresh.Enabled = false;
        bool hadCache = rates.Snapshot is not null;
        rateStatus = hadCache ? DescribeRates("Refreshing; using cached rates") : "Loading ECB rates…";
        RenderStatus();
        try
        {
            var snapshot = await rates.RefreshAsync();
            if (IsDisposed || Disposing) return;
            settings.Put("currency.snapshot", snapshot.Encode());
            settings.Flush();
            PopulateCurrencies(hadCache ? selectors[0].CommittedCode : settings.Get("currency.from", "EUR"),
                hadCache ? selectors[1].CommittedCode : settings.Get("currency.to", "USD"));
            rateStatus = DescribeRates("Rates updated");
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or ArgumentException or IOException or ObjectDisposedException)
        {
            if (IsDisposed || Disposing) return;
            rateStatus = rates.Snapshot is null ? "ECB rates unavailable. Check your connection and retry." : DescribeRates("Refresh failed; using cached rates");
        }
        finally
        {
            refreshing = false;
            if (!IsDisposed && !Disposing)
            {
                refresh.Text = rates.Snapshot is null ? "Retry" : "Refresh";
                refresh.Enabled = true;
                EntryState.Update();
                Render();
            }
        }
    }

    private string DescribeRates(string state) => $"{state} · ECB · {rates!.Snapshot!.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\nReference rates; working-day updates.";

    private void PopulateCurrencies(string? first, string? second)
    {
        string[] codes = rates?.Snapshot?.Rates.Keys.ToArray() ?? ["EUR", "USD"];
        selectors[0].SetCodes(codes, codes.Contains(first) ? first : "EUR");
        selectors[1].SetCodes(codes, codes.Contains(second) ? second : codes.Contains("USD") ? "USD" : "EUR");
    }

    private void Render()
    {
        rendering = true;
        try
        {
            for (int side = 0; side < 2; side++)
            {
                var entry = EntryState.Entry(side);
                if (amounts[side].Text != entry.Text) amounts[side].Text = entry.Text;
                amounts[side].Select(Math.Min(entry.Anchor, entry.Caret), Math.Abs(entry.Anchor - entry.Caret));
            }
            copy.Enabled = EntryState.Result.Length != 0;
            RenderStatus();
        }
        finally { rendering = false; }
        Invalidate();
    }

    private void RenderStatus()
    {
        string note = mode == ModeId.CURRENCY ? rateStatus : mode == ModeId.STORAGE ? "Decimal: powers of 1000 · Binary: powers of 1024" : "";
        status.Text = EntryState.Error.Length == 0 ? note : EntryState.Error + (note.Length == 0 ? "" : "\n" + note);
        status.ForeColor = EntryState.Error.Length == 0 ? Palette.Muted : Palette.Error;
        tips.SetToolTip(status, status.Text);
        if (layoutReady) PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (!layoutReady) return;
        int pad = ScaleLogical(8), gap = ScaleLogical(10);
        int width = Math.Max(1, ClientSize.Width - pad * 2), height = Math.Max(1, ClientSize.Height - pad * 2);
        IsWideLayout = ClientSize.Width * 96d / DeviceDpi >= 640;
        keypad.Expanding = IsWideLayout;
        int statusHeight = status.Text.Length == 0 ? 0 : ScaleLogical(mode == ModeId.CURRENCY ? EntryState.Error.Length == 0 ? 34 : 42 : 30);
        int actionHeight = ScaleLogical(34), actions = actionHeight + statusHeight + (statusHeight == 0 ? 0 : ScaleLogical(5));
        int displayWidth, regionHeight, actionsTop;
        if (IsWideLayout)
        {
            displayWidth = (width - gap) / 2;
            regionHeight = Math.Max(1, (height - actions) / 2);
            actionsTop = pad + height - actions;
            keypad.SetBounds(pad + displayWidth + gap, pad, width - displayWidth - gap, height);
        }
        else
        {
            displayWidth = width;
            int keypadHeight = Math.Max(ScaleLogical(194), height - Math.Min(ScaleLogical(172), height * 2 / 5) - actions - gap);
            keypadHeight = Math.Min(Math.Min(height, ScaleLogical(344)), keypadHeight);
            int displayHeight = Math.Max(0, height - keypadHeight - gap - actions);
            regionHeight = displayHeight / 2;
            actionsTop = pad + displayHeight;
            keypad.SetBounds(pad, pad + height - keypadHeight, width, keypadHeight);
        }
        float regionLogical = regionHeight * 96f / DeviceDpi;
        float selectorFont = IsWideLayout ? Math.Clamp(displayWidth * 96f / DeviceDpi / 24, 12, 18) : 12;
        int selectorHeight = ScaleLogical(IsWideLayout ? (int)selectorFont + 16 : 22);
        int amountGap = ScaleLogical(IsWideLayout ? 3 : 2);
        // A font's line box is taller than its nominal pixel size. Reserve the actual
        // selector/line spacing before sizing amounts, including at the minimum window.
        FontFamily family = amounts[0].Font.FontFamily;
        float lineRatio = family.GetLineSpacing(FontStyle.Regular) / (float)family.GetEmHeight(FontStyle.Regular);
        float fittingFont = Math.Max(1, regionHeight - selectorHeight - amountGap - 1) * 96f / DeviceDpi / lineRatio;
        float amountFont = IsWideLayout ? Math.Clamp(Math.Min(displayWidth * 96f / DeviceDpi / 7, regionLogical * .32f), 27, 56) : Math.Clamp(fittingFont, 14, 40);
        for (int side = 0; side < 2; side++)
        {
            Ui.SetFont(amounts[side], amountFont);
            Ui.SetFont(selectors[side], selectorFont);
            int amountHeight = amounts[side].PreferredHeight;
            int contentHeight = amountHeight + selectorHeight + amountGap;
            int top = pad + side * regionHeight + Math.Max(0, (regionHeight - contentHeight) / 2);
            amounts[side].SetBounds(pad + ScaleLogical(5), top, Math.Max(1, displayWidth - ScaleLogical(10)), amountHeight);
            selectors[side].SetBounds(pad, top + amountHeight + amountGap, displayWidth, selectorHeight);
            ((AmountTextBox)amounts[side]).ShowWholeValueWhenItFits();
        }
        int swapWidth = ScaleLogical(76), copyWidth = ScaleLogical(60), refreshWidth = ScaleLogical(79), actionGap = ScaleLogical(5);
        swap.SetBounds(pad, actionsTop, swapWidth, actionHeight);
        copy.SetBounds(pad + swapWidth + actionGap, actionsTop, copyWidth, actionHeight);
        refresh.SetBounds(pad + swapWidth + copyWidth + actionGap * 2, actionsTop, refreshWidth, actionHeight);
        status.SetBounds(pad, actionsTop + actionHeight + ScaleLogical(5), displayWidth, statusHeight);
        float keyFont = IsWideLayout ? Math.Clamp(keypad.Height * 96f / DeviceDpi / 16, 22, 56) : 22;
        float actionFont = IsWideLayout ? Math.Clamp(keypad.Height * 96f / DeviceDpi / 24, 14, 28) : 14;
        foreach (var key in keypad.Buttons) Ui.SetFont(key, key.Text is "CE" or "⌫" or "±" ? actionFont : keyFont);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!layoutReady) return;
        var amount = amounts[EntryState.ActiveSide];
        using var pen = new Pen(Palette.Accent, Math.Max(1, ScaleLogical(2)));
        e.Graphics.DrawLine(pen, amount.Left, amount.Bottom + ScaleLogical(1), amount.Right, amount.Bottom + ScaleLogical(1));
    }

    public override void ApplyTheme(ThemePalette palette)
    {
        base.ApplyTheme(palette);
        foreach (var selector in selectors) selector.ApplyTheme(palette);
        foreach (var amount in amounts) amount.BackColor = palette.Background;
        if (layoutReady) { RenderStatus(); PerformLayout(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { rates?.Dispose(); tips.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class AmountTextBox : TextBox
    {
        public void ShowWholeValueWhenItFits()
        {
            if (!IsHandleCreated || TextRenderer.MeasureText(Text, Font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width >= ClientSize.Width) return;
            // Native single-line edits can retain a horizontal offset after an amount
            // was selected while the control was narrow. Keep the saved selection.
            int start = SelectionStart, length = SelectionLength;
            Select(0, 0);
            ScrollToCaret();
            Select(start, length);
        }

        protected override void WndProc(ref Message message)
        {
            const int PasteMessage = 0x0302;
            if (message.Msg == PasteMessage)
            {
                try
                {
                    if (Clipboard.ContainsText() && TextLength - SelectionLength + Clipboard.GetText().Length > NumericEntry.Limit) return;
                }
                catch (System.Runtime.InteropServices.ExternalException) { return; }
            }
            base.WndProc(ref message);
        }
    }
}
