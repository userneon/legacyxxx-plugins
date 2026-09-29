#!/usr/bin/env bash
# Keeps copies of the LEGACY-X .env files (API, Discord bot, CS2 plugins) so a deleted or broken file,
# or a reinstalled VPS, never loses them. Copies go to /root/legacyx-env-backups/<date-time>/, readable
# by root only; the newest 30 are kept.
#
#   sudo ./scripts/env-backup.sh run          back up now
#   sudo ./scripts/env-backup.sh on | off     daily backup at 04:30 (systemd timer)
#   sudo ./scripts/env-backup.sh list         what is backed up
#   sudo ./scripts/env-backup.sh restore <backup-folder>   put those files back (current ones saved first)
#   sudo ./scripts/env-backup.sh bundle       one .tar.gz of the newest backup, to copy off the VPS
#
# Other paths: ENV_FILES="/a/.env /b/.env" sudo -E ./scripts/env-backup.sh run
set -euo pipefail

DEST="/root/legacyx-env-backups"
KEEP=30
ENV_FILES="${ENV_FILES:-/root/legacyxxx-backend/.env /root/legacyxxx-discord-bot/.env /home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env}"
UNIT="/etc/systemd/system/legacyx-env-backup"
SELF="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"

say() { printf '==> %s\n' "$*"; }
die() { printf '!! %s\n' "$*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || die "Run with sudo."

cmd_run() {
  umask 077
  mkdir -p "$DEST"; chmod 700 "$DEST"
  local stamp dir copied=0
  stamp="$(date +%Y%m%d-%H%M%S)"
  dir="$DEST/$stamp"
  for f in $ENV_FILES; do
    if [[ -f "$f" ]]; then
      mkdir -p "$dir$(dirname "$f")"
      cp -p "$f" "$dir$f"
      copied=$((copied + 1))
      say "saved $f"
    else
      say "skipped $f (not on this VPS)"
    fi
  done
  [[ $copied -gt 0 ]] || { rmdir "$dir" 2>/dev/null || true; die "No .env file found to back up."; }
  # Keep the newest $KEEP backups.
  ls -1d "$DEST"/20*/ 2>/dev/null | sort | head -n -"$KEEP" | xargs -r rm -rf --
  say "Backup: $dir ($copied files)"
}

cmd_list() {
  ls -1d "$DEST"/20*/ 2>/dev/null | sort | while read -r d; do
    printf '%s  %s\n' "$(basename "$d")" "$(cd "$d" && find . -type f | sed 's#^\.##' | tr '\n' ' ')"
  done
}

cmd_restore() {
  local dir="${1:-}"
  [[ -n "$dir" ]] || die "Usage: $0 restore <backup-folder> (see: $0 list)"
  [[ -d "$dir" ]] || dir="$DEST/$dir"
  [[ -d "$dir" ]] || die "No backup $1 (see: $0 list)"
  say "Saving the current files first"
  ( cmd_run ) || true
  (cd "$dir" && find . -type f) | while read -r rel; do
    local target="${rel#.}"
    mkdir -p "$(dirname "$target")"
    cp -p "$dir$target" "$target"
    say "restored $target"
  done
  say "Done. Restart what uses them: pm2 restart all; sudo systemctl restart 'cs2@*'"
}

cmd_bundle() {
  local newest
  newest="$(ls -1d "$DEST"/20*/ 2>/dev/null | sort | tail -n1)"
  [[ -n "$newest" ]] || die "No backup yet: $0 run"
  local out="$DEST/legacyx-env-$(basename "$newest").tar.gz"
  (umask 077; tar -czf "$out" -C "$newest" .)
  say "Bundle: $out"
  say "Copy it to your own computer: scp root@<VPS-IP>:$out ."
}

cmd_timer() {
  case "${1:-}" in
    on)
      cat > "$UNIT.service" <<EOF
[Unit]
Description=LEGACY-X .env backup

[Service]
Type=oneshot
Environment="ENV_FILES=$ENV_FILES"
ExecStart=$SELF run
EOF
      cat > "$UNIT.timer" <<EOF
[Unit]
Description=LEGACY-X .env backup every day at 04:30

[Timer]
OnCalendar=*-*-* 04:30:00
Persistent=true

[Install]
WantedBy=timers.target
EOF
      systemctl daemon-reload
      systemctl enable --now legacyx-env-backup.timer >/dev/null
      say "Daily .env backup on (04:30). Log: journalctl -u legacyx-env-backup"
      ;;
    off)
      systemctl disable --now legacyx-env-backup.timer >/dev/null 2>&1 || true
      say "Daily .env backup off."
      ;;
  esac
}

case "${1:-}" in
  run) cmd_run ;;
  list) cmd_list ;;
  restore) shift; cmd_restore "$@" ;;
  bundle) cmd_bundle ;;
  on|off) cmd_timer "$1" ;;
  *) sed -n '2,/^set -euo/{/^set -euo/!p}' "$0"; exit 1 ;;
esac
