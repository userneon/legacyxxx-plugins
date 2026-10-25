using System.Net.Http.Headers;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
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
    public bool Welcome { get; set; } = true;
    /// <summary>What .discord tells a player; set LEGACYX_COMMUNITY_DISCORD_URL to change it.</summary>
    public string DiscordUrl { get; set; } = "https://discord.gg/legacyx";
    /// <summary>A player in a LEGACY-X clan shows the clan tag before the rank in Tab: [WOLF | OPERATOR I].</summary>
    public bool ClanTag { get; set; } = true;
    /// <summary>Killing the player who killed you last tells both of you "revenge" in the chat.</summary>
    public bool Revenge { get; set; } = true;
    /// <summary>.tp turns on a third person view. Off by default: it is for Fun servers (LEGACYX_27016_COMMUNITY_THIRDPERSON=true), never ranked play.</summary>
    public bool ThirdPerson { get; set; }
    public bool SmokeGlow { get; set; } = true;
    /// <summary>A one-line Discord invite in everyone's chat every this many minutes (0 = never).</summary>
    public int DiscordAdMinutes { get; set; } = 10;
    /// <summary>The game's money lines in chat: "prefix" (one short LEGACY-X line), "hide" or "game" (left as they are); LEGACYX_&lt;port&gt;_COMMUNITY_MONEY_LINES per server.</summary>
    public string MoneyLines { get; set; } = "prefix";
}

