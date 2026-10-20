using System.Globalization;
using System.Numerics;

namespace Calculator.Core;

public enum NumberBase { BIN = 2, OCT = 8, DEC = 10, HEX = 16 }
public enum WordSize { BYTE = 8, WORD = 16, DWORD = 32, QWORD = 64 }
public enum BitOperation { ADD, SUBTRACT, MULTIPLY, DIVIDE, MOD, AND, OR, XOR, LEFT, RIGHT, UNSIGNED_RIGHT }

/// <summary>Signed fixed-word arithmetic and the immediate-execution programmer entry state.</summary>
public sealed class ProgrammerEngine
{
    private BigInteger left;
    private BitOperation? pending;
    private bool replace = true;
    public NumberBase Base { get; private set; } = NumberBase.DEC;
    public WordSize Size { get; private set; } = WordSize.QWORD;
    public BigInteger Value { get; private set; }
    public string Display => Error ?? Format(Base);
    public string Expression { get; private set; } = "";
    public string? Error { get; private set; }
    public event Action<string, string>? Calculated;

    public void Configure(NumberBase numberBase, WordSize wordSize)
    {
        if (!Enum.IsDefined(numberBase) || !Enum.IsDefined(wordSize)) throw new ArgumentOutOfRangeException();
        if (Error is not null) Clear();
        Base = numberBase;
        Size = wordSize;
        Value = Signed(Value);
        left = Signed(left);
        if (pending.HasValue) Expression = Format(left, Base) + " " + pending.Value;
    }

    public BigInteger Unsigned(BigInteger value) => value & ((BigInteger.One << (int)Size) - 1);
    public BigInteger Signed(BigInteger value)
    {
        var masked = Unsigned(value);
        return (masked & (BigInteger.One << ((int)Size - 1))) != 0
            ? masked - (BigInteger.One << (int)Size) : masked;
    }

    public BigInteger Parse(string text, NumberBase numberBase)
    {
        if (text is null || text.Length is 0 or > 128) throw new CalculationException("Enter a valid integer");
        bool negative = text[0] == '-';
        int start = negative ? 1 : 0;
        if (start == text.Length) throw new CalculationException("Enter a valid integer");
        BigInteger value = BigInteger.Zero;
        for (int i = start; i < text.Length; i++)
        {
            int digit = DigitValue(text[i]);
            if (digit < 0 || digit >= (int)numberBase) throw new CalculationException("Digit is not valid in " + numberBase);
            value = value * (int)numberBase + digit;
        }
        return Signed(negative ? -value : value);
    }

    public string Format(NumberBase numberBase) => Format(Value, numberBase);
    public string Format(BigInteger value, NumberBase numberBase)
    {
        if (numberBase == NumberBase.DEC) return Signed(value).ToString(CultureInfo.InvariantCulture);
        value = Unsigned(value);
        if (value.IsZero) return "0";
        Span<char> characters = stackalloc char[64];
        int index = characters.Length;
        while (value > 0)
        {
            value = BigInteger.DivRem(value, (int)numberBase, out BigInteger digit);
            characters[--index] = "0123456789ABCDEF"[(int)digit];
        }
        return new string(characters[index..]);
    }

    public BigInteger Apply(BitOperation operation, BigInteger a, BigInteger b)
    {
        a = Signed(a);
        b = Signed(b);
        if (operation is BitOperation.DIVIDE or BitOperation.MOD && b.IsZero)
            throw new CalculationException("Cannot divide by zero", CalculationError.DivideByZero);
        if (operation is BitOperation.LEFT or BitOperation.RIGHT or BitOperation.UNSIGNED_RIGHT && (b < 0 || b >= (int)Size))
            throw new CalculationException($"Shift count must be 0–{(int)Size - 1}");
        return Signed(operation switch
        {
            BitOperation.ADD => a + b,
            BitOperation.SUBTRACT => a - b,
            BitOperation.MULTIPLY => a * b,
            BitOperation.DIVIDE => a / b,
            BitOperation.MOD => a % b,
            BitOperation.AND => a & b,
            BitOperation.OR => a | b,
            BitOperation.XOR => a ^ b,
            BitOperation.LEFT => a << (int)b,
            BitOperation.RIGHT => a >> (int)b,
            BitOperation.UNSIGNED_RIGHT => Unsigned(a) >> (int)b,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        });
    }

