using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using PanoramaManager;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace LegacyXHud;

// The admin panel (legacyx_admin): LegacyX-Admin's !admin opens it (lx_hud_admin open <slot>) when this plugin is loaded.
// Players and Server pages, and the Ban step. Every action goes back to LegacyX-Admin as lx_admin_do <actor> <action> ...,
// which checks the clicking staff member's own rights; the panel only greys out buttons the viewer cannot use.
public sealed partial class LegacyXHud
{
    // A sentinel LegacyX-Admin looks for before it hands !admin to this panel.
    private readonly FakeConVar<bool> adminReady = new("lx_hud_admin_ready", "LegacyX-Hud's admin panel is available", true);

    private string adminLayout = LayoutDir + "legacyx_admin.vxml_c";
    private PanelHandle? admin;
    private bool adminListening;
    private CssTimer? adminRefresh;

    private const int AdminRows = 12;
    private static readonly string[] AdminMaps = { "de_dust2", "de_mirage", "de_inferno", "de_nuke", "de_ancient", "de_anubis", "de_overpass", "de_vertigo", "de_train" };
    private static readonly (string Key, string Label, int Minutes)[] BanLengths =
    {
        ("1h", "1 hour", 60), ("1d", "1 day", 1440), ("7d", "7 days", 10080), ("30d", "30 days", 43200), ("perm", "Permanent", 0),
    };
    private static readonly (string Key, string Label)[] BanReasons =
    {
        ("cheat", "Cheating"), ("grief", "Griefing"), ("voice", "Voice abuse"), ("evade", "Ban evasion"), ("other", "Other"),
    };

    private sealed class AdminView
    {
        public int Tab;                    // 0 players, 1 server, 2 bans, 3 logins, 4 staff
        public bool ServerTab => Tab == 1;
        public string BanFilter = "active";
        public int BanOffset;
        public List<JsonElement> BanItems = new();
        public bool BanMore;
        public int BanSelected = -1;
        public string LoginFilter = "all";
        public int LoginOffset;
        public List<JsonElement> LoginItems = new();
        public bool LoginMore;
        public int LoginSelected = -1;
        public readonly int[] Rows = new int[AdminRows];
        public int RowCount;
        public int Selected = -1;          // slot of the picked player
        public bool Muted, Gagged;         // the picked player's penalties, answered by LegacyX-Admin
        public string? Map;                // picked map
        public DateTime WarmupAsked;       // first click of "back to warmup"
        public bool StepOpen;              // the Ban step is showing
        public string StepKind = "ban";    // ban | mute | gag | silence
        public int StepTarget = -1;
        public bool IpBan;
        public string Length = "7d";
        public string Reason = "grief";
    }

    private readonly Dictionary<int, AdminView> adminViews = new();
    private readonly Dictionary<int, Dictionary<string, string>> adminSent = new();

    // ---- sending only what changed (same idea as the menu) -----------------------------------------------------

    private bool AdminChanged(int slot, string key, string value)
    {
        if (!adminSent.TryGetValue(slot, out var known)) adminSent[slot] = known = new Dictionary<string, string>();
        if (known.TryGetValue(key, out var old) && old == value) return false;
        known[key] = value;
        return true;
    }

    private void Av(CCSPlayerController p, string name, string value)
    {
        if (admin is not null && AdminChanged(p.Slot, "v:" + name, value)) admin.SetVariableFor(p, name, value);
    }

    private void Ac(CCSPlayerController p, string id, string cls, bool on)
    {
        if (admin is not null && AdminChanged(p.Slot, $"c:{id}:{cls}", on ? "1" : "0")) admin.SetClassFor(p, id, cls, on);
    }

    private void As(CCSPlayerController p, string id, string group, string? cls)
    {
        if (admin is not null && AdminChanged(p.Slot, $"s:{id}:{group}", cls ?? "")) SetState(admin, p, id, group, cls);
    }

    // ---- who may do what ----------------------------------------------------------------------------------------

