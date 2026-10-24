using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace LegacyXCommunity;

/// <summary>
/// The Smoke colour cosmetic (owner request 2026-10-24): a player's own smoke grenades bloom in the colour they bought and wear on
/// legacyx.cc (Shop → Smoke). The colour comes with the player's profile (<c>smoke_color</c>, a #rrggbb), read when they join and
/// after every finished match, so a change on the site shows up on the next map or match. Nothing else about the grenade changes.
/// </summary>
public sealed partial class LegacyXCommunity
{
    // SteamID64 -> RGB 0-255; players with no colour worn are not in it.
    private readonly Dictionary<ulong, (float R, float G, float B)> smokeColors = new();

    private void RegisterSmokeColor()
    {
        RegisterListener<Listeners.OnEntitySpawned>(entity =>
        {
            if (entity.DesignerName != "smokegrenade_projectile") return;
            var projectile = new CSmokeGrenadeProjectile(entity.Handle);
            // The thrower is filled in a moment after the grenade exists.
            Server.NextFrame(() =>
            {
                if (!projectile.IsValid) return;
                var thrower = projectile.Thrower.Value;
                if (thrower == null || !thrower.IsValid) return;
                var controller = new CCSPlayerPawn(thrower.Handle).Controller.Value;
                if (controller == null || !controller.IsValid) return;
                if (!smokeColors.TryGetValue(controller.SteamID, out var color)) return;
                projectile.SmokeColor.X = color.R;
                projectile.SmokeColor.Y = color.G;
                projectile.SmokeColor.Z = color.B;
            });
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) smokeColors.Remove(left.SteamID);
            return HookResult.Continue;
        });
    }

    /// <summary>Keeps (or drops) a player's worn smoke colour; only a plain #rrggbb counts. Main thread.</summary>
    private void StoreSmokeColor(ulong steamId, string? hex)
    {
        if (hex is { Length: 7 } && hex[0] == '#'
            && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            smokeColors[steamId] = ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return;
        }
        smokeColors.Remove(steamId);
    }
}
