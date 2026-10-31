using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using LegacyX.Shared.Configuration;
using PanoramaManager;

namespace LegacyXHud;

/// <summary>
/// The in-game skin menu (owner request 2026-10-30, the Skinchanger HUD back, skins only). The menu is a Panorama layout from the LEGACY-X
/// Workshop addon (legacyxxx-workshop, sent to players with MultiAddonManager); this plugin creates its custom_hud_layout entity and fills it per
/// player: texts by variable name (= the Label id), states by toggling classes (CONTRACT.md of the addon).
/// !ws opens it in warmup and before the round; it picks skins from the website's own catalog, saves the pick to the website loadout and applies
/// it at once through SkinBridge. Nothing else is drawn: no rank card, no admin panel, no side vote.
/// </summary>
public sealed partial class LegacyXHud : BasePlugin
{
    // PanoramaManager spawns the compiled name (.vxml_c); LEGACYX_HUD_MENU_LAYOUT overrides it if this game build wants the source .xml.
    private const string LayoutDir = "panorama/layout/custom_game/";

    private bool enabled;
    private string menuLayout = LayoutDir + "legacyx_menu.vxml_c";
    private string apiBase = "";
    private PanelHandle? menu;
    private string knifeLayout = LayoutDir + "legacyx_knife.vxml_c";
    private PanelHandle? knifeHud;
    // After a map change a client that got the Workshop addon at connect has lost it (the new level's addon list is empty) and its HUD is dead until it
    // connects again. A short reconnect brings the addon back, exactly like leaving and joining by hand (LEGACYX_HUD_RECONNECT_ON_MAP, default on).
    private bool reconnectOnMap = true;
    private bool firstMapSeen;

    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Hud";
    public override string ModuleVersion => "0.5.0-legacyx.1";

