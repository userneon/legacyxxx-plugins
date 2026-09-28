using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LegacyX.Shared.Configuration;

public static class LegacyXEnvironmentLoader
{
    private static readonly object Sync = new();
    private static LegacyXEnvironment? _current;

    public static LegacyXEnvironment Load()
    {
        lock (Sync) return _current ??= LoadCore();
    }

    private static LegacyXEnvironment LoadCore()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Servers sharing one install share this file; the port tells them apart.
        var port = LegacyXEnvironment.DetectPort(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("LEGACYX_SERVER_PORT"));
        var envPath = ResolveEnvironmentPath();
        if (envPath == null) return new LegacyXEnvironment(values, null, port);

        foreach (var line in File.ReadLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith("export ", StringComparison.Ordinal)) trimmed = trimmed[7..].TrimStart();
            var separator = trimmed.IndexOf('=');
            if (separator <= 0) continue;
            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            if (key.Length > 0) values[key] = value;
        }

        return new LegacyXEnvironment(values, envPath, port);
    }

    private static string? ResolveEnvironmentPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("LEGACYX_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return Path.GetFullPath(explicitPath);
        // This library lives in addons/counterstrikesharp/shared/<name>/, so its own folder finds the .env first.
        // AppContext.BaseDirectory and the working directory are the game's bin folder under CounterStrikeSharp.
        return FindEnvironmentFile(new[]
        {
            Path.GetDirectoryName(typeof(LegacyXEnvironmentLoader).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        });
    }

    // Linux paths are case-sensitive: CounterStrikeSharp installs as addons/counterstrikesharp.
    private static readonly string[] EnvironmentFolders = { "", "CounterStrikeSharp", "counterstrikesharp", "addons/counterstrikesharp", "csgo/addons/counterstrikesharp" };

    /// <summary>Walks up from each start folder looking for .env (or .env.txt) directly or in a CounterStrikeSharp folder.</summary>
    public static string? FindEnvironmentFile(IEnumerable<string?> starts)
    {
        foreach (var start in starts.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start!));
            while (directory != null)
            {
                foreach (var folder in EnvironmentFolders)
                {
                    // ".env.txt" too: Windows Notepad adds it, and game-panel file managers (AMP) can't rename it away.
                    foreach (var name in new[] { ".env", ".env.txt" })
                    {
                        var candidate = Path.Combine(directory.FullName, folder, name);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                directory = directory.Parent;
            }
        }
        return null;
    }
}
