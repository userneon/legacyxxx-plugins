using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

/// <summary>
/// The Discord invite in everyone's chat, one short line every <see cref="LegacyXCommunityConfig.DiscordAdMinutes"/> minutes (owner request 2026-10-24),
/// only while a real player is on the server. LEGACYX_&lt;port&gt;_COMMUNITY_DISCORD_AD_MINUTES changes the gap for one server; 0 turns it off.
/// The invite is DISCORD_URL, the same one !discord gives.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private void RegisterDiscordAd()
    {
        if (Config.DiscordAdMinutes <= 0 || string.IsNullOrWhiteSpace(Config.DiscordUrl)) return;
        AddTimer(Config.DiscordAdMinutes * 60f, () =>
        {
            var line = LegacyXChat.System($"Join our Discord: {{white}}{Config.DiscordUrl.Replace("https://", "")}{{grey}}");
            foreach (var player in Utilities.GetPlayers().Where(player => player.IsValid && !player.IsBot && !player.IsHLTV)) player.PrintToChat(line);
        }, TimerFlags.REPEAT);
    }
}
