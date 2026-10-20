using System.Collections.Concurrent;
using System.Numerics;
using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Core.Tests;

[TestClass]
public sealed class AdditionalEngineTests
{
    [TestMethod]
    public void ProgrammerSignedRoundTripsEveryWordAndBase()
    {
        var engine = new ProgrammerEngine();
        foreach (WordSize size in Enum.GetValues<WordSize>())
        foreach (NumberBase numberBase in Enum.GetValues<NumberBase>())
        {
            engine.Configure(numberBase, size);
            foreach (long value in new long[] { 0, 1, -1, 127, -128 })
                Assert.AreEqual(new BigInteger(value), engine.Parse(engine.Format(value, numberBase), numberBase));
        }
    }

    [TestMethod]
    public void ProgrammerWrapsAndParsesHexadecimalAsUnsignedMagnitude()
    {
        var engine = new ProgrammerEngine();
        engine.Configure(NumberBase.HEX, WordSize.BYTE);
        Assert.AreEqual(new BigInteger(-1), engine.Parse("FF", NumberBase.HEX));
        Assert.AreEqual(new BigInteger(-128), engine.Apply(BitOperation.ADD, 127, 1));
        Assert.AreEqual(BigInteger.Zero, engine.Signed(256));
        Assert.AreEqual(new BigInteger(-2), engine.Apply(BitOperation.DIVIDE, -7, 3));
        Assert.AreEqual(new BigInteger(-128), engine.Apply(BitOperation.DIVIDE, -128, -1));
        engine.Configure(NumberBase.HEX, WordSize.QWORD);
        Assert.AreEqual(new BigInteger(-1), engine.Parse("FFFFFFFFFFFFFFFF", NumberBase.HEX));
        Assert.AreEqual("FFFFFFFFFFFFFFFF", engine.Format(-1, NumberBase.HEX));
    }

    [TestMethod]
    public void ProgrammerBitOperationsAndValidation()
    {
        var engine = new ProgrammerEngine();
        engine.Configure(NumberBase.DEC, WordSize.BYTE);
        Assert.AreEqual(new BigInteger(-1), engine.Apply(BitOperation.RIGHT, -2, 1));
        Assert.AreEqual(new BigInteger(127), engine.Apply(BitOperation.UNSIGNED_RIGHT, -2, 1));
        Assert.AreEqual(new BigInteger(8), engine.Apply(BitOperation.LEFT, 1, 3));
        Assert.AreEqual(new BigInteger(2), engine.Apply(BitOperation.AND, 6, 3));
        Assert.AreEqual(new BigInteger(7), engine.Apply(BitOperation.OR, 6, 3));
        Assert.AreEqual(new BigInteger(5), engine.Apply(BitOperation.XOR, 6, 3));
        Assert.AreEqual(new BigInteger(-1), engine.Not(0));
        Assert.AreEqual(new BigInteger(-128), engine.Toggle(0, 7));
        Assert.ThrowsException<CalculationException>(() => engine.Parse("2", NumberBase.BIN));
        Assert.ThrowsException<CalculationException>(() => engine.Parse(new string('1', 129), NumberBase.BIN));
        Assert.ThrowsException<CalculationException>(() => engine.Apply(BitOperation.DIVIDE, 1, 0));
        Assert.ThrowsException<CalculationException>(() => engine.Apply(BitOperation.LEFT, 1, -1));
        Assert.ThrowsException<CalculationException>(() => engine.Apply(BitOperation.RIGHT, 1, 8));
        Assert.ThrowsException<CalculationException>(() => engine.Toggle(0, 8));
    }

    [TestMethod]
    public void ProgrammerEntrySupportsOperationsBackspaceAndErrorRecovery()
    {
        var engine = new ProgrammerEngine();
        string? calculated = null;
        engine.Calculated += (_, value) => calculated = value;
        foreach (string key in new[] { "6", "AND", "3", "9", "Backspace", "=" }) engine.Press(key);
        Assert.AreEqual("2", engine.Display);
        Assert.AreEqual("2", calculated);
        engine.Press("/"); engine.Press("0"); engine.Press("=");
        Assert.IsNotNull(engine.Error);
        engine.Press("4");
        Assert.AreEqual("4", engine.Display);
        engine.Configure(NumberBase.BIN, WordSize.BYTE);
        engine.Press("9");
        Assert.AreEqual("100", engine.Display);
        engine.Press("C");
        Assert.AreEqual("100", engine.Display, "Invalid hexadecimal keyboard digits must not clear the current value.");
        engine.Press("Clear");
        Assert.AreEqual("0", engine.Display);
    }

