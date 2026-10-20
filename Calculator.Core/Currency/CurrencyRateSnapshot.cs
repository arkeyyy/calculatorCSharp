using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using PeterO.Numbers;

namespace Calculator.Core;

public sealed class CurrencyRateSnapshot
{
    public const int MaximumEncodedLength = 8192;
    public DateOnly Date { get; }
    public IReadOnlyDictionary<string, EDecimal> Rates { get; }

    public CurrencyRateSnapshot(DateOnly date, IReadOnlyDictionary<string, EDecimal> rates, DateOnly? today = null)
    {
        if (date > (today ?? DateOnly.FromDateTime(DateTime.Now)) || rates.Count is < 2 or > 100)
            throw new ArgumentException("Invalid rate snapshot.");
        var copy = new SortedDictionary<string, EDecimal>(StringComparer.Ordinal);
        foreach (var (code, rate) in rates)
        {
            if (!CurrencyPresentation.IsRecognizedCode(code) || !rate.IsFinite || rate.Sign <= 0 ||
                rate.Precision().CompareTo(EInteger.FromInt32(34)) > 0 || rate.Exponent.Abs().CompareTo(EInteger.FromInt32(12)) > 0)
                throw new ArgumentException("Invalid currency code or rate.");
            copy.Add(code, rate);
        }
        if (!copy.TryGetValue("EUR", out var euro) || euro.CompareTo(EDecimal.One) != 0)
            throw new ArgumentException("EUR must have rate 1.");
        Date = date;
        Rates = new ReadOnlyDictionary<string, EDecimal>(copy);
    }

    public EDecimal Convert(EDecimal amount, string from, string to)
    {
        if (!Rates.TryGetValue(from, out var source) || !Rates.TryGetValue(to, out var target))
            throw new ArgumentException("Currency is unavailable in this snapshot.");
        return Numbers.Checked(Numbers.Checked(amount).Multiply(target, Numbers.MC).Divide(source, Numbers.MC));
    }

    public string Encode()
    {
        var text = new StringBuilder(Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var rate in Rates.OrderBy(item => item.Key, StringComparer.Ordinal))
            text.Append('\n').Append(rate.Key).Append('=').Append(rate.Value.ToPlainString());
        if (text.Length > MaximumEncodedLength) throw new ArgumentException("Snapshot is too large to save.");
        return text.ToString();
    }

    public static CurrencyRateSnapshot Decode(string text, DateOnly? today = null)
    {
        if (text.Length > MaximumEncodedLength) throw new ArgumentException("Snapshot is too large.");
        string[] lines = text.Split('\n');
        if (!DateOnly.TryParseExact(lines[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ArgumentException("Invalid snapshot date.");
        var rates = new Dictionary<string, EDecimal>(StringComparer.Ordinal);
        foreach (string line in lines.Skip(1))
        {
            string[] pair = line.Split('=');
            if (pair.Length != 2 || !rates.TryAdd(pair[0], ParseRate(pair[1])))
                throw new ArgumentException("Invalid or duplicate cached rate.");
        }
        return new(date, rates, today);
    }

    public static EDecimal ParseRate(string text)
    {
        if (text.Length == 0 || text.Length > 128 || text.Any(char.IsWhiteSpace)) throw new ArgumentException("Invalid currency rate.");
        try { return EDecimal.FromString(text); }
        catch (FormatException error) { throw new ArgumentException("Invalid currency rate.", error); }
    }
}
