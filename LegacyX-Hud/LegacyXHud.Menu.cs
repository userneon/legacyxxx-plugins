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

    // CS2's own UI sounds, played on the player's client with `play`. Empty = silent (LEGACYX_HUD_MENU_SOUNDS=false turns all off).
    private bool menuSounds = true;
    private string soundClick = "sounds/ui/buttonclick.vsnd_c";
    private string soundOpen = "sounds/ui/menu_accept.vsnd_c";
    private string soundBack = "sounds/ui/menu_back.vsnd_c";
    private string soundPick = "sounds/ui/panorama/itemtile_click_02.vsnd_c";

    private void MenuSound(CCSPlayerController player, string sound)
    {
        if (!menuSounds || sound.Length == 0 || player is not { IsValid: true, IsBot: false }) return;
        player.ExecuteClientCommand($"play {sound}");
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
        menuOpen.Add(player.Slot);
        MenuSound(player, soundOpen);
        SetState(panel, player, "menu_hold_fill", "p", "p0");

        var name = serverName.Length > 0 ? serverName : (ConVar.Find("hostname")?.StringValue ?? "LEGACY-X");
        panel.SetVariableFor(player, "menu_footer", name.ToUpperInvariant());
        panel.SetVariableFor(player, "menu_name", $"Welcome, {player.PlayerName}");
        var ranked = profiles.TryGetValue(player.SteamID, out var profile);
        panel.SetClassFor(player, "menu_rankrow", "hidden", !ranked);
        if (ranked && profile is not null)
        {
            panel.SetVariableFor(player, "menu_rank", profile.RankName);
            panel.SetVariableFor(player, "menu_exp", profile.Exp.ToString("N0"));
            panel.SetVariableFor(player, "menu_matches", profile.Matches.ToString("N0"));
            panel.SetVariableFor(player, "menu_next", NextLine(profile).ToUpperInvariant());
            panel.SetVariableFor(player, "menu_togo", TogoLine(profile));
            SetState(panel, player, "menu_fill", "p", ProgressClass(profile) ?? "p0");
            SetState(panel, player, "menu_emblem", "rank", $"rank-{profile.RankId}");
            SetState(panel, player, "menu_rank", "tier", TierClass(profile.RankName));
        }
        // The server box reads this server itself: its name, the map and who is on it.
        var humans = Utilities.GetPlayers().Count(p => p is { IsValid: true, IsBot: false });
        panel.SetVariableFor(player, "menu_srv_name", name);
        panel.SetVariableFor(player, "menu_srv_map", Server.MapName);
        panel.SetVariableFor(player, "menu_srv_players", $"{humans} / {Server.MaxPlayers} players");
        ShowMenuPage(panel, player, "welcome");
        panel.SetClassFor(player, "menu_dim", "shown", true);
        panel.SetClassFor(player, "menu", "shown", true);
    }

    private void ShowMenuPage(PanelHandle panel, CCSPlayerController player, string page)
    {
        foreach (var name in MenuPages)
        {
            panel.SetClassFor(player, $"menu_page_{name}", "hidden", name != page);
            panel.SetClassFor(player, $"menu_tab_{name}", "active", name == page);
        }
    }

    private void CloseMenu(PanelHandle panel, CCSPlayerController player)
    {
        skinViews.Remove(player.Slot);
        if (menuOpen.Remove(player.Slot)) MenuSound(player, soundBack);
        panel.SetClassFor(player, "menu", "shown", false);
        panel.SetClassFor(player, "menu_dim", "shown", false);
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
        if (e.ElementId != "menu_close") MenuSound(player, soundClick);
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
