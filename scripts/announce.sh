#!/usr/bin/env bash
# Posts what an update changed to a Discord channel, from facts only: the subjects of the commits it
# brought, exactly as written, and lines the caller passes (a CS2 build, a CounterStrikeSharp version).
# Nothing is generated or translated. Commits that only touch docs/ or *.md, and commits whose message
# contains [skip announce], are left out; when nothing is left, nothing is posted.
#
#   sudo ./scripts/announce.sh setup          save the API address and an announce token (typed, not shown), then test
#   sudo ./scripts/announce.sh setup-webhook  instead: save a Discord channel webhook (typed, not shown), then test
#   sudo ./scripts/announce.sh test           post a test message
#   ./scripts/announce.sh --title "Website updated" [--commits <repo-dir> <from> <to>] [--line "text"]…
#                         [--footer "text"] [--banner cs2-update-finished] [--dry-run]
#
# The messages go to the LEGACY-X API, and the Discord bot posts them in the channel chosen with /updates
# (the bot also draws the banner). With only a webhook saved they are posted straight to that channel instead.
# The settings live in /etc/legacyx/announce.env (root only). Without them, or when the API or Discord cannot
# be reached, nothing is posted and the update that called this is not affected: this always exits 0.
# The token: node --env-file=.env scripts/create-api-token.mjs legacyx-announce announce:write (legacyxxx-backend).
# The ops/deploy.sh of legacyxxx-backend, -discord-bot and -frontend, and cs2-host.sh, call it.
set -uo pipefail

CONF="${LEGACYX_ANNOUNCE_CONF:-/etc/legacyx/announce.env}"
WEBHOOK_PATTERN='^https://(canary\.|ptb\.)?discord(app)?\.com/api/webhooks/[0-9]+/[A-Za-z0-9_-]+$'

say() { printf '==> Discord: %s\n' "$*"; }

BANNER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/assets"
setting() { [[ -r "$CONF" ]] && sed -n "s/^$1=//p" "$CONF" | tail -n1 | tr -d '\r\n ' || true; }

webhook() {
  local url=""
  [[ -r "$CONF" ]] && url="$(sed -n 's/^DISCORD_ANNOUNCE_WEBHOOK=//p' "$CONF" | tail -n1 | tr -d '\r\n ')"
  [[ "$url" =~ $WEBHOOK_PATTERN ]] && printf '%s' "$url"
}

# Subjects of the commits from..to that change more than documentation, oldest first.
commit_subjects() {
  local dir="$1" from="$2" to="$3"
  [[ -n "$from" && -n "$to" && "$from" != "$to" ]] || return 0
  git -c safe.directory="$dir" -C "$dir" log --no-merges --reverse -i --invert-grep --grep='\[skip announce\]' \
    --format='%s' "$from..$to" -- . ':(exclude)docs' ':(exclude)*.md' 2>/dev/null
}

# One embed: the title, a bullet per line (Discord markdown escaped, no mentions), a footer and the time.
payload() {
  python3 - "$1" "$2" "$3" "${4:-}" <<'PY'
import datetime, json, re, sys
title, footer, body, image = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
lines = [line.strip() for line in body.split("\n") if line.strip()]
escape = lambda text: re.sub(r"([\\*_~`|>])", r"\\\1", text)
shown = lines[:15]
description = "\n".join("• " + escape(line[:200]) for line in shown)
if len(lines) > len(shown):
    description += f"\nand {len(lines) - len(shown)} more."
embed = {"title": title[:256], "description": description[:4000], "color": 0xE11D48,
         "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat()}
if image:
    embed["image"] = {"url": "attachment://banner.png"}
if footer:
    embed["footer"] = {"text": footer[:200]}
print(json.dumps({"username": "LEGACY-X", "allowed_mentions": {"parse": []}, "embeds": [embed]}))
PY
}

# Through the API: the bot posts it. Lines go as written; the bot escapes them.
post_api() {
  local api="$1" token="$2" title="$3" footer="$4" body="$5" banner="$6" dry="$7" json
  json="$(python3 - "$title" "$footer" "$body" "$banner" <<'PY'
import json, sys
title, footer, body, banner = sys.argv[1:5]
lines = [line.strip()[:200] for line in body.split("\n") if line.strip()][:30]
note = {"title": title[:80], "lines": lines}
if footer:
    note["footer"] = footer[:200]
if banner:
    note["banner"] = banner
print(json.dumps(note))
PY
)" || { say "could not build the message; not posted."; return 0; }
  if [[ "$dry" == yes ]]; then printf '%s\n' "$json"; return 0; fi
  # The address and token go to curl on stdin, so they never show in the process list or in an error message.
  if printf 'url = "%s/api/v1/plugin/announcements"\nheader = "authorization: Bearer %s"\n' "${api%/}" "$token" \
      | curl -fsS -m 15 -o /dev/null -H 'content-type: application/json' --data-binary "$json" -K - 2>/dev/null; then
    say "sent \"$title\" to the bot."
  else
    say "the API did not accept the message; the update itself is done."
  fi
}

