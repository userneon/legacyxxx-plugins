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
    // SteamID64 -> RGB 0-255 and, for a two-colour (gradient) smoke, the second RGB; players with no colour worn are not in it.
    private readonly Dictionary<ulong, ((float R, float G, float B) First, (float R, float G, float B)? Second)> smokeColors = new();
    // Smokes thrown so far by each player on this map: a two-colour smoke alternates, the first grenade in the first colour, the next in the second.
    private readonly Dictionary<ulong, int> smokesThrown = new();

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
                var count = smokesThrown.GetValueOrDefault(controller.SteamID);
                smokesThrown[controller.SteamID] = count + 1;
                var shade = color.Second is { } second && count % 2 == 1 ? second : color.First;
                projectile.SmokeColor.X = shade.R;
                projectile.SmokeColor.Y = shade.G;
                projectile.SmokeColor.Z = shade.B;
            });
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) { smokeColors.Remove(left.SteamID); smokesThrown.Remove(left.SteamID); }
            return HookResult.Continue;
        });
    }

    private static (float R, float G, float B)? ParseSmokeColor(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
            : null;

    /// <summary>Keeps (or drops) a player's worn smoke colour and its optional second colour; only a plain #rrggbb counts. Main thread.</summary>
    private void StoreSmokeColor(ulong steamId, string? hex, string? hex2 = null)
    {
        if (ParseSmokeColor(hex) is { } first)
        {
            smokeColors[steamId] = (first, ParseSmokeColor(hex2));
            return;
        }
        smokeColors.Remove(steamId);
    }
}
