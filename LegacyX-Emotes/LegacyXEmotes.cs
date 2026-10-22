using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using FortniteEmotes.API;
using LegacyX.Shared.Configuration;

namespace LegacyXEmotes;

public sealed class LegacyXEmotesConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-community";
    public string PluginSecret { get; set; } = "";
    /// <summary>Owners and root admins may play any emote (to test them) without owning it.</summary>
    public bool RootPlaysAll { get; set; } = true;
}

/// <summary>
/// Sells the animated emotes of FortniteEmotesNDances (owner request 2026-10-22). That plugin plays them; this one only decides who may.
/// It listens to its OnPlayerEmotePre, and stops an emote the player has not bought in the Shop. What a player owns comes from the API
/// (/plugin/community/players/:steamId, field emotes.owned), by the emote's name in the plugin's config. Closed by default: with the API
/// unreachable or unconfigured nobody but root plays an emote. lx_emotes_sql writes the Shop rows for every emote the server has.
/// </summary>
public sealed class LegacyXEmotes : BasePlugin, IPluginConfig<LegacyXEmotesConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly PluginCapability<IFortniteEmotesAPI> capability = new("FortniteEmotes");
    private readonly ConcurrentDictionary<ulong, HashSet<string>> owned = new();
    private readonly ConcurrentDictionary<ulong, DateTime> refreshedAt = new();
    private IFortniteEmotesAPI? api;

    public required LegacyXEmotesConfig Config { get; set; }
    public override string ModuleName => "LEGACY-X Emotes";
    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXEmotesConfig config)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        config.Enabled = environment.GetModuleBoolean("EMOTES", "ENABLED", config.Enabled);
        config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl).TrimEnd('/');
        // The same plugin credentials the Community plugin uses, unless EMOTES has its own.
        config.PluginId = environment.GetModule("EMOTES", "PLUGIN_ID", environment.GetModule("COMMUNITY", "PLUGIN_ID", config.PluginId));
        config.PluginSecret = environment.GetModule("EMOTES", "PLUGIN_TOKEN", environment.GetModule("COMMUNITY", "PLUGIN_TOKEN", config.PluginSecret));
        config.RootPlaysAll = environment.GetModuleBoolean("EMOTES", "ROOT_PLAYS_ALL", config.RootPlaysAll);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] Loaded.");
        if (!Config.Enabled) return;
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            if (@event.Userid is { IsValid: true, IsBot: false } player) _ = RefreshAsync(player.SteamID);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid is { } player) { owned.TryRemove(player.SteamID, out _); refreshedAt.TryRemove(player.SteamID, out _); }
            return HookResult.Continue;
        });
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        if (!Config.Enabled) return;
        api = capability.Get();
        if (api == null)
        {
            Console.WriteLine($"[{ModuleName}] FortniteEmotesNDances is not loaded, so there is nothing to sell yet.");
            return;
        }
        api.OnPlayerEmotePre += OnEmotePre;
    }

    public override void Unload(bool hotReload)
    {
        if (api != null) api.OnPlayerEmotePre -= OnEmotePre;
    }

    private bool Configured => !string.IsNullOrWhiteSpace(Config.ApiBaseUrl) && !string.IsNullOrWhiteSpace(Config.PluginSecret);

    private HookResult OnEmotePre(CCSPlayerController player, Emote emote)
    {
        if (player.IsBot) return HookResult.Continue;
        if (Config.RootPlaysAll && AdminManager.PlayerHasPermissions(player, "@css/root")) return HookResult.Continue;

        if (owned.TryGetValue(player.SteamID, out var keys) && keys.Contains(emote.Name)) return HookResult.Continue;

        // Not owned, or not loaded yet. Read the list again (at most every 5 seconds) so a purchase made a moment ago counts on the next try.
        if (Configured && (!refreshedAt.TryGetValue(player.SteamID, out var last) || DateTime.UtcNow - last > TimeSpan.FromSeconds(5))) _ = RefreshAsync(player.SteamID);
        player.PrintToChat(Configured
            ? $" {ChatColors.White}LEGACY-X •{ChatColors.Grey} You don't own this {(emote.IsEmote ? "emote" : "dance")}. Get it in the Shop on the website."
            : $" {ChatColors.White}LEGACY-X •{ChatColors.Grey} Emotes aren't available right now.");
        return HookResult.Handled;
    }

    private async Task RefreshAsync(ulong steamId)
    {
        if (!Configured) return;
        refreshedAt[steamId] = DateTime.UtcNow;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Config.ApiBaseUrl}/api/v1/plugin/community/players/{steamId}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (document.RootElement.GetProperty("profile").TryGetProperty("emotes", out var emotes)
                && emotes.TryGetProperty("owned", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in list.EnumerateArray()) if (entry.GetString() is { Length: > 0 } key) keys.Add(key);
            }
            owned[steamId] = keys;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Emote list lookup failed: {exception.Message}");
        }
    }

    [ConsoleCommand("lx_emotes_sql", "Server console: writes legacyx-emotes-shop.sql with a Shop row for every emote and dance this server has")]
    public void OnWriteSql(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return;
        if (api == null) { command.ReplyToCommand($"[{ModuleName}] FortniteEmotesNDances is not loaded."); return; }
        var rows = new List<string>();
        var used = new HashSet<string>();
        var sort = 100;
        foreach (var emote in api.GetEmoteList().Concat(api.GetDanceList()))
        {
            var slug = new string(emote.Name.ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            var id = ("fn-" + slug);
            if (id.Length > 40) id = id[..40].TrimEnd('-');
            if (id.Length < 4 || !used.Add(id) || emote.Name.Length is < 2 or > 40) continue;
            var name = emote.Name.Replace("'", "''");
            // Dances cost more than short emotes; rarity follows. Edit the price in the SQL (or the Staff panel) before running it.
            var price = emote.IsEmote ? 300 : 500;
            var rarity = emote.IsEmote ? 2 : 3;
            rows.Add($"  ('{id}', 'emote', '{name}', '{name}', 'coin', {price}, '', {sort++}, '{name}', {rarity})");
        }
        var path = Path.Combine(ModuleDirectory, "legacyx-emotes-shop.sql");
        File.WriteAllText(path, "-- Shop rows for every emote and dance on the server. Check the prices, then run it in Supabase after legacy_x_cosmetics_emotes.sql.\n"
            + "INSERT INTO legacy_x.cosmetic_items (id, kind, name_en, name_mn, unlock, price, requirement, sort, game_key, rarity) VALUES\n"
            + string.Join(",\n", rows) + "\nON CONFLICT (id) DO NOTHING;\n", new UTF8Encoding(false));
        command.ReplyToCommand($"[{ModuleName}] {rows.Count} rows written to {path}");
    }
}
