namespace Calculator.Core;

/// <summary>Cancellation plus guards before dispatch and consumption prevent stale plot results.</summary>
public sealed class GraphWorker(Action<Action> dispatch) : IDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? pending;
    private long generation;
    private bool disposed;

    public void Cancel()
    {
        lock (gate)
        {
            generation++;
            pending?.Cancel();
            pending = null;
        }
    }

    public Task Submit(IReadOnlyList<GraphCurve> curves, Viewport viewport, int width, int height,
        Action<IReadOnlyList<GraphPlot>> completed)
    {
        CancellationTokenSource source;
        long ticket;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Cancel();
            ticket = generation;
            source = new CancellationTokenSource();
            pending = source;
        }
        GraphCurve[] snapshot = curves.ToArray();
        return Task.Run(() =>
        {
            try
            {
                GraphPlot[] plots = snapshot.Select(curve => GraphSampler.Sample(curve, viewport, width, height, source.Token)).ToArray();
                lock (gate)
                {
                    if (disposed || generation != ticket) return;
                }
                dispatch(() =>
                {
                    lock (gate)
                    {
                        if (!disposed && generation == ticket) completed(Array.AsReadOnly(plots));
                    }
                });
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
            finally
            {
                lock (gate)
                {
                    if (ReferenceEquals(pending, source)) pending = null;
                    source.Dispose();
                }
            }
        });
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            Cancel();
            disposed = true;
        }
    }
}
