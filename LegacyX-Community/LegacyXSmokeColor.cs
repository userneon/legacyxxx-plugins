using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

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
    // The colour each smoke in flight was given (by the projectile's entity index), so its glow matches it where it bursts.
    private readonly Dictionary<uint, (float R, float G, float B)> smokeShades = new();
    private const float SmokeGlowSeconds = 17f;
    // When each player last had their colour read again by !rs: at most once every few seconds, so !rs spam is not API load.
    private readonly Dictionary<ulong, DateTime> smokeRefreshAt = new();

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
                smokeShades[projectile.Index] = shade;
            });
        });
        // A coloured light where the smoke bursts, in the same colour, for as long as the cloud lasts (experiment, 2026-10-24).
        RegisterEventHandler<EventSmokegrenadeDetonate>((@event, _) =>
        {
            if (smokeShades.Remove((uint)@event.Entityid, out var shade) && Config.SmokeGlow) SpawnSmokeGlow(@event.X, @event.Y, @event.Z, shade);
            return HookResult.Continue;
        });
        RegisterListener<Listeners.OnMapStart>(_ => { smokeShades.Clear(); smokesThrown.Clear(); });
        // !rs reloads skins from the site; the smoke colour is read again with it, so a colour changed on the site shows at once.
        AddCommandListener("css_rs", (player, _) =>
        {
            if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;
            var now = DateTime.UtcNow;
            if (smokeRefreshAt.TryGetValue(player.SteamID, out var last) && now - last < TimeSpan.FromSeconds(5)) return HookResult.Continue;
            smokeRefreshAt[player.SteamID] = now;
            _ = LoadScoreboardRankAsync(player);
            return HookResult.Continue;
        }, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
        {
            if (@event.Userid is { } left) { smokeRefreshAt.Remove(left.SteamID); smokeColors.Remove(left.SteamID); smokesThrown.Remove(left.SteamID); }
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

    private void SpawnSmokeGlow(float x, float y, float z, (float R, float G, float B) shade)
    {
        try
        {
            var light = Utilities.CreateEntityByName<CBarnLight>("light_omni2");
            if (light == null || !light.IsValid) return;
            light.Enabled = true;
            light.Color = System.Drawing.Color.FromArgb(255, (int)shade.R, (int)shade.G, (int)shade.B);
            light.Brightness = 6f;
            light.Range = 360f;
            light.Teleport(new Vector(x, y, z + 40f), new QAngle(0, 0, 0), new Vector(0, 0, 0));
            light.DispatchSpawn();
            AddTimer(SmokeGlowSeconds, () => { if (light.IsValid) light.Remove(); });
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Smoke glow failed: {exception.Message}");
        }
    }

    [ConsoleCommand("lx_smoke_glow", "Coloured light where a coloured smoke bursts: lx_smoke_glow 0|1")]
    public void OnSmokeGlowCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null) return; // server console / RCON only
        if (command.ArgCount > 1 && command.ArgByIndex(1) is "0" or "1") Config.SmokeGlow = command.ArgByIndex(1) == "1";
        command.ReplyToCommand($"[{ModuleName}] lx_smoke_glow is {(Config.SmokeGlow ? 1 : 0)}.");
    }
}
