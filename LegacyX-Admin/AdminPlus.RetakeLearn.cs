using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AdminPlus;

/// <summary>
/// Retake spawns the plugin finds for itself (owner request 2026-11-06, "can the plugin decide it?"): while the server plays ordinary rounds, every two seconds the
/// position of each living player near a bomb site is noted in a grid (96-unit cells). A place somebody really stood on is a place that is walkable, so nothing is ever
/// put inside a wall. Close to the site those places become the Terrorists' spots, a little further out (where a retake starts) the Counter-Terrorists', each facing the
/// site. Kept per map in configs/retake/&lt;map&gt;.learned.json and added to as the server is played. Spots the Owner marks by hand (css_retakespawn) win; a site with
/// too few learned places keeps the computed fallback. css_retakelearn shows how much has been learned.
/// </summary>
public partial class AdminPlus
{
    private sealed class LearnedCell { public string Site { get; set; } = "A"; public float X { get; set; } public float Y { get; set; } public float Z { get; set; } public int Count { get; set; } }
    private sealed class LearnedFile { public List<LearnedCell> Cells { get; set; } = new(); }

    private const float LearnCell = 96f;
    private const float LearnRadius = 1500f;
    private const float DefendersWithin = 450f;
    private const float AttackersFrom = 650f, AttackersTo = 1400f;
    private const int LearnMinCount = 2, LearnNeeded = 5, LearnCellsPerSite = 800;

    private readonly Dictionary<(string Site, int X, int Y, int Z), LearnedCell> learnedCells = new();
    private string learnedMap = "";
    private bool learnedDirty;
    private float learnedSavedAt;

    private string LearnedPath(string map) => RetakeSpawnPath(map).Replace(".json", ".learned.json");

    private void RegisterRetakeLearning()
    {
        AddCommand("css_retakelearn", "Retake: how many places have been learned on this map (Owner)", (caller, _) =>
        {
            if (caller != null && (!caller.IsValid || !CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(caller, "@css/root"))) return;
            EnsureLearnedLoaded();
            FunReply(caller, $"{Server.MapName}: learned places {string.Join(" · ", new[] { "A", "B" }.Select(l => $"{l}: {learnedCells.Values.Count(c => c.Site == l && c.Count >= LearnMinCount)}"))} (a site needs {LearnNeeded} close to it and {LearnNeeded} further out). Marked by hand: {RetakeSummary(LoadRetakeSpawns())}");
        });
        AddTimer(2f, SampleForRetake, TimerFlags.REPEAT);
        RegisterListener<Listeners.OnMapStart>(_ => { learnedCells.Clear(); learnedMap = ""; learnedDirty = false; });
    }

    private void EnsureLearnedLoaded()
    {
        var map = Server.MapName ?? "";
        if (learnedMap == map) return;
        if (learnedDirty) SaveLearned();
        learnedCells.Clear();
        learnedMap = map;
        try
        {
            var path = LearnedPath(map);
            if (File.Exists(path))
                foreach (var cell in JsonSerializer.Deserialize<LearnedFile>(File.ReadAllText(path), RetakeJson)?.Cells ?? new())
                    if (cell.Site is "A" or "B") learnedCells[(cell.Site, (int)MathF.Floor(cell.X / LearnCell), (int)MathF.Floor(cell.Y / LearnCell), (int)MathF.Floor(cell.Z / 64f))] = cell;
        }
        catch (Exception exception) { LogError($"learned retake places: {exception.Message}"); }
    }

    private void SaveLearned()
    {
        try
        {
            var path = LearnedPath(learnedMap);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new LearnedFile { Cells = learnedCells.Values.Where(c => c.Count >= 1).ToList() }, RetakeJson));
            learnedDirty = false;
            learnedSavedAt = Server.CurrentTime;
        }
        catch (Exception exception) { LogError($"learned retake places could not be saved: {exception.Message}"); }
    }

    private void SampleForRetake()
    {
        try
        {
            if (gameMode.Length > 0 || InWarmup() || FreezeTimeOn()) return;   // only ordinary live rounds teach it where people really stand
            var sites = FindBombSites();
            if (sites.Count == 0) return;
            EnsureLearnedLoaded();
            foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsHLTV && p.TeamNum is 2 or 3 && p.PawnIsAlive))
            {
                if (player.PlayerPawn?.Value?.AbsOrigin is not { } at) continue;
                // Only a player standing on the ground, not falling or jumping.
                if ((player.PlayerPawn.Value.Flags & 1) == 0) continue;
                for (var index = 0; index < Math.Min(2, sites.Count); index++)
                {
                    var centre = sites[index];
                    var dx = at.X - centre.X; var dy = at.Y - centre.Y; var dz = at.Z - centre.Z;
                    if (dx * dx + dy * dy > LearnRadius * LearnRadius || MathF.Abs(dz) > 220f) continue;
                    var site = index == 0 ? "A" : "B";
                    var key = (site, (int)MathF.Floor(at.X / LearnCell), (int)MathF.Floor(at.Y / LearnCell), (int)MathF.Floor(at.Z / 64f));
                    if (learnedCells.TryGetValue(key, out var cell)) { cell.Count++; cell.X = at.X; cell.Y = at.Y; cell.Z = at.Z; }
                    else if (learnedCells.Count(kv => kv.Key.Site == site) < LearnCellsPerSite) learnedCells[key] = new LearnedCell { Site = site, X = at.X, Y = at.Y, Z = at.Z, Count = 1 };
                    learnedDirty = true;
                }
            }
            if (learnedDirty && Server.CurrentTime - learnedSavedAt > 60f) SaveLearned();
        }
        catch (Exception exception) { LogError($"retake learning: {exception.Message}"); }
    }

    /// <summary>The Terrorists' and Counter-Terrorists' spots the plugin has learned for a site, each facing the site; empty lists when too few are known.</summary>
    private (List<RetakeSpawn> T, List<RetakeSpawn> CT) LearnedSpots(string site, Vector centre)
    {
        EnsureLearnedLoaded();
        RetakeSpawn Make(LearnedCell c, string team) => new() { Site = site, Team = team, X = c.X, Y = c.Y, Z = c.Z, Yaw = MathF.Atan2(centre.Y - c.Y, centre.X - c.X) * 180f / MathF.PI };
        // Places somebody stood on more than once, spread apart so nobody is placed on top of another.
        static List<LearnedCell> Spread(IEnumerable<LearnedCell> cells, int want)
        {
            var chosen = new List<LearnedCell>();
            foreach (var cell in cells.OrderBy(_ => FunRandom.Next()))
            {
                if (chosen.All(o => (o.X - cell.X) * (o.X - cell.X) + (o.Y - cell.Y) * (o.Y - cell.Y) > 70f * 70f)) chosen.Add(cell);
                if (chosen.Count >= want) break;
            }
            return chosen;
        }
        var near = learnedCells.Values.Where(c => c.Site == site && c.Count >= LearnMinCount && Dist(c, centre) <= DefendersWithin);
        var far = learnedCells.Values.Where(c => c.Site == site && c.Count >= LearnMinCount && Dist(c, centre) is >= AttackersFrom and <= AttackersTo);
        var tCells = Spread(near, 12); var ctCells = Spread(far, 12);
        if (tCells.Count < LearnNeeded || ctCells.Count < LearnNeeded) return (new(), new());
        return (tCells.Select(c => Make(c, "T")).ToList(), ctCells.Select(c => Make(c, "CT")).ToList());
    }

    private static float Dist(LearnedCell c, Vector v) => MathF.Sqrt((c.X - v.X) * (c.X - v.X) + (c.Y - v.Y) * (c.Y - v.Y));
}
