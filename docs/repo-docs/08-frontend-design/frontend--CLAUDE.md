> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/CLAUDE.md](https://github.com/userneon/legacyxxx-frontend/blob/main/CLAUDE.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X frontend — working rules

React + TypeScript + Tailwind v4 + shadcn frontend for the LEGACY-X CS2/CSGO community
platform (matches, skinchanger, leaderboard, penalties, reviews, tournaments). The full
design spec of record lives in `docs/design/` (`README.md` first, then `PROMPT.md`,
`RANK-SYSTEM.md`) — read it before touching layout, tokens, or copy. This file is the
short version plus the rules an agent working from `docs/design/` alone would miss or
get wrong (like the accent-color exception below).

## Non-negotiable

- **Never fabricate data.** Every number, name, and list comes from a real API call.
  Loading = skeletons shaped like the reference. Empty = short neutral message in
  `--text-dim`, no illustration. Error = one line + Retry (`RotateCcw`), never a blank
  area or sample data standing in for the real thing.
- **Security boundary.** The browser bundle only ever calls the public, rate-limited
  `/api/public/*` read endpoints. The operator API (`/api/*`, `x-api-secret` header) is
  server/operator-only and must never be reachable from frontend code or env files.
  Never commit `API_SECRET`, plugin secrets, RCON credentials, or Supabase
  service-role credentials into anything under this repo, including `.env` files.
- **Git: main only.** No branches, worktrees, or PRs for this project — work directly
  on `main`, one commit per logical change, push after. If a tool creates a branch
  anyway, merge it back into main and delete it.
- **Commit subjects are public.** Every deploy posts the subjects of the commits it brought, as
  written, to a Discord channel (`legacyxxx-plugins/scripts/announce.sh`; same in all four repos).
  Write the subject as one plain sentence a player could read. A security fix, or anything that must
  not be announced, gets `[skip announce]` in the commit message. Docs-only commits are skipped.
- **Escape all user text.** Names, review text, penalty reasons — anything a player
  typed — renders as escaped text, never raw HTML.

## Design tokens (`src/index.css`)

Neutral surfaces, white as the workhorse accent, and **crimson as the brand accent** (owner decision
2026-09-27, see `docs/design/PROMPT.md`). This is what the site does today; the earlier orange `#ff5a1f`
exception was dropped and no longer exists in `src/`.
- Surfaces: `--bg` `#0a0a0a`, `--panel` `#0f0f0f`, `--card-surface` `#141414`, `--raised` `#1a1a1a`
- Lines: `--line-soft` `#1f1f1f`, `--line` `#262626`, `--line-strong` `#333333`
- Text: `--text` `#fafafa` → `--text-faint` `#525252`
- `--accent-solid` `#fafafa` (on `#0a0a0a`): primary buttons (`.lx-primary-button`), progress fills, unread dots
- `--brand` `#e11d48` (fills, borders, glows), `--brand-bright` `#ff3d6e` (text, icons), `--brand-deep`
  `#4c0717`, `--brand-on` `#fff5f7`. Crimson is used sparingly (owner request 2026-10-01: too much of it).
  It belongs to: the wordmark's "-X", the active sidebar item, the hero's accent word, the flagship
  "Players Online" figure, the #1 leader and the viewer's own row, the active sort column, the Pro League
  card, the rank/EXP progress fill and the soft page glows. Everything else is neutral: section-heading bars
  (`--text-faint`), hover borders (`--line-strong`), hover fills (`--raised`), focus rings (`--accent-solid`),
  icon tiles, slot dots (`--text-2`). A primary button stays white. Before adding crimson somewhere new,
  ask whether it is one of the spots above; if not, leave it neutral.
- `--status-green` `#22c55e` (online/live/verified) and `--status-red` `#ef4444` (limits, bans) are the status colors
- Rank emblems use the CS2 rarity ladder (`--rank-<tier>`) **only** inside emblem
  images and the rank name next to one — never on buttons, backgrounds, or chrome
- Font: Onest (400/500/600/700) with its default proportional figures. Owner request
  2026-09-28: no `tabular-nums` — Onest's tabular "1" made numbers like "11" read as "1 1"

### One scale for everything (use these, not new values)

- **Radius:** 8px controls (`rounded-lg`), 10–12px cards (`rounded-[10px]`, `rounded-xl`), 14–16px floating
  panels (`rounded-2xl`), 999px pills (`rounded-full`). An avatar or tile may use a proportional radius
  (about a quarter of its size), and a thing nested in a padded box uses the box's radius minus the padding;
  nothing else gets its own number.
- **Text:** 10–11px meta and labels, 13px body and controls, 15px emphasis, 22px+ headings, `font-display`
  only for the wordmark and page titles. Don't add a new pixel size.
- **Buttons:** the primary action is `.lx-primary-button` (white); a secondary action is a bordered
  `--raised` button; hover on neutral controls is `hover:bg-[var(--raised)]`. Brand crimson appears on a
  button only as a selected/active state, never as the default look.
- **Surfaces:** shell and cards use the glass tokens below; a plain `--panel`/`--raised` fill is for nested
  or solid-over-art cases. Overlays that must stay readable over images use a solid `--card-surface` gradient.

### Limit red (owner request 2026-09-30)

When a player hits the daily or weekly EXP limit (gains count for ¼), the limit bar, the `×¼` label,
its one-line note and the `×¼ limit` pill in the match row are red (`--status-red` `#ef4444`, already in `src/index.css`). Nothing else
uses red; it is a status color like `--status-green`, not a general accent. Reference: `docs/design/exp-limit.png`.

### Glass (owner request 2026-09-28)

The whole app sits on a near-black backdrop (`body::before`) with white `<BackgroundBeams />` (owner request 2026-09-30, replaces the Dust II art). The shell — sidebar, top bar and
page panel — is `.lx-glass-shell`: translucent and blurred. Cards inside use `--glass-fill`
(`bg-[var(--glass-fill)]`, `.glass`, `.lx-glass`) with a `--glass-line` hairline; they do not blur
themselves, the panel under them already does. Keep new surfaces on these tokens; image overlays
(`from-[var(--card-surface)]` gradients) stay solid so text over art stays readable. Home adds a
soft crimson light (`.lx-glass-page`). Stats and timers use `.lx-stat-grid` cells, not a box each.

## Icons and assets

lucide-react only, stroke 2, no emoji, no placeholder icons. Sizes: 18px nav/top bar,
16px buttons/menus, 14px inline/meta. Real game/brand assets already in the repo
(`public/logolegacyx.webp`, `src/lib/cs2-map-art.ts`, `src/assets/skinchanger/*`,
rank SVGs in `public/ranks/`) — reuse them, don't redraw. CS2 rarity colors appear
only as a thin indicator on skin tiles/equipped weapon cards, never as backgrounds.

## Commands

```bash
npm ci                                   # install from the checked-in lockfile
VITE_API_URL=<api origin> npm run dev    # dev server
npm run build                            # production bundle
npm run check:colors                     # palette lint — must pass before commit
```

## Reference docs

- `docs/design/README.md` — index of every reference screen (HTML spec + PNG)
- `docs/design/PROMPT.md` — the original full build prompt (git workflow, phased
  rebuild plan, per-page component/icon mapping)
- `docs/design/RANK-SYSTEM.md` — EXP/rank calculation spec
