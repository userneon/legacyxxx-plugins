using System.Text.Json.Serialization;

namespace LegacyX.Checker;

/// <summary>One thing the scan found. Only names and masked paths: never file contents.</summary>
public sealed record Finding(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("path"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonPropertyName("note"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null)
{
    public const string Detection = "detection";
    public const string Suspicion = "suspicion";
}

/// <summary>One Steam account that has signed in on this PC, from Steam's own files. Nothing here is a password or a login token.</summary>
public sealed class SteamAccount
{
    [JsonPropertyName("steamId")] public string SteamId { get; set; } = "";
    [JsonPropertyName("accountName")] public string? AccountName { get; set; }
    [JsonPropertyName("personaName")] public string? PersonaName { get; set; }
    [JsonPropertyName("lastLogin")] public string? LastLogin { get; set; }
    [JsonPropertyName("mostRecent")] public bool? MostRecent { get; set; }
    [JsonPropertyName("cs2LastPlayed")] public string? Cs2LastPlayed { get; set; }
    [JsonPropertyName("cs2Hours")] public double? Cs2Hours { get; set; }
    [JsonPropertyName("launchOptions")] public string? LaunchOptions { get; set; }
}

public sealed class Cs2Info
{
    [JsonPropertyName("installed")] public bool Installed { get; set; }
    [JsonPropertyName("lastUpdated")] public string? LastUpdated { get; set; }
}

/// <summary>Exactly what is sent to the server (POST /api/v1/checks/code/:code/report). The server refuses anything else.</summary>
/// <summary>What the program found about one program: facts, not a verdict. The server judges them.</summary>
public sealed class ProgramFact
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("path"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Path { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("signed")] public bool Signed { get; set; }
    [JsonPropertyName("managed")] public bool Managed { get; set; }
    [JsonPropertyName("running"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Running { get; set; }
    [JsonPropertyName("protector"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Protector { get; set; }
    [JsonPropertyName("apis")] public List<string> Apis { get; set; } = new();
    [JsonPropertyName("game")] public List<string> Game { get; set; } = new();
    [JsonPropertyName("offsets")] public List<string> Offsets { get; set; } = new();
    [JsonPropertyName("family")] public List<string> Family { get; set; } = new();
    /// <summary>How much it matched, to keep the most telling ones when there are too many to send.</summary>
    [JsonIgnore] public int Weight => Family.Count * 6 + Offsets.Count * 3 + Game.Count * 2 + Apis.Count;
}

public sealed class HwidPart
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("hash")] public string Hash { get; set; } = "";
}

public sealed class HwidInfo
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("parts")] public List<HwidPart> Parts { get; set; } = new();
}

public sealed class CheckReport
{
    [JsonPropertyName("consent")] public bool Consent { get; set; }
    [JsonPropertyName("checkerVersion")] public string CheckerVersion { get; set; } = App.Version;
    [JsonPropertyName("steamIds")] public List<string> SteamIds { get; set; } = new();
    [JsonPropertyName("steamAccounts")] public List<SteamAccount> SteamAccounts { get; set; } = new();
    [JsonPropertyName("cs2")] public Cs2Info? Cs2 { get; set; }
    [JsonPropertyName("hwid")] public HwidInfo? Hwid { get; set; }
    [JsonPropertyName("facts")] public List<ProgramFact> Facts { get; set; } = new();
    [JsonPropertyName("filesScanned")] public long FilesScanned { get; set; }
    [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; set; }
    [JsonPropertyName("findings")] public List<Finding> Findings { get; set; } = new();
}

public sealed class CodeInfo
{
    [JsonPropertyName("requestedBy")] public string RequestedBy { get; set; } = "Staff";
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
}
