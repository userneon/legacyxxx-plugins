> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/docs/design/PROMPT.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/design/PROMPT.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

TASK: Rebuild the Legacy-X app shell and pages to match the design references
exactly, using the site's real icons and assets.
Repos: legacyxxx-frontend-main (main work), legacyxxx-backend-main (only where stated).
References: docs/design/ in the frontend repo — read README.md first, then every
HTML file for the phase you're on.
Git: main only, no branches (see section 0 first). One commit per phase, in order.
Push to main after each phase.
If an earlier prompt already did part of this, only fix what differs from the references.

═══ 0. GIT — EVERYTHING ON MAIN (do this before Phase 1) ═══
Applies to every repo you touch: legacyxxx-frontend-main, legacyxxx-backend-main,
and the plugins repo.
1. Safety first: `git fetch --all --prune`, then tag the current main on the
   remote as `backup/pre-main-consolidation-<date>` and push the tag.
2. List every local and remote branch with how many commits it is ahead of /
   behind main (`git log --oneline main..<branch>`). Show me this list.
3. Merge every branch that has commits not in main into main — `git merge
   --no-ff`, no rebase, no force-push, no history rewrite. Oldest branch first.
   If a merge conflicts, resolve it only when the correct result is obvious;
   otherwise stop and show me the conflicting hunks.
4. After all merges: install, typecheck and build must pass on main. Push main.
5. Only then delete the branches that are now fully merged (`git branch -d`,
   and `git push origin --delete <branch>`). Never delete a branch that still
   has unmerged commits. Report which branches were deleted.
6. Make sure deployment uses main: check the deploy config/scripts/CI (e.g.
   anything pointing at fix/public-read-routes or another branch) and switch
   it to main. Tell me exactly what you changed.
From now on: work only on main. Never create a branch, worktree or PR for this
project. If a tool creates one anyway, merge it back into main immediately and
delete it.

═══ 1. REFERENCE RULES ═══
- The static HTML files are the spec: every color, px size, radius, gap, border,
  font size/weight and blur value in them is intentional. Match them.
- The PNGs show the expected result at 1440x900. The Playwright check at the end
  of each phase compares against them.
- Grey skeleton bars/blocks in the references are placeholders. Replace each one
  with the REAL data and REAL asset for that spot (see section 3). Never ship
  skeletons as decoration and never show fake/sample data.
- React + Tailwind + existing shadcn components. Translate inline styles into
  Tailwind classes / CSS variables. Don't paste the reference HTML.
- Keep all existing routing, auth, API calls and business logic. Change
  presentation only unless a phase says otherwise.

═══ 2. DESIGN TOKENS ═══
Palette revision 2026-09-27 (owner decision): crimson is the brand accent. Where
the static HTML references show a white accent (primary buttons, active markers,
focus rings, the #1 spot), the site uses crimson instead; everything else in the
references (surfaces, lines, text, radii, spacing) still applies. In this
document "accent" means the crimson --brand, not white.

Define in src/index.css as CSS variables and map shadcn's variables to them:
- Surfaces: --bg #0a0a0a, --panel #0f0f0f, --card #141414, --raised #1a1a1a
- Lines: --line-soft #1f1f1f, --line #262626, --line-strong #333333
- Text: --text #fafafa, --text-2 #d4d4d4, --text-muted #a3a3a3,
  --text-dim #737373, --text-faint #525252
- Brand accent = crimson, taken from the LEGACY-X key art:
  --brand #e11d48 (fills, borders, glows), --brand-bright #ff3d6e (text, icons,
  markers, the light end of gradients), --brand-deep #4c0717, --brand-on #fff5f7
  (text on a crimson fill). Never hard-code these values; use the tokens.
- Crimson goes on: primary buttons (.lx-primary-button: neutral white, no hue; was crimson until 2026-09-30,
  e.g. Play now, Sign in with Steam, Save, Connect, Appeal), the active nav item
  (crimson marker + faint tint), section-title markers (3px bar), selected items
  and their borders, focus rings (--brand-bright at 60%), progress / EXP / slot
  fills, the #1 spot (medal, podium, leaderboard position), hover edges and glows
  on interactive cards, and the page heroes (.lx-hero-glow + .lx-hero-grid).
- Crimson never goes on: body text, large solid backgrounds, ordinary borders,
  or anything that carries its own meaning (below).
- White #fafafa (--accent-solid) stays the neutral emphasis: headings, values,
  the selected Segmented thumb's label, secondary buttons' text.
- Colours with a meaning keep their own tokens and are not replaced by crimson:
  status green #22c55e (online / live / verified / win), --status-red (loss,
  active penalty), penalty types (--penalty-ban / -mute / -gag), T and CT side
  colours (--team-t / --team-ct), CS2 rarity bars, rank tier colours, and the
  review stars / favourites (--star, yellow — owner request 2026-09-25).
