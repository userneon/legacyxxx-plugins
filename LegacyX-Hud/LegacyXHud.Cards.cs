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

    private sealed record MatchSnap(string Outcome, int Ours, int Theirs, int Kills, int Deaths, string Map);

    // SteamID64 -> last profile read; the "before" of the next match.
    private readonly Dictionary<ulong, Profile> profiles = new();
    private readonly Dictionary<ulong, MatchSnap> finishedMatch = new();
    private readonly HashSet<int> compactOpen = new();

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

    private static string NextLine(Profile p) => p.NextMin is { } n && p.NextName is { } name ? $"{name} at {n:N0}" : "Top rank";
    private static string TogoLine(Profile p) => p.NextMin is { } n ? $"{Math.Max(0, n - p.Exp):N0} to go" : "";

    // ---- rank card at round start ---------------------------------------------------------------------

    private void ShowRankCards()
    {
        var panel = EnsureNotify();
        if (panel is null) return;
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false }))
        {
            if (!profiles.TryGetValue(player.SteamID, out var profile) || !OpenFor(panel, player)) continue;
            panel.SetVariableFor(player, "rank_name", profile.RankName);
            panel.SetVariableFor(player, "rank_exp", $"{profile.Exp:N0} EXP");
            panel.SetVariableFor(player, "rank_next", NextLine(profile));
            panel.SetVariableFor(player, "rank_togo", TogoLine(profile));
            SetState(panel, player, "rank_emblem", "rank", $"rank-{profile.RankId}");
            SetState(panel, player, "rank_name", "tier", TierClass(profile.RankName));
            SetState(panel, player, "rank_fill", "p", ProgressClass(profile));
            Flash(panel, player, "rank", 6f);
        }
    }

    // ---- end of a match -------------------------------------------------------------------------------

    private HookResult OnMatchEnd(EventCsWinPanelMatch e, GameEventInfo info)
    {
        if (!ApiReady) return HookResult.Continue;
        var scores = new Dictionary<int, int>();
        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
            scores[team.TeamNum] = team.Score;
        if (!scores.TryGetValue(2, out var t) || !scores.TryGetValue(3, out var ct)) return HookResult.Continue;

        var map = Server.MapName;
        finishedMatch.Clear();
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false } && p.TeamNum is 2 or 3))
        {
            var ours = player.TeamNum == 2 ? t : ct;
            var theirs = player.TeamNum == 2 ? ct : t;
            var stats = player.ActionTrackingServices?.MatchStats;
            finishedMatch[player.SteamID] = new MatchSnap(
                ours > theirs ? "Victory" : ours < theirs ? "Defeat" : "Draw", ours, theirs, stats?.Kills ?? 0, stats?.Deaths ?? 0, map);
        }
        // The API needs a moment to rate the match: look a few times, stop at the first reading that moved.
        foreach (var delay in new[] { 8f, 18f, 35f })
            AddTimer(delay, () => _ = CheckMatchResultAsync());
        return HookResult.Continue;
    }

    private async Task CheckMatchResultAsync()
    {
        foreach (var (steamId, snap) in finishedMatch.ToList())
        {
            if (!profiles.TryGetValue(steamId, out var before)) continue;
            var after = await FetchProfileAsync(steamId);
            if (after is null || after.Matches <= before.Matches) continue;
            Server.NextFrame(() =>
            {
                if (!finishedMatch.Remove(steamId)) return;
                profiles[steamId] = after;
                if (Utilities.GetPlayerFromSteamId(steamId) is { IsValid: true, IsBot: false } player)
                    ShowMatchResult(player, snap, before, after);
            });
        }
    }

    private void ShowMatchResult(CCSPlayerController player, MatchSnap snap, Profile before, Profile after)
    {
        var panel = EnsureMatch();
        if (panel is null || !OpenFor(panel, player)) return;

        var delta = after.Exp - before.Exp;
        var deltaText = delta > 0 ? $"+{delta:N0} EXP" : delta == 0 ? "0 EXP" : $"{delta:N0} EXP";
        var deltaClass = delta > 0 ? "up" : delta == 0 ? "zero" : null;
        var score = $"{snap.Ours} : {snap.Theirs} · {snap.Kills} / {snap.Deaths}";
        var tier = TierClass(after.RankName);
        var calibrating = after.Matches < 10;

        // full card, centre
        panel.SetVariableFor(player, "mf_mode", $"5x5 · {snap.Map} · counted");
        panel.SetVariableFor(player, "mf_title", snap.Outcome);
        panel.SetVariableFor(player, "mf_score", score);
        panel.SetVariableFor(player, "mf_delta", deltaText);
        panel.SetVariableFor(player, "mf_rank", after.RankName);
        panel.SetVariableFor(player, "mf_exp", $"{before.Exp:N0} → {after.Exp:N0} EXP");
        panel.SetVariableFor(player, "mf_next", NextLine(after));
        panel.SetVariableFor(player, "mf_togo", TogoLine(after));
        panel.SetVariableFor(player, "mf_calib_text", $"Calibration {Math.Min(after.Matches, 10)} / 10");
        panel.SetClassFor(player, "mf_calib", "hidden", !calibrating);
        SetState(panel, player, "mf_emblem", "rank", $"rank-{after.RankId}");
        SetState(panel, player, "mf_delta", "delta", deltaClass);
        SetState(panel, player, "mf_rank", "tier", tier);
        SetState(panel, player, "mf_fill", "p", ProgressClass(after));
        SetState(panel, player, "mf_lost", "p", delta < 0 ? ProgressClass(before) : null);

        // compact card, right, until the next match is live
        panel.SetVariableFor(player, "mc_title", $"Last match · {snap.Outcome} {snap.Ours} : {snap.Theirs}");
        panel.SetVariableFor(player, "mc_delta", deltaText);
        panel.SetVariableFor(player, "mc_rank", after.RankName);
        panel.SetVariableFor(player, "mc_exp", $"{after.Exp:N0} EXP");
        panel.SetVariableFor(player, "mc_next", NextLine(after));
        panel.SetVariableFor(player, "mc_togo", TogoLine(after));
        SetState(panel, player, "mc_emblem", "rank", $"rank-{after.RankId}");
        SetState(panel, player, "mc_delta", "delta", deltaClass);
        SetState(panel, player, "mc_rank", "tier", tier);
        SetState(panel, player, "mc_fill", "p", ProgressClass(after));

        Flash(panel, player, "mf", 10f);
        var slot = player.Slot;
        AddTimer(11f, () =>
        {
            var still = Utilities.GetPlayerFromSlot(slot);
            if (still is not { IsValid: true }) return;
            compactOpen.Add(slot);
            panel.SetClassFor(still, "mc", "open", true);
            if (after.RankId != before.RankId) ShowRankChange(still, before, after);
        });
    }

    private void CloseMatchCompactCards()
    {
        if (match is null || compactOpen.Count == 0) return;
        foreach (var slot in compactOpen.ToList())
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is { IsValid: true } && match.IsOpenFor(player)) match.SetClassFor(player, "mc", "open", false);
        }
        compactOpen.Clear();
    }

    // ---- rank up / down -------------------------------------------------------------------------------

    private void ShowRankChange(CCSPlayerController player, Profile before, Profile after)
    {
        var panel = EnsureNotify();
        if (panel is null || !OpenFor(panel, player)) return;
        var up = after.RankId > before.RankId;
        var proNow = after.ProLeague && !before.ProLeague;
        panel.SetVariableFor(player, "rc_kicker", up ? "Rank up" : "Rank down");
        panel.SetVariableFor(player, "rc_name", after.RankName);
        panel.SetVariableFor(player, "rc_body",
            (proNow ? "Pro League is open to you now. " : "") + $"{after.Exp:N0} EXP" + (after.NextMin is null ? "" : " · " + NextLine(after)));
        SetState(panel, player, "rc_old", "rank", $"rank-{before.RankId}");
        SetState(panel, player, "rc_new", "rank", $"rank-{after.RankId}");
        SetState(panel, player, "rc_name", "tier", TierClass(after.RankName));
        panel.SetClassFor(player, "rc_unlock", "hidden", !proNow);
        Flash(panel, player, "rc", 8f);
    }
}
