using System.Globalization;
using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Core.Tests;

[TestClass]
public sealed class EngineTests
{
    private static void EqualNumber(string expected, EDecimal actual) =>
        Assert.AreEqual(0, EDecimal.FromString(expected).CompareTo(actual), $"Expected {expected}; received {Numbers.Format(actual)}");

    private static void Enter(StandardEngine engine, params string[] keys)
    {
        foreach (string key in keys) engine.Press(key);
    }

    [DataTestMethod]
    [DataRow("1E+9999")]
    [DataRow("1E-10000")]
    [DataRow("-1E+9999")]
    [DataRow("0")]
    [DataRow(".5")]
    [DataRow("+1.")]
    public void NumberBoundariesAndAcceptedSyntaxSurviveFormatting(string input)
    {
        EDecimal value = Numbers.Parse(input);
        Assert.AreEqual(0, value.CompareTo(Numbers.Parse(Numbers.Format(value))));
    }

    [DataTestMethod]
    [DataRow("1E+10000")]
    [DataRow("1E-10001")]
    [DataRow("0E+10001")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("1E")]
    [DataRow("1..2")]
    [DataRow(" 1")]
    [DataRow("1,5")]
    [DataRow("1e100000")]
    [DataRow("١")]
    public void InvalidAndOutOfRangeNumbersAreRejected(string input) =>
        Assert.ThrowsException<CalculationException>(() => Numbers.Parse(input));

    [TestMethod]
    public void NumberPrecisionIsThirtyFourDigitsWithHalfEvenRounding()
    {
        EqualNumber("1.234567890123456789012345678901234", Numbers.Parse("1.2345678901234567890123456789012345"));
        EqualNumber("1.234567890123456789012345678901236", Numbers.Parse("1.2345678901234567890123456789012355"));
        EqualNumber("0.3333333333333333333333333333333333", Numbers.Divide(EDecimal.One, EDecimal.FromInt32(3)));
        EqualNumber("0.3", Numbers.Add(Numbers.Parse("0.1"), Numbers.Parse("0.2")));
        Assert.ThrowsException<CalculationException>(() => Numbers.Parse(new string('1', 4097)));
    }

    [DataTestMethod]
    [DataRow("1000000000000000", "1000000000000000")]
    [DataRow("10000000000000000", "1E+16")]
    [DataRow("0.000000001", "0.000000001")]
    [DataRow("0.0000000001", "1E-10")]
    [DataRow("-123400000000000000", "-1.234E+17")]
    [DataRow("1.23000", "1.23")]
    [DataRow("-0.000", "0")]
    public void DisplayFormattingUsesTheDocumentedThresholds(string input, string expected) =>
        Assert.AreEqual(expected, Numbers.Format(Numbers.Parse(input)));

