> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-MatchZy/RANK_BRIDGE.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-MatchZy/RANK_BRIDGE.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X MatchZy Rank Bridge

MatchZy нь final `map_result` дээр Match ID, map number/name, winner, хоёр 5-player team, SteamID64 болон player stats бүхий rank payload үүсгэнэ. `event_id` нь `matchzy:<matchId>:<mapNumber>:map_result` хэлбэртэй deterministic тул backend retry/duplicate delivery-г аюулгүй ignore хийнэ.

## Server-only configuration

`cfg/MatchZy/legacyx-rank.private.cfg.example`-ийг private runtime file болгон хуулж дараах гурван утгыг тохируулна:

```cfg
legacyx_rank_season "season-1"
matchzy_remote_log_url "https://api.legacyx.cc/api/v1/plugin/matchzy/events"
matchzy_remote_log_header_key "x-plugin-secret"
matchzy_remote_log_header_value "YOUR_PLUGIN_INGEST_SECRET"
```

Private cfg нь `.gitignore`-д орсон. `matchzy_remote_log_url` enable бол бүх MatchZy remote event очно, гэхдээ backend нь зөвхөн `map_result` дээр leaderboard/rank update хийнэ.

## Match lifecycle ownership

| Component | Ownership |
|---|---|
| Exact 5v5, ready, map end, demos, soft rotation | MatchZy |
| Rank payload emit | MatchZy Rank Bridge |
| HTTP auth, input validation, rate limiting | AdminPlus API |
| Rating, idempotency, leaderboard persistence | Supabase RPC |
| RCON/admin action | AdminPlus plugin/backend |

AFK Manager болон AdminPlus нь MatchZy-ийн rank event-г өөрчилдөггүй.
