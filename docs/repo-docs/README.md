# Бүх repo-ийн баримт (ангилсан)

Таван repo-ийн README-ээс бусад бүх баримтыг (`.md`) ангилж цуглуулсан. **Хуулбар (snapshot)**: жинхэнэ хувилбар нь эх repo дээр.

Сүүлд цуглуулсан: 2026-10-02. README-ууд: [readmes/](../readmes/README.md). Нэгтгэсэн гарын авлага: [MANUAL_MN.md](../MANUAL_MN.md).

Оруулаагүй: Discord bot-ын `faq/*.md`, `rules/*.md` (эдгээр нь bot-ын ажиллах агуулга, баримт биш), CounterStrikeSharp-ийн гадны файл.

## Суулгах, deploy, ops

VPS, production deploy, зураг/asset түгээлт

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X API: VPS + Nginx + PM2 Deployment](01-deploy/backend--VPS_DEPLOY.md) | `backend/VPS_DEPLOY.md` | 132 |
| [LEGACY-X Backend Production Deployment](01-deploy/backend--docs__PRODUCTION_DEPLOYMENT.md) | `backend/docs/PRODUCTION_DEPLOYMENT.md` | 105 |
| [Legacy-X Image Delivery Runbook](01-deploy/frontend--docs__IMAGE_DELIVERY.md) | `frontend/docs/IMAGE_DELIVERY.md` | 35 |

## API, хамгаалалт, гэрээ

API лавлах, аюулгүй байдал, feature flag, plugin ↔ API гэрээ

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X REST API](02-api-security/backend--API.md) | `backend/API.md` | 83 |
| [LEGACY-X Root API Security Hardening](02-api-security/backend--docs__API_SECURITY_HARDENING.md) | `backend/docs/API_SECURITY_HARDENING.md` | 88 |
| [Deferred Feature Launch Flags](02-api-security/backend--docs__FEATURE_FLAGS.md) | `backend/docs/FEATURE_FLAGS.md` | 24 |
| [LEGACY-X Frontend Endpoint Adapter Specification](02-api-security/backend--docs__FRONTEND_ENDPOINT_ADAPTER_SPEC.md) | `backend/docs/FRONTEND_ENDPOINT_ADAPTER_SPEC.md` | 52 |
| [LEGACY-X Legacy Table RLS Hardening — 2026-08-24](02-api-security/backend--docs__LEGACY_RLS_HARDENING_2026-08-24.md) | `backend/docs/LEGACY_RLS_HARDENING_2026-08-24.md` | 35 |
| [LEGACY-X Plugin-Ready Contract v1](02-api-security/backend--docs__PLUGIN_READY_CONTRACT_V1.md) | `backend/docs/PLUGIN_READY_CONTRACT_V1.md` | 77 |

## Цол, EXP, match

Rank систем, сарын reset, leaderboard, match систем, telemetry

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X Community Progression & Clan Integration](03-rank-match/backend--docs__COMMUNITY_PROGRESSION_CLANS.md) | `backend/docs/COMMUNITY_PROGRESSION_CLANS.md` | 53 |
| [LEGACY-X leaderboard & rank integration](03-rank-match/backend--docs__LEADERBOARD_RANK_INTEGRATION.md) | `backend/docs/LEADERBOARD_RANK_INTEGRATION.md` | 51 |
| [LEGACY-X Monthly Rank Reset](03-rank-match/backend--docs__MONTHLY_RANK_RESET.md) | `backend/docs/MONTHLY_RANK_RESET.md` | 29 |
| [Ranked match telemetry — `competitive_result` v2](03-rank-match/backend--docs__PLUGIN_RANKED_TELEMETRY_V2.md) | `backend/docs/PLUGIN_RANKED_TELEMETRY_V2.md` | 101 |
| [Legacy-X rank system (v1.0) — implementation notes](03-rank-match/backend--docs__RANK_SYSTEM.md) | `backend/docs/RANK_SYSTEM.md` | 47 |
| [LEGACY-X Unified Match System Runbook](03-rank-match/backend--docs__UNIFIED_MATCH_SYSTEM_RUNBOOK.md) | `backend/docs/UNIFIED_MATCH_SYSTEM_RUNBOOK.md` | 108 |
| [LEGACY-X MatchZy Rank Bridge](03-rank-match/plugins--LegacyX-MatchZy__RANK_BRIDGE.md) | `plugins/LegacyX-MatchZy/RANK_BRIDGE.md` | 28 |