public sealed partial class LegacyXCommunity : BasePlugin, IPluginConfig<LegacyXCommunityConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    public required LegacyXCommunityConfig Config { get; set; }
    // SteamID64 -> LEGACY-X rank (1-18) and ranked matches, from the API; re-applied every round
    // because the game resets the scoreboard fields.
    private readonly Dictionary<ulong, (int RankId, int Matches, int Exp, string Name, string Clan)> scoreboardRanks = new();
    // With type 0 the rank name is the clan tag, shown as [OPERATOR I]; with a picture type it is left out (only a clan tag stays). The rank column shows our own rank picture:
    // type 12 with the ranking set to 18 + the rank (19-36), which are the names of the addon's skillgroup19-36.svg. Those names
    // do not exist in CS2 itself, so the game cannot answer with its own picture (ranking 1-18 gave the CS:GO icons, never ours).
    // 11 = Premier rating (EXP); 0 = tag only. Switchable live with lx_scoreboard_type.
    private int scoreboardRankType = 12;
    // The first of our 18 picture numbers is 19: the game's own are 1-18.
    private int scoreboardIconOffset = 18;
    private readonly HashSet<ulong> scoreboardLogged = new();
    // Slots holding Tab last tick: the client only draws rank icons after a reveal sent while Tab is open.
    private readonly HashSet<int> scoreboardOpen = new();
    private bool scoreboardBlocked;
    // victim SteamID64 -> SteamID64 of whoever killed them last, on this map. Taking revenge clears it.
    private readonly Dictionary<ulong, ulong> lastKiller = new();
    // (killer, victim) -> how many times the first killed the second on this map; the "3 vs 2" of the revenge line.
    private readonly Dictionary<(ulong, ulong), int> duelKills = new();
    // SteamID64 -> when the player last got the welcome, so a map change does not repeat it.
    private readonly Dictionary<ulong, DateTime> welcomed = new();

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
        config.Welcome = environment.GetModuleBoolean("COMMUNITY", "WELCOME", config.Welcome);
        config.DiscordUrl = environment.GetModule("COMMUNITY", "DISCORD_URL", config.DiscordUrl);
        config.ClanTag = environment.GetModuleBoolean("COMMUNITY", "CLAN_TAG", config.ClanTag);
        config.Revenge = environment.GetModuleBoolean("COMMUNITY", "REVENGE", config.Revenge);
        config.ThirdPerson = environment.GetModuleBoolean("COMMUNITY", "THIRDPERSON", false);
        config.SmokeGlow = environment.GetModuleBoolean("COMMUNITY", "SMOKEGLOW", true);
        config.DiscordAdMinutes = environment.GetModuleInt("COMMUNITY", "DISCORD_AD_MINUTES", config.DiscordAdMinutes, 0, 120);
        config.MoneyLines = environment.GetModule("COMMUNITY", "MONEY_LINES", config.MoneyLines).Trim().ToLowerInvariant();
        scoreboardRankType = environment.GetModuleInt("COMMUNITY", "SCOREBOARD_RANK_TYPE", 12, 0, 12);
        scoreboardIconOffset = environment.GetModuleInt("COMMUNITY", "SCOREBOARD_ICON_OFFSET", 18, 0, 18);
        Config = config;
        Config.ApiBaseUrl = Config.ApiBaseUrl.TrimEnd('/');
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] Loaded — rank and EXP command ready.");
        if (!Config.Enabled) return;
        if (Config.ThirdPerson) RegisterThirdPerson();
        RegisterSmokeColor();
        RegisterDiscordAd();
        RegisterMoneyLines();
        // The game's own "left the game" line is not shown (owner request 2026-10-24); this one wears the LEGACY-X prefix. It must be a
        // Pre hook: DontBroadcast set after the event went out (Post) does nothing, so both lines showed.
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid is { } player && player.IsValid && !player.IsBot && !player.IsHLTV)
            {
                info.DontBroadcast = true;
                var name = new string(player.PlayerName.Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
                var line = LegacyXChat.System($"{{white}}{name}{{grey}} disconnected.");
                foreach (var other in Utilities.GetPlayers().Where(other => other.IsValid && !other.IsBot && other.Slot != player.Slot)) other.PrintToChat(line);
            }
            return HookResult.Continue;
        }, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid && !player.IsBot) _ = LoadScoreboardRankAsync(player, welcome: Config.Welcome);
            return HookResult.Continue;
        });
        if (!Config.ScoreboardRanks) return;
        RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
        {
            var player = @event.Userid;
            if (player != null && player.IsValid) ApplyScoreboardRank(player);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDeath>(OnRevengeDeath);
        RegisterListener<Listeners.OnMapStart>(_ => { lastKiller.Clear(); duelKills.Clear(); });
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
        // The rank column only draws after a reveal arrives while Tab is open, and CS2 may never send the
        // server the Tab button: with an icon mode on, reveal to everyone every second instead.
        AddTimer(1.0f, () =>
        {
            if (scoreboardRankType != 0 && scoreboardRanks.Count > 0) RevealScoreboardRanks();
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
    private async Task LoadScoreboardRankAsync(CCSPlayerController player, bool welcome = false)
    {
        var steamId = player.SteamID;
        if (string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret))
        {
            if (welcome) Server.NextFrame(() => ScheduleWelcome(player, null));
            return;
        }
        var (status, profile) = await FetchProfileAsync(steamId.ToString());
        // The worn Smoke colour rides along with the profile; a player the site does not know has none.
        if (profile is { } colored) { var hex = StringOrNull(colored, "smoke_color"); var hex2 = StringOrNull(colored, "smoke_color2"); Server.NextFrame(() => StoreSmokeColor(steamId, hex, hex2)); }
        else if (status == System.Net.HttpStatusCode.NotFound) Server.NextFrame(() => StoreSmokeColor(steamId, null));
        if (welcome) Server.NextFrame(() => ScheduleWelcome(player, WelcomeRankLine(status, profile)));
        if (!Config.ScoreboardRanks) return;
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
        var clanTag = Config.ClanTag ? SafeClanTag(StringOrNull(found, "clan_tag")) : "";
        Server.NextFrame(() =>
        {
            if (rankId is >= 1 and <= 18 && rankName.Length > 0) scoreboardRanks[steamId] = (rankId, matches, exp, rankName, clanTag);
            else scoreboardRanks.Remove(steamId);
            if (player.IsValid) ApplyScoreboardRank(player);
            RevealScoreboardRanks();
        });
    }

    /// <summary>
    /// A short welcome in the joining player's own chat only (nobody else sees it), a few seconds after
    /// they are in so it is not lost in the connect spam. Once per player per few hours, not every map.
    /// </summary>
    private void ScheduleWelcome(CCSPlayerController player, string? rankLine)
    {
        if (!player.IsValid || player.IsBot) return;
        var steamId = player.SteamID;
        if (welcomed.TryGetValue(steamId, out var last) && DateTime.UtcNow - last < TimeSpan.FromHours(3)) return;
        if (welcomed.Count > 1000)
            foreach (var old in welcomed.Where(entry => DateTime.UtcNow - entry.Value > TimeSpan.FromHours(3)).Select(entry => entry.Key).ToList()) welcomed.Remove(old);
        welcomed[steamId] = DateTime.UtcNow;
        AddTimer(4.0f, () =>
        {
            if (!player.IsValid || player.SteamID != steamId) return;
            var name = new string(player.PlayerName.Where(c => c >= ' ' && c != '{' && c != '}').ToArray()).Trim();
            player.PrintToChat(LegacyXChat.System(name.Length > 0 ? $"Welcome to LEGACY-X, {{white}}{name}{{grey}}." : "Welcome to LEGACY-X."));
            if (rankLine != null) player.PrintToChat(LegacyXChat.System(rankLine));
        });
    }

    /// <summary>The player's rank and EXP from the API, or null when it could not be read (then the line is left out).</summary>
    private static string? WelcomeRankLine(System.Net.HttpStatusCode? status, JsonElement? profile)
    {
        if (status == System.Net.HttpStatusCode.NotFound) return "No rank yet. Play a 5v5 match to get one.";
        if (profile is not { } found) return null;
        var rankName = StringOrNull(found, "rank_name")?.Trim();
        if (string.IsNullOrEmpty(rankName)) return null;
        var exp = IntOrNull(found, "current_exp") ?? 0;
        return $"{{white}}{rankName}{{grey}} · {{white}}{exp:N0}{{grey}} EXP. Type {{white}}!rank{{grey}} for more.";
    }

    /// <summary>
    /// Revenge (owner request 2026-10-18): remember who killed whom and how often; when a player kills the one who killed them
    /// last, both are told in their own chat as "LEGACY-X | Name 3 vs Name 2" (the kills each has on the other on this map).
    /// Nobody else sees it, so it is never noise. Not for bots, suicides or team kills.
    /// </summary>
    private HookResult OnRevengeDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!Config.Revenge) return HookResult.Continue;
        var victim = @event.Userid;
        var killer = @event.Attacker;
        if (victim == null || killer == null || !victim.IsValid || !killer.IsValid || victim.IsBot || killer.IsBot) return HookResult.Continue;
        if (victim.Slot == killer.Slot || victim.TeamNum == killer.TeamNum || victim.SteamID == 0 || killer.SteamID == 0) return HookResult.Continue;
        duelKills[(killer.SteamID, victim.SteamID)] = duelKills.GetValueOrDefault((killer.SteamID, victim.SteamID)) + 1;
        if (lastKiller.TryGetValue(killer.SteamID, out var theirKiller) && theirKiller == victim.SteamID)
        {
            lastKiller.Remove(killer.SteamID);
            var line = RevengeLine(ChatName(killer), killer.TeamNum, duelKills.GetValueOrDefault((killer.SteamID, victim.SteamID)), ChatName(victim), victim.TeamNum, duelKills.GetValueOrDefault((victim.SteamID, killer.SteamID)));
            killer.PrintToChat(line);
            victim.PrintToChat(line);
        }
        lastKiller[victim.SteamID] = killer.SteamID;
        return HookResult.Continue;
    }

    // The one chat line that is red on purpose (owner's format "LEGACY-X | name N vs name N"): LegacyXChat turns red into white, so the
    // CS2 codes are used here directly. \x07 red (the prefix only), \x08 grey. Each name is in its team's color and a colon sets it apart from its number
    // ("Bat: 3 vs Temuujin: 2"), so a name that ends in digits is not read as part of the count (owner request 2026-10-24).
    internal static string RevengeLine(string killer, int killerTeam, int killerKills, string victim, int victimTeam, int victimKills) =>
        $" \x07LEGACY-X \x08| {LegacyXChat.TeamColor(killerTeam)}{killer}\x08: \x01{killerKills} \x08vs {LegacyXChat.TeamColor(victimTeam)}{victim}\x08: \x01{victimKills}";

    /// <summary>A player's name safe to put in a chat line (no colour braces, no control characters).</summary>
    private static string ChatName(CCSPlayerController player)
    {
        var name = new string((player.PlayerName ?? "").Where(c => c >= ' ' && c != '{' && c != '}').ToArray()).Trim();
        return name.Length > 0 ? name : "a player";
    }

    private void ApplyScoreboardRank(CCSPlayerController player)
    {
        if (scoreboardBlocked || !player.IsValid || player.IsBot || !scoreboardRanks.TryGetValue(player.SteamID, out var rank)) return;
        try
        {
            ApplyRankTag(player, rank.Name, rank.Clan);
            if (RankHidden())
            {
                // A ranked match is on: no rank in Tab, as on FACEIT. The new rank is on the website after the match.
                player.CompetitiveRankType = 0;
                player.CompetitiveRanking = 0;
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");
                return;
            }
            if (scoreboardRankType == 0) return;
            player.CompetitiveRankType = (sbyte)scoreboardRankType;
            player.CompetitiveRanking = scoreboardRankType == 11 ? rank.Exp : rank.RankId + scoreboardIconOffset;
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

    /// <summary>The website clan tag, upper-case letters and digits only (anything else could break the scoreboard text).</summary>
    private static string SafeClanTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = new string(raw.Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        return clean.Length > 8 ? clean[..8] : clean;
    }

    /// <summary>True while LegacyX-MatchZy has a ranked match live (its legacyx_match_live convar): ranks stay out of Tab until it ends.</summary>
    private static bool RankHidden()
    {
        try { return ConVar.Find("legacyx_match_live")?.GetPrimitiveValue<bool>() == true; }
        catch { return false; }
    }

    /// <summary>The rank name as the player's clan tag: shown before the name in Tab, chat and the kill feed.</summary>
    private void ApplyRankTag(CCSPlayerController player, string rankName, string clanTag = "")
    {
        // Staff (every LEGACY-X staff role carries @css/generic) show their role instead; LegacyX-Admin sets it.
        if (AdminManager.PlayerHasPermissions(new SteamID(player.SteamID), "@css/generic")) return;
        // The scoreboard puts the clan tag in brackets itself: [OPERATOR I] 777.
        // A clan member shows the clan first: [WOLF | OPERATOR I]. The clan part is cut before the rank is.
        // With a rank picture in the rank column (type 6, 7, 10, 11 or 12) the rank name is not repeated as a tag: only the clan stays.
        var nameAsTag = scoreboardRankType == 0 && !RankHidden();
        var tag = nameAsTag ? rankName.ToUpperInvariant() : clanTag;
        if (nameAsTag && clanTag.Length > 0)
        {
            var room = 31 - tag.Length - 3;
            if (room >= 2) tag = $"{(clanTag.Length > room ? clanTag[..room] : clanTag)} | {tag}";
        }
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
            // By name first; if a CS2 update renamed it, by its number (350), which other rank plugins use.
            UserMessage message;
            try { message = UserMessage.FromPartialName("ServerRankRevealAll"); }
            catch { message = UserMessage.FromId(350); }
            if (player != null) message.Recipients.Add(player);
            else message.Recipients.AddAllPlayers();
            message.Send();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Scoreboard rank reveal unavailable: {exception.Message}");
        }
    }

    /// <summary>
    /// Server console: lx_scoreboard_type for everyone at once. 0 = the rank as a clan tag only. 6, 7, 10 and 12 put the rank (1-18) in the
    /// rank column as a skill-group icon (CS2's own rank types: 6 Competitive, 7 Wingman, 10 Danger Zone; 12 was tried first), 11 shows the EXP
    /// as a Premier rating. Which type draws the icon on a community server is found by trying them one after the other.
    /// </summary>
    [ConsoleCommand("lx_scoreboard_type", "Tab rank display: 0 = rank tag, 6/7/10/12 = skill-group icons, 11 = Premier rating (EXP)")]
    public void OnScoreboardType(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return; // server console / RCON only
        if (!int.TryParse(command.GetArg(1), out var type) || type is < 0 or > 12)
        {
            command.ReplyToCommand($"[{ModuleName}] lx_scoreboard_type is {scoreboardRankType}. Use 0 (rank tag), 6, 7, 10 or 12 (skill-group icons) or 11 (Premier rating).");
            return;
        }
        scoreboardRankType = type;
        scoreboardLogged.Clear();
        foreach (var online in Utilities.GetPlayers()) ApplyScoreboardRank(online);
        RevealScoreboardRanks();
        command.ReplyToCommand($"[{ModuleName}] Tab rank type set to {type} for {scoreboardRanks.Count} ranked player(s). Open Tab to check.");
    }

    [ConsoleCommand("css_discord", "Shows the LEGACY-X Discord invite")]
    public void OnDiscord(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        Print(player, $"Join our Discord: {Config.DiscordUrl}");
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
            Print(player, "Ranks are off on this server.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret))
        {
            Print(player, "Ranks aren't ready yet.");
            return;
        }

        var (status, found) = await FetchProfileAsync(player.SteamID.ToString());
        if (status == System.Net.HttpStatusCode.NotFound)
        {
            Print(player, "No rank yet. Play a 5v5 match to get one at {white}legacyx.cc{grey}.");
            return;
        }
        if (found is not { } profile)
        {
            Print(player, "Your rank is unavailable right now. Try again soon.");
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

            var parts = new List<string> { $"{{white}}{rankName}{{grey}}", $"{{white}}{exp:N0}{{grey}} EXP" };
            if (position.HasValue) parts.Add($"{{white}}#{position.Value:N0}{{grey}}");
            parts.Add(nextName != null && nextExp.HasValue ? $"{{white}}{Math.Max(0, nextExp.Value - exp):N0}{{grey}} to {nextName}" : "top rank");
            if (proLeague) parts.Add("Pro League {green}open{grey}");
            Print(player, string.Join(" · ", parts));
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Profile lookup failed: {exception.Message}");
            Print(player, "Your rank is unavailable right now. Try again soon.");
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
