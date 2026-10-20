using System.Globalization;
using System.Numerics;
using PeterO.Numbers;

namespace Calculator.Core;

public enum AngleUnit { DEG, RAD }

/// <summary>An immutable expression that can be reused across graph samples.</summary>
public sealed class CompiledExpression
{
    private readonly Func<EDecimal, CancellationToken, EDecimal> evaluate;
    internal CompiledExpression(Func<EDecimal, CancellationToken, EDecimal> evaluate) => this.evaluate = evaluate;

    public EDecimal Evaluate(EDecimal x) => Evaluate(x, CancellationToken.None);
    public EDecimal Evaluate(EDecimal x, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return Numbers.Checked(evaluate(Numbers.Checked(x), cancellationToken)); }
        catch (ArithmeticException) { throw new CalculationException("Invalid input or result outside the supported range"); }
    }
}

/// <summary>Bounded recursive-descent parser: sum, product, unary/power, postfix, primary.</summary>
public sealed class ScientificEngine
{
    public EDecimal Evaluate(string expression, AngleUnit angle = AngleUnit.DEG) =>
        Compile(expression, angle).Evaluate(EDecimal.Zero);

    public CompiledExpression Compile(string expression, AngleUnit angle, bool allowX = false)
    {
        if (expression is null || expression.Length > 4096) throw Invalid("Expression is too long");
        return new CompiledExpression(new Parser(
            expression.Replace('×', '*').Replace('÷', '/').Replace('−', '-'), angle, allowX).Parse());
    }

    private static CalculationException Invalid(string message) => new(message);

    private sealed class Parser(string text, AngleUnit angle, bool allowX)
    {
        private static readonly HashSet<string> Functions = ["sqrt", "abs", "sin", "cos", "tan", "asin", "acos", "atan", "ln", "log"];
        private int position;
        private int tokens;
        private int depth;

        public Func<EDecimal, CancellationToken, EDecimal> Parse()
        {
            var result = Sum();
            Space();
            if (position != text.Length) throw Invalid("Unexpected input at position " + (position + 1));
            return result;
        }

        private void Space()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
        }

        private void Token()
        {
            if (++tokens > 512) throw Invalid("Use at most 512 expression tokens");
        }

        private bool Take(char character)
        {
            Space();
            if (position >= text.Length || text[position] != character) return false;
            position++;
            Token();
            return true;
        }

        private Func<EDecimal, CancellationToken, EDecimal> Sum()
        {
            var value = Product();
            while (true)
            {
                var left = value;
                if (Take('+'))
                {
                    var right = Product();
                    value = (x, token) => Numbers.Add(left(x, token), right(x, token));
                }
                else if (Take('-'))
                {
                    var right = Product();
                    value = (x, token) => Numbers.Subtract(left(x, token), right(x, token));
                }
                else return value;
            }
        }

        private Func<EDecimal, CancellationToken, EDecimal> Product()
        {
            var value = Unary();
            while (true)
            {
                var left = value;
                if (Take('*'))
                {
                    var right = Unary();
                    value = (x, token) => Numbers.Multiply(left(x, token), right(x, token));
                }
                else if (Take('/'))
                {
                    var right = Unary();
                    value = (x, token) => Numbers.Divide(left(x, token), right(x, token));
                }
                else return value;
            }
        }

        private Func<EDecimal, CancellationToken, EDecimal> Unary()
        {
            if (++depth > 128) throw Invalid("Expression is nested too deeply");
            try
            {
                if (Take('+')) return Unary();
                if (Take('-'))
                {
                    var operand = Unary();
                    return (x, token) => operand(x, token).Negate();
                }
                var value = Postfix();
                if (Take('^'))
                {
                    var exponent = Unary();
                    return (x, token) => Power(value(x, token), exponent(x, token));
                }
                return value;
            }
            finally { depth--; }
        }

        private static EDecimal Power(EDecimal value, EDecimal power)
        {
            if (power.Abs().CompareTo(EDecimal.FromInt32(10000)) > 0)
                throw Invalid("Exponent must be between -10000 and 10000");
            if (power.Reduce(null).Exponent.Sign >= 0)
            {
                int exponent = power.ToInt32IfExact();
                if (value.IsZero && exponent == 0) throw Invalid("Zero to the zero power is undefined");
                return exponent < 0
                    ? Numbers.Divide(EDecimal.One, value.Pow(-exponent, Numbers.MC))
                    : Numbers.Checked(value.Pow(exponent, Numbers.MC));
            }
            double result = Math.Pow(Numbers.ToDouble(value), Numbers.ToDouble(power));
            if (result == 0 && !value.IsZero) throw Invalid("Result is below the supported floating-point range");
            return Numbers.Approximate(result);
        }

