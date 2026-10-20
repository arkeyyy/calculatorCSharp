using System.Text;
using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Core.Tests;

[TestClass]
public sealed class CurrencyTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static CurrencyRateSnapshot Snapshot(string date = "2026-10-15") => CurrencyRateSnapshot.Decode(date + "\nEUR=1\nJPY=177.06\nUSD=1.1208", Today);

    [TestMethod]
    public void SnapshotRoundTripsExactDecimalsAndConvertsInBothDirections()
    {
        var snapshot = Snapshot();
        Assert.AreEqual("2026-10-15\nEUR=1\nJPY=177.06\nUSD=1.1208", snapshot.Encode());
        Assert.AreEqual(0, snapshot.Convert(Numbers.Parse("10"), "EUR", "USD").CompareTo(Numbers.Parse("11.208")));
        Assert.AreEqual(0, snapshot.Convert(Numbers.Parse("11.208"), "USD", "EUR").CompareTo(Numbers.Parse("10")));
        Assert.ThrowsException<ArgumentException>(() => snapshot.Convert(EDecimal.One, "GBP", "USD"));
    }

    [DataTestMethod]
    [DataRow("2026-10-21\nEUR=1\nUSD=1")]
    [DataRow("2026-10-15\nEUR=2\nUSD=1")]
    [DataRow("2026-10-15\nEUR=1\nUSD=0")]
    [DataRow("2026-10-15\nEUR=1\nUSD=-1")]
    [DataRow("2026-10-15\nEUR=1\nUSD=NaN")]
    [DataRow("2026-10-15\nEUR=1\nUSD=1e-13")]
    [DataRow("2026-10-15\nEUR=1\nUSD=1.1234567890123456789012345678901234")]
    [DataRow("2026-10-15\nEUR=1\nZZZ=1")]
    [DataRow("2026-10-15\nEUR=1\nUSD=1\nUSD=2")]
    [DataRow("2026-10-15\nEUR=1\nUSD=1\n")]
    public void InvalidCacheIsRejected(string text) => Assert.ThrowsException<ArgumentException>(() => CurrencyRateSnapshot.Decode(text, Today));

    [TestMethod]
    public void CurrencyPresentationAndTimedSearchAreStable()
    {
        var codes = CurrencyPresentation.SortedCodes(["USD", "JPY", "CNY", "CHF", "CAD", "CZK", "EUR"]);
        Assert.AreEqual("China — Chinese yuan (CNY)", CurrencyPresentation.ForCode("CNY").Label);
        Assert.AreEqual("Euro area — Euro (EUR)", CurrencyPresentation.ForCode("EUR").Label);
        Assert.AreEqual("ZZZ (ZZZ)", CurrencyPresentation.ForCode("ZZZ").Label);
        var search = new CurrencyTypeAhead();
        Assert.AreEqual("CAD", search.Find(codes, "USD", "C", 0));
        Assert.AreEqual("CNY", search.Find(codes, "CAD", "c", 100));
        Assert.AreEqual("CZK", search.Find(codes, "CNY", "C", 200));
        Assert.AreEqual("CAD", search.Find(codes, "CZK", "c", 300));
        search.Reset();
        Assert.AreEqual("CAD", search.Find(codes, "USD", "C", 1000));
        Assert.AreEqual("CNY", search.Find(codes, "CAD", "h", 1100));
        Assert.AreEqual("CNY", search.Find(codes, "CNY", "i", 1200));
        Assert.AreEqual("JPY", search.Find(codes, "CNY", "J", 2200));
        Assert.AreEqual("JPY", search.Find(codes, "JPY", "z", 2300));
    }

    [TestMethod]
    public void FeedParserAcceptsOneDatedNamespaceAndRejectsUnsafeOrAmbiguousXml()
    {
        const string start = "<Envelope xmlns='http://www.ecb.int/vocabulary/2002-08-01/eurofxref'><Cube><Cube time='2026-10-15'>";
        const string end = "</Cube></Cube></Envelope>";
        var snapshot = EcbRateProvider.Parse(Encoding.UTF8.GetBytes(start + "<Cube currency='USD' rate='1.1208'/>" + end), Today);
        Assert.AreEqual(2, snapshot.Rates.Count);
        foreach (string invalid in new[]
        {
            "<!DOCTYPE Envelope [<!ENTITY x SYSTEM 'file:///missing'>]>" + start + "<Cube currency='USD' rate='1'/>" + end,
            start + "<Cube currency='USD' rate='1'/><Cube currency='USD' rate='2'/>" + end,
            start + "<Cube currency='USD' rate='1'/><Cube time='2026-10-14'/>" + end,
            start + "<Cube currency='USD' rate='1'><nested/></Cube>" + end,
            start + "<invalid currency='USD' rate='1'/>" + end
        }) Assert.ThrowsException<ArgumentException>(() => EcbRateProvider.Parse(Encoding.UTF8.GetBytes(invalid), Today));
        Assert.ThrowsException<ArgumentException>(() => EcbRateProvider.Parse(new byte[EcbRateProvider.MaximumBytes + 1], Today));
    }

    [TestMethod]
    public async Task RefreshDeduplicatesAndPreservesCacheAfterFailureOrOlderRates()
    {
        var provider = new ControlledProvider();
        var cache = Snapshot();
        using var rates = new CurrencyRates(provider, cache);
        Task<CurrencyRateSnapshot> first = rates.RefreshAsync();
        Assert.AreSame(first, rates.RefreshAsync());
        Assert.AreEqual(1, provider.Calls);
        provider.Pending.SetException(new HttpRequestException("Offline"));
        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => first);
        Assert.AreSame(cache, rates.Snapshot);
        var second = rates.RefreshAsync();
        provider.Pending.SetResult(Snapshot("2026-10-14"));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => second);
        Assert.AreSame(cache, rates.Snapshot);
        var third = rates.RefreshAsync();
        var newer = Snapshot("2026-10-16");
        provider.Pending.SetResult(newer);
        Assert.AreSame(newer, await third);
        Assert.AreSame(newer, rates.Snapshot);
    }

    private sealed class ControlledProvider : ICurrencyRateProvider
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<CurrencyRateSnapshot> Pending { get; private set; } = new();
        public Task<CurrencyRateSnapshot> FetchAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return Pending.Task;
        }
    }

    [TestMethod]
    public async Task ClosingRatesCancelsRequestsAndDiscardsLateResults()
    {
        var provider = new ControlledProvider();
        var cached = Snapshot();
        var rates = new CurrencyRates(provider, cached);
        var pending = rates.RefreshAsync();
        rates.Dispose();
        provider.Pending.SetResult(Snapshot("2026-10-16"));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => pending);
        Assert.AreSame(cached, rates.Snapshot);
        Assert.ThrowsException<ObjectDisposedException>(() => rates.RefreshAsync());
        rates.Dispose();
    }
}
