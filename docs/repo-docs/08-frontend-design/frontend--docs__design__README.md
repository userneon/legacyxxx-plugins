> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/docs/design/README.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/design/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# Legacy-X design references

All layouts at 1440x900. Each state has a static HTML file (exact inline values = the spec) and a PNG screenshot.
Skeleton bars mark where real data renders. Neutral surfaces with a crimson brand accent (`--brand` #e11d48 / `--brand-bright` #ff3d6e, since 2026-09-27 — see PROMPT.md §2): where a reference shows a white accent (primary button, active marker, focus ring, #1 spot), the site uses crimson. Status green #22c55e, rank colors only inside rank emblems/names. Font: Onest.

## Files

| Page / state | HTML | PNG | Notes |
|---|---|---|---|
| App shell | `shell.html` | `shell.png` | Sidebar expanded, top bar with kill feed, notifications, avatar; empty content panel. |
| App shell — collapsed | `shell-collapsed.html` | `shell-collapsed.png` | Sidebar collapsed to icon rail via the toggle. |
| App shell — notifications | `shell-notifications.html` | `shell-notifications.png` | Bell clicked: notifications popover. |
| App shell — profile menu | `shell-profile-menu.html` | `shell-profile-menu.png` | Avatar clicked: Profile, Settings, Sign out. |
| Skinchanger — Terrorist | `skinchanger/skinchanger-t.html` | `skinchanger/skinchanger-t.png` | T-only + shared weapons in category columns. |
| Skinchanger — Counter-Terrorist | `skinchanger/skinchanger-ct.html` | `skinchanger/skinchanger-ct.png` | CT-only + shared weapons. |
| Skinchanger — skin picker | `skinchanger/skinchanger-modal.html` | `skinchanger/skinchanger-modal.png` | Weapon clicked: blurred overlay + picker with customize panel. |
| Leaders | `leaders.html` | `leaders.png` | Sort EXP / K/D / Win rate, top-3 cards, table from #4, pinned "You" row. |
| Leaders — sorted by K/D | `leaders-sort-kd.html` | `leaders-sort-kd.png` | Active sort column highlighted; min. 10 matches. |
| Penalties | `penalties.html` | `penalties.png` | Your status, search + type/status filters, Active and History groups. |
| Penalties — details drawer | `penalties-details.html` | `penalties-details.png` | Row click opens the details sheet. |
| Penalties — your active penalty | `penalties-my-active.html` | `penalties-my-active.png` | Your status in the active state + your drawer with Appeal. |
| Reviews | `reviews.html` | `reviews.png` | Reading-first: list + filters on the left, summary + write action on the right. |
| Reviews — filtered to 5★ | `reviews-filter-5.html` | `reviews-filter-5.png` | Distribution row / chip filter active. |
| Reviews — write dialog | `reviews-write.html` | `reviews-write.png` | Blurred overlay, star rating with label, 0/500 counter. |
| Reviews — after posting (cooldown) | `reviews-cooldown.html` | `reviews-cooldown.png` | Your review pinned; write button shows time until next review. |
| Explore — empty | `explore.html` | `explore.png` | Player search, no query. |
| Explore — results | `explore-results.html` | `explore-results.png` | Player cards for a query. |
| Tournaments — none scheduled | `tournaments-none.html` | `tournaments-none.png` | Empty state + Discord notify + past tournaments. |
| Tournaments — registration open | `tournaments-registration.html` | `tournaments-registration.png` | Event hero, Overview tab, Join solo / Register a team. |
| Tournaments — live bracket | `tournaments-live-bracket.html` | `tournaments-live-bracket.png` | Bracket with your path in the crimson accent, next match + connect. |
| Settings | `settings.html` | `settings.png` | Connections, Notifications, Website — applies instantly. |
| Settings — link Discord | `settings-discord-link.html` | `settings-discord-link.png` | 3-step /link code dialog. |
| Settings — Connections | `settings-connections.html` | `settings-connections.png` | Steam connected, Link Discord, FACEIT auto-detected. |
| Settings — Notifications | `settings-notifications.html` | `settings-notifications.png` | Switches; Penalties locked on. |
| Settings — Website | `settings-website.html` | `settings-website.png` | Kill feed, sidebar collapsed, motion, time format (per device). |
| Profile — another player | `profile.html` | `profile.png` | Header, rank, trust, stats, recent matches. |
| Profile — lower sections | `profile-lower.html` | `profile-lower.png` | Recent matches, maps, FACEIT, penalty history, loadout. |
| Profile — your own | `profile-me.html` | `profile-me.png` | Own profile: "Profile settings" instead of More. |
| Profile — privacy popover | `profile-me-privacy.html` | `profile-me-privacy.png` | "What others can see" switches on your own profile. |
| Profile — owner | `profile-owner.html` | `profile-owner.png` | Owner badge, Legacy-X team card, playing-now + Join. |
| Profile — hidden sections (others' view) | `profile-hidden-stats.html` | `profile-hidden-stats.png` | Stats, matches, maps and FACEIT hidden by the player. |
| Play — 5x5 Matches | `play-5x5.html` | `play-5x5.png` | Quick join, map chips, server cards with 10 slots. |
| Play — server details | `play-5x5-server.html` | `play-5x5-server.png` | Live scoreboard sheet with Copy IP / Spectate / Connect. |
| Play — Fun Mode | `play-fun.html` | `play-fun.png` | Mode chips, capacity bars. |
| Play — Pro League locked | `play-pro-locked.html` | `play-pro-locked.png` | Rank requirement + progress + path to unlock. |
| Ranks in Leaders | `leaders-ranks.html` | `leaders-ranks.png` | Top-3 emblems 40px, rank column emblem 22px + colored name, "You" row. |
| Ranks in Profile | `profile-ranks.html` | `profile-ranks.png` | Header badge 16px, rank card emblem 64px + colored name. |
| Ranks in profile menu | `profile-menu-ranks.html` | `profile-menu-ranks.png` | Rank badge under the name in the dropdown header. |
| Ranks in Pro League gate | `play-pro-locked-ranks.html` | `play-pro-locked-ranks.png` | Required rank inline, You → Required emblems on the progress bar. |

## Also in this folder

- `PROMPT.md` — the full Claude Code prompt
- `RANK-SYSTEM.md` — EXP / rank calculation spec (Phase 11)
- `ranks/` — 18 rank emblems (SVG + PNG) + `ranks-preview.png`
- `index.html` — gallery of every screen (works on phone)
- `legacyx-layouts.pdf` — every screen, one per page

Home keeps its current content inside the new shell (see PROMPT.md).