        private Func<EDecimal, CancellationToken, EDecimal> Postfix()
        {
            var value = Primary();
            while (true)
            {
                var operand = value;
                if (Take('!')) value = (x, token) => Factorial(operand(x, token), token);
                else if (Take('%')) value = (x, token) => operand(x, token).ScaleByPowerOfTen(-2);
                else return value;
            }
        }

        private static EDecimal Factorial(EDecimal value, CancellationToken token)
        {
            if (value.Sign < 0 || value.CompareTo(EDecimal.FromInt32(1000)) > 0 || value.Reduce(null).Exponent.Sign < 0)
                throw Invalid("Factorial requires an integer from 0 to 1000");
            BigInteger result = BigInteger.One;
            int upper = value.ToInt32IfExact();
            for (int i = 2; i <= upper; i++)
            {
                token.ThrowIfCancellationRequested();
                result *= i;
            }
            return EDecimal.FromString(result.ToString(CultureInfo.InvariantCulture), Numbers.MC);
        }

        private Func<EDecimal, CancellationToken, EDecimal> Primary()
        {
            if (Take('('))
            {
                var result = Sum();
                if (!Take(')')) throw Invalid("Missing closing parenthesis");
                return result;
            }
            Space();
            int start = position;
            if (position < text.Length && (char.IsLetter(text[position]) || text[position] == 'π'))
            {
                while (position < text.Length && (char.IsLetter(text[position]) || text[position] == 'π')) position++;
                Token();
                string name = text[start..position].ToLowerInvariant();
                if (name is "pi" or "π") return (_, _) => Numbers.Approximate(Math.PI);
                if (name == "e") return (_, _) => Numbers.Approximate(Math.E);
                if (allowX && name == "x") return (x, _) => x;
                if (!Functions.Contains(name)) throw Invalid("Unknown function: " + name);
                if (!Take('(')) throw Invalid("Function requires parentheses");
                var argument = Sum();
                if (!Take(')')) throw Invalid("Missing closing parenthesis");
                return (x, token) => Function(name, argument(x, token));
            }
            while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.')) position++;
            if (position < text.Length && text[position] is 'e' or 'E')
            {
                position++;
                if (position < text.Length && text[position] is '+' or '-') position++;
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (start == position) throw Invalid("Expected a number at position " + (position + 1));
            Token();
            EDecimal literal = Numbers.Parse(text[start..position]);
            return (_, _) => literal;
        }

        private EDecimal Function(string name, EDecimal value)
        {
            if (name == "sqrt")
            {
                if (value.Sign < 0) throw Invalid("Square root requires a nonnegative number");
                return value.Sqrt(Numbers.MC);
            }
            if (name == "abs") return value.Abs();
            double input = Numbers.ToDouble(value);
            double radians = angle == AngleUnit.DEG ? input * (Math.PI / 180.0) : input;
            if (name is "sin" or "cos" or "tan" && radians == 0 && input != 0)
                throw Invalid("Angle is below the supported floating-point range");
            return name switch
            {
                "sin" => Numbers.Approximate(Math.Sin(radians)),
                "cos" => Numbers.Approximate(Math.Cos(radians)),
                "tan" => Math.Abs(Math.Cos(radians)) < 1e-15
                    ? throw Invalid("Tangent is undefined at this angle") : Numbers.Approximate(Math.Tan(radians)),
                "asin" => Inverse(Math.Asin(input)),
                "acos" => Inverse(Math.Acos(input)),
                "atan" => Inverse(Math.Atan(input)),
                "ln" => Numbers.Approximate(Math.Log(input)),
                "log" => Numbers.Approximate(Math.Log10(input)),
                _ => throw Invalid("Unknown function: " + name)
            };
        }

        private EDecimal Inverse(double radians) => Numbers.Approximate(angle == AngleUnit.DEG ? radians * (180.0 / Math.PI) : radians);
    }
}