    public BigInteger Not(BigInteger value) => Signed(~value);
    public BigInteger Toggle(BigInteger value, int bit)
    {
        if (bit < 0 || bit >= (int)Size) throw new CalculationException("Bit is outside the word size");
        return Signed(Unsigned(value) ^ (BigInteger.One << bit));
    }

    public void ToggleBit(int bit)
    {
        if (Error is not null) Clear();
        Value = Toggle(Value, bit);
        replace = false;
    }

    public void Paste(string text)
    {
        try { Value = Parse(text.Trim(), Base); Error = null; replace = false; }
        catch (CalculationException error) { Error = error.Message; }
    }

    public void Recall(string decimalValue)
    {
        if (!BigInteger.TryParse(decimalValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger number)) return;
        Value = Signed(number);
        Error = null;
        replace = false;
    }

    public void Press(string key)
    {
        try
        {
            if (key is "Clear" or "AC") { Clear(); return; }
            if (key.Length == 1 && DigitValue(key[0]) is int digit && digit >= 0)
            {
                if (digit >= (int)Base) return;
                if (Error is not null) Clear();
                string entry = replace ? "" : Format(Base);
                Value = Parse(entry == "0" ? key : entry + key, Base);
                replace = false;
                return;
            }
            switch (key)
            {
                case "CE": Value = 0; replace = false; Error = null; return;
                case "⌫": case "Backspace":
                    if (replace || Error is not null) return;
                    string text = Format(Base);
                    text = text.Length > 1 ? text[..^1] : "0";
                    Value = text == "-" ? 0 : Parse(text, Base);
                    return;
            }
            if (Error is not null) return;
            if (key is "NOT" or "Not") { Value = Not(Value); replace = false; return; }
            if (key is "±" or "+/-") { Value = Signed(-Value); replace = false; return; }
            if (key is "=" or "Enter")
            {
                if (!pending.HasValue) return;
                Expression = $"{Format(left, Base)} {pending.Value} {Format(Base)} = ({Base}, {(int)Size}-bit)";
                Value = Apply(pending.Value, left, Value);
                pending = null;
                replace = true;
                Calculated?.Invoke(Expression, Value.ToString(CultureInfo.InvariantCulture));
                return;
            }
            BitOperation? next = key switch
            {
                "+" => BitOperation.ADD, "-" or "−" => BitOperation.SUBTRACT,
                "*" or "×" => BitOperation.MULTIPLY, "/" or "÷" => BitOperation.DIVIDE,
                "%" or "mod" or "MOD" => BitOperation.MOD,
                "AND" => BitOperation.AND, "OR" => BitOperation.OR, "XOR" => BitOperation.XOR,
                "<<" or "Lsh" or "LEFT" => BitOperation.LEFT,
                ">>" or "Rsh" or "RIGHT" => BitOperation.RIGHT,
                ">>>" or "UNSIGNED_RIGHT" => BitOperation.UNSIGNED_RIGHT,
                _ => null
            };
            if (!next.HasValue) return;
            if (pending.HasValue && !replace) Value = Apply(pending.Value, left, Value);
            left = Value;
            pending = next;
            replace = true;
            Expression = Format(left, Base) + " " + next.Value;
        }
        catch (CalculationException error) { Error = error.Message; }
    }

    public void Clear()
    {
        Value = left = BigInteger.Zero;
        pending = null;
        replace = true;
        Expression = "";
        Error = null;
    }

    public static int DigitValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'A' and <= 'F' => character - 'A' + 10,
        >= 'a' and <= 'f' => character - 'a' + 10,
        _ => -1
    };
}
