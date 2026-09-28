using LegacyX.Shared.Configuration;

namespace MatchZy;

public partial class MatchZy
{
    private bool legacyXCentralEnabled = true;
    // Rank events (map results → EXP/rank on the website) straight from CounterStrikeSharp/.env:
    // no private cfg needed. Used whenever the match config carries no remote log URL of its own.
    private string legacyXRankEventsUrl = "";
    private string legacyXRankToken = "";

    private void ApplyLegacyXCentralEnvironment()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        legacyXCentralEnabled = environment.GetModuleBoolean("MATCHZY", "ENABLED", true);
        // One server token (LEGACYX_PLUGIN_TOKEN, from scripts/create-game-server.mjs) serves every module.
        var apiBase = environment.Get("LEGACYX_API_BASE_URL").Trim().TrimEnd('/');
        var sharedToken = environment.Get("LEGACYX_PLUGIN_TOKEN").Trim();
        legacyXMatchCorePluginSecret.Value = environment.GetModule("MATCHZY", "MATCH_CORE_PLUGIN_TOKEN", sharedToken.Length > 0 ? sharedToken : legacyXMatchCorePluginSecret.Value).Trim();
        legacyXMatchCoreApiUrl.Value = environment.GetModule("MATCHZY", "MATCH_CORE_API_URL", apiBase.Length > 0 ? $"{apiBase}/api/v1/plugin/match-core/events" : legacyXMatchCoreApiUrl.Value).Trim();
        legacyXMatchCoreEnabled.Value = environment.GetModuleBoolean("MATCHZY", "MATCH_CORE_ENABLED", legacyXMatchCorePluginSecret.Value.Length > 0 && legacyXMatchCoreApiUrl.Value.Length > 0);
        var rankToken = environment.GetModule("MATCHZY", "RANK_TOKEN", sharedToken).Trim();
        var rankEnabled = environment.GetModuleBoolean("MATCHZY", "RANK_ENABLED", true);
        legacyXRankEventsUrl = rankEnabled && apiBase.Length > 0 && rankToken.Length > 0 ? $"{apiBase}/api/v1/plugin/matchzy/events" : "";
        legacyXRankToken = rankEnabled ? rankToken : "";
        legacyXMatchCoreServerId.Value = environment.Get("LEGACYX_SERVER_ID", legacyXMatchCoreServerId.Value).Trim();
        SetLegacyXMatchMode(environment.Get("LEGACYX_SERVER_MODE", "competitive_5v5"));
    }
}
