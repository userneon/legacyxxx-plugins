using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace LegacyXCommunity;

/// <summary>
/// The welcome card (owner request 2026-11-06): once a player has chosen a team, the middle of their screen shows their name, rank, EXP and how far it is to the next rank
/// for a few seconds. LegacyX-Hud draws it (command lx_welcome_hud); this plugin only passes what the site said about the player when they joined. A player whose profile
/// could not be read gets no card (nothing is made up). Once per player per few hours, not every map, like the chat welcome.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private sealed record WelcomeCardData(int RankId, int Exp, int Place, int RankExp, int NextExp, string RankName, string NextName);

    private readonly Dictionary<ulong, WelcomeCardData> pendingWelcomeCards = new();
    private readonly Dictionary<ulong, DateTime> welcomeCardShownAt = new();

    private void RegisterWelcomeCard()
    {
        // The card waits for the team: a player who is still choosing (or spectating) is not shown anything.
        RegisterEventHandler<EventPlayerTeam>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true, IsBot: false } who && @event.Team is 2 or 3)
            {
                var slot = who.Slot;
                Server.NextFrame(() => { if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } player) TryShowWelcomeCard(player); });
            }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } player) pendingWelcomeCards.Remove(player.SteamID);
            return HookResult.Continue;
        });
    }

    private static string CardSafe(string? text) => new string((text ?? "").Where(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or ' ').ToArray()).Trim();

    private void QueueWelcomeCard(CCSPlayerController player, System.Net.HttpStatusCode? status, JsonElement? profile)
    {
        if (!Config.Welcome || !player.IsValid || player.IsBot) return;
        WelcomeCardData? data = null;
        if (status == System.Net.HttpStatusCode.NotFound) data = new WelcomeCardData(0, 0, 0, 0, 0, "", "");
        else if (profile is { } found)
        {
            var rankId = IntOrNull(found, "rank_id") ?? 0;
            data = new WelcomeCardData(
                rankId is >= 1 and <= 18 ? rankId : 0,
                IntOrNull(found, "current_exp") ?? 0,
                Math.Max(0, IntOrNull(found, "position") ?? 0),
                IntOrNull(found, "current_rank_min_exp") ?? 0,
                IntOrNull(found, "next_rank_min_exp") ?? 0,
                CardSafe(StringOrNull(found, "rank_name")),
                CardSafe(StringOrNull(found, "next_rank_name")));
        }
        if (data is null) return;
        pendingWelcomeCards[player.SteamID] = data;
        TryShowWelcomeCard(player);
    }

    private void TryShowWelcomeCard(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot || player.TeamNum is not (2 or 3)) return;
        var steamId = player.SteamID;
        if (!pendingWelcomeCards.TryGetValue(steamId, out var data)) return;
        pendingWelcomeCards.Remove(steamId);
        if (welcomeCardShownAt.TryGetValue(steamId, out var last) && DateTime.UtcNow - last < TimeSpan.FromHours(3)) return;
        welcomeCardShownAt[steamId] = DateTime.UtcNow;
        var slot = player.Slot;
        AddTimer(1.5f, () =>
        {
            if (Utilities.GetPlayerFromSlot(slot) is not { IsValid: true } again || again.SteamID != steamId || again.TeamNum is not (2 or 3)) return;
            Server.ExecuteCommand($"lx_welcome_hud {slot} {data.RankId} {data.Exp} {data.Place} {data.RankExp} {data.NextExp} \"{data.RankName}\" \"{data.NextName}\"");
        });
    }
}
