using System.Net.Http.Headers;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

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
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] Loaded — EXP, level and clan profile commands ready.");
    }

    [ConsoleCommand("css_xp", "Shows your LEGACY-X XP, level and competitive rank")]
    [ConsoleCommand("css_level", "Shows your LEGACY-X XP, level and competitive rank")]
    [ConsoleCommand("css_progress", "Shows your LEGACY-X XP, level and competitive rank")]
    public void OnProgress(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        _ = SendProfileAsync(player, false);
    }

    [ConsoleCommand("css_clan", "Shows your LEGACY-X clan membership and season contribution")]
    public void OnClan(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        _ = SendProfileAsync(player, true);
    }

    private async Task SendProfileAsync(CCSPlayerController player, bool clanOnly)
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
                Print(player, "No completed LEGACY-X match history yet. Finish a 5v5 map to earn XP.");
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                Print(player, "Community profile is temporarily unavailable.");
                return;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var profile = document.RootElement.GetProperty("profile");
            var level = profile.GetProperty("level").GetInt32();
            var xp = profile.GetProperty("experience").GetInt32();
            var rating = profile.GetProperty("rating").GetInt32();
            var tier = profile.GetProperty("rank_tier").GetString() ?? "rookie";
            var clanTag = profile.TryGetProperty("clan_tag", out var tag) && tag.ValueKind == JsonValueKind.String ? tag.GetString() : null;
            var clanName = profile.TryGetProperty("clan_name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
            var clanRole = profile.TryGetProperty("clan_role", out var role) && role.ValueKind == JsonValueKind.String ? role.GetString() : null;

            if (clanOnly)
            {
                Print(player, string.IsNullOrWhiteSpace(clanTag)
                    ? "You are not in a LEGACY-X clan yet. Clan creation is managed through the community API."
                    : $"Clan [{clanTag}] {clanName} — role: {clanRole}. Season points are earned from completed 5v5 maps.");
                return;
            }

            var clan = string.IsNullOrWhiteSpace(clanTag) ? "No clan" : $"[{clanTag}] {clanName}";
            Print(player, $"Level {level} · {xp} XP · {tier} {rating} rating · {clan}");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Profile lookup failed: {exception.Message}");
            Print(player, "Community profile is temporarily unavailable.");
        }
    }

    private void Print(CCSPlayerController player, string message)
    {
        Server.NextFrame(() =>
        {
            if (player.IsValid) player.PrintToChat($"{Config.ChatPrefix} {message}");
        });
    }
}
