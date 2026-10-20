using System.Diagnostics;
using PeterO.Numbers;

namespace Calculator.Core;

public sealed record Viewport
{
    public static readonly Viewport Default = new(-10, 10, -10, 10);
    public double XMin { get; }
    public double XMax { get; }
    public double YMin { get; }
    public double YMax { get; }
    public double Width => XMax - XMin;
    public double Height => YMax - YMin;
    public Viewport(double xMin, double xMax, double yMin, double yMax)
    {
        if (new[] { xMin, xMax, yMin, yMax }.Any(value => !double.IsFinite(value) || Math.Abs(value) > 1000000))
            throw new ArgumentException("Coordinates must be finite and within ±1,000,000");
        if (xMax - xMin < .000001 || yMax - yMin < .000001)
            throw new ArgumentException("Each axis must increase by at least 0.000001");
        XMin = xMin; XMax = xMax; YMin = yMin; YMax = yMax;
    }
    public Viewport Pan(double dx, double dy) => new(XMin + dx, XMax + dx, YMin + dy, YMax + dy);
    public Viewport Zoom(double factor, double x, double y) => new(
        x + (XMin - x) * factor, x + (XMax - x) * factor,
        y + (YMin - y) * factor, y + (YMax - y) * factor);
}

public sealed record GraphCurve(int Id, Func<EDecimal, CancellationToken, EDecimal> Evaluate)
{
    public GraphCurve(int id, Func<EDecimal, EDecimal> evaluate) : this(id, (x, _) => evaluate(x)) { }
}
public readonly record struct GraphSegment(double X1, double Y1, double X2, double Y2);
public sealed record GraphPlot(int Id, IReadOnlyList<GraphSegment> Segments, bool Limited);

/// <summary>Bounded adaptive sampling; undefined and unresolved intervals stay disconnected.</summary>
public sealed class GraphSampler
{
    private readonly Viewport viewport;
    private readonly double tolerance;
    private readonly CancellationToken cancellationToken;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private int evaluations;
    private bool limited;
    private GraphSampler(Viewport viewport, int height, CancellationToken cancellationToken)
    {
        this.viewport = viewport;
        tolerance = viewport.Height / Math.Max(240, height) * .75;
        this.cancellationToken = cancellationToken;
    }
    public static GraphPlot Sample(GraphCurve curve, Viewport viewport, int width, int height, CancellationToken cancellationToken = default)
    {
        var sampler = new GraphSampler(viewport, height, cancellationToken);
        var segments = new List<GraphSegment>();
        int steps = Math.Clamp(width, 128, 1200);
        double x1 = viewport.XMin, y1 = sampler.Value(curve, x1);
        for (int i = 1; i <= steps; i++)
        {
            double x2 = viewport.XMin + viewport.Width * i / steps;
            double y2 = sampler.Value(curve, x2);
            sampler.Interval(curve, x1, y1, x2, y2, 0, segments);
            if (sampler.limited) break;
            x1 = x2; y1 = y2;
        }
        return new(curve.Id, segments.AsReadOnly(), sampler.limited);
    }
    private double Value(GraphCurve curve, double x)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (++evaluations > 16000 || elapsed.Elapsed >= TimeSpan.FromSeconds(2))
        {
            limited = true;
            return double.NaN;
        }
        try
        {
            double value = curve.Evaluate(EDecimal.FromString(x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)), cancellationToken).ToDouble();
            return double.IsFinite(value) ? value : double.NaN;
        }
        catch (Exception error) when (error is CalculationException or ArithmeticException)
        {
            return double.NaN;
        }
    }
    private void Interval(GraphCurve curve, double x1, double y1, double x2, double y2, int depth, List<GraphSegment> segments)
    {
        if (limited) return;
        double middle = x1 + (x2 - x1) / 2, ym = Value(curve, middle);
        bool finite = double.IsFinite(y1) && double.IsFinite(y2) && double.IsFinite(ym);
        if (!double.IsFinite(y1) && !double.IsFinite(y2) && !double.IsFinite(ym)) return;
        if (finite && (y1 > viewport.YMax && y2 > viewport.YMax && ym > viewport.YMax ||
            y1 < viewport.YMin && y2 < viewport.YMin && ym < viewport.YMin)) return;
        if (finite && Math.Abs(ym - (y1 / 2 + y2 / 2)) <= tolerance && Math.Abs(y2 - y1) <= viewport.Height / 3)
            segments.Add(new(x1, y1, x2, y2));
        else if (depth < 8)
        {
            Interval(curve, x1, y1, middle, ym, depth + 1, segments);
            Interval(curve, middle, ym, x2, y2, depth + 1, segments);
        }
    }
}
