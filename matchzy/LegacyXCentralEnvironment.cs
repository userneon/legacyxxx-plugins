using LegacyX.Shared.Configuration;

namespace MatchZy;

public partial class MatchZy
{
    private bool legacyXCentralEnabled = true;

    private void ApplyLegacyXCentralEnvironment()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        legacyXCentralEnabled = environment.GetModuleBoolean("MATCHZY", "ENABLED", true);
        legacyXMatchCoreEnabled.Value = environment.GetModuleBoolean("MATCHZY", "MATCH_CORE_ENABLED", legacyXMatchCoreEnabled.Value);
        legacyXMatchCoreApiUrl.Value = environment.GetModule("MATCHZY", "MATCH_CORE_API_URL", legacyXMatchCoreApiUrl.Value).Trim();
        legacyXMatchCorePluginSecret.Value = environment.GetModule("MATCHZY", "MATCH_CORE_PLUGIN_TOKEN", legacyXMatchCorePluginSecret.Value).Trim();
        legacyXMatchCoreServerId.Value = environment.Get("LEGACYX_SERVER_ID", legacyXMatchCoreServerId.Value).Trim();
    }
}
