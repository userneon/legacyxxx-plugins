using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// .swap (owner request 2026-10-22): both teams trade sides if a majority of each team agrees within 30 seconds. Whoever is alive
/// keeps the guns, armor and money they had, so a swap in the middle of a round costs nobody their buy. Right after the knife round the
/// winners' own .swap still works as .switch. Matches built from a setup keep their fixed rosters and do not take a vote.
/// </summary>
public partial class MatchZy
{
    private const float LegacyXSwapVoteSeconds = 30f;

    private sealed class SwapGear
    {
        public string[] Weapons = Array.Empty<string>();
        public int Money;
        public int Armor;
        public bool Helmet;
        public bool Defuser;
    }

    private readonly HashSet<int> legacyXSwapYes = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? legacyXSwapTimer;

    [ConsoleCommand("css_swap", "Vote to swap the two teams' sides")]
    public void OnSwapVoteCommand(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid || player.IsBot || !player.UserId.HasValue) return;
        if (isSideSelectionPhase)
        {
            OnTeamSwitch(player, command);
            return;
        }
        if (!LegacyXTeamGateActive || isKnifeRound || (!matchStarted && !readyAvailable))
        {
            PrintToPlayerChat(player, "A swap vote is not available now.");
            return;
        }
        if (!IsSeatedTeam(player.TeamNum))
        {
            PrintToPlayerChat(player, "Only players on a team can vote to swap.");
            return;
        }

        if (legacyXSwapYes.Count == 0)
        {
            legacyXSwapTimer?.Kill();
            legacyXSwapTimer = AddTimer(LegacyXSwapVoteSeconds, () =>
            {
                if (legacyXSwapYes.Count == 0) return;
                legacyXSwapYes.Clear();
                PrintToAllChat("The swap vote ended without enough votes.");
            });
            PrintToAllChat($"{ChatColors.White}{player.PlayerName}{ChatColors.Grey} wants to swap sides. Type {ChatColors.White}.swap{ChatColors.Grey} to agree. Both teams need a majority.");
        }
        if (!legacyXSwapYes.Add(player.UserId.Value))
        {
            PrintToPlayerChat(player, "You already voted to swap.");
            return;
        }

        var humans = Utilities.GetPlayers().Where(p => IsPlayerValid(p) && !p.IsBot && !p.IsHLTV && IsSeatedTeam(p.TeamNum)).ToList();
        var passed = true;
        foreach (var team in new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist })
        {
            var members = humans.Where(p => p.TeamNum == (byte)team).ToList();
            var yes = members.Count(p => legacyXSwapYes.Contains(p.UserId!.Value));
            if (members.Count > 0 && yes * 2 <= members.Count) passed = false;
        }
        if (!passed)
        {
            var tYes = humans.Count(p => p.TeamNum == (byte)CsTeam.Terrorist && legacyXSwapYes.Contains(p.UserId!.Value));
            var tAll = humans.Count(p => p.TeamNum == (byte)CsTeam.Terrorist);
            var ctYes = humans.Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist && legacyXSwapYes.Contains(p.UserId!.Value));
            var ctAll = humans.Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist);
            PrintToAllChat($"Swap votes: T {tYes}/{tAll}, CT {ctYes}/{ctAll}.");
            return;
        }

        legacyXSwapTimer?.Kill();
        legacyXSwapTimer = null;
        legacyXSwapYes.Clear();
        PerformLegacyXSwap(humans);
    }

    private void PerformLegacyXSwap(List<CCSPlayerController> humans)
    {
        var gear = new Dictionary<int, SwapGear>();
        foreach (var player in humans)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.Health <= 0) continue;
            gear[player.UserId!.Value] = new SwapGear
            {
                Weapons = pawn.WeaponServices?.MyWeapons
                    .Where(handle => handle.IsValid && handle.Value != null && !string.IsNullOrWhiteSpace(handle.Value.DesignerName))
                    .Select(handle => handle.Value!.DesignerName)
                    .Where(name => name.StartsWith("weapon_", StringComparison.Ordinal) && name != "weapon_c4")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>(),
                Money = player.InGameMoneyServices?.Account ?? 0,
                Armor = pawn.ArmorValue,
                Helmet = (pawn.ItemServices as CCSPlayer_ItemServices)?.HasHelmet ?? false,
                Defuser = (pawn.ItemServices as CCSPlayer_ItemServices)?.HasDefuser ?? false,
            };
        }

        Server.ExecuteCommand("mp_swapteams");
        if (matchStarted && isMatchSetup) SwapSidesInTeamData(true);
        PrintToAllChat("Both teams agreed. Sides are swapped, and everyone keeps their gear.");

        AddTimer(0.4f, () =>
        {
            foreach (var player in humans)
            {
                if (!IsPlayerValid(player) || !player.UserId.HasValue || !gear.TryGetValue(player.UserId.Value, out var kept)) continue;
                var pawn = player.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid || pawn.Health <= 0) continue;
                player.RemoveWeapons();
                foreach (var weapon in kept.Weapons) player.GiveNamedItem(weapon);
                if (kept.Armor > 0) player.GiveNamedItem(kept.Helmet ? "item_assaultsuit" : "item_kevlar");
                if (kept.Defuser && player.TeamNum == (byte)CsTeam.CounterTerrorist) player.GiveNamedItem("item_defuser");
                if (player.InGameMoneyServices != null)
                {
                    player.InGameMoneyServices.Account = kept.Money;
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
                }
            }
        });
    }
}
