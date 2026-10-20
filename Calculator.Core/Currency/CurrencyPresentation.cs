using System.Globalization;

namespace Calculator.Core;

public sealed record CurrencyPresentation(string Region, string Name, string Code)
{
    public string Label => (Region == Name ? Name : $"{Region} — {Name}") + $" ({Code})";
    public override string ToString() => Label;
    private static readonly Dictionary<string, CurrencyPresentation> Known = BuildKnown();
    private static readonly Dictionary<string, string> EnglishNames = BuildNames();
    private static readonly HashSet<string> HistoricCodes = new(("ADP AFA ATS AYM AZM BEF BGL BGN BOV BYB BYR CHE CHW CLF COU CYP CSD CUC DEM EEK ESP FIM FRF GHC GRD GWP HRK IEP ITL LTL LUF LVL MGF MRO MTL MZM NLG PEI PLZ PTE ROL RUR SDD SIT SKK SRG STD SUR TMM TPE TRL UAK USS USN UYI UYW VEB VEF VED VNN XAD XAG XAU XBA XBB XBC XBD XDR XFO XFU XPD XPT XSU XTS XUA XXX YUM ZMK ZWD ZWL ZWN ZWR").Split(' '), StringComparer.Ordinal);

    public static bool IsRecognizedCode(string code) => code.Length == 3 && code.All(c => c is >= 'A' and <= 'Z') &&
        (Known.ContainsKey(code) || EnglishNames.ContainsKey(code) || HistoricCodes.Contains(code));

    public static CurrencyPresentation ForCode(string code)
    {
        if (Known.TryGetValue(code, out var presentation)) return presentation;
        string name = EnglishNames.GetValueOrDefault(code, code);
        return new(name, name, code);
    }

    public static IReadOnlyList<string> SortedCodes(IEnumerable<string> codes) => codes.Select(ForCode)
        .OrderBy(item => item.Region, StringComparer.OrdinalIgnoreCase)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(item => item.Code, StringComparer.Ordinal).Select(item => item.Code).ToArray();

    private static Dictionary<string, string> BuildNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                names.TryAdd(region.ISOCurrencySymbol, region.CurrencyEnglishName);
            }
            catch (ArgumentException) { }
        }
        return names;
    }

    private static Dictionary<string, CurrencyPresentation> BuildKnown()
    {
        const string rows = """
AUD|Australia|Australian dollar
BGN|Bulgaria|Bulgarian lev
BRL|Brazil|Brazilian real
CAD|Canada|Canadian dollar
CHF|Switzerland|Swiss franc
CNY|China|Chinese yuan
CYP|Cyprus|Cypriot pound
CZK|Czechia|Czech koruna
DKK|Denmark|Danish krone
EEK|Estonia|Estonian kroon
EUR|Euro area|Euro
GBP|United Kingdom|Pound sterling
GRD|Greece|Greek drachma
HKD|Hong Kong|Hong Kong dollar
HRK|Croatia|Croatian kuna
HUF|Hungary|Hungarian forint
IDR|Indonesia|Indonesian rupiah
ILS|Israel|Israeli shekel
INR|India|Indian rupee
ISK|Iceland|Icelandic krona
JPY|Japan|Japanese yen
KRW|South Korea|South Korean won
LTL|Lithuania|Lithuanian litas
LVL|Latvia|Latvian lats
MTL|Malta|Maltese lira
MXN|Mexico|Mexican peso
MYR|Malaysia|Malaysian ringgit
NOK|Norway|Norwegian krone
NZD|New Zealand|New Zealand dollar
PHP|Philippines|Philippine peso
PLN|Poland|Polish zloty
ROL|Romania|Old Romanian leu
RON|Romania|Romanian leu
RUB|Russia|Russian rouble
SEK|Sweden|Swedish krona
SGD|Singapore|Singapore dollar
SIT|Slovenia|Slovenian tolar
SKK|Slovakia|Slovak koruna
THB|Thailand|Thai baht
TRL|Turkey|Old Turkish lira
TRY|Turkey|Turkish lira
USD|United States|US dollar
ZAR|South Africa|South African rand
""";
        return rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.Trim().Split('|'))
            .ToDictionary(parts => parts[0], parts => new CurrencyPresentation(parts[1], parts[2], parts[0]), StringComparer.Ordinal);
    }
}