    [TestMethod]
    public void NumbersAreCultureIndependentAndFloatingConversionIsGuarded()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.AreEqual("1.25", Numbers.Format(Numbers.Parse("1.25")));
            EqualNumber("0.125", Numbers.Approximate(0.125));
        }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.ThrowsException<CalculationException>(() => Numbers.ToDouble(Numbers.Parse("1E1000")));
        Assert.ThrowsException<CalculationException>(() => Numbers.ToDouble(Numbers.Parse("1E-1000")));
        Assert.ThrowsException<CalculationException>(() => Numbers.Approximate(double.PositiveInfinity));
        Assert.ThrowsException<CalculationException>(() => Numbers.Checked(EDecimal.NaN));
        Assert.AreEqual(0d, Numbers.ToDouble(EDecimal.Zero));
    }

    [DataTestMethod]
    [DataRow("2+3*4", "14")]
    [DataRow("(2+3)*4", "20")]
    [DataRow("2^3^2", "512")]
    [DataRow("-2^2", "-4")]
    [DataRow("(-2)^2", "4")]
    [DataRow("2^-3", "0.125")]
    [DataRow("5!", "120")]
    [DataRow("0!", "1")]
    [DataRow("sqrt(81)", "9")]
    [DataRow("abs(-4)", "4")]
    [DataRow("0.1+0.2", "0.3")]
    [DataRow("50%", "0.5")]
    [DataRow("1E-3+2", "2.001")]
    [DataRow("2×3−4÷2", "4")]
    [DataRow("sqrt(1E-1000)", "1E-500")]
    [DataRow("abs(-1E+1000)", "1E+1000")]
    public void ScientificExactExpressionsMatchReferenceValues(string expression, string expected) =>
        EqualNumber(expected, new ScientificEngine().Evaluate(expression));

    [DataTestMethod]
    [DataRow("1/0")]
    [DataRow("sqrt(-1)")]
    [DataRow("log(0)")]
    [DataRow("ln(-1)")]
    [DataRow("asin(2)")]
    [DataRow("tan(90)")]
    [DataRow("3.5!")]
    [DataRow("1001!")]
    [DataRow("2(3)")]
    [DataRow("2+")]
    [DataRow("(2+3")]
    [DataRow("1..2")]
    [DataRow("2^10001")]
    [DataRow("0^0")]
    [DataRow("hello(2)")]
    [DataRow("sin 30")]
    [DataRow("x+1")]
    [DataRow("1E-1000^0.5")]
    [DataRow("1E+1000^0.5")]
    [DataRow("1E-200^2.5")]
    [DataRow("sin(1E-1000)")]
    [DataRow("ln(1E-1000)")]
    [DataRow("atan(1E+1000)")]
    [DataRow("sin(4.9E-324)")]
    public void ScientificInvalidExpressionsAndApproximateRangeErrorsAreRejected(string expression) =>
        Assert.ThrowsException<CalculationException>(() => new ScientificEngine().Evaluate(expression));

    [DataTestMethod]
    [DataRow("sin(0)")]
    [DataRow("asin(0)")]
    [DataRow("atan(0)")]
    [DataRow("ln(1)")]
    [DataRow("0^0.5")]
    public void ApproximateFunctionsAcceptLegitimateZero(string expression) =>
        EqualNumber("0", new ScientificEngine().Evaluate(expression));

    [TestMethod]
    public void TrigonometryHonorsIndependentAngleUnits()
    {
        var engine = new ScientificEngine();
        Assert.AreEqual(0.5, engine.Evaluate("sin(30)", AngleUnit.DEG).ToDouble(), 1e-14);
        Assert.AreEqual(1, engine.Evaluate("sin(pi/2)", AngleUnit.RAD).ToDouble(), 1e-14);
        Assert.AreEqual(30, engine.Evaluate("asin(.5)", AngleUnit.DEG).ToDouble(), 1e-12);
        Assert.AreEqual(Math.PI / 6, engine.Evaluate("asin(.5)", AngleUnit.RAD).ToDouble(), 1e-14);
        EqualNumber("2", engine.Evaluate("log(100)"));
        Assert.AreEqual(15, engine.Evaluate("pi").Precision().ToInt32Checked());
    }

    [TestMethod]
    public void ExpressionLimitsAndCancellationBoundWork()
    {
        var engine = new ScientificEngine();
        Assert.ThrowsException<CalculationException>(() => engine.Evaluate(new string('(', 200) + "1" + new string(')', 200)));
        Assert.ThrowsException<CalculationException>(() => engine.Evaluate(string.Concat(Enumerable.Repeat("1+", 260)) + "1"));
        Assert.ThrowsException<CalculationException>(() => engine.Evaluate(new string('1', 4097)));
        Assert.IsTrue(engine.Evaluate("1000!").Precision().ToInt32Checked() <= 34);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => engine.Compile("1000!", AngleUnit.RAD).Evaluate(EDecimal.Zero, cancellation.Token));
    }

    [TestMethod]
    public void CompiledGraphExpressionIsReusableAndVariableIsOptIn()
    {
        var engine = new ScientificEngine();
        CompiledExpression expression = engine.Compile("x^2+2*x+1", AngleUnit.RAD, allowX: true);
        EqualNumber("9", expression.Evaluate(EDecimal.FromInt32(2)));
        EqualNumber("0", expression.Evaluate(EDecimal.FromInt32(-1)));
        Assert.ThrowsException<CalculationException>(() => engine.Compile("x", AngleUnit.RAD));
    }

    [TestMethod]
    public void StandardExecutesImmediatelyAndRepeatsEquals()
    {
        var engine = new StandardEngine();
        Enter(engine, "2", "+", "3", "×", "4", "=");
        Assert.AreEqual("20", engine.Display);
        Assert.IsTrue(engine.Completed);
        Enter(engine, "C", "2", "+", "3", "=", "=");
        Assert.AreEqual("8", engine.Display);
        Enter(engine, "7", "=");
        Assert.AreEqual("7", engine.Display);
        Assert.IsFalse(engine.Completed);
    }

    [TestMethod]
    public void StandardOperatorReplacementClearEntryAndDecimalRemainCoherent()
    {
        var engine = new StandardEngine();
        Enter(engine, "2", "+", "×", "3", "=");
        Assert.AreEqual("6", engine.Display);
        Enter(engine, "C", "8", "+", "2", "CE", "3", "=");
        Assert.AreEqual("11", engine.Display);
        Enter(engine, "C", ".", ".", "5", "+", ".", "5", "=");
        Assert.AreEqual("1", engine.Display);
        Enter(engine, "C", "1", ".", "±", "2", "0");
        Assert.AreEqual("-1.20", engine.Display);
        engine.Press("⌫");
        Assert.AreEqual("-1.2", engine.Display);
    }

    [DataTestMethod]
    [DataRow("+", "220")]
    [DataRow("-", "180")]
    [DataRow("*", "20")]
    [DataRow("/", "2000")]
    public void StandardPercentUsesPendingOperationContext(string operation, string expected)
    {
        var engine = new StandardEngine();
        Enter(engine, "2", "0", "0", operation, "1", "0", "%", "=");
        Assert.AreEqual(expected, engine.Display);
    }

    [DataTestMethod]
    [DataRow("1E+16")]
    [DataRow("1E+9999")]
    [DataRow("-1E-1000")]
    [DataRow("12.5")]
    public void RecalledValuesAreOperandsAndNeverEditableDisplayStrings(string input)
    {
        var engine = new StandardEngine();
        engine.Recall(Numbers.Parse(input));
        engine.Press("⌫");
        EqualNumber(input, engine.Value);
        engine.Press("2");
        Assert.AreEqual("2", engine.Display);
        engine.Recall(Numbers.Parse(input));
        Enter(engine, ".", "5");
        Assert.AreEqual("0.5", engine.Display);
    }

    [TestMethod]
    public void RecallAndUnaryValuesKeepPendingOperations()
    {
        var engine = new StandardEngine();
        Enter(engine, "8", "+");
        engine.Recall(EDecimal.FromInt32(2));
        Enter(engine, "*", "3", "=");
        Assert.AreEqual("30", engine.Display);
        Enter(engine, "x²", "⌫");
        Assert.AreEqual("900", engine.Display);
        engine.Press("4");
        Assert.AreEqual("4", engine.Display);
        Enter(engine, "C", "9", "√");
        Assert.AreEqual("3", engine.Display);
        Enter(engine, "C", "4", "1/x");
        Assert.AreEqual("0.25", engine.Display);
    }

    [TestMethod]
    public void ErrorsAndEmptyEqualsRecoverWithoutStaleFlags()
    {
        var engine = new StandardEngine();
        engine.Press("=");
        Assert.AreEqual("0", engine.Display);
        Enter(engine, "1", "+", "C", "+", "2", "=");
        Assert.AreEqual("2", engine.Display);
        Enter(engine, "C", "8", "/", "0", "=");
        Assert.IsTrue(engine.Error);
        Assert.AreEqual(CalculationError.DivideByZero, engine.ErrorCode);
        engine.Press("7");
        Assert.AreEqual("7", engine.Display);
        Assert.IsFalse(engine.Error);
        engine.Recall(Numbers.Parse("1E+9999"));
        engine.Press("x²");
        Assert.IsTrue(engine.Error);
        engine.Press("C");
        Assert.AreEqual("0", engine.Display);
    }

    [TestMethod]
    public void StandardRawDigitLimitDoesNotFeedFormattedResultsBackIntoArithmetic()
    {
        var engine = new StandardEngine();
        for (int i = 0; i < 40; i++) engine.Press("1");
        Assert.AreEqual(new string('1', 34), engine.Display);
        Enter(engine, "C", "0", ".", "1", "+", "0", ".", "2", "=");
        Assert.AreEqual("0.3", engine.Display);
        Enter(engine, "C", "1", "/", "3", "=", "*", "3", "=");
        EqualNumber("0.9999999999999999999999999999999999", engine.Value);
    }
}