## Admin, staff, эрх

AdminPlus, staff panel, role шилжилт

| Баримт | Repo | Мөр |
|---|---|---|
| [AdminPlus API-only deployment](04-admin-staff/backend--docs__ADMINPLUS_API_ONLY.md) | `backend/docs/ADMINPLUS_API_ONLY.md` | 34 |
| [AdminPlus production setup](04-admin-staff/backend--docs__ADMINPLUS_PRODUCTION_SETUP.md) | `backend/docs/ADMINPLUS_PRODUCTION_SETUP.md` | 5 |
| [LEGACY-X Staff Panel](04-admin-staff/backend--docs__STAFF_PANEL.md) | `backend/docs/STAFF_PANEL.md` | 52 |

## Skin ба HUD

Skinchanger runbook, Workshop HUD гэрээ ба гарын авлага

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X Production Skinchanger Operator Runbook](05-skin-hud/backend--docs__SKINCHANGER_OPERATOR_RUNBOOK.md) | `backend/docs/SKINCHANGER_OPERATOR_RUNBOOK.md` | 55 |
| [Skinchanger Static Asset Hosting](05-skin-hud/backend--docs__SKINCHANGER_STATIC_ASSET_HOSTING.md) | `backend/docs/SKINCHANGER_STATIC_ASSET_HOSTING.md` | 33 |
| [Contract between the addon and the plugins](05-skin-hud/workshop--CONTRACT.md) | `workshop/CONTRACT.md` | 137 |

## Аудит, шалгалт

Тодорхой огнооны аудит, production шалгалт

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X цэвэрлэгээний аудит — 2026-09-20](06-audits/backend--AUDIT_2026-09-20.md) | `backend/AUDIT_2026-09-20.md` | 156 |
| [LEGACY-X Plugin ба Live Server Integration Gap Audit](06-audits/backend--docs__PLUGIN_GAP_AUDIT_2026-08-24.md) | `backend/docs/PLUGIN_GAP_AUDIT_2026-08-24.md` | 122 |
| [LEGACY-X Production Database Audit](06-audits/backend--docs__PRODUCTION_DB_AUDIT.md) | `backend/docs/PRODUCTION_DB_AUDIT.md` | 55 |
| [LEGACY-X 18-Rank EXP Audit](06-audits/backend--docs__RANK_EXP_AUDIT_2026-08-24.md) | `backend/docs/RANK_EXP_AUDIT_2026-08-24.md` | 44 |
| [LEGACY-X Role Migration and Security Audit](06-audits/backend--docs__ROLE_MIGRATION_AUDIT.md) | `backend/docs/ROLE_MIGRATION_AUDIT.md` | 37 |
| [Live Server Match Panel Audit — 2026-08-24](06-audits/backend--docs__SERVER_LIVE_MATCH_AUDIT_2026-08-24.md) | `backend/docs/SERVER_LIVE_MATCH_AUDIT_2026-08-24.md` | 27 |
| [LEGACY-X Unified Match System Refactor — Audit](06-audits/backend--docs__UNIFIED_MATCH_SYSTEM_AUDIT.md) | `backend/docs/UNIFIED_MATCH_SYSTEM_AUDIT.md` | 60 |
| [Users Role Migration Audit — 2026-08-24](06-audits/backend--docs__USER_ROLE_MIGRATION_AUDIT_2026-08-24.md) | `backend/docs/USER_ROLE_MIGRATION_AUDIT_2026-08-24.md` | 15 |
| [V1 cleanup audit (2026-09-24)](06-audits/backend--docs__V1_CLEANUP_AUDIT.md) | `backend/docs/V1_CLEANUP_AUDIT.md` | 60 |

## Changelog-ууд

