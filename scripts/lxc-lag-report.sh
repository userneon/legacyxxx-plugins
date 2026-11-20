#!/usr/bin/env bash
# LXC lag / disconnect report: ONE read-only look at why a game server freezes (owner request 2026-11-20).
#   sudo bash scripts/lxc-lag-report.sh [service] ["since"] | tee /tmp/lxc-lag-report.txt
#   defaults: service cs2@27015, since "7 days ago"
# Changes nothing, restarts nothing. Player names, SteamIDs and IP addresses are replaced in the output; secrets (.env, GSLT, tokens) are never read or printed.
set -u
UNIT="${1:-cs2@27015}"
SINCE="${2:-7 days ago}"
PLUGINS_DIR="${PLUGINS_DIR:-/home/cs2/cs2/game/csgo/addons/counterstrikesharp/plugins}"

san() {
  sed -E 's/"[^"]*<[0-9]+><[^>]*><[^>]*>"/"<player>"/g; s/7656119[0-9]{10}/<steamid>/g; s/\[?U:1:[0-9]+\]?/<steamid>/g; s/([0-9]{1,3}\.){3}[0-9]{1,3}(:[0-9]+)?/<ip>/g'
}
strip() { sed -E 's/^[0-9.]+ [^ ]+ [^ ]+: //'; }
section() { printf '\n===== %s =====\n' "$1"; }
jr() { journalctl -u "$UNIT" --no-pager --since "$SINCE" "$@" 2>/dev/null; }

echo "LXC lag report  unit=$UNIT  since=\"$SINCE\"  now(UTC)=$(date -u '+%F %T')"

section "1. history"
echo "first: $(jr -o short-iso | head -1 | san | cut -c1-160)"
echo "last : $(jr -o short-iso | tail -1 | san | cut -c1-160)"
echo "lines: $(jr | wc -l)"

section "2. starts / exits per day (UTC)"
printf '%-11s %7s %7s %9s %8s\n' day started stopped status139 sigkill
jr -o short-iso | awk '
  { d=substr($1,1,10) }
  /Started /                      { s[d]++; days[d]=1 }
  /Stopped /                      { t[d]++; days[d]=1 }
  /status=139|SEGV/               { c[d]++; days[d]=1 }
  /SIGKILL/                       { k[d]++; days[d]=1 }
  END { for (d in days) printf "%-11s %7d %7d %9d %8d\n", d, s[d], t[d], c[d], k[d] }' | sort

section "3. SERVER SLOW lines (plugin LagMonitor: game time slower than real time)"
echo "count: $(jr | grep -c 'SERVER SLOW')"
jr -o short-iso | grep 'SERVER SLOW' | tail -25 | san | cut -c1-220

section "4. minutes with the most disconnect / timeout lines (top 15)"
jr -o short-iso | grep -i -E 'disconnect|timed out|timeout|connection lost|dropped' | cut -c1-16 | sort | uniq -c | sort -rn | head -15

section "5. the 15 longest log gaps (the server printed nothing for 5+ s)"
jr -o short-unix | awk '{ ts=$1; if (pts!="" && ts-pts>=5) printf "%s\t%.1f\t%s\t%s\n", pts, ts-pts, substr(prev,1,220), substr($0,1,220); pts=ts; prev=$0 }' \
 | sort -t"$(printf '\t')" -k2 -rn | head -15 \
 | while IFS="$(printf '\t')" read -r pts gap before after; do
     printf '%s UTC  gap=%ss\n  before: %s\n  after : %s\n' "$(date -u -d "@${pts%.*}" '+%F %T')" "$gap" "$(printf '%s' "$before" | strip | san)" "$(printf '%s' "$after" | strip | san)"
   done

section "6. our plugin lines in the 24 h (counts)"
for k in 'Agents:' 'Knife subclass' 'Weapon models' 'Could not precache' 'Spawned' 'CCSCustomHudLayout' 'serializer' 'PackEntity' 'overflow'; do
  printf '%-22s %s\n' "$k" "$(journalctl -u "$UNIT" --no-pager --since '24 hours ago' 2>/dev/null | grep -c -- "$k")"
done

section "7. the 40 lines before the latest segfault / exit 139"
LAST139="$(jr -o short-iso | grep -n -E 'status=139|SEGV|Segmentation' | tail -1 | cut -d: -f1)"
if [ -n "${LAST139:-}" ]; then jr -o short-iso | head -n "$LAST139" | tail -40 | san | cut -c1-230; else echo "none in this period"; fi

section "8. resources now"
uptime; echo "cores: $(nproc)"; free -m | head -2; df -h / | tail -1
echo "-- vmstat 1 10 (st = CPU stolen by the host, wa = waiting for disk; busy: us/sy)"; vmstat 1 10 2>/dev/null
echo "-- top processes by CPU"; ps -eo pid,pcpu,pmem,etime,comm --sort=-pcpu 2>/dev/null | head -8

section "9. network"
ping -c 30 -i 0.5 -q 8.8.8.8 2>/dev/null | tail -3
ip -s link 2>/dev/null | awk '/^[0-9]+:/ {name=$2} /RX:/ {getline; rx=$0} /TX:/ {getline; print name, "RX:", rx, " TX:", $0}' | head -6
timedatectl 2>/dev/null | grep -E 'Local time|synchronized|NTP service' | head -3

section "10. what could use the CPU / disk (names only, never contents)"
systemctl list-timers --all --no-pager 2>/dev/null | head -14
echo "-- cron.d:    $(ls /etc/cron.d 2>/dev/null | tr '\n' ' ')"
echo "-- cron.daily: $(ls /etc/cron.daily 2>/dev/null | tr '\n' ' ')"

section "11. demos and big folders"
find /home -name '*.dem' -printf '%s\n' 2>/dev/null | awk '{n++; s+=$1} END {printf "demo files: %d, total %.1f GB\n", n, s/1e9}'
du -sh /var/log/journal 2>/dev/null
du -sh "$PLUGINS_DIR/../logs" 2>/dev/null

section "12. which build is running (file dates only)"
ls -l --time-style=long-iso "$PLUGINS_DIR"/*/LegacyX-Community.dll "$PLUGINS_DIR"/*/LegacyX-Hud.dll 2>/dev/null | awk '{print $6, $7, $8}'
jr -o short-iso | grep -E 'Started ' | tail -5 | san | cut -c1-120

echo; echo "END OF REPORT"
