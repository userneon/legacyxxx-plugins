using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// The half time EXP card (owner request 2026-10-30): when the first half of a ranked match ends (round 12 of 24), everyone sees what each player
/// would have earned so far, and each player sees how their own EXP was made up (result, margin, performance, bonus). The numbers come from the
/// site, calculated from the stats of the match so far with the same formula as the final result; nothing is stored or paid at this point.
/// </summary>
public partial class MatchZy
{
    private bool halftimeExpShown;

    private void RegisterLegacyXHalftimeExp()
    {
        RegisterEventHandler<EventRoundEnd>((_, _) =>
        {
            AddTimer(1.5f, TryShowLegacyXHalftimeExp);
            return HookResult.Continue;
        });
    }

    private void TryShowLegacyXHalftimeExp()
    {
        try
        {
            if (halftimeExpShown || !IsMatchCoreEnabled || !matchCoreId.HasValue || matchCoreCreateFailed || !isMatchLive) return;
            var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;
            if (maxRounds < 2) return;
            var (team1, team2) = GetTeamsScore();
            if (team1 + team2 != maxRounds / 2) return;
            halftimeExpShown = true;

            // Built here, on the game thread; the request is made on a worker thread.
            var body = JsonSerializer.Serialize(new { match_id = matchCoreId.Value.ToString(), result = new { competitive_result = BuildCompetitiveResult(team1, team2, true) } });
            var url = legacyXMatchCoreApiUrl.Value.Trim();
            var slash = url.LastIndexOf('/');
            if (slash < 0) return;
            url = url[..(slash + 1)] + "exp-preview";
            var secret = legacyXMatchCorePluginSecret.Value.Trim();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("x-plugin-id", "legacyx-match-core");
                    request.Headers.Add("x-plugin-secret", secret);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using var response = await matchCoreHttp.SendAsync(request);
                    var text = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        Log($"[LegacyXMatchCore] The half time EXP card was refused ({(int)response.StatusCode}): {text[..Math.Min(200, text.Length)]}");
                        return;
                    }
                    Server.NextFrame(() => ShowLegacyXHalftimeExp(text));
                }
                catch (Exception exception)
                {
                    Log($"[LegacyXMatchCore] The half time EXP card failed: {exception.Message}");
                }
            });
        }
        catch (Exception exception)
        {
            Log($"[LegacyXMatchCore] The half time EXP card could not be built: {exception.Message}");
        }
    }

    private void ShowLegacyXHalftimeExp(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("players", out var rows) || rows.ValueKind != JsonValueKind.Array) return;

        static string Signed(int value) => value > 0 ? $"{{green}}+{value}" : value < 0 ? $"{{white}}{value}" : "{white}0";
        var byPlayer = new Dictionary<ulong, JsonElement>();
        foreach (var row in rows.EnumerateArray())
            if (row.TryGetProperty("steam_id", out var id) && ulong.TryParse(id.GetString(), out var steamId)) byPlayer[steamId] = row;

        var humans = Utilities.GetPlayers().Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV && byPlayer.ContainsKey(player.SteamID)).ToList();
        if (humans.Count == 0) return;

        PrintToAllChat("{white}Half time{grey} · EXP so far (the final result can still change it)");
        foreach (var (label, teamNum) in new[] { ("CT", (byte)CsTeam.CounterTerrorist), ("T", (byte)CsTeam.Terrorist) })
        {
            var side = humans.Where(player => player.TeamNum == teamNum)
                .Select(player => $"{{white}}{player.PlayerName}{{grey}} {Signed(byPlayer[player.SteamID].GetProperty("exp_delta").GetInt32())}")
                .ToList();
            if (side.Count > 0) PrintToAllChat($"{{grey}}{label}: " + string.Join("{grey} · ", side));
        }

        foreach (var player in humans)
        {
            var row = byPlayer[player.SteamID];
            var delta = row.GetProperty("exp_delta").GetInt32();
            var after = row.GetProperty("exp_after").GetInt32();
            var parts = new List<string>();
            if (row.TryGetProperty("breakdown", out var breakdown))
            {
                foreach (var (key, name) in new[] { ("result", "Result"), ("margin", "Margin"), ("performance", "Performance"), ("bonus", "Bonus") })
                    if (breakdown.TryGetProperty(key, out var part) && part.ValueKind == JsonValueKind.Number)
                        parts.Add($"{{grey}}{name} {Signed((int)Math.Round(part.GetDouble()))}");
            }
            PrintToPlayerChat(player, $"{{white}}Your EXP so far{{grey}}: {Signed(delta)}{{grey}} ({string.Join("{grey} · ", parts)}{{grey}}) → {{white}}{after}");
        }
    }
}
