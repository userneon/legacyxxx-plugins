using System;
using System.Collections.Generic;
using System.Linq;

namespace LegacyX.Shared.Configuration;

/// <summary>
/// The central CounterStrikeSharp/.env, read by every LEGACY-X module.
///
/// Several CS2 servers can share one install (one set of files, one .env). Each server process is
/// told apart by its port (<c>-port 27016</c> on its command line, or LEGACYX_SERVER_PORT):
/// <list type="bullet">
/// <item>LEGACYX_SERVER_ID defaults to <c>srv-27016</c> (LEGACYX_SERVER_ID_PREFIX-27016), LEGACYX_SERVER_ADDRESS to <c>LEGACYX_SERVER_HOST:27016</c>.</item>
/// <item>Any setting can be set for one server with the port after LEGACYX_:
/// <c>LEGACYX_27016_SERVER_MODE=fun</c> wins over <c>LEGACYX_SERVER_MODE</c> on port 27016 only.</item>
/// </list>
/// Order: the process environment, then this server's LEGACYX_&lt;port&gt;_ line, then the shared line,
/// then the port-based default, then the caller's fallback.
/// </summary>
public sealed class LegacyXEnvironment
{
    private readonly IReadOnlyDictionary<string, string> _values;

    internal LegacyXEnvironment(IReadOnlyDictionary<string, string> values, string? loadedFrom, int? instancePort = null)
    {
        _values = values;
        LoadedFrom = loadedFrom;
        InstancePort = instancePort;
    }

    public string? LoadedFrom { get; }

    /// <summary>This server process's game port, when it could be found.</summary>
    public int? InstancePort { get; }

    public string Get(string key, string fallback = "")
    {
        var processValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(processValue)) return processValue.Trim();
        if (InstancePort is { } port && key.StartsWith("LEGACYX_", StringComparison.OrdinalIgnoreCase)
            && _values.TryGetValue($"LEGACYX_{port}_{key[8..]}", out var instanceValue) && instanceValue.Length > 0)
            return instanceValue;
        if (_values.TryGetValue(key, out var fileValue) && fileValue.Length > 0) return fileValue;
        return PortDefault(key) ?? fallback;
    }

    private string? PortDefault(string key)
    {
        if (InstancePort is not { } port) return null;
        // Machines with their own install can set LEGACYX_SERVER_ID_PREFIX so their ports don't collide.
        if (key.Equals("LEGACYX_SERVER_ID", StringComparison.OrdinalIgnoreCase)) return $"{Get("LEGACYX_SERVER_ID_PREFIX", "srv").Trim()}-{port}";
        if (key.Equals("LEGACYX_SERVER_ADDRESS", StringComparison.OrdinalIgnoreCase))
        {
            var host = Get("LEGACYX_SERVER_HOST").Trim();
            return host.Length > 0 ? $"{host}:{port}" : null;
        }
        return null;
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

    /// <summary>
    /// The game port from LEGACYX_SERVER_PORT or the server's command line (<c>-port 27016</c>,
    /// <c>-port=27016</c>, <c>+hostport 27016</c>). Null when neither says.
    /// </summary>
    public static int? DetectPort(IReadOnlyList<string> commandLine, string? portVariable)
    {
        if (TryPort(portVariable, out var fromVariable)) return fromVariable;
        for (var i = 0; i < commandLine.Count; i++)
        {
            var arg = commandLine[i];
            foreach (var name in new[] { "-port", "+hostport" })
            {
                if (arg.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < commandLine.Count && TryPort(commandLine[i + 1], out var next)) return next;
                if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase) && TryPort(arg[(name.Length + 1)..], out var inline)) return inline;
            }
        }
        return null;
    }

    private static bool TryPort(string? value, out int port) =>
        int.TryParse(value?.Trim(), out port) && port is >= 1 and <= 65535;
}
