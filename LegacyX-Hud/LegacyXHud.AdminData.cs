using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace LegacyXHud;

// The data pages of the admin panel: Bans, Logins and Staff. They read LEGACY-X's API with the game server's own token
// (/api/v1/plugin/admin/bans | logins | staff, scope admin:read) and show what comes back; when the API cannot be reached
// they say so instead of showing anything made up. Lifting a ban goes back through LegacyX-Admin (!unban), which checks rights.
public sealed partial class LegacyXHud
{
    private string ThisServerId()
    {
        var fromEnv = Environment.GetEnvironmentVariable("LEGACYX_SERVER_ID");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();
        var port = ConVar.Find("hostport")?.GetPrimitiveValue<int>() ?? 0;
        return port > 0 ? $"srv-{port}" : "";
    }

    private static string Ago(DateTime then)
    {
        var span = DateTime.UtcNow - then;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        return $"{(int)span.TotalDays} d ago";
    }

    private static string Span(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "under a minute";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} h {span.Minutes} min";
        return $"{(int)span.TotalDays} d {span.Hours} h";
    }

    private static DateTime? Time(JsonElement e, string name) =>
        Text(e, name) is { } t && DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>Reads one API page away from the game thread, then hands it to <paramref name="apply"/> on the game thread if that panel is still open.</summary>
    private void AdminLoad(CCSPlayerController actor, AdminView view, string path, Action<int, JsonElement?> apply)
    {
        var slot = actor.Slot;
        if (apiBase.Length == 0 || skinSecret.Length == 0) { apply(0, null); return; }
        _ = Task.Run(async () =>
        {
            int status = 0;
            JsonElement? body = null;
            try { (status, body) = await SkinsApiAsync(HttpMethod.Get, path); }
            catch (Exception ex) { Console.WriteLine($"[{ModuleName}] Admin read failed: {ex.Message}"); }
            Server.NextFrame(() =>
            {
                if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } still && adminViews.TryGetValue(slot, out var current) && current == view)
                    apply(status, body);
            });
        });
    }

    private static List<JsonElement> Items(JsonElement? body) =>
        body is { } b && b.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array ? items.EnumerateArray().Select(i => i.Clone()).ToList() : new List<JsonElement>();

    private static string Failed(int status) => status == 0 ? "Could not reach the LEGACY-X API. Try again in a moment." : $"The API answered {status}. Try again in a moment.";

    // ---- Bans ---------------------------------------------------------------------------------------------------

    private void LoadBans(CCSPlayerController actor, AdminView view)
    {
        Av(actor, "bans_empty", "Loading…");
        Ac(actor, "bans_empty", "hidden", false);
        AdminLoad(actor, view, $"/api/v1/plugin/admin/bans?steam_id={actor.SteamID}&filter={view.BanFilter}&offset={view.BanOffset}", (status, body) =>
        {
            if (status != 200 || body is not { } b) { view.BanItems = new(); view.BanMore = false; Av(actor, "bans_empty", Failed(status)); DrawBans(actor, view, false); return; }
            view.BanItems = Items(b);
            view.BanMore = Flag(b, "hasMore");
            if (b.TryGetProperty("stats", out var stats))
            {
                Av(actor, "bans_active", (Number(stats, "active") ?? 0).ToString("N0"));
                Av(actor, "bans_today", (Number(stats, "today") ?? 0).ToString("N0"));
                Av(actor, "bans_perm", (Number(stats, "permanent") ?? 0).ToString("N0"));
                Av(actor, "bans_mine", (Number(stats, "mine") ?? 0).ToString("N0"));
            }
            if (view.BanSelected >= view.BanItems.Count) view.BanSelected = -1;
            Av(actor, "bans_empty", "No bans here.");
            DrawBans(actor, view, true);
        });
    }

    private static string BanLeft(JsonElement ban)
    {
        return Text(ban, "state") switch
        {
            "lifted" => "Lifted",
            "expired" => "Expired",
            _ => Flag(ban, "permanent") ? "Permanent" : Time(ban, "expiresAt") is { } end ? $"{Span(end - DateTime.UtcNow)} left" : "-",
        };
    }

    private void DrawBans(CCSPlayerController actor, AdminView view, bool ok)
    {
        foreach (var f in new[] { "active", "mine", "lifted", "expired" }) Ac(actor, $"bf_{f}", "sel", f == view.BanFilter);
        for (var i = 0; i < 8; i++)
        {
            var id = $"ban_row{i}";
            if (i >= view.BanItems.Count) { Ac(actor, id, "hidden", true); continue; }
            var ban = view.BanItems[i];
            Ac(actor, id, "hidden", false);
            Ac(actor, id, "sel", i == view.BanSelected);
            Av(actor, $"{id}_name", Text(ban, "player") ?? "Unknown");
            Av(actor, $"{id}_reason", Text(ban, "reason") ?? "");
            Av(actor, $"{id}_via", $"{Text(ban, "via") ?? "-"}" + (Text(ban, "issuedBy") is { } by ? $" · by {by}" : ""));
            Av(actor, $"{id}_left", BanLeft(ban));
        }
        Ac(actor, "bans_empty", "hidden", view.BanItems.Count > 0 && ok);
        Av(actor, "bans_page", $"Page {view.BanOffset / 8 + 1}");
        Ac(actor, "bans_prev", "disabled", view.BanOffset == 0);
        Ac(actor, "bans_next", "disabled", !view.BanMore);
        DrawBanDetail(actor, view);
    }

    private void DrawBanDetail(CCSPlayerController actor, AdminView view)
    {
        var has = view.BanSelected >= 0 && view.BanSelected < view.BanItems.Count;
        var ban = has ? view.BanItems[view.BanSelected] : default;
        var state = has ? Text(ban, "state") : null;
        Av(actor, "bd_name", has ? Text(ban, "player") ?? "Unknown" : "No ban picked");
        Av(actor, "bd_reason", has ? Text(ban, "reason") ?? "" : "Pick a ban in the list.");
        Av(actor, "bd_sid", has ? Text(ban, "steamId") ?? "-" : "-");
        Av(actor, "bd_by", has ? $"{Text(ban, "issuedBy") ?? "-"} · {Text(ban, "via") ?? "-"}" : "-");
        Av(actor, "bd_date", has && Time(ban, "createdAt") is { } created ? created.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " UTC" : "-");
        string length = "-";
        if (has) length = Flag(ban, "permanent") ? "Permanent" : Time(ban, "createdAt") is { } c && Time(ban, "expiresAt") is { } x ? Span(x - c) : "-";
        Av(actor, "bd_len", length);
        Av(actor, "bd_state", state switch { "lifted" => "Lifted", "expired" => "Expired", "active" => "Active", _ => "-" });
        var lifted = state == "lifted";
        foreach (var k in new[] { "bd_lifted_by", "bd_lifted_on", "bd_lift_reason" })
        {
            Ac(actor, k, "hidden", !lifted);
            Ac(actor, $"{k}_key", "hidden", !lifted);
        }
        Av(actor, "bd_lifted_by", lifted ? Text(ban, "liftedBy") ?? "Unknown" : "");
        Av(actor, "bd_lifted_on", lifted && Time(ban, "liftedAt") is { } at ? at.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " UTC" : "");
        Av(actor, "bd_lift_reason", lifted ? Text(ban, "liftReason") ?? "-" : "");
        var timed = state == "active" && !Flag(ban, "permanent") && Time(ban, "createdAt") is not null && Time(ban, "expiresAt") is not null;
        Ac(actor, "bd_fill", "hidden", !timed);
        if (timed && Time(ban, "createdAt") is { } from && Time(ban, "expiresAt") is { } to && to > from)
        {
            var left = Math.Clamp((to - DateTime.UtcNow).TotalSeconds / (to - from).TotalSeconds, 0, 1);
            As(actor, "bd_fill", "p", "p" + (int)(Math.Round(left * 20) * 5));
        }
        Ac(actor, "bd_unban", "hidden", state != "active");
        Ac(actor, "bd_unban", "disabled", !Has(actor, "@css/unban"));
        Av(actor, "bd_note", has ? "IP bans are kept by the game servers and are not in this list." : "");
    }

    // ---- Logins -------------------------------------------------------------------------------------------------

    private void LoadLogins(CCSPlayerController actor, AdminView view)
    {
        Av(actor, "logins_empty", "Loading…");
        Ac(actor, "logins_empty", "hidden", false);
        var path = $"/api/v1/plugin/admin/logins?offset={view.LoginOffset}";
        if (view.LoginFilter == "here" && ThisServerId() is { Length: > 0 } sid) path += $"&server_id={Uri.EscapeDataString(sid)}";
        if (view.LoginFilter == "online") path += "&online=1";
        AdminLoad(actor, view, path, (status, body) =>
        {
            if (status != 200 || body is not { } b) { view.LoginItems = new(); view.LoginMore = false; Av(actor, "logins_empty", Failed(status)); DrawLogins(actor, view, false); return; }
            view.LoginItems = Items(b);
            view.LoginMore = Flag(b, "hasMore");
            if (view.LoginSelected >= view.LoginItems.Count) view.LoginSelected = -1;
            Av(actor, "logins_empty", "No logins here.");
            DrawLogins(actor, view, true);
        });
    }

    private void DrawLogins(CCSPlayerController actor, AdminView view, bool ok)
    {
        foreach (var f in new[] { "all", "here", "online" }) Ac(actor, $"lf_{f}", "sel", f == view.LoginFilter);
        for (var i = 0; i < 10; i++)
        {
            var id = $"lg_row{i}";
            if (i >= view.LoginItems.Count) { Ac(actor, id, "hidden", true); continue; }
            var row = view.LoginItems[i];
            Ac(actor, id, "hidden", false);
            Ac(actor, id, "sel", i == view.LoginSelected);
            Av(actor, $"{id}_name", Text(row, "player") ?? "Unknown");
            Av(actor, $"{id}_sid", Text(row, "steamId") ?? "");
            Av(actor, $"{id}_server", Text(row, "server") ?? "");
            Av(actor, $"{id}_ago", Time(row, "connectedAt") is { } at ? Ago(at) : "-");
            Ac(actor, $"{id}_dot", "online", Flag(row, "online"));
        }
        Ac(actor, "logins_empty", "hidden", view.LoginItems.Count > 0 && ok);
        Av(actor, "logins_page", $"Page {view.LoginOffset / 10 + 1}");
        Ac(actor, "logins_prev", "disabled", view.LoginOffset == 0);
        Ac(actor, "logins_next", "disabled", !view.LoginMore);
        DrawLoginDetail(actor, view);
    }

    private CCSPlayerController? OnlineBySteam(string? steamId) =>
        ulong.TryParse(steamId, out var id) ? Utilities.GetPlayers().FirstOrDefault(p => p is { IsValid: true, IsBot: false } && p.SteamID == id) : null;

    private void DrawLoginDetail(CCSPlayerController actor, AdminView view)
    {
        var has = view.LoginSelected >= 0 && view.LoginSelected < view.LoginItems.Count;
        var row = has ? view.LoginItems[view.LoginSelected] : default;
        Av(actor, "lg_name", has ? Text(row, "player") ?? "Unknown" : "No login picked");
        Av(actor, "lg_sid", has ? Text(row, "steamId") ?? "-" : "-");
        Av(actor, "lg_server", has ? Text(row, "server") ?? "-" : "-");
        var joined = has ? Time(row, "connectedAt") : null;
        Av(actor, "lg_joined", joined is { } j ? j.ToString("d MMM, HH:mm", CultureInfo.InvariantCulture) + " UTC" : "-");
        var online = has && Flag(row, "online");
        string status = "-";
        if (has) status = online ? "Online" : Time(row, "disconnectedAt") is { } left && joined is { } start ? $"Left after {Span(left - start)}" : "Offline";
        Av(actor, "lg_status", status);
        Av(actor, "lg_meta", has && joined is { } at ? $"Connected {Ago(at)} · {Text(row, "server")}" : "Pick a login in the list.");
        var here = has ? OnlineBySteam(Text(row, "steamId")) : null;
        Ac(actor, "lg_open", "disabled", here is null);
        Ac(actor, "lg_ban", "disabled", here is null || !Has(actor, "@css/ban"));
        Av(actor, "lg_note", has ? "From the servers' own session records. Open and Ban work for a player who is on this server now." : "");
    }

    // ---- Staff --------------------------------------------------------------------------------------------------

    private void LoadStaff(CCSPlayerController actor, AdminView view)
    {
        Av(actor, "st_empty", "Loading…");
        Ac(actor, "st_empty", "hidden", false);
        AdminLoad(actor, view, "/api/v1/plugin/admin/staff", (status, body) =>
        {
            var items = status == 200 ? Items(body) : new List<JsonElement>();
            if (status != 200) Av(actor, "st_empty", Failed(status));
            else Av(actor, "st_empty", "Nobody is on the staff list.");
            Av(actor, "st_count", $"{items.Count} on the team");
            for (var i = 0; i < 12; i++)
            {
                var id = $"st_row{i}";
                if (i >= items.Count) { Ac(actor, id, "hidden", true); continue; }
                Ac(actor, id, "hidden", false);
                Av(actor, $"{id}_name", Text(items[i], "player") ?? "Unknown");
                Av(actor, $"{id}_role", (Text(items[i], "role") ?? "").ToUpperInvariant());
            }
            Ac(actor, "st_empty", "hidden", items.Count > 0);
        });
    }

    // ---- clicks on the data pages ---------------------------------------------------------------------------------

    private bool OnAdminDataClick(CCSPlayerController actor, AdminView view, string id)
    {
        if (view.Tab == 2)
        {
            if (id.StartsWith("bf_", StringComparison.Ordinal) && id[3..] is "active" or "mine" or "lifted" or "expired")
            {
                view.BanFilter = id[3..]; view.BanOffset = 0; view.BanSelected = -1; LoadBans(actor, view); return true;
            }
            if (id.StartsWith("ban_row", StringComparison.Ordinal) && int.TryParse(id[7..], out var row) && row >= 0 && row < view.BanItems.Count)
            {
                view.BanSelected = row; DrawBans(actor, view, true); return true;
            }
            switch (id)
            {
                case "bans_prev" when view.BanOffset > 0: view.BanOffset -= 8; view.BanSelected = -1; LoadBans(actor, view); return true;
                case "bans_next" when view.BanMore: view.BanOffset += 8; view.BanSelected = -1; LoadBans(actor, view); return true;
                case "bd_unban":
                    if (view.BanSelected >= 0 && view.BanSelected < view.BanItems.Count && Has(actor, "@css/unban")
                        && Text(view.BanItems[view.BanSelected], "steamId") is { Length: > 0 } steamId && Text(view.BanItems[view.BanSelected], "state") == "active")
                    {
                        Server.ExecuteCommand($"lx_admin_do {actor.Slot} run css_unban {steamId}");
                        AddTimer(1.2f, () =>
                        {
                            if (Utilities.GetPlayerFromSlot(actor.Slot) is { IsValid: true } still && adminViews.TryGetValue(actor.Slot, out var v) && v == view && view.Tab == 2) LoadBans(still, view);
                        });
                    }
                    return true;
            }
            return false;
        }
        if (view.Tab == 3)
        {
            if (id is "lf_all" or "lf_here" or "lf_online")
            {
                view.LoginFilter = id[3..]; view.LoginOffset = 0; view.LoginSelected = -1; LoadLogins(actor, view); return true;
            }
            if (id.StartsWith("lg_row", StringComparison.Ordinal) && int.TryParse(id[6..], out var row) && row >= 0 && row < view.LoginItems.Count)
            {
                view.LoginSelected = row; DrawLogins(actor, view, true); return true;
            }
            switch (id)
            {
                case "logins_prev" when view.LoginOffset > 0: view.LoginOffset -= 10; view.LoginSelected = -1; LoadLogins(actor, view); return true;
                case "logins_next" when view.LoginMore: view.LoginOffset += 10; view.LoginSelected = -1; LoadLogins(actor, view); return true;
                case "lg_open" or "lg_ban":
                    if (view.LoginSelected < 0 || view.LoginSelected >= view.LoginItems.Count) return true;
                    var target = OnlineBySteam(Text(view.LoginItems[view.LoginSelected], "steamId"));
                    if (target is null) return true;
                    if (id == "lg_open")
                    {
                        view.Selected = target.Slot;
                        view.Muted = view.Gagged = false;
                        ShowAdminTab(actor, view, 0);
                        Server.ExecuteCommand($"lx_admin_status {actor.Slot} {target.Slot}");
                    }
                    else if (Has(actor, "@css/ban"))
                    {
                        view.Selected = target.Slot;
                        OpenStep(actor, view, "ban", target);
                    }
                    return true;
            }
        }
        return false;
    }
}
