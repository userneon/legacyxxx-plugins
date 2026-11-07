# LEGACY-X MatchZy Customization Report

## Scope

LEGACY-X нь upstream MatchZy-ийг тусдаа `matchzy/` module болгон оруулж, existing AdminPlus command bridge-ээс match lifecycle-ийг салгасан. MatchZy нь competitive match state, ready gate, demo/stat flow болон map transition-ийг эзэмшинэ. AdminPlus нь dashboard-аас ирэх player/server/admin action-ийг эзэмшинэ.

## Upstream baseline

Base project: [shobhit-pathak/MatchZy](https://github.com/shobhit-pathak/MatchZy). The fork keeps the upstream source, language files, spawns, configs and technical documentation in `matchzy/`. Upstream README is preserved as `matchzy/README.upstream.md`.

## LEGACY-X changes

| Area | Change | Reason |
|---|---|---|
| Branding | Module name/version/author/description and chat prefix changed to LEGACY-X | Community identity and clear server logs |
| Ready gate | Strict mode requires exactly 5 CT + 5 T; every active player must be ready | Prevent 6v5, 5v6 and 6v6 starts |
| Match JSON | `players_per_team` and `min_players_to_ready` are forced to 5 in strict mode | Match files cannot weaken server policy |
| Force ready | Disabled in production preset | Every active player must explicitly ready |
| Post-match flow | Result/stat/demo persistence remains upstream; then `PLEASE WAIT`, clean reset and soft map change | Prevent previous match state/bug leakage without hard restart |
| Map rotation | Configurable installed map pool; current map excluded from next random choice | Avoid immediate same-map repetition |
| Configuration | Added `legacyx_exact_5v5`, `legacyx_random_map_after_match`, `legacyx_map_pool`, `legacyx_map_transition_delay` | Production operators can control policy without recompiling |
| Repository structure | `matchzy/` owns MatchZy; `adminplus/` owns AdminPlus only | Avoid duplicate EventCsWinPanelMatch handlers |

## Match lifecycle

```text
Load match config
→ force exact 5v5 policy
→ warmup and ready lobby
→ all ten active players ready
→ match start
→ round/match result + stats/demo persistence
→ PLEASE WAIT
→ ResetMatch(false)
→ random installed map excluding current map
→ new warmup and ready lobby
```

The plugin uses CS2 `changelevel`, not a hard process restart. The map loading transition provides the black/loading state on clients while the server process remains alive.

## Production configuration

```text
legacyx_exact_5v5 true
legacyx_random_map_after_match true
legacyx_map_pool "de_ancient,de_anubis,de_dust2,de_inferno,de_mirage,de_nuke,de_overpass,de_vertigo"
legacyx_map_transition_delay 3.0
matchzy_minimum_ready_required 5
matchzy_allow_force_ready false
```

Only maps installed on the CS2 server should be listed. Keep at least two valid maps in the pool so “random different map” has a real choice.

## Validation

The customized MatchZy Release build and the separate AdminPlus Release build must both pass before deployment. A live CS2 server test is still required to validate the exact event timing, server-side map availability, installed configuration path, demo flush timing and the behavior of the selected game mode.

## Known limitations

The plugin cannot force a client-side keybind silently. If the community wants Enter-to-ready, publish `bind ENTER css_ready` as an optional user instruction. MatchZy does not own the web API or backend persistence; those remain in `legacyxxx-backend`. Do not install an additional AdminPlus match-end lifecycle handler beside this MatchZy customization.

## Final local validation

On 2026-08-20, both modules built successfully with .NET 8 Release configuration. MatchZy completed with 0 warnings and 0 errors; AdminPlus completed with 0 warnings and 0 errors. `git diff --check` passed and the repository secret scan found no candidate secret values outside documentation. Build outputs remain ignored and are supplied separately only as deployment artifacts.

## Opening round games (2026-11-07)

The round before a match that decides the sides is no longer always the knife (owner request: "make the knife round not only the knife but a fun game, random"). `LegacyXKnifeFun.cs` picks one at random each time: Knife fight, AWP only, Deagle only, Scout on the moon, Knives on the moon (gravity 300) and Pistol duel. It sets `mp_t/ct_default_primary` and `_secondary` and `sv_gravity` right after the knife config and before the restart, names the game in chat, and puts weapons and gravity back before the side vote (and when a match is cancelled or goes live early). The round is still won by the team left standing, so the winner picks the side as before. `matchzy_knife_fun_rounds 0` brings the plain knife round back. Add a game by adding a line to `KnifeFunGames`.

## !up (2026-11-07)

While the match is paused, `!up` now works next to `.up` and `.unpause` (owner request: "one player from each side writes up, not everyone"). It was only registered as a `.up` chat word, so `!up` did nothing. One player of each team is enough to resume; a team with nobody on the server is not waited for.

## !fup in the Roles list (2026-11-07)

`fup` (force a paused match to resume) is now a command on Staff → Roles with its own permission, `@css/pause`; MatchZy accepts it next to `@css/config` and the Owner. Admin and Manager have it by default, as before for the Manager. Tick it on any other role to let that role use `!fup`.

## Deploy no longer verifies the game (2026-11-07)

`cs2-host.sh deploy` and `update` leave the CS2 game files alone (owner request: "why does it verify the install every time"): they used to run `app_update 730 validate`, a check of all 70 GB. `--cs2` fetches a new build only, `--verify` checks every file, and CS2 is still installed when `gameinfo.gi` is missing. The automatic updater passes `--cs2` when Steam has a newer build.

## !kickbots and !chatlock (2026-11-07)

`!kickbots` removes every bot at once (needs `@css/kick`); `!chatlock` lets only staff write in chat until it is run again or the map changes (needs `@css/chat`). Both are on Staff → Roles, Admin and above have them by default.

## Checker: hidden files (2026-11-07)

The scan now looks harder where a program may be hidden (owner request): the Recycle Bin is read (a thrown-away program is still on the disk), a file with the Hidden or System attribute is noted, and a Windows program carrying another file's name (no extension, .tmp .dat .bin .txt .log .db .cfg .ini .png .jpg, or any name when hidden; only files that start with "MZ" are opened) is analysed like an .exe. The facts carry `hidden`, `disguised` and `deleted`; the server adds 1, 2 and 1 points (Checker rules `points`). Deploy the API first: it refuses unknown fields. Not covered yet: NTFS alternate data streams.

## !check <name> (2026-11-07)

A staff member (Admin, Manager or Owner, signed in on the website) types `!check <name>` in game: the API makes the one-time Checker code (`POST /api/v1/plugin/checks`, scope `bans:write`) and the game server shows the download link and the code to the asked player alone, in chat and console. The staff member only hears that it was shown. A player who already has an open check is refused. On Staff → Roles it is the command `check` (Bans). Deploy the API first.
