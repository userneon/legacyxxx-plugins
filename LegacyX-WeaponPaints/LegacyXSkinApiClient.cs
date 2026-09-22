using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeaponPaints;

internal sealed class LegacyXSkinApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly WeaponPaintsConfig _config;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    internal LegacyXSkinApiClient(WeaponPaintsConfig config) => _config = config;

    /// <summary>
    /// The player's saved website loadout, or null when the SteamID has no LEGACY-X account.
    /// </summary>
    internal async Task<SkinchangerPayload?> GetLoadoutAsync(string steamId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/plugin/skinchanger/loadout?steam_id={Uri.EscapeDataString(steamId)}");
        using var response = await Http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SkinchangerPayload>(_json) ?? new SkinchangerPayload();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, _config.ApiBaseUrl + path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.PluginSecret);
        request.Headers.Add("x-plugin-id", _config.PluginId);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var statusCode = (int)response.StatusCode;
        var detail = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"LEGACY-X API returned {statusCode}: {detail[..Math.Min(detail.Length, 240)]}");
    }
}

internal sealed class SkinchangerPayload
{
    [JsonPropertyName("entries")] public List<SkinchangerEntry> Entries { get; set; } = [];
}

internal sealed class SkinchangerEntry
{
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("teamScope")] public string TeamScope { get; set; } = "all";
    [JsonPropertyName("weaponDefindex")] public int? WeaponDefindex { get; set; }
    [JsonPropertyName("paintId")] public int? PaintId { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("options")] public SkinchangerOptions Options { get; set; } = new();
}

internal sealed class SkinchangerOptions
{
    [JsonPropertyName("wear")] public float? Wear { get; set; }
    [JsonPropertyName("seed")] public int? Seed { get; set; }
    [JsonPropertyName("statTrak")] public bool? StatTrak { get; set; }
    [JsonPropertyName("nameTag")] public string? NameTag { get; set; }
    [JsonPropertyName("stickers")] public List<SkinchangerStickerOption> Stickers { get; set; } = [];
    [JsonPropertyName("charm")] public SkinchangerCharmOption? Charm { get; set; }
}

internal sealed class SkinchangerStickerOption
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("schema")] public int? Schema { get; set; }
    [JsonPropertyName("offsetX")] public float? OffsetX { get; set; }
    [JsonPropertyName("offsetY")] public float? OffsetY { get; set; }
    [JsonPropertyName("wear")] public float? Wear { get; set; }
    [JsonPropertyName("scale")] public float? Scale { get; set; }
    [JsonPropertyName("rotation")] public float? Rotation { get; set; }
}

internal sealed class SkinchangerCharmOption
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("offsetX")] public float? OffsetX { get; set; }
    [JsonPropertyName("offsetY")] public float? OffsetY { get; set; }
    [JsonPropertyName("offsetZ")] public float? OffsetZ { get; set; }
    [JsonPropertyName("seed")] public int? Seed { get; set; }
}
