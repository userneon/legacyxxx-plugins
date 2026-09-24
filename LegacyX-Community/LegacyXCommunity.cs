using System.Net.Http.Headers;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

public sealed class LegacyXCommunityConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-community";
    public string PluginSecret { get; set; } = "";
    public string ChatPrefix { get; set; } = "{Lime}[LEGACY-X]{Default}";
}

public sealed class LegacyXCommunity : BasePlugin, IPluginConfig<LegacyXCommunityConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    public required LegacyXCommunityConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Community";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXCommunityConfig config)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        config.Enabled = environment.GetModuleBoolean("COMMUNITY", "ENABLED", config.Enabled);
        config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl);
        config.PluginId = environment.GetModule("COMMUNITY", "PLUGIN_ID", config.PluginId);
        config.PluginSecret = environment.GetModule("COMMUNITY", "PLUGIN_TOKEN", config.PluginSecret);
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] Loaded — rank and EXP command ready.");
    }

    [ConsoleCommand("css_rank", "Shows your LEGACY-X rank, EXP and leaderboard position")]
    [ConsoleCommand("css_xp", "Shows your LEGACY-X rank, EXP and leaderboard position")]
    [ConsoleCommand("css_level", "Shows your LEGACY-X rank, EXP and leaderboard position")]
    [ConsoleCommand("css_progress", "Shows your LEGACY-X rank, EXP and leaderboard position")]
    public void OnProgress(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        _ = SendProfileAsync(player);
    }

    private async Task SendProfileAsync(CCSPlayerController player)
    {
        if (!Config.Enabled)
        {
            Print(player, "Community profile is disabled on this server.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret))
        {
            Print(player, "Community profile is not configured yet.");
            return;
        }

        var steamId = player.SteamID.ToString();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Config.ApiBaseUrl}/api/v1/plugin/community/players/{steamId}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Print(player, "No LEGACY-X rank yet. Sign in on the website and finish a ranked 5v5 match.");
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                Print(player, "Community profile is temporarily unavailable.");
                return;
            }

            // Rank and EXP come from the API (competitive_leaderboard); the plugin never calculates them.
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var profile = document.RootElement.GetProperty("profile");
            var rankName = StringOrNull(profile, "rank_name") ?? "Unranked";
            var exp = IntOrNull(profile, "current_exp") ?? 0;
            var position = IntOrNull(profile, "position");
            var nextName = StringOrNull(profile, "next_rank_name");
            var nextExp = IntOrNull(profile, "next_rank_min_exp");
            var proLeague = profile.TryGetProperty("pro_league_unlocked", out var unlocked) && unlocked.ValueKind == JsonValueKind.True;

            var parts = new List<string> { rankName, $"{exp:N0} EXP" };
            if (position.HasValue) parts.Add($"#{position.Value:N0}");
            parts.Add(nextName != null && nextExp.HasValue ? $"{Math.Max(0, nextExp.Value - exp):N0} EXP to {nextName}" : "Highest rank");
            parts.Add(proLeague ? "Pro League unlocked" : "Pro League at Vanguard I");
            Print(player, string.Join(" · ", parts));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Profile lookup failed: {exception.Message}");
            Print(player, "Community profile is temporarily unavailable.");
        }
    }

    private static string? StringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Postgres numerics can arrive as numbers or strings.</summary>
    private static int? IntOrNull(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return (int)Math.Round(number);
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return (int)Math.Round(parsed);
        return null;
    }

    private void Print(CCSPlayerController player, string message)
    {
        Server.NextFrame(() =>
        {
            if (player.IsValid) player.PrintToChat($"{Config.ChatPrefix} {message}");
        });
    }
}
