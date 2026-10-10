using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using PanoramaManager;

namespace LegacyXHud;

// The player menu (legacyx_menu): !menu opens it, the mouse is captured while it is open. Three tabs
// (Welcome, Skins, Settings) and a Close button; every click arrives as a Button with the element's id.
// Welcome shows the player's name and rank from the same profile the welcome card uses.
public sealed partial class LegacyXHud
{
    private static readonly string[] MenuPages = { "welcome", "skins", "settings" };

    // CS2's own UI sounds, played quietly on the player's client with `playvol` (soft ticks, not the loud button clicks).
    // Each has its own gentle level; LEGACYX_HUD_MENU_SOUND_VOLUME (0 to 1, default 1) scales them all, and
    // LEGACYX_HUD_MENU_SOUNDS=false turns them off. A sound is a `sounds/...vsnd_c` path from the game; empty = silent.
    private bool menuSounds = true;
    private float soundVolume = 1f;
    private string soundClick = "sounds/ui/panorama/itemtile_rollover_09.vsnd_c";
    private string soundOpen = "sounds/ui/menu_focus.vsnd_c";
    private string soundBack = "sounds/ui/panorama/cards_rollover_01.vsnd_c";
    private string soundPick = "sounds/ui/panorama/itemtile_click_02.vsnd_c";
    private const float LevelClick = 0.35f, LevelOpen = 0.4f, LevelBack = 0.35f, LevelPick = 0.5f;

