using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System.Collections.Generic;

namespace MatchZy;

/// <summary>
/// Warmup shopping rules (owner request 2026-10-17): money never runs out, no Zeus, armor or defuse kit; flashbangs are blocked
/// with ammo_grenade_limit_flashbang 0 and Zeus with mp_weapons_allow_zeus 0 in warmup.cfg. The game has no cvar for armor and
/// the kit, so a purchase of them is taken back here. Only active while the match is in warmup.
/// </summary>
public partial class MatchZy
{
    private const int WarmupMoney = 16000;
    /// <summary>A gun on the ground in warmup is removed this many seconds after it was dropped (owner request 2026-10-18).</summary>
    private const double WarmupDropLifetimeSeconds = 3;
    // Guns lying on the map when warmup started (map-placed ones): never removed. Dropped guns: when they were first seen unowned.
    private readonly HashSet<uint> warmupMapWeapons = new();
    private readonly Dictionary<uint, DateTime> warmupDropped = new();
    private bool warmupSweepWasWarmup;

    private void RegisterWarmupDropCleanup() => AddTimer(1.0f, WarmupDropSweep, TimerFlags.REPEAT);

    /// <summary>
    /// Warmup only: people hold ctrl and buy over and over, and every purchase drops the gun they had, so the floor fills with guns.
    /// Every second the unowned guns are looked at; one that has lain there for 3 seconds is removed. Guns the map placed are left
    /// alone, and nothing is touched outside warmup (in a match a dropped gun is for a teammate).
    /// </summary>
    private void WarmupDropSweep()
    {
        if (!isWarmup || isPractice)
        {
            if (warmupSweepWasWarmup)
            {
                warmupMapWeapons.Clear();
                warmupDropped.Clear();
                warmupSweepWasWarmup = false;
            }
            return;
        }
        var firstSweep = !warmupSweepWasWarmup;
        warmupSweepWasWarmup = true;
        var now = DateTime.UtcNow;
        var seen = new HashSet<uint>();
        foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CBasePlayerWeapon>("weapon_"))
        {
            if (weapon == null || !weapon.IsValid) continue;
            if (weapon.OwnerEntity.IsValid) continue;   // someone carries it
            var index = weapon.Index;
            seen.Add(index);
            if (firstSweep) { warmupMapWeapons.Add(index); continue; }
            if (warmupMapWeapons.Contains(index)) continue;
            if (!warmupDropped.TryGetValue(index, out var since)) { warmupDropped[index] = now; continue; }
            if ((now - since).TotalSeconds < WarmupDropLifetimeSeconds) continue;
            weapon.Remove();
            warmupDropped.Remove(index);
        }
        // A gun that was picked up or is gone is forgotten, so a new one with the same number starts its 3 seconds again.
        foreach (var index in new List<uint>(warmupDropped.Keys)) if (!seen.Contains(index)) warmupDropped.Remove(index);
        warmupMapWeapons.RemoveWhere(index => !seen.Contains(index));
    }

    private HookResult EventWarmupItemPurchase(EventItemPurchase @event, GameEventInfo info)
    {
        if (!isWarmup || isPractice) return HookResult.Continue;
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var pawn = player.PlayerPawn.Value;
        var weapon = @event.Weapon ?? "";
        if (pawn != null && pawn.IsValid)
        {
            if (weapon is "item_kevlar" or "item_assaultsuit")
            {
                pawn.ArmorValue = 0;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_ArmorValue");
                if (pawn.ItemServices != null)
                {
                    new CCSPlayer_ItemServices(pawn.ItemServices.Handle).HasHelmet = false;
                    Utilities.SetStateChanged(pawn, "CBasePlayerPawn", "m_pItemServices");
                }
            }
            else if (weapon == "item_defuser" && pawn.ItemServices != null)
            {
                new CCSPlayer_ItemServices(pawn.ItemServices.Handle).HasDefuser = false;
                Utilities.SetStateChanged(pawn, "CBasePlayerPawn", "m_pItemServices");
            }
        }
        // Money never runs out in warmup: every purchase fills it up again.
        var money = player.InGameMoneyServices;
        if (money != null)
        {
            money.Account = WarmupMoney;
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
        }
        return HookResult.Continue;
    }
}
