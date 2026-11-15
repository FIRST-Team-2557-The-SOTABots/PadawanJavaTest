using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FrcResearchTest;

public sealed class AppConfig
{
    public int TimeLimitMinutes { get; set; } = 30;
    public bool Kiosk { get; set; } = true;
    public bool ShowScoreOnSubmit { get; set; } = false;
    public string HomeUrl { get; set; } = "https://www.google.com/";
    public string SearchEngineUrl { get; set; } = "https://www.google.com/search?q={query}&udm=14";
    public string ResultsFolder { get; set; } = "";
    public string MentorPinSalt { get; set; } = "";
    public string MentorPinHash { get; set; } = "";
    public int ResearchPassCorrect { get; set; } = 8;
    public List<string> AllowedCitationDomains { get; set; } = new();
    public List<string> BlockedDomains { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string EffectiveResultsFolder =>
        string.IsNullOrWhiteSpace(ResultsFolder)
            ? AppPaths.DefaultResultsFolder
            : Environment.ExpandEnvironmentVariables(ResultsFolder);

    public static AppConfig Load()
    {
        if (!File.Exists(AppPaths.ConfigPath))
        {
            var fresh = CreateDefault();
            fresh.Save();
            return fresh;
        }

        var json = File.ReadAllText(AppPaths.ConfigPath);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        cfg.AllowedCitationDomains ??= new List<string>();
        cfg.BlockedDomains ??= new List<string>();
        if (cfg.TimeLimitMinutes <= 0) cfg.TimeLimitMinutes = 30;
        return cfg;
    }

    public static AppConfig CreateDefault() => new()
    {
        AllowedCitationDomains = new List<string>
        {
            "wpilib.org",           // docs.wpilib.org and github.wpilib.org (API reference)
            "ctre-phoenix.com",     // Phoenix 6 documentation
            "revrobotics.com",      // REVLib docs and API reference
            "github.com",
            "github.io",
            "firstinspires.org",
            "chiefdelphi.com",
        },
        BlockedDomains = new List<string>
        {
            "chatgpt.com",
            "chat.openai.com",
            "platform.openai.com",
            "claude.ai",
            "gemini.google.com",
            "bard.google.com",
            "aistudio.google.com",
            "notebooklm.google.com",
            "copilot.microsoft.com",
            "perplexity.ai",
            "phind.com",
            "you.com",
            "poe.com",
            "grok.com",
            "x.ai",
            "deepseek.com",
            "chat.mistral.ai",
            "openrouter.ai",
            "huggingface.co",
            "character.ai",
            "pi.ai",
        },
    };

    public void Save() =>
        File.WriteAllText(AppPaths.ConfigPath, JsonSerializer.Serialize(this, JsonOptions));

    public bool HasMentorPin =>
        !string.IsNullOrWhiteSpace(MentorPinHash) && !string.IsNullOrWhiteSpace(MentorPinSalt);

    public void SetMentorPin(string pin)
    {
        MentorPinSalt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        MentorPinHash = HashPin(pin, MentorPinSalt);
        Save();
    }

    public bool VerifyMentorPin(string pin)
    {
        if (!HasMentorPin) return false;
        var candidate = HashPin(pin, MentorPinSalt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(candidate),
            Encoding.UTF8.GetBytes(MentorPinHash));
    }

    public static string HashPin(string pin, string salt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + pin)));
}

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string ConfigPath => Path.Combine(BaseDirectory, "config.json");

    public static string QuestionsPath => Path.Combine(BaseDirectory, "questions.json");

    public static string LocalDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FRCResearchTest");

    public static string ProfilesDirectory => Path.Combine(LocalDataDirectory, "profiles");

    public static string DefaultResultsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FRC Research Test",
        "results");
}

public static class NameUtil
{
    public static string Safe(string? value, string fallback = "student")
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim()
            .Select(c => invalid.Contains(c) || c == ' ' ? '-' : c)
            .ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        cleaned = cleaned.Trim('-', '.');
        return cleaned.Length == 0 ? fallback : cleaned;
    }
}
