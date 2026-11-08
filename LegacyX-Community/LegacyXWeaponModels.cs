using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace LegacyXCommunity;

/// <summary>
/// Shop weapon models (owner request 2026-11-06): a player's worn knife, Desert Eagle and AK-47 models (Shop → Weapons) come with the
/// profile as <c>weapon_models</c> (per weapon: world path, view path, <c>anim_base</c>). They are put on the player's weapon when it
/// spawns, is picked up or the player respawns. UNTESTED in game: try it on a test server first (lx_wm_status, lx_wm_off).
/// </summary>
public sealed partial class LegacyXCommunity
{
    private sealed record WeaponModel(string World, string View, string? AnimBase);
    // SteamID64 -> weapon (knife / deagle / ak47) -> model; players wearing none are not in it.
    private readonly Dictionary<ulong, Dictionary<string, WeaponModel>> weaponModels = new();
    // Every model path seen so far: precached again on each map so the server can show it.
    private readonly HashSet<string> weaponModelPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool weaponModelsOn = true;
    // Swapping a knife for the animation knife removes a weapon entity, which crashed a server once (WriteEnterPVS, 2026-11-06). Off unless LEGACYX_COMMUNITY_KNIFE_SWAP=true.
    private bool knifeSwapOn;
    // The real knife whose animations a custom knife is made for: a knife model plays the animations of the knife entity it sits on, so the player is given that knife.
    private static readonly Dictionary<string, string> AnimKnives = new() { ["m9"] = "weapon_knife_m9_bayonet", ["bayonet"] = "weapon_bayonet" };
    // When each player's knife was last swapped for the animation knife: at most once every few seconds, so another plugin giving the knife back cannot start a loop.
    private readonly Dictionary<ulong, DateTime> knifeSwappedAt = new();
    // Agent models (owner request 2026-11-08): the player model the player wears for the CT side and for the T side (Shop → Weapons → CT agents / T agents).
    private readonly Dictionary<ulong, (string? Ct, string? T)> agentModels = new();
    // The pawn and model an agent was last put on, so a weapon pickup does not set the same model again.
    private readonly Dictionary<ulong, (uint PawnIndex, string Path)> agentApplied = new();

