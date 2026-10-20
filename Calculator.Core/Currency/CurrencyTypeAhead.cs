namespace Calculator.Core;

public sealed class CurrencyTypeAhead
{
    public const long TimeoutMilliseconds = 1000;
    private string prefix = "";
    private long lastTyped;

    public string? Find(IReadOnlyList<string> codes, string? previewCode, string characters, long nowMilliseconds)
    {
        string typed = characters.ToLowerInvariant();
        if (typed.Length == 0 || !typed.All(char.IsLetter)) return previewCode;
        bool continuing = prefix.Length != 0 && nowMilliseconds >= lastTyped && nowMilliseconds - lastTyped < TimeoutMilliseconds;
        bool cycling = continuing && prefix.Length == 1 && prefix == typed;
        prefix = continuing && !cycling ? prefix + typed : typed;
        lastTyped = nowMilliseconds;
        var entries = codes.Select(CurrencyPresentation.ForCode).ToArray();
        var matches = Match(entries, item => item.Region);
        if (matches.Count == 0) matches = Match(entries, item => item.Name);
        if (matches.Count == 0) matches = Match(entries, item => item.Code);
        if (matches.Count == 0) return previewCode;
        int current = Array.FindIndex(entries, item => item.Code == previewCode);
        if (cycling)
            foreach (int index in matches)
                if (index > current) return entries[index].Code;
        return entries[matches[0]].Code;
    }

    private List<int> Match(IReadOnlyList<CurrencyPresentation> entries, Func<CurrencyPresentation, string> text) =>
        Enumerable.Range(0, entries.Count).Where(index => text(entries[index]).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

    public void Reset() { prefix = ""; lastTyped = 0; }
}
