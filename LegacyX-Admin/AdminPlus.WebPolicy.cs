using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AdminPlus;

public partial class AdminPlus
{
    private WebsiteAdminPolicyClient? _websiteAdminPolicyClient;
    private string _websiteAdminPolicyVersion = string.Empty;

    private void InitializeWebsiteAdminPolicySync()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        if (!environment.GetModuleBoolean("ADMIN", "POLICY_SYNC_ENABLED", false))
        {
            Console.WriteLine("[LEGACY-X Admin] Website admin policy sync is disabled; local generated cache remains unchanged.");
            return;
        }

        var apiBaseUrl = environment.GetModule("ADMIN", "API_BASE_URL").TrimEnd('/');
        var pluginSecret = environment.GetModule("ADMIN", "PLUGIN_SECRET");
        var pluginId = environment.GetModule("ADMIN", "PLUGIN_ID", "legacyx-admin");
        if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(pluginSecret))
        {
            LogError("Website admin policy sync is enabled but LEGACYX_ADMIN_API_BASE_URL or LEGACYX_ADMIN_PLUGIN_SECRET is missing.");
            return;
        }

        _websiteAdminPolicyClient = new WebsiteAdminPolicyClient(apiBaseUrl, pluginId, pluginSecret);
        var refreshSeconds = environment.GetModuleInt("ADMIN", "POLICY_REFRESH_SECONDS", 60, 10, 600);
        AddTimer(3.0f, () => _ = RefreshWebsiteAdminPolicyAsync());
        AddTimer(refreshSeconds, () => _ = RefreshWebsiteAdminPolicyAsync(), TimerFlags.REPEAT);
    }

    private async Task RefreshWebsiteAdminPolicyAsync()
    {
        var client = _websiteAdminPolicyClient;
        if (client == null) return;
        try
        {
            var policy = await client.GetPolicyAsync();
            if (string.IsNullOrWhiteSpace(policy.PolicyVersion) || policy.PolicyVersion == _websiteAdminPolicyVersion) return;

            var root = new JsonObject();
            foreach (var admin in policy.Admins.Where(item => item.IsUsable))
            {
                root[admin.SteamId] = new JsonObject
                {
                    ["identity"] = admin.SteamId,
                    ["name"] = SanitizeName(admin.Username),
                    ["staffRole"] = admin.Role,
                    ["stamina"] = admin.Stamina,
                    ["immunity"] = admin.Immunity,
                    ["flags"] = new JsonArray(admin.Permissions.Select(permission => (JsonNode?)permission).ToArray())
                };
            }

            WriteAdminsFile(root);
            LoadImmunity();
            _websiteAdminPolicyVersion = policy.PolicyVersion;
            Console.WriteLine($"[LEGACY-X Admin] Website admin policy synchronized: {root.Count} active entries.");
        }
        catch (Exception ex)
        {
            LogError($"Website admin policy sync failed; retaining the last generated cache: {ex.Message}");
        }
    }

    private static void SendWebsitePolicyManagedMessage(CounterStrikeSharp.API.Core.CCSPlayerController? caller)
    {
        const string message = "{green}LEGACY-X • {default}ADMIN ACCESS IS MANAGED FROM THE WEBSITE STAFF PANEL";
        if (caller != null && caller.IsValid) caller.Print(message);
        else Console.WriteLine("[LEGACY-X Admin] Admin access is managed from the website Staff Panel.");
    }
}
