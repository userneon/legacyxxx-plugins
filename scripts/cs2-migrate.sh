#!/usr/bin/env bash
# Moves the LEGACY-X CS2 servers to another VPS. Only what is yours travels: the plugins' .env and each
# server's config (port, GSLT, map). CS2, Metamod, CounterStrikeSharp and the plugins are installed fresh
# on the new VPS by cs2-host.sh, which also writes gameinfo.gi: never copy gameinfo.gi (a copy from an
# older CS2 build would break the new install). Bans, accounts and skins live in the database, nothing to move.
#
#   OLD VPS:  sudo ./scripts/cs2-migrate.sh pack                  -> /root/legacyx-cs2-move.tar.gz (secrets, mode 600)
#   copy it:  scp /root/legacyx-cs2-move.tar.gz root@NEW:/root/    (delete it from both machines afterwards)
#   NEW VPS:  git clone the plugins repo, then
#             sudo ./scripts/cs2-host.sh install                      # no --env: unpack brings the .env
#             sudo ./scripts/cs2-migrate.sh unpack /root/legacyx-cs2-move.tar.gz <NEW public IP or hostname>
#   Stop the servers on the OLD VPS first: one GSLT runs on one machine at a time.
set -euo pipefail

CS2_USER="${CS2_USER:-cs2}"
CSGO_DIR="/home/$CS2_USER/cs2/game/csgo"
ENV_TARGET="$CSGO_DIR/addons/counterstrikesharp/.env"
CONF_DIR="/etc/legacyx/cs2"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

say() { printf '==> %s\n' "$*"; }
die() { printf '!! %s\n' "$*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || die "Run with sudo."

cmd_pack() {
  local out="${1:-/root/legacyx-cs2-move.tar.gz}" tmp
  [[ -e "$ENV_TARGET" ]] || die "No $ENV_TARGET on this VPS."
  ls "$CONF_DIR"/*.conf >/dev/null 2>&1 || die "No server configs in $CONF_DIR."
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
  umask 077
  mkdir -p "$tmp/conf"
  cp -L "$ENV_TARGET" "$tmp/plugins.env"
  cp "$CONF_DIR"/*.conf "$tmp/conf/"
  [[ -f /etc/legacyx/announce.env ]] && cp /etc/legacyx/announce.env "$tmp/announce.env"
  tar -czf "$out" -C "$tmp" .
  chmod 600 "$out"
  say "Packed $out"
  say "Servers: $(ls "$CONF_DIR" | sed -n 's/\.conf$//p' | tr '\n' ' ')"
  say "Next: stop them here (sudo systemctl stop 'cs2@*'), copy the file to the new VPS, run unpack there."
}

cmd_unpack() {
  local file="${1:-}" host="${2:-}" tmp port
  [[ -f "$file" && -n "$host" ]] || die "Usage: unpack <file.tar.gz> <new public IP or hostname>"
  [[ "$host" =~ ^[A-Za-z0-9.-]+$ ]] || die "Host: letters, digits, dots and dashes only."
  [[ -d "$CSGO_DIR" ]] || die "CS2 is not installed here: run cs2-host.sh install first."
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
  tar -xzf "$file" -C "$tmp"
  umask 077
  # The plugins' .env, pointed at this machine. If cs2-host.sh env linked it to the repository's .env, write there.
  local dest="$ENV_TARGET"
  [[ -L "$dest" ]] && dest="$(readlink -f "$dest")"
  [[ -f "$dest" ]] && cp -p "$dest" "$dest.before-move"
  install -m 600 "$tmp/plugins.env" "$dest"
  if grep -q '^LEGACYX_SERVER_HOST=' "$dest"; then sed -i "s|^LEGACYX_SERVER_HOST=.*|LEGACYX_SERVER_HOST=$host|" "$dest"; else echo "LEGACYX_SERVER_HOST=$host" >> "$dest"; fi
  chown "$CS2_USER:$CS2_USER" "$dest" 2>/dev/null || true
  say "Plugins .env restored, LEGACYX_SERVER_HOST=$host"
  mkdir -p "$CONF_DIR"; install -m 600 "$tmp"/conf/*.conf "$CONF_DIR/"
  if [[ -f "$tmp/announce.env" ]]; then mkdir -p /etc/legacyx; install -m 600 "$tmp/announce.env" /etc/legacyx/announce.env; fi
  # gameinfo.gi is written by the install; make sure Metamod is in it (an update may have replaced it).
  if ! grep -q 'csgo/addons/metamod' "$CSGO_DIR/gameinfo.gi"; then
    sed -i '/Game_LowViolence[[:space:]]*csgo_lv/i\			Game	csgo/addons/metamod' "$CSGO_DIR/gameinfo.gi"
    say "Metamod hooked into gameinfo.gi"
  fi
  for conf in "$CONF_DIR"/*.conf; do
    port="$(basename "$conf" .conf)"
    [[ "$port" =~ ^[0-9]+$ ]] || continue
    command -v ufw >/dev/null && ufw status | grep -q active && { ufw allow "$port"/udp >/dev/null; ufw allow "$port"/tcp >/dev/null; }
    systemctl enable --now "cs2@$port" >/dev/null && say "cs2@$port started"
  done
  say "Done. The API token in the .env still works (the host is only a label). Check: $REPO/scripts/cs2-host.sh status"
  say "Delete the move file on both machines. Then point the website's server address to $host if it is shown anywhere by hand."
}

case "${1:-}" in
  pack) shift; cmd_pack "$@" ;;
  unpack) shift; cmd_unpack "$@" ;;
  *) sed -n '2,13p' "$0"; exit 1 ;;
esac
