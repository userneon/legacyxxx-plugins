using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using PanoramaManager;

namespace LegacyXHud;

// The skin menu (legacyx_menu): !ws opens it, the mouse is captured while it is open. A header with the T / CT emblems and a Close
// button, a category list and the skins (LegacyXHud.Skins.cs); every click arrives as a Button with the element's id.
public sealed partial class LegacyXHud
{
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

    /// <summary>Forgets what was sent to a player's client. <paramref name="everything"/> also drops their open menu, their pages and the hold of R.</summary>
    private void ForgetMenuState(int slot, bool everything = false)
    {
        menuSent.Remove(slot);
        foreach (var key in applied.Keys.Where(k => k.Slot == slot && (k.PanelId.StartsWith("sk_", StringComparison.Ordinal) || k.PanelId.StartsWith("menu_", StringComparison.Ordinal))).ToList())
            applied.Remove(key);
        if (!everything) return;
        holds.Remove(slot);
        menuOpen.Remove(slot);
        skinViews.Remove(slot);
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

    /// <summary>!ws: the skin menu (knives, gloves, guns, agents), opened on its Skins page.</summary>
    [ConsoleCommand("css_ws", "Open the LEGACY-X skin menu")]
    public void OnSkinMenuCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!enabled || player is not { IsValid: true, IsBot: false }) return;
        OpenMenu(player);
        if (!menuOpen.Contains(player.Slot)) return;
        var panel = EnsureMenu();
        if (panel is null) return;
        // It opens on the side the player is on now (T or CT); a spectator or an unassigned player keeps the side they had last.
        var side = player.TeamNum == 2 ? "t" : player.TeamNum == 3 ? "ct" : null;
        SkinsOpen(panel, player, side);
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

        Sc(panel, player, "menu_dim", "shown", true);
        Sc(panel, player, "menu", "shown", true);
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
        if (e.Action == PanelAction.Restored) { OnRestored(e); return; }
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
            default:
                SkinsClick(panel, player, e.ElementId);
                break;
        }
    }
}
