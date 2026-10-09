using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegacyX.Checker;

/// <summary>What to look for. Read from rules.json next to the program.</summary>
public sealed class Rules
{
    [JsonPropertyName("nameKeywords")] public List<string> NameKeywords { get; set; } = new();
    [JsonPropertyName("processKeywords")] public List<string> ProcessKeywords { get; set; } = new();
    [JsonPropertyName("knownFileNames")] public List<string> KnownFileNames { get; set; } = new();
    [JsonPropertyName("sha256")] public List<string> Sha256 { get; set; } = new();
    /// <summary>Names a program mentions when it is about CS2: the game's program and its libraries.</summary>
    [JsonPropertyName("gameMarkers")] public List<string> GameMarkers { get; set; } = new();
    /// <summary>Names of the values a CS2 cheat reads out of the game (they come from public offset lists).</summary>
    [JsonPropertyName("offsetMarkers")] public List<string> OffsetMarkers { get; set; } = new();
    /// <summary>Section names that program protectors (VMProtect, Themida …) leave in a program.</summary>
    [JsonPropertyName("protectorSections")] public List<string> ProtectorSections { get; set; } = new();

    private HashSet<string>? _hashes;
    private HashSet<string>? _fileNames;

    public bool HasHashes => Sha256.Count > 0;
    public bool IsKnownHash(string hash) => (_hashes ??= new HashSet<string>(Sha256.Select(h => h.Trim().ToLowerInvariant()))).Contains(hash.ToLowerInvariant());
    public bool IsKnownFileName(string fileName) => (_fileNames ??= new HashSet<string>(KnownFileNames.Select(n => n.Trim().ToLowerInvariant()))).Contains(fileName.ToLowerInvariant());

    /// <summary>The first keyword of <paramref name="keywords"/> found in <paramref name="text"/> (case does not matter), or null.</summary>
    public static string? Match(IEnumerable<string> keywords, string text)
    {
        foreach (var keyword in keywords)
        {
            if (keyword.Length >= 4 && text.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return keyword;
        }
        return null;
    }

    public static Rules Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "rules.json");
            // A rules.json next to the program wins (staff can ship newer rules); otherwise the copy built into the program.
            if (File.Exists(path)) return JsonSerializer.Deserialize<Rules>(File.ReadAllText(path)) ?? new Rules();
            using var built = typeof(Rules).Assembly.GetManifestResourceStream("rules.json");
            if (built is not null) return JsonSerializer.Deserialize<Rules>(built) ?? new Rules();
        }
        catch
        {
            // A broken rules file must not stop the scan; it just looks for less.
        }
        return new Rules();
    }
}
