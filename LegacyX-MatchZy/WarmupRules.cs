using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace MatchZy;

/// <summary>
/// Warmup shopping rules (owner request 2026-10-17): money never runs out, no Zeus, armor or defuse kit; flashbangs are blocked
/// with ammo_grenade_limit_flashbang 0 and Zeus with mp_weapons_allow_zeus 0 in warmup.cfg. The game has no cvar for armor and
/// the kit, so a purchase of them is taken back here. Only active while the match is in warmup.
/// </summary>
public partial class MatchZy
{
    private const int WarmupMoney = 16000;

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
