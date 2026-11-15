using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace LegacyXCommunity;

/// <summary>
/// The seller's re-rigged character models (agents/models/bluesen/default_agents/&lt;name&gt;/&lt;name&gt;.vmdl, owner request 2026-11-15): the Apex knives' animations play only on them. Every
/// player who has one of the game's default agents (characters/models/&lt;name&gt;/&lt;name&gt;.vmdl) is given the pack's model of the SAME name, so they look the same and only the skeleton changes. A player
/// with another agent (a Skinchanger agent) keeps it. Done once at spawn, never at equip. LEGACYX_COMMUNITY_REPLACE_AGENTS=true switches it on (off by default). The pack's models are found by
/// reading the file list of the downloaded Workshop addon (the VPK's directory file); LEGACYX_COMMUNITY_APEX_WORKSHOP_ID is that addon (default 3830785234).
/// </summary>
public sealed partial class LegacyXCommunity
{
    private bool replaceAgentsOn;   // off unless LEGACYX_COMMUNITY_REPLACE_AGENTS=true: the pack's agents are not in the published addon any more (2026-11-15, players' games crashed while loading it)
    private Dictionary<string, string>? agentPack;            // default agent name -> the pack's model path
    private DateTime agentPackSearchedAt = DateTime.MinValue;
    private string agentWorkshopId = "3830785234";
    private readonly Dictionary<ulong, DateTime> agentSetAt = new();
    private readonly HashSet<string> agentMissLogged = new(StringComparer.Ordinal);
    private readonly HashSet<string> agentPrecached = new(StringComparer.OrdinalIgnoreCase);

