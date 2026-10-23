using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// Who may sit on a team (owner request 2026-10-22). Anyone may choose a side in warmup; ready is a separate step (the "type .r first"
/// rule was removed on 2026-10-23). Once the match has started, a player who walks in is put to spectators, and pressing a team button there does not seat them: it puts them in that
/// team's queue, and the first in line takes the seat the moment one frees up. Matches built from a setup (tournaments, ranked
/// slots) keep their own rules and are left alone.
/// </summary>
public partial class MatchZy
{
    private readonly List<int> legacyXQueueT = new();
    private readonly List<int> legacyXQueueCt = new();

    private void RegisterLegacyXTeamGate()
    {
        AddTimer(1f, LegacyXTeamGateTick, TimerFlags.REPEAT);
    }

    private bool LegacyXTeamGateActive => !isMatchSetup && !isVeto;

    private static bool IsSeatedTeam(int team) => team is (int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist;

    /// <summary>Called first from the jointeam listener; Continue means the other rules decide.</summary>
    private HookResult LegacyXTeamGateJoin(CCSPlayerController? player, int toTeam)
    {
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || !player.UserId.HasValue || !LegacyXTeamGateActive) return HookResult.Continue;
        var userId = player.UserId.Value;

        if (matchStarted && !IsSeatedTeam(player.TeamNum))
        {
            if (!IsSeatedTeam(toTeam))
            {
                if (toTeam == 0) PrintToPlayerChat(player, "The match has started. Choose T or CT to join the queue.");
                return toTeam == 0 ? HookResult.Stop : HookResult.Continue;
            }
            legacyXQueueT.Remove(userId);
            legacyXQueueCt.Remove(userId);
            var queue = toTeam == (int)CsTeam.Terrorist ? legacyXQueueT : legacyXQueueCt;
            queue.Add(userId);
            PrintToPlayerChat(player, $"You are in the {(toTeam == (int)CsTeam.Terrorist ? "T" : "CT")} queue, place {ChatColors.White}{queue.Count}{ChatColors.Grey}. You join when a seat is free.");
            return HookResult.Stop;
        }
        return HookResult.Continue;
    }

    private void OnLegacyXTeamGateConnect(CCSPlayerController player)
    {
        if (!matchStarted || !LegacyXTeamGateActive || player.IsBot || player.IsHLTV) return;
        if (IsMatchCoreEnabled && matchCoreSlotsByOriginal.ContainsKey(CoreSteamId(player))) return;
        AddTimer(1.5f, () =>
        {
            if (!IsPlayerValid(player) || !matchStarted || IsSeatedTeam(player.TeamNum)) return;
            player.ChangeTeam(CsTeam.Spectator);
            PrintToPlayerChat(player, "The match is under way. Choose T or CT to join that team's queue.");
        });
    }

    private void OnLegacyXTeamGateDisconnect(int userId)
    {
        legacyXQueueT.Remove(userId);
        legacyXQueueCt.Remove(userId);
    }

    private void LegacyXTeamGateTick()
    {
        if (!matchStarted || !LegacyXTeamGateActive)
        {
            legacyXQueueT.Clear();
            legacyXQueueCt.Clear();
            return;
        }
        FillFromQueue(legacyXQueueT, CsTeam.Terrorist);
        FillFromQueue(legacyXQueueCt, CsTeam.CounterTerrorist);
    }

    private void FillFromQueue(List<int> queue, CsTeam team)
    {
        if (queue.Count == 0) return;
        var seated = Utilities.GetPlayers().Count(p => IsPlayerValid(p) && !p.IsHLTV && p.TeamNum == (byte)team);
        var perTeam = matchConfig.PlayersPerTeam > 0 ? matchConfig.PlayersPerTeam : 5;
        while (queue.Count > 0 && seated < perTeam)
        {
            var userId = queue[0];
            queue.RemoveAt(0);
            var next = Utilities.GetPlayers().FirstOrDefault(p => IsPlayerValid(p) && p.UserId == userId);
            if (next == null || IsSeatedTeam(next.TeamNum)) continue;
            next.ChangeTeam(team);
            seated++;
            PrintToAllChat($"{ChatColors.White}{next.PlayerName}{ChatColors.Grey} joined {(team == CsTeam.Terrorist ? "T" : "CT")} from the queue.");
        }
    }
}
