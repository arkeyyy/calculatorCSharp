using System.Globalization;
using System.Text.RegularExpressions;
using PeterO.Numbers;

namespace Calculator.Core;

/// <summary>The decimal policy shared by the calculator engines and converters.</summary>
public static partial class Numbers
{
    public static readonly EContext MC = EContext.ForPrecisionAndRounding(34, ERounding.HalfEven);
    private static readonly EContext ApproximateContext = EContext.ForPrecisionAndRounding(15, ERounding.HalfEven);

    [GeneratedRegex(@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]{1,5})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex NumericPattern();

    public static EDecimal Parse(string input)
    {
        if (input is null || input.Length > 4096 || !NumericPattern().IsMatch(input))
            throw new CalculationException("Enter a valid number");
        try
        {
            return Checked(EDecimal.FromString(input, MC));
        }
        catch (FormatException)
        {
            throw new CalculationException("Enter a valid number");
        }
    }

    public static EDecimal Checked(EDecimal value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.IsFinite)
            throw new CalculationException("Result is undefined or outside the supported range", CalculationError.Domain);
        // EDecimal's exponent is the negative of Java BigDecimal's scale.
        if (value.Exponent.Abs().CompareTo(EInteger.FromInt32(10000)) > 0 ||
            value.Precision().Add(value.Exponent).CompareTo(EInteger.FromInt32(10000)) > 0)
            throw new CalculationException("Result is too large", CalculationError.TooLarge);
        return value;
    }

    public static string Format(EDecimal value)
    {
        value = Checked(value).Reduce(null);
        if (value.IsZero) return "0";
        int exponent = value.Precision().Add(value.Exponent).Subtract(1).ToInt32Checked();
        if (exponent is < 16 and > -10) return value.ToPlainString();

        // Explicit formatting avoids a library's independent notation thresholds.
        string digits = value.UnsignedMantissa.ToString();
        string significand = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
        return (value.IsNegative ? "-" : "") + significand + "E" +
            (exponent >= 0 ? "+" : "") + exponent.ToString(CultureInfo.InvariantCulture);
    }

    public static EDecimal Approximate(double value)
    {
        if (!double.IsFinite(value))
            throw new CalculationException("Result is undefined or outside the supported range", CalculationError.Domain);
        return EDecimal.FromString(value.ToString("R", CultureInfo.InvariantCulture), ApproximateContext);
    }

    public static double ToDouble(EDecimal value)
    {
        Checked(value);
        double result = value.ToDouble();
        if (!double.IsFinite(result) || result == 0 && !value.IsZero)
            throw new CalculationException("Input is outside the supported floating-point range");
        return result;
    }

    public static EDecimal Add(EDecimal a, EDecimal b) => Checked(a.Add(b, MC));
    public static EDecimal Subtract(EDecimal a, EDecimal b) => Checked(a.Subtract(b, MC));
    public static EDecimal Multiply(EDecimal a, EDecimal b) => Checked(a.Multiply(b, MC));
    public static EDecimal Divide(EDecimal a, EDecimal b)
    {
        if (b.IsZero) throw new CalculationException("Cannot divide by zero", CalculationError.DivideByZero);
        return Checked(a.Divide(b, MC));
    }
}
