using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;

namespace MatchZy;

/// <summary>
/// An abandoned match goes back to warmup by itself (owner request 2026-10-30): when a match (live, knife, side pick, veto or set up)
/// has had no human player on the server for MATCHZY_EMPTY_RESET_SECONDS (default 300; 0 turns it off), it is reset exactly like an
/// admin's css_endmatch, so the next players find an ordinary warmup and not a half-played match. Practice mode is left alone, and the
/// wait is long enough for players who dropped out together to come back to the match they were in.
/// </summary>
public partial class MatchZy
{
    private int legacyXEmptyResetSeconds = 300;
    private DateTime? legacyXEmptySince;

    private void RegisterLegacyXEmptyReset()
    {
        legacyXEmptyResetSeconds = LegacyXEnvironmentLoader.Load().GetModuleInt("MATCHZY", "EMPTY_RESET_SECONDS", 300, 0, 86_400);
        if (legacyXEmptyResetSeconds <= 0) return;
        AddTimer(10f, CheckLegacyXEmptyServer, TimerFlags.REPEAT);
        RegisterListener<Listeners.OnMapStart>(_ => legacyXEmptySince = null);
    }

    private void CheckLegacyXEmptyServer()
    {
        try
        {
            var inMatch = matchStarted || isMatchSetup || isVeto || isPreVeto || isKnifeRound || isSideSelectionPhase;
            if (!inMatch || isPractice) { legacyXEmptySince = null; return; }
            if (Utilities.GetPlayers().Any(player => player.IsValid && !player.IsBot && !player.IsHLTV)) { legacyXEmptySince = null; return; }
            legacyXEmptySince ??= DateTime.UtcNow;
            if ((DateTime.UtcNow - legacyXEmptySince.Value).TotalSeconds < legacyXEmptyResetSeconds) return;
            legacyXEmptySince = null;
            Log($"[LegacyX] No players for {legacyXEmptyResetSeconds} s: the match is reset and the server goes back to warmup.");
            ResetMatch();
        }
        catch (Exception exception)
        {
            Log($"[LegacyX] Empty-server check failed: {exception.Message}");
        }
    }
}
