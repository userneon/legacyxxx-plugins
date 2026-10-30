using System.Diagnostics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;

namespace LegacyXCommunity;

/// <summary>
/// Is the server itself slow? (owner report 2026-10-30: "slow motion in the middle of a match for some players"). Every 5 seconds the game clock
/// is compared with the real clock: with a healthy server game time moves as fast as real time (1.00). A ratio clearly below 1 means the
/// server's ticks were late (a busy or shared CPU, disk or demo writes), and then everyone feels slow motion. When the ratio stays
/// near 1 while a player reports slow motion, the cause is on that player's side (their packet loss, ping, FPS or rate settings).
/// Only lines for a slow stretch and a quiet line every 10 minutes are written, so the log stays small.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private readonly Stopwatch lagClock = Stopwatch.StartNew();
    private double lagLastReal;
    private float lagLastGame;
    private int lagQuietChecks;

    private void RegisterLagMonitor()
    {
        lagLastReal = lagClock.Elapsed.TotalSeconds;
        lagLastGame = Server.CurrentTime;
        AddTimer(5f, CheckServerSpeed, TimerFlags.REPEAT);
        RegisterListener<Listeners.OnMapStart>(_ => { lagLastReal = lagClock.Elapsed.TotalSeconds; lagLastGame = Server.CurrentTime; });
    }

    private void CheckServerSpeed()
    {
        var real = lagClock.Elapsed.TotalSeconds;
        var game = Server.CurrentTime;
        var realStep = real - lagLastReal;
        var gameStep = game - lagLastGame;
        lagLastReal = real; lagLastGame = game;
        // A map change, a pause or a restart moves the game clock on its own: only a normal step is judged.
        if (realStep < 3 || gameStep < 0 || gameStep > realStep * 1.5) return;
        var ratio = gameStep / realStep;
        string Setting(string name) { try { var convar = ConVar.Find(name); return convar == null ? "?" : convar.StringValue; } catch { return "?"; } }
        var settings = $"host_timescale {Setting("host_timescale")}, sv_cheats {Setting("sv_cheats")}";
        var humans = Utilities.GetPlayers().Count(player => player.IsValid && !player.IsBot && !player.IsHLTV);
        if (ratio < 0.92)
        {
            lagQuietChecks = 0;
            Console.WriteLine($"[{ModuleName}] SERVER SLOW: game time ran at {ratio:0.00}x of real time over {realStep:0.0} s ({humans} players; {settings}). Check CPU steal (top: st), disk and demo writes.");
        }
        else if (++lagQuietChecks >= 120)
        {
            lagQuietChecks = 0;
            Console.WriteLine($"[{ModuleName}] Server speed is normal ({ratio:0.00}x, {humans} players; {settings}).");
        }
    }
}
