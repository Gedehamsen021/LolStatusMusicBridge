using System.Diagnostics;

namespace LolStatusMusicBridge;

internal sealed class LeagueClientLocator
{
    private static readonly string[] CommonLockfilePaths =
    [
        @"C:\Riot Games\League of Legends\lockfile",
        @"C:\Program Files\Riot Games\League of Legends\lockfile",
        @"C:\Program Files (x86)\Riot Games\League of Legends\lockfile"
    ];

    public LeagueLockfileCredentials? TryReadCredentials(string? configuredLockfilePath)
    {
        var lockfilePath = TryLocateLockfile(configuredLockfilePath);
        if (lockfilePath is null)
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                lockfilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            var contents = reader.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(contents))
            {
                return null;
            }

            var parts = contents.Split(':', StringSplitOptions.None);
            if (parts.Length != 5 || !int.TryParse(parts[1], out var processId) ||
                !int.TryParse(parts[2], out var port))
            {
                return null;
            }

            return new LeagueLockfileCredentials(
                lockfilePath,
                parts[0],
                processId,
                port,
                parts[3],
                parts[4]);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryLocateLockfile(string? configuredLockfilePath)
    {
        foreach (var candidate in EnumerateLockfileCandidates(configuredLockfilePath))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string?> EnumerateLockfileCandidates(string? configuredLockfilePath)
    {
        yield return configuredLockfilePath;

        foreach (var processName in new[] { "LeagueClientUx", "LeagueClient" })
        {
            foreach (var processPath in TryGetProcessPaths(processName))
            {
                yield return Path.Combine(processPath, "lockfile");
            }
        }

        foreach (var commonLockfilePath in CommonLockfilePaths)
        {
            yield return commonLockfilePath;
        }
    }

    private static IEnumerable<string> TryGetProcessPaths(string processName)
    {
        Process[] processes;

        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            yield break;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                string? executablePath = null;

                try
                {
                    executablePath = process.MainModule?.FileName;
                }
                catch
                {
                    executablePath = null;
                }

                if (!string.IsNullOrWhiteSpace(executablePath))
                {
                    var directory = Path.GetDirectoryName(executablePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        yield return directory;
                    }
                }
            }
        }
    }
}
