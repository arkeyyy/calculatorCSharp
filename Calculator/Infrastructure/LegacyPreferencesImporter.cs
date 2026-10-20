using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using Calculator.Core;
using Microsoft.Win32;

namespace Calculator.Infrastructure;

/// <summary>Reads an unambiguous compatible calculator profile without modifying the source.</summary>
public static class LegacyPreferencesImporter
{
    private static readonly string[] Keys = ["mode", "theme", "accent", "angle", "base", "wordSize", "currency.from", "currency.to", "currency.snapshot"];

    public static IReadOnlyDictionary<string, string> ReadUniqueCandidate()
    {
        var candidates = new List<IReadOnlyDictionary<string, string>>();
        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey(@"Software\JavaSoft\Prefs\com\example", writable: false);
            if (parent is null) return new Dictionary<string, string>();
            foreach (string child in parent.GetSubKeyNames())
            {
                using var profile = parent.OpenSubKey(child + @"\v2", writable: false);
                if (profile is null) continue;
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string key in Keys)
                {
                    string storedKey = string.Concat(key.Select(c => c is >= 'A' and <= 'Z' ? "/" + c : c.ToString()));
                    if (profile.GetValue(storedKey, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string raw) continue;
                    values[storedKey] = raw;
                }
                candidates.Add(values);
            }
        }
        catch (Exception error) when (error is SecurityException or UnauthorizedAccessException or IOException)
        { Trace.TraceWarning("Legacy calculator settings could not be read: {0}", error.Message); }
        return SelectUniqueProfile(candidates);
    }

    public static IReadOnlyDictionary<string, string> SelectUniqueProfile(IEnumerable<IReadOnlyDictionary<string, string>> encodedProfiles)
    {
        var candidates = new List<Dictionary<string, string>>();
        foreach (var profile in encodedProfiles)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in Keys)
            {
                string storedKey = string.Concat(key.Select(c => c is >= 'A' and <= 'Z' ? "/" + c : c.ToString()));
                if (!profile.TryGetValue(storedKey, out string? raw)) continue;
                try
                {
                    string decoded = DecodeValue(raw);
                    if (IsValidSetting(key, decoded)) values[key] = decoded;
                }
                catch (FormatException) { }
            }
            if (values.ContainsKey("mode") && values.ContainsKey("theme") &&
                (values.ContainsKey("wordSize") || values.ContainsKey("currency.snapshot") || values.ContainsKey("currency.from"))) candidates.Add(values);
        }
        return candidates.Count == 1 ? candidates[0] : new Dictionary<string, string>();
    }

    public static string DecodeValue(string encoded)
    {
        var decoded = new StringBuilder();
        for (int i = 0; i < encoded.Length; i++)
        {
            char current = encoded[i];
            if (current == '\\') { decoded.Append('/'); continue; }
            if (current == '/' && i + 1 < encoded.Length)
            {
                char next = encoded[i + 1];
                if (next == 'u')
                {
                    if (i + 5 >= encoded.Length || !ushort.TryParse(encoded.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort character))
                        throw new FormatException("Invalid preference escape.");
                    decoded.Append((char)character);
                    i += 5;
                    continue;
                }
                if (next is >= 'A' and <= 'Z') { decoded.Append(next); i++; continue; }
                if (next == '/') { decoded.Append('\\'); i++; continue; }
            }
            decoded.Append(current);
        }
        return decoded.ToString();
    }

    public static bool IsValidSetting(string key, string value)
    {
        switch (key)
        {
            case "mode": return Enum.TryParse<ModeId>(value, out var mode) && Enum.IsDefined(mode) && mode.ToString() == value;
            case "theme": return value is "light" or "dark";
            case "accent": return value is "BLUE" or "PURPLE" or "PINK" or "TEAL" or "YELLOW" or "CORAL";
            case "angle": return value is "DEG" or "RAD";
            case "base": return value is "BIN" or "OCT" or "DEC" or "HEX";
            case "wordSize": return value is "BYTE" or "WORD" or "DWORD" or "QWORD";
            case "currency.from": case "currency.to": return CurrencyPresentation.IsRecognizedCode(value);
            case "currency.snapshot":
                try { CurrencyRateSnapshot.Decode(value); return true; }
                catch (Exception error) when (error is ArgumentException or ArithmeticException or FormatException) { return false; }
            default: return value.Length <= 8192;
        }
    }
}
