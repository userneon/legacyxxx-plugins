# LegacyX Phantom

## Safe implementation boundary

LegacyX Phantom is a **server-only virtual honeypot**. It does not create a fake `CCSPlayerController`, bot, pawn, hitbox, `prop_dynamic`, or any other networked client entity. Every Phantom is an in-memory trajectory associated with one connected real-player SteamID64, and is discarded on disconnect.

This is intentional. CounterStrikeSharp supports server-side events, timers, schema access, and basic entity manipulation, but a networked entity cannot be reliably hidden from ESP while still being sent to the client. A fake player/bot can also affect slot counts, scoreboards, round logic, collision, or hit registration. LegacyX Phantom therefore rejects that unsafe design rather than pretending it is production-safe.

| Requirement | Supported behavior |
|---|---|
| One Phantom per real player | Yes, virtual in-memory mapping up to the configured cap |
| Invisible/non-interactive | Yes, because no game or client entity exists |
| Collision, damage, score, K/D, round safety | Yes, impossible for a virtual record to affect gameplay |
| Historical replay | Not yet available: existing telemetry has no position/rotation samples |
| Fallback movement | Independent procedural trajectory; never reads another active player's position/input |
| Aim/shot evidence | Supported as low-confidence server-side correlation evidence |
| Wall-ray, ESP visibility proof, fake-player interaction | Not supported with CounterStrikeSharp alone; no claim is made |
| Automatic ban | Never implemented |

## Evidence model

The plugin records only repeated high-alignment weapon-fire events. Each evidence record includes match/server identifiers, subject SteamID64, Phantom ID/mapping, both sampled positions, round, tick, correlation scores, confidence, and aggregate suspicion score. Root API ingestion is scoped to `legacyx-phantom` with `phantom:write`, and the database enforces `(plugin_id,event_id)` idempotency. Owner-only Staff Panel API review is available at `/staffpanel/anti-cheat/phantom-evidence` after the deferred migration is applied.

The signal is evidence for staff review only. It does not punish a player, change their state, or call any ban action. A single aligned shot is deliberately weak; repeated events increase the score slowly and remain contextual.

## Required server-local configuration

```dotenv
LEGACYX_PHANTOM_ENABLED=true
LEGACYX_PHANTOM_COUNT_MODE=per_player
LEGACYX_PHANTOM_MOVEMENT_SOURCE=historical_round
LEGACYX_PHANTOM_INTERACTION_TELEMETRY=true
LEGACYX_PHANTOM_MOVEMENT_DELAY_MS=250
LEGACYX_PHANTOM_MAX_COUNT=64
LEGACYX_PHANTOM_TELEMETRY_BATCH_SECONDS=5
LEGACYX_PHANTOM_PLUGIN_ID=legacyx-phantom
LEGACYX_PHANTOM_PLUGIN_TOKEN=SERVER_LOCAL_PHANTOM_WRITE_TOKEN
```

The real secret belongs only in the CS2 host `.env`, never in a plugin config committed to Git, the browser, or the frontend.

## Controlled test plan

After Supabase MCP is restored and authorization is given: apply `legacy_x_phantom_evidence.sql`; provision `legacyx-phantom` with `phantom:write`; deploy the DLL; start a controlled test server; connect 5 then 10 test players; verify matching virtual mapping count in the server log; disconnect/reconnect players and verify mapping cleanup/recreation; fire only in controlled target-direction scenarios; verify idempotent evidence ingest and Owner review; confirm no entity appears on scoreboards and no round, hit-registration, collision, damage, K/D, audio, or movement change occurs. Test Community and competitive modes separately. Keep `LEGACYX_PHANTOM_ENABLED=false` as the explicit rollback.

## Sources and limitations

CounterStrikeSharp documents a server-side framework with commands, game events, timers, listeners, schema access, and basic entity manipulation.[1] Its API exposes entity creation and network-state functions, which means entity-based decoys can be networked to clients.[2] A documented entity-rendering issue reports that alpha-zero models may not update visually as expected, reinforcing that alpha is not a reliable stealth/security boundary.[3] Collision groups exist, but collision suppression alone does not solve the fake-player/networking and competitive-integrity risks.[4]

[1]: https://github.com/roflmuffin/CounterStrikeSharp
[2]: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Utilities.cs
[3]: https://github.com/roflmuffin/CounterStrikeSharp/issues/765
[4]: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Entities/Constants/CollisionGroup.cs
