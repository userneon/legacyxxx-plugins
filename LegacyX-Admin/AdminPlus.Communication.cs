using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AdminPlus;

public partial class AdminPlus
{

    private List<CommunicationPunishment> _communicationPunishments = new();

    private Dictionary<ulong, PlayerCommState> _commStates = new();
    
    private Timer? _muteEnforceTimer;
    private Timer? _expiredCheckTimer;
    private Timer? _syncTimer;
    private DateTime _lastCommRefreshUtc = DateTime.MinValue;
    private DateTime _commLocalChangeUtc = DateTime.MinValue;
    private DateTime _commErrorLogUtc = DateTime.MinValue;
    private readonly HashSet<string> _allowedChatCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rtv", "nominate", "timeleft", "rank", "top", "help", "rules",
        "admins", "hideadmin", "admin", "adminmenu", "banlist", "menu", "rs", "ws", "wp", "knife", "glove", "agent",
        "kf", "ajan", "report", "calladmin", "spec", "join", "jointeam",
        "version", "css_version"
    };

    public class CommunicationPunishment
    {
        public ulong SteamID { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public ulong AdminSteamID { get; set; }
        public DateTime Created { get; set; }
        public DateTime EndTime { get; set; }

        [JsonIgnore]
        public bool IsExpired => !IsPermanent && DateTime.Now >= EndTime;

        [JsonIgnore]
        public bool IsPermanent => Duration == 0;

        [JsonIgnore]
        public DateTime? InternalEnds => IsPermanent ? null : EndTime;
    }

    private sealed class PlayerCommState
    {
        public ulong SteamId { get; set; }
        public string Nick { get; set; } = string.Empty;
        public PunishEntry? Mute { get; set; }
        public PunishEntry? Gag { get; set; }
    }

    private sealed class PunishEntry
    {
        public bool Permanent { get; set; }
        public int DurationMinutes { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public ulong AdminSteamId { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Ends { get; set; }
    }

    public void InitializeCommunication()
    {
        LoadCommunicationData();

        AddCommandListener("say", OnPlayerSay, HookMode.Pre);
        AddCommandListener("say_team", OnPlayerSay, HookMode.Pre);

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid && !player.IsBot)
            {
                ApplyExistingPunishments(player);
            }
        }

        _muteEnforceTimer = AddTimer(10.0f, () => EnforceMutePunishments(), TimerFlags.REPEAT);
        _expiredCheckTimer = AddTimer(60.0f, () => CheckExpiredPunishments(), TimerFlags.REPEAT);
        _syncTimer = AddTimer(30.0f, () => SyncMuteSystemsFromJson(), TimerFlags.REPEAT);
    }

    private void ApplyExistingPunishments(CCSPlayerController player)
    {
        if (IsPlayerPunished(player.SteamID, "MUTE"))
        {
            player.VoiceFlags = VoiceFlags.Muted;
        }
    }

    public void RegisterCommunicationCommands()
    {
        AddCommand("mute", Localizer["Mute.Usage"], CmdMute);
        AddCommand("gag", Localizer["Gag.Usage"], CmdGag);
        AddCommand("unmute", Localizer["Unmute.Usage"], CmdUnmute);
        AddCommand("ungag", Localizer["Ungag.Usage"], CmdUngag);
        AddCommand("mutelist", Localizer["MuteList.Header"], CmdMuteList);
        AddCommand("gaglist", Localizer["GagList.Header"], CmdGagList);

        AddCommand("silence", Localizer["Silence.Usage"], CmdSilence);
        AddCommand("unsilence", Localizer["Unsilence.Usage"], CmdUnsilence);

        AddCommand("css_mute", "Mute a player from console", CmdMute);
        AddCommand("css_gag", "Gag a player from console", CmdGag);
        AddCommand("css_unmute", "Unmute a player from console", CmdUnmute);
        AddCommand("css_ungag", "Ungag a player from console", CmdUngag);
        AddCommand("css_mutelist", "List muted players from console", CmdMuteList);
        AddCommand("css_gaglist", "List gagged players from console", CmdGagList);

        AddCommand("css_silence", "Mute+Gag a player from console", CmdSilence);
        AddCommand("css_unsilence", "Remove mute+gag from a player (console)", CmdUnsilence);
        
        AddCommand("css_cleanall", "Clean all punishments manually", CmdCleanAll);
        AddCommand("css_cleanmute", "Clean all mute punishments manually", CmdCleanMute);
        AddCommand("css_cleangag", "Clean all gag punishments manually", CmdCleanGag);
        
    }

    /// <summary>Mutes and gags live only in the database; this reads them (at start and every few seconds).</summary>
    private void LoadCommunicationData() => _ = RefreshCommunicationAsync();

    private async System.Threading.Tasks.Task RefreshCommunicationAsync()
    {
        if (!_centralPenaltiesEnabled) return;
        var startedAt = DateTime.UtcNow;
        List<CentralCommPenalty> rows;
        try
        {
            rows = await FetchActiveCommPenaltiesAsync();
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _commErrorLogUtc > TimeSpan.FromMinutes(5))
            {
                _commErrorLogUtc = DateTime.UtcNow;
                LogError($"Mute and gag list refresh failed; the last copy stays: {ex.Message}");
            }
            return;
        }

        var punishments = rows
            .Where(row => ulong.TryParse(row.SteamId, out _))
            .Select(row =>
            {
                var created = row.CreatedAt.ToLocalTime().DateTime;
                var ends = row.IsPermanent || row.ExpiresAt == null ? DateTime.MaxValue : row.ExpiresAt.Value.ToLocalTime().DateTime;
                return new CommunicationPunishment
                {
                    SteamID = ulong.Parse(row.SteamId),
                    PlayerName = string.IsNullOrWhiteSpace(row.Name) ? "Unknown" : SanitizeName(row.Name),
                    Type = row.Type == "gag" ? "GAG" : "MUTE",
                    Duration = row.IsPermanent ? 0 : Math.Max(1, (int)Math.Round((ends - created).TotalMinutes)),
                    Reason = row.Reason,
                    AdminName = row.Issuer,
                    AdminSteamID = 0,
                    Created = created,
                    EndTime = ends,
                };
            })
            .Where(p => !p.IsExpired)
            .ToList();

        // Game state may only be touched on the main thread.
        Server.NextFrame(() =>
        {
            // A mute or gag made on this server a moment ago may not have reached the database yet; keep it and refresh again later.
            if (_commLocalChangeUtc > startedAt - TimeSpan.FromSeconds(3)) return;
            _communicationPunishments = punishments;
            foreach (var state in _commStates.Values) { state.Mute = null; state.Gag = null; }
            SyncMuteSystemsFromJson();
        });
    }

    private void SyncMuteSystemsFromJson()
    {
        try
        {
            foreach (var punishment in _communicationPunishments.Where(p => !p.IsExpired))
            {
                if (!_commStates.ContainsKey(punishment.SteamID))
                {
                    _commStates[punishment.SteamID] = new PlayerCommState
                    {
                        SteamId = punishment.SteamID,
                        Nick = punishment.PlayerName
                    };
                }

                var state = _commStates[punishment.SteamID];

                var punishEntry = new PunishEntry
                {
                    Permanent = punishment.IsPermanent,
                    DurationMinutes = punishment.Duration,
                    Reason = punishment.Reason,
                    AdminName = punishment.AdminName,
                    AdminSteamId = punishment.AdminSteamID,
                    Created = punishment.Created,
                    Ends = punishment.InternalEnds
                };

                if (punishment.Type == "MUTE")
                    state.Mute = punishEntry;
                else if (punishment.Type == "GAG")
                    state.Gag = punishEntry;
            }

            foreach (var kv in _commStates.ToList())
            {
                if (kv.Value.Mute != null && !IsPunishmentActive(kv.Value.Mute))
                    kv.Value.Mute = null;
                if (kv.Value.Gag != null && !IsPunishmentActive(kv.Value.Gag))
                    kv.Value.Gag = null;

                if (kv.Value.Mute == null && kv.Value.Gag == null)
                    _commStates.Remove(kv.Key);
            }
        }
        catch (Exception ex)
        {
            LogError($"in SyncMuteSystemsFromJson: {ex.Message}");
        }
    }

    private bool IsPunishmentActive(PunishEntry? entry)
        => entry != null && (entry.Permanent || (entry.Ends != null && entry.Ends > DateTime.Now));

    /// <summary>Nothing is written to disk. This marks a local change so a refresh already under way does not undo it.</summary>
    private void SaveCommunicationData() => _commLocalChangeUtc = DateTime.UtcNow;

    private void TryRefreshCommunicationDataIfChanged()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCommRefreshUtc).TotalSeconds < 5) return;
        _lastCommRefreshUtc = now;
        LoadCommunicationData();
    }

    private void EnforceMutePunishments()
    {
        try
        {
            var players = Utilities.GetPlayers();

            TryRefreshCommunicationDataIfChanged();
            SyncMuteSystemsFromJson();

            foreach (var player in players)
            {
                if (!player.IsValid || player.IsBot) continue;

                bool isMuted = IsPlayerPunished(player.SteamID, "MUTE") ||
                              (_commStates.TryGetValue(player.SteamID, out var state) &&
                               state.Mute != null && IsPunishmentActive(state.Mute));

                if (isMuted && player.VoiceFlags != VoiceFlags.Muted)
                {
                    player.VoiceFlags = VoiceFlags.Muted;
                }
                else if (!isMuted && player.VoiceFlags == VoiceFlags.Muted)
                {
                    player.VoiceFlags = VoiceFlags.Normal;
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"in EnforceMutePunishments: {ex.Message}");
        }
    }

    private void CheckExpiredPunishments()
    {
        try
        {
            int removedCount = 0;
            var players = Utilities.GetPlayers();

            for (int i = _communicationPunishments.Count - 1; i >= 0; i--)
            {
                var punishment = _communicationPunishments[i];

                if (punishment.IsExpired)
                {
                    var player = players.FirstOrDefault(pl => pl.IsValid && pl.SteamID == punishment.SteamID);

                    if (player != null)
                    {
                        if (punishment.Type == "MUTE")
                        {
                            player.VoiceFlags = VoiceFlags.Normal;
                        }
                        
                    }

                    _communicationPunishments.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                SaveCommunicationData();
                SyncMuteSystemsFromJson();
            }
            else
            {
                SaveCommunicationData();
            }
        }
        catch (Exception ex)
        {
            LogError($"in CheckExpiredPunishments: {ex.Message}");
        }
    }

    private bool IsPlayerPunished(ulong steamId, string type)
    {
        return _communicationPunishments.Any(p => p.SteamID == steamId && p.Type == type && !p.IsExpired);
    }

    private void RemovePunishment(ulong steamId, string type, CCSPlayerController? caller = null)
    {
        var punishment = _communicationPunishments.FirstOrDefault(p => p.SteamID == steamId && p.Type == type);
        
        int removedCount = _communicationPunishments.RemoveAll(p => p.SteamID == steamId && p.Type == type);

        if (_commStates.TryGetValue(steamId, out var state))
        {
            if (type == "MUTE")
                state.Mute = null;
            else if (type == "GAG")
                state.Gag = null;

            if (state.Mute == null && state.Gag == null)
                _commStates.Remove(steamId);
        }

        SaveCommunicationData();
        
        if (removedCount > 0)
        {
            ReportCommunicationLifted(steamId, type, caller);
            
            if (punishment != null)
            {
                string executorName = GetExecutorName(caller);
                AddTimer(0.1f, () => {
                    _ = Discord.SendCommunicationLog(punishment.PlayerName, steamId, executorName, caller?.SteamID ?? 0, punishment.Reason, 0, type, false, this);
                });
            }
        }

        if (type == "MUTE")
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
            if (player != null)
            {
                player.VoiceFlags = VoiceFlags.Normal;
            }
        }
    }

    private void CmdMute(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Mute.Usage", "Usage: css_mute <target> [duration] [reason]");
            return;
        }

        HandlePunishment(caller, info, "MUTE");
    }

    private void CmdGag(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Gag.Usage", "Usage: css_gag <target> [duration] [reason]");
            return;
        }

        HandlePunishment(caller, info, "GAG");
    }

    private void CmdUnmute(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Unmute.Usage", "Usage: css_unmute <target>");
            return;
        }

        HandleUnpunishment(caller, info, "MUTE");
    }

    private void CmdUngag(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Ungag.Usage", "Usage: css_ungag <target>");
            return;
        }

        HandleUnpunishment(caller, info, "GAG");
    }

    private void CmdSilence(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Silence.Usage", "Usage: css_silence <target> [duration] [reason]");
            return;
        }

        var targetInput = info.GetArg(1);
        var target = FindPlayerByNameOrId(targetInput);

        if (target == null)
        {
            SendErrorMessage(caller, "NoMatchingClient", "No matching player found.");
            return;
        }

        if (caller != null && caller.IsValid && CheckImmunity(caller, target))
        {
            caller.Print(Localizer["Punish.ImmunityBlocked"]);
            return;
        }

        int duration = 0;
        if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var parsed))
            duration = parsed;

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 4)
        {
            reason = string.Join(" ", Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));
        }

        ApplyPunishment(target, "MUTE", duration, reason, caller);
        ApplyPunishment(target, "GAG", duration, reason, caller);

        string executorName = GetExecutorName(caller);
        string targetName = SanitizeName(target.PlayerName);

        if (duration == 0)
            PlayerExtensions.PrintToAll(Localizer["PermaSILENCE", executorName, targetName, reason]);
        else
            PlayerExtensions.PrintToAll(Localizer["SILENCE", executorName, targetName, duration, reason]);

        LogAction($"{executorName} silenced {targetName} ({target.SteamID}) for {duration} minutes. Reason: {reason}");
    }

    private void CmdUnsilence(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        if (info.ArgCount < 2)
        {
            SendUsageMessage(caller, "Unsilence.Usage", "Usage: css_unsilence <target>");
            return;
        }

        var targetInput = info.GetArg(1);
        var target = FindPlayerByNameOrId(targetInput);

        if (target == null)
        {
            SendErrorMessage(caller, "NoMatchingClient", "No matching player found.");
            return;
        }

        RemovePunishment(target.SteamID, "MUTE", caller);
        RemovePunishment(target.SteamID, "GAG", caller);

        string executorName = GetExecutorName(caller);
        string targetName = SanitizeName(target.PlayerName);

        PlayerExtensions.PrintToAll(Localizer["Unsilence", executorName, targetName]);
        LogAction($"{executorName} unsilenced {targetName} ({target.SteamID})");
    }

    internal void ApplyPunishment(CCSPlayerController target, string type, int duration, string reason, CCSPlayerController? caller = null)
    {
        string executorName = GetExecutorName(caller);
        string targetName = SanitizeName(target.PlayerName);

        var punishment = new CommunicationPunishment
        {
            SteamID = target.SteamID,
            PlayerName = targetName,
            Type = type,
            Duration = duration,
            Reason = reason,
            AdminName = executorName,
            AdminSteamID = caller?.SteamID ?? 0,
            Created = DateTime.Now,
            EndTime = duration == 0 ? DateTime.MaxValue : DateTime.Now.AddMinutes(duration)
        };

        _communicationPunishments.Add(punishment);

        if (!_commStates.ContainsKey(target.SteamID))
        {
            _commStates[target.SteamID] = new PlayerCommState
            {
                SteamId = target.SteamID,
                Nick = targetName
            };
        }

        var state = _commStates[target.SteamID];
        var punishEntry = new PunishEntry
        {
            Permanent = duration == 0,
            DurationMinutes = duration,
            Reason = reason,
            AdminName = executorName,
            AdminSteamId = caller?.SteamID ?? 0,
            Created = DateTime.Now,
            Ends = duration == 0 ? null : DateTime.Now.AddMinutes(duration)
        };

        if (type == "MUTE")
        {
            state.Mute = punishEntry;
            target.VoiceFlags = VoiceFlags.Muted;
        }
        else if (type == "GAG")
        {
            state.Gag = punishEntry;
        }

        SaveCommunicationData();
        ReportCommunication(target.SteamID, type, duration, reason, caller);

        AddTimer(0.1f, () => {
            _ = Discord.SendCommunicationLog(target.PlayerName, target.SteamID, executorName, caller?.SteamID ?? 0, reason, duration, type, true, this);
        });
    }

    private void HandlePunishment(CCSPlayerController? caller, CommandInfo info, string type)
    {
        var targetInput = info.GetArg(1);
        var target = FindPlayerByNameOrId(targetInput);

        if (target == null)
        {
            SendErrorMessage(caller, "NoMatchingClient", "No matching player found.");
            return;
        }

        if (caller != null && caller.IsValid && CheckImmunity(caller, target))
        {
            caller.Print(Localizer["Punish.ImmunityBlocked"]);
            return;
        }

        if (IsPlayerPunished(target.SteamID, type))
        {
            SendErrorMessage(caller, type == "MUTE" ? "Mute.Already" : "Gag.Already",
                type == "MUTE" ? "Player is already muted." : "Player is already gagged.");
            return;
        }

        int duration = 0;
        if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var parsed))
            duration = parsed;

        string reason = Localizer["Ban.NoReason"];
        if (info.ArgCount >= 4)
        {
            reason = string.Join(" ", Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));
        }

        ApplyPunishment(target, type, duration, reason, caller);

        string executorName = GetExecutorName(caller);
        string targetName = SanitizeName(target.PlayerName);

        if (duration == 0)
            PlayerExtensions.PrintToAll(Localizer[$"Perma{type}Reason", executorName, targetName, reason]);
        else
            PlayerExtensions.PrintToAll(Localizer[$"{type}Reason", executorName, targetName, duration, reason]);

        LogAction($"{executorName} {type.ToLower()}ed {targetName} ({target.SteamID}) for {duration} minutes. Reason: {reason}");
    }

    private void HandleUnpunishment(CCSPlayerController? caller, CommandInfo info, string type)
    {
        var targetInput = info.GetArg(1);
        var target = FindPlayerByNameOrId(targetInput);

        if (target == null)
        {
            SendErrorMessage(caller, "NoMatchingClient", "No matching player found.");
            return;
        }

        if (!IsPlayerPunished(target.SteamID, type))
        {
            SendErrorMessage(caller, type == "MUTE" ? "Mute.Not" : "Gag.Not",
                type == "MUTE" ? "Player is not muted." : "Player is not gagged.");
            return;
        }

        RemovePunishment(target.SteamID, type, caller);

        string executorName = GetExecutorName(caller);
        string targetName = SanitizeName(target.PlayerName);

        PlayerExtensions.PrintToAll(Localizer[$"Un{type.ToLower()}", executorName, targetName]);
        LogAction($"{executorName} un{type.ToLower()}ed {targetName} ({target.SteamID})");
    }

    private void CmdMuteList(CCSPlayerController? caller, CommandInfo info)
    {
        var activeMutes = _communicationPunishments.Where(p => p.Type == "MUTE" && !p.IsExpired).ToList();

        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Mute list:");
            if (!activeMutes.Any())
            {
                Console.WriteLine("  Mute list is empty.");
            }
            else
            {
                foreach (var mute in activeMutes)
                {
                    string duration = mute.IsPermanent ? "Permanent" : $"{mute.Duration} minutes";
                    string remaining = mute.IsPermanent ? "Permanent" : $"{(mute.EndTime - DateTime.Now).TotalMinutes:F0} minutes";
                    Console.WriteLine($"  • {mute.PlayerName} ({mute.SteamID}) - {duration} - Remaining: {remaining}");
                }
            }
            return;
        }

        if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["MuteList.Header"]);
        if (!activeMutes.Any())
        {
            menu?.AddMenuOption(Localizer["MuteList.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var mute in activeMutes)
            {
                string duration = mute.IsPermanent ? Localizer["Duration.Forever"] : $"{mute.Duration} {Localizer["Duration.Minute"]}";
                string remaining = mute.IsPermanent ? Localizer["Duration.Forever"] : $"{(mute.EndTime - DateTime.Now).TotalMinutes:F0} {Localizer["Duration.Minute"]}";

                menu?.AddMenuOption(Localizer["MuteList.Item", mute.PlayerName, duration, remaining], (ply, opt) => { });
            }
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private void CmdGagList(CCSPlayerController? caller, CommandInfo info)
    {
        var activeGags = _communicationPunishments.Where(p => p.Type == "GAG" && !p.IsExpired).ToList();

        if (caller == null)
        {
            Console.WriteLine("[AdminPlus] Gag list:");
            if (!activeGags.Any())
            {
                Console.WriteLine("  Gag list is empty.");
            }
            else
            {
                foreach (var gag in activeGags)
                {
                    string duration = gag.IsPermanent ? "Permanent" : $"{gag.Duration} minutes";
                    string remaining = gag.IsPermanent ? "Permanent" : $"{(gag.EndTime - DateTime.Now).TotalMinutes:F0} minutes";
                    Console.WriteLine($"  • {gag.PlayerName} ({gag.SteamID}) - {duration} - Remaining: {remaining}");
                }
            }
            return;
        }

        if (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/chat"))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        var menu = CreateMenu(Localizer["GagList.Header"]);
        if (!activeGags.Any())
        {
            menu?.AddMenuOption(Localizer["GagList.Empty"], (ply, opt) => { });
        }
        else
        {
            foreach (var gag in activeGags)
            {
                string duration = gag.IsPermanent ? Localizer["Duration.Forever"] : $"{gag.Duration} {Localizer["Duration.Minute"]}";
                string remaining = gag.IsPermanent ? Localizer["Duration.Forever"] : $"{(gag.EndTime - DateTime.Now).TotalMinutes:F0} {Localizer["Duration.Minute"]}";

                menu?.AddMenuOption(Localizer["GagList.Item", gag.PlayerName, duration, remaining], (ply, opt) => { });
            }
        }

        if (menu != null)
        {
            menu.ExitButton = true;
            OpenMenu(caller, menu);
        }
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var chatText = info.GetArg(1) ?? "";
        if (HandleSuppressedPluginChatCommand(player, chatText))
            return HookResult.Handled;
        if (TryHandlePublicVersionChatCommand(player, chatText))
            return HookResult.Handled;

        TryRefreshCommunicationDataIfChanged();

        bool isGagged = IsPlayerPunished(player.SteamID, "GAG") ||
                       (_commStates.TryGetValue(player.SteamID, out var state) &&
                        state.Gag != null && IsPunishmentActive(state.Gag));

        if (!isGagged)
            return SendFormattedChat(player, info, chatText);

        string message = chatText;

        if (IsPrefixedCommand(message) && IsAllowedCommand(message))
            return HookResult.Continue;

        var punishment = _communicationPunishments
            .FirstOrDefault(p => p.SteamID == player.SteamID && p.Type == "GAG" && !p.IsExpired);

        if (punishment != null)
        {
            if (punishment.IsPermanent)
            {
                player.Print(Localizer["Gag.Message.Permanent"]);
            }
            else
            {
                int remainingMinutes = (int)Math.Ceiling((punishment.EndTime - DateTime.Now).TotalMinutes);
                remainingMinutes = Math.Max(0, remainingMinutes);
                player.Print(Localizer["Gag.Message.Temporary", remainingMinutes]);
            }
        }
        else
        {
            player.Print(Localizer["Gag.Message.Permanent"]);
        }

        return HookResult.Stop;
    }

    /// <summary>
    /// Plain chat becomes "ALL | Name: text" / "TEAM | Name: text". Commands, @ staff chat, menu input and a report reason being typed
    /// are left to their own handlers; the dead only talk to the dead, and a team line only goes to that team.
    /// </summary>
    private HookResult SendFormattedChat(CCSPlayerController player, CommandInfo info, string text)
    {
        text = text.Trim();
        // '!' and '/' are plugin commands, '@' is staff chat, '.' is MatchZy (.ready, .pause, .stay …): MatchZy and the report prompt read
        // the player_chat event, which only fires when the say command runs, so these must stay native chat.
        if (text.Length == 0 || text[0] is '!' or '/' or '@' or '.') return HookResult.Continue;
        if (_selectedReportTarget != null) return HookResult.Continue;
        if (_menuStates.TryGetValue(player.Slot, out var menu) && menu.InputMode) return HookResult.Continue;
        var team = string.Equals(info.GetArg(0), "say_team", StringComparison.OrdinalIgnoreCase);
        // No rank or staff role in chat (owner request): just ALL | Name: text, the name in its team's color.
        var line = LegacyXChat.Player(team, player.PlayerName ?? player.SteamID.ToString(), text, teamNum: player.TeamNum);
        var alive = player.PawnIsAlive;
        foreach (var recipient in Utilities.GetPlayers())
        {
            if (!recipient.IsValid || recipient.IsBot) continue;
            if (team && recipient.TeamNum != player.TeamNum) continue;
            if (!alive && recipient.PawnIsAlive && recipient.TeamNum > 1) continue;
            recipient.PrintToChat(line);
        }
        Console.WriteLine($"[chat] {(team ? "TEAM" : "ALL")} | {player.PlayerName}: {text}");
        _ = Discord.SendChatLog(player.PlayerName, player.SteamID.ToString(), text, team ? "Team Chat" : "All Chat", this);
        return HookResult.Handled;
    }

    private static bool IsPrefixedCommand(string s)
        => !string.IsNullOrEmpty(s) && (s[0] == '!' || s[0] == '/');

    private static string ExtractCommandToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        int i = 0;
        while (i < s.Length && (s[i] == '!' || s[i] == '/' || s[i] == ' ')) i++;
        int start = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
        return s.Substring(start, i - start);
    }

    private bool IsAllowedCommand(string message)
    {
        if (IsPrefixedCommand(message))
        {
            string command = ExtractCommandToken(message).ToLower();
            return _allowedChatCommands.Contains(command);
        }
        return false;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot)
        {
            TryRefreshCommunicationDataIfChanged();
            bool isMuted = IsPlayerPunished(player.SteamID, "MUTE") ||
                          (_commStates.TryGetValue(player.SteamID, out var state) &&
                           state.Mute != null && IsPunishmentActive(state.Mute));

            if (isMuted)
            {
                player.VoiceFlags = VoiceFlags.Muted;
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot)
        {
            TryRefreshCommunicationDataIfChanged();
            bool isMuted = IsPlayerPunished(player.SteamID, "MUTE") ||
                          (_commStates.TryGetValue(player.SteamID, out var state) &&
                           state.Mute != null && IsPunishmentActive(state.Mute));

            if (isMuted)
            {
                AddTimer(0.5f, () => {
                    if (player.IsValid)
                    {
                        player.VoiceFlags = VoiceFlags.Muted;
                    }
                });
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot)
        {
            _commStates.Remove(player.SteamID);
        }
        return HookResult.Continue;
    }


    private void CmdCleanAll(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        // Mutes and gags are kept in the database only; lift them one by one with !unmute / !ungag, or on the website.
        const string message = "Mutes and gags are kept on the website. Lift them with !unmute / !ungag, or on the penalties page.";
        if (caller != null && caller.IsValid) caller.PrintToChat(LegacyXChat.System(message));
        else Console.WriteLine($"[LEGACY-X Admin] {message}");
    }

    private void CmdCleanMute(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        // Mutes and gags are kept in the database only; lift them one by one with !unmute / !ungag, or on the website.
        const string message = "Mutes and gags are kept on the website. Lift them with !unmute / !ungag, or on the penalties page.";
        if (caller != null && caller.IsValid) caller.PrintToChat(LegacyXChat.System(message));
        else Console.WriteLine($"[LEGACY-X Admin] {message}");
    }

    private void CmdCleanGag(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && (!caller.IsValid || !AdminManager.PlayerHasPermissions(caller, "@css/root")))
        {
            if (caller.IsValid) caller.Print(Localizer["NoPermission"]);
            return;
        }

        // Mutes and gags are kept in the database only; lift them one by one with !unmute / !ungag, or on the website.
        const string message = "Mutes and gags are kept on the website. Lift them with !unmute / !ungag, or on the penalties page.";
        if (caller != null && caller.IsValid) caller.PrintToChat(LegacyXChat.System(message));
        else Console.WriteLine($"[LEGACY-X Admin] {message}");
    }

    public void CleanupCommunication()
    {
        try
        {
            _muteEnforceTimer?.Kill();
            _muteEnforceTimer = null;
            
            _expiredCheckTimer?.Kill();
            _expiredCheckTimer = null;
            
            _syncTimer?.Kill();
            _syncTimer = null;
            
            _communicationPunishments.Clear();
            _commStates.Clear();
            
            RemoveCommandListener("say", OnPlayerSay, HookMode.Pre);
            RemoveCommandListener("say_team", OnPlayerSay, HookMode.Pre);
            
        }
        catch (Exception ex)
        {
            LogError($"during communication cleanup: {ex.Message}");
        }
    }

}