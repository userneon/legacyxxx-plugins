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
/// Screens: legacyx_notify (the !me card), legacyx_menu (!ws) and legacyx_knife (side vote by keyboard).
/// legacyx_admin is the staff panel LegacyX-Admin's !admin opens.
///
/// Other plugins reach it through a server command, so nothing is shared between plugin contexts:
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
    private string knifeLayout = LayoutDir + "legacyx_knife.vxml_c";
    private string menuLayout = LayoutDir + "legacyx_menu.vxml_c";
    private string serverName = "";
    private PanelHandle? notify;
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
        knifeLayout = env.Get("LEGACYX_HUD_KNIFE_LAYOUT", knifeLayout);
        menuLayout = env.Get("LEGACYX_HUD_MENU_LAYOUT", menuLayout);
        adminLayout = env.Get("LEGACYX_HUD_ADMIN_LAYOUT", adminLayout);
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
        RegisterListener<Listeners.OnMapStart>(OnMapChanged);
        menuKeys = env.GetModuleBoolean("HUD", "MENU_KEYS", true);
        menuSounds = env.GetModuleBoolean("HUD", "MENU_SOUNDS", true);
        if (float.TryParse(env.GetModule("HUD", "MENU_SOUND_VOLUME", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var volume))
            soundVolume = Math.Clamp(volume, 0f, 1f);
        soundClick = env.GetModule("HUD", "MENU_SOUND_CLICK", soundClick);
        soundOpen = env.GetModule("HUD", "MENU_SOUND_OPEN", soundOpen);
        soundBack = env.GetModule("HUD", "MENU_SOUND_BACK", soundBack);
        soundPick = env.GetModule("HUD", "MENU_SOUND_PICK", soundPick);
        if (menuKeys)
        {
            RegisterListener<Listeners.OnTick>(OnMenuKeysTick);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        }
        Console.WriteLine($"[{ModuleName}] Ready. Layouts {notifyLayout}, {knifeLayout}, {menuLayout}");
        if (string.IsNullOrEmpty(apiBase) || string.IsNullOrEmpty(pluginSecret))
            Console.WriteLine($"[{ModuleName}] No API address or plugin token: the rank on the welcome card and the menu stays off.");
    }

    public override void Unload(bool hotReload)
    {
        notify?.Dispose();
        knife?.Dispose();
        menu?.Dispose();
        admin?.Dispose();
        notify = knife = menu = admin = null;
        if (enabled) Panorama.Shutdown();
    }

    private HookResult OnRoundStart(EventRoundStart e, GameEventInfo info)
    {
        Console.WriteLine($"[{ModuleName}] round_start, notify entity {(notify is null ? "not spawned yet" : "already spawned")}");
        EnsureNotify();
        if (IsWarmup()) return HookResult.Continue;
        return HookResult.Continue;
    }

    /// <summary>
    /// PanoramaManager keeps the layouts across a map change or a round restart, but whatever this plugin wrote with SetVariableFor /
    /// SetClassFor is gone from the client (it never saw what it meant): the "Hold E" hint, the welcome card, an open menu. The caches that
    /// say "already sent" must go too, or nothing would be sent again and the HUD would stay empty. A map change clears them for everyone.
    /// </summary>
    private void OnMapChanged(string mapName)
    {
        Console.WriteLine($"[{ModuleName}] Map {mapName}: the HUD is drawn again from scratch");
        foreach (var slot in Enumerable.Range(0, 64)) ForgetHudState(slot);
        knifeVotes.Clear();
        knifeTimer?.Kill();
        knifeTimer = null;
        adminRefresh?.Kill();
        adminRefresh = null;
        rulesProxy = null;
        // Players are still loading the new map: let go of the mouse and keyboard for everyone once they are in, and again later.
        AddTimer(2f, ReleaseHudInputForAll);
        AddTimer(8f, ReleaseHudInputForAll);
    }

    private void ReleaseHudInputForAll()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })) ReleaseHudInput(player);
    }

    /// <summary>
    /// The menu, the admin panel and the knife vote capture the player's mouse and keys while they are open. After a map change a
    /// panel that was open is not open any more on our side, but the client can still hold the capture: the HUD and the microphone key
    /// then do nothing until the player reconnects. Close whatever is not really open.
    /// </summary>
    private void ReleaseHudInput(CCSPlayerController player)
    {
        try
        {
            if (menu is { } m && !menuOpen.Contains(player.Slot) && m.IsOpenFor(player)) m.Close(player);
            if (admin is { } a && !adminViews.ContainsKey(player.Slot) && a.IsOpenFor(player)) a.Close(player);
            if (knife is { } k && !knifeVotes.ContainsKey(player.Slot) && k.IsOpenFor(player)) k.Close(player);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not release the HUD input for {player.PlayerName}: {ex.Message}");
        }
    }

    /// <summary>One player's HUD was redrawn by the library after a world reset: forget what was sent to them, and treat their menu and admin panel as closed.</summary>
    private void ForgetHudState(int slot)
    {
        foreach (var key in applied.Keys.Where(k => k.Slot == slot).ToList()) applied.Remove(key);
        menuSent.Remove(slot);
        adminSent.Remove(slot);
        holds.Remove(slot);
        menuOpen.Remove(slot);
        skinViews.Remove(slot);
        adminViews.Remove(slot);
        generation.Keys.Where(k => k.Slot == slot).ToList().ForEach(k => generation.Remove(k));
    }

    private void OnRestored(PanelEvent e)
    {
        if (e.Action != PanelAction.Restored || e.Player is not { IsValid: true } player) return;
        ForgetHudState(player.Slot);
        ReleaseHudInput(player);
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var slot = player.Slot;
        var steamId = player.SteamID;
        // A new player in an old slot starts with a clean hint state.
        holds.Remove(slot);
        foreach (var key in applied.Keys.Where(k => k.Slot == slot && k.PanelId == "hint_fill").ToList()) applied.Remove(key);
        _ = LoadProfileAsync(steamId);
        return HookResult.Continue;
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

    private bool notifyListening;

    private PanelHandle? EnsureNotify()
    {
        var handle = Ensure(ref notify, notifyLayout, "lx_notify");
        if (handle is not null && !notifyListening)
        {
            handle.OnEvent += OnRestored;
            notifyListening = true;
        }
        return handle;
    }

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

    // ---- legacyx_notify: announcement, welcome card ----------------------------------------------------------------

    /// <summary>
    /// legacyx_notify "wc", the !me card: rank, EXP to the next rank, matches, K/D and win rate of the player, for
    /// <paramref name="seconds"/>. A figure the API did not send shows as "-", never a made-up number.
    /// </summary>
    private void Welcome(CCSPlayerController player, Profile? profile, float seconds = 12f)
    {
        var panel = EnsureNotify();
        if (panel is null || !OpenFor(panel, player)) return;
        panel.SetVariableFor(player, "wc_name", player.PlayerName);
        var ranked = profile is not null;
        panel.SetClassFor(player, "wc_emblem", "hidden", !ranked);
        panel.SetClassFor(player, "wc_rankrow", "hidden", !ranked);
        if (profile is not null)
        {
            panel.SetVariableFor(player, "wc_rank", profile.RankName);
            panel.SetVariableFor(player, "wc_exp", $"{profile.Exp:N0} EXP");
            panel.SetVariableFor(player, "wc_next", profile.NextMin is { } next && profile.NextName is { Length: > 0 } nextName ? $"{Math.Max(0, next - profile.Exp):N0} to {nextName}" : "Top rank");
            panel.SetVariableFor(player, "wc_matches", profile.Matches.ToString("N0"));
            panel.SetVariableFor(player, "wc_kd", KillDeathText(profile));
            panel.SetVariableFor(player, "wc_winrate", WinRateText(profile));
            SetState(panel, player, "wc_emblem", "rank", $"rank-{profile.RankId}");
            SetState(panel, player, "wc_rank", "tier", TierClass(profile.RankName));
            SetState(panel, player, "wc_fill", "p", ProgressClass(profile) ?? "p0");
        }
        Flash(panel, player, "wc", seconds);
    }

    private static string KillDeathText(Profile p)
    {
        if (p.Kills is not { } kills || p.Deaths is not { } deaths || kills + deaths == 0) return "-";
        return (deaths > 0 ? kills / (double)deaths : kills).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string WinRateText(Profile p)
    {
        if (p.Wins is not { } wins) return "-";
        var played = wins + (p.Losses ?? 0) + (p.Draws ?? 0);
        return played > 0 ? $"{Math.Round(wins * 100.0 / played)}%" : "-";
    }
}
