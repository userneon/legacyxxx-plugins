using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace LegacyXCommunity;

/// <summary>
/// The re-rigged character models of the seller's Apex pack (agents/models/bluesen/..., owner request 2026-11-15: "make the model change by itself"): the Apex knives' animations
/// play only on them, so a player who wears an Apex knife is given the one of their team. LEGACYX_COMMUNITY_APEX_AGENT_CT / _T name the models; when they are empty the plugin finds
/// them itself in the file list of the Workshop addon the server has downloaded (the VPK's directory file), the first ctm_* and tm_* model whose folder has the same name as the file.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private string? apexAgentFoundCt, apexAgentFoundT;
    private DateTime apexAgentSearchedAt = DateTime.MinValue;
    private string apexWorkshopId = "3830785234";

    private string? ApexAgentFor(int team)
    {
        var set = team == 3 ? apexAgentCt : apexAgentT;
        if (set is not null) return set;
        if (apexAgentFoundCt is null || apexAgentFoundT is null) SearchApexAgents();
        return team == 3 ? apexAgentFoundCt : apexAgentFoundT;
    }

    /// <summary>Reads the VPK's file list at most once a minute (the addon may be downloaded after the plugin started).</summary>
    private void SearchApexAgents()
    {
        if ((DateTime.UtcNow - apexAgentSearchedAt).TotalSeconds < 60) return;
        apexAgentSearchedAt = DateTime.UtcNow;
        try
        {
            var env = LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_APEX_WORKSHOP_ID").Trim();
            if (env.Length is > 0 and <= 20 && env.All(char.IsAsciiDigit)) apexWorkshopId = env;
            var vpk = Path.GetFullPath(Path.Combine(Server.GameDirectory, "..", "bin", "linuxsteamrt64", "steamapps", "workshop", "content", "730", apexWorkshopId, $"{apexWorkshopId}_dir.vpk"));
            if (!File.Exists(vpk)) { Console.WriteLine($"[{ModuleName}] Apex agents: {vpk} is not there yet (the Workshop addon is not downloaded)."); return; }
            var models = ReadVpkModels(vpk, "agents/models/bluesen/");
            var ct = models.Where(path => Path.GetFileName(path).StartsWith("ctm_", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
            var t = models.Where(path => Path.GetFileName(path).StartsWith("tm_", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
            if (apexAgentFoundCt is null && ct is not null && ValidModelPath(ct)) { apexAgentFoundCt = ct; weaponModelPaths.Add(ct); TryPrecache(ct); }
            if (apexAgentFoundT is null && t is not null && ValidModelPath(t)) { apexAgentFoundT = t; weaponModelPaths.Add(t); TryPrecache(t); }
            Console.WriteLine($"[{ModuleName}] Apex agents found in the addon: CT={apexAgentFoundCt ?? "none"} T={apexAgentFoundT ?? "none"} ({models.Count} character model(s) in agents/models/bluesen).");
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Apex agents: could not read the addon's file list: {problem.Message}"); }
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
