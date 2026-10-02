> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/COMMUNITY_LEGACYX_CHANGELOG.md](https://github.com/userneon/legacyxxx-plugins/blob/main/COMMUNITY_LEGACYX_CHANGELOG.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Community EXP & Clan Customization

## What changed

The plugins repository now contains a first-party `community/` CounterStrikeSharp module. It is branded for LEGACY-X and provides `css_xp`, `css_level`, `css_progress` and `css_clan`. The plugin reads a server-authorized profile only; it never calculates XP, modifies ranks, or creates clans locally.

## Why this boundary matters

MatchZy sends the one authoritative completed map result. AdminPlus validates the plugin identity and backend payload rules. Supabase processes XP, level, rank and clan score idempotently. This prevents a duplicated gameplay plugin, accidental EXP farming, local server database drift and the need to place staff credentials on a CS2 server.

## Stack ownership

| Module | LEGACY-X customized behavior |
|---|---|
| MatchZy | Exact 5v5/all-ready gate, non-repeat map flow and final map result event. |
| AdminPlus | Frontendless RCON/admin API and secure game-server event ingress. |
| AFK Manager | MatchZy warmup-safe AFK policy; never owns map or match state. |
| Community | Player-facing EXP/level/rank/clan lookup, LEGACY-X chat branding, plugin-secret-only profile access. |

## Validation

`LegacyXCommunity.dll` builds on .NET 8 with CounterStrikeSharp API 1.0.342 and no warnings/errors. Production still requires migration application plus a real completed 5v5 map to validate database rows and the in-game profile response.
