#!/usr/bin/env bash
# LEGACY-X server check (owner request 2026-11-15): one read-only run that says how the VPS, the CS2 service, the Workshop addon, the plugins and the logs are.
# Nothing is changed and no secret is printed (tokens are shown as present/absent and their length).
#   sudo ./scripts/server-check.sh [port] [hours of log, default 2] | tee /tmp/lx-check.txt
PORT="${1:-27015}"
HOURS="${2:-2}"
UNIT="cs2@${PORT}"
CS2_HOME="${CS2_HOME:-/home/cs2}"
CS2_DIR="$CS2_HOME/cs2"
CSGO="$CS2_DIR/game/csgo"
WS_ID="${WS_ID:-3830785234}"
WS_DIR="$CS2_DIR/game/bin/linuxsteamrt64/steamapps/workshop/content/730/$WS_ID"
SINCE="${HOURS} hours ago"

h() { printf '\n==== %s ====\n' "$*"; }
journal() { journalctl -u "$UNIT" --since "$SINCE" --no-pager 2>/dev/null; }
count() { journal | grep -cE "$1"; }

h "1. System"
date -u '+%F %T UTC'
uptime
free -h | sed -n '1,2p'
df -h / "$CS2_HOME" 2>/dev/null | awk '!seen[$0]++'
timedatectl 2>/dev/null | grep -E "synchronized|NTP service|Time zone"
echo "cpu cores: $(nproc)"

h "2. Service $UNIT"
systemctl is-active "$UNIT"
systemctl show "$UNIT" -p ActiveEnterTimestamp -p NRestarts -p MainPID -p MemoryCurrent 2>/dev/null
echo "cs2 processes: $(pgrep -fc 'cs2' || true)"
echo "-- unit line (token hidden):"
systemctl cat "$UNIT" 2>/dev/null | grep -E "ExecStart|EnvironmentFile" | sed -E 's/(sv_setsteamaccount )[^ ]+/\1<hidden>/'
echo "-- restarts / starts in the log window:"
journal | grep -cE "Started .*CS2|Started LEGACY-X"

h "3. Versions"
grep -E "PatchVersion|ClientVersion|ServerVersion" "$CSGO/steam.inf" 2>/dev/null
ls "$CSGO/addons" 2>/dev/null | tr '\n' ' '; echo
ls "$CSGO/addons/counterstrikesharp/plugins" 2>/dev/null | tr '\n' ' '; echo
journal | grep -E "MultiAddonManager\] (v|Loaded|Version)|Metamod:Source version" | tail -3

h "4. Settings (names only, secrets hidden)"
for f in "$CS2_HOME"/*.env /root/legacyxxx-plugins/.env /etc/legacyx/*.env; do
  [ -f "$f" ] || continue
  echo "file: $f"
  grep -E "^[A-Z0-9_]+=" "$f" | sed -E 's/^([A-Z0-9_]+)=(.*)$/\1 (length \2)/' | awk '{ n=$0; sub(/ \(length .*/,"",n); v=$0; sub(/^[^(]*\(length /,"",v); sub(/\)$/,"",v); print n " = " (length(v) ? "set, " length(v) " chars" : "empty") }' | grep -E "LEGACYX_|GSLT|WORKSHOP|MAXPLAYERS|MAP|GAME_ARGS|API" | head -60
done

h "5. Workshop addon $WS_ID on the server"
if [ -d "$WS_DIR" ]; then
  ls -la --time-style=full-iso "$WS_DIR"
  total=$(find "$WS_DIR" -maxdepth 1 -type f -printf '%s\n' | awk '{s+=$1} END {print s}')
  echo "sum of files: $total bytes = $(awk -v t="$total" 'BEGIN{printf "%.3f", t/1e6}') MB (the Steam page shows this number)"
  for name in legacyx_ready legacyx_bomb legacyx_board vdata_c katana_blood apex_karambit apex_balisong; do
    printf '  %-16s %s\n' "$name" "$(strings "$WS_DIR/${WS_ID}_dir.vpk" 2>/dev/null | grep -c "$name") hits in the VPK index"
  done
