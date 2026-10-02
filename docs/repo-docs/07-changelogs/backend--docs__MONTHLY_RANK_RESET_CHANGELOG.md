> **Хуулбар.** Эх сурвалж: [legacyxxx-backend/docs/MONTHLY_RANK_RESET_CHANGELOG.md](https://github.com/userneon/legacyxxx-backend/blob/main/docs/MONTHLY_RANK_RESET_CHANGELOG.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Monthly Rank Reset Changelog

The rank system now supports an automatic monthly UTC season rollover. The new SQL migration adds a unique monthly rollover record, season archive snapshots and an idempotent `rollover_monthly_rank_season` RPC. The API-only AdminPlus backend starts an immediate catch-up check on boot and repeats it hourly. It also exposes staff-only current-season and tightly disabled manual rollover endpoints.

The game server is not responsible for scheduling. MatchZy continues to send final map events; backend processing binds each valid event to the active database season. This avoids mismatches if a server keeps running across the month boundary.
