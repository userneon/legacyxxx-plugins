using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using LegacyX.Shared.Configuration;

namespace AdminPlus;

/// <summary>
/// Moderation tools for a live match (owner request 2026-11-07): <c>!kickbots</c> removes every bot at once and <c>!chatlock</c> lets only staff
/// write in chat until it is run again (or the map changes), for a spamming or raiding crowd. Both are audited like any other command and can be
/// given to a role on the staff site (Roles → Chat and punishments / Bans).
/// </summary>
public partial class AdminPlus
{
    private bool _chatLocked;

    private void RegisterModToolCommands()
    {
        AddPermCmd("@css/kick", "css_kickbots", "", CmdKickBots);
        AddPermCmd("@css/chat", "css_chatlock", "", CmdChatLock);
        RegisterListener<Listeners.OnMapStart>(_ => _chatLocked = false);
    }

    private void CmdKickBots(CCSPlayerController? caller, CommandInfo info)
    {
        var count = Utilities.GetPlayers().Count(p => p != null && p.IsValid && p.IsBot && !p.IsHLTV);
        if (count == 0)
        {
            Reply(caller, "There are no bots on the server.");
            return;
        }
        Server.ExecuteCommand("bot_kick");
        Server.PrintToChatAll(LegacyXChat.System($"{GetExecutorNameCommand(caller)} removed {count} bot{(count == 1 ? "" : "s")}."));
    }

    private void CmdChatLock(CCSPlayerController? caller, CommandInfo info)
    {
        _chatLocked = !_chatLocked;
        Server.PrintToChatAll(LegacyXChat.System(_chatLocked
            ? $"{GetExecutorNameCommand(caller)} locked the chat: only staff can write until it is unlocked."
            : $"{GetExecutorNameCommand(caller)} unlocked the chat."));
    }

    /// <summary>True when the chat is locked and this player may not write (staff who can gag are always let through).</summary>
    private bool ChatLockedFor(CCSPlayerController player)
        => _chatLocked && !AdminManager.PlayerHasPermissions(player, ChatPerm);

    private static void Reply(CCSPlayerController? caller, string text)
    {
        if (caller != null && caller.IsValid) caller.PrintToChat(LegacyXChat.System(text));
        else Server.PrintToConsole(text);
    }
}
