using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

/// <summary>
/// The game's own lines about people coming and going are shown in the LEGACY-X look (owner request 2026-10-30, "put everything in our
/// format"): "Name connected.", "Name joined the Terrorists.", "Name is now spectating.", "Name is now called New." They come from game
/// events, so the game's line is switched off in a Pre hook (DontBroadcast) and ours is printed to the humans in its place.
/// The leave line already did this. GameLines: "prefix" (default) or "game" (the game's own lines).
/// </summary>
public sealed partial class LegacyXCommunity
{
    private static string CleanName(string? name) => new string((name ?? "").Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();

    private static void TellHumans(string line, int exceptSlot = -1)
    {
        foreach (var other in Utilities.GetPlayers().Where(other => other.IsValid && !other.IsBot && !other.IsHLTV && other.Slot != exceptSlot)) other.PrintToChat(line);
    }

    private void RegisterGameLines()
    {
        if (string.Equals(Config.GameLines, "game", StringComparison.OrdinalIgnoreCase)) return;
        // The game prints "Name connected." when the connection starts; ours comes when the player is fully in.
        RegisterEventHandler<EventPlayerConnect>((_, info) => { info.DontBroadcast = true; return HookResult.Continue; }, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>((@event, _) =>
        {
            if (@event.Userid is { } player && player.IsValid && !player.IsBot && !player.IsHLTV)
                TellHumans(LegacyXChat.System($"{{white}}{CleanName(player.PlayerName)}{{grey}} connected."), player.Slot);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerTeam>((@event, info) =>
        {
            info.DontBroadcast = true;
            var player = @event.Userid;
            // Only a first seat and a move to spectators are told: a half-time swap or a team shuffle would be ten lines at once.
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || @event.Disconnect) return HookResult.Continue;
            var name = CleanName(player.PlayerName);
            if (@event.Team == (int)CsTeam.Spectator && @event.Oldteam >= (int)CsTeam.Terrorist) TellHumans(LegacyXChat.System($"{{white}}{name}{{grey}} is now spectating."));
            else if (@event.Oldteam <= (int)CsTeam.Spectator && @event.Team >= (int)CsTeam.Terrorist)
                TellHumans(LegacyXChat.System($"{{white}}{name}{{grey}} joined the {(@event.Team == (int)CsTeam.Terrorist ? "Terrorists" : "Counter-Terrorists")}."));
            return HookResult.Continue;
        }, HookMode.Pre);
        RegisterEventHandler<EventPlayerChangename>((@event, info) =>
        {
            info.DontBroadcast = true;
            if (@event.Userid is { } player && player.IsValid && !player.IsBot && !player.IsHLTV)
                TellHumans(LegacyXChat.System($"{{white}}{CleanName(@event.Oldname)}{{grey}} is now called {{white}}{CleanName(@event.Newname)}{{grey}}."));
            return HookResult.Continue;
        }, HookMode.Pre);
    }
}