    private void RegisterWeaponModels()
    {
        knifeSwapOn = string.Equals(LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_KNIFE_SWAP"), "true", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[{ModuleName}] Weapon models: knife swap for the M9/Bayonet animations is {(knifeSwapOn ? "ON" : "off")}.");
        foreach (var path in LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_MODEL_PRECACHE").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (ValidModelPath(path)) weaponModelPaths.Add(path);
        RegisterListener<Listeners.OnServerPrecacheResources>(manifest =>
        {
            foreach (var path in weaponModelPaths) manifest.AddResource(path);
            Console.WriteLine($"[{ModuleName}] Weapon models: {weaponModelPaths.Count} path(s) added to the precache manifest.");
        });
        // A second chance once the map is running: a model the manifest missed is loaded now (clients that join later still get it in their list).
        RegisterListener<Listeners.OnMapStart>(_ => Server.NextFrame(() =>
        {
            foreach (var path in weaponModelPaths)
                try { Server.PrecacheModel(path); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
        }));
        // A player types !wm to see what the server has for them (the console commands below are for the server's own console only).
        AddCommand("css_wm", "Weapon models: what is worn", (player, _) =>
        {
            if (player is null || !player.IsValid) return;
            if (agentModels.TryGetValue(player.SteamID, out var agents))
                player.PrintToChat(LegacyX.Shared.Configuration.LegacyXChat.System($"{{white}}agent{{grey}}: CT {agents.Ct ?? "default"}, T {agents.T ?? "default"}"));
            if (!weaponModels.TryGetValue(player.SteamID, out var worn)) { player.PrintToChat(LegacyX.Shared.Configuration.LegacyXChat.System($"{{grey}}No weapon model is worn (or the server has not read your profile yet). {weaponModelPaths.Count} model path(s) known.")); return; }
            foreach (var (weapon, model) in worn) player.PrintToChat(LegacyX.Shared.Configuration.LegacyXChat.System($"{{white}}{weapon}{{grey}}: {model.World}{(model.AnimBase is null ? "" : $" ({model.AnimBase} animations)")}"));
        });
        RegisterEventHandler<EventPlayerSpawn>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true } player) Server.NextFrame(() => ApplyWeaponModels(player));
            return HookResult.Continue;
        });
        RegisterEventHandler<EventItemPickup>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true } player) Server.NextFrame(() => ApplyWeaponModels(player));
            return HookResult.Continue;
        });
        AddCommand("lx_wm_off", "Weapon models: switch off or on (0/1)", (player, command) =>
        {
            if (player != null) return;
            weaponModelsOn = command.ArgCount < 2 || command.GetArg(1) != "0";
            Console.WriteLine($"[{ModuleName}] Weapon models {(weaponModelsOn ? "on" : "off")}.");
        });
        AddCommand("lx_wm_status", "Weapon models: who wears what", (player, _) =>
        {
            if (player != null) return;
            Console.WriteLine($"[{ModuleName}] Weapon models {(weaponModelsOn ? "on" : "off")}: {weaponModels.Count} players, {weaponModelPaths.Count} paths precached.");
            foreach (var (steam, set) in weaponModels) Console.WriteLine($"  {steam}: {string.Join(", ", set.Select(entry => $"{entry.Key}={entry.Value.World}"))}");
        });
    }

    private static bool ValidModelPath(string path) =>
        path.Length is >= 4 and <= 200 && path.EndsWith(".vmdl", StringComparison.Ordinal) && path.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '/' or '.' or '-');

    private void StoreWeaponModels(ulong steamId, JsonElement? profile)
    {
        weaponModels.Remove(steamId);
        agentModels.Remove(steamId);
        agentApplied.Remove(steamId);
        if (profile is not { } found || !found.TryGetProperty("weapon_models", out var all) || all.ValueKind != JsonValueKind.Object) return;
        var set = new Dictionary<string, WeaponModel>();
        foreach (var weapon in new[] { "knife", "deagle", "ak47" })
        {
            if (!all.TryGetProperty(weapon, out var entry) || entry.ValueKind != JsonValueKind.Object) continue;
            var world = StringOrNull(entry, "world");
            var view = StringOrNull(entry, "view") ?? world;
            if (world is null || view is null || !ValidModelPath(world) || !ValidModelPath(view)) continue;
            var anim = StringOrNull(entry, "anim_base");
            set[weapon] = new WeaponModel(world, view, anim is not null && AnimKnives.ContainsKey(anim) ? anim : null);
            foreach (var path in new[] { world, view })
                if (weaponModelPaths.Add(path))
                {
                    // A model this server has not precached yet: try to load it now; it is also kept for the next map.
                    try { Server.PrecacheModel(path); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
                }
        }
        if (set.Count > 0) weaponModels[steamId] = set;
        // The agents: one model for each side, from the same profile answer (weapon_models.agent_ct / agent_t).
        string? ct = AgentPath(all, "agent_ct"), t = AgentPath(all, "agent_t");
        if (ct is not null || t is not null)
        {
            agentModels[steamId] = (ct, t);
            foreach (var path in new[] { ct, t })
                if (path is not null && weaponModelPaths.Add(path))
                    try { Server.PrecacheModel(path); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
        }
    }

    private static string? AgentPath(JsonElement all, string key)
    {
        if (!all.TryGetProperty(key, out var entry) || entry.ValueKind != JsonValueKind.Object) return null;
        var world = StringOrNull(entry, "world");
        return world is not null && ValidModelPath(world) ? world : null;
    }

    /// <summary>Puts the agent model the player wears for their team on their pawn (once per pawn: a weapon pickup calls this again).</summary>
    private void ApplyAgentModel(CCSPlayerController player)
    {
        if (!agentModels.TryGetValue(player.SteamID, out var agents)) return;
        var path = player.TeamNum == 3 ? agents.Ct : player.TeamNum == 2 ? agents.T : null;
        var pawn = player.PlayerPawn.Value;
        if (path is null || pawn is null || !pawn.IsValid) return;
        if (agentApplied.TryGetValue(player.SteamID, out var done) && done.PawnIndex == pawn.Index && done.Path == path) return;
        try
        {
            pawn.SetModel(path);
            agentApplied[player.SteamID] = (pawn.Index, path);
            Console.WriteLine($"[{ModuleName}] Agent model {path} put on {player.PlayerName} ({(player.TeamNum == 3 ? "CT" : "T")}).");
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Agent model for {player.PlayerName} failed: {problem.Message}"); }
    }

    private void ApplyWeaponModels(CCSPlayerController player)
    {
        // Nothing for a player who is choosing a team, spectating or dead: there is no gun in their hands to change.
        if (!weaponModelsOn || !player.IsValid || player.IsBot || !player.PawnIsAlive || player.TeamNum < 2) return;
        ApplyAgentModel(player);
        if (!weaponModels.TryGetValue(player.SteamID, out var set)) return;
        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid || pawn.WeaponServices is null) return;
        // A knife model made for the M9 or Bayonet animations needs that knife under it: swap the player's knife for it (once, then the model goes on).
        if (knifeSwapOn && set.TryGetValue("knife", out var knifeModel) && knifeModel.AnimBase is { } animBase && AnimKnives.TryGetValue(animBase, out var wanted)
            && (!knifeSwappedAt.TryGetValue(player.SteamID, out var swapped) || (DateTime.UtcNow - swapped).TotalSeconds > 5))
        {
            var current = pawn.WeaponServices.MyWeapons.Select(h => h.Value).FirstOrDefault(w => w is { IsValid: true } && (w.DesignerName.Contains("knife", StringComparison.Ordinal) || w.DesignerName.Contains("bayonet", StringComparison.Ordinal)));
            if (current is not null && current.DesignerName != wanted)
            {
                knifeSwappedAt[player.SteamID] = DateTime.UtcNow;
                try
                {
                    current.Remove();
                    player.GiveNamedItem(wanted);
                    Console.WriteLine($"[{ModuleName}] Knife of {player.PlayerName} swapped for {wanted} (animations of the {animBase}).");
                }
                catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not swap the knife for {wanted}: {problem.Message}"); }
                AddTimer(0.3f, () => { if (player.IsValid) ApplyWeaponModels(player); });
                return;
            }
        }
        foreach (var handle in pawn.WeaponServices.MyWeapons)
        {
            var weapon = handle.Value;
            if (weapon is null || !weapon.IsValid) continue;
            var name = weapon.DesignerName;
            var kind = name.Contains("knife", StringComparison.Ordinal) || name.Contains("bayonet", StringComparison.Ordinal) ? "knife" : name == "weapon_deagle" ? "deagle" : name == "weapon_ak47" ? "ak47" : null;
            if (kind is null || !set.TryGetValue(kind, out var model)) continue;
            try
            {
                weapon.SetModel(model.World);
                Console.WriteLine($"[{ModuleName}] Weapon model {model.World} put on {player.PlayerName}'s {kind}.");
                // The first-person model: the view model entity that is showing this weapon.
                if (weapon.Handle == pawn.WeaponServices.ActiveWeapon.Value?.Handle)
                    foreach (var view in Utilities.FindAllEntitiesByDesignerName<CBaseModelEntity>("predicted_viewmodel"))
                        if (view.IsValid && Schema.GetSchemaValue<CHandle<CBasePlayerWeapon>>(view.Handle, "CBaseViewModel", "m_hWeapon").Value?.Handle == weapon.Handle) view.SetModel(model.View);
            }
            catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Weapon model for {player.PlayerName} ({kind}) failed: {problem.Message}"); }
        }
    }
}
