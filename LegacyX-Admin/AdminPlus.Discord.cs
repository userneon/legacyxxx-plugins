using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Core;
using LegacyX.Shared.Configuration;

namespace AdminPlus;

// LEGACY-X policy: Discord is an opt-in Call channel for player !admin/report alerts only.
// Every moderation, connection, chat, match and status event remains inside the game/API/database.
public static class Discord
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static string _callChannelWebhook = string.Empty;
    private static string _callChannelMention = string.Empty;
    public static string ConfiguredServerAddress = string.Empty;

    public static void LoadConfig()
    {
        var environment = LegacyXEnvironmentLoader.Load();
        ConfiguredServerAddress = environment.Get("LEGACYX_SERVER_ADDRESS");
        _callChannelMention = environment.GetModule("ADMIN", "CALL_CHANNEL_MENTION", string.Empty);
        _callChannelWebhook = environment.GetModuleBoolean("ADMIN", "CALL_CHANNEL_ENABLED", false)
            ? NormalizeCallWebhook(environment.GetModule("ADMIN", "CALL_CHANNEL_WEBHOOK"))
            : string.Empty;
    }

    public static void Dispose() => Http.Dispose();

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

    // This is the sole allowed outbound Discord event: an in-game player report / !admin call.
    public static async Task SendPlayerReport(string reporterName, string reporterSteamId, string reportedName, string reportedSteamId, string reason, string serverIp, AdminPlus plugin)
    {
        if (string.IsNullOrWhiteSpace(_callChannelWebhook)) return;

        var payload = new
        {
            content = NormalizeMention(_callChannelMention),
            allowed_mentions = new { parse = Array.Empty<string>() },
            embeds = new[]
            {
                new
                {
                    title = "LEGACY-X player call",
                    color = 0xd9b45f,
                    fields = new[]
                    {
                        new { name = "Reporter", value = $"{Safe(reporterName)}\nSteamID: `{Safe(reporterSteamId)}`", inline = true },
                        new { name = "Reported player", value = $"{Safe(reportedName)}\nSteamID: `{Safe(reportedSteamId)}`", inline = true },
                        new { name = "Reason", value = Safe(reason), inline = false },
                        new { name = "Server", value = Safe(serverIp), inline = false },
                    },
                    timestamp = DateTime.UtcNow,
                },
            },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _callChannelWebhook)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                AdminPlus.LogError($"Call channel delivery failed ({(int)response.StatusCode}).");
        }
        catch (Exception exception)
        {
            AdminPlus.LogError($"Call channel delivery error: {exception.Message}");
        }
    }

    private static string NormalizeCallWebhook(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return string.Empty;
        if (!(uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)))
            return string.Empty;
        return uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal) ? uri.ToString() : string.Empty;
    }

    private static string NormalizeMention(string value)
    {
        // Explicit role/user mention only; never allow @everyone/@here from configuration.
        return value.StartsWith("<@&", StringComparison.Ordinal) && value.EndsWith('>') ? value : string.Empty;
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "Not supplied" : value.Trim()[..Math.Min(value.Trim().Length, 900)];
}
