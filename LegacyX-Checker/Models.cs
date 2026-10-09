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
public sealed class CheckReport
{
    [JsonPropertyName("consent")] public bool Consent { get; set; }
    [JsonPropertyName("checkerVersion")] public string CheckerVersion { get; set; } = App.Version;
    [JsonPropertyName("steamIds")] public List<string> SteamIds { get; set; } = new();
    [JsonPropertyName("steamAccounts")] public List<SteamAccount> SteamAccounts { get; set; } = new();
    [JsonPropertyName("cs2")] public Cs2Info? Cs2 { get; set; }
    [JsonPropertyName("filesScanned")] public long FilesScanned { get; set; }
    [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; set; }
    [JsonPropertyName("findings")] public List<Finding> Findings { get; set; } = new();
}

public sealed class CodeInfo
{
    [JsonPropertyName("requestedBy")] public string RequestedBy { get; set; } = "Staff";
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
}
