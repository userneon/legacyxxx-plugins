using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// Turn the running match into another mode on the spot (owner request 2026-11-06, "make the match another type straight away: retake, 1v1, random cheats,
/// wh vs wh"). Owner only (@css/root), on any server at any time (owner request 2026-11-06: root may use them whenever; everyone is told in chat who switched it). Switching restarts
/// the game (mp_restartgame 1); a map change or `off` ends the mode. UNTESTED in game, so try each on a test Fun server first.
///   css_retake        every round the bomb is already planted on a random site, the Ts are around it, the CTs come from their spawn with a kit
///   css_1v1           every round one T and one CT fight with the same random weapon, the rest wait dead (the ones who fought least go next)
///   css_randomcheats  every round every player gets a random cheat (wallhack, aimlock, silent aim or bunny hop) and is told which
///   css_whwh          everyone has the wallhack, both teams
///   css_modeoff       back to the ordinary game          css_gamemode [retake|1v1|cheats|wh|off]   with no word it says the current mode
/// </summary>
public partial class AdminPlus
{
    private static string gameMode = "";
    private readonly Dictionary<ulong, int> duelsPlayed = new();
    private readonly List<Vector> retakeSites = new();

    private static readonly string[] DuelWeapons = { "weapon_ak47", "weapon_m4a1", "weapon_awp", "weapon_deagle", "weapon_usp_silencer", "weapon_ssg08", "weapon_p90", "weapon_negev" };
    private static readonly string[] RetakeTPrimary = { "weapon_ak47", "weapon_galilar", "weapon_mac10", "weapon_ssg08", "weapon_sg556" };
    private static readonly string[] RetakeCtPrimary = { "weapon_m4a1", "weapon_m4a1_silencer", "weapon_famas", "weapon_aug", "weapon_mp9" };

    private void RegisterGameModes()
    {
        AddPermCmd("@css/root", "css_gamemode", "[retake|1v1|cheats|wh|off]", (c, i) => SetGameMode(c, i.ArgCount > 1 ? i.GetArg(1).ToLowerInvariant() : null));
        AddPermCmd("@css/root", "css_retake", "- Retake mode: the bomb is planted, Ts defend, CTs retake", (c, _) => SetGameMode(c, "retake"));
        AddPermCmd("@css/root", "css_1v1", "- Duel mode: one T and one CT fight each round", (c, _) => SetGameMode(c, "1v1"));
        AddPermCmd("@css/root", "css_randomcheats", "- Everyone gets a random cheat each round", (c, _) => SetGameMode(c, "cheats"));
        AddPermCmd("@css/root", "css_whwh", "- Wallhack for everyone, both teams", (c, _) => SetGameMode(c, "wh"));
        AddPermCmd("@css/root", "css_modeoff", "- Back to the ordinary game", (c, _) => SetGameMode(c, "off"));

        RegisterEventHandler<EventRoundStart>((_, _) => { if (gameMode.Length > 0) AddTimer(0.4f, ApplyGameModeRound, TimerFlags.STOP_ON_MAPCHANGE); return HookResult.Continue; });
        RegisterEventHandler<EventRoundFreezeEnd>((_, _) => { if (gameMode == "retake") PlantRetakeBomb(); return HookResult.Continue; });
        RegisterEventHandler<EventPlayerSpawn>((@event, _) =>
        {
            if (gameMode == "wh" && @event.Userid is { IsValid: true, IsBot: false } p) { wallhackSlots.Add(p.Slot); EnsureFunCheatTimer(); }
            return HookResult.Continue;
        });
        RegisterListener<Listeners.OnMapStart>(_ => { gameMode = ""; duelsPlayed.Clear(); retakeSites.Clear(); });
    }

    private static void ModeSay(string text)
    {
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
            player.PrintToChat(LegacyXChat.System(text));
    }

