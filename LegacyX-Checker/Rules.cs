using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegacyX.Checker;

/// <summary>
/// What the checker is told to look for: lists of Windows functions and words. It only reports which of them a program holds. How that is judged (the
/// points, the limits, which words belong to which cheat) is decided on the server, so none of it is in this program. The server sends the lists at the
/// start of a scan; the copy built into the program is only a plain fallback for when it cannot be reached.
/// </summary>
public sealed class Rules
{
    [JsonPropertyName("version")] public int Version { get; set; }
    /// <summary>Windows functions (lower case) to report when a program uses them.</summary>
    [JsonPropertyName("apis")] public List<string> Apis { get; set; } = new();
    [JsonPropertyName("gameMarkers")] public List<string> GameMarkers { get; set; } = new();
    [JsonPropertyName("offsetMarkers")] public List<string> OffsetMarkers { get; set; } = new();
    /// <summary>Words staff gave to recognise a cheat; the server knows which of them belong together.</summary>
    [JsonPropertyName("familyStrings")] public List<string> FamilyStrings { get; set; } = new();
    [JsonPropertyName("protectorSections")] public List<string> ProtectorSections { get; set; } = new();
    [JsonPropertyName("knownFileNames")] public List<string> KnownFileNames { get; set; } = new();
    [JsonPropertyName("sha256")] public List<string> Sha256 { get; set; } = new();
    [JsonPropertyName("cheatHosts")] public List<string> CheatHosts { get; set; } = new();

    private HashSet<string>? _hashes;
    private HashSet<string>? _fileNames;
    private HashSet<string>? _apis;

    public bool HasHashes => Sha256.Count > 0;
    public bool IsKnownHash(string hash) => (_hashes ??= new HashSet<string>(Sha256.Select(h => h.Trim().ToLowerInvariant()))).Contains(hash.ToLowerInvariant());
    public bool IsKnownFileName(string fileName) => (_fileNames ??= new HashSet<string>(KnownFileNames.Select(n => n.Trim().ToLowerInvariant()))).Contains(fileName.ToLowerInvariant());
    public bool IsProbeApi(string api) => (_apis ??= new HashSet<string>(Apis.Select(a => a.Trim().ToLowerInvariant()))).Contains(api.ToLowerInvariant());

    /// <summary>The lists the server sent.</summary>
    public static Rules? FromServer(string json)
    {
        try
        {
            var rules = JsonSerializer.Deserialize<Rules>(json);
            return rules is { Apis.Count: > 0 } ? rules : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The plain fallback: rules.json next to the program if there is one, else the copy inside it.</summary>
    public static Rules Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "rules.json");
            if (File.Exists(path)) return JsonSerializer.Deserialize<Rules>(File.ReadAllText(path)) ?? new Rules();
            using var built = typeof(Rules).Assembly.GetManifestResourceStream("rules.json");
            if (built is not null) return JsonSerializer.Deserialize<Rules>(built) ?? new Rules();
        }
        catch
        {
            // A broken rules file must not stop the scan; it just reports less.
        }
        return new Rules();
    }
}
