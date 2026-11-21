using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace LegacyXCommunity;

/// <summary>
/// The seller's re-rigged character models (agents/models/bluesen/default_agents/&lt;name&gt;/&lt;name&gt;.vmdl, owner request 2026-11-15): the Apex knives' animations play only on them. Only a player
/// who WEARS an Apex knife is given one of the pack's COMPLETE models (the same agent when the pack has it complete, else a random one of their team), at spawn, never at equip. on by default; LEGACYX_COMMUNITY_REPLACE_AGENTS=false switches it off. The pack's models are found by
/// reading the file list of the downloaded Workshop addon (the VPK's directory file); LEGACYX_COMMUNITY_APEX_WORKSHOP_ID is that addon (default 3830785234).
/// </summary>
public sealed partial class LegacyXCommunity
{
    private bool replaceAgentsOn = true;   // on by default (owner 2026-11-19: the agent must be given by itself); LEGACYX_COMMUNITY_REPLACE_AGENTS=false switches it off. Only the pack's COMPLETE models are ever given
    private Dictionary<string, string>? agentPack;            // default agent name -> the pack's model path
    private DateTime agentPackSearchedAt = DateTime.MinValue;
    private string agentWorkshopId = "3830785234";
    private readonly Dictionary<ulong, DateTime> agentSetAt = new();
    private readonly HashSet<string> agentMissLogged = new(StringComparer.Ordinal);
    private readonly HashSet<string> agentPrecached = new(StringComparer.OrdinalIgnoreCase);

