using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
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
    /// <summary>The item number of each knife in <see cref="AnimKnives"/> (the same numbers as the API's ANIMATION_KNIVES): what tells a given knife from the plain one.</summary>
    private static readonly Dictionary<string, int> KnifeDefIndex = new()
    {
        ["weapon_bayonet"] = 500, ["weapon_knife_css"] = 503, ["weapon_knife_flip"] = 505, ["weapon_knife_gut"] = 506, ["weapon_knife_karambit"] = 507, ["weapon_knife_m9_bayonet"] = 508,
        ["weapon_knife_tactical"] = 509, ["weapon_knife_falchion"] = 512, ["weapon_knife_survival_bowie"] = 514, ["weapon_knife_butterfly"] = 515, ["weapon_knife_push"] = 516,
        ["weapon_knife_cord"] = 517, ["weapon_knife_canis"] = 518, ["weapon_knife_ursus"] = 519, ["weapon_knife_gypsy_jackknife"] = 520, ["weapon_knife_outdoor"] = 521,
        ["weapon_knife_stiletto"] = 522, ["weapon_knife_widowmaker"] = 523, ["weapon_knife_skeleton"] = 525,
    };

    /// <summary>The knives a model can be made for the animations of (`anim_base`): the knife that must sit under it. Same list as ANIMATION_KNIVES in the API and the SQL check.</summary>
    private static readonly Dictionary<string, string> AnimKnives = new()
    {
        ["bayonet"] = "weapon_bayonet", ["classic"] = "weapon_knife_css", ["flip"] = "weapon_knife_flip", ["gut"] = "weapon_knife_gut", ["karambit"] = "weapon_knife_karambit",
        ["m9"] = "weapon_knife_m9_bayonet", ["huntsman"] = "weapon_knife_tactical", ["falchion"] = "weapon_knife_falchion", ["bowie"] = "weapon_knife_survival_bowie",
        ["butterfly"] = "weapon_knife_butterfly", ["daggers"] = "weapon_knife_push", ["paracord"] = "weapon_knife_cord", ["survival"] = "weapon_knife_canis",
        ["ursus"] = "weapon_knife_ursus", ["navaja"] = "weapon_knife_gypsy_jackknife", ["nomad"] = "weapon_knife_outdoor", ["stiletto"] = "weapon_knife_stiletto",
        ["talon"] = "weapon_knife_widowmaker", ["skeleton"] = "weapon_knife_skeleton",
    };
    // When each player's knife was last swapped for the animation knife: at most once every few seconds, so another plugin giving the knife back cannot start a loop.
    private readonly Dictionary<ulong, DateTime> knifeSwappedAt = new();
    // When each player's knife last got its weapon subclass: at most once every 1.5 s, so a subclass change that makes the game announce the knife again cannot start a loop.
    private readonly Dictionary<ulong, DateTime> knifeSubclassAt = new();
    // The knife entity that already has its Apex subclass (player -> entity index and subclass): a subclass is put on a knife ONCE, not every time the knife is drawn again (each ChangeSubclass
    // re-sets the weapon's model, animation class and skeleton: repeated at every knife/gun switch it was a visible hitch, owner 2026-11-19). Cleared at spawn and when the profile is read again.
    private readonly Dictionary<ulong, (uint Index, string Subclass)> knifeSubclassDone = new();
    // How many times the subclass was put on this knife entity: the log of 2026-11-21 showed it put on again and again every 2 s (the model name read back right after ChangeSubclass need not equal the pick's path), so after 2 tries it is left alone.
    private readonly Dictionary<ulong, int> knifeSubclassTries = new();
    // When a pick's model was last put on a weapon entity (player, entity index): used only to tell in the log that something changed it back.
    private readonly Dictionary<(ulong, uint), DateTime> modelSetAt = new();
    private readonly Dictionary<ulong, DateTime> weaponApplyAt = new();
    // The Apex prefab knives (ChangeSubclass) need scripts/weapons.vdata_c in the published addon; until it is there the knife keeps its model through SetModel as before.
    // On unless LEGACYX_COMMUNITY_APEX_SUBCLASS=false.
    private bool apexSubclassOn;
    // anim_base -> the weapon prefab (weapons.vdata) whose animation class that knife has, from LEGACYX_COMMUNITY_KNIFE_SUBCLASS="paracord=<prefab>,karambit=<prefab>". Nothing is guessed: a base without an entry is left alone.
    private readonly Dictionary<string, string> animBaseSubclass = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The seller's Apex knives are weapon prefabs in the addon's scripts/weapons.vdata (apex_karambit, apex_balisong ...), each with its own model, animation class and skeleton.
    /// The engine reads those from the prefab named by the weapon's SUBCLASS, so the knife in hand is told its subclass: <c>ChangeSubclass "apex_karambit"</c>
    /// (README-APEX.txt of the seller's v2 archive). The prefab name is the model's folder: weapons/bluesen/apex/&lt;name&gt;/&lt;name&gt;.vmdl.
    /// </summary>
    private static string? KnifeSubclass(string world)
    {
        const string prefix = "weapons/bluesen/apex/";
        if (!world.StartsWith(prefix, StringComparison.Ordinal) || !world.EndsWith(".vmdl", StringComparison.Ordinal)) return null;
        var parts = world[prefix.Length..^5].Split('/');
        return parts.Length == 2 && parts[0] == parts[1] && parts[0].StartsWith("apex_", StringComparison.Ordinal) ? parts[0] : null;
    }

    /// <summary>
    /// Runs one engine call and says in the log when it took 15 ms or more (a 64-tick frame is 15.6 ms): the log then shows which call freezes the server, and for how long
    /// (owner 2026-11-20: other servers run 195 custom models and re-rigged agents without trouble, this one hitches). Turn off with LEGACYX_COMMUNITY_SLOW_CALL_LOG=false.
    /// </summary>
    private void Timed(string what, Action action)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try { action(); }
        finally
        {
            watch.Stop();
            if (slowCallLog && watch.ElapsedMilliseconds >= 15)
                Console.WriteLine($"[{ModuleName}] SLOW CALL: {what} took {watch.ElapsedMilliseconds} ms ({Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && !p.IsHLTV)} players)");
        }
    }
    private bool slowCallLog = true;

    private void RegisterWeaponModels()
    {
        // LegacyX-WeaponPaints asks this before it puts the player's own knife pick (a paracord ...) on their knife: a player who wears a Shop knife model keeps the
        // default knife under it, or the pick's own model and animations replaced the Shop model a moment after it was put on (owner report 2026-11-14).
        // Only a knife without an animation base (the Apex prefab knives): a model made for the paracord's skeleton sits on the knife pick (the paracord) the skinchanger gives.
        // A knife model made for another knife's animations (the katanas, made for the paracord) puts that knife under it by itself: the skinchanger plugin asks which one.
        Capabilities.RegisterPluginCapability(new PluginCapability<Func<ulong, string?>>("legacyx:shop_knife_pick"), () => steamId =>
            weaponModelsOn && weaponModels.TryGetValue(steamId, out var worn) && worn.TryGetValue("knife", out var knife) && knife.AnimBase is { } baseName && AnimKnives.TryGetValue(baseName, out var designer) ? designer : null);
        Capabilities.RegisterPluginCapability(new PluginCapability<Func<ulong, bool>>("legacyx:custom_knife_model"), () => steamId => weaponModelsOn && weaponModels.TryGetValue(steamId, out var worn) && worn.TryGetValue("knife", out var knife) && knife.AnimBase is null);
        slowCallLog = !string.Equals(LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_SLOW_CALL_LOG"), "false", StringComparison.OrdinalIgnoreCase);
        knifeSwapOn = string.Equals(LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_KNIFE_SWAP"), "true", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[{ModuleName}] Weapon models: knife swap for the M9/Bayonet animations is {(knifeSwapOn ? "ON" : "off")}.");
        // On unless LEGACYX_COMMUNITY_APEX_SUBCLASS=false: the seller's scripts/weapons.vdata_c is in the published Workshop addon (owner, 2026-11-14), so the Apex knives
        // take their animations from their prefabs. (It was switched off for a day while a server that would not start was looked into: that was a CS2 update that failed on
        // root-owned folders, not this.)
        apexSubclassOn = !string.Equals(LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_APEX_SUBCLASS"), "false", StringComparison.OrdinalIgnoreCase);
        RegisterAgentReplacement();
        Console.WriteLine($"[{ModuleName}] Weapon models: Apex knife subclass (weapons.vdata prefabs) is {(apexSubclassOn ? "ON" : "off (LEGACYX_COMMUNITY_APEX_SUBCLASS=false), the model is put on with SetModel")}.");
        foreach (var pair in LegacyX.Shared.Configuration.LegacyXEnvironmentLoader.Load().Get("LEGACYX_COMMUNITY_KNIFE_SUBCLASS").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && AnimKnives.ContainsKey(parts[0]) && parts[1].Length is > 0 and <= 80 && parts[1].All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                animBaseSubclass[parts[0]] = parts[1];
        }
        Console.WriteLine($"[{ModuleName}] Weapon models: knife subclasses for animation bases: {(animBaseSubclass.Count == 0 ? "none" : string.Join(", ", animBaseSubclass.Select(pair => $"{pair.Key}={pair.Value}")))}.");
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
                try { Timed($"PrecacheModel {path}", () => Server.PrecacheModel(path)); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
        }));
        // A player types !wm to see what the server has for them (the console commands below are for the server's own console only).
        AddCommand("css_wm", "Weapon models: what is worn", (player, _) =>
        {
            if (player is null || !player.IsValid) return;
            if (!weaponModels.TryGetValue(player.SteamID, out var worn)) { player.PrintToChat(LegacyX.Shared.Configuration.LegacyXChat.System($"{{grey}}No weapon model is worn (or the server has not read your profile yet). {weaponModelPaths.Count} model path(s) known.")); return; }
            foreach (var (weapon, model) in worn) player.PrintToChat(LegacyX.Shared.Configuration.LegacyXChat.System($"{{white}}{weapon}{{grey}}: {model.World}{(model.AnimBase is null ? "" : $" ({model.AnimBase} animations)")}"));
        });
        RegisterEventHandler<EventPlayerSpawn>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true } player) { knifeSubclassDone.Remove(player.SteamID); ApplyWeaponModelsLater(player); ReplaceAgentLater(player); }
            return HookResult.Continue;
        });
        // The subclass can only be changed on the knife that is IN HAND (on a holstered weapon it crashed the seller's server), so it is applied when the knife is equipped.
        RegisterEventHandler<EventItemEquip>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true } player)
            {
                // A knife drawn: its model / Apex subclass goes on at once (the next frame), not only after the series below (owner 2026-11-20: for the first seconds the plain knife was in the hand,
                // then it changed). ApplyWeaponModels leaves a knife that already has its model alone, so the extra call costs nothing.
                var item = @event.Item ?? "";
                if (item.Contains("knife", StringComparison.Ordinal) || item.Contains("bayonet", StringComparison.Ordinal))
                    Server.NextFrame(() => { if (player.IsValid) ApplyWeaponModels(player); });
                ApplyWeaponModelsLater(player);
            }
            return HookResult.Continue;
        });
        // The end of the freeze time: everybody's models are checked once more (a player who spawned while loading is in the game by now).
        RegisterEventHandler<EventRoundFreezeEnd>((_, _) =>
        {
            foreach (var each in Utilities.GetPlayers()) if (each.IsValid && !each.IsBot && each.PawnIsAlive) { ApplyWeaponModelsLater(each); ReplaceAgentLater(each); }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventItemPickup>((@event, _) =>
        {
            if (@event.Userid is { IsValid: true } player) ApplyWeaponModelsLater(player);
            return HookResult.Continue;
        });
        AddCommand("lx_wm_off", "Weapon models: switch off or on (0/1)", (player, command) =>
        {
            if (player != null) return;
            weaponModelsOn = command.ArgCount < 2 || command.GetArg(1) != "0";
            Console.WriteLine($"[{ModuleName}] Weapon models {(weaponModelsOn ? "on" : "off")}.");
        });
        // Try a knife base without touching the database: `lx_wm_try <steamid64> <base>` puts that knife under the player's worn knife model right now (base `none` clears it).
        // For finding which knife an own-graph model plays right on; the choice lasts until the player's profile is read again (a new join), the database is not changed.
        AddCommand("lx_wm_try", "Weapon models: try an animation base on a player (lx_wm_try <steamid64> <base|none>)", (player, command) =>
        {
            if (player != null) return;
            if (command.ArgCount < 3 || !ulong.TryParse(command.GetArg(1), out var steam)) { Say(command, $"[{ModuleName}] Usage: lx_wm_try <steamid64> <base|none>. Bases: {string.Join(", ", AnimKnives.Keys)}"); return; }
            var baseName = command.GetArg(2).ToLowerInvariant();
            if (baseName != "none" && !AnimKnives.ContainsKey(baseName)) { Say(command, $"[{ModuleName}] Unknown base '{baseName}'. Bases: {string.Join(", ", AnimKnives.Keys)}"); return; }
            if (!weaponModels.TryGetValue(steam, out var worn) || !worn.TryGetValue("knife", out var knife)) { Say(command, $"[{ModuleName}] {steam} wears no knife model (or the profile has not been read yet)."); return; }
            var target = Utilities.GetPlayers().FirstOrDefault(candidate => candidate.IsValid && candidate.SteamID == steam);
            if (target is null) { Say(command, $"[{ModuleName}] {steam} is not on this server."); return; }
            worn["knife"] = knife with { AnimBase = baseName == "none" ? null : baseName };
            knifeSwapOn = true;
            knifeSwappedAt.Remove(steam);
            Say(command, $"[{ModuleName}] Trying base '{baseName}' on {target.PlayerName} (knife swap is now ON until the plugin reloads).");
            ApplyWeaponModels(target);
        });
        // The seller of the Apex knives says their own animations play only on the character models of his replace pack (re-rigged agents). This puts one of them on a player
        // right now to find out: `lx_wm_agent <steamid64> <path.vmdl>` (a path under agents/models/bluesen/...; `none` is not needed, the next spawn gives the player's own model back).
        AddCommand("lx_wm_agent", "Weapon models: put a character model on a player (lx_wm_agent <steamid64> <path.vmdl>)", (player, command) =>
        {
            if (player != null) return;
            if (command.ArgCount < 3 || !ulong.TryParse(command.GetArg(1), out var steam)) { Say(command, $"[{ModuleName}] Usage: lx_wm_agent <steamid64> <agents/models/bluesen/default_agents/ctm_fbi/ctm_fbi.vmdl>"); return; }
            var path = command.GetArg(2).ToLowerInvariant();
            if (!ValidModelPath(path)) { Say(command, $"[{ModuleName}] '{path}' is not a model path (lower case letters, digits, _ - . / and it ends in .vmdl)."); return; }
            var target = Utilities.GetPlayers().FirstOrDefault(candidate => candidate.IsValid && candidate.SteamID == steam);
            var pawn = target?.PlayerPawn.Value;
            if (target is null || pawn is null || !pawn.IsValid || !target.PawnIsAlive) { Say(command, $"[{ModuleName}] {steam} is not on this server and alive."); return; }
            try
            {
                // Kept for every later map too (the precache list is rebuilt from this set at each map start), so nothing has to be added to the .env for a test.
                if (weaponModelPaths.Add(path)) Say(command, $"[{ModuleName}] {path} added to the precache list (it is in the manifest from the next map on).");
                Server.PrecacheModel(path);
                pawn.SetModel(path);
                Say(command, $"[{ModuleName}] Character model {path} put on {target.PlayerName} (until the next spawn).");
            }
            catch (Exception problem) { Say(command, $"[{ModuleName}] Could not put {path} on {target.PlayerName}: {problem.Message}"); }
        });
        AddCommand("lx_wm_status", "Weapon models: who wears what", (player, command) =>
        {
            if (player != null) return;
            Say(command, $"[{ModuleName}] Weapon models {(weaponModelsOn ? "on" : "off")}: {weaponModels.Count} players, {weaponModelPaths.Count} paths precached.");
            foreach (var (steam, set) in weaponModels) Say(command, $"  {steam}: {string.Join(", ", set.Select(entry => $"{entry.Key}={entry.Value.World}"))}");
        });
    }

    // Answers on the server's console and, when the command came from RCON or a console that can hear it, back to whoever sent it.
    private void Say(CommandInfo command, string text)
    {
        Console.WriteLine(text);
        try { command.ReplyToCommand(text); } catch { }
    }

    private static bool ValidModelPath(string path) =>
        path.Length is >= 4 and <= 200 && path.EndsWith(".vmdl", StringComparison.Ordinal) && path.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '/' or '.' or '-');

    private void StoreWeaponModels(ulong steamId, JsonElement? profile)
    {
        weaponModels.Remove(steamId);
        knifeSubclassDone.Remove(steamId);
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
                    try { Timed($"PrecacheModel {path}", () => Server.PrecacheModel(path)); } catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Could not precache {path}: {problem.Message}"); }
                }
        }
        if (set.Count > 0) weaponModels[steamId] = set;
    }

    /// <summary>Applies the player's models on the next frame and again a little later, after other plugins and the game itself have finished changing the weapon.</summary>
    private static string? CurrentModel(CBaseEntity entity)
    {
        try { return entity.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName; }
        catch { return null; }
    }

    private void ApplyWeaponModelsLater(CCSPlayerController player)
    {
        if (!weaponModelsOn || !weaponModels.ContainsKey(player.SteamID)) return;
        // At most one series a second per player (2026-11-15: the log showed the same SetModel lines dozens of times a second and players' games closed; something kept announcing the weapons again).
        if (weaponApplyAt.TryGetValue(player.SteamID, out var last) && (DateTime.UtcNow - last).TotalSeconds < 1.0) return;
        weaponApplyAt[player.SteamID] = DateTime.UtcNow;
        Server.NextFrame(() => ApplyWeaponModels(player));
        // The last tries (3.5 s .. 10 s) are for a player whose game is still loading (owner 2026-11-22: the models and gloves are put on only sometimes). ApplyWeaponModels leaves what is already right alone.
        foreach (var delay in new[] { 0.25f, 0.8f, 2.0f, 3.5f, 6.0f, 10.0f })
            AddTimer(delay, () => { if (player.IsValid) ApplyWeaponModels(player); });
    }

    /// <summary>Puts `path` on the view model entity that shows `weapon` in `player`'s hands, if the weapon is still the one in hand; says in the log when no view model was found.</summary>
    private void PutViewModel(CCSPlayerController player, CBasePlayerWeapon weapon, string path, string kind)
    {
        try
        {
            if (!player.IsValid || !weapon.IsValid || player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value?.Handle != weapon.Handle) return;
            var found = false;
            // The view model entities that show this weapon (also found by their weapon handle).
            foreach (var view in Utilities.FindAllEntitiesByDesignerName<CBaseModelEntity>("predicted_viewmodel"))
            {
                if (!view.IsValid || Schema.GetSchemaValue<CHandle<CBasePlayerWeapon>>(view.Handle, "CBaseViewModel", "m_hWeapon").Value?.Handle != weapon.Handle) continue;
                SetViewModel(view, path);
                found = true;
            }
            if (!found && DateTime.UtcNow - lastNoViewModelLog > TimeSpan.FromMinutes(2))
            {
                // journalctl 2026-11-21: no server-side view model entity of any kind exists (the diagnosis listed none), so there is nothing to put the first-person model on; the weapon's own model (set by the caller) is what
                // the player sees in hand, and the katanas show. Said once in two minutes, not on every try (it was dozens of lines a minute). The earlier fallback that read the pawn's view model services from raw memory is gone: its handles were garbage.
                lastNoViewModelLog = DateTime.UtcNow;
                Console.WriteLine($"[{ModuleName}] No server-side view model entity exists: the first-person picture is the weapon's own model ({kind}, {path}). This line is shown once in two minutes.");
            }
        }
        catch (Exception problem) { Console.WriteLine($"[{ModuleName}] First-person model for {player.PlayerName} ({kind}) failed: {problem.Message}"); }
    }

    private DateTime lastNoViewModelLog = DateTime.MinValue;

    /// <summary>The model on a view model entity, told to the clients too (the engine does not send a changed model by itself).</summary>
    private void SetViewModel(CBaseModelEntity view, string path)
    {
        Timed($"view model SetModel {path}", () => view.SetModel(path));
        Utilities.SetStateChanged(view, "CBaseEntity", "m_nModelIndex");
        Utilities.SetStateChanged(view, "CBaseModelEntity", "m_vecViewOffset");
    }

    private void ApplyWeaponModels(CCSPlayerController player)
    {
        // Nothing for a player who is choosing a team, spectating or dead: there is no gun in their hands to change.
        if (!weaponModelsOn || !player.IsValid || player.IsBot || !player.PawnIsAlive || player.TeamNum < 2 || !weaponModels.TryGetValue(player.SteamID, out var set)) return;
        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid || pawn.WeaponServices is null) return;
        // A knife model made for the M9 or Bayonet animations needs that knife under it: swap the player's knife for it (once, then the model goes on).
        if (knifeSwapOn && set.TryGetValue("knife", out var knifeModel) && knifeModel.AnimBase is { } animBase && AnimKnives.TryGetValue(animBase, out var wanted)
            && (!knifeSwappedAt.TryGetValue(player.SteamID, out var swapped) || (DateTime.UtcNow - swapped).TotalSeconds > 5))
        {
            var current = pawn.WeaponServices.MyWeapons.Select(h => h.Value).FirstOrDefault(w => w is { IsValid: true } && (w.DesignerName.Contains("knife", StringComparison.Ordinal) || w.DesignerName.Contains("bayonet", StringComparison.Ordinal)));
            // Compared by item number as well as by name: most knives (the Karambit, the Paracord ...) are called weapon_knife as entities, so a name check alone is never equal and the knife would be removed and given again forever, which froze the server at team select.
            if (current is not null && current.DesignerName != wanted && !(KnifeDefIndex.TryGetValue(wanted, out var wantedIndex) && current.AttributeManager.Item.ItemDefinitionIndex == wantedIndex))
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
            if (kind == "knife" && apexSubclassOn && KnifeSubclass(model.World) is { } subclass)
            {
                // A prefab knife: only the knife in hand can be changed; its model, animation class and skeleton then come from the vdata, so no SetModel on top (that would keep the vanilla animations).
                if (weapon.Handle != pawn.WeaponServices.ActiveWeapon.Value?.Handle) continue;
                // Already done AND still true: this knife entity got the subclass and its model is the Apex model. Not repeated at every knife/gun switch (a hitch). When the model is NOT the Apex
                // model any more (the subclass did not take, another plugin put the pick back, a reconnect), it is put on again.
                if (knifeSubclassDone.TryGetValue(player.SteamID, out var done) && done.Index == weapon.Index && done.Subclass == subclass
                    && (string.Equals(CurrentModel(weapon), model.World, StringComparison.OrdinalIgnoreCase) || knifeSubclassTries.GetValueOrDefault(player.SteamID) >= 2)) continue;
                if (knifeSubclassAt.TryGetValue(player.SteamID, out var changed) && (DateTime.UtcNow - changed).TotalSeconds < 1.5) continue;
                knifeSubclassAt[player.SteamID] = DateTime.UtcNow;
                try
                {
                    Timed($"ChangeSubclass {subclass}", () => weapon.AcceptInput("ChangeSubclass", value: subclass));
                    knifeSubclassTries[player.SteamID] = knifeSubclassDone.TryGetValue(player.SteamID, out var before) && before.Index == weapon.Index ? knifeSubclassTries.GetValueOrDefault(player.SteamID) + 1 : 1;
                    knifeSubclassDone[player.SteamID] = (weapon.Index, subclass);
                    Console.WriteLine($"[{ModuleName}] Knife subclass {subclass} put on {player.PlayerName}'s knife in hand.");
                }
                catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Knife subclass {subclass} for {player.PlayerName} failed: {problem.Message}"); }
                continue;
            }
            // A knife model made for another knife's animations (a katana on the paracord's skeleton) is NOT given a weapon subclass any more (owner, 2026-11-15): its own
            // model goes on with SetModel and plays that knife's animations through the skeleton it was made for. Only the Apex prefab knives (above) use a subclass.
            try
            {
                // A weapon that already has the model is left alone: setting it again is announced to every client again.
                if (string.Equals(CurrentModel(weapon), model.World, StringComparison.OrdinalIgnoreCase))
                {
                    // The world model is on, but the game puts the weapon's own view model back while it is drawn: skipping the weapon here left the world model (the shadow)
                    // and an empty hand (audit 2026-11-18). The view model is put on again here, as every series did before 24d1ccb8 (the one-second limit of ApplyWeaponModelsLater keeps it rare).
                    if (weapon.Handle == pawn.WeaponServices.ActiveWeapon.Value?.Handle) PutViewModel(player, weapon, model.View, kind);
                    continue;
                }
                // Diagnosis (owner 2026-11-22: "something puts the model back"): when this very weapon already got the pick's model before, say WHAT it has now and how long ago we set it.
                var currentNow = CurrentModel(weapon);
                if (modelSetAt.TryGetValue((player.SteamID, weapon.Index), out var setBefore) && (DateTime.UtcNow - setBefore).TotalSeconds < 120)
                    Console.WriteLine($"[{ModuleName}] Model of {player.PlayerName}'s {kind} was changed back by something: now {currentNow ?? "?"}, pick {model.World}, set {(DateTime.UtcNow - setBefore).TotalSeconds:0.0} s ago.");
                modelSetAt[(player.SteamID, weapon.Index)] = DateTime.UtcNow;
                Timed($"weapon SetModel {model.World}", () => weapon.SetModel(model.World));
                Console.WriteLine($"[{ModuleName}] Weapon model {model.World} put on {player.PlayerName}'s {kind}.");
                // The first-person model: the view model entity that is showing this weapon. The game puts the weapon's own view model back while it is being
                // drawn (the equip animation), so a katana put on at once can vanish from the hand; it is put on again a moment later, twice.
                if (weapon.Handle == pawn.WeaponServices.ActiveWeapon.Value?.Handle)
                {
                    PutViewModel(player, weapon, model.View, kind);
                    foreach (var delay in new[] { 0.12f, 0.4f })
                        AddTimer(delay, () => PutViewModel(player, weapon, model.View, kind));
                }
            }
            catch (Exception problem) { Console.WriteLine($"[{ModuleName}] Weapon model for {player.PlayerName} ({kind}) failed: {problem.Message}"); }
        }
    }
}
