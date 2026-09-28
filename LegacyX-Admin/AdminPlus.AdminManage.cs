using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using LegacyX.Admin.Authorization;
using System;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// Staff are managed on the LEGACY-X website (staff and per-server assignments), never on the game
/// server. These commands only show who is authorized here and ask the API again.
/// </summary>
public partial class AdminPlus
{
    private const string StaffManagedMessage = "{green}LEGACY-X • {default}STAFF ARE MANAGED ON LEGACYX.CC — NOT ON THE SERVER";

    private void CmdAddAdmin(CCSPlayerController? caller, CommandInfo info) => ReplyStaffManagedOnWebsite(caller);

    private void CmdRemoveAdmin(CCSPlayerController? caller, CommandInfo info) => ReplyStaffManagedOnWebsite(caller);

    private void ReplyStaffManagedOnWebsite(CCSPlayerController? caller)
    {
        if (caller != null && caller.IsValid) caller.Print(StaffManagedMessage);
        else AuthLog("Staff are managed on the LEGACY-X website; addadmin/removeadmin are disabled on the server.");
    }

    private void CmdAdminList(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasEffectivePermission(caller, "@css/root")))
        {
            caller?.Print(Localizer["NoPermission"]);
            return;
        }

        var lines = OnlineStaff()
            .Select(grant =>
            {
                var name = FindOnlineHuman(grant.SteamId) is { } player ? SanitizeName(player.PlayerName) : grant.SteamId.ToString();
                var expiry = grant.ExpiresAt is { } expiresAt ? $", until {expiresAt:yyyy-MM-dd HH:mm} UTC" : string.Empty;
                return $"• {name} [{grant.SteamId}] {StaffPermissions.For(grant.Role).Name}{expiry}";
            })
            .ToList();

        void Write(string line)
        {
            if (caller == null) Console.WriteLine(line);
            else caller.PrintToConsole(line);
        }

        Write("--------- ONLINE STAFF (LEGACY-X API) ---------");
        if (lines.Count == 0) Write("No authorized staff online.");
        foreach (var line in lines) Write(line);
        Write("--------- END ---------");
        caller?.Print(Localizer["Admin.List.Printed"]);
    }

    private void CmdAdminReload(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !HasEffectivePermission(caller, "@css/root")))
        {
            caller?.Print(Localizer["NoPermission"]);
            return;
        }

        _ = RefreshOnlineStaffAsync();
        if (caller != null && caller.IsValid) caller.Print("{green}Staff list updated.");
        else AuthLog("Staff permissions re-check requested.");
    }

    public void RegisterAdminManageCommands()
    {
        AddCommand("addadmin", "Staff are managed on legacyx.cc", CmdAddAdmin);
        AddCommand("removeadmin", "Staff are managed on legacyx.cc", CmdRemoveAdmin);
        AddCommand("adminlist", Localizer["Admin.List.Header"], CmdAdminList);
        AddCommand("adminreload", "Re-check online staff with the LEGACY-X API", CmdAdminReload);

        AddCommand("css_addadmin", "Staff are managed on legacyx.cc", CmdAddAdmin);
        AddCommand("css_removeadmin", "Staff are managed on legacyx.cc", CmdRemoveAdmin);
        AddCommand("css_adminlist", "List authorized staff online", CmdAdminList);
        AddCommand("css_adminreload", "Re-check online staff with the LEGACY-X API", CmdAdminReload);
        AddCommand("css_admin_reload", "Re-check online staff with the LEGACY-X API", CmdAdminReload);
    }
}
