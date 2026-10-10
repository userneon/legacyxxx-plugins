using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;
using PanoramaManager;

namespace LegacyXHud;

/// <summary>
/// Shows the LEGACY-X Workshop layouts (legacyxxx-workshop) to players. The layout is drawn on the
/// client (Workshop addon via MultiAddonManager); this plugin creates the custom_hud_layout entities and
/// fills them per player: texts by variable name (= the Label id), states by toggling classes (see
/// CONTRACT.md of the addon).
///
/// Screens: legacyx_notify (welcome, toast, rank card, rank up / down), legacyx_match (result of a ranked
/// match), legacyx_knife (side vote by keyboard). The !admin panel (legacyx_admin) is not driven yet.
///
/// Other plugins reach it through two server commands, so nothing is shared between plugin contexts:
///   lx_hud_toast &lt;steamId64&gt; &lt;ok|info&gt; &lt;text…&gt;     one line under the top bar
///   lx_hud_knife start &lt;team 2|3&gt; | stop              the knife-round side vote of that team
/// </summary>
public sealed partial class LegacyXHud : BasePlugin
{
    // PanoramaManager's own examples spawn the compiled name (.vxml_c). The addon CONTRACT.md said the
    // source .xml, untested. LEGACYX_HUD_*_LAYOUT in the .env overrides it if the other one is right.
    private const string LayoutDir = "panorama/layout/custom_game/";

    // What PanelHandle needs to know about a layout. PanoramaManager writes every text (dialog variable)
    // on the panel with RootPanelId and toggles by panel id, so each layout has one inner wrapper with
    // this id and its Labels read {s:<label id>}. Nothing takes the mouse and there is no menu row pool.
    // Only a player with an open session gets the entity and can be written to, so every player is
    // Open()ed on a layout before the first text or class is set.
    private static LayoutContract Contract(string rootId, bool captureInput = false) => new()
    {
        RootPanelId = rootId,
        RowCount = 0,
        CaptureInput = captureInput,
    };

    private bool enabled;
    private string notifyLayout = LayoutDir + "legacyx_notify.vxml_c";
    private string matchLayout = LayoutDir + "legacyx_match.vxml_c";
    private string knifeLayout = LayoutDir + "legacyx_knife.vxml_c";
    private string menuLayout = LayoutDir + "legacyx_menu.vxml_c";
    private bool rankCardEnabled = true;
    private string serverName = "";
    private PanelHandle? notify;
    private PanelHandle? match;
    private PanelHandle? knife;
    private PanelHandle? menu;

    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Hud";
    public override string ModuleVersion => "0.3.0-legacyx.1";