    private void RegisterAgentReplacement()
    {
        var env = LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load();
        replaceAgentsOn = string.Equals(env.Get("LEGACYX_COMMUNITY_REPLACE_AGENTS"), "true", StringComparison.OrdinalIgnoreCase);
        var id = env.Get("LEGACYX_COMMUNITY_APEX_WORKSHOP_ID").Trim();
        if (id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit)) agentWorkshopId = id;
        // Without a restart: `lx_agents 0` stops giving the pack's models from the next spawn on (the console of the server only), `lx_agents 1` starts again.
        AddCommand("lx_agents", "Agents: the pack's re-rigged character models (lx_agents 0/1)", (caller, command) =>
        {
            if (caller != null) return;
            if (command.ArgCount >= 2) replaceAgentsOn = command.GetArg(1) != "0";
            Console.WriteLine($"[{ModuleName}] Agents: the pack's character models are {(replaceAgentsOn ? "ON" : "off")}.");
        });
        Console.WriteLine($"[{ModuleName}] Agents: the pack's re-rigged character models are {(replaceAgentsOn ? "ON" : "off")} (off by default; LEGACYX_COMMUNITY_REPLACE_AGENTS=true switches them on).");
    }

    private void ReplaceAgentLater(CCSPlayerController player)
    {
        if (!replaceAgentsOn || !weaponModelsOn || player.IsBot) return;
        // A second look later: the Skinchanger agent (WeaponPaints) is put on a moment after the spawn, and a player who has one must keep it.
        foreach (var delay in new[] { 0.7f, 2.0f })
            AddTimer(delay, () => ReplaceAgent(player));
    }

    private void ReplaceAgent(CCSPlayerController player)
    {
        try
        {
            if (!player.IsValid || !player.PawnIsAlive || player.TeamNum is not (2 or 3)) return;
            var pawn = player.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid) return;
            if (agentSetAt.TryGetValue(player.SteamID, out var last) && (DateTime.UtcNow - last).TotalSeconds < 1.0) return;
            var current = CurrentModel(pawn)?.ToLowerInvariant();
            const string defaultFolder = "characters/models/";
            if (current is null || !current.StartsWith(defaultFolder, StringComparison.Ordinal) || !current.EndsWith(".vmdl", StringComparison.Ordinal))
            {
                // not a default agent (a Skinchanger agent, or the model could not be read): left alone
                if (agentMissLogged.Add("other:" + (current ?? "unreadable"))) Console.WriteLine($"[{ModuleName}] Agents: {player.PlayerName}'s model is {current ?? "(not readable)"}, not one of the game's default agents: left alone.");
                return;
            }
            var name = Path.GetFileNameWithoutExtension(current);
            var pack = AgentPack();
            if (pack is null) return;
            // The game's agents have variants (ctm_fbi_variantb): the pack's model of the base name (ctm_fbi) is the one that fits; else a pack name that starts like it.
            var baseName = System.Text.RegularExpressions.Regex.Replace(name, "_variant.*$", "");
            if (!pack.TryGetValue(name, out var target) && !pack.TryGetValue(baseName, out target))
                target = pack.Where(entry => entry.Key.StartsWith(baseName, StringComparison.Ordinal) || baseName.StartsWith(entry.Key, StringComparison.Ordinal)).Select(entry => entry.Value).FirstOrDefault();
            if (target is null)
            {
                if (agentMissLogged.Add(name)) Console.WriteLine($"[{ModuleName}] Agents: {player.PlayerName} has {current}, and the pack has no model for \"{name}\" (pack: {string.Join(", ", pack.Keys.Take(40))}).");
                return;
            }
            agentSetAt[player.SteamID] = DateTime.UtcNow;
            if (agentPrecached.Add(target)) TryPrecache(target);
            pawn.SetModel(target);
            Console.WriteLine($"[{ModuleName}] Agent {name} of {player.PlayerName} is now the pack's {target}.");
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Agent replacement failed: {problem.Message}"); }
    }

    /// <summary>The pack's models by default agent name, read from the addon's file list (at most once a minute until found).</summary>
    private Dictionary<string, string>? AgentPack()
    {
        if (agentPack is not null) return agentPack;
        if ((DateTime.UtcNow - agentPackSearchedAt).TotalSeconds < 60) return null;
        agentPackSearchedAt = DateTime.UtcNow;
        try
        {
            var vpk = Path.GetFullPath(Path.Combine(Server.GameDirectory, "..", "bin", "linuxsteamrt64", "steamapps", "workshop", "content", "730", agentWorkshopId, $"{agentWorkshopId}_dir.vpk"));
            if (!File.Exists(vpk)) { Console.WriteLine($"[{ModuleName}] Agents: {vpk} is not there yet (the Workshop addon is not downloaded)."); return null; }
            var pack = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in ReadVpkModels(vpk, "agents/models/bluesen/")) pack.TryAdd(Path.GetFileNameWithoutExtension(path), path);
            Console.WriteLine($"[{ModuleName}] Agents: {pack.Count} re-rigged character model(s) in the addon: {string.Join(", ", pack.Keys.Take(30))}.");
            agentPack = pack;
            return pack;
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Agents: could not read the addon's file list: {problem.Message}"); return null; }
    }

    private void TryPrecache(string path)
    {
        try { Server.PrecacheModel(path); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
    }

    /// <summary>The character models (.vmdl, folder named like the file) under a folder of a VPK v1/v2 directory file.</summary>
    private static List<string> ReadVpkModels(string file, string folder)
    {
        var found = new List<string>();
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x55AA1234) throw new InvalidDataException("not a VPK directory file");
        var version = reader.ReadUInt32();
        var treeSize = reader.ReadUInt32();
        stream.Position = version == 2 ? 28 : 12;
        var end = stream.Position + treeSize;
        string Read()
        {
            var bytes = new List<byte>();
            int value;
            while ((value = stream.ReadByte()) > 0) bytes.Add((byte)value);
            if (value < 0) throw new EndOfStreamException();
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }
        while (stream.Position < end)
        {
            var extension = Read();
            if (extension.Length == 0) break;
            while (true)
            {
                var directory = Read();
                if (directory.Length == 0) break;
                while (true)
                {
                    var name = Read();
                    if (name.Length == 0) break;
                    reader.ReadUInt32();                        // crc
                    var preload = reader.ReadUInt16();
                    stream.Position += 2 + 4 + 4 + 2 + preload; // archive, offset, length, terminator, preload bytes
                    if (extension == "vmdl_c" && directory.StartsWith(folder, StringComparison.Ordinal) && directory.EndsWith("/" + name, StringComparison.Ordinal))
                        found.Add($"{directory}/{name}.vmdl");
                }
            }
        }
        return found;
    }
}
