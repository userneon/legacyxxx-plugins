using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;

namespace AdminPlus;

/// <summary>LEGACY-X numeric command policy. Explicit CounterStrikeSharp permission remains mandatory.</summary>
public partial class AdminPlus
{
    private static readonly IReadOnlyDictionary<string, int> CommandStaminaRequirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["ban"] = 500, ["ipban"] = 750, ["unban"] = 750, ["lastban"] = 500, ["baninfo"] = 250,
        ["kick"] = 500, ["mute"] = 250, ["gag"] = 250, ["unmute"] = 250, ["ungag"] = 250, ["silence"] = 250, ["unsilence"] = 250, ["mutelist"] = 250, ["gaglist"] = 250,
        ["slap"] = 500, ["slay"] = 750, ["money"] = 1000, ["armor"] = 1000, ["rr"] = 750, ["map"] = 750, ["wsmap"] = 750, ["workshop"] = 750, ["who"] = 250, ["players"] = 0, ["rename"] = 750, ["team"] = 750, ["swap"] = 750,
        ["asay"] = 250, ["csay"] = 500, ["hsay"] = 500, ["psay"] = 250, ["admins"] = 0, ["hideadmin"] = 500, ["report"] = 0, ["calladmin"] = 0,
        ["vote"] = 250, ["votemap"] = 250, ["rvote"] = 500, ["cancelvote"] = 500, ["votekick"] = 250, ["voteban"] = 500, ["votegag"] = 250, ["votemute"] = 250, ["votesilence"] = 500,
        ["freeze"] = 1000, ["unfreeze"] = 1000, ["gravity"] = 1000, ["bury"] = 1000, ["unbury"] = 1000, ["beacon"] = 1000, ["shake"] = 1000, ["unshake"] = 1000, ["blind"] = 1000, ["unblind"] = 1000, ["clean"] = 1000, ["goto"] = 1000, ["bring"] = 1000, ["hrespawn"] = 1000, ["1up"] = 1000, ["drug"] = 1000, ["undrug"] = 1000, ["glow"] = 1000, ["color"] = 1000,
        ["revive"] = 1000, ["respawn"] = 1000, ["noclip"] = 1000, ["weapon"] = 1000, ["strip"] = 1000, ["sethp"] = 1000, ["hp"] = 1000, ["speed"] = 1000, ["unspeed"] = 1000, ["god"] = 1000,
        ["admin"] = 250, ["adminmenu"] = 250, ["banlist"] = 750, ["adminhelp"] = 0, ["adminlist"] = 750, ["addadmin"] = 1000, ["removeadmin"] = 1000, ["adminreload"] = 1000, ["admin_reload"] = 1000, ["version"] = 0, ["calladmin"] = 0, ["callmanager"] = 0,
        ["rcon"] = 1000, ["cvar"] = 1000, ["cleanbans"] = 1000, ["cleanipbans"] = 1000, ["cleansteambans"] = 1000, ["cleanall"] = 1000, ["cleanmute"] = 1000, ["cleangag"] = 1000,
    };

    private static readonly IReadOnlyDictionary<string, string> CommandPermissionRequirements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ban"] = "@css/ban", ["ipban"] = "@css/ban", ["unban"] = "@css/unban", ["lastban"] = "@css/ban", ["baninfo"] = "@css/ban", ["banlist"] = "@css/ban",
        ["kick"] = "@css/generic", ["mute"] = "@css/chat", ["gag"] = "@css/chat", ["unmute"] = "@css/chat", ["ungag"] = "@css/chat", ["silence"] = "@css/chat", ["unsilence"] = "@css/chat", ["mutelist"] = "@css/chat", ["gaglist"] = "@css/chat",
        ["slap"] = "@css/slay", ["slay"] = "@css/slay", ["money"] = "@css/slay", ["armor"] = "@css/slay", ["rename"] = "@css/slay", ["rr"] = "@css/generic", ["map"] = "@css/generic", ["wsmap"] = "@css/generic", ["workshop"] = "@css/generic", ["who"] = "@css/generic", ["team"] = "@css/kick", ["swap"] = "@css/kick",
        ["asay"] = "@css/chat", ["csay"] = "@css/chat", ["hsay"] = "@css/chat", ["psay"] = "@css/chat", ["hideadmin"] = "@css/generic", ["vote"] = "@css/generic", ["votemap"] = "@css/generic", ["cancelvote"] = "@css/generic", ["votekick"] = "@css/generic", ["voteban"] = "@css/generic", ["votegag"] = "@css/generic", ["votemute"] = "@css/generic", ["votesilence"] = "@css/generic",
        ["freeze"] = "@css/slay", ["unfreeze"] = "@css/slay", ["gravity"] = "@css/slay", ["bury"] = "@css/slay", ["unbury"] = "@css/slay", ["beacon"] = "@css/slay", ["shake"] = "@css/slay", ["unshake"] = "@css/slay", ["blind"] = "@css/slay", ["unblind"] = "@css/slay", ["clean"] = "@css/slay", ["goto"] = "@css/slay", ["bring"] = "@css/slay", ["hrespawn"] = "@css/slay", ["1up"] = "@css/slay", ["drug"] = "@css/slay", ["undrug"] = "@css/slay", ["glow"] = "@css/slay", ["color"] = "@css/slay",
        ["revive"] = "@css/cheats", ["respawn"] = "@css/cheats", ["noclip"] = "@css/cheats", ["weapon"] = "@css/cheats", ["strip"] = "@css/cheats", ["sethp"] = "@css/cheats", ["hp"] = "@css/cheats", ["speed"] = "@css/cheats", ["unspeed"] = "@css/cheats", ["god"] = "@css/cheats",
        ["admin"] = "@css/generic", ["adminmenu"] = "@css/generic", ["addadmin"] = "@css/root", ["removeadmin"] = "@css/root", ["adminlist"] = "@css/root", ["adminreload"] = "@css/root", ["admin_reload"] = "@css/root", ["rcon"] = "@css/rcon", ["cvar"] = "@css/cvar", ["cleanbans"] = "@css/root", ["cleanipbans"] = "@css/root", ["cleansteambans"] = "@css/root", ["cleanall"] = "@css/root", ["cleanmute"] = "@css/root", ["cleangag"] = "@css/root",
    };

    private void RegisterStaminaCommandGuards()
    {
        foreach (var requirement in CommandStaminaRequirements)
        {
            var command = requirement.Key;
            AddCommandListener($"css_{command}", (caller, _info) => RequireCommandStamina(caller, command) ? HookResult.Continue : HookResult.Handled, HookMode.Pre);
        }
    }

    private bool RequireCommandStamina(CCSPlayerController? caller, string command)
    {
        if (caller == null || !caller.IsValid || caller.IsBot) return true;
        var normalized = (command ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.StartsWith("css_", StringComparison.Ordinal)) normalized = normalized[4..];
        if (!CommandStaminaRequirements.TryGetValue(normalized, out var required)) return true;
        adminStamina.TryGetValue(caller.SteamID, out var available);
        if (available >= required) return true;
        caller.PrintToChat(LegacyXChat.System($"STAMINA {required} REQUIRED ({available}/1000)"));
        return false;
    }

    private bool HasCommandAccess(CCSPlayerController? caller, string command)
    {
        if (caller == null || !caller.IsValid || caller.IsBot) return false;
        var normalized = (command ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.StartsWith("css_", StringComparison.Ordinal)) normalized = normalized[4..];
        if (!CommandStaminaRequirements.TryGetValue(normalized, out var required) || !CommandPermissionRequirements.TryGetValue(normalized, out var permission)) return false;
        adminStamina.TryGetValue(caller.SteamID, out var available);
        return available >= required && HasEffectivePermission(caller, permission);
    }

    private void RunStaminaCheckedServerCommand(CCSPlayerController admin, string command)
    {
        var safeCommand = command ?? string.Empty;
        var token = safeCommand.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (token.Length == 0 || !HasCommandAccess(admin, token))
        {
            RequireCommandStamina(admin, token);
            return;
        }
        AdminPlus._menuInvokerName = admin.PlayerName;
        Server.ExecuteCommand(safeCommand);
        AddTimer(0.1f, () => { AdminPlus._menuInvokerName = null; });
    }
}
