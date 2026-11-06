# LegacyX-AntiWallhack

The game server decides who a player may know about. An enemy who is **fully behind solid cover** is not sent to that player's game at all
(their player entity and weapons are taken out of what the server transmits, `CheckTransmit`), so a wallhack has nothing to draw. Written by us
(owner request 2026-11-06); the idea is the one of other anti-wallhack plugins, none of their code is used.

## How it decides

Every `UPDATE_TICKS` ticks (2 by default, about 32 times a second) and for every living player against every living enemy:

1. **Close enemy** (under `MIN_DISTANCE`, 140 units): always sent.
2. **Line of sight.** Line traces from the viewer's eye to 11 points on and around the enemy: three on the body (head, chest, feet) and the four corners of a box
   `PADDING` units (28) wider than the player, at two heights. One clear line is enough to show the enemy. The padding is why an enemy appears a moment
   *before* they step into view: the server is a few ticks behind the viewer's screen and a player who peeks must already be there.
3. **A moment ahead.** The same again with the viewer and the enemy moved on by their speed for `PREDICT_MS` (100 ms), which shows a peek before it happens.
4. **Smoke.** A line through a smoke ball (`SMOKE_RADIUS`, 125 units, from 1.2 s after it pops until it ends) does not count as clear.
5. **Grace.** An enemy who was seen is kept for `GRACE_TICKS` (20) ticks after the last clear line, so one who ducks back behind a corner does not flicker.

Only **world solids** block the eye: glass, things bullets pass (fences, grates), clip brushes and triggers do not (`InteractsExclude`). Everything not
listed here is not a reason to hide anyone.

## It fails open

Whenever it is not sure, the enemy is sent as usual: no tracer, a trace that does not answer, more than `TRACE_BUDGET` traces (900) in one update, warmup,
a dead viewer (spectators see everything), bots and GOTV as viewers, an error anywhere (everything is shown until the next good update). The round start
and a map change start over with everyone shown.

## Install

1. **The Ray-Trace module** on each game server (github.com/FUNPLAY-pro-CS2/Ray-Trace, GPL-3.0, a separate install): `RayTrace.vdf` and its native library in
   `addons/metamod`, `RayTraceImpl.dll` in `addons/counterstrikesharp/plugins/RayTraceImpl/` and `RayTraceApi.dll` in `addons/counterstrikesharp/shared/RayTraceApi/`.
2. **Build this plugin with the API dll:** `scripts/build-raytrace.sh` makes `libs/RayTraceApi.dll` (needs the .NET 10 SDK), then `scripts/build-all.sh`.
   Without that file the plugin still builds, as one that says it is off.
3. **Switch it on for one server** in the central `.env`: `LEGACYX_27015_ANTIWALLHACK_ENABLED=true`. It is off by default and runs only on the modes in
   `LEGACYX_ANTIWALLHACK_MODES` (`competitive_5v5,proleague`; never Fun, where the owner's wallhack toy needs to see through walls).
4. `lx_awh_status` (server console) shows updates, traces, how often the budget was reached and how many enemies are hidden from someone now.
   `LEGACYX_ANTIWALLHACK_DEBUG=true` writes a line every ten seconds.

## Try it on one server first

The layer names the trace uses (`Solid` with `Window`, `PassBullets`, `PlayerClip`, `Trigger` left out) follow the module's own list and have not been
checked against every map. On a test server: play a map, watch a teammate walk behind a wall and a fence and a window, and check nobody is hidden
who is plainly in view. If a kind of surface hides someone it should not, tell us which map and where.

## Known limits

- A hidden enemy is also missing from the viewer's radar and is not drawn, but **sounds still come from the server** (footsteps, shots).
- It cannot stop a cheat that reads other things (sounds, a bomb carrier's position on the scoreboard, aimbots). It only closes the wallhack.
- Tests of the rules (no game needed): `tests/LegacyX.Admin.Authorization.Tests/AntiWallhackTests.cs`.
