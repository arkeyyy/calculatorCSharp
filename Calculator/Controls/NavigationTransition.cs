using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Calculator.Controls;

/// <summary>Elapsed-time slide state; layout and focus belong to the shell.</summary>
internal sealed class NavigationTransition : IDisposable
{
    internal const double FullDurationMilliseconds = 180;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    private readonly Action changed;
    private readonly Func<double> now;
    private readonly Func<bool?> animationsEnabled;
    private double start, startedAt, duration;
    private bool disposed;

    internal NavigationTransition(Action changed, Func<double>? now = null, Func<bool?>? animationsEnabled = null)
    {
        this.changed = changed;
        this.now = now ?? (() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency);
        this.animationsEnabled = animationsEnabled ?? ReadAnimationPreference;
        timer.Tick += Tick;
    }

    internal bool RequestedOpen { get; private set; }
    internal double Position { get; private set; }
    internal bool IsRunning => timer.Enabled;

    internal void Request(bool open)
    {
        if (disposed) return;
        double time = now();
        Advance(time);
        RequestedOpen = open;
        start = Position;
        startedAt = time;
        duration = FullDurationMilliseconds * Math.Abs((open ? 1 : 0) - start);
        if (duration == 0 || animationsEnabled() != true) Finish();
        else timer.Start();
        changed();
    }

    private void Tick(object? sender, EventArgs e) => Advance();

    // A clock seam keeps transition tests deterministic while production uses a single native timer.
    internal void Advance() => Advance(now());

    private void Advance(double time)
    {
        if (disposed || !IsRunning) return;
        double progress = Math.Clamp((time - startedAt) / duration, 0, 1);
        if (progress >= 1 || animationsEnabled() != true) Finish();
        else Position = start + ((RequestedOpen ? 1 : 0) - start) * (1 - Math.Pow(1 - progress, 3));
        changed();
    }

    private void Finish()
    {
        timer.Stop();
        Position = RequestedOpen ? 1 : 0;
    }

    private static bool? ReadAnimationPreference()
    {
        try { return SystemParametersInfo(0x1042, 0, out bool enabled, 0) ? enabled : null; }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= Tick;
        timer.Dispose();
    }
}
