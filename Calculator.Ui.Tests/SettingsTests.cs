using Calculator.Infrastructure;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    public void JavaRegistryEscapesAndCandidateAmbiguityAreHandledWithoutRegistryWrites()
    {
        Assert.AreEqual("CORAL", LegacyPreferencesImporter.DecodeValue("/C/O/R/A/L"));
        Assert.AreEqual("a\\b/c\n", LegacyPreferencesImporter.DecodeValue("a//b\\c/u000a"));
        Assert.ThrowsException<FormatException>(() => LegacyPreferencesImporter.DecodeValue("/u00"));
        var encoded = new Dictionary<string, string>
        {
            ["mode"] = "/M/A/S/S", ["theme"] = "dark", ["accent"] = "/C/O/R/A/L",
            ["word/Size"] = "/Q/W/O/R/D", ["currency.from"] = "/C/N/Y", ["currency.to"] = "/P/H/P",
            ["currency.snapshot"] = "2020-01-02/u000a/E/U/R=1/u000a/U/S/D=1.2",
            ["width"] = "900"
        };
        var profile = LegacyPreferencesImporter.SelectUniqueProfile([encoded]);
        Assert.AreEqual("MASS", profile["mode"]);
        Assert.AreEqual("QWORD", profile["wordSize"]);
        Assert.AreEqual("2020-01-02\nEUR=1\nUSD=1.2", profile["currency.snapshot"]);
        Assert.IsFalse(profile.ContainsKey("width"));
        Assert.AreEqual(0, LegacyPreferencesImporter.SelectUniqueProfile([encoded, encoded]).Count);
        Assert.AreEqual(7, LegacyPreferencesImporter.SelectUniqueProfile([encoded, new Dictionary<string, string> { ["theme"] = "light" }]).Count);
        Assert.AreEqual("/M/A/S/S", encoded["mode"], "Import must never modify the source values.");
    }

    [TestMethod]
    public void FirstLaunchImportIsPersistentOnceAndExistingCSharpValuesWin()
    {
        InDirectory(directory =>
        {
            var existing = new SettingsStore(directory, importLegacy: false);
            existing.Put("theme", "dark");
            int imports = 0;
            IReadOnlyDictionary<string, string> Read()
            {
                imports++;
                return new Dictionary<string, string> { ["theme"] = "light", ["mode"] = "STORAGE", ["accent"] = "TEAL" };
            }
            var imported = new SettingsStore(directory, legacyReader: Read);
            Assert.AreEqual("dark", imported.Get("theme", "light"));
            Assert.AreEqual("STORAGE", imported.Get("mode", "STANDARD"));
            var reopened = new SettingsStore(directory, legacyReader: Read);
            Assert.AreEqual(1, imports);
            Assert.AreEqual("TEAL", reopened.Get("accent", "BLUE"));
            reopened.Put("mode", "DATE");
            Assert.AreEqual("DATE", new SettingsStore(directory, importLegacy: false).Get("mode", "STANDARD"), "Put must save preferences without requiring orderly shutdown.");
        });
    }

    [TestMethod]
    public void NullInvalidAndFutureSettingsKeepSafeDefaultsAndFutureDataUntouched()
    {
        InDirectory(directory =>
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"Version\":1,\"Values\":null}");
            Assert.AreEqual("STANDARD", new SettingsStore(directory, false).Get("mode", "STANDARD"));
            File.WriteAllText(path, "{\"Version\":1,\"Values\":{\"mode\":\"unknown\",\"accent\":\"removed\",\"theme\":null,\"currency.snapshot\":\"invalid\"}}");
            var invalid = new SettingsStore(directory, false);
            Assert.AreEqual("STANDARD", invalid.Get("mode", "STANDARD"));
            Assert.AreEqual("BLUE", invalid.Get("accent", "BLUE"));
            Assert.AreEqual("light", invalid.Get("theme", "light"));
            Assert.AreEqual("", invalid.Get("currency.snapshot", ""));
            const string future = "{\"Version\":99,\"Values\":{\"mode\":\"FUTURE\"},\"Extra\":123}";
            File.WriteAllText(path, future);
            var unsupported = new SettingsStore(directory, legacyReader: () => throw new AssertFailedException("Future settings must not trigger migration."));
            unsupported.Put("mode", "DATE");
            Assert.AreEqual("DATE", unsupported.Get("mode", "STANDARD"));
            Assert.AreEqual(future, File.ReadAllText(path));
        });
    }

    private static void InDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CalculatorTests", Guid.NewGuid().ToString("N"));
        try { action(directory); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
