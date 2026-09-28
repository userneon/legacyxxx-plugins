using System;
using System.Collections.Generic;

namespace LegacyX.Admin.Authorization;

/// <summary>In-game staff roles, lowest to highest. Anything the API can't vouch for is <see cref="Player"/>.</summary>
public enum StaffRole
{
    Player = 0,
    Staff = 1,
    Admin = 2,
    Manager = 3,
    Owner = 4,
}

/// <summary>What a role receives on the game server: CounterStrikeSharp flags, immunity and command stamina.</summary>
public sealed record RoleGrant(StaffRole Role, IReadOnlyList<string> Flags, uint Immunity, int Stamina, string Name);

/// <summary>
/// The single role → permission mapping. Stamina gates individual AdminPlus commands
/// (see CommandStaminaRequirements); the flags gate them in CounterStrikeSharp and in other plugins
/// (MatchZy admin commands need @css/config; @legacyx/match lets admins force-start a match).
/// </summary>
public static class StaffPermissions
{
    private static readonly RoleGrant PlayerGrant = new(StaffRole.Player, Array.Empty<string>(), 0, 0, "PLAYER");

    private static readonly RoleGrant StaffGrant = new(StaffRole.Staff,
        new[] { "@css/generic", "@css/chat", "@css/vote" }, 250, 250, "STAFF");

    private static readonly RoleGrant AdminGrant = new(StaffRole.Admin,
        new[] { "@css/generic", "@css/kick", "@css/ban", "@css/slay", "@css/changemap", "@css/chat", "@css/vote", "@legacyx/match" }, 500, 500, "ADMIN");

    private static readonly RoleGrant ManagerGrant = new(StaffRole.Manager,
        new[] { "@css/generic", "@css/kick", "@css/ban", "@css/unban", "@css/slay", "@css/changemap", "@css/chat", "@css/vote", "@css/config", "@legacyx/match" }, 750, 750, "MANAGER");

    private static readonly RoleGrant OwnerGrant = new(StaffRole.Owner,
        new[] { "@css/root", "@css/generic", "@css/kick", "@css/ban", "@css/unban", "@css/slay", "@css/changemap", "@css/chat", "@css/vote", "@css/config", "@css/cvar", "@css/rcon", "@css/cheats", "@legacyx/match" }, 1000, 1000, "OWNER");

    public static RoleGrant For(StaffRole role) => role switch
    {
        StaffRole.Owner => OwnerGrant,
        StaffRole.Manager => ManagerGrant,
        StaffRole.Admin => AdminGrant,
        StaffRole.Staff => StaffGrant,
        _ => PlayerGrant,
    };

    /// <summary>Exact, lower-case API role names only; anything else is not a role.</summary>
    public static bool TryParseRole(string? value, out StaffRole role)
    {
        switch (value)
        {
            case "owner": role = StaffRole.Owner; return true;
            case "manager": role = StaffRole.Manager; return true;
            case "admin": role = StaffRole.Admin; return true;
            case "staff": role = StaffRole.Staff; return true;
            case "player": role = StaffRole.Player; return true;
            default: role = StaffRole.Player; return false;
        }
    }
}
