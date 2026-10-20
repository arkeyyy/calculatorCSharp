using System.Diagnostics;
using System.Security;
using System.Text.Json;

namespace Calculator.Infrastructure;

public sealed class SettingsStore
{
    private readonly string file;
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    private bool legacyImportCompleted;
    private bool canPersist = true;
    public string StoragePath => file;
    public string? LastError { get; private set; }

    public SettingsStore(string? storageDirectory = null, bool importLegacy = true, Func<IReadOnlyDictionary<string, string>>? legacyReader = null)
    {
        file = Path.Combine(storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Calculator"), "settings.json");
        Load();
        if (importLegacy && !legacyImportCompleted && canPersist)
        {
            foreach (var (key, value) in (legacyReader ?? LegacyPreferencesImporter.ReadUniqueCandidate)()) values.TryAdd(key, value);
            legacyImportCompleted = true;
            Flush();
        }
    }

    public string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
    public void Put(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80 || value is null || !LegacyPreferencesImporter.IsValidSetting(key, value)) return;
        if (values.TryGetValue(key, out string? old) && old == value) return;
        values[key] = value;
        Flush();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(file)) return;
            if (new FileInfo(file).Length > 1_048_576) throw new JsonException("Settings file is too large.");
            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(file));
            if (saved is null) return;
            if (saved.Version != 1)
            {
                canPersist = false;
                LastError = "Settings were created by a different application version; using session defaults.";
                return;
            }
            foreach (var (key, value) in saved.Values ?? [])
                if (!string.IsNullOrWhiteSpace(key) && key.Length <= 80 && value is not null && LegacyPreferencesImporter.IsValidSetting(key, value)) values[key] = value;
            legacyImportCompleted = saved.LegacyImportCompleted;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        { RecordError(error); }
    }

    public void Flush()
    {
        if (!canPersist) return;
        string temporary = file + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var saved = new SavedSettings { Values = new(values), LegacyImportCompleted = legacyImportCompleted };
            File.WriteAllText(temporary, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, file, overwrite: true);
            LastError = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        { RecordError(error); }
    }

    private void RecordError(Exception error)
    {
        LastError = "Settings could not be saved or loaded; changes remain available for this session.";
        Trace.TraceWarning("Calculator settings: {0}", error.Message);
    }

    private sealed class SavedSettings
    {
        public int Version { get; set; } = 1;
        public bool LegacyImportCompleted { get; set; }
        public Dictionary<string, string>? Values { get; set; } = new();
    }
}
