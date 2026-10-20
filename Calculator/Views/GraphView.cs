using System.Drawing.Drawing2D;
using System.Globalization;
using Calculator.Controls;
using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Views;

/// <summary>Retained graph workspace with native expression editing and independently scrolling controls.</summary>
public sealed class GraphView : ModeView
{
    private readonly ScientificEngine engine = new();
    private readonly GraphWorker worker;
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 90 };
    private readonly List<ExpressionRow> rows = [];
    private readonly Panel toolbar = new(), workspace = new(), left = new(), right = new();
    private readonly Panel expressionScroll = new() { AutoScroll = true, Name = "graph-expressions-scroll" };
    private readonly Panel expressionContent = new();
    private readonly Panel advancedScroll = new() { AutoScroll = true, Name = "graph-advanced-scroll" };
    private readonly Panel advancedContent = new();
    private readonly Label editing = Ui.Label("Add a function", 13, "graph-editing");
    private readonly Label status = Ui.Label("Enter a function and choose Plot.", 12, "graph-status");
    private readonly Label rangeError = Ui.Label("", 12, "graph-range-error");
    private readonly ComboBox angle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Name = "graph-angle", AccessibleName = "Graph angle unit" };
    private readonly ComboBox traceCurve = new() { DropDownStyle = ComboBoxStyle.DropDownList, Name = "graph-trace-function", AccessibleName = "Trace function number" };
    private readonly TextBox[] ranges = new TextBox[4];
    private readonly Label[] rangeLabels = new Label[4];
    private readonly TextBox traceX = Ui.TextField("graph-trace-x", false, 15);
    private readonly TextBox traceY = Ui.TextField("graph-trace-y", true, 15);
    private readonly Label traceCurveLabel = Ui.Label("Trace function", 12), traceXLabel = Ui.Label("Trace X", 12), traceYLabel = Ui.Label("Y", 12);
    private readonly Label hint = Ui.Label("Use explicit multiplication (2*x). Plot or Enter applies expression edits.", 12);
    private readonly Button add, zoomIn, zoomOut, reset, advancedToggle, applyRanges, copyTrace, functions;
    private readonly ContextMenuStrip functionMenu = new() { Name = "graph-functions-menu", ShowImageMargin = false };
    private readonly KeypadControl keypad;
    private readonly PlotSurface plot;
    private Viewport viewport = Viewport.Default;
    private IReadOnlyList<GraphPlot> plots = Array.Empty<GraphPlot>();
    private ExpressionRow? activeRow;
    private bool advancedExpanded, ready, layingOut, activated;
    private Point dragStart;
    private Viewport? dragViewport;
    private double? tracedX, tracedY;
    private static readonly Color[] CurveColors = [Color.FromArgb(55, 112, 236), Color.FromArgb(209, 62, 123), Color.FromArgb(0, 143, 121), Color.FromArgb(171, 101, 223), Color.FromArgb(202, 119, 20), Color.FromArgb(213, 80, 67)];
    private static readonly DashStyle[] CurveDashes = [DashStyle.Solid, DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot, DashStyle.DashDotDot, DashStyle.Dash];

    public GraphView(SessionStore session)
    {
        Name = "graph-view";
        AccessibleName = "Graphing calculator";
        worker = new GraphWorker(action =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                try { BeginInvoke(action); }
                catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated) { }
            }
        });
        plot = new PlotSurface { Name = "graph-canvas", AccessibleName = "Function plot. Arrow keys pan, plus and minus zoom, Home resets. Use trace controls for coordinates." };
        plot.Paint += (_, e) => Render(e.Graphics);
        plot.MouseDown += PlotMouseDown;
        plot.MouseMove += PlotMouseMove;
        plot.MouseUp += (_, _) => { dragViewport = null; plot.Capture = false; };
        plot.MouseCaptureChanged += (_, _) => { if (!plot.Capture) dragViewport = null; };
        plot.MouseWheel += (_, e) =>
        {
            if (!plot.Focused) return;
            var bounds = PlotBounds;
            TryViewport(() => viewport.Zoom(e.Delta > 0 ? .8 : 1.25,
                viewport.XMin + Math.Clamp((e.X - bounds.Left) / bounds.Width, 0, 1) * viewport.Width,
                viewport.YMax - Math.Clamp((e.Y - bounds.Top) / bounds.Height, 0, 1) * viewport.Height));
        };
        plot.KeyDown += (_, e) => { if (HandlePlotKey(e.KeyData)) { e.Handled = true; e.SuppressKeyPress = true; } };
        plot.SizeChanged += (_, _) => RequestSample();
        angle.Items.AddRange([AngleUnit.RAD, AngleUnit.DEG]);
        angle.SelectedIndex = 0;
        angle.SelectedIndexChanged += (_, _) => ApplyExpressions();
        add = Ui.Button("Add function", AddRow, "graph-add");
        zoomIn = Ui.Button("+", () => Zoom(.8), "graph-zoom-in"); zoomIn.AccessibleName = "Zoom in";
        zoomOut = Ui.Button("−", () => Zoom(1.25), "graph-zoom-out"); zoomOut.AccessibleName = "Zoom out";
        reset = Ui.Button("Reset", () => ChangeViewport(Viewport.Default), "graph-reset");
        advancedToggle = Ui.Button("Ranges and trace ▸", () => { advancedExpanded = !advancedExpanded; advancedToggle!.Text = advancedExpanded ? "Ranges and trace ▾" : "Ranges and trace ▸"; PerformLayout(); }, "graph-advanced");
        applyRanges = Ui.Button("Apply ranges", ApplyRanges, "graph-apply-ranges");
        copyTrace = Ui.Button("Copy trace", () => { string text = CopyValue(); if (text.Length > 0) Clipboard.SetText(text); }, "graph-copy-trace");
        functions = Ui.Button("ƒ(x)", () => functionMenu.Show(functions!, new Point(0, functions!.Height)), "graph-functions");
        functions.AccessibleName = "More mathematical functions";
        foreach (string token in new[] { "asin", "acos", "atan", "ln", "log", "abs", "1/x", "10ˣ", "eˣ", "x³", "n!", "%", "±" })
        {
            var item = new ToolStripMenuItem(token) { Name = "graph-function-" + token, AccessibleName = token };
            item.Click += (_, _) => { functionMenu.Close(); Input(token); };
            functionMenu.Items.Add(item);
        }
        Ui.WatchPopup(functionMenu);
        keypad = new KeypadControl([
            ["π", "e", "(", ")", "⌫"],
            ["x", "x²", "^", "", "C"],
            ["√", "7", "8", "9", "÷"],
            ["sin", "4", "5", "6", "×"],
            ["cos", "1", "2", "3", "−"],
            ["tan", ".", "0", "Plot", "+"]], Input) { Name = "graph-keypad" };
        keypad.SetCell(1, 3, functions);
        keypad.Buttons.Single(button => button.Text == "Plot").Name = "graph-plot";
        keypad.Buttons.Single(button => button.Text == "Plot").AccessibleName = "Plot functions";

        string[] names = ["X minimum", "X maximum", "Y minimum", "Y maximum"];
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = Ui.TextField("graph-range-" + i, false, 15);
            ranges[i].AccessibleName = names[i]; ranges[i].MaxLength = 128;
            ranges[i].KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyRanges(); e.SuppressKeyPress = true; } };
            rangeLabels[i] = Ui.Label(names[i], 12);
            advancedContent.Controls.AddRange([rangeLabels[i], ranges[i]]);
        }
        traceX.Text = "0"; traceX.MaxLength = 128;
        traceX.TextChanged += (_, _) => Trace();
        traceCurve.SelectedIndexChanged += (_, _) => Trace();
        rangeError.Tag = "error"; status.Tag = hint.Tag = editing.Tag = "muted";
        status.AutoEllipsis = false; hint.AutoEllipsis = false; rangeError.AutoEllipsis = false;
        advancedContent.Controls.AddRange([applyRanges, rangeError, traceCurveLabel, traceCurve, traceXLabel, traceX, traceYLabel, traceY, copyTrace, hint]);
        toolbar.Controls.AddRange([angle, zoomIn, zoomOut, reset]);
        expressionScroll.Controls.Add(expressionContent);
        expressionScroll.SizeChanged += (_, _) => LayoutExpressions();
        advancedScroll.Controls.Add(advancedContent);
        advancedScroll.SizeChanged += (_, _) => LayoutAdvanced();
        left.Controls.AddRange([plot, advancedToggle, advancedScroll]);
        right.Controls.AddRange([editing, add, expressionScroll, keypad]);
        workspace.Controls.AddRange([left, right]);
        Controls.AddRange([toolbar, workspace, status]);
        debounce.Tick += (_, _) => { debounce.Stop(); Sample(); };
        VisibleChanged += (_, _) =>
        {
            if (Visible && activated) RequestSample();
            else { debounce.Stop(); worker.Cancel(); }
        };
        SyncRanges();
        ready = true;
        AddRow();
        ApplyTheme(Palette);
    }

    private void AddRow()
    {
        if (rows.Count >= 6) return;
        int id = Enumerable.Range(1, 6).First(candidate => rows.All(row => row.Id != candidate));
        var row = new ExpressionRow(this, id);
        rows.Add(row);
        expressionContent.Controls.Add(row.Panel);
        add.Enabled = rows.Count < 6;
        LayoutExpressions();
        SelectRow(row);
        RefreshTraceChoices();
        ApplyTheme(Palette);
        row.Input.Focus();
    }

    private void RemoveRow(ExpressionRow row)
    {
        int index = rows.IndexOf(row);
        rows.Remove(row);
        expressionContent.Controls.Remove(row.Panel);
        if (activeRow == row) SelectRow(rows.Count == 0 ? null : rows[Math.Min(index, rows.Count - 1)]);
        row.Panel.Dispose();
        add.Enabled = true;
        LayoutExpressions();
        RefreshTraceChoices();
        RequestSample(); Trace();
        FocusInput();
    }

    private void SelectRow(ExpressionRow? row)
    {
        activeRow = row;
        editing.Text = row is null ? "Add a function" : $"Editing f{row.Id}(x)";
        keypad.Enabled = row is not null;
        foreach (var candidate in rows) candidate.Panel.Invalidate();
        if (row is not null) expressionScroll.ScrollControlIntoView(row.Panel);
    }

    private void Input(string key)
    {
        if (key == "Plot") ApplyExpressions();
        else if (activeRow is not null)
        {
            activeRow.Editor.Insert(key);
            expressionScroll.ScrollControlIntoView(activeRow.Panel);
        }
        FocusInput();
    }

    private void ApplyExpressions()
    {
        foreach (var row in rows)
        {
            row.Compiled = null;
            row.Error.Text = "";
            if (string.IsNullOrWhiteSpace(row.Input.Text)) continue;
            try { row.Compiled = engine.Compile(row.Input.Text, (AngleUnit)angle.SelectedItem!, true); }
            catch (CalculationException error) { row.Error.Text = error.Message; }
        }
        LayoutExpressions();
        RequestSample(); Trace();
    }

    private void RequestSample()
    {
        if (!ready || IsDisposed) return;
        worker.Cancel();
        plots = Array.Empty<GraphPlot>();
        plot.Invalidate();
        debounce.Stop();
        if (activated && Visible && IsHandleCreated) { status.Text = "Plotting…"; debounce.Start(); }
    }

    private async void Sample()
    {
        if (!activated || !Visible || IsDisposed) return;
        GraphCurve[] curves = rows.Where(row => row.Compiled is not null && row.Show.Checked)
            .Select(row =>
            {
                CompiledExpression compiled = row.Compiled!;
                return new GraphCurve(row.Id, (x, cancellationToken) => compiled.Evaluate(x, cancellationToken));
            }).ToArray();
        status.Text = curves.Length == 0 ? "Enter a function and choose Plot." : "Plotting…";
        try
        {
            await worker.Submit(curves, viewport, (int)PlotBounds.Width, (int)PlotBounds.Height, result =>
            {
                if (!activated || IsDisposed) return;
                plots = result;
                status.Text = result.Count == 0 ? "Enter a function and choose Plot." :
                    result.Any(curve => curve.Limited) ? "Sampling limit reached; some intervals are omitted." :
                    "Drag to pan. Focus the plot to zoom with the wheel. Gaps indicate undefined or unresolved intervals.";
                plot.Invalidate();
            });
        }
        catch (ObjectDisposedException) when (IsDisposed) { }
    }

    private void RefreshTraceChoices()
    {
        object? current = traceCurve.SelectedItem;
        traceCurve.BeginUpdate();
        traceCurve.Items.Clear();
        foreach (var row in rows) traceCurve.Items.Add(row.Id);
        if (current is int id && rows.Any(row => row.Id == id)) traceCurve.SelectedItem = id;
        else if (traceCurve.Items.Count > 0) traceCurve.SelectedIndex = 0;
        traceCurve.EndUpdate();
        Trace();
    }

    private void Trace()
    {
        tracedX = tracedY = null;
        traceY.Text = "";
        var selected = rows.FirstOrDefault(row => Equals(row.Id, traceCurve.SelectedItem));
        if (selected?.Compiled is not null && selected.Show.Checked)
        {
            try
            {
                var x = Numbers.Parse(traceX.Text);
                if (x.Abs().CompareTo(EDecimal.FromInt32(1000000)) > 0) throw new ArgumentException();
                double xd = x.ToDouble();
                var y = selected.Compiled.Evaluate(x);
                traceY.Text = Numbers.Format(y);
                double yd = y.ToDouble();
                if (double.IsFinite(yd)) { tracedX = xd; tracedY = yd; }
            }
            catch (Exception error) when (error is CalculationException or ArgumentException or ArithmeticException)
            { traceY.Text = "Undefined / invalid X"; }
        }
        plot.Invalidate();
    }

    private void ApplyRanges()
    {
        try
        {
            var values = ranges.Select(field => double.Parse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            ChangeViewport(new(values[0], values[1], values[2], values[3]));
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        { rangeError.Text = error is ArgumentException ? error.Message : "Enter valid numeric axis limits."; }
    }

    private void SyncRanges()
    {
        double[] values = [viewport.XMin, viewport.XMax, viewport.YMin, viewport.YMax];
        for (int i = 0; i < ranges.Length; i++) ranges[i].Text = values[i].ToString("G12", CultureInfo.InvariantCulture);
    }
    private void ChangeViewport(Viewport next)
    {
        viewport = next;
        rangeError.Text = "";
        SyncRanges(); RequestSample();
    }
    private void Zoom(double factor) => TryViewport(() => viewport.Zoom(factor,
        (viewport.XMin + viewport.XMax) / 2, (viewport.YMin + viewport.YMax) / 2));
    private void TryViewport(Func<Viewport> action)
    {
        try { ChangeViewport(action()); }
        catch (ArgumentException) { /* Retain the last supported viewport at the pan/zoom boundary. */ }
    }

    private void PlotMouseDown(object? sender, MouseEventArgs e)
    {
        plot.Focus();
        if (e.Button != MouseButtons.Left) return;
        dragStart = e.Location;
        dragViewport = viewport;
        plot.Capture = true;
    }
    private void PlotMouseMove(object? sender, MouseEventArgs e)
    {
        var bounds = PlotBounds;
        if (dragViewport is not null && e.Button == MouseButtons.Left)
        {
            var origin = dragViewport;
            TryViewport(() => origin.Pan((dragStart.X - e.X) / bounds.Width * origin.Width,
                (e.Y - dragStart.Y) / bounds.Height * origin.Height));
        }
        else if (bounds.Contains(e.Location))
            traceX.Text = (viewport.XMin + (e.X - bounds.Left) / bounds.Width * viewport.Width).ToString("G12", CultureInfo.InvariantCulture);
    }

    private bool HandlePlotKey(Keys data)
    {
        if (!plot.Focused || (data & (Keys.Control | Keys.Alt)) != 0) return false;
        switch (data & Keys.KeyCode)
        {
            case Keys.Left: TryViewport(() => viewport.Pan(-viewport.Width / 10, 0)); break;
            case Keys.Right: TryViewport(() => viewport.Pan(viewport.Width / 10, 0)); break;
            case Keys.Up: TryViewport(() => viewport.Pan(0, viewport.Height / 10)); break;
            case Keys.Down: TryViewport(() => viewport.Pan(0, -viewport.Height / 10)); break;
            case Keys.Add: case Keys.Oemplus: Zoom(.8); break;
            case Keys.Subtract: case Keys.OemMinus: Zoom(1.25); break;
            case Keys.Home: ChangeViewport(Viewport.Default); break;
            default: return false;
        }
        return true;
    }

    public override bool HandleKey(Keys key)
    {
        if (plot.Focused) return HandlePlotKey(key);
        if (Ui.PopupDepth > 0 || (key & (Keys.Control | Keys.Alt)) != 0) return false;
        if ((key & Keys.KeyCode) == Keys.Enter) { ApplyExpressions(); return true; }
        string? command = KeyCommands.FromKey(key);
        if (command is null)
        {
            Keys code = key & Keys.KeyCode;
            if (code is >= Keys.A and <= Keys.Z) command = ((char)('a' + (int)code - (int)Keys.A)).ToString();
        }
        if (command is null || activeRow is null) return false;
        Input(command == "=" ? "Plot" : command);
        return true;
    }
    public override void Paste(string text)
    {
        if (activeRow is null) return;
        activeRow.Editor.Insert(text);
    }
    public override string CopyValue() => traceY.Text.Length == 0 || traceY.Text.StartsWith("Undefined", StringComparison.Ordinal)
        ? "" : $"f{traceCurve.SelectedItem}: x={traceX.Text}, y={traceY.Text}";
    public override void ActivateMode() { activated = true; RequestSample(); }
    public override void DeactivateMode()
    {
        activated = false;
        debounce.Stop();
        worker.Cancel();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (ready && activated) RequestSample();
    }
    public override void ApplyTheme(ThemePalette palette)
    {
        base.ApplyTheme(palette);
        functionMenu.Renderer = new PaletteRenderer(palette);
        foreach (var row in rows) { row.Error.ForeColor = palette.Error; row.Number.ForeColor = CurveColors[row.Id - 1]; row.Panel.Invalidate(); }
        plot.Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (!ready || layingOut) return;
        layingOut = true;
        try
        {
            int gap = ScaleLogical(8), barHeight = ScaleLogical(34), statusHeight = ScaleLogical(40);
            toolbar.SetBounds(0, 0, Width, barHeight);
            angle.SetBounds(0, ScaleLogical(4), ScaleLogical(76), barHeight);
            zoomIn.SetBounds(ScaleLogical(84), 0, ScaleLogical(38), barHeight);
            zoomOut.SetBounds(ScaleLogical(128), 0, ScaleLogical(38), barHeight);
            reset.SetBounds(ScaleLogical(174), 0, ScaleLogical(70), barHeight);
            status.SetBounds(0, Math.Max(0, Height - statusHeight), Width, statusHeight);
            workspace.SetBounds(0, barHeight + gap, Width, Math.Max(0, Height - barHeight - statusHeight - 2 * gap));
            int leftWidth = (int)((workspace.Width - gap) * .53);
            left.SetBounds(0, 0, Math.Max(0, leftWidth), workspace.Height);
            right.SetBounds(leftWidth + gap, 0, Math.Max(0, workspace.Width - leftWidth - gap), workspace.Height);
            int advancedHeight = advancedExpanded ? Math.Min(ScaleLogical(180), Math.Max(ScaleLogical(90), left.Height - ScaleLogical(200))) : 0;
            int plotHeight = Math.Max(0, left.Height - barHeight - gap - advancedHeight - (advancedExpanded ? gap : 0));
            plot.SetBounds(0, 0, left.Width, plotHeight);
            advancedToggle.SetBounds(0, plotHeight + gap, left.Width, barHeight);
            advancedScroll.Visible = advancedExpanded;
            advancedScroll.SetBounds(0, plotHeight + 2 * gap + barHeight, left.Width, advancedHeight);
            int keypadHeight = Math.Min(ScaleLogical(280), Math.Max(ScaleLogical(234), right.Height - ScaleLogical(130)));
            keypadHeight = Math.Min(keypadHeight, Math.Max(0, right.Height - barHeight - gap));
            add.SetBounds(Math.Max(0, right.Width - ScaleLogical(112)), 0, ScaleLogical(112), barHeight);
            editing.SetBounds(0, 0, Math.Max(0, add.Left - gap), barHeight);
            keypad.SetBounds(0, Math.Max(barHeight + gap, right.Height - keypadHeight), right.Width, keypadHeight);
            expressionScroll.SetBounds(0, barHeight + gap, right.Width, Math.Max(0, keypad.Top - barHeight - 2 * gap));
            LayoutExpressions(); LayoutAdvanced();
        }
        finally { layingOut = false; }
    }

    private void LayoutExpressions()
    {
        if (!ready) return;
        int gap = ScaleLogical(8), y = 0;
        int width = Math.Max(0, expressionScroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
        foreach (var row in rows)
        {
            int rowHeight = ScaleLogical(row.Error.Text.Length > 0 ? 100 : 76);
            row.Panel.SetBounds(0, y, width, rowHeight);
            row.Number.SetBounds(gap, ScaleLogical(4), ScaleLogical(54), ScaleLogical(26));
            row.Remove.SetBounds(Math.Max(0, width - ScaleLogical(36)), ScaleLogical(3), ScaleLogical(30), ScaleLogical(28));
            row.Show.SetBounds(Math.Max(0, row.Remove.Left - ScaleLogical(70)), ScaleLogical(3), ScaleLogical(66), ScaleLogical(28));
            row.Input.SetBounds(gap, ScaleLogical(36), Math.Max(0, width - 2 * gap), ScaleLogical(28));
            row.Error.SetBounds(gap, ScaleLogical(69), Math.Max(0, width - 2 * gap), ScaleLogical(27));
            row.Error.Visible = row.Error.Text.Length > 0;
            y += rowHeight + gap;
        }
        expressionContent.Size = new(width, Math.Max(0, y - gap));
        expressionScroll.AutoScrollMinSize = new(0, expressionContent.Height);
    }

    private void LayoutAdvanced()
    {
        if (!ready) return;
        int width = Math.Max(0, advancedScroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
        int gap = ScaleLogical(8), half = Math.Max(0, (width - gap) / 2);
        for (int i = 0; i < ranges.Length; i++)
        {
            int x = i % 2 * (half + gap), y = i / 2 * ScaleLogical(55);
            rangeLabels[i].SetBounds(x, y, half, ScaleLogical(22));
            ranges[i].SetBounds(x, y + ScaleLogical(24), half, ScaleLogical(25));
        }
        int top = ScaleLogical(114);
        applyRanges.SetBounds(0, top, width, ScaleLogical(32)); top += ScaleLogical(36);
        rangeError.SetBounds(0, top, width, ScaleLogical(40)); top += ScaleLogical(44);
        traceCurveLabel.SetBounds(0, top, width, ScaleLogical(22)); top += ScaleLogical(24);
        traceCurve.SetBounds(0, top, width, ScaleLogical(28)); top += ScaleLogical(34);
        traceXLabel.SetBounds(0, top, width, ScaleLogical(22)); top += ScaleLogical(24);
        traceX.SetBounds(0, top, width, ScaleLogical(25)); top += ScaleLogical(31);
        traceYLabel.SetBounds(0, top, width, ScaleLogical(22)); top += ScaleLogical(24);
        traceY.SetBounds(0, top, width, ScaleLogical(25)); top += ScaleLogical(31);
        copyTrace.SetBounds(0, top, width, ScaleLogical(32)); top += ScaleLogical(38);
        hint.SetBounds(0, top, width, ScaleLogical(50)); top += ScaleLogical(54);
        advancedContent.Size = new(width, top);
        advancedScroll.AutoScrollMinSize = new(0, top);
    }

    private RectangleF PlotBounds => new(ScaleLogical(48), ScaleLogical(16),
        Math.Max(1, plot.Width - ScaleLogical(64)), Math.Max(1, plot.Height - ScaleLogical(48)));
    private float PlotX(double x) => (float)(PlotBounds.Left + (x - viewport.XMin) / viewport.Width * PlotBounds.Width);
    private float PlotY(double y) => (float)(PlotBounds.Top + (viewport.YMax - y) / viewport.Height * PlotBounds.Height);

    private void Render(Graphics graphics)
    {
        graphics.Clear(Palette.Surface);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = PlotBounds;
        using var grid = new Pen(Palette.Border);
        using var axis = new Pen(Palette.Muted);
        using var text = new SolidBrush(Palette.Muted);
        using var tickFont = Ui.Font(10);
        for (int i = 0; i <= 4; i++)
        {
            float x = area.Left + area.Width * i / 4, y = area.Top + area.Height * i / 4;
            graphics.DrawLine(grid, x, area.Top, x, area.Bottom);
            graphics.DrawLine(grid, area.Left, y, area.Right, y);
            graphics.DrawString((viewport.XMin + viewport.Width * i / 4).ToString("G3", CultureInfo.InvariantCulture), tickFont, text,
                new RectangleF(Math.Max(0, x - ScaleLogical(20)), area.Bottom + ScaleLogical(6), ScaleLogical(54), ScaleLogical(20)));
            graphics.DrawString((viewport.YMax - viewport.Height * i / 4).ToString("G3", CultureInfo.InvariantCulture), tickFont, text,
                new RectangleF(ScaleLogical(2), y - ScaleLogical(8), ScaleLogical(43), ScaleLogical(20)));
        }
        GraphicsState state = graphics.Save();
        graphics.SetClip(area);
        if (viewport.XMin <= 0 && viewport.XMax >= 0) graphics.DrawLine(axis, PlotX(0), area.Top, PlotX(0), area.Bottom);
        if (viewport.YMin <= 0 && viewport.YMax >= 0) graphics.DrawLine(axis, area.Left, PlotY(0), area.Right, PlotY(0));
        foreach (var curve in plots)
        {
            using var pen = new Pen(CurveColors[(curve.Id - 1) % CurveColors.Length], Math.Max(1, ScaleLogical(2))) { DashStyle = CurveDashes[(curve.Id - 1) % CurveDashes.Length] };
            foreach (var segment in curve.Segments)
            {
                float x1 = PlotX(segment.X1), y1 = PlotY(segment.Y1), x2 = PlotX(segment.X2), y2 = PlotY(segment.Y2);
                if (float.IsFinite(x1) && float.IsFinite(y1) && float.IsFinite(x2) && float.IsFinite(y2))
                    graphics.DrawLine(pen, x1, y1, x2, y2);
            }
        }
        if (tracedX.HasValue && tracedY.HasValue && tracedX >= viewport.XMin && tracedX <= viewport.XMax && tracedY >= viewport.YMin && tracedY <= viewport.YMax)
        {
            using var marker = new SolidBrush(Palette.Text);
            float radius = ScaleLogical(4);
            graphics.FillEllipse(marker, PlotX(tracedX.Value) - radius, PlotY(tracedY.Value) - radius, radius * 2, radius * 2);
        }
        graphics.Restore(state);
        using var border = new Pen(plot.Focused ? Palette.Accent : Palette.Border, plot.Focused ? ScaleLogical(2) : 1);
        graphics.DrawRectangle(border, 1, 1, Math.Max(0, plot.Width - 3), Math.Max(0, plot.Height - 3));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { ready = false; debounce.Stop(); debounce.Dispose(); worker.Dispose(); functionMenu.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed class ExpressionRow
    {
        public int Id { get; }
        public Panel Panel { get; } = new();
        public Label Number { get; }
        public TextBox Input { get; }
        public Label Error { get; }
        public CheckBox Show { get; } = new() { Text = "Show", Checked = true, AutoSize = false };
        public Button Remove { get; }
        public ExpressionEditor Editor { get; }
        public CompiledExpression? Compiled { get; set; }
        public ExpressionRow(GraphView owner, int id)
        {
            Id = id;
            Panel.Name = "graph-function-row-" + id;
            Number = Ui.Label($"f{id}(x)", 14);
            Input = Ui.TextField("graph-expression-" + id, false, 18);
            Input.TextAlign = HorizontalAlignment.Left;
            Input.PlaceholderText = "x^2";
            Input.AccessibleName = $"Function {id} expression";
            Error = Ui.Label("", 12, "graph-error-" + id);
            Error.Tag = "error";
            Editor = new ExpressionEditor(Input);
            Remove = Ui.Button("×", () => owner.RemoveRow(this), "graph-remove-" + id);
            Remove.AccessibleName = "Remove function " + id;
            Show.AccessibleName = "Show function " + id;
            Show.Name = "graph-show-" + id;
            Panel.Controls.AddRange([Number, Show, Remove, Input, Error]);
            Panel.Paint += (_, e) =>
            {
                using var border = new Pen(owner.activeRow == this ? owner.Palette.Accent : owner.Palette.Border, owner.ScaleLogical(1));
                e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Panel.Width - 1), Math.Max(0, Panel.Height - 1));
            };
            Panel.MouseDown += (_, _) => owner.SelectRow(this);
            foreach (Control child in Panel.Controls) child.MouseDown += (_, _) => owner.SelectRow(this);
            Input.Enter += (_, _) => owner.SelectRow(this);
            Input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { owner.ApplyExpressions(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            Input.TextChanged += (_, _) =>
            {
                Compiled = null; Error.Text = ""; owner.LayoutExpressions(); owner.RequestSample(); owner.Trace();
            };
            Show.CheckedChanged += (_, _) => { owner.RequestSample(); owner.Trace(); };
        }
    }

    private sealed class PlotSurface : Control
    {
        public PlotSurface()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
            GotFocus += (_, _) => Invalidate();
            LostFocus += (_, _) => Invalidate();
        }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home || base.IsInputKey(keyData);
    }
}
