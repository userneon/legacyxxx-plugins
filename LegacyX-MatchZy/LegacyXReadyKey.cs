using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// Ready with the R key (owner request 2026-10-16): while the match waits for players, a short line under the crosshair says
/// "Press R to ready"; a tap on R readies, another tap un-readies. Typing .ready still works. A tap is a press shorter than
/// <see cref="LegacyXReadyTapSeconds"/>, so holding R never toggles it.
/// </summary>
public partial class MatchZy
{
    private const float LegacyXReadyTapSeconds = 0.35f;
    private const int LegacyXReadyRedrawTicks = 24;

    private sealed class ReadyKeyState
    {
        public bool Down;
        public float PressedAt;
        public int LastDrawTick = -1000;
        public string LastDraw = "";
    }

    private readonly Dictionary<int, ReadyKeyState> legacyXReadyKeys = new();

    // Private chat nudge (owner request 2026-10-22): each player who has not readied hears it alone, every so often.
    private const float LegacyXReadyNudgeSeconds = 20f;
    private readonly Dictionary<int, float> legacyXReadyNudged = new();

    private void OnLegacyXReadyKeyTick()
    {
        if (!readyAvailable || matchStarted)
        {
            if (legacyXReadyKeys.Count > 0) legacyXReadyKeys.Clear();
            if (legacyXReadyNudged.Count > 0) legacyXReadyNudged.Clear();
            return;
        }

        var humans = Utilities.GetPlayers().Where(player => IsPlayerValid(player) && !player.IsBot && !player.IsHLTV && player.UserId.HasValue).ToList();
        var seated = humans.Where(player => player.TeamNum >= (byte)CsTeam.Terrorist).ToList();
        var readyCount = seated.Count(player => playerReadyStatus.TryGetValue(player.UserId!.Value, out var ready) && ready);
        var needed = minimumReadyRequired > 0 ? minimumReadyRequired : seated.Count;

        foreach (var player in seated)
        {
            var userId = player.UserId!.Value;
            if (!legacyXReadyKeys.TryGetValue(userId, out var state)) legacyXReadyKeys[userId] = state = new ReadyKeyState();

            var down = (player.Buttons & PlayerButtons.Reload) != 0;
            if (down && !state.Down)
            {
                state.Down = true;
                state.PressedAt = Server.CurrentTime;
            }
            else if (!down && state.Down)
            {
                state.Down = false;
                if (Server.CurrentTime - state.PressedAt <= LegacyXReadyTapSeconds)
                {
                    var isReady = playerReadyStatus.TryGetValue(userId, out var current) && current;
                    if (isReady) OnPlayerUnReady(player, null);
                    else OnPlayerReady(player, null);
                    state.LastDraw = "";
                    // Counted again below so the line shows the new number at once.
                    readyCount = seated.Count(other => playerReadyStatus.TryGetValue(other.UserId!.Value, out var r) && r);
                }
            }

            var mine = playerReadyStatus.TryGetValue(userId, out var mineReady) && mineReady;
            var html = mine
                ? $"<font class='fontSize-m' color='#22c55e'>You are ready</font><br><font class='fontSize-s' color='#a3a3a3'>Press <font color='#a78bfa'>R</font> to unready · {readyCount}/{needed} ready</font>"
                : $"<font class='fontSize-m' color='#fafafa'>Press <font color='#a78bfa'>R</font> to ready</font><br><font class='fontSize-s' color='#a3a3a3'>{readyCount}/{needed} ready</font>";
            if (html == state.LastDraw && Server.TickCount - state.LastDrawTick < LegacyXReadyRedrawTicks) continue;
            state.LastDraw = html;
            state.LastDrawTick = Server.TickCount;
            player.PrintToCenterHtml(html);
        }

        foreach (var player in humans)
        {
            var userId = player.UserId!.Value;
            var isSeated = player.TeamNum >= (byte)CsTeam.Terrorist;
            if (isSeated && playerReadyStatus.TryGetValue(userId, out var nudgeReady) && nudgeReady) continue;
            if (legacyXReadyNudged.TryGetValue(userId, out var last) && Server.CurrentTime - last < LegacyXReadyNudgeSeconds) continue;
            legacyXReadyNudged[userId] = Server.CurrentTime;
            PrintToPlayerChat(player, isSeated
                ? $"You are not ready. Press {ChatColors.White}R{ChatColors.Grey} or type {ChatColors.White}.r{ChatColors.Grey} to ready."
                : $"Type {ChatColors.White}.r{ChatColors.Grey} to ready, then choose your team.");
        }

        foreach (var stale in legacyXReadyNudged.Keys.Where(id => humans.All(player => player.UserId != id)).ToList()) legacyXReadyNudged.Remove(stale);
        foreach (var stale in legacyXReadyKeys.Keys.Where(id => seated.All(player => player.UserId != id)).ToList()) legacyXReadyKeys.Remove(stale);
    }
}
