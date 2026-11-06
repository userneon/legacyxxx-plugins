using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AdminPlus;

/// <summary>
/// The retake spawns of a map (owner request 2026-11-06: "make the retake good"): instead of guessing where the Terrorists and Counter-Terrorists should stand,
/// the Owner walks the map once and marks each spot where they are standing. A spawn is a team (T, CT or BOMB) on a site (A or B) with a position and the way it
/// faces. They are kept per map in addons/counterstrikesharp/configs/retake/&lt;map&gt;.json and !retake uses them; a map without them keeps the computed fallback.
///   css_retakespawn &lt;T|CT|BOMB&gt; &lt;A|B&gt;   add one where you stand, facing the way you look (BOMB: where the bomb is planted)
///   css_retakespawnremove               remove the nearest one (within 250 units)
///   css_retakespawns                    count them and show each for a few seconds (T orange, CT blue, BOMB red)
/// All @css/root, in chat (!retakespawn T A) or in the console.
/// </summary>
public partial class AdminPlus
{
    private sealed class RetakeSpawn
    {
        public string Site { get; set; } = "A";
        public string Team { get; set; } = "T";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
    }

    private sealed class RetakeSpawnFile { public List<RetakeSpawn> Spawns { get; set; } = new(); }

    private static readonly JsonSerializerOptions RetakeJson = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private static string RetakeSpawnPath(string map)
    {
        var safe = new string((map ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-').ToArray());
        return Path.Combine(Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "retake", $"{safe}.json");
    }

    private List<RetakeSpawn> LoadRetakeSpawns()
    {
        try
        {
            var path = RetakeSpawnPath(Server.MapName);
            if (!File.Exists(path)) return new List<RetakeSpawn>();
            var file = JsonSerializer.Deserialize<RetakeSpawnFile>(File.ReadAllText(path), RetakeJson);
            return (file?.Spawns ?? new()).Where(s => s.Site is "A" or "B" && s.Team is "T" or "CT" or "BOMB").ToList();
        }
        catch (Exception exception) { LogError($"retake spawns: {exception.Message}"); return new List<RetakeSpawn>(); }
    }

    private bool SaveRetakeSpawns(List<RetakeSpawn> spawns)
    {
        try
        {
            var path = RetakeSpawnPath(Server.MapName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new RetakeSpawnFile { Spawns = spawns }, RetakeJson));
            return true;
        }
        catch (Exception exception) { LogError($"retake spawns could not be saved: {exception.Message}"); return false; }
    }

    private void RegisterRetakeSpawnCommands()
    {
        AddPermCmd("@css/root", "css_retakespawn", "<T|CT|BOMB> <A|B> - Mark a retake spawn where you stand", CmdRetakeSpawn);
        AddPermCmd("@css/root", "css_retakespawnremove", "- Remove the nearest retake spawn", CmdRetakeSpawnRemove);
        AddPermCmd("@css/root", "css_retakespawns", "- Count the retake spawns of this map and show them", CmdRetakeSpawns);
    }

    private static string RetakeSummary(List<RetakeSpawn> spawns) =>
        string.Join(" · ", new[] { "A", "B" }.Select(site => $"{site}: {spawns.Count(s => s.Site == site && s.Team == "T")} T, {spawns.Count(s => s.Site == site && s.Team == "CT")} CT, {spawns.Count(s => s.Site == site && s.Team == "BOMB")} bomb"));

    private void CmdRetakeSpawn(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid || !Alive(caller)) { FunReply(caller, "Stand on the map and type it in chat: !retakespawn <T|CT|BOMB> <A|B>"); return; }
        var team = info.ArgCount > 1 ? info.GetArg(1).ToUpperInvariant() : "";
        var site = info.ArgCount > 2 ? info.GetArg(2).ToUpperInvariant() : "";
        if (team is not ("T" or "CT" or "BOMB") || site is not ("A" or "B")) { FunReply(caller, "Usage: !retakespawn <T|CT|BOMB> <A|B>   (stand where it should be, look the way it should face)"); return; }
        var pawn = caller.PlayerPawn!.Value!;
        if (pawn.AbsOrigin is not { } at) return;
        var spawns = LoadRetakeSpawns();
        // The bomb has one place per site: a new one replaces the old.
        if (team == "BOMB") spawns.RemoveAll(s => s.Team == "BOMB" && s.Site == site);
        spawns.Add(new RetakeSpawn { Site = site, Team = team, X = at.X, Y = at.Y, Z = at.Z, Yaw = pawn.EyeAngles.Y });
        if (!SaveRetakeSpawns(spawns)) { FunReply(caller, "The spawn could not be saved (see the server log)."); return; }
        FunReply(caller, $"Saved {team} on {site}. {Server.MapName}: {RetakeSummary(spawns)}");
        SpawnFx(FxBurst, new Vector(at.X, at.Y, at.Z + 8f), team == "T" ? Color.Orange : team == "CT" ? Color.DeepSkyBlue : Color.Red, 3f);
    }

    private void CmdRetakeSpawnRemove(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid || !Alive(caller) || caller.PlayerPawn!.Value!.AbsOrigin is not { } at) { FunReply(caller, "Stand next to the spawn and type it in chat: !retakespawnremove"); return; }
        var spawns = LoadRetakeSpawns();
        var nearest = spawns.OrderBy(s => (s.X - at.X) * (s.X - at.X) + (s.Y - at.Y) * (s.Y - at.Y) + (s.Z - at.Z) * (s.Z - at.Z)).FirstOrDefault();
        if (nearest == null || MathF.Sqrt((nearest.X - at.X) * (nearest.X - at.X) + (nearest.Y - at.Y) * (nearest.Y - at.Y) + (nearest.Z - at.Z) * (nearest.Z - at.Z)) > 250f) { FunReply(caller, "No retake spawn within 250 units of you."); return; }
        spawns.Remove(nearest);
        FunReply(caller, SaveRetakeSpawns(spawns) ? $"Removed {nearest.Team} on {nearest.Site}. {Server.MapName}: {RetakeSummary(spawns)}" : "The change could not be saved (see the server log).");
    }

    private void CmdRetakeSpawns(CCSPlayerController? caller, CommandInfo info)
    {
        var spawns = LoadRetakeSpawns();
        FunReply(caller, $"{Server.MapName}: {(spawns.Count == 0 ? "no retake spawns yet" : RetakeSummary(spawns))}");
        foreach (var s in spawns)
            SpawnFx(FxBurst, new Vector(s.X, s.Y, s.Z + 8f), s.Team == "T" ? Color.Orange : s.Team == "CT" ? Color.DeepSkyBlue : Color.Red, 6f);
    }

    /// <summary>The positions of a team on a site, shuffled; empty when none were marked.</summary>
    private static List<RetakeSpawn> RetakeSpawnsFor(List<RetakeSpawn> all, string site, string team) =>
        all.Where(s => s.Site == site && s.Team == team).OrderBy(_ => FunRandom.Next()).ToList();

    private static void PlaceAt(CCSPlayerController player, RetakeSpawn spawn, float spread = 0f)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;
        var shift = spread <= 0f ? new Vector(0, 0, 0) : new Vector((float)(FunRandom.NextDouble() - 0.5) * spread, (float)(FunRandom.NextDouble() - 0.5) * spread, 0);
        pawn.Teleport(new Vector(spawn.X + shift.X, spawn.Y + shift.Y, spawn.Z + 4f), new QAngle(0, spawn.Yaw, 0), new Vector(0, 0, 0));
    }
}