    private static List<CCSPlayerController> Contenders(int team) =>
        Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.TeamNum == team).ToList();

    private void SetGameMode(CCSPlayerController? caller, string? mode)
    {
        if (mode == null) { FunReply(caller, $"Game mode: {(gameMode.Length == 0 ? "ordinary" : gameMode)}. Use css_gamemode retake|1v1|cheats|wh|off."); return; }
        if (mode is not ("retake" or "1v1" or "cheats" or "wh" or "off")) { FunReply(caller, "Use css_gamemode retake|1v1|cheats|wh|off."); return; }

        EndGameMode();
        var who = caller?.PlayerName ?? "Console";
        if (mode == "off") { ModeSay($"{{white}}{SanitizeName(who)}{{grey}} put the game back to normal."); }
        else
        {
            gameMode = mode;
            var name = mode switch { "retake" => "Retake", "1v1" => "1v1 duels", "cheats" => "Random cheats", _ => "WH vs WH" };
            ModeSay($"{{white}}{SanitizeName(who)}{{grey}} switched the match to {{white}}{name}{{grey}}. The game restarts.");
            Server.ExecuteCommand("mp_restartgame 1");
        }
        FunReply(caller, $"Game mode: {(mode == "off" ? "ordinary" : mode)}.");
        AdminPlus.LogAction($"{who} set the game mode to {mode}");
    }

    private void EndGameMode()
    {
        gameMode = "";
        wallhackSlots.Clear(); aimlockSlots.Clear(); silentAimSlots.Clear(); bhopSlots.Clear();
        ClearWallhackGlows();
    }

    private void ApplyGameModeRound()
    {
        try
        {
            Console.WriteLine($"[AdminPlus] Game mode round: {gameMode}.");
            switch (gameMode)
            {
                case "retake": SetUpRetakeRound(); break;
                case "1v1": SetUpDuelRound(); break;
                case "cheats": SetUpRandomCheats(); break;
                case "wh":
                    foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot)) wallhackSlots.Add(p.Slot);
                    EnsureFunCheatTimer();
                    break;
            }
        }
        catch (Exception exception) { LogError($"game mode {gameMode}: {exception.Message}"); }
    }

    // ---- random cheats ------------------------------------------------------------------------------

    private void SetUpRandomCheats()
    {
        wallhackSlots.Clear(); aimlockSlots.Clear(); silentAimSlots.Clear(); bhopSlots.Clear();
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.TeamNum is 2 or 3))
        {
            var (name, set) = FunRandom.Next(4) switch
            {
                0 => ("wallhack", wallhackSlots),
                1 => ("aimlock (hold fire)", aimlockSlots),
                2 => ("silent aim", silentAimSlots),
                _ => ("bunny hop (hold jump)", bhopSlots),
            };
            set.Add(player.Slot);
            player.PrintToChat(LegacyXChat.System($"Your cheat this round: {{white}}{name}{{grey}}."));
        }
        EnsureFunCheatTimer();
    }

    // ---- 1v1 ----------------------------------------------------------------------------------------

    private static void Arm(CCSPlayerController player, params string[] items)
    {
        if (!player.IsValid || !player.PawnIsAlive) return;
        player.RemoveWeapons();
        foreach (var item in items) { try { player.GiveNamedItem(item); } catch { } }
    }

    private void SetUpDuelRound()
    {
        var picked = new List<CCSPlayerController>();
        foreach (var team in new[] { 2, 3 })
        {
            var pool = Contenders(team).Where(p => p.PawnIsAlive).ToList();
            if (pool.Count == 0) { ModeSay("{grey}1v1 needs a player on both teams."); return; }
            var fewest = pool.Min(p => duelsPlayed.GetValueOrDefault(p.SteamID));
            var next = pool.Where(p => duelsPlayed.GetValueOrDefault(p.SteamID) == fewest).OrderBy(_ => FunRandom.Next()).First();
            duelsPlayed[next.SteamID] = fewest + 1;
            picked.Add(next);
        }
        var weapon = DuelWeapons[FunRandom.Next(DuelWeapons.Length)];
        foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.TeamNum is 2 or 3 && p.PawnIsAlive))
        {
            if (picked.Contains(player)) Arm(player, "weapon_knife", weapon, "item_assaultsuit");
            else player.PlayerPawn!.Value!.CommitSuicide(false, false);   // waits for the next round, dead
        }
        ModeSay($"{{white}}1v1{{grey}}: {{white}}{SanitizeName(picked[0].PlayerName)}{{grey}} against {{white}}{SanitizeName(picked[1].PlayerName)}{{grey}}, {{white}}{weapon[7..]}{{grey}}.");
    }

    // ---- retake -------------------------------------------------------------------------------------

    private List<Vector> FindBombSites()
    {
        if (retakeSites.Count > 0) return retakeSites;
        foreach (var target in Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_bomb_target"))
        {
            try
            {
                var origin = target.AbsOrigin ?? new Vector(0, 0, 0);
                var mins = target.Collision.Mins; var maxs = target.Collision.Maxs;
                // The middle of the trigger's box in the world, whether the entity sits at the origin or in the site.
                retakeSites.Add(new Vector(origin.X + (mins.X + maxs.X) / 2f, origin.Y + (mins.Y + maxs.Y) / 2f, origin.Z + mins.Z + 20f));
            }
            catch { }
        }
        return retakeSites;
    }

    private Vector? retakeSite;
    private int retakeSiteIndex;

    private void SetUpRetakeRound()
    {
        var sites = FindBombSites();
        Console.WriteLine($"[AdminPlus] Retake: {sites.Count} bomb site(s) found on {Server.MapName}: {string.Join(" | ", sites.Select(v => $"{v.X:0},{v.Y:0},{v.Z:0}"))}");
        if (sites.Count == 0) { ModeSay("{grey}This map has no bomb site to retake."); return; }
        retakeSiteIndex = FunRandom.Next(sites.Count);
        var site = retakeSite = sites[retakeSiteIndex];

        var defenders = Contenders(2).Where(p => p.PawnIsAlive).ToList();
        for (var i = 0; i < defenders.Count; i++)
        {
            var angle = i * 2 * MathF.PI / Math.Max(1, defenders.Count);
            var pawn = defenders[i].PlayerPawn!.Value!;
            pawn.Teleport(new Vector(site.X + MathF.Cos(angle) * 90f, site.Y + MathF.Sin(angle) * 90f, site.Z + 8f), null, new Vector(0, 0, 0));
            Arm(defenders[i], "weapon_knife", FunRandom.Next(2) == 0 ? "weapon_glock" : "weapon_tec9", RetakeTPrimary[FunRandom.Next(RetakeTPrimary.Length)], "item_assaultsuit", "weapon_hegrenade", "weapon_smokegrenade");
        }
        foreach (var attacker in Contenders(3).Where(p => p.PawnIsAlive))
            Arm(attacker, "weapon_knife", "weapon_usp_silencer", RetakeCtPrimary[FunRandom.Next(RetakeCtPrimary.Length)], "item_assaultsuit", "item_defuser", "weapon_flashbang", "weapon_smokegrenade");
        Console.WriteLine($"[AdminPlus] Retake: site {retakeSiteIndex} at {site.X:0},{site.Y:0},{site.Z:0}; {defenders.Count} defender(s), {Contenders(3).Count(p => p.PawnIsAlive)} retaker(s).");
        ModeSay($"{{white}}Retake{{grey}}: the bomb is planted on site {{white}}{(retakeSiteIndex == 0 ? "A" : "B")}{{grey}}. Terrorists defend it, Counter-Terrorists retake it.");
    }

    private void PlantRetakeBomb()
    {
        if (retakeSite is not { } site) return;
        try
        {
            var c4 = Utilities.CreateEntityByName<CPlantedC4>("planted_c4");
            if (c4 == null || !c4.IsValid) return;
            c4.Teleport(site, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            c4.BombTicking = true;
            c4.CannotBeDefused = false;
            var seconds = ConVar.Find("mp_c4timer")?.GetPrimitiveValue<float>() ?? 40f;
            c4.C4Blow = Server.CurrentTime + seconds;
            c4.TimerLength = seconds;
            c4.DispatchSpawn();
            var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            Console.WriteLine($"[AdminPlus] Retake: the bomb is planted ({seconds:0} s), game rules {(rules != null ? "found" : "missing")}.");
            if (rules != null) { rules.BombPlanted = true; Utilities.SetStateChanged(Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First(), "CCSGameRulesProxy", "m_pGameRules"); }
        }
        catch (Exception exception) { LogError($"retake bomb: {exception.Message}"); }
    }
}