else
  echo "NOT DOWNLOADED: $WS_DIR does not exist"
fi
echo "-- MultiAddonManager cfg:"
grep -E "^mm_" "$CSGO/cfg/multiaddonmanager/multiaddonmanager.cfg" 2>/dev/null
echo "-- MultiAddonManager log lines:"
journal | grep "MultiAddonManager" | grep -vE "Downloading addon .*MB" | tail -12

h "6. Steam login and players being dropped (last ${HOURS} h)"
echo "logged on to Steam:        $(count 'logged on to Steam')"
echo "STEAMAUTH failures:        $(count 'STEAMAUTH')"
echo "STEAM_LOGON drops:         $(count 'NETWORK_DISCONNECT_STEAM_LOGON')"
echo "timed out:                 $(count 'NETWORK_DISCONNECT_TIMEDOUT')"
echo "kicked by plugins/console: $(count 'NETWORK_DISCONNECT_KICKED')"
echo "-- who failed, and how often:"
journal | grep "STEAMAUTH: Client" | sed -E 's/.*Client (.*) received failure code ([0-9]+).*/\1 code \2/' | sort | uniq -c | sort -rn | head -10
echo "(Steam auth code 8 = ticket invalid, 4 = logged in elsewhere, 3 = VAC, 2 = no licence)"

h "7. Plugins: loops, agents, ready card, knives"
echo "Weapon model lines:        $(count 'Weapon model .* put on')   (hundreds per minute = a loop)"
echo "Knife subclass lines:      $(count 'Knife subclass')"
echo "Peak lines in one minute:  $(journal | grep -E 'Weapon model .* put on|Knife subclass' | cut -c1-12 | sort | uniq -c | sort -rn | head -1)"
echo "-- Weapon models / Agents start-up lines:"
journal | grep -E "Weapon models:|\] Agents:|Agent .* is now" | tail -8
echo "-- ready card / bomb / HUD:"
journal | grep -E "ready card|Spawned .*vxml|bomb banner" | tail -6
echo "-- LegacyX plugin load lines:"
journal | grep -E "\[LEGACY-X [A-Za-z]+\] (Ready|Loaded|Disabled)|LegacyX.*loaded" | tail -12

h "8. Errors (distinct, with counts)"
journal | grep -iE "error|exception|failed|fatal|segfault|assert" | grep -vE "Failed loading resource|texturebase.cpp|Failed single interp|Failed loading mesh" | sed -E 's/^[A-Z][a-z]{2} [0-9 ]+[0-9:]+ [^ ]+ [^ ]+: //' | cut -c1-150 | sort | uniq -c | sort -rn | head -15
echo "-- resources the server could not load (only the number):"
journal | grep -c "Failed loading resource"
journal | grep "Failed loading resource" | sed -E 's/.*resource "([^"]+)".*/\1/' | sed -E 's#/[^/]*$##' | sort | uniq -c | sort -rn | head -8
echo "-- crash dumps / segfaults:"
coredumpctl list --no-pager 2>/dev/null | tail -3
dmesg 2>/dev/null | grep -iE "segfault|out of memory|killed process" | tail -3

h "9. Network"
ss -ulpn 2>/dev/null | grep -E ":${PORT}\b" | head -3
(ufw status 2>/dev/null | head -8) || true
echo "public ip: $(curl -s --max-time 4 https://ifconfig.me 2>/dev/null)"

h "10. Last 15 plugin/game lines (without the repeated weapon-model lines)"
journal | grep -vE "Weapon model .* put on|Knife subclass .* put on|Quality:|Dropped  |Sent  |Received  " | tail -15 | cut -c1-200

echo; echo "==== done: $(date -u '+%F %T UTC') ===="
