using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.TeamWindow;

namespace AdminPlus;

/// <summary>
/// !team and !swap never move a living player in the middle of a round (owner request 2026-10-25): the move waits for freeze time,
/// buy time or a pause (LegacyXTeamWindow) and happens then. A dead player or a spectator moves at once, since that changes no round.
/// </summary>
public partial class AdminPlus
{
    private readonly Dictionary<ulong, CsTeam> _pendingTeamMoves = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _teamMoveTimer;

    private void MoveTeamWhenAllowed(CCSPlayerController player, CsTeam team)
    {
        var alive = player.PawnIsAlive && player.Team is (CsTeam.Terrorist or CsTeam.CounterTerrorist);
        if (!alive || LegacyXTeamWindow.IsOpen())
        {
            _pendingTeamMoves.Remove(player.SteamID);
            player.ChangeTeam(team);
            return;
        }
        _pendingTeamMoves[player.SteamID] = team;
        player.PrintToChat($" You move to {(team == CsTeam.Terrorist ? "T" : team == CsTeam.CounterTerrorist ? "CT" : "spectators")} {LegacyXTeamWindow.WaitHint}.");
        _teamMoveTimer ??= AddTimer(1f, ApplyPendingTeamMoves, TimerFlags.REPEAT);
    }

    private void ApplyPendingTeamMoves()
    {
        if (_pendingTeamMoves.Count == 0) return;
        var open = LegacyXTeamWindow.IsOpen();
        foreach (var (steamId, team) in _pendingTeamMoves.ToList())
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
            if (player == null) { _pendingTeamMoves.Remove(steamId); continue; }
            if (!open && player.PawnIsAlive) continue;
            _pendingTeamMoves.Remove(steamId);
            if (player.Team != team) player.ChangeTeam(team);
        }
    }
}