Plugin, rank, reconnect-ийн өөрчлөлтийн түүх

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X AdminPlus — Repository Split Change Report](07-changelogs/backend--docs__ADMINPLUS_LEGACYX_CHANGELOG.md) | `backend/docs/ADMINPLUS_LEGACYX_CHANGELOG.md` | 63 |
| [LEGACY-X EXP, Level & Clan System Changelog](07-changelogs/backend--docs__COMMUNITY_PROGRESSION_CHANGELOG.md) | `backend/docs/COMMUNITY_PROGRESSION_CHANGELOG.md` | 30 |
| [LEGACY-X Monthly Rank Reset Changelog](07-changelogs/backend--docs__MONTHLY_RANK_RESET_CHANGELOG.md) | `backend/docs/MONTHLY_RANK_RESET_CHANGELOG.md` | 5 |
| [LEGACY-X AdminPlus API-only & Rank Integration Changelog](07-changelogs/backend--docs__RANK_ADMINPLUS_CHANGELOG.md) | `backend/docs/RANK_ADMINPLUS_CHANGELOG.md` | 69 |
| [LEGACY-X Reconnect Changelog](07-changelogs/backend--docs__RECONNECT_CHANGELOG.md) | `backend/docs/RECONNECT_CHANGELOG.md` | 3 |
| [LEGACY-X AFK Manager Customization Report](07-changelogs/plugins--AFKMANAGER_LEGACYX_CHANGELOG.md) | `plugins/AFKMANAGER_LEGACYX_CHANGELOG.md` | 56 |
| [LEGACY-X Community EXP & Clan Customization](07-changelogs/plugins--COMMUNITY_LEGACYX_CHANGELOG.md) | `plugins/COMMUNITY_LEGACYX_CHANGELOG.md` | 22 |
| [MatchZy Changelog](07-changelogs/plugins--LegacyX-MatchZy__CHANGELOG.md) | `plugins/LegacyX-MatchZy/CHANGELOG.md` | 461 |
| [LEGACY-X MatchZy Customization Report](07-changelogs/plugins--MATCHZY_LEGACYX_CHANGELOG.md) | `plugins/MATCHZY_LEGACYX_CHANGELOG.md` | 64 |
| [LEGACY-X Spectator Comms Changelog](07-changelogs/plugins--SPECTATOR_COMMS_LEGACYX_CHANGELOG.md) | `plugins/SPECTATOR_COMMS_LEGACYX_CHANGELOG.md` | 5 |

## Вэб дизайн ба ажлын дүрэм

Frontend-ийн дизайн, token, зураг, `CLAUDE.md`

| Баримт | Repo | Мөр |
|---|---|---|
| [LEGACY-X frontend — working rules](08-frontend-design/frontend--CLAUDE.md) | `frontend/CLAUDE.md` | 105 |
| [docs/design/PROMPT.md](08-frontend-design/frontend--docs__design__PROMPT.md) | `frontend/docs/design/PROMPT.md` | 645 |
| [Legacy-X rank system (v1.0)](08-frontend-design/frontend--docs__design__RANK-SYSTEM.md) | `frontend/docs/design/RANK-SYSTEM.md` | 165 |
| [Legacy-X design references](08-frontend-design/frontend--docs__design__README.md) | `frontend/docs/design/README.md` | 59 |

## Бусад

Context, todo, promotion code, reconnect, Steam background

| Баримт | Repo | Мөр |
|---|---|---|
| [MASTER_CONTEXT — LEGACY-X](09-other/backend--MASTER_CONTEXT.md) | `backend/MASTER_CONTEXT.md` | 74 |
| [LEGACY-X Promotion Codes](09-other/backend--docs__PROMOTION_CODES.md) | `backend/docs/PROMOTION_CODES.md` | 27 |
| [LEGACY-X Reconnect & Last Played](09-other/backend--docs__RECONNECT_LAST_PLAYED.md) | `backend/docs/RECONNECT_LAST_PLAYED.md` | 25 |
| [Steam Profile Background Feasibility](09-other/backend--docs__STEAM_PROFILE_BACKGROUND_FEASIBILITY.md) | `backend/docs/STEAM_PROFILE_BACKGROUND_FEASIBILITY.md` | 17 |
| [LEGACY-X Backend & Plugin Integration TODO](09-other/backend--todo.md) | `backend/todo.md` | 129 |

