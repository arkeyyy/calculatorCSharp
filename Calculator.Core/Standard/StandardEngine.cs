using PeterO.Numbers;

namespace Calculator.Core;

/// <summary>Immediate arithmetic with raw entry, accumulator and repeated equals kept separately.</summary>
public sealed class StandardEngine
{
    private string entry = "0";
    private string expression = "";
    private EDecimal entryValue = EDecimal.Zero;
    private EDecimal accumulator = EDecimal.Zero;
    private EDecimal? repeatOperand;
    private string? pending;
    private string? repeatOperation;
    private bool replaceEntry = true;
    private bool editable = true;
    private CalculationException? error;

    public string Display => error?.Message ?? entry;
    public string Expression => expression;
    public EDecimal Value => error is null ? entryValue : EDecimal.Zero;
    public bool Error => error is not null;
    public CalculationError? ErrorCode => error?.Code;
    public bool Completed { get; private set; }

    public void Press(string key)
    {
        Completed = false;
        try
        {
            if (key is "C" or "AC") Reset();
            else if (key.Length == 1 && key[0] is >= '0' and <= '9')
            {
                if (Error) Reset();
                BeginEntry();
                if (entry.Length < 34) SetEntry(entry == "0" ? key : entry + key);
                repeatOperation = null;
            }
            else if (!Error)
            {
                string operation = key switch { "−" => "-", "×" => "*", "÷" => "/", _ => key };
                if (operation is "+" or "-" or "*" or "/") Binary(operation);
                else ApplyKey(key);
            }
        }
        catch (CalculationException exception) { error = exception; }
        catch (ArithmeticException) { error = new CalculationException("Invalid input", CalculationError.Domain); }
    }

    public void Recall(EDecimal value)
    {
        Completed = false;
        try
        {
            if (Error) Reset();
            SetValue(value);
            replaceEntry = false;
            repeatOperation = null;
        }
        catch (CalculationException exception) { error = exception; }
    }

    private void Binary(string operation)
    {
        if (pending is not null && !replaceEntry) accumulator = Calculate(pending, accumulator, entryValue);
        else if (pending is null) accumulator = entryValue;
        SetValue(accumulator);
        pending = operation;
        repeatOperation = null;
        expression = entry + " " + Symbol(operation);
        replaceEntry = true;
    }

    private void ApplyKey(string key)
    {
        EDecimal value = entryValue;
        switch (key)
        {
            case ".":
                BeginEntry();
                if (!entry.Contains('.')) SetEntry(entry + ".");
                repeatOperation = null;
                break;
            case "CE":
                SetEntry("0");
                replaceEntry = false;
                break;
            case "⌫":
            case "Backspace":
                if (!replaceEntry && editable)
                {
                    string shortened = entry.Length > 1 ? entry[..^1] : "0";
                    SetEntry(shortened == "-" ? "0" : shortened);
                }
                break;
            case "±":
                if (editable && !replaceEntry) SetEntry(entry.StartsWith('-') ? entry[1..] : "-" + entry);
                else SetValue(value.Negate());
                replaceEntry = false;
                break;
            case "%":
                SetValue((pending is "+" or "-" ? accumulator.Multiply(value, Numbers.MC) : value).ScaleByPowerOfTen(-2));
                replaceEntry = false;
                break;
            case "1/x":
                Unary(Numbers.Divide(EDecimal.One, value), "1/(" + entry + ")");
                break;
            case "x²":
                Unary(Numbers.Multiply(value, value), "sqr(" + entry + ")");
                break;
            case "√":
                if (value.Sign < 0) throw new CalculationException("Square root requires a nonnegative number", CalculationError.Domain);
                Unary(value.Sqrt(Numbers.MC), "sqrt(" + entry + ")");
                break;
            case "=":
                string? operation = pending ?? repeatOperation;
                if (operation is not null)
                {
                    EDecimal left = pending is not null ? accumulator : value;
                    EDecimal right = pending is not null ? value : repeatOperand!;
                    EDecimal result = Calculate(operation, left, right);
                    expression = Numbers.Format(left) + " " + Symbol(operation) + " " + Numbers.Format(right) + " =";
                    SetValue(result);
                    accumulator = result;
                    repeatOperation = operation;
                    repeatOperand = right;
                    pending = null;
                    Completed = true;
                }
                replaceEntry = true;
                break;
        }
    }

    private void Unary(EDecimal result, string label)
    {
        SetValue(result);
        expression = label;
        replaceEntry = false;
        repeatOperation = null;
    }

    private void BeginEntry()
    {
        if (replaceEntry || !editable)
        {
            SetEntry("0");
            replaceEntry = false;
            if (pending is null) expression = "";
        }
    }

    private void Reset()
    {
        SetEntry("0");
        expression = "";
        accumulator = EDecimal.Zero;
        pending = repeatOperation = null;
        repeatOperand = null;
        replaceEntry = true;
        error = null;
        Completed = false;
    }

    private void SetEntry(string input)
    {
        EDecimal parsed = Numbers.Parse(input);
        entry = input;
        entryValue = parsed;
        editable = true;
    }

    private void SetValue(EDecimal value)
    {
        EDecimal checkedValue = Numbers.Checked(value.RoundToPrecision(Numbers.MC));
        string formatted = Numbers.Format(checkedValue);
        entryValue = checkedValue;
        entry = formatted;
        editable = false;
    }

    private static string Symbol(string operation) => operation switch { "-" => "−", "*" => "×", "/" => "÷", _ => operation };
    private static EDecimal Calculate(string operation, EDecimal a, EDecimal b) => operation switch
    {
        "+" => Numbers.Add(a, b),
        "-" => Numbers.Subtract(a, b),
        "*" => Numbers.Multiply(a, b),
        "/" => Numbers.Divide(a, b),
        _ => throw new CalculationException("Unknown operation")
    };
}