    private void RegisterAgentReplacement()
    {
        var env = LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load();
        replaceAgentsOn = !string.Equals(env.Get("LEGACYX_COMMUNITY_REPLACE_AGENTS").Trim(), "false", StringComparison.OrdinalIgnoreCase);
        var id = env.Get("LEGACYX_COMMUNITY_APEX_WORKSHOP_ID").Trim();
        if (id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit)) agentWorkshopId = id;
        // Without a restart: `lx_agents 0` stops giving the pack's models from the next spawn on (the console of the server only), `lx_agents 1` starts again.
        AddCommand("lx_agents", "Agents: the pack's re-rigged character models (lx_agents 0/1)", (caller, command) =>
        {
            if (caller != null) return;
            if (command.ArgCount >= 2) replaceAgentsOn = command.GetArg(1) != "0";
            Console.WriteLine($"[{ModuleName}] Agents: the pack's character models are {(replaceAgentsOn ? "ON" : "off")}.");
        });
        Console.WriteLine($"[{ModuleName}] Agents: the pack's re-rigged character models are {(replaceAgentsOn ? "ON" : "off")} (on by default; LEGACYX_COMMUNITY_REPLACE_AGENTS=false switches them off).");
    }

    // The pack models that are complete (tools/check_model_deps.py: the other 47 miss thirdperson_body_vmorf.vtex_c, 2026-11-15). Only these are given to players.
    private static readonly HashSet<string> CompleteAgents = new(StringComparer.Ordinal)
    {
        "ctm_fbi_varianta", "ctm_fbi_variantc", "ctm_fbi_variantg", "ctm_gendarmerie_variantb", "ctm_sas", "ctm_sas_variantf", "ctm_sas_variantg",
        "ctm_st6_variante", "ctm_st6_variantj", "ctm_st6_variantk", "ctm_swat_variantg", "ctm_swat_varianth", "ctm_swat_varianti", "ctm_swat_variantj",
        "tm_balkan_variantf", "tm_balkan_varianth", "tm_balkan_variantl", "tm_jungle_raider_varianta", "tm_jungle_raider_variantd",
        "tm_leet_varianta", "tm_leet_variantb", "tm_leet_variantc", "tm_leet_variantd", "tm_leet_variante", "tm_leet_variantg", "tm_leet_varianth",
        "tm_leet_varianti", "tm_leet_variantj", "tm_leet_variantk", "tm_professional_varg", "tm_professional_varh", "tm_professional_vari", "tm_professional_varj",
    };

    private void ReplaceAgentLater(CCSPlayerController player)
    {
        if (!replaceAgentsOn || !weaponModelsOn || player.IsBot) return;
        // Only a player who wears an Apex knife needs the pack's character model (its animations play only on it): everybody else keeps their own.
        if (!WearsApexKnife(player.SteamID))
        {
            // Said once per player, so "the env is on but nothing happens" shows why in the server log: the knife is not an Apex one, or the weapon models have not loaded for the player yet.
            if (agentMissLogged.Add("knife:" + player.SteamID)) Console.WriteLine($"[{ModuleName}] Agents: {player.PlayerName} is not given the pack's agent: no Apex knife worn (apex subclass {(apexSubclassOn ? "on" : "OFF")}, weapon models loaded: {weaponModels.ContainsKey(player.SteamID)}).");
            return;
        }
        // More looks later: the Skinchanger agent (WeaponPaints) is put on a moment after the spawn, and the player's profile (their worn knife) can arrive after the spawn: LoadScoreboardRankAsync calls this again when it does.
        foreach (var delay in new[] { 0.7f, 2.0f, 4.5f, 8.0f })
            AddTimer(delay, () => ReplaceAgent(player));
    }

    private bool WearsApexKnife(ulong steamId) =>
        apexSubclassOn && weaponModels.TryGetValue(steamId, out var set) && set.TryGetValue("knife", out var knife) && KnifeSubclass(knife.World) is not null;

    private void ReplaceAgent(CCSPlayerController player)
    {
        try
        {
            if (!player.IsValid || !player.PawnIsAlive || player.TeamNum is not (2 or 3) || !WearsApexKnife(player.SteamID)) return;
            var pawn = player.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid) return;
            if (agentSetAt.TryGetValue(player.SteamID, out var last) && (DateTime.UtcNow - last).TotalSeconds < 1.0) return;
            var pack = AgentPack();
            if (pack is null) return;
            var current = CurrentModel(pawn)?.ToLowerInvariant();
            // Already one of the pack's models: nothing to do.
            if (current is not null && pack.Values.Any(path => string.Equals(path, current, StringComparison.OrdinalIgnoreCase))) return;
            // The player's own agent (characters/models/<name>/<name>.vmdl): the pack's model of the same name when it is complete, else a random complete one of their team.
            var name = current is not null && current.StartsWith("characters/models/", StringComparison.Ordinal) ? Path.GetFileNameWithoutExtension(current) : "";
            string? target = null;
            if (name.Length > 0)
            {
                var baseName = System.Text.RegularExpressions.Regex.Replace(name, "_variant.*$", "");
                target = new[] { name, baseName }.Where(n => CompleteAgents.Contains(n) && pack.ContainsKey(n)).Select(n => pack[n]).FirstOrDefault();
            }
            if (target is null)
            {
                var prefix = player.TeamNum == 3 ? "ctm_" : "tm_";
                var options = CompleteAgents.Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && !(prefix == "tm_" && n.StartsWith("ctm_", StringComparison.Ordinal)) && pack.ContainsKey(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (options.Count == 0)
                {
                    if (agentMissLogged.Add("none:" + prefix)) Console.WriteLine($"[{ModuleName}] Agents: the pack has no complete model for team {player.TeamNum} (pack: {string.Join(", ", pack.Keys.Take(40))}).");
                    return;
                }
                target = pack[options[Random.Shared.Next(options.Count)]];   // random among the team's complete models, a new pick at every spawn
            }
            agentSetAt[player.SteamID] = DateTime.UtcNow;
            if (agentPrecached.Add(target)) TryPrecache(target);
            Timed($"agent SetModel {target}", () => pawn.SetModel(target));
            Console.WriteLine($"[{ModuleName}] {player.PlayerName} wears an Apex knife: agent {(name.Length > 0 ? name : current ?? "?")} is now the pack's {target}.");
            RefreshGlovesAfterAgent(player);
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Agent replacement failed: {problem.Message}"); }
    }

    /// <summary>
    /// The gloves are put on by WeaponPaints at the spawn (next frame, then 0.08 s later); this plugin changes the player's model 0.7 s and 2 s after the spawn, and the gloves
    /// are not given again (owner 2026-11-20: with lx_agents 0 the gloves are there, with the pack's agent they are gone). The same refresh WeaponPaints uses when it changes a glove
    /// ("lastinv" twice, 0.08 s apart, and the first_or_third_person body group off and on) is done once after the model change.
    /// </summary>
    private void RefreshGlovesAfterAgent(CCSPlayerController player)
    {
        AddTimer(0.1f, () =>
        {
            try
            {
                var pawn = player.PlayerPawn.Value;
                if (!player.IsValid || !player.PawnIsAlive || pawn is null || !pawn.IsValid) return;
                player.ExecuteClientCommand("lastinv");
                AddTimer(0.08f, () =>
                {
                    try
                    {
                        var current = player.PlayerPawn.Value;
                        if (!player.IsValid || !player.PawnIsAlive || current is null || !current.IsValid) return;
                        player.ExecuteClientCommand("lastinv");
                        current.AcceptInput("SetBodygroup", value: "first_or_third_person,0");
                        AddTimer(0.2f, () => { if (player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value is { IsValid: true } again) again.AcceptInput("SetBodygroup", value: "first_or_third_person,1"); });
                        Console.WriteLine($"[{ModuleName}] Gloves refreshed for {player.PlayerName} after the agent model change.");
                    }
                    catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Glove refresh (second step) failed: {problem.Message}"); }
                });
            }
            catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Glove refresh failed: {problem.Message}"); }
        });
    }

    /// <summary>The pack's models by default agent name, read from the addon's file list (at most once a minute until found).</summary>
    private Dictionary<string, string>? AgentPack()
    {
        if (agentPack is not null) return agentPack;
        if ((DateTime.UtcNow - agentPackSearchedAt).TotalSeconds < 60) return null;
        agentPackSearchedAt = DateTime.UtcNow;
        try
        {
            // Server.GameDirectory is /home/cs2/cs2/game on this server (journalctl 2026-11-18), and the Workshop item sits at game/bin/linuxsteamrt64/..., so the folder next to
            // the game directory ("..") was never right here. Both places are tried, the game directory's own bin first.
            var relative = new[] { "bin", "linuxsteamrt64", "steamapps", "workshop", "content", "730", agentWorkshopId, $"{agentWorkshopId}_dir.vpk" };
            var candidates = new[] { Server.GameDirectory, Path.Combine(Server.GameDirectory, ".."), Path.Combine(Server.GameDirectory, "..", "..") }
                .Select(root => Path.GetFullPath(Path.Combine(new[] { root }.Concat(relative).ToArray()))).ToList();
            var vpk = candidates.FirstOrDefault(File.Exists);
            if (vpk is null) { Console.WriteLine($"[{ModuleName}] Agents: the Workshop addon's {agentWorkshopId}_dir.vpk is not there yet, looked in: {string.Join(" | ", candidates)}."); return null; }
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
        try { Timed($"PrecacheModel {path}", () => Server.PrecacheModel(path)); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
    }

    /// <summary>Every character model (.vmdl) under a folder of a VPK v1/v2 directory file (the variants sit in the agent's folder: ctm_fbi/ctm_fbi_varianta.vmdl).</summary>
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
                    if (extension == "vmdl_c" && directory.StartsWith(folder, StringComparison.Ordinal))
                        found.Add($"{directory}/{name}.vmdl");
                }
            }
        }
        return found;
    }
}
