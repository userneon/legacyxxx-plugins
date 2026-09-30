using System;
using System.Threading.Tasks;
using LegacyX.Shared.Configuration;

namespace AdminPlus;

// LEGACY-X policy: nothing is sent to Discord from the game server. Every moderation, connection, chat, match
// and status event stays inside the game/API/database. Player !report / !calladmin / !callmanager requests go to
// the API (see AdminPlus.CentralPenalties.cs) and the LEGACY-X Discord bot posts them in its admin calls channel.
// The old call-channel webhook (LEGACYX_ADMIN_CALL_CHANNEL_*) is gone; those settings are ignored.
public static class Discord
{
    public static string ConfiguredServerAddress = string.Empty;

    public static void LoadConfig()
    {
        ConfiguredServerAddress = LegacyXEnvironmentLoader.Load().Get("LEGACYX_SERVER_ADDRESS");
    }

    public static void Dispose() { }

    // Kept as no-ops so upstream AdminPlus call sites remain compatible without any outbound event delivery.
    public static void StartStatusTimer(AdminPlus plugin) { }
    public static void StopStatusTimer() { }
    public static void RegisterDiscordCommands(AdminPlus plugin) { }
    public static Task SendCommunicationLog(string playerName, ulong playerSteamId, string adminName, ulong adminSteamId, string reason, int duration, string actionType, bool isApplied, AdminPlus plugin) => Task.CompletedTask;
    public static Task SendAdminActionLog(string action, string targetName, ulong targetSteamId, string adminName, ulong adminSteamId, string reason, AdminPlus plugin) => Task.CompletedTask;
    public static Task SendBanLog(string playerName, string steamId, string adminName, string reason, string duration, bool isUnban, AdminPlus plugin) => Task.CompletedTask;
    public static Task SendChatLog(string playerName, string steamId, string message, string channel, AdminPlus plugin) => Task.CompletedTask;
    public static Task SendConnectionLog(string playerName, string steamId, string action, AdminPlus plugin) => Task.CompletedTask;
    public static Task SendServerStatus(AdminPlus plugin, string status, int playerCount, int maxPlayers, string currentMap, string uptime, string serverIp = "", string timeLeft = "") => Task.CompletedTask;
}
