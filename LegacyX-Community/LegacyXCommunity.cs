using System.Net.Http.Headers;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.UserMessages;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

public sealed class LegacyXCommunityConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-community";
    public string PluginSecret { get; set; } = "";
    public string ChatPrefix { get; set; } = LegacyXChat.Prefix;
    public bool ScoreboardRanks { get; set; } = true;
}

public sealed class LegacyXCommunity : BasePlugin, IPluginConfig<LegacyXCommunityConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    public required LegacyXCommunityConfig Config { get; set; }
    // SteamID64 -> LEGACY-X rank (1-18) and ranked matches, from the API; re-applied every round
    // because the game resets the scoreboard fields.
    private readonly Dictionary<ulong, (int RankId, int Matches, int Exp, string Name)> scoreboardRanks = new();
    // The rank name is always the clan tag, shown as [OPERATOR I]. On top of it, the rank column can try
    // 12 = skill-group icon or 11 = Premier rating (EXP); 0 = tag only (default). Switchable live with lx_scoreboard_type.
    private int scoreboardRankType = 0;
    private readonly HashSet<ulong> scoreboardLogged = new();
    // Slots holding Tab last tick: the client only draws rank icons after a reveal sent while Tab is open.
    private readonly HashSet<int> scoreboardOpen = new();
    private bool scoreboardBlocked;

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
        config.ScoreboardRanks = environment.GetModuleBoolean("COMMUNITY", "SCOREBOARD_RANKS", config.ScoreboardRanks);
        scoreboardRankType = environment.GetModuleInt("COMMUNITY", "SCOREBOARD_RANK_TYPE", 0, 0, 12);
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] Loaded — rank and EXP command ready.");
        if (!Config.Enabled || !Config.ScoreboardRanks) return;
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid && !player.IsBot) _ = LoadScoreboardRankAsync(player);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid) ApplyScoreboardRank(player);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            foreach (var player in Utilities.GetPlayers()) ApplyScoreboardRank(player);
            RevealScoreboardRanks();
            return HookResult.Continue;
        });
        // A finished match can move ranks: read them again for everyone still on the server.
        RegisterEventHandler<EventCsWinPanelMatch>((_, _) =>
        {
            foreach (var player in Utilities.GetPlayers().Where(player => !player.IsBot)) _ = LoadScoreboardRankAsync(player);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid is { } player)
            {
                scoreboardRanks.Remove(player.SteamID);
                scoreboardOpen.Remove(player.Slot);
            }
            return HookResult.Continue;
        });
        // The game rewrites these fields (team changes, match state); keep them set, as other CS2 rank plugins do.
        AddTimer(2.0f, () =>
        {
            foreach (var online in Utilities.GetPlayers()) ApplyScoreboardRank(online);
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsBot) continue;
                var open = (player.Buttons & PlayerButtons.Scoreboard) != 0;
                if (open && scoreboardOpen.Add(player.Slot)) RevealScoreboardRanks(player);
                else if (!open) scoreboardOpen.Remove(player.Slot);
            }
        });
    }

    /// <summary>Tab scoreboard: the player's LEGACY-X rank (1-18) in the rank column, as the 18-step skill-group icon.</summary>
    private async Task LoadScoreboardRankAsync(CCSPlayerController player)
    {
        var steamId = player.SteamID;
        if (string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret)) return;
        var (status, profile) = await FetchProfileAsync(steamId.ToString());
        if (status == System.Net.HttpStatusCode.NotFound)
        {
            Server.NextFrame(() => scoreboardRanks.Remove(steamId));
            return;
        }
        if (profile is not { } found) return;
        var rankId = IntOrNull(found, "rank_id") ?? 0;
        var matches = IntOrNull(found, "matches_completed") ?? 0;
        var exp = IntOrNull(found, "current_exp") ?? 0;
        var rankName = StringOrNull(found, "rank_name")?.Trim() ?? "";
        Server.NextFrame(() =>
        {
            if (rankId is >= 1 and <= 18 && rankName.Length > 0) scoreboardRanks[steamId] = (rankId, matches, exp, rankName);
            else scoreboardRanks.Remove(steamId);
            if (player.IsValid) ApplyScoreboardRank(player);
            RevealScoreboardRanks();
        });
    }

    private void ApplyScoreboardRank(CCSPlayerController player)
    {
        if (scoreboardBlocked || !player.IsValid || player.IsBot || !scoreboardRanks.TryGetValue(player.SteamID, out var rank)) return;
        try
        {
            ApplyRankTag(player, rank.Name);
            if (scoreboardRankType == 0) return;
            player.CompetitiveRankType = (sbyte)scoreboardRankType;
            player.CompetitiveRanking = scoreboardRankType == 11 ? rank.Exp : rank.RankId;
            // The client hides a skill group below 10 wins; the rank itself is already earned on the site.
            player.CompetitiveWins = Math.Max(10, rank.Matches);
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveWins");
            if (scoreboardLogged.Add(player.SteamID))
                Console.WriteLine($"[{ModuleName}] Tab rank for {player.SteamID}: type {scoreboardRankType}, value {player.CompetitiveRanking}.");
        }
        catch (Exception exception)
        {
            // CounterStrikeSharp's FollowCS2ServerGuidelines (configs/core.json) blocks these fields; say it once, then stop trying.
            scoreboardBlocked = true;
            Console.WriteLine($"[{ModuleName}] Tab rank icons are off: {exception.Message} Set \"FollowCS2ServerGuidelines\": false in addons/counterstrikesharp/configs/core.json to allow them.");
        }
    }

    /// <summary>The rank name as the player's clan tag: shown before the name in Tab, chat and the kill feed.</summary>
    private void ApplyRankTag(CCSPlayerController player, string rankName)
    {
        // The scoreboard puts the clan tag in brackets itself: [OPERATOR I] 777.
        var tag = rankName.ToUpperInvariant();
        if (tag.Length > 31) tag = tag[..31];
        var current = player.Clan ?? "";
        // MatchZy's coach tag ([TEAM COACH]) says more during a match; leave it.
        if (current.EndsWith("COACH]", StringComparison.Ordinal) || current == tag) return;
        player.Clan = tag;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
        if (scoreboardLogged.Add(player.SteamID))
            Console.WriteLine($"[{ModuleName}] Rank tag for {player.SteamID}: {tag}.");
    }

    /// <summary>Asks clients (one, or everyone) to show every player's rank in the Tab scoreboard.</summary>
    private void RevealScoreboardRanks(CCSPlayerController? player = null)
    {
        try
        {
            var message = UserMessage.FromPartialName("ServerRankRevealAll");
            if (player != null) message.Recipients.Add(player);
            else message.Recipients.AddAllPlayers();
            message.Send();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Scoreboard rank reveal unavailable: {exception.Message}");
        }
    }

    /// <summary>Server console: lx_scoreboard_type 0 (rank tag), 12 (skill-group icon) or 11 (Premier number), for everyone at once.</summary>
    [ConsoleCommand("lx_scoreboard_type", "Tab rank display: 0 = rank tag, 12 = skill-group icons, 11 = Premier rating (EXP)")]
    public void OnScoreboardType(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return; // server console / RCON only
        if (!int.TryParse(command.GetArg(1), out var type) || type is < 0 or > 12)
        {
            command.ReplyToCommand($"[{ModuleName}] lx_scoreboard_type is {scoreboardRankType}. Use 0 (rank tag), 12 (skill-group icons) or 11 (Premier rating).");
            return;
        }
        scoreboardRankType = type;
        scoreboardLogged.Clear();
        foreach (var online in Utilities.GetPlayers()) ApplyScoreboardRank(online);
        RevealScoreboardRanks();
        command.ReplyToCommand($"[{ModuleName}] Tab rank type set to {type} for {scoreboardRanks.Count} ranked player(s). Open Tab to check.");
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

        var (status, found) = await FetchProfileAsync(player.SteamID.ToString());
        if (status == System.Net.HttpStatusCode.NotFound)
        {
            Print(player, "No LEGACY-X rank yet. Sign in on the website and finish a ranked 5v5 match.");
            return;
        }
        if (found is not { } profile)
        {
            Print(player, "Community profile is temporarily unavailable.");
            return;
        }
        try
        {
            // Rank and EXP come from the API (competitive_leaderboard); the plugin never calculates them.
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

    /// <summary>GET /plugin/community/players/:steamId. Profile is null when the API has none or can't be reached.</summary>
    private async Task<(System.Net.HttpStatusCode? Status, JsonElement? Profile)> FetchProfileAsync(string steamId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Config.ApiBaseUrl}/api/v1/plugin/community/players/{steamId}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (response.StatusCode, null);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return (response.StatusCode, document.RootElement.GetProperty("profile").Clone());
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Profile lookup failed: {exception.Message}");
            return (null, null);
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
            if (player.IsValid) player.PrintToChat(LegacyXChat.System(message));
        });
    }
}
