using PeterO.Numbers;

namespace Calculator.Core;

public sealed class ConverterSession
{
    private readonly NumericEntry[] entries = [new("1"), new("")];
    private readonly Func<EDecimal, bool, EDecimal?> convert;
    public int ActiveSide { get; private set; }
    public int SourceSide { get; private set; }
    public string Error { get; private set; } = "";
    public string RawSource => entries[SourceSide].Text;
    public string Result => entries[1 - SourceSide].Text;
    public NumericEntry Entry(int side) => entries[side];
    public string Text(int side) => entries[side].Text;

    public ConverterSession(Func<EDecimal, bool, EDecimal?> convert) => this.convert = convert;

    public void Activate(int side, bool selectAll = true)
    {
        if (side is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(side));
        ActiveSide = side;
        if (selectAll) entries[side].Select(0, entries[side].Text.Length);
    }

    public bool TrySetText(int side, string text)
    {
        if (!entries[side].SetText(text)) return false;
        ActiveSide = SourceSide = side;
        Update();
        return true;
    }

    public bool Press(string key)
    {
        var entry = entries[ActiveSide];
        long revision = entry.Revision;
        if (!entry.Press(key)) return false;
        if (entry.Revision != revision)
        {
            SourceSide = ActiveSide;
            Update();
        }
        return true;
    }

    public void Paste(string text) => TrySetText(ActiveSide, text.Trim());

    public void Update()
    {
        string raw = RawSource.Trim(), result = "";
        Error = "";
        if (raw is not ("" or "-" or "+" or "." or "-." or "+."))
        {
            try
            {
                var value = convert(Numbers.Parse(raw), SourceSide == 1);
                if (value is not null) result = Numbers.Format(value);
            }
            catch (Exception error) when (error is CalculationException or ArithmeticException or ArgumentException or FormatException)
            {
                Error = error.Message;
            }
        }
        int opposite = 1 - SourceSide;
        entries[opposite].SetText(result);
        if (ActiveSide == opposite) entries[opposite].Select(0, result.Length);
    }

    /// <summary>The caller exchanges unit identifiers first; quantities keep their authority and selection.</summary>
    public void Swap()
    {
        (entries[0], entries[1]) = (entries[1], entries[0]);
        SourceSide = 1 - SourceSide;
        ActiveSide = 1 - ActiveSide;
    }
}
