using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// The bomb banner (owner request 2026-11-01): "BOMB PLANTED" and "BOMB DEFUSED" slide in from the right edge for every player. Nothing says where the bomb
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
        RegisterEventHandler<EventBombPlanted>((_, _) => { ShowBomb("planted", "BOMB PLANTED", ""); return HookResult.Continue; });
        RegisterEventHandler<EventBombDefused>((e, _) =>
        {
            var name = e.Userid is { IsValid: true } defuser ? CleanBombName(defuser.PlayerName) : "";
            ShowBomb("defused", "BOMB DEFUSED", name.Length > 0 ? name : "");
            return HookResult.Continue;
        });
    }

    /// <summary>A name for a label: no control characters, at most 24 characters.</summary>
    private static string CleanBombName(string? raw)
    {
        var name = new string((raw ?? "").Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
        return name.Length > 24 ? name[..24] : name;
    }

    private void ShowBomb(string kind, string title, string sub)
    {
        var panel = Ensure(ref bombHud, bombLayout, "lx_bomb");
        if (panel is null) return;
        var id = ++bombShowId;
        var shown = new List<(int Slot, ulong SteamId)>();
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            if (!OpenFor(panel, player)) continue;
            Sv(panel, player, "bomb_title", title);
            Sv(panel, player, "bomb_sub", sub);
            SetState(panel, player, "bomb_bar", "kind", kind);
            panel.SetClassFor(player, "bomb", "shown", true);
            shown.Add((player.Slot, player.SteamID));
        }
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
}