post() {
  local title="$1" footer="$2" body="$3" dry="$4" image="${5:-}" banner="${6:-}" url json api token
  command -v python3 >/dev/null || { say "python3 is missing; not posted."; return 0; }
  api="$(setting LEGACYX_ANNOUNCE_API_URL)"
  token="$(setting LEGACYX_ANNOUNCE_TOKEN)"
  if [[ -n "$api" && -n "$token" ]]; then post_api "$api" "$token" "$title" "$footer" "$body" "$banner" "$dry"; return 0; fi
  [[ -z "$image" && -f "$BANNER_DIR/$banner.png" ]] && image="$BANNER_DIR/$banner.png"
  [[ -n "$image" && ! -r "$image" ]] && image=""
  json="$(payload "$title" "$footer" "$body" "$image")" || { say "could not build the message; not posted."; return 0; }
  if [[ "$dry" == yes ]]; then printf '%s\n' "$json"; return 0; fi
  url="$(webhook)"
  if [[ -z "$url" ]]; then
    if [[ -e "$CONF" && ! -r "$CONF" ]]; then say "cannot read $CONF (run this as root); not posted."
    else say "no webhook set (sudo $0 setup); not posted."; fi
    return 0
  fi
  # The URL goes to curl on stdin, so it never shows in the process list or in an error message.
  local form=(-H 'content-type: application/json' --data-binary "$json")
  # With a banner the message goes as a form: the JSON plus the picture it points to.
  if [[ -n "$image" ]]; then form=(-F "payload_json=$json" -F "files[0]=@$image;filename=banner.png;type=image/png"); fi
  if printf 'url = "%s"\n' "$url" | curl -fsS -m 20 -o /dev/null "${form[@]}" -K - 2>/dev/null; then
    say "posted \"$title\"."
  else
    say "Discord did not accept the message; the update itself is done."
  fi
}

case "${1:-}" in
  setup)
    [[ $EUID -eq 0 ]] || { echo "!! Run with sudo." >&2; exit 1; }
    echo "The LEGACY-X API address (e.g. https://api.legacyx.cc) and a token with the announce:write scope."
    read -rp "API address: " api
    [[ "$api" =~ ^https://[A-Za-z0-9.-]+(:[0-9]+)?/?$ ]] || { echo "!! That is not an https address." >&2; exit 1; }
    read -rsp "Token: " token; echo
    [[ "$token" =~ ^[A-Za-z0-9_-]{20,}$ ]] || { echo "!! That does not look like a token." >&2; exit 1; }
    mkdir -p "$(dirname "$CONF")"
    old="$(sed -n 's/^DISCORD_ANNOUNCE_WEBHOOK=//p' "$CONF" 2>/dev/null | tail -n1)"
    (umask 077; { printf 'LEGACYX_ANNOUNCE_API_URL=%s\nLEGACYX_ANNOUNCE_TOKEN=%s\n' "${api%/}" "$token"; [[ -n "$old" ]] && printf 'DISCORD_ANNOUNCE_WEBHOOK=%s\n' "$old"; true; } > "$CONF")
    chmod 600 "$CONF"
    say "saved to $CONF. Pick the channel with /updates in Discord."
    post "Update announcements are on" "" "Website, API, Discord bot and game server updates are posted here." no
    exit 0
    ;;
  setup-webhook)
    [[ $EUID -eq 0 ]] || { echo "!! Run with sudo." >&2; exit 1; }
    echo "Discord: channel settings > Integrations > Webhooks > New Webhook > Copy Webhook URL."
    read -rsp "Webhook URL: " url; echo
    [[ "$url" =~ $WEBHOOK_PATTERN ]] || { echo "!! That is not a Discord webhook URL." >&2; exit 1; }
    mkdir -p "$(dirname "$CONF")"
    (umask 077; printf 'DISCORD_ANNOUNCE_WEBHOOK=%s\n' "$url" > "$CONF")
    chmod 600 "$CONF"
    say "saved to $CONF."
    post "Update announcements are on" "" "Website, API, Discord bot and game server updates are posted here." no
    exit 0
    ;;
  test)
    post "Update announcements are on" "" "Website, API, Discord bot and game server updates are posted here." no
    exit 0
    ;;
esac

title="" footer="" image="" banner="" dry=no lines=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --title) title="${2:-}"; shift 2 ;;
    --footer) footer="${2:-}"; shift 2 ;;
    --image) image="${2:-}"; shift 2 ;;
    --banner) banner="${2:-}"; shift 2 ;;
    --line) [[ -n "${2:-}" ]] && lines+=("$2"); shift 2 ;;
    --commits)
      while IFS= read -r subject; do [[ -n "$subject" ]] && lines+=("$subject"); done \
        < <(commit_subjects "${2:-}" "${3:-}" "${4:-}")
      shift 4 ;;
    --dry-run) dry=yes; shift ;;
    *) sed -n '2,15p' "$0" >&2; exit 0 ;;
  esac
done
[[ -n "$title" ]] || { sed -n '2,15p' "$0" >&2; exit 0; }
if [[ ${#lines[@]} -eq 0 ]]; then
  say "nothing to announce (no code changes)."
  exit 0
fi
post "$title" "$footer" "$(printf '%s\n' "${lines[@]}")" "$dry" "$image" "$banner"
exit 0
