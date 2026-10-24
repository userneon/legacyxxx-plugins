using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXCommunity;

/// <summary>
/// Third person view for Fun servers (owner request 2026-10-24): .tp turns it on and off. A camera (point_viewcontrol) is handed to the
/// player and moved behind their head every tick, so no cheats are switched on. Off unless LEGACYX_COMMUNITY_THIRDPERSON is true for
/// that port (LEGACYX_27016_COMMUNITY_THIRDPERSON=true on a Fun server): behind-the-wall views would be an advantage in a ranked match.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private const float ThirdPersonDistance = 110f;
    private const float ThirdPersonEyeHeight = 64f;

    // player slot -> the camera they look through
    private readonly Dictionary<int, CBaseEntity> thirdPersonCameras = new();

    private void RegisterThirdPerson()
    {
        RegisterListener<Listeners.OnTick>(OnThirdPersonTick);
        // The cameras are entities: they go with the map, and a new round puts everyone on a new pawn.
        RegisterListener<Listeners.OnMapStart>(_ => thirdPersonCameras.Clear());
        RegisterEventHandler<EventRoundStart>((_, _) => { EndAllThirdPerson(); return HookResult.Continue; });
        RegisterEventHandler<EventPlayerDeath>((@event, _) => { if (@event.Userid is { } dead) EndThirdPerson(dead); return HookResult.Continue; });
        RegisterEventHandler<EventPlayerDisconnect>((@event, _) => { if (@event.Userid is { } left) EndThirdPerson(left); return HookResult.Continue; });
    }

    [ConsoleCommand("css_tp", "Fun servers: third person view on or off")]
    [ConsoleCommand("css_thirdperson", "Fun servers: third person view on or off")]
    public void OnThirdPerson(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid || player.IsBot) return;
        if (!Config.ThirdPerson)
        {
            Print(player, "Third person is only on Fun servers.");
            return;
        }
        if (thirdPersonCameras.ContainsKey(player.Slot))
        {
            EndThirdPerson(player);
            Print(player, "Third person off.");
            return;
        }
        Print(player, StartThirdPerson(player) ? "Third person on. Type {white}.tp{grey} again for the normal view." : "You can use third person while you are alive.");
    }

    private bool StartThirdPerson(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) return false;
        try
        {
            var camera = Utilities.CreateEntityByName<CBaseEntity>("point_viewcontrol");
            if (camera == null || !camera.IsValid) return false;
            camera.DispatchSpawn();
            PlaceThirdPersonCamera(camera, pawn);
            camera.AcceptInput("Enable", player, camera);
            thirdPersonCameras[player.Slot] = camera;
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Third person could not start: {exception.Message}");
            return false;
        }
    }

    private void EndThirdPerson(CCSPlayerController player)
    {
        if (!thirdPersonCameras.Remove(player.Slot, out var camera)) return;
        try
        {
            if (camera.IsValid)
            {
                camera.AcceptInput("Disable", player, camera);
                camera.Remove();
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Third person could not stop: {exception.Message}");
        }
    }

    private void EndAllThirdPerson()
    {
        foreach (var player in Utilities.GetPlayers().Where(player => thirdPersonCameras.ContainsKey(player.Slot))) EndThirdPerson(player);
        thirdPersonCameras.Clear();
    }

    private void OnThirdPersonTick()
    {
        if (thirdPersonCameras.Count == 0) return;
        foreach (var player in Utilities.GetPlayers().Where(player => thirdPersonCameras.ContainsKey(player.Slot)).ToList())
        {
            var pawn = player.PlayerPawn.Value;
            if (!player.IsValid || pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE || !thirdPersonCameras[player.Slot].IsValid)
            {
                EndThirdPerson(player);
                continue;
            }
            PlaceThirdPersonCamera(thirdPersonCameras[player.Slot], pawn);
        }
    }

    /// <summary>The camera sits behind the player's head on the line they are looking along, and looks the same way.</summary>
    private static void PlaceThirdPersonCamera(CBaseEntity camera, CCSPlayerPawn pawn)
    {
        var origin = pawn.AbsOrigin;
        if (origin == null) return;
        var angles = pawn.EyeAngles;
        var pitch = angles.X * MathF.PI / 180f;
        var yaw = angles.Y * MathF.PI / 180f;
        var forward = new Vector(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Cos(pitch) * MathF.Sin(yaw), -MathF.Sin(pitch));
        var position = new Vector(
            origin.X - forward.X * ThirdPersonDistance,
            origin.Y - forward.Y * ThirdPersonDistance,
            origin.Z + ThirdPersonEyeHeight - forward.Z * ThirdPersonDistance);
        camera.Teleport(position, new QAngle(angles.X, angles.Y, 0f), new Vector(0f, 0f, 0f));
    }
}
