using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// <c>css_setscore &lt;T&gt; &lt;CT&gt;</c> (owner request 2026-11-07): sets the round score of the running match, from the staff site's server console or by staff in game.
/// The score MatchZy reports at the end is read from the game's two team entities (<c>GetTeamsScore</c>), so this writes the score there.
/// The numbers are the sides as they are now: the first is the Terrorists, the second the Counter-Terrorists.
/// </summary>
public partial class MatchZy
{
    [ConsoleCommand("css_setscore", "Sets the round score: css_setscore <T score> <CT score>")]
    public void OnSetScoreCommand(CCSPlayerController? player, CommandInfo? command)
    {
        if (!IsPlayerAdmin(player, "css_setscore", "@css/config", "@legacyx/match"))
        {
            SendPlayerNotAdminMessage(player);
            return;
        }
        if (!matchStarted || isPractice)
        {
            ReplyToUserCommand(player, "There is no running match to change the score of.");
            return;
        }
        if (command == null || command.ArgCount < 3 || !int.TryParse(command.GetArg(1), out var terrorists) || !int.TryParse(command.GetArg(2), out var counterTerrorists)
            || terrorists < 0 || counterTerrorists < 0 || terrorists > 60 || counterTerrorists > 60)
        {
            ReplyToUserCommand(player, "Use: !setscore <T score> <CT score>, each from 0 to 60.");
            return;
        }
        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
        {
            var score = team.TeamNum == (byte)CsTeam.Terrorist ? terrorists : team.TeamNum == (byte)CsTeam.CounterTerrorist ? counterTerrorists : -1;
            if (score < 0) continue;
            team.Score = score;
            Utilities.SetStateChanged(team, "CTeam", "m_iScore");
        }
        PrintToAllChat($"Staff set the score: T {terrorists} - {counterTerrorists} CT.");
        Log($"[SetScore] {(player?.PlayerName ?? "console")} set the score to T {terrorists}, CT {counterTerrorists}");
    }
}
