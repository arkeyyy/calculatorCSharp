namespace Calculator.Core;

/// <summary>One shared refresh preserves the most recent valid snapshot through failures.</summary>
public sealed class CurrencyRates : IDisposable
{
    private readonly ICurrencyRateProvider provider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken cancellation;
    private readonly object gate = new();
    private Task<CurrencyRateSnapshot>? request;
    private bool disposed;
    public CurrencyRateSnapshot? Snapshot { get; private set; }

    public CurrencyRates(ICurrencyRateProvider provider, CurrencyRateSnapshot? cached = null)
    {
        this.provider = provider;
        cancellation = lifetime.Token;
        Snapshot = cached;
    }

    public Task<CurrencyRateSnapshot> RefreshAsync()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (request is { IsCompleted: false }) return request;
            return request = FetchAsync();
        }
    }

    private async Task<CurrencyRateSnapshot> FetchAsync()
    {
        var replacement = await provider.FetchAsync(cancellation).ConfigureAwait(false);
        lock (gate)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Snapshot is not null && replacement.Date < Snapshot.Date)
                throw new ArgumentException("ECB returned an older rate date.");
            Snapshot = replacement;
        }
        return replacement;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        lifetime.Cancel();
        if (provider is IDisposable disposable) disposable.Dispose();
        lifetime.Dispose();
    }
}