    public override void Load(bool hotReload)
    {
        var env = LegacyXEnvironmentLoader.Load();
        enabled = env.GetModuleBoolean("HUD", "ENABLED", true);
        if (!enabled)
        {
            Console.WriteLine($"[{ModuleName}] Disabled by central environment.");
            return;
        }
        notifyLayout = env.Get("LEGACYX_HUD_NOTIFY_LAYOUT", notifyLayout);
        matchLayout = env.Get("LEGACYX_HUD_MATCH_LAYOUT", matchLayout);
        knifeLayout = env.Get("LEGACYX_HUD_KNIFE_LAYOUT", knifeLayout);
        menuLayout = env.Get("LEGACYX_HUD_MENU_LAYOUT", menuLayout);
        rankCardEnabled = env.GetModuleBoolean("HUD", "RANK_CARD", true);
        // The name players know this server by (LEGACYX_<port>_SERVER_NAME or LEGACYX_SERVER_NAME), shown on the welcome card.
        serverName = env.Get("LEGACYX_SERVER_NAME", "").Trim();
        // Same API access the Community plugin uses for the rank card and the Tab icons.
        apiBase = env.Get("LEGACYX_API_BASE_URL", "").TrimEnd('/');
        pluginId = env.GetModule("COMMUNITY", "PLUGIN_ID", "legacyx-community");
        pluginSecret = env.GetModule("COMMUNITY", "PLUGIN_TOKEN", "");
        // The Skins tab of the menu reads and saves the loadout with SkinBridge's own token (skinchanger:read and :write).
        skinPluginId = env.GetModule("SKINBRIDGE", "PLUGIN_ID", skinPluginId);
        skinSecret = env.GetModule("SKINBRIDGE", "PLUGIN_TOKEN", "");

        Panorama.Init(this);

        // The entity system is not ready at plugin load: spawn after the first round starts.
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);
        RegisterListener<Listeners.OnTick>(OnKnifeTick);
        menuKeys = env.GetModuleBoolean("HUD", "MENU_KEYS", true);
        menuSounds = env.GetModuleBoolean("HUD", "MENU_SOUNDS", true);
        soundClick = env.GetModule("HUD", "MENU_SOUND_CLICK", soundClick);
        soundOpen = env.GetModule("HUD", "MENU_SOUND_OPEN", soundOpen);
        soundBack = env.GetModule("HUD", "MENU_SOUND_BACK", soundBack);
        soundPick = env.GetModule("HUD", "MENU_SOUND_PICK", soundPick);
        if (menuKeys)
        {
            RegisterListener<Listeners.OnTick>(OnMenuKeysTick);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        }
        Console.WriteLine($"[{ModuleName}] Ready. Layouts {notifyLayout}, {matchLayout}, {knifeLayout}");
        if (string.IsNullOrEmpty(apiBase) || string.IsNullOrEmpty(pluginSecret))
            Console.WriteLine($"[{ModuleName}] No API address or plugin token: rank and match cards stay off.");
    }

    public override void Unload(bool hotReload)
    {
        notify?.Dispose();
        match?.Dispose();
        knife?.Dispose();
        menu?.Dispose();
        notify = match = knife = menu = null;
        if (enabled) Panorama.Shutdown();
    }

    private HookResult OnRoundStart(EventRoundStart e, GameEventInfo info)
    {
        Console.WriteLine($"[{ModuleName}] round_start, notify entity {(notify is null ? "not spawned yet" : "already spawned")}");
        EnsureNotify();
        if (IsWarmup()) return HookResult.Continue;
        CloseMatchCompactCards();
        if (rankCardEnabled) ShowRankCards();
        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var slot = player.Slot;
        var steamId = player.SteamID;
        _ = LoadProfileAsync(steamId);
        // A few seconds: the client must finish loading the map and the addon before it can draw the panel.
        AddTimer(5f, () =>
        {
            var joined = Utilities.GetPlayerFromSlot(slot);
            if (joined is not { IsValid: true, IsBot: false }) return;
            if (profiles.TryGetValue(steamId, out var known)) Welcome(joined, known);
            else _ = WelcomeAfterLookupAsync(slot, steamId);
        });
        return HookResult.Continue;
    }

    [ConsoleCommand("css_lxhud", "Show the LEGACY-X HUD test announcement to yourself")]
    public void OnLxHud(CCSPlayerController? player, CommandInfo command)
    {
        if (!enabled || player is not { IsValid: true }) return;
        Announce(player, "LEGACY-X HUD test", "If you can read this, the Workshop addon and the plugin work together.");
    }

    /// <summary>Server console / other plugins: lx_hud_toast &lt;steamId64&gt; &lt;ok|info&gt; &lt;text…&gt;</summary>
    [ConsoleCommand("lx_hud_toast", "One line under the top bar for a player: lx_hud_toast <steamId64> <ok|info> <text>")]
    public void OnToastCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!enabled || caller != null || command.ArgCount < 4) return;
        if (!ulong.TryParse(command.GetArg(1), out var steamId)) return;
        var player = Utilities.GetPlayerFromSteamId(steamId);
        if (player is not { IsValid: true, IsBot: false }) return;
        var ok = command.GetArg(2).Equals("ok", StringComparison.OrdinalIgnoreCase);
        var text = string.Join(' ', Enumerable.Range(3, command.ArgCount - 3).Select(command.GetArg)).Trim();
        if (text.Length > 0) Toast(player, text, ok);
    }

    private PanelHandle? Ensure(ref PanelHandle? handle, string layout, string rootId, bool captureInput = false)
    {
        if (handle is not null) return handle;
        try
        {
            handle = Panorama.Spawn(layout, Contract(rootId, captureInput));
            Console.WriteLine($"[{ModuleName}] Spawned {layout}: {(handle is null ? "null handle" : "ok")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not spawn {layout}: {ex.Message}");
            handle = null;
        }
        return handle;
    }

    private PanelHandle? EnsureNotify() => Ensure(ref notify, notifyLayout, "lx_notify");
    private PanelHandle? EnsureMatch() => Ensure(ref match, matchLayout, "lx_match");
    private PanelHandle? EnsureKnife() => Ensure(ref knife, knifeLayout, "lx_knife");

    /// <summary>SetVariableFor / SetClassFor do nothing for a player without a session: open the layout first.</summary>
    private static bool OpenFor(PanelHandle panel, CCSPlayerController player)
    {
        if (!panel.IsOpenFor(player)) panel.Open(player);
        return panel.IsOpenFor(player);
    }

    private static bool IsWarmup()
    {
        var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return rules?.WarmupPeriod ?? false;
    }

    // ---- classes that replace each other (rank-N, tier-x, pNN) ---------------------------------------

    private readonly Dictionary<(int Slot, string PanelId, string Group), string> applied = new();

    /// <summary>Puts <paramref name="cls"/> on the panel and takes off the one of the same group set before.</summary>
    private void SetState(PanelHandle panel, CCSPlayerController player, string panelId, string group, string? cls)
    {
        var key = (player.Slot, panelId, group);
        if (applied.TryGetValue(key, out var old) && old != cls)
            panel.SetClassFor(player, panelId, old, false);
        if (cls is null)
        {
            applied.Remove(key);
            return;
        }
        panel.SetClassFor(player, panelId, cls, true);
        applied[key] = cls;
    }

    // ---- open / close a panel for a while ---------------------------------------------------------------

    private readonly Dictionary<(int Slot, string PanelId), int> generation = new();

    /// <summary>Adds "open" to a panel and removes it after <paramref name="seconds"/> (a newer show keeps it open).</summary>
    private void Flash(PanelHandle panel, CCSPlayerController player, string panelId, float seconds)
    {
        var key = (player.Slot, panelId);
        var mine = generation.TryGetValue(key, out var g) ? g + 1 : 1;
        generation[key] = mine;
        panel.SetClassFor(player, panelId, "open", true);
        var slot = player.Slot;
        AddTimer(seconds, () =>
        {
            if (!generation.TryGetValue(key, out var current) || current != mine) return;
            var still = Utilities.GetPlayerFromSlot(slot);
            if (still is { IsValid: true }) panel.SetClassFor(still, panelId, "open", false);
        });
    }

    // ---- legacyx_notify: announcement, toast ------------------------------------------------------------

    /// <summary>legacyx_notify "ann": sets the two texts for this player, drops the banner in, then away.</summary>
    private void Announce(CCSPlayerController player, string title, string body)
    {
        var panel = EnsureNotify();
        if (panel is null)
        {
            Console.WriteLine($"[{ModuleName}] Announce for {player.PlayerName} skipped: layout is not spawned");
            return;
        }
        var opened = OpenFor(panel, player);
        Console.WriteLine($"[{ModuleName}] Announce to slot {player.Slot}: \"{title}\" (open: {opened})");
        panel.SetVariableFor(player, "ann_title", title);
        panel.SetVariableFor(player, "ann_body", body);
        Flash(panel, player, "ann", 7f);
    }

    /// <summary>The rank was not loaded yet: ask once more, then show the card with it (or without, for a player with no rank).</summary>
    private async Task WelcomeAfterLookupAsync(int slot, ulong steamId)
    {
        var profile = await FetchProfileAsync(steamId);
        Server.NextFrame(() =>
        {
            var joined = Utilities.GetPlayerFromSlot(slot);
            if (joined is not { IsValid: true, IsBot: false }) return;
            if (profile is not null) profiles[steamId] = profile;
            Welcome(joined, profile);
        });
    }

    /// <summary>
    /// legacyx_notify "wc": the welcome card in the middle of the screen for 7 seconds, with the server's name, the
    /// player's name and rank in one box. A player without a rank yet gets the server and the name only.
    /// </summary>
    private void Welcome(CCSPlayerController player, Profile? profile)
    {
        var panel = EnsureNotify();
        if (panel is null || !OpenFor(panel, player)) return;
        var name = serverName.Length > 0 ? serverName : (ConVar.Find("hostname")?.StringValue ?? "LEGACY-X");
        panel.SetVariableFor(player, "wc_server", name);
        panel.SetVariableFor(player, "wc_name", player.PlayerName);
        var ranked = profile is not null;
        panel.SetClassFor(player, "wc_emblem", "hidden", !ranked);
        panel.SetClassFor(player, "wc_rankrow", "hidden", !ranked);
        if (profile is not null)
        {
            panel.SetVariableFor(player, "wc_rank", profile.RankName);
            panel.SetVariableFor(player, "wc_exp", $"{profile.Exp:N0} EXP");
            SetState(panel, player, "wc_emblem", "rank", $"rank-{profile.RankId}");
            SetState(panel, player, "wc_rank", "tier", TierClass(profile.RankName));
        }
        Flash(panel, player, "wc", 7f);
    }

    /// <summary>legacyx_notify "toast": one short line.</summary>
    private void Toast(CCSPlayerController player, string text, bool ok)
    {
        var panel = EnsureNotify();
        if (panel is null || !OpenFor(panel, player)) return;
        panel.SetVariableFor(player, "toast_text", text);
        SetState(panel, player, "toast_icon", "icon", ok ? "ic-check" : "ic-circle-alert");
        panel.SetClassFor(player, "toast", "ok", ok);
        Flash(panel, player, "toast", 3.5f);
    }
}
