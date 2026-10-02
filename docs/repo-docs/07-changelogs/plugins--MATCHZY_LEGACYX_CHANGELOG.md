> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/MATCHZY_LEGACYX_CHANGELOG.md](https://github.com/userneon/legacyxxx-plugins/blob/main/MATCHZY_LEGACYX_CHANGELOG.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

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