    [TestMethod]
    public void GregorianEpochMatchesDotNetForOrdinaryDates()
    {
        var epoch = new DateTime(1970, 1, 1);
        foreach (int year in new[] { 1, 4, 100, 400, 1582, 1900, 1970, 2000, 2024, 9999 })
        foreach (int month in new[] { 1, 2, 3, 12 })
        {
            var date = new CalendarDate(year, month, CalendarDate.DaysInMonth(year, month));
            Assert.AreEqual((date.ToDateTime() - epoch).Days, date.ToEpochDay());
            Assert.AreEqual(date, CalendarDate.FromEpochDay(date.ToEpochDay()));
        }
    }

    [TestMethod]
    public void ExtendedDatesPreserveProlepticGregorianRangeAndFormatting()
    {
        foreach (int year in new[] { -999999999, -10000, -400, -100, -1, 0, 1, 10000, 999999999 })
        {
            var date = new CalendarDate(year, 3, 1);
            Assert.AreEqual(date, CalendarDate.FromEpochDay(date.ToEpochDay()));
            Assert.AreEqual(date, CalendarDate.Parse(date.ToString()));
        }
        Assert.AreEqual("+10000-01-01", new CalendarDate(9999, 12, 31).PlusDays(1).ToString());
        Assert.AreEqual("0000-12-31", new CalendarDate(1, 1, 1).PlusDays(-1).ToString());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CalendarDate(999999999, 12, 31).PlusDays(1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => CalendarDate.Parse("1900-02-29"));
    }

    [TestMethod]
    public void ExtendedGregorianDatesKeepThe400YearCycleAcrossNegativeYears()
    {
        foreach (int year in new[] { -999999599, -800, -401, -400, -100, -4, -1, 0, 1, 400, 999999000 })
        {
            var date = new CalendarDate(year, 3, 1);
            Assert.AreEqual(146097L, date.PlusYears(400).ToEpochDay() - date.ToEpochDay());
            Assert.AreEqual(date, date.PlusMonths(-4800).PlusMonths(4800));
            Assert.AreEqual(date, CalendarDate.FromEpochDay(date.ToEpochDay()));
        }
        Assert.IsTrue(CalendarDate.IsLeapYear(0));
        Assert.IsTrue(CalendarDate.IsLeapYear(-400));
        Assert.IsFalse(CalendarDate.IsLeapYear(-100));
        Assert.AreEqual(new CalendarDate(-1, 12, 1), new CalendarDate(0, 1, 1).PlusMonths(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CalendarDate(-999999999, 1, 1).PlusDays(-1));
    }

    [TestMethod]
    public void DateAdjustsInYearMonthDayOrderAndKeepsSignedRemainders()
    {
        var calculator = new DateCalculator();
        Assert.AreEqual(new CalendarDate(2025, 3, 28), calculator.Adjust(new(2024, 2, 29), 1, 1, 0));
        Assert.AreEqual(new CalendarDate(2024, 2, 29), calculator.Adjust(new(2024, 1, 31), 0, 1, 0));
        Assert.AreEqual(new CalendarDate(2023, 12, 31), calculator.Adjust(new(2024, 1, 1), 0, 0, 1, true));
        var difference = calculator.Between(new(2024, 1, 10), new(2024, 1, 1));
        Assert.AreEqual(-9L, difference.Days);
        Assert.AreEqual(-1L, difference.Weeks);
        Assert.AreEqual(-2L, difference.RemainingDays);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => calculator.Adjust(CalendarDate.Today, 10000, 0, 0));
    }

    [TestMethod]
    public void GraphSamplerKeepsDiscontinuitiesDisconnected()
    {
        var plot = GraphSampler.Sample(new(1, x => Numbers.Divide(EDecimal.One, x)), Viewport.Default, 640, 400);
        Assert.IsTrue(plot.Segments.Count > 0);
        Assert.IsFalse(plot.Segments.Any(segment => segment.X1 < 0 && segment.X2 > 0));
        Assert.IsFalse(plot.Segments.Any(segment => Math.Abs(segment.Y2 - segment.Y1) > Viewport.Default.Height / 3));
        Assert.IsFalse(plot.Limited);
    }