    public override void Load(bool hotReload)
    {
        var env = LegacyXEnvironmentLoader.Load();
        enabled = env.GetModuleBoolean("HUD", "ENABLED", true);
        if (!enabled)
        {
            Console.WriteLine($"[{ModuleName}] Disabled by central environment.");
            return;
        }
        menuLayout = env.Get("LEGACYX_HUD_MENU_LAYOUT", menuLayout);
        knifeLayout = env.Get("LEGACYX_HUD_KNIFE_LAYOUT", knifeLayout);
        hintLayout = env.Get("LEGACYX_HUD_HINT_LAYOUT", hintLayout);
        apiBase = env.Get("LEGACYX_API_BASE_URL", "").TrimEnd('/');
        // The menu reads and saves the loadout with SkinBridge's own token (skinchanger:read and :write).
        skinPluginId = env.GetModule("SKINBRIDGE", "PLUGIN_ID", skinPluginId);
        skinSecret = env.GetModule("SKINBRIDGE", "PLUGIN_TOKEN", "");

        menuKeys = env.GetModuleBoolean("HUD", "MENU_KEYS", true);
        reconnectOnMap = env.GetModuleBoolean("HUD", "RECONNECT_ON_MAP", true);
        menuSounds = env.GetModuleBoolean("HUD", "MENU_SOUNDS", true);
        if (float.TryParse(env.GetModule("HUD", "MENU_SOUND_VOLUME", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var volume))
            soundVolume = Math.Clamp(volume, 0f, 1f);
        soundClick = env.GetModule("HUD", "MENU_SOUND_CLICK", soundClick);
        soundOpen = env.GetModule("HUD", "MENU_SOUND_OPEN", soundOpen);
        soundBack = env.GetModule("HUD", "MENU_SOUND_BACK", soundBack);
        soundPick = env.GetModule("HUD", "MENU_SOUND_PICK", soundPick);

        Panorama.Init(this);
        RegisterListener<Listeners.OnMapStart>(OnMapChanged);
        if (menuKeys)
        {
            RegisterListener<Listeners.OnTick>(OnMenuKeysTick);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        }
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        Console.WriteLine($"[{ModuleName}] Ready. Layout {menuLayout}");
        if (!SkinsReady)
            Console.WriteLine($"[{ModuleName}] No API address or SkinBridge token: the menu opens but cannot read or save skins.");
    }

    public override void Unload(bool hotReload)
    {
        menu?.Dispose();
        menu = null;
        hintHud?.Dispose();
        hintHud = null;
        knifeHudWin?.Dispose();
        knifeHudWin = null;
        knifeHud?.Dispose();
        knifeHud = null;
        if (enabled) Panorama.Shutdown();
    }

    /// <summary>
    /// PanoramaManager keeps the layout across a map change, but whatever this plugin wrote to a player's client is gone: the menu is drawn
    /// again from scratch and every "already sent" cache is cleared. Players are still loading the new map, so the mouse and the keys are let
    /// go once they are in (and again later) in case the client still holds the capture of a menu that was open.
    /// </summary>
    private void OnMapChanged(string mapName)
    {
        Console.WriteLine($"[{ModuleName}] Map {mapName}: the menu is drawn again from scratch");
        foreach (var slot in Enumerable.Range(0, 64)) ForgetMenuState(slot, everything: true);
        rulesProxy = null;
        if (reconnectOnMap && firstMapSeen) AddTimer(5f, ReconnectPlayersForAddon);
        firstMapSeen = true;
        AddTimer(2f, ReleaseMenuInputForAll);
        AddTimer(8f, ReleaseMenuInputForAll);
    }

    /// <summary>Players who were already on the server when the map changed connect again, so the client gets the Workshop addon (and the HUD) back.</summary>
    private void ReconnectPlayersForAddon()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            try
            {
                Console.WriteLine($"[{ModuleName}] Slot {player.Slot} connects again so the addon is back after the map change.");
                player.ExecuteClientCommandFromServer("reconnect");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Could not send the reconnect to slot {player.Slot}: {ex.Message}");
            }
        }
    }

    private void ReleaseMenuInputForAll()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })) ReleaseMenuInput(player);
    }

    /// <summary>A menu that was open is not open any more on our side after a map change, but the client can still hold the mouse and keys: close whatever is not really open.</summary>
    private void ReleaseMenuInput(CCSPlayerController player)
    {
        try
        {
            if (menu is { } m && !menuOpen.Contains(player.Slot) && m.IsOpenFor(player)) m.Close(player);
            // The vote card that holds the mouse never outlives the vote.
            if (knifeHudWin is { } k && !knifeHudLogged.Contains(player.Slot) && k.IsOpenFor(player)) k.Close(player);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not release the menu input for {player.PlayerName}: {ex.Message}");
        }
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect e, GameEventInfo info)
    {
        if (e.Userid is { } player) ForgetMenuState(player.Slot, everything: true);
        return HookResult.Continue;
    }

    /// <summary>The library redrew one player's layout after a world reset: forget what was sent to them and treat their menu as closed.</summary>
    private void OnRestored(PanelEvent e)
    {
        if (e.Action != PanelAction.Restored || e.Player is not { IsValid: true } player) return;
        ForgetMenuState(player.Slot, everything: true);
        ReleaseMenuInput(player);
    }

    private PanelHandle? Ensure(ref PanelHandle? handle, string layout, string rootId, bool captureInput = false)
    {
        if (handle is not null) return handle;
        try
        {
            // What PanelHandle needs to know about a layout: one inner wrapper with RootPanelId, its Labels read {s:<label id>}.
            handle = Panorama.Spawn(layout, new LayoutContract { RootPanelId = rootId, RowCount = 0, CaptureInput = captureInput });
            Console.WriteLine($"[{ModuleName}] Spawned {layout}: {(handle is null ? "null handle" : "ok")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not spawn {layout}: {ex.Message}");
            handle = null;
        }
        return handle;
    }

    /// <summary>SetVariableFor / SetClassFor do nothing for a player without a session: open the layout first.</summary>
    private static bool OpenFor(PanelHandle panel, CCSPlayerController player)
    {
        if (!panel.IsOpenFor(player)) panel.Open(player);
        return panel.IsOpenFor(player);
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
}
