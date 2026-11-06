using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// The bomb banner (owner request 2026-11-01): "BOMB PLANTED" and "BOMB DEFUSED" rise and fade in in the middle of the screen for every player. Nothing says where the bomb
// was planted or who planted it; a defused bomb shows who defused it. Layout legacyx_bomb.xml, the slide is a CSS transition started by a class.
public sealed partial class LegacyXHud
{
    private const float BombShownSeconds = 4.0f;
    private const float BombSlideSeconds = 0.6f;

    private string bombLayout = LayoutDir + "legacyx_bomb.vxml_c";
    private PanelHandle? bombHud;
    private int bombShowId;

    private void RegisterBomb()
    {
        RegisterEventHandler<EventBombPlanted>((_, _) => { ShowBomb("planted", "BOMB PLANTED", "", null); return HookResult.Continue; });
        RegisterEventHandler<EventBombDefused>((e, info) =>
        {
            var name = e.Userid is { IsValid: true } defuser ? CleanBombName(defuser.PlayerName) : "";
            var steamId = e.Userid is { IsValid: true } who ? who.SteamID : 0UL;
            // The defuser's agent (the one they wear from the skin menu, else the plain Counter-Terrorist picture) rides with the banner; it is asked for first.
            _ = Task.Run(async () =>
            {
                var picture = await DefuserAgentPictureAsync(steamId);
                Server.NextFrame(() => ShowBomb("defused", "BOMB DEFUSED", name, picture));
            });
            return HookResult.Continue;
        });
    }

    /// <summary>The picture class of the agent a player wears as a Counter-Terrorist (ag-&lt;defindex&gt;), or the plain team picture when they wear none or it could not be read.</summary>
    private async Task<string> DefuserAgentPictureAsync(ulong steamId)
    {
        const string plain = "agn-ct";
        if (steamId == 0 || !SkinsReady) return plain;
        try
        {
            var (status, body) = await SkinsApiAsync(HttpMethod.Get, $"/api/v1/plugin/menu/skins/loadout?steam_id={steamId}&team=ct");
            if (status != 200 || body is not { } root || !root.TryGetProperty("tiles", out var tiles) || tiles.ValueKind != System.Text.Json.JsonValueKind.Array) return plain;
            foreach (var tile in tiles.EnumerateArray())
            {
                if (Text(tile, "slot") != "agent") continue;
                if (tile.TryGetProperty("equipped", out var worn) && worn.ValueKind == System.Text.Json.JsonValueKind.Object && Number(worn, "defindex") is { } defindex) return SkinPicture("agent", null, null, defindex) ?? plain;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] The defuser's agent could not be read: {ex.Message}");
        }
        return plain;
    }

    /// <summary>A name for a label: no control characters, at most 24 characters.</summary>
    private static string CleanBombName(string? raw)
    {
        var name = new string((raw ?? "").Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
        return name.Length > 24 ? name[..24] : name;
    }

    private void ShowBomb(string kind, string title, string sub, string? agentPicture)
    {
        var panel = Ensure(ref bombHud, bombLayout, "lx_bomb");
        if (panel is null) return;
        var id = ++bombShowId;
        var shown = new List<(int Slot, ulong SteamId)>();
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            // A closed card is gone from the client: what was sent for it before (the title, the picture) is forgotten, or a second banner would stay empty.
            if (!panel.IsOpenFor(player)) ForgetBombCardState(player.Slot);
            if (!OpenFor(panel, player)) continue;
            Sv(panel, player, "bomb_title", title);
            Sv(panel, player, "bomb_sub", sub);
            SetState(panel, player, "bomb_bar", "kind", kind);
            Sc(panel, player, "bomb_agent", "hidden", agentPicture is null);
            Ss(panel, player, "bomb_agent", "ag", agentPicture);
            shown.Add((player.Slot, player.SteamID));
        }
        Console.WriteLine($"[{ModuleName}] The bomb banner ({kind}) is shown to {shown.Count} player(s).");
        // The slide is a transition: the class follows a moment after the card is opened, or it appears at once (see OpenMenu).
        AddTimer(MenuRiseDelaySeconds, () =>
        {
            if (id != bombShowId || bombHud is not { } card) return;
            foreach (var (slot, steamId) in shown)
                if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } now && now.SteamID == steamId && card.IsOpenFor(now)) card.SetClassFor(now, "bomb", "shown", true);
        });
        AddTimer(BombShownSeconds, () =>
        {
            // A newer banner (the bomb was planted, then defused) keeps the card on screen and sets its own timers.
            if (id != bombShowId) return;
            foreach (var (slot, steamId) in shown)
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player is not { IsValid: true } || player.SteamID != steamId || bombHud is null || !bombHud.IsOpenFor(player)) continue;
                bombHud.SetClassFor(player, "bomb", "shown", false);
            }
            AddTimer(BombSlideSeconds, () =>
            {
                if (id != bombShowId) return;
                foreach (var (slot, steamId) in shown)
                {
                    var player = Utilities.GetPlayerFromSlot(slot);
                    if (player is { IsValid: true } && player.SteamID == steamId && bombHud is { } handle && handle.IsOpenFor(player)) handle.Close(player);
                }
            });
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void ForgetBombCardState(int slot)
    {
        if (menuSent.TryGetValue(slot, out var known))
            foreach (var key in known.Keys.Where(key => key.Contains(":bomb_", StringComparison.Ordinal)).ToList()) known.Remove(key);
        foreach (var key in applied.Keys.Where(key => key.Slot == slot && key.PanelId.StartsWith("bomb", StringComparison.Ordinal)).ToList()) applied.Remove(key);
    }
}
