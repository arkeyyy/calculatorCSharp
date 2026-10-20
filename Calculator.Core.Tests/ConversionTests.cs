using Calculator.Core;
using PeterO.Numbers;

namespace Calculator.Core.Tests;

[TestClass]
public sealed class ConversionTests
{
    [TestMethod]
    public void CatalogContainsAllTwelveCategoriesAndSeventyFiveUnits()
    {
        Assert.AreEqual(12, UnitCatalog.Categories.Count);
        Assert.AreEqual(75, UnitCatalog.Categories.Sum(category => category.Units.Count));
        foreach (var category in UnitCatalog.Categories)
        {
            Assert.AreEqual(category.Units.Count, category.Units.Select(unit => unit.Id).Distinct().Count());
            foreach (var unit in category.Units)
                Assert.AreEqual(0, category.Convert(EDecimal.One, unit.Id, unit.Id).CompareTo(EDecimal.One));
        }
    }

    [DataTestMethod]
    [DataRow(ModeId.LENGTH, "mi", "m", "1", "1609.344")]
    [DataRow(ModeId.AREA, "acre", "m2", "1", "4046.8564224")]
    [DataRow(ModeId.VOLUME, "usgal", "l", "1", "3.785411784")]
    [DataRow(ModeId.MASS, "lb", "kg", "1", "0.45359237")]
    [DataRow(ModeId.SPEED, "mph", "kph", "10", "16.09344")]
    [DataRow(ModeId.TIME, "week", "s", "1", "604800")]
    [DataRow(ModeId.STORAGE, "kib", "b", "1", "1024")]
    [DataRow(ModeId.STORAGE, "kb", "b", "1", "1000")]
    [DataRow(ModeId.ENERGY, "cal", "j", "1", "4.184")]
    [DataRow(ModeId.POWER, "hp", "w", "1", "745.69987158227022")]
    [DataRow(ModeId.PRESSURE, "atm", "pa", "1", "101325")]
    [DataRow(ModeId.ANGLE, "gon", "deg", "100", "90")]
    public void DefinitionsPreserveExactReferenceFactors(ModeId mode, string from, string to, string input, string expected)
    {
        Assert.AreEqual(0, UnitCatalog.Get(mode).Convert(Numbers.Parse(input), from, to).CompareTo(EDecimal.FromString(expected)));
    }

    [TestMethod]
    public void TemperatureOffsetsAndAbsoluteZeroAreEnforced()
    {
        var temperature = UnitCatalog.Get(ModeId.TEMPERATURE);
        Assert.IsTrue(temperature.Convert(Numbers.Parse("32"), "fahrenheit", "celsius").IsZero);
        Assert.IsTrue(temperature.Convert(Numbers.Parse("-273.15"), "celsius", "kelvin").IsZero);
        Assert.AreEqual(0, temperature.Convert(Numbers.Parse("100"), "celsius", "fahrenheit").CompareTo(Numbers.Parse("212")));
        Assert.ThrowsException<CalculationException>(() => temperature.Convert(Numbers.Parse("-0.01"), "kelvin", "celsius"));
        Assert.ThrowsException<ArgumentException>(() => temperature.Convert(EDecimal.One, "mm", "celsius"));
    }

    [TestMethod]
    public void SelectionAndIgnoredKeysDoNotPromoteAFormattedResult()
    {
        int conversions = 0;
        var state = new ConverterSession((value, reverse) =>
        {
            conversions++;
            return reverse ? Numbers.Multiply(value, Numbers.Parse("3")) : Numbers.Divide(value, Numbers.Parse("3"));
        });
        state.Update();
        string raw = state.RawSource;
        state.Activate(1);
        Assert.AreEqual(1, conversions);
        Assert.AreEqual(0, state.SourceSide);
        state.Entry(1).Select(1, 1);
        state.Press(".");
        Assert.AreEqual(0, state.SourceSide);
        Assert.AreEqual(raw, state.RawSource);
        state.Entry(1).Select(0, 0);
        state.Press("⌫");
        Assert.AreEqual(0, state.SourceSide);
        Assert.AreEqual(1, conversions);
        state.Activate(1);
        state.Press("2");
        Assert.AreEqual(1, state.SourceSide);
        Assert.AreEqual("6", state.Text(0));
        state.Press("5");
        Assert.AreEqual("25", state.RawSource);
        Assert.AreEqual("75", state.Text(0));
    }

    [TestMethod]
    public void EntryEditsSelectionAndRejectsOverflowWithoutChangingAuthority()
    {
        var entry = new NumericEntry("12345");
        entry.Select(1, 4);
        entry.Press("9");
        Assert.AreEqual("195", entry.Text);
        entry.Press("⌫");
        Assert.AreEqual("15", entry.Text);
        entry.Select(0, 2);
        entry.Press(".");
        Assert.AreEqual("0.", entry.Text);
        entry.Press("5");
        entry.Press("±");
        Assert.AreEqual("-0.5", entry.Text);
        entry.SetText(new string('9', NumericEntry.Limit));
        long revision = entry.Revision;
        entry.Press("1");
        Assert.AreEqual(revision, entry.Revision);
        entry.Press("±");
        Assert.AreEqual(revision, entry.Revision);
    }

    [TestMethod]
    public void IncompleteInputAndSwapPreserveTheEditingState()
    {
        var state = new ConverterSession((value, reverse) => reverse ? Numbers.Divide(value, Numbers.Parse("100")) : Numbers.Multiply(value, Numbers.Parse("100")));
        state.Update();
        state.Entry(0).Select(0, 1);
        state.Swap();
        Assert.AreEqual(1, state.SourceSide);
        Assert.AreEqual(1, state.ActiveSide);
        Assert.AreEqual("1", state.RawSource);
        Assert.AreEqual("100", state.Result);
        Assert.AreEqual(0, state.Entry(1).Anchor);
        foreach (string incomplete in new[] { "", "-", "+", ".", "-.", "+." })
        {
            state.TrySetText(1, incomplete);
            Assert.AreEqual(incomplete, state.RawSource);
            Assert.AreEqual("", state.Error);
            Assert.AreEqual("", state.Result);
        }
    }

    [TestMethod]
    public void InvalidNativeEditsDoNotReplaceRawQuantityOrChangeSourceSide()
    {
        var state = new ConverterSession((value, reverse) => reverse ? Numbers.Multiply(value, Numbers.Parse("3")) : Numbers.Divide(value, Numbers.Parse("3")));
        state.TrySetText(0, "1.23456789012345678901234567890123456789");
        string raw = state.RawSource;
        state.Activate(1);
        Assert.IsFalse(state.TrySetText(1, "not a number"));
        Assert.IsFalse(state.TrySetText(1, "1..2"));
        Assert.IsFalse(state.TrySetText(1, new string('1', NumericEntry.Limit + 1)));
        Assert.AreEqual(0, state.SourceSide);
        Assert.AreEqual(raw, state.RawSource);
        Assert.AreEqual(34, Numbers.Parse(state.Text(1)).Precision().ToInt32Checked());
        state.Activate(0);
        Assert.AreEqual(raw, state.Text(0), "Activating a formatted side must never overwrite the full raw entry.");
    }
}