    private static bool Has(CCSPlayerController p, string flag) =>
        AdminManager.PlayerHasPermissions(p, flag) || AdminManager.PlayerHasPermissions(p, "@css/root");

    // ---- opening and closing -------------------------------------------------------------------------------------

    private PanelHandle? EnsureAdmin()
    {
        var handle = Ensure(ref admin, adminLayout, "lx_admin", captureInput: true);
        if (handle is not null && !adminListening)
        {
            handle.OnEvent += OnAdminEvent;
            adminListening = true;
        }
        return handle;
    }

    /// <summary>Server console, from LegacyX-Admin: lx_hud_admin open &lt;slot&gt; | status &lt;actor&gt; &lt;target&gt; &lt;mute&gt; &lt;gag&gt;</summary>
    [ConsoleCommand("lx_hud_admin", "LegacyX-Admin panel: lx_hud_admin open <slot> | status <actor> <target> <mute 0|1> <gag 0|1>")]
    public void OnAdminCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!enabled || caller != null) return;
        switch (command.GetArg(1).ToLowerInvariant())
        {
            case "open" when int.TryParse(command.GetArg(2), out var slot):
                if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true, IsBot: false } who) OpenAdmin(who);
                break;
            case "status" when command.ArgCount >= 6 && int.TryParse(command.GetArg(2), out var actorSlot) && int.TryParse(command.GetArg(3), out var targetSlot):
                if (!adminViews.TryGetValue(actorSlot, out var view) || view.Selected != targetSlot) break;
                view.Muted = command.GetArg(4) == "1";
                view.Gagged = command.GetArg(5) == "1";
                if (Utilities.GetPlayerFromSlot(actorSlot) is { IsValid: true } actor) DrawAdminDetail(actor, view);
                break;
        }
    }

    private void OpenAdmin(CCSPlayerController actor)
    {
        if (!Has(actor, "@css/generic"))
        {
            actor.PrintToChat(" You are not allowed to use the admin panel.");
            return;
        }
        var panel = EnsureAdmin();
        if (panel is null)
        {
            actor.PrintToChat(" The admin panel is not available on this server right now.");
            return;
        }
        if (!OpenFor(panel, actor)) return;
        adminSent.Remove(actor.Slot);
        foreach (var key in applied.Keys.Where(k => k.Slot == actor.Slot && (k.PanelId.StartsWith("adm", StringComparison.Ordinal) || k.PanelId.StartsWith("pl_", StringComparison.Ordinal) || k.PanelId.StartsWith("ban", StringComparison.Ordinal))).ToList())
            applied.Remove(key);
        var view = new AdminView();
        adminViews[actor.Slot] = view;
        MenuSound(actor, soundOpen, LevelOpen);
        Av(actor, "srv_now", $"Now: {Server.MapName}");
        DrawAdminPage(actor, view);
        DrawAdminPlayers(actor, view);
        DrawAdminServer(actor, view);
        Ac(actor, "ban", "shown", false);
        Ac(actor, "ban_dim", "shown", false);
        Ac(actor, "adm_dim", "shown", true);
        Ac(actor, "adm", "shown", true);
        adminRefresh ??= AddTimer(2f, AdminTick, TimerFlags.REPEAT);
    }

    private void CloseAdmin(CCSPlayerController player)
    {
        if (!adminViews.Remove(player.Slot)) return;
        MenuSound(player, soundBack, LevelBack);
        if (admin is not null)
        {
            Ac(player, "ban", "shown", false);
            Ac(player, "ban_dim", "shown", false);
            Ac(player, "adm", "shown", false);
            Ac(player, "adm_dim", "shown", false);
            admin.Close(player);
        }
        if (adminViews.Count == 0) { adminRefresh?.Kill(); adminRefresh = null; }
    }

    /// <summary>Keeps K / D and ping fresh while a panel is open, and closes it for someone who left.</summary>
    private void AdminTick()
    {
        foreach (var (slot, view) in adminViews.ToList())
        {
            var actor = Utilities.GetPlayerFromSlot(slot);
            if (actor is not { IsValid: true }) { adminViews.Remove(slot); adminSent.Remove(slot); continue; }
            if (view.Tab != 0) continue;
            DrawAdminPlayers(actor, view);
        }
        if (adminViews.Count == 0) { adminRefresh?.Kill(); adminRefresh = null; }
    }

    // ---- drawing -------------------------------------------------------------------------------------------------

    private static readonly string[] AdminTabs = { "players", "server", "bans", "logins", "staff" };

    private void DrawAdminPage(CCSPlayerController actor, AdminView view)
    {
        for (var i = 0; i < AdminTabs.Length; i++)
        {
            Ac(actor, $"adm_page_{AdminTabs[i]}", "hidden", i != view.Tab);
            Ac(actor, $"adm_tab_{AdminTabs[i]}", "active", i == view.Tab);
        }
    }

    private void ShowAdminTab(CCSPlayerController actor, AdminView view, int tab)
    {
        view.Tab = tab;
        DrawAdminPage(actor, view);
        switch (tab)
        {
            case 0: DrawAdminPlayers(actor, view); break;
            case 1: DrawAdminServer(actor, view); break;
            case 2: LoadBans(actor, view); break;
            case 3: LoadLogins(actor, view); break;
            case 4: LoadStaff(actor, view); break;
        }
    }

    private static string TeamLabel(CCSPlayerController p) => p.TeamNum == 2 ? "T" : p.TeamNum == 3 ? "CT" : "SPEC";

    private static string Ip(CCSPlayerController p)
    {
        var ip = p.IpAddress ?? "";
        var colon = ip.LastIndexOf(':');
        return colon > 0 ? ip[..colon] : (ip.Length > 0 ? ip : "-");
    }

    private void DrawAdminPlayers(CCSPlayerController actor, AdminView view)
    {
        var players = Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false })
            .OrderBy(p => p.TeamNum == 2 ? 0 : p.TeamNum == 3 ? 1 : 2).ThenBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase).ToList();
        Av(actor, "pl_count", $"{players.Count} online · {(serverName.Length > 0 ? serverName : (ConVar.Find("hostname")?.StringValue ?? "LEGACY-X"))}");
        view.RowCount = Math.Min(players.Count, AdminRows);
        for (var i = 0; i < AdminRows; i++)
        {
            var id = $"pl_row{i}";
            if (i >= view.RowCount) { Ac(actor, id, "hidden", true); continue; }
            var p = players[i];
            view.Rows[i] = p.Slot;
            Ac(actor, id, "hidden", false);
            Ac(actor, id, "sel", p.Slot == view.Selected);
            Av(actor, $"{id}_team", TeamLabel(p));
            Av(actor, $"{id}_name", p.PlayerName);
            Av(actor, $"{id}_flag", p.VoiceFlags == VoiceFlags.Muted ? "MUTED" : "");
            var stats = p.ActionTrackingServices?.MatchStats;
            Av(actor, $"{id}_kd", stats is null ? "-" : $"{stats.Kills} / {stats.Deaths}");
            Av(actor, $"{id}_ping", $"{p.Ping} ms");
            if (profiles.TryGetValue(p.SteamID, out var profile))
            {
                Ac(actor, $"{id}_rank", "hidden", false);
                As(actor, $"{id}_rank", "rank", $"rank-{profile.RankId}");
            }
            else Ac(actor, $"{id}_rank", "hidden", true);
        }
        Ac(actor, "pl_empty", "hidden", players.Count > 0);
        Av(actor, "pl_empty", "Nobody is on the server.");
        if (view.Selected >= 0 && players.All(p => p.Slot != view.Selected)) view.Selected = -1;
        DrawAdminDetail(actor, view);
    }

    private void DrawAdminDetail(CCSPlayerController actor, AdminView view)
    {
        var target = view.Selected >= 0 ? Utilities.GetPlayerFromSlot(view.Selected) : null;
        var has = target is { IsValid: true };
        Ac(actor, "pl_detail", "hidden", false);
        Ac(actor, "act_kick", "disabled", !has || !Has(actor, "@css/generic"));
        Ac(actor, "act_ban", "disabled", !has || !Has(actor, "@css/ban"));
        Ac(actor, "act_slay", "disabled", !has || !Has(actor, "@css/slay"));
        Ac(actor, "act_respawn", "disabled", !has || !Has(actor, "@css/slay"));
        foreach (var id in new[] { "act_mute", "act_gag", "act_silence" }) Ac(actor, id, "disabled", !has || !Has(actor, "@css/chat"));
        Ac(actor, "act_team", "disabled", !has || !Has(actor, "@css/kick"));
        if (!has || target is null)
        {
            Av(actor, "pl_av", "");
            Av(actor, "pl_name", "No player picked");
            Av(actor, "pl_meta", "Pick a player in the list.");
            Av(actor, "pl_sid", "-");
            Av(actor, "pl_ip", "-");
            Av(actor, "pl_pen", "-");
            Ac(actor, "pl_rank", "hidden", true);
            foreach (var chip in new[] { "mute", "gag", "silence" }) { Ac(actor, $"pl_chip_{chip}", "on", false); Av(actor, $"pl_chip_{chip}_text", $"{chip.ToUpperInvariant()} · -"); }
            Av(actor, "act_mute_text", "Mute"); Av(actor, "act_gag_text", "Gag"); Av(actor, "act_silence_text", "Silence (mute + gag)");
            Av(actor, "act_team_text", "Move team");
            Av(actor, "pl_note", "");
            return;
        }
        var name = target.PlayerName;
        Av(actor, "pl_av", (name.Length >= 2 ? name[..2] : name).ToUpperInvariant());
        Av(actor, "pl_name", name);
        var stats = target.ActionTrackingServices?.MatchStats;
        Av(actor, "pl_meta", $"{TeamLabel(target)} · {(stats is null ? "-" : $"{stats.Kills} / {stats.Deaths}")} · {target.Ping} ms");
        Av(actor, "pl_sid", target.SteamID.ToString());
        // The address is private: only staff who may ban see it.
        Av(actor, "pl_ip", Has(actor, "@css/ban") ? Ip(target) : "Hidden");
        var muted = view.Muted || target.VoiceFlags == VoiceFlags.Muted;
        var pen = new List<string>();
        if (muted) pen.Add("Muted");
        if (view.Gagged) pen.Add("Gagged");
        Av(actor, "pl_pen", pen.Count == 0 ? "None active" : string.Join(" · ", pen));
        if (profiles.TryGetValue(target.SteamID, out var profile))
        {
            Ac(actor, "pl_rank", "hidden", false);
            As(actor, "pl_rank", "rank", $"rank-{profile.RankId}");
        }
        else Ac(actor, "pl_rank", "hidden", true);
        var silenced = muted && view.Gagged;
        Ac(actor, "pl_chip_mute", "on", muted); Av(actor, "pl_chip_mute_text", $"MUTE · {(muted ? "on" : "off")}");
        Ac(actor, "pl_chip_gag", "on", view.Gagged); Av(actor, "pl_chip_gag_text", $"GAG · {(view.Gagged ? "on" : "off")}");
        Ac(actor, "pl_chip_silence", "on", silenced); Av(actor, "pl_chip_silence_text", $"SILENCE · {(silenced ? "on" : "off")}");
        Av(actor, "act_mute_text", muted ? "Unmute" : "Mute");
        Av(actor, "act_gag_text", view.Gagged ? "Ungag" : "Gag");
        Av(actor, "act_silence_text", silenced ? "Unsilence" : "Silence (mute + gag)");
        Av(actor, "act_team_text", target.TeamNum == 3 ? "Move to T" : "Move to CT");
        Av(actor, "pl_note", "Mute, gag and silence ask for a length. IP is shown only to staff who may ban.");
    }

    private void DrawAdminServer(CCSPlayerController actor, AdminView view)
    {
        var current = Server.MapName;
        foreach (var map in AdminMaps)
        {
            Ac(actor, $"map_{map}_now", "hidden", map != current);
            Ac(actor, $"map_{map}", "sel", map == view.Map);
        }
        var label = view.Map is null ? "Pick a map" : $"Change to {AdminMapName(view.Map)}";
        Av(actor, "srv_change_text", label);
        Ac(actor, "srv_change", "disabled", view.Map is null || !Has(actor, "@css/generic"));
        var armed = (DateTime.UtcNow - view.WarmupAsked).TotalSeconds <= 10;
        Av(actor, "srv_warmup_text", armed ? "Click again to confirm" : "Back to warmup");
        Ac(actor, "srv_warmup", "disabled", !Has(actor, "@css/generic"));
        Ac(actor, "srv_clean", "disabled", !Has(actor, "@css/slay"));
        Ac(actor, "srv_rr", "disabled", !Has(actor, "@css/generic"));
    }

    private static string AdminMapName(string id) => id switch
    {
        "de_dust2" => "Dust II", "de_mirage" => "Mirage", "de_inferno" => "Inferno", "de_nuke" => "Nuke", "de_ancient" => "Ancient",
        "de_anubis" => "Anubis", "de_overpass" => "Overpass", "de_vertigo" => "Vertigo", "de_train" => "Train", _ => id,
    };

    // ---- the Ban step (also asks for the length of a mute, gag or silence) ---------------------------------------

    private void DrawStep(CCSPlayerController actor, AdminView view)
    {
        var target = Utilities.GetPlayerFromSlot(view.StepTarget);
        if (target is not { IsValid: true }) { CloseStep(actor, view); return; }
        var isBan = view.StepKind == "ban";
        var verb = view.StepKind switch { "ban" => view.IpBan ? "IP ban" : "Ban", "mute" => "Mute", "gag" => "Gag", _ => "Silence" };
        Av(actor, "ban_title", isBan ? $"Ban {target.PlayerName}" : $"{verb} {target.PlayerName}");
        Av(actor, "ban_sub", $"{TeamLabel(target)} · {target.SteamID}" + (Has(actor, "@css/ban") ? $" · {Ip(target)}" : ""));
        Av(actor, "ban_ip_sub", Has(actor, "@css/ban") && Ip(target) != "-" ? $"Blocks {Ip(target)}, so new accounts from this address cannot join either." : "No address to block for this player.");
        Ac(actor, "ban_typebox", "hidden", !isBan);
        Ac(actor, "ban_whybox", "hidden", !isBan);
        Ac(actor, "ban_type_steam", "sel", !view.IpBan);
        Ac(actor, "ban_type_ip", "sel", view.IpBan);
        foreach (var (key, _, _) in BanLengths) Ac(actor, $"ban_len_{key}", "sel", key == view.Length);
        foreach (var (key, _) in BanReasons) Ac(actor, $"ban_why_{key}", "sel", key == view.Reason);
        var len = BanLengths.First(l => l.Key == view.Length).Label.ToLowerInvariant();
        var why = BanReasons.First(r => r.Key == view.Reason).Label;
        Av(actor, "ban_confirm_text", isBan ? $"{verb} for {len} · {why}" : $"{verb} for {len}");
        Av(actor, "ban_note", isBan ? "A reason that is not in the list is typed in chat afterwards." : "The player is told how long it lasts.");
    }

    private void OpenStep(CCSPlayerController actor, AdminView view, string kind, CCSPlayerController target)
    {
        view.StepOpen = true;
        view.StepKind = kind;
        view.StepTarget = target.Slot;
        view.IpBan = false;
        view.Length = kind == "ban" ? "7d" : "1h";
        view.Reason = "grief";
        DrawStep(actor, view);
        Ac(actor, "ban_dim", "shown", true);
        Ac(actor, "ban", "shown", true);
    }

    private void CloseStep(CCSPlayerController actor, AdminView view)
    {
        view.StepOpen = false;
        Ac(actor, "ban", "shown", false);
        Ac(actor, "ban_dim", "shown", false);
    }

    // ---- clicks --------------------------------------------------------------------------------------------------

    private void OnAdminEvent(PanelEvent e)
    {
        var panel = admin;
        var actor = e.Player;
        if (panel is null || actor is not { IsValid: true }) return;
        if (e.Action == PanelAction.Close) { CloseAdmin(actor); return; }
        if (e.Action != PanelAction.Button || !adminViews.TryGetValue(actor.Slot, out var view)) return;
        var id = e.ElementId;
        if (id != "adm_close") MenuSound(actor, soundClick, LevelClick);

        if (view.StepOpen)
        {
            OnStepClick(actor, view, id);
            return;
        }
        switch (id)
        {
            case "adm_close": CloseAdmin(actor); return;
            case "adm_tab_players": ShowAdminTab(actor, view, 0); return;
            case "adm_tab_server": ShowAdminTab(actor, view, 1); return;
            case "adm_tab_bans": ShowAdminTab(actor, view, 2); return;
            case "adm_tab_logins": ShowAdminTab(actor, view, 3); return;
            case "adm_tab_staff": ShowAdminTab(actor, view, 4); return;
        }
        if (view.Tab >= 2 && OnAdminDataClick(actor, view, id)) return;
        if (id.StartsWith("pl_row", StringComparison.Ordinal) && int.TryParse(id[6..], out var row) && row >= 0 && row < view.RowCount)
        {
            view.Selected = view.Rows[row];
            view.Muted = view.Gagged = false;
            DrawAdminPlayers(actor, view);
            // LegacyX-Admin answers with lx_hud_admin status ..., which fills in the penalties.
            Server.ExecuteCommand($"lx_admin_status {actor.Slot} {view.Selected}");
            return;
        }
        if (id.StartsWith("map_de_", StringComparison.Ordinal))
        {
            var map = id[4..];
            if (AdminMaps.Contains(map)) { view.Map = map; DrawAdminServer(actor, view); }
            return;
        }
        if (view.ServerTab) { OnServerClick(actor, view, id); return; }
        OnPlayerAction(actor, view, id);
    }

    private void OnPlayerAction(CCSPlayerController actor, AdminView view, string id)
    {
        var target = view.Selected >= 0 ? Utilities.GetPlayerFromSlot(view.Selected) : null;
        if (target is not { IsValid: true }) return;
        var slot = actor.Slot;
        var uid = target.UserId ?? -1;
        switch (id)
        {
            case "act_kick": Server.ExecuteCommand($"lx_admin_do {slot} kick {target.Slot}"); break;
            case "act_slay": Server.ExecuteCommand($"lx_admin_do {slot} run css_slay #{uid}"); break;
            case "act_respawn": Server.ExecuteCommand($"lx_admin_do {slot} run css_respawn #{uid}"); break;
            case "act_team": Server.ExecuteCommand($"lx_admin_do {slot} run css_team #{uid} {(target.TeamNum == 3 ? "t" : "ct")}"); break;
            case "act_ban": if (Has(actor, "@css/ban")) OpenStep(actor, view, "ban", target); return;
            case "act_mute":
                if (view.Muted || target.VoiceFlags == VoiceFlags.Muted) Server.ExecuteCommand($"lx_admin_do {slot} run css_unmute #{uid}");
                else if (Has(actor, "@css/chat")) { OpenStep(actor, view, "mute", target); return; }
                break;
            case "act_gag":
                if (view.Gagged) Server.ExecuteCommand($"lx_admin_do {slot} run css_ungag #{uid}");
                else if (Has(actor, "@css/chat")) { OpenStep(actor, view, "gag", target); return; }
                break;
            case "act_silence":
                if (view.Muted && view.Gagged) Server.ExecuteCommand($"lx_admin_do {slot} run css_unsilence #{uid}");
                else if (Has(actor, "@css/chat")) { OpenStep(actor, view, "silence", target); return; }
                break;
            default: return;
        }
        // The action ran on the next server frame; ask again a moment later so the penalties on screen are current.
        var selected = view.Selected;
        AddTimer(0.6f, () =>
        {
            if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } still && adminViews.TryGetValue(slot, out var v) && v.Selected == selected)
            {
                DrawAdminPlayers(still, v);
                Server.ExecuteCommand($"lx_admin_status {slot} {selected}");
            }
        });
    }

    private void OnServerClick(CCSPlayerController actor, AdminView view, string id)
    {
        var slot = actor.Slot;
        switch (id)
        {
            case "srv_change":
                if (view.Map is not null) { Server.ExecuteCommand($"lx_admin_do {slot} map 0 {view.Map}"); CloseAdmin(actor); }
                break;
            case "srv_warmup":
                if ((DateTime.UtcNow - view.WarmupAsked).TotalSeconds <= 10)
                {
                    view.WarmupAsked = DateTime.MinValue;
                    Server.ExecuteCommand($"lx_admin_do {slot} run css_rr");
                    CloseAdmin(actor);
                }
                else
                {
                    // The server command has no chat to ask in, so the button asks: a second click within 10 seconds confirms.
                    view.WarmupAsked = DateTime.UtcNow;
                    DrawAdminServer(actor, view);
                    AddTimer(10.5f, () =>
                    {
                        if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } still && adminViews.TryGetValue(slot, out var v) && v == view) DrawAdminServer(still, v);
                    });
                }
                break;
            case "srv_clean": Server.ExecuteCommand($"lx_admin_do {slot} run css_clean"); break;
            case "srv_rr": Server.ExecuteCommand($"lx_admin_do {slot} restartround 0"); break;
        }
    }

    private void OnStepClick(CCSPlayerController actor, AdminView view, string id)
    {
        switch (id)
        {
            case "ban_close":
            case "ban_cancel":
                CloseStep(actor, view);
                return;
            case "ban_type_steam": view.IpBan = false; break;
            case "ban_type_ip": view.IpBan = true; break;
            case "ban_confirm": ConfirmStep(actor, view); return;
            default:
                if (id.StartsWith("ban_len_", StringComparison.Ordinal) && BanLengths.Any(l => l.Key == id[8..])) view.Length = id[8..];
                else if (id.StartsWith("ban_why_", StringComparison.Ordinal) && BanReasons.Any(r => r.Key == id[8..])) view.Reason = id[8..];
                else return;
                break;
        }
        DrawStep(actor, view);
    }

    private void ConfirmStep(CCSPlayerController actor, AdminView view)
    {
        var target = Utilities.GetPlayerFromSlot(view.StepTarget);
        if (target is not { IsValid: true }) { CloseStep(actor, view); return; }
        var minutes = BanLengths.First(l => l.Key == view.Length).Minutes;
        var slot = actor.Slot;
        if (view.StepKind == "ban")
        {
            var why = BanReasons.First(r => r.Key == view.Reason).Label;
            Server.ExecuteCommand($"lx_admin_do {slot} {(view.IpBan ? "ipban" : "ban")} {target.Slot} {minutes} {why}");
        }
        else Server.ExecuteCommand($"lx_admin_do {slot} {view.StepKind} {target.Slot} {minutes}");
        CloseStep(actor, view);
        var selected = view.Selected;
        AddTimer(0.8f, () =>
        {
            if (Utilities.GetPlayerFromSlot(slot) is { IsValid: true } still && adminViews.TryGetValue(slot, out var v))
            {
                DrawAdminPlayers(still, v);
                if (selected >= 0) Server.ExecuteCommand($"lx_admin_status {slot} {selected}");
            }
        });
    }
}