## Repo бүрийн гарын авлага

Repo бүрийн өөрийн docs/MANUAL_MN.md: тухайн repo-г суулгах, ажиллуулах, засах

| Баримт | Repo | Мөр |
|---|---|---|
| [legacyxxx-backend: гарын авлага](11-manuals/backend--docs__MANUAL_MN.md) | `backend/docs/MANUAL_MN.md` | 248 |
| [legacyxxx-discord-bot: гарын авлага](11-manuals/discord-bot--docs__MANUAL_MN.md) | `discord-bot/docs/MANUAL_MN.md` | 238 |
| [legacyxxx-frontend: гарын авлага](11-manuals/frontend--docs__MANUAL_MN.md) | `frontend/docs/MANUAL_MN.md` | 184 |
| [legacyxxx-workshop: гарын авлага](11-manuals/workshop--docs__MANUAL_MN.md) | `workshop/docs/MANUAL_MN.md` | 163 |

## Upstream (гадны) баримт

MatchZy болон бусад гадны төслийн баримт: LEGACY-X-ийнх биш

| Баримт | Repo | Мөр |
|---|---|---|
| [LegacyX-MatchZy/README.upstream.md](10-upstream/plugins--LegacyX-MatchZy__README.upstream.md) | `plugins/LegacyX-MatchZy/README.upstream.md` | 59 |
| [Usage Commands](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__commands.md) | `plugins/LegacyX-MatchZy/documentation/docs/commands.md` | 84 |
| [Configuration](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__configuration.md) | `plugins/LegacyX-MatchZy/documentation/docs/configuration.md` | 213 |
| [LegacyX-MatchZy/documentation/docs/credits.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__credits.md) | `plugins/LegacyX-MatchZy/documentation/docs/credits.md` | 9 |
| [LegacyX-MatchZy/documentation/docs/database_stats.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__database_stats.md) | `plugins/LegacyX-MatchZy/documentation/docs/database_stats.md` | 30 |
| [LegacyX-MatchZy/documentation/docs/developers.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__developers.md) | `plugins/LegacyX-MatchZy/documentation/docs/developers.md` | 8 |
| [LegacyX-MatchZy/documentation/docs/donation.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__donation.md) | `plugins/LegacyX-MatchZy/documentation/docs/donation.md` | 12 |
| [Events & Forwards](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__events_and_forwards.md) | `plugins/LegacyX-MatchZy/documentation/docs/events_and_forwards.md` | 18 |
| [LegacyX-MatchZy/documentation/docs/get5.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__get5.md) | `plugins/LegacyX-MatchZy/documentation/docs/get5.md` | 154 |
| [LegacyX-MatchZy/documentation/docs/getting_started.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__getting_started.md) | `plugins/LegacyX-MatchZy/documentation/docs/getting_started.md` | 15 |
| [LegacyX-MatchZy/documentation/docs/gotv.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__gotv.md) | `plugins/LegacyX-MatchZy/documentation/docs/gotv.md` | 96 |
| [LegacyX-MatchZy/documentation/docs/index.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__index.md) | `plugins/LegacyX-MatchZy/documentation/docs/index.md` | 29 |
| [Installation](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__installation.md) | `plugins/LegacyX-MatchZy/documentation/docs/installation.md` | 14 |
| [LegacyX-MatchZy/documentation/docs/match_setup.md](10-upstream/plugins--LegacyX-MatchZy__documentation__docs__match_setup.md) | `plugins/LegacyX-MatchZy/documentation/docs/match_setup.md` | 79 |
| [LEGACY-X WeaponPaints Fork](10-upstream/plugins--LegacyX-WeaponPaints__UPSTREAM.md) | `plugins/LegacyX-WeaponPaints/UPSTREAM.md` | 19 |
| [AdminPlus Plugin](10-upstream/plugins--README.upstream.md) | `plugins/README.upstream.md` | 50 |

