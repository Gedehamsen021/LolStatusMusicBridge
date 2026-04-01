using System.Text.Json;

namespace LolStatusMusicBridge;

internal sealed record AppOptions(
    TimeSpan PollInterval,
    string StatusPrefix,
    int MaxStatusLength,
    string? LeagueLockfilePath,
    IReadOnlyList<string> AllowedSourceApps,
    string ConfigPath)
{
    private const string ConfigFileName = "config.json";
    private const int DefaultPollSeconds = 5;
    private const int DefaultMaxStatusLength = 124;
    private const string DefaultStatusPrefix = "Now playing";
    private static readonly string[] DefaultAllowedSourceApps = ["Spotify"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static AppOptions Load()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        var fileConfig = LoadFromConfigFile(configPath);

        var pollSeconds = ParsePositiveInt(
            Environment.GetEnvironmentVariable("LOL_STATUS_POLL_SECONDS"),
            fileConfig.PollSeconds ?? DefaultPollSeconds);

        var maxStatusLength = ParsePositiveInt(
            Environment.GetEnvironmentVariable("LOL_STATUS_MAX_LENGTH"),
            fileConfig.MaxStatusLength ?? DefaultMaxStatusLength);

        var statusPrefix = Environment.GetEnvironmentVariable("LOL_STATUS_PREFIX");
        if (string.IsNullOrWhiteSpace(statusPrefix))
        {
            statusPrefix = fileConfig.StatusPrefix;
        }

        if (string.IsNullOrWhiteSpace(statusPrefix))
        {
            statusPrefix = DefaultStatusPrefix;
        }

        var leagueLockfilePath = Environment.GetEnvironmentVariable("LEAGUE_LOCKFILE_PATH");
        if (string.IsNullOrWhiteSpace(leagueLockfilePath))
        {
            leagueLockfilePath = fileConfig.LeagueLockfilePath;
        }

        if (string.IsNullOrWhiteSpace(leagueLockfilePath))
        {
            leagueLockfilePath = null;
        }

        var allowedSourceApps = ParseStringList(
            Environment.GetEnvironmentVariable("LOL_STATUS_ALLOWED_APPS"),
            fileConfig.AllowedSourceApps,
            DefaultAllowedSourceApps);

        return new AppOptions(
            TimeSpan.FromSeconds(pollSeconds),
            statusPrefix.Trim(),
            maxStatusLength,
            leagueLockfilePath,
            allowedSourceApps,
            configPath);
    }

    private static int ParsePositiveInt(string? rawValue, int fallbackValue)
    {
        if (int.TryParse(rawValue, out var parsedValue) && parsedValue > 0)
        {
            return parsedValue;
        }

        return fallbackValue;
    }

    private static AppOptionsFile LoadFromConfigFile(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return new AppOptionsFile();
            }

            using var stream = File.OpenRead(configPath);
            return JsonSerializer.Deserialize<AppOptionsFile>(stream, JsonOptions) ?? new AppOptionsFile();
        }
        catch
        {
            return new AppOptionsFile();
        }
    }

    private static IReadOnlyList<string> ParseStringList(
        string? rawValue,
        IReadOnlyList<string>? fileValues,
        IReadOnlyList<string> fallbackValues)
    {
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var parsedValues = rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (parsedValues.Length > 0)
            {
                return parsedValues;
            }
        }

        if (fileValues is not null && fileValues.Count > 0)
        {
            var normalizedValues = fileValues
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToArray();

            if (normalizedValues.Length > 0)
            {
                return normalizedValues;
            }
        }

        return fallbackValues.ToArray();
    }

    private sealed class AppOptionsFile
    {
        public string? StatusPrefix { get; init; }

        public int? PollSeconds { get; init; }

        public int? MaxStatusLength { get; init; }

        public string? LeagueLockfilePath { get; init; }

        public string[]? AllowedSourceApps { get; init; }
    }
}
