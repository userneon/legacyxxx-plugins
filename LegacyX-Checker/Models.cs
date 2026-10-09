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

/// <summary>Exactly what is sent to the server (POST /api/v1/checks/code/:code/report). The server refuses anything else.</summary>
public sealed class CheckReport
{
    [JsonPropertyName("consent")] public bool Consent { get; set; }
    [JsonPropertyName("checkerVersion")] public string CheckerVersion { get; set; } = App.Version;
    [JsonPropertyName("steamIds")] public List<string> SteamIds { get; set; } = new();
    [JsonPropertyName("filesScanned")] public long FilesScanned { get; set; }
    [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; set; }
    [JsonPropertyName("findings")] public List<Finding> Findings { get; set; } = new();
}

public sealed class CodeInfo
{
    [JsonPropertyName("requestedBy")] public string RequestedBy { get; set; } = "Staff";
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
}
