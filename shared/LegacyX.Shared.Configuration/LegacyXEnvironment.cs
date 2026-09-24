namespace LegacyX.Shared.Configuration;

public sealed class LegacyXEnvironment
{
    private readonly IReadOnlyDictionary<string, string> _values;

    internal LegacyXEnvironment(IReadOnlyDictionary<string, string> values, string? loadedFrom)
    {
        _values = values;
        LoadedFrom = loadedFrom;
    }

    public string? LoadedFrom { get; }

    public string Get(string key, string fallback = "")
    {
        var processValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(processValue)) return processValue.Trim();
        return _values.TryGetValue(key, out var fileValue) ? fileValue : fallback;
    }

    public int GetInt(string key, int fallback, int minimum, int maximum)
    {
        var raw = Get(key, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return int.TryParse(raw, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    }

    public string GetModule(string module, string setting, string fallback = "")
    {
        var prefix = $"LEGACYX_{Normalize(module)}_";
        return Get(prefix + setting, Get("LEGACYX_" + setting, fallback));
    }

    public bool GetModuleBoolean(string module, string setting, bool fallback)
    {
        var raw = GetModule(module, setting, fallback ? "true" : "false");
        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    public int GetModuleInt(string module, string setting, int fallback, int minimum, int maximum)
    {
        var raw = GetModule(module, setting, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return int.TryParse(raw, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    }

    public string RequireModule(string module, string setting)
    {
        var value = GetModule(module, setting);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Missing required central environment variable LEGACYX_{Normalize(module)}_{setting}.");
        return value;
    }

    public static string Normalize(string value) => new string(value.Trim().ToUpperInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}
