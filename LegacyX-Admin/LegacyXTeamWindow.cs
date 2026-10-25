using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace LegacyX.TeamWindow;

/// <summary>
/// (The same file lives in LegacyX-MatchZy and LegacyX-Admin: the shared project has no CounterStrikeSharp reference.)
/// When a player may be moved to another team during a match (owner request 2026-10-25): never in the middle of a round, only in
/// warmup, freeze time, buy time or while the match is paused (a pause, a tactical or technical timeout). LegacyX-MatchZy seats queued
/// players and LegacyX-Admin applies !team / !swap only inside this window; outside it the move waits and happens as soon as it opens.
/// </summary>
public static class LegacyXTeamWindow
{
    public static bool IsOpen()
    {
        CCSGameRules? rules;
        try
        {
            rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        }
        catch
        {
            return true;
        }
        if (rules == null) return true;
        if (rules.WarmupPeriod || rules.FreezePeriod) return true;
        if (rules.GamePaused || rules.TerroristTimeOutActive || rules.CTTimeOutActive || rules.MatchWaitingForResume) return true;
        // Buy time: mp_buytime seconds from the end of freeze time (when the round actually starts).
        var buyTime = ConVar.Find("mp_buytime")?.GetPrimitiveValue<float>() ?? 20f;
        return Server.CurrentTime - rules.RoundStartTime <= buyTime;
    }

    /// <summary>What to tell players while a move waits.</summary>
    public const string WaitHint = "at the next freeze time (or during buy time / a pause)";
}
