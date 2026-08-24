# Source Map

Phase A preserves existing module paths to avoid gameplay behavior changes and fragile mass moves. This directory records the target source ownership for the next compatibility-preserving migration.

| Target source area | Existing Phase A module path |
|---|---|
| `src/MatchZy/` | `matchzy/` |
| `src/AFKManager/` | `afkmanager/` |
| `src/AdminPlus/` | `adminplus/plugin/AdminPlus/` |
| `src/Community/` | `community/` |
| `src/Reconnect/` | `reconnect/` |
| `src/SpectatorComms/` | `spectator-comms/` |
| `src/SkinBridge/` | `weaponpaints-legacyx/` |

No source is moved in Phase A. The map makes the intended source-versus-runtime boundary explicit without changing CounterStrikeSharp load paths.