    [TestMethod]
    public void GraphSamplerCancellationAndViewportLimits()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => GraphSampler.Sample(new(1, x => x), Viewport.Default, 100, 100, cancellation.Token));
        Assert.ThrowsException<ArgumentException>(() => new Viewport(1, 1, -10, 10));
        Assert.ThrowsException<ArgumentException>(() => new Viewport(double.NaN, 10, -10, 10));
        Assert.ThrowsException<ArgumentException>(() => Viewport.Default.Pan(1000000, 0));
        Assert.AreEqual(new Viewport(-5, 5, -5, 5), Viewport.Default.Zoom(.5, 0, 0));
    }

    [TestMethod]
    public void GraphSamplerStopsAtItsEvaluationBudget()
    {
        int evaluations = 0;
        var curve = new GraphCurve(1, _ => EDecimal.FromInt32(++evaluations % 2 == 0 ? 100 : -100));
        GraphPlot result = GraphSampler.Sample(curve, Viewport.Default, 1200, 400);
        Assert.IsTrue(result.Limited);
        Assert.IsTrue(evaluations <= 16000);
    }

    [TestMethod]
    public void GraphSamplerPassesCancellationIntoExpressionEvaluation()
    {
        using var cancellation = new CancellationTokenSource();
        var curve = new GraphCurve(1, (x, token) =>
        {
            Assert.AreEqual(cancellation.Token, token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return x;
        });
        Assert.ThrowsException<OperationCanceledException>(() => GraphSampler.Sample(curve, Viewport.Default, 128, 240, cancellation.Token));
    }

    [TestMethod]
    public async Task GraphWorkerRejectsResultAlreadyQueuedBeforeNewRequest()
    {
        var dispatches = new ConcurrentQueue<Action>();
        var completed = new List<int>();
        using var worker = new GraphWorker(dispatches.Enqueue);
        await worker.Submit(new[] { new GraphCurve(1, x => x) }, Viewport.Default, 128, 240, plots => completed.Add(plots[0].Id));
        await worker.Submit(new[] { new GraphCurve(2, x => x) }, Viewport.Default, 128, 240, plots => completed.Add(plots[0].Id));
        while (dispatches.TryDequeue(out Action? action)) action();
        CollectionAssert.AreEqual(new[] { 2 }, completed);
    }

    [TestMethod]
    public async Task GraphWorkerCancellationRejectsQueuedResult()
    {
        var dispatches = new ConcurrentQueue<Action>();
        bool completed = false;
        using var worker = new GraphWorker(dispatches.Enqueue);
        await worker.Submit(new[] { new GraphCurve(1, x => x) }, Viewport.Default, 128, 240, _ => completed = true);
        worker.Cancel();
        while (dispatches.TryDequeue(out Action? action)) action();
        Assert.IsFalse(completed);
    }

    [TestMethod]
    public async Task GraphWorkerCancelsInFlightWorkBeforeDispatch()
    {
        var dispatches = new ConcurrentQueue<Action>();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var worker = new GraphWorker(dispatches.Enqueue);
        var completed = new List<int>();
        var slow = new GraphCurve(1, x => { entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); return x; });
        Task first = worker.Submit(new[] { slow }, Viewport.Default, 128, 240, plots => completed.Add(plots[0].Id));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        Task second = worker.Submit(new[] { new GraphCurve(2, x => x) }, Viewport.Default, 128, 240, plots => completed.Add(plots[0].Id));
        release.Set();
        await Task.WhenAll(first, second);
        while (dispatches.TryDequeue(out Action? action)) action();
        CollectionAssert.AreEqual(new[] { 2 }, completed);
    }

    [TestMethod]
    public async Task GraphWorkerDoesNotHoldStateLockWhileInvokingDispatcher()
    {
        bool dispatchedWithoutBlocking = false;
        bool completed = false;
        using var worker = new GraphWorker(action => dispatchedWithoutBlocking = Task.Run(action).Wait(TimeSpan.FromSeconds(2)));
        await worker.Submit(new[] { new GraphCurve(1, x => x) }, Viewport.Default, 128, 240, _ => completed = true);
        Assert.IsTrue(dispatchedWithoutBlocking, "Synchronous UI dispatch must not deadlock on the worker state lock.");
        Assert.IsTrue(completed);
    }
}
