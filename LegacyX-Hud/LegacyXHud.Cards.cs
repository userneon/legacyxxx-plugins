using System.Net.Http.Headers;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PanoramaManager;

namespace LegacyXHud;

// The rank card (round start), the result of a ranked match and the rank up / down card. Rank and EXP come from
// the API (GET /plugin/community/players/:steamId, the same the Community plugin reads); nothing is calculated
// here except the bar width and the difference between two readings.
public sealed partial class LegacyXHud
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly string[] Tiers = { "recruit", "operator", "vanguard", "ace", "apex", "legacy" };

    private string apiBase = "";
    private string pluginId = "legacyx-community";
    private string pluginSecret = "";

    private sealed record Profile(int RankId, string RankName, int Exp, int Matches, int? MinExp, int? NextMin, string? NextName, bool ProLeague);


    // SteamID64 -> last profile read; the "before" of the next match.
    private readonly Dictionary<ulong, Profile> profiles = new();

    private bool ApiReady => apiBase.Length > 0 && pluginSecret.Length > 0;

    // ---- API ------------------------------------------------------------------------------------------

    private async Task<Profile?> FetchProfileAsync(ulong steamId)
    {
        if (!ApiReady) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/v1/plugin/community/players/{steamId}");
            request.Headers.Add("x-plugin-id", pluginId);
            request.Headers.Add("x-plugin-secret", pluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var p = document.RootElement.GetProperty("profile");
            var rankId = Int(p, "rank_id") ?? 0;
            var name = Str(p, "rank_name")?.Trim() ?? "";
            if (rankId is < 1 or > 18 || name.Length == 0) return null;
            return new Profile(
                rankId, name, Int(p, "current_exp") ?? 0, Int(p, "matches_completed") ?? 0,
                Int(p, "current_rank_min_exp"), Int(p, "next_rank_min_exp"), Str(p, "next_rank_name"),
                p.TryGetProperty("pro_league_unlocked", out var pro) && pro.ValueKind == JsonValueKind.True);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Profile lookup failed: {ex.Message}");
            return null;
        }
    }

    private async Task LoadProfileAsync(ulong steamId)
    {
        var profile = await FetchProfileAsync(steamId);
        if (profile is null) return;
        Server.NextFrame(() => profiles[steamId] = profile);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return (int)Math.Round(d);
        return null;
    }

    // ---- small helpers --------------------------------------------------------------------------------

    private static string? TierClass(string rankName)
    {
        var first = rankName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        return first is not null && Tiers.Contains(first) ? "tier-" + first : null;
    }

    /// <summary>Progress towards the next rank as the nearest p0, p5 … p100.</summary>
    private static string? ProgressClass(Profile p)
    {
        if (p.NextMin is not { } next) return "p100";
        if (p.MinExp is not { } min || next <= min) return null;
        var fraction = Math.Clamp((p.Exp - min) / (double)(next - min), 0, 1);
        return "p" + (int)(Math.Round(fraction * 20) * 5);
    }

}
