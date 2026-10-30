using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

public partial class MatchZy
{
    private CounterStrikeSharp.API.Modules.Timers.Timer? legacyXMatchOverlayTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? legacyXMatchOverlayStopTimer;
    private bool legacyXPostMatchSummarySent;

    /// <summary>" #12": the match's number from the site (1, 2, 3 ... over real matches, the same on every server); empty while it is not known, so a
    /// match the site did not register shows no number at all instead of a made-up one.</summary>
    private string LegacyXMatchLabel() => matchCoreNumber.HasValue ? $" #{matchCoreNumber.Value}" : "";

    private void ShowLegacyXWelcome(CCSPlayerController player)
    {
        if (!legacyXQuietOverlayEnabled.Value || !IsPlayerValid(player) || player.IsBot || player.IsHLTV) return;
        player.PrintToCenter($"LEGACY-X\n5v5 · Match{LegacyXMatchLabel()}");
    }

    private void StartLegacyXMatchOverlay()
    {
        if (!legacyXQuietOverlayEnabled.Value) return;
        legacyXPostMatchSummarySent = false;
        KillLegacyXMatchOverlay();
        ShowLegacyXMatchOverlay();
        legacyXMatchOverlayTimer = AddTimer(8.0f, ShowLegacyXMatchOverlay, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        legacyXMatchOverlayStopTimer = AddTimer(legacyXMatchOverlayDuration.Value, KillLegacyXMatchOverlay);
    }

    private void ShowLegacyXMatchOverlay()
    {
        if (!isMatchLive)
        {
            KillLegacyXMatchOverlay();
            return;
        }
        var overlay = $"LEGACY-X · Match{LegacyXMatchLabel()} · Live";
        foreach (var player in Utilities.GetPlayers().Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV))
        {
            player.PrintToCenter(overlay);
        }
    }

    private void KillLegacyXMatchOverlay()
    {
        legacyXMatchOverlayTimer?.Kill();
        legacyXMatchOverlayStopTimer?.Kill();
        legacyXMatchOverlayTimer = null;
        legacyXMatchOverlayStopTimer = null;
    }

    private void ShowLegacyXPostMatchSummary(string winnerTeam)
    {
        if (legacyXPostMatchSummarySent) return;
        legacyXPostMatchSummarySent = true;
        KillLegacyXMatchOverlay();
        var outcome = winnerTeam switch
        {
            "team1" => $"{matchzyTeam1.teamName} won",
            "team2" => $"{matchzyTeam2.teamName} won",
            _ => "Draw",
        };
        PrintToAllChat($"Final: {ChatColors.White}{outcome}{ChatColors.Grey}.");
    }
}
