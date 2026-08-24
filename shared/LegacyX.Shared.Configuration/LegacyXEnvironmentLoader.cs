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
        var envPath = ResolveEnvironmentPath();
        if (envPath == null) return new LegacyXEnvironment(values, null);

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

        return new LegacyXEnvironment(values, envPath);
    }

    private static string? ResolveEnvironmentPath()
    {
        var explicitPath = Environment.GetEnvironmentVariable("LEGACYX_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return Path.GetFullPath(explicitPath);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory != null)
            {
                var direct = Path.Combine(directory.FullName, ".env");
                if (File.Exists(direct)) return direct;
                var counterStrikeSharp = Path.Combine(directory.FullName, "CounterStrikeSharp", ".env");
                if (File.Exists(counterStrikeSharp)) return counterStrikeSharp;
                directory = directory.Parent;
            }
        }
        return null;
    }
}
