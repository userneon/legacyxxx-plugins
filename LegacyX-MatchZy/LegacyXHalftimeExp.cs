using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// The EXP card (owner request 2026-10-30; overtime and final added 2026-11-06): at half time (round 12 of 24), when overtime starts (the score is level
/// after the regulation rounds) and when the match is over, everyone sees what each player earned (so far, or in the end), and each player sees how their
/// own EXP was made up (result, margin, performance, bonus). The numbers come from the site, calculated with the same formula as the final result;
/// at half time and in overtime nothing is stored or paid, the final card shows what was really applied.
/// </summary>
public partial class MatchZy
{
    private bool halftimeExpShown;
    private bool overtimeExpShown;

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
            if (!IsMatchCoreEnabled || !matchCoreId.HasValue || matchCoreCreateFailed || !isMatchLive) return;
            var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;
            if (maxRounds < 2) return;
            var (team1, team2) = GetTeamsScore();
            string title;
            if (!halftimeExpShown && team1 + team2 == maxRounds / 2) { halftimeExpShown = true; title = "Half time"; }
            else if (!overtimeExpShown && team1 + team2 == maxRounds && team1 == team2) { overtimeExpShown = true; title = "Overtime"; }
            else return;

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
                        Log($"[LegacyXMatchCore] The {title} EXP card was refused ({(int)response.StatusCode}): {text[..Math.Min(200, text.Length)]}");
                        return;
                    }
                    Server.NextFrame(() => ShowLegacyXExpCard(text, title, final: false));
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

    /// <summary>The final card: what the match really gave each player (the site's answer to the final result), shown when the match is over.</summary>
    private void ShowLegacyXFinalExp(JsonElement competitive)
    {
        if (!competitive.TryGetProperty("players", out var rows) || rows.ValueKind != JsonValueKind.Array) return;
        ShowLegacyXExpRows(rows, "Match over", final: true);
    }

    private void ShowLegacyXExpCard(string responseBody, string title, bool final)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("result", out var result) || !result.TryGetProperty("players", out var rows) || rows.ValueKind != JsonValueKind.Array) return;
        ShowLegacyXExpRows(rows, title, final);
    }

    private void ShowLegacyXExpRows(JsonElement rows, string title, bool final)
    {

        static string Signed(int value) => value > 0 ? $"{{green}}+{value}" : value < 0 ? $"{{white}}{value}" : "{white}0";
        var byPlayer = new Dictionary<ulong, JsonElement>();
        foreach (var row in rows.EnumerateArray())
            if (row.TryGetProperty("steam_id", out var id) && ulong.TryParse(id.GetString(), out var steamId)) byPlayer[steamId] = row;

        var humans = Utilities.GetPlayers().Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV && byPlayer.ContainsKey(player.SteamID)).ToList();
        if (humans.Count == 0) return;

        PrintToAllChat(final ? $"{{white}}{title}{{grey}} · EXP of this match" : $"{{white}}{title}{{grey}} · EXP so far (the final result can still change it)");
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
            PrintToPlayerChat(player, $"{{white}}{(final ? "Your EXP" : "Your EXP so far")}{{grey}}: {Signed(delta)}{{grey}} ({string.Join("{grey} · ", parts)}{{grey}}) → {{white}}{after}");
        }
    }
}
