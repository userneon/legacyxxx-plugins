using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;

namespace LegacyXHud;

// A player who has never joined this server sees the menu's Welcome page, once, in the middle of the screen. After that the usual
// short welcome card is all they get. Who has been welcomed is kept in a small file next to the plugin (no database change); each
// server keeps its own list. The menu opens only in warmup and before a round (see MenuAllowed), so a player who joins in the
// middle of a live round is shown it at the next freeze time.
public sealed partial class LegacyXHud
{
    private bool firstJoinMenu = true;
    private readonly HashSet<ulong> welcomed = new();
    private readonly HashSet<int> welcomePending = new();
    private string welcomedPath = "";

    private void FirstJoinStart()
    {
        welcomedPath = Path.Combine(ModuleDirectory, "welcomed-players.json");
        try
        {
            if (File.Exists(welcomedPath))
                foreach (var id in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(welcomedPath)) ?? new())
                    if (ulong.TryParse(id, out var steamId)) welcomed.Add(steamId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not read {welcomedPath}: {ex.Message}");
        }
        AddTimer(2f, FirstJoinTick, TimerFlags.REPEAT);
    }

    private void MarkWelcomed(ulong steamId)
    {
        if (!welcomed.Add(steamId)) return;
        try
        {
            File.WriteAllText(welcomedPath, JsonSerializer.Serialize(welcomed.Select(id => id.ToString()).OrderBy(id => id).ToList()));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not save {welcomedPath}: {ex.Message}");
        }
    }

    /// <summary>True when this player has not been welcomed yet and the Welcome page will be shown to them instead of the card.</summary>
    private bool WaitForFirstWelcome(CCSPlayerController player)
    {
        if (!firstJoinMenu || !menuKeys || welcomed.Contains(player.SteamID)) return false;
        welcomePending.Add(player.Slot);
        return true;
    }

    private void FirstJoinTick()
    {
        if (welcomePending.Count == 0 || !MenuAllowed()) return;
        foreach (var slot in welcomePending.ToList())
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not { IsValid: true, IsBot: false })
            {
                welcomePending.Remove(slot);
                continue;
            }
            if (menuOpen.Contains(slot) || knifeVotes.ContainsKey(slot)) continue;
            welcomePending.Remove(slot);
            MarkWelcomed(player.SteamID);
            OpenMenu(player);
        }
    }
}