    private void MenuSound(CCSPlayerController player, string sound, float level)
    {
        if (!menuSounds || sound.Length == 0 || player is not { IsValid: true, IsBot: false }) return;
        var volume = Math.Clamp(level * soundVolume, 0f, 1f);
        player.ExecuteClientCommand($"playvol {sound} {volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
    }

    // ---- sending only what changed -----------------------------------------------------------------------
    // Every SetVariableFor / SetClassFor is a message to the player's client, and the Skins page sets hundreds of them.
    // The menu remembers what each player's client already has and sends only the difference, so going back to a page
    // that is already drawn costs nothing. Reset when the menu is opened (the client's state may have been dropped).

    private readonly Dictionary<int, Dictionary<string, string>> menuSent = new();

    private bool MenuChanged(int slot, string key, string value)
    {
        if (!menuSent.TryGetValue(slot, out var known)) menuSent[slot] = known = new Dictionary<string, string>();
        if (known.TryGetValue(key, out var old) && old == value) return false;
        known[key] = value;
        return true;
    }

    private void Sv(PanelHandle panel, CCSPlayerController player, string name, string value)
    {
        if (MenuChanged(player.Slot, "v:" + name, value)) panel.SetVariableFor(player, name, value);
    }

    private void Sc(PanelHandle panel, CCSPlayerController player, string id, string cls, bool on)
    {
        if (MenuChanged(player.Slot, $"c:{id}:{cls}", on ? "1" : "0")) panel.SetClassFor(player, id, cls, on);
    }

    /// <summary>Like SetState (a class of a group that replaces the previous one), but only when it changed.</summary>
    private void Ss(PanelHandle panel, CCSPlayerController player, string id, string group, string? cls)
    {
        if (MenuChanged(player.Slot, $"s:{id}:{group}", cls ?? "")) SetState(panel, player, id, group, cls);
    }

    private void ForgetMenuState(int slot)
    {
        menuSent.Remove(slot);
        foreach (var key in applied.Keys.Where(k => k.Slot == slot && (k.PanelId.StartsWith("sk_", StringComparison.Ordinal) || k.PanelId.StartsWith("menu_", StringComparison.Ordinal))).ToList())
            applied.Remove(key);
    }

    private bool menuListening;

    private PanelHandle? EnsureMenu()
    {
        var handle = Ensure(ref menu, menuLayout, "lx_menu", captureInput: true);
        if (handle is not null && !menuListening)
        {
            handle.OnEvent += OnMenuEvent;
            menuListening = true;
        }
        return handle;
    }

    [ConsoleCommand("css_menu", "Open the LEGACY-X menu")]
    public void OnMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!enabled || player is not { IsValid: true, IsBot: false }) return;
        OpenMenu(player);
    }

    private void OpenMenu(CCSPlayerController player)
    {
        if (menuKeys && !MenuAllowed())
        {
            player.PrintToChat(" The menu opens in warmup and before the round starts.");
            return;
        }
        var panel = EnsureMenu();
        if (panel is null)
        {
            player.PrintToChat(" The menu is not available on this server right now.");
            return;
        }
        if (!OpenFor(panel, player)) return;
        ForgetMenuState(player.Slot);
        SkinsWarm(player);
        menuOpen.Add(player.Slot);
        MenuSound(player, soundOpen, LevelOpen);
        Ss(panel, player, "menu_hold_fill", "p", "p0");

        var name = serverName.Length > 0 ? serverName : (ConVar.Find("hostname")?.StringValue ?? "LEGACY-X");
        Sv(panel, player, "menu_footer", name.ToUpperInvariant());
        Sv(panel, player, "menu_name", $"Welcome, {player.PlayerName}");
        var ranked = profiles.TryGetValue(player.SteamID, out var profile);
        Sc(panel, player, "menu_rankrow", "hidden", !ranked);
        if (ranked && profile is not null)
        {
            Sv(panel, player, "menu_rank", profile.RankName);
            Sv(panel, player, "menu_exp", profile.Exp.ToString("N0"));
            Sv(panel, player, "menu_matches", profile.Matches.ToString("N0"));
            Sv(panel, player, "menu_next", NextLine(profile).ToUpperInvariant());
            Sv(panel, player, "menu_togo", TogoLine(profile));
            Ss(panel, player, "menu_fill", "p", ProgressClass(profile) ?? "p0");
            Ss(panel, player, "menu_emblem", "rank", $"rank-{profile.RankId}");
            Ss(panel, player, "menu_rank", "tier", TierClass(profile.RankName));
        }
        // The server box reads this server itself: its name, the map and who is on it.
        var humans = Utilities.GetPlayers().Count(p => p is { IsValid: true, IsBot: false });
        Sv(panel, player, "menu_srv_name", name);
        Sv(panel, player, "menu_srv_map", Server.MapName);
        Sv(panel, player, "menu_srv_players", $"{humans} / {Server.MaxPlayers} players");
        ShowMenuPage(panel, player, "welcome");
        Sc(panel, player, "menu_dim", "shown", true);
        Sc(panel, player, "menu", "shown", true);
    }

    private void ShowMenuPage(PanelHandle panel, CCSPlayerController player, string page)
    {
        foreach (var name in MenuPages)
        {
            Sc(panel, player, $"menu_page_{name}", "hidden", name != page);
            Sc(panel, player, $"menu_tab_{name}", "active", name == page);
        }
    }

    private void CloseMenu(PanelHandle panel, CCSPlayerController player)
    {
        skinViews.Remove(player.Slot);
        if (menuOpen.Remove(player.Slot)) MenuSound(player, soundBack, LevelBack);
        Sc(panel, player, "menu", "shown", false);
        Sc(panel, player, "menu_dim", "shown", false);
        panel.Close(player);
    }

    private void OnMenuEvent(PanelEvent e)
    {
        var panel = menu;
        var player = e.Player;
        if (panel is null || player is not { IsValid: true }) return;
        if (e.Action == PanelAction.Close)
        {
            CloseMenu(panel, player);
            return;
        }
        if (e.Action != PanelAction.Button) return;
        if (e.ElementId != "menu_close") MenuSound(player, soundClick, LevelClick);
        switch (e.ElementId)
        {
            case "menu_close":
                CloseMenu(panel, player);
                break;
            case "menu_pick":
                ShowMenuPage(panel, player, "skins");
                SkinsOpen(panel, player);
                break;
            case "menu_tab_welcome":
                ShowMenuPage(panel, player, "welcome");
                break;
            case "menu_tab_skins":
                ShowMenuPage(panel, player, "skins");
                SkinsOpen(panel, player);
                break;
            case "menu_tab_settings":
                ShowMenuPage(panel, player, "settings");
                break;
            default:
                SkinsClick(panel, player, e.ElementId);
                break;
        }
    }
}