- No other yellow/amber: not for warnings, badges or the #1 spot (that is crimson).
- Exception — rank colors: rank emblems use the CS2 rarity ladder (same values as
  the --rarity-* tokens): Recruit #b0c3d9, Operator #5e98d9, Vanguard #4b69ff,
  Ace #8847ff, Apex #eb4b4b, Legacy #e4ae39. Define them as --rank-<tier> tokens.
  They appear ONLY inside the emblem images and as a thin color on the rank name
  next to an emblem (e.g. rank text in a badge) — never on buttons, backgrounds
  or other UI chrome.
- No blue anywhere in UI chrome. Replace every blue/slate/gray/zinc/indigo/sky
  class and blue-tinted hex with the neutrals above. Add scripts/check-no-blue.mjs
  (npm run check:colors) that fails on hue 180–270° colors in src/**. (The script
  now blocks 180–340° and 25–70°; crimson sits at ~345° and passes. Game and
  brand colours that must stay are marked `palette-exempt` on their line.)
- Radius: 8px controls, 10–12px cards, 14px floating panels, 999px pills.
- Spacing: 8px gutters between sidebar, top bar and content panel.
- Font: Onest (Google Fonts, weights 400/500/600/700) with system fallback.
  Tabular numbers for all stats.

═══ 3. ICONS AND REAL ASSETS (use these — no placeholders, no emoji) ═══
UI icons: lucide-react (already installed and used across the site). Stroke 2.
Sizes: 18px nav/top bar, 16px buttons/menus, 14px inline/meta.
Use exactly these:
- Sidebar: Home → House, Play → Play, Skinchanger → KnifeIcon (src/components/knife-icon.tsx,
  owner-supplied combat knife, replaces Paintbrush 2026-09-27),
  Leaders → Trophy, Penalties → Gavel, Reviews → MessageSquare, Explore → Search,
  sidebar toggle → PanelLeft, Pro League locked → Lock
- Top bar: notifications → Bell, headshot in kill feed → Crosshair
- Profile menu: Profile → User, Settings → Settings, Sign out → LogOut
- Skinchanger: customize → SlidersHorizontal, remove skin → Trash2,
  dialog close → X, search → Search
- Reviews: rating → Star (filled yellow --star when selected), submit → Send
- Tables/lists: copy IP → Copy, server info → Info, join/play → Play
- Status/misc: loading → LoaderCircle (spin), error → ServerCrash, refresh → RotateCcw

Real game and brand assets (already in the repo — reuse, don't redraw):
- Logo: public/logolegacyx.webp in the sidebar header (keep the LEGACY-X
  wordmark next to it if the logo is icon-only).
- Rank badges: the existing CompetitiveRankBadge component with rank_image_key
  (Leaders table, profile menu header).
- Map art: src/lib/cs2-map-art.ts for map thumbnails (server cards). If those images still live under /manus-storage/, move them into
  public/maps/ and update the paths as part of Phase 1.
- Team art: src/assets/skinchanger/team-t.webp and team-ct.webp inside the
  TERRORIST / COUNTER-TERRORIST switch (small icon left of the label).
- Skin and weapon images: skinchanger_catalog_items.image_key from the catalog
  API, for every weapon card (equipped skin, or the weapon's default image when
  nothing is equipped) and every skin tile in the picker.
- Category art: src/assets/skinchanger/pistol.png, rifles.png, knife.png,
  gloves.png etc. may be used as the default image for empty cards.
- Steam: the Steam mark is an inline SVG (`SteamIcon` in `src/components/steam-login-gate.tsx`) on the "Sign in with Steam" button.
- Avatars: real Steam avatars from the API, rounded square (radius ~ 28% of size).
  Fallback: first letter of the name on --raised.
- Rarity colors (CS2 official) are allowed ONLY as a thin indicator on skin tiles
  and equipped weapon cards — never as backgrounds.
Remove duplicate files (e.g. "… copy.webp", "(1).webp") that aren't referenced.

- Rank emblems: use the 18 original SVGs in docs/design/ranks/ (rank-01.svg …
  rank-18.svg, preview in ranks-preview.png). Copy them to public/ranks/ and
  point competitive_rank_definitions.image_key (rank-01 … rank-18) at them.
  Delete the old rank images (they look like Valve's CS:GO emblems); the old
  CS:GO rank names are replaced too (see RANK-SYSTEM.md section 4). Render
  them as <img> with explicit size (72px on Profile rank card, 22–24px in
  badges/tables, 40px on Leaders top-3) and alt = rank name.
  Placement references: leaders-ranks, profile-ranks, profile-menu-ranks,
  play-pro-locked-ranks. Next to an emblem the rank name is written in its tier color.

═══ 4. STATES (every data-driven area) ═══
- Loading: skeletons shaped exactly like the reference.
- Empty: short neutral message (e.g. "No matches yet") in --text-dim, no illustration.
- Error: one line + "Retry" (RotateCcw), never a blank area.
- Hover: border → --line-strong (interactive cards: a crimson edge + lift, see
  section 7), background → one step lighter.
- Focus: visible 2px ring in --brand-bright at 60% opacity on every interactive element.
- prefers-reduced-motion: no ticker animation, no hover blur, no transitions.
- Every flex/grid child that can hold long content (kill feed, tables, names)
  gets min-width: 0 and text truncation with a title tooltip.
- All user-provided text (names, reasons, reviews) rendered as escaped text.

═══ 5. PHASES ═══

PHASE 1 — App shell  (shell, shell-collapsed, shell-notifications, shell-profile-menu)
- Floating sidebar: shadcn <Sidebar variant="floating" collapsible="icon">,
  14px radius, 8px gap to the window edges. Header: logo + toggle (PanelLeft).
  Collapsed state persists (shadcn cookie).
- Nav order: Home · Play (5x5 Matches, Fun Mode, Pro League, Tournaments) ·
  Skinchanger · Leaders · Penalties · Reviews · Explore. Remove Shop and Clan.
- Play children sit under one 1px vertical guide line. Active item: --raised
  background, --text label, 2px accent segment on the line / left edge.
- Right side of "5x5 Matches" and "Fun Mode" on the same row: 6px green dot +
  real online player count, refreshed every 30s, hidden when 0 or on error.
- Pro League: Lock icon + muted label when the user hasn't unlocked it.
- Floating top bar (same surface as the sidebar): kill feed left (section 6),
  divider, Bell with accent unread dot, rounded-square avatar.
- Bell → notifications popover (shell-notifications): header with
  "Mark all as read", rows with icon/avatar, two text lines, unread accent dot.
- Avatar → profile menu (shell-profile-menu, shadcn DropdownMenu): header with
  avatar, name, rank badge, EXP bar toward next rank; then exactly two items —
  Profile, Settings — then a separator and Sign out. No "Steam profile" item.
- Content panel: rounded 14px box that scrolls internally.
- Home page (src/pages/home.tsx): keep ALL current content, sections, order,
  data and behavior. Only restyle to the neutral tokens inside the new panel.
  Remove only what depends on deleted features (reconnect card, "Active Clans"
  stat, clan badges) and list what you removed.
- Commit: "App shell: floating sidebar, top bar, menus"

PHASE 2 — Leaders  (leaders, leaders-sort-kd)
- Header: title + subtitle on the left; on the right a segmented sort control
  EXP / K/D / Win rate (sliding thumb) and a search input (debounced 250ms).
  Subtitle changes with the sort: "Ranked by EXP from completed matches." or
  "Ranked by <metric> · minimum 10 matches."
- K/D and Win rate rankings only include players with >= 10 completed matches
  (do this server-side; add ?sort=exp|kd|win to the leaderboard endpoint if it
  doesn't support it). K/D = kills/deaths, Win rate = wins/matches.
- Top 3 cards (#1, #2, #3) above the table: position number (#1 in accent with
  a 2px accent top bar), rank badge, avatar + name, the active sort metric as
  the big number, plus Matches and Win rate. Card click → profile.
- Table starts at #4. Columns: # / Player (avatar + name) / Rank (badge + name)
  / EXP / Matches / Win rate / K/D. The active sort column header is crimson with one underline that slides
  between columns when the sort changes, and
  semibold; others #737373. Sticky header while the list scrolls under it.
- Pinned "You" row at the bottom (accent ring on the avatar), always showing the
  user's real position for the active sort; hidden when logged out or unranked.
- Sort + search are stored in the URL. Row click → profile.
- Commit: "Leaders page redesign"

PHASE 3 — Penalties  (penalties, penalties-details, penalties-my-active)
The page answers three questions, in this order: "Am I penalized?",
"Is this player penalized?", "What happened on the servers?".
- Header: title, subtitle "Every ban, mute and gag on Legacy-X servers is
  public.", "Server rules" link (external) on the right.
- Your status card (logged in only), from the existing
  /api/v1/profile/me/penalties endpoint:
  - clean → green shield (ShieldCheck), "Your record is clean".
  - active → accent border, ShieldAlert, type + reason + term on one line,
    "Details" (opens the drawer for that penalty) and "Appeal on Discord"
    (link to the Discord appeals channel/ticket — put the URL in config).
  - logged out → hide the card (no sign-in nag here).
- Controls row: search "Check a player — name, Steam ID or profile link"
  (accept a pasted Steam profile URL and extract the ID), Type segmented
  control All / Bans / Mutes / Gags with a small count on each (replaces the
  old 5 stat tiles), Status segmented control All / Active only.
- List: sticky header Player / Type / Reason / Term / Date / chevron.
  Rows are grouped: "Active" (accent dot) first, then "History" (grey dot).
  Active only hides History. Reason is one line, truncated with ellipsis.
  Admin column is removed from the list (it's in the drawer).
- Row click → details drawer on the right (shadcn Sheet, 400px, dim overlay,
  Esc/overlay closes): player avatar, name, Steam ID; fields Type, Status
  (Active / Expired / Unbanned / Permanent from isPermanent/isUnbanned/
  moderationStatus), Term, Issued by (admin), Date; full Reason text.
  Footer: "View profile" and, only when it is the viewer's own penalty,
  "Appeal on Discord".
- Filters, search and the open drawer id are stored in the URL (shareable link
  to a specific penalty).
- Backend follow-up (tell me if missing): an expiresAt timestamp per penalty so
  the UI can show "Ends in 2d 4h" instead of only the raw term.
- Commit: "Penalties page redesign"

PHASE 4 — Reviews  (reviews, reviews-filter-5, reviews-write, reviews-cooldown)
Most visitors come to READ reviews and judge the community; only a few write.
So reading is the main area, writing is one clear action, and trust is visible.
- Layout: reviews list on the left (readable column, max-width 760px), a
  340px summary panel on the right that stays in view.
- List header: star filter chips All / 5★ / 4★ / 3★ / 2★ / 1★ and a sort
  control Newest / Highest / Lowest (sliding thumb).
- Review card: avatar, name, "Verified player" badge (ShieldCheck, green) ONLY
  when the entry has a steamId of a registered Legacy-X user, star rating,
  relative date ("3 days ago", full date on hover), message clamped to 4 lines
  with "Read more" that expands inline.
- If the viewer has a review, pin it first with a "YOUR REVIEW" label and an
  accent border (same card otherwise).
- Summary panel: big average (1 decimal) + stars + "N reviews"; distribution
  rows 5→1 (bar width = share) that act as filters (click toggles; synced with
  the chips); then the write area:
  - can write → short prompt text + primary "Write a review" button.
  - on cooldown → disabled button "Next review in 3d 4h" (Clock icon) +
    "One review per week keeps the page honest." Compute the next allowed time
    from the viewer's latest review (or a backend field if one exists) so the
    user never has to hit the weekly_cooldown error to find out.
  - logged out → "Sign in with Steam to write a review".
- Write dialog (shadcn Dialog, blurred overlay like the skin picker): star
  rating with hover preview and a word label (Bad / Poor / Okay / Good /
  Great), textarea with 0/500 counter, note "Posted publicly with your Steam
  name. One review per week.", Cancel / Post review (disabled until a rating
  is picked and the text has at least 10 characters).
- After posting: dialog closes, the new review appears pinned at the top with
  a fade-in, the panel switches to the cooldown state. Keep the existing
  weekly_cooldown error handling as a fallback, shown inline in the dialog.
- Filter + sort in the URL. Average and distribution are computed from the full
  feedback list (not the filtered one).
- Commit: "Reviews page redesign"

PHASE 5 — Skinchanger  (skinchanger/skinchanger-t, -ct, -modal)
- Header: "Loadout", "!rs" hint, centered TERRORIST / COUNTER-TERRORIST switch
  (with team art), "Equipped N" (real count) on the right.
- Columns: PISTOLS | SMGS | RIFLES + KNIFE | SNIPER RIFLES + HEAVY | AGENT + GLOVES.
  T shows T-only + shared weapons, CT shows CT-only + shared; derive from catalog
  weapon_class, driven by the existing teamScope state.
- Weapon card (landscape, ~96px tall): real skin/weapon image filling the card,
  name bottom-left uppercase, small corner notch top-left, charm slot above the
  name, rarity indicator if a skin is equipped.
- Hover: image blur 4px + opacity .55, centered SlidersHorizontal button fades in;
  Trash2 top-right only when a skin is equipped (removes it, doesn't open dialog).
- Click → shadcn Dialog with blurred overlay (backdrop-blur 10px, black/55):
  weapon name + team, skin search, rarity filter pills, skin grid with real images
  (selected = accent border), customize panel (preview, wear slider showing value +
  FN/MW/FT/WW/BS band, seed, StatTrak switch, name tag, "Use on both T and CT"),
  Cancel / Save. Reuse the existing save/remove/customize API calls.
- AGENT only if the catalog has agents and the plugin can apply them; otherwise
  GLOVES alone. Tell me which case applies.
- Commit: "Skinchanger: inventory-style loadout grid"

PHASE 6 — Explore  (explore, explore-results)
- Player search only (remove the Clans tab). Large search input (debounced
  250ms), empty state, results grid of cards: avatar, name, last played,
  moderation status, K/D / Matches / Win rate / Hours. Card → profile.
- Commit: "Explore page redesign"

PHASE 7 — Tournaments  (tournaments-none, tournaments-registration, tournaments-live-bracket)
The page has three states and must look intentional in each — "no tournament"
will be the most common state for now.
- State "none": centered card (Trophy icon) "No tournament scheduled right
  now", one line about Discord announcements, primary "Get notified on
  Discord" (links to the Discord announcements role/channel from config).
  Below: "Past tournaments" list (name, date, winner with a Trophy icon,
  chevron → that tournament's page). Hide the list if there are none.
- Event hero (registration / upcoming / live / finished): status pill
  (Registration open = accent, Live = green, Upcoming/Finished = neutral), name,
  short description, meta row Starts / Format / Prize (Calendar, Users, Trophy
  icons), countdown on the right (label depends on state: "Registration closes
  in", "Starts in", "Round ends in"), players/slots progress bar.
- Tabs (sliding thumb): Overview · Bracket · Players · Matches.
  - Overview: "How it works" (short rules text + "Full rules" link) and a
    Schedule timeline (Registration closes → Check-in → Round 1 → Final), the
    current step marked in accent.
  - Bracket: rounds as columns (Quarterfinals → Semifinals → Final →
    Champion), match cards with two teams and scores, winner row brighter,
    LIVE badge on running matches, the viewer's team path outlined in accent.
    Horizontal scroll on small screens.
  - Players: registered players/teams list. Matches: schedule list with time,
    teams, server, status.
- "You" panel (right, sticky):
  - registration open → "Join solo" (primary) and "Register a team"
    (secondary), note "Check-in opens 30 min before start."
  - registered → your team, check-in button when check-in is open.
  - live → "YOUR NEXT MATCH" card (opponent, time, server) + "Connect to
    server" (steam://connect) + your team's avatars.
  - logged out → "Sign in with Steam to join".
- Tab + selected tournament id in the URL.

BACKEND/DATA — required, because clans are being removed:
- tournament_registrations today only has clan_id, so nobody can register
  without clans. Replace it with player-based registration:
  tournament_registrations(tournament_id, user_id, team_id nullable, mode
  'solo'|'team', checked_in_at) and tournament_teams(id, tournament_id, name,
  captain_user_id) + members via tournament_registrations.team_id.
  Solo players are auto-balanced into teams by EXP when registration closes.
- tournament_matches: drop clan_a_id/clan_b_id, reference tournament_teams
  (team_a_id, team_b_id), add server_id, and change scheduled_time from text to
  timestamptz. tournaments: add name, description, starts_at,
  registration_closes_at, check_in_opens_at (timestamptz), max_players; change
  next_match_time from text to timestamptz.
- Tables are empty (0 rows), so this is a clean migration. Show me the
  migration before applying it.
- Commit: "Tournaments page redesign + player-based registration"

PHASE 8 — Settings  (settings, settings-connections, settings-discord-link,
settings-notifications, settings-website; opened from the profile dropdown)
Settings holds exactly three things: connections, notifications, website
preferences. Nothing about how your profile looks to others lives here — that
is "Profile settings" on your own profile (Phase 9).
- Entry point: "Settings" (lucide Settings icon) in the profile dropdown, right
  after "Profile". Route /settings, logged-in only.
- Layout: left section nav (Connections · Notifications · Website) with
  scroll-spy; right column max 720px, one card per section.
- Saving: everything applies immediately (switches, segmented controls,
  link/unlink) with a small "Saved" confirmation; no Save buttons on this page.
  On a failed request, revert the control and show an inline error.
- Connections:
  - Steam: "Connected" (green check), read-only.
  - Discord: "Link Discord" opens a dialog: 1) open the Discord server, 2) type
    /link, 3) enter the code (XXXX-XXXX, expires in 10 min). Uses the /bot/link
    flow from the Discord bot task; if that backend isn't there yet, render the
    row disabled with "Coming soon". Linked → "Unlink".
  - FACEIT: "Auto-detected", read-only — found via steam_id, nothing to link.
  - No Profile links and no Spotify (removed; don't add a replacement).
- Notifications (saved to the account, users.notification_prefs jsonb):
  Tournaments and Rank changes switches; Penalties always on (locked, "Always on").
- Website (saved per device in localStorage, applied instantly, read on boot
  before first paint to avoid flashes):
  - Kill feed on/off (hides the ticker in the top bar).
  - Start with sidebar collapsed.
  - Motion: System / Reduce / Full (System follows prefers-reduced-motion;
    Reduce forces the reduced-motion rules from section 7).
  - Time format: 24h / 12h (used everywhere times are shown).
- Removed from Settings: account/display name editing (names and avatars come
  from Steam) and the sessions list.
- Backend fixes that belong here:
  - profileUpdateSchema accepts ANY avatar URL (up to 2048 chars). Remove
    avatar (and username) from the update schema: both come only from Steam.
    Arbitrary external image URLs let users show anything and let third
    parties log every viewer's IP.
- Commit: "Settings page"

PHASE 9 — Profile  (profile, profile-lower, profile-me, profile-owner)
A profile answers different questions for different visitors: "how am I
doing / what's next?" (me), "can I trust / how good is this player?" (others),
"who runs this place?" (staff). Same layout for everyone, a few parts change.
- Header: banner (Steam background if available, else a neutral gradient),
  104px rounded-square avatar overlapping it, display name, role badge for
  staff (Owner = crimson pill with Crown icon; other staff roles = crimson-outlined pill),
  rank badge; meta line "#N on leaderboard · Member since … · Last played …".
  Actions on the right:
  - viewing someone else: Copy link, Steam profile (icon button), More (…)
    with "Report player" (links to the Discord report channel for now).
  - viewing yourself: "Profile settings" (Eye icon) + Copy link + Steam profile.
    "Profile settings" opens a popover "What others can see" with switches:
    Legacy-X stats · Recent matches & maps · FACEIT stats · Loadout, and the note
    "Rank, leaderboard position and penalty history are always public."
    Saved immediately to users.profile_visibility (jsonb). The owner and staff
    always see everything. Hidden sections render for other viewers as a small
    card with EyeOff icon "<Section> hidden by player" (see profile-hidden-stats),
    and the matching fields are omitted from the public API response — hiding
    must happen server-side, not just in the UI.
  - if the player is on a server right now: green "Playing now" pill + "Join"
    (steam://connect) — from live server presence; hidden otherwise.
- Rank card: rank badge + name, EXP progress bar to the next rank, EXP and next
  threshold, link to the Leaderboard at their position.
- Trust card: On Legacy-X since, Steam account age (Steam API timecreated,
  hidden if the Steam profile is private), Record: "Clean" (green ShieldCheck)
  or "Active penalty" (links to it). Helps people spot smurfs and cheaters.
- Legacy-X stats tiles: Matches, Win rate, K/D, HS %, Avg kills (real data only;
  drop a tile if the data doesn't exist rather than showing zeros).
- Recent matches: "Form" strip of the last 10 results (W = green --result-win
  square, L = red --result-loss square), then a table Map (thumbnail) / Result / Score / K/D /
  EXP gained / Date, "All matches" link. Row click → match details if that
  page exists.
- Maps: win rate per map from their match history, bars, min. 3 matches per map.
- Right column: FACEIT card (level icon, ELO, win rate, avg K/D, avg kills, HS %,
  "Open" link; hidden if no FACEIT account is found via steam_id), Penalty
  history (compact list, newest first, active ones first; "View all" → the
  Penalties page filtered to this player; hidden entirely when there are none —
  the Trust card already says "Clean"), Loadout showcase (knife, gloves, AK-47,
  AWP for the side with most equipped skins; hidden if nothing is equipped).
- Owner / staff profiles additionally show a "Legacy-X team" card under the
  header: role + one line about the role, the reminder "Staff never ask for your
  password or items", "Penalties issued N →" (links to Penalties filtered by
  admin — public accountability), and "Contact on Discord".
  No admin tools or private data are ever shown on a public profile. Staff
  actions (issue penalty etc.) stay in the staff panel.
- Remove clan info and balance (deleted features). Works for /profile/me and
  /profile/:id.
- Commit: "Profile page redesign"

PHASE 10 — Play: 5x5 Matches, Fun Mode, Pro League  (play-5x5, play-5x5-server,
play-fun, play-pro-locked)
The only goal on these pages is "get me into a good game fast". One obvious
primary action (Quick join); manual browsing is secondary.
- One shared Play page component for /play/5x5, /play/fun, /play/pro (the
  sidebar item for the current mode is active). Header: title, one-line
  description per mode, live "● N players · M servers" on the right.
- Quick join card: Zap icon, one line explaining the pick rule per mode (always two
  lines tall so every mode's hero is the same height), big crimson "Play now"
  button (the only crimson primary button in the hero).
  Pick rule (backend: GET /api/v1/play/:mode/quick-join → server + connect address):
  - 5x5 / Pro: among joinable servers (waiting/warmup, not full), prefer the one
    with the most players (closest to starting), then the viewer's favourite maps.
  - Fun: busiest server with at least one free slot.
  If nothing is joinable, the button says "No open servers" (disabled) and the
  card suggests Fun Mode.
- Filters row: map chips (All maps + the real map pool from game_servers / maps;
  for Fun: mode chips from the servers' mode field), and toggles "Hide full" and
  "Favourites". Stored in the URL.
- Server grid (auto-fill, min 280px): card = map art (from the map library) with a
  status pill and a favourite star, server name + map, then:
  - 5x5 / Pro: 10 slot squares filled per player (+ "7/10").
  - Fun: a capacity bar + "12/16".
  - Status pill values: Waiting (neutral), Warmup (neutral), Live with score and
    round ("9 : 6 · R16", green dot), Full (dim).
  - Buttons: Copy IP (icon), Details, Connect (secondary style — Quick join stays
    the only primary). Connect is disabled with a tooltip when the server is full
    or offline.
  - Default sort: joinable first, then most players. No manual sort control.
- Details sheet (right, 440px, dim overlay): map art header with name/map, status
  + round, T : CT score, both teams' players (avatar, name, K / D / A) from the
  live match snapshot, footer Copy IP / Spectate / Connect. Spectate only if the
  server exposes a GOTV address; hide it otherwise. Live data refreshes every 5s
  while the sheet is open.
- Pro League locked (competitive access says not unlocked): no server list; a
  centered card with Lock icon; "Pro League unlocks at" and the rank badge sit on
  one flex row, centered together (not the badge floating above the text baseline);
  one line on why
  (even matches), progress bar from the viewer's rank to the required rank with
  "N EXP to go", and "Play 5x5 to rank up". Logged out → Steam sign-in gate.
  Unlocked → same page as 5x5 filtered to Pro servers.
- Keep the existing copy/connect helpers (steam://connect, join intent logging)
  and favourites API. Remove the old "Match #N" naming — show the server name.
- Commit: "Play pages redesign"

PHASE 11 — Rank system (backend)  (spec: docs/design/RANK-SYSTEM.md — read it fully)
- Implement the EXP formula exactly as specified: Elo result (K = 30), margin
  (±3), lobby-relative performance z-score (±8), highlight bonus (max +2),
  calibration ×2 for the first 10 ranked matches, clamp ±45 (±90 calibrating),
  floor 0. Fun Mode never changes EXP.
- Validity rules (section 3): ≥ 8 human players at the end, ≥ 13 rounds, player
  present ≥ 50 % of rounds; leaver = −25; short-handed team loss halved.
- Formula lives in TypeScript (pure function + unit tests covering the worked
  example in section 5 and every special case). One SQL function applies all
  deltas for a match atomically and idempotently (receipt by event id).
- Replace rank thresholds, slugs and names with section 4 (Recruit I–VI, Operator
  I–IV, Vanguard I–IV, Ace I–II, Apex, Legacy) and the rank images with
  docs/design/ranks/. Remove every "Silver … Global Elite" string from the code.
  Pro League unlock at 1400 (Vanguard I), removal
  only below 1350.
- Save exp_before / exp_delta / exp_breakdown per player on the match snapshot and
  show the breakdown in match rows (profile recent matches, match details).
- Migration: everyone to 1000 EXP.
- Report which plugin fields are missing (section 7).
- Commit: "Rank system: skill-based EXP"

═══ 6. KILL FEED (Phase 1) ═══
Data flow:
- Plugin → backend: the match plugin posts kill events to the existing plugin
  ingest route (same auth). Payload: { eventId, serverId, attackerSteamId,
  attackerName, victimSteamId, victimName, weapon, headshot, timestamp }.
  If the plugin doesn't send kills yet, build the backend side only and tell me
  exactly what the plugin must send.
- Backend: DO NOT store kills in the database. Keep the last 50 in an in-memory
  ring buffer, deduplicated by eventId. GET /api/v1/public/killfeed?after=<cursor>
  returns newest kills + cursor. Rate-limit like other public routes.
- Frontend: poll every 5s while the tab is visible; stop when hidden.
Display:
- "● LIVE" label, then a continuous right-to-left ticker (40s loop).
- Entry: attacker (--text, 500) · weapon name (--text-dim, 12px) · Crosshair icon
  only on headshots · victim (--text-2) · dim separator. Neutral colors only.
- Max 30 entries; new kills append. Edges fade with a mask gradient.
- Hover pauses. Click → that server. Reduced motion: latest 5, static.
- No kills in 10 minutes: "No live matches right now" in --text-dim.

═══ 7. MOTION AND SMOOTHNESS (every phase) ═══
Goal: everything feels instant and calm — no jumps, no flashes, no bouncy effects.
Tools: CSS/Tailwind transitions + tailwindcss-animate (shadcn default). Don't add
an animation library unless one is already installed.

Timing tokens (define as CSS variables and reuse):
- --ease-out: cubic-bezier(0.2, 0, 0, 1)   --ease-in: cubic-bezier(0.4, 0, 1, 1)
- --dur-fast 150ms (hover, press, color) · --dur-base 200ms (menus, popovers,
  tabs, page content) · --dur-slow 250ms (dialogs) · sidebar 300ms.
- Exits are ~30% faster than entries. Animate only opacity and transform
  (sidebar width is the only exception — use shadcn's built-in transition).

Shell:
- Sidebar collapse/expand 300ms; labels fade out in 120ms before the width
  shrinks; the content panel reflows with it, no jump.
- Route change: new page content fades in with a 4px upward slide (200ms).
  Sidebar and top bar never re-render or flicker. Restore scroll position when
  returning to a page.
- Profile menu and notifications popover: fade + scale 0.96→1 from the
  top-right origin, 200ms; close 140ms. Esc and outside click close them.
- Online counts and unread dot: crossfade when the value changes (no layout shift;
  reserve width with tabular numbers).
- Kill feed: new entries fade in at the end of the loop; the ticker never
  restarts or jumps when data refreshes; hover pause eases to a stop.

Data loading (all pages):
- First load: skeletons shaped like the final content, crossfading to real
  content in 150ms. Subtle shimmer on skeletons (1.5s, low contrast).
- Refetch/filter/search: keep the previous data visible (stale-while-revalidate)
  with a small LoaderCircle in the header — never flash back to skeletons.
- Images fade in on load (200ms) inside fixed-size boxes — zero layout shift.
- Keep URLs clean (owner request 2026-09-27): filters, sort, tabs, search, team
  (T/CT) and the open item are page state (src/hooks/use-view-params.ts), never
  written to the address bar — /leaders, not /leaders?sort=kd. A link that still
  carries a query (Profile → /penalties?q=…, old shared links) is read once and
  the URL is cleaned. One name per page (PAGE_TITLES in src/lib/routes.ts) feeds
  the sidebar, the page heading and the browser tab ("Leaders · LEGACY-X"; a
  profile shows the player's name).

Controls:
- Buttons: hover color 150ms, press scale 0.98. Disabled = 40–50% opacity, no motion.
  Primary buttons use .lx-primary-button (neutral white); secondary buttons stay outlined.
- Segmented controls (T/CT, All/Bans/Mutes/Gags, Recent/Top rated): a sliding
  thumb animates to the selected option (200ms) instead of instant color swaps.
- Focus ring appears instantly (no transition) for accessibility.

Page specifics:
- Leaders: search filters with the previous rows kept until new rows arrive;
  pinned "You" row stays fixed while the table scrolls; row hover 150ms.
- Penalties: switching filter crossfades the table body (150ms); stat tiles
  don't reload when only the filter changes.
- Reviews: star hover previews the rating (stars fill up to the hovered one);
  on Submit the new review appears at the top with a fade/slide-in and the
  composer resets; cooldown error shows inline under the button, no toast spam.
- Skinchanger:
  - T/CT switch: the grid crossfades (150ms out, 200ms in); cards keep their
    positions where the weapon exists on both sides (shared weapons don't move).
  - Card hover: image blur 4px + opacity .55 and the customize button fade in
    (150ms).
  - Dialog: overlay fades in with backdrop blur 10px (200ms); panel fades +
    scales 0.98→1 (250ms); closes in 150ms. Focus moves to the search input.
  - Selecting a skin updates the preview immediately; wear slider updates the
    value/band label live without lag.
  - Save is optimistic: the card shows the new skin at once; on error it
    reverts and shows an inline error. Trash2 removal is optimistic too, with a
    5s "Undo" snackbar.
- Explore: results update as you type (debounced 250ms), keeping the previous
  results visible while loading.

Performance:
- 60fps on a mid-range laptop: no animating width/height/top/left (except the
  sidebar), no large box-shadow animations, will-change only while animating.
- Long lists (skin catalog, leaderboard, penalties) are virtualized or paginated
  (infinite scroll with a sentinel) — never render thousands of nodes.
- Custom thin neutral scrollbars inside panels (8px, --line thumb).

Reduced motion (prefers-reduced-motion): no ticker animation, no hover blur,
no slides/scales — only instant state changes or 1-frame opacity swaps.

Crimson redesign motion (2026-09-27) — the shared classes live in src/index.css:
- Interactive cards (.lx-fx-card: Home modes, Play servers, podium, stat tiles,
  reviews, search results): cascade in on mount (fill-mode `backwards`, never
  `both`, or the finished animation overrides the hover transform); on hover
  they lift 5px with a crimson edge light (.lx-fx-card::before), a pointer
  spotlight (.lx-spotlight fed by --mx/--my) and a crimson shadow. Entering
  hover is quick (≈450ms ease-out); leaving is slow and eased (≈800ms,
  cubic-bezier(0.37, 0, 0.18, 1)) so a card floats back instead of dropping.
  Springy overshoot only on the way in, never on the way out.
- Tailwind v4's scale-*, rotate-* and translate-* set the standalone `scale`,
  `rotate` and `translate` properties: list those in transition-[…], not
  `transform`, or the change snaps. Images that zoom on hover get their own
  compositor layer (.lx-map-img) and never transition `filter`.
- No flash when a hover ends: anything that moves, scales or fades on hover keeps
  its own compositor layer at rest (.lx-layer; .lx-fx-card and .lx-lift do it
  themselves; a global rule covers every hover:/group-hover: translate, scale,
  rotate and opacity utility). Otherwise Chrome promotes it when the hover starts
  and demotes it when the way back ends, re-rasterising it and its images at that
  moment. Resting transforms are translate3d(0,0,0), never none; a hover blur
  rests at blur(0px); infinite decorative animations run only while hovered.
  Containers that overlap lifting cards (the podium, table rows under a sticky
  header, Segmented) get a fixed layer too.
- A card that lifts must not sit flush against the top of a scroll area — leave
  at least 12px of padding above it, or the lift is clipped.
- Table rows (.lx-row-in) cascade in and replay when the data or filters change;
  hover slides in a 3px crimson marker at the left edge and tints the row.
- Content swapped in place (subtitles, counts, the Play hero between modes) uses
  .lx-swap-in / .lx-swap-fade instead of re-mounting the page.
- Page changes: the old page fades up and out in 160ms, then the new one flows
  in (App.tsx renders the shown location through <Routes location>). Moves
  inside one section (the Play modes, URL filters) apply without that fade.
- Side detail panels (.lx-sheet + .lx-sheet-overlay): nearly opaque glass with a
  crimson top line over a blurred page; slide in 450ms ease-out, out 250ms;
  their blocks rise in one after another (.lx-sheet-rise).
- Live dots ping (.lx-live-dot); progress fills carry a light sweep
  (.lx-progress-fill) and grow in once (.lx-bar-grow).

═══ 8. RESPONSIVE ═══
- ≥1280px: layouts as in the references.
- 1024–1279px: sidebar starts collapsed; the Reviews right column drops below.
- <768px: sidebar becomes shadcn's sheet (hamburger in the top bar); kill feed
  shows only the LIVE label + latest entry; tables become stacked cards;
  Skinchanger columns stack into one list grouped by category.

═══ 9. VERIFY (every phase) ═══
- Record a short Playwright video (or trace) of: sidebar toggle, profile menu,
  T/CT switch, skin dialog open/close, filter change. Check nothing jumps,
  flashes or shifts layout.
- Playwright screenshots at 1440x900 of each reference state, side by side with
  the PNGs. Fix visible differences in spacing, size, color or icons first.
- Also screenshot 1280px and 390px widths.
- npm run check:colors passes. tsc + build pass. No console errors.
- Keyboard: Tab reaches every control in order; Esc closes dialogs/menus.
- Final report per phase: what changed, what differs from the reference and why,
  any missing backend data or asset you had to work around.
