#!/usr/bin/env bash
# LEGACY-X CS2 host: runs several CS2 servers on one Linux VPS (Ubuntu 22.04/24.04, x86_64),
# all from one shared install, each as its own systemd service named by its port.
#
#   sudo ./scripts/cs2-host.sh install --env legacyx-srv-HOST.env [--package legacyx-cs2.zip]
#       steamcmd, the CS2 dedicated server, Metamod:Source, CounterStrikeSharp, MultiAddonManager, the LEGACY-X plugins
#       and the .env from create-game-server.mjs (legacyxxx-backend). Safe to run again.
#   sudo ./scripts/cs2-host.sh add <port> <GSLT> [competitive|wingman|casual|deathmatch] [map] [maxplayers] [site-mode] [name…]
#       a server on <port>: config, firewall, systemd service, started now and at boot. site-mode picks the
#       website Play page (competitive_5v5, fun, proleague, tournaments; default from the .env) and name
#       the name shown there; both are written to the .env as LEGACYX_<port>_SERVER_MODE / _NAME.
#       GSLT: a Steam game server login token, one per server (steamcommunity.com/dev/managegameservers, app 730).
#   sudo ./scripts/cs2-host.sh update [--package legacyx-cs2.zip]
#       stops the servers, updates CS2, Metamod, CounterStrikeSharp, MultiAddonManager and the plugins, starts them again.
#   sudo ./scripts/cs2-host.sh deploy
#       git pull of this repository first, then update: new plugin commits live now, not at 05:00.
#   sudo ./scripts/cs2-host.sh env
#       copy this repository's .env (edit it with `nano .env`; deploy and update copy it too) to where the
#       plugins read it, then restart the servers you changed.
#   sudo ./scripts/cs2-host.sh autoupdate on|off|check
#       every 10 minutes (on by default after install): a new CS2 build, and the CounterStrikeSharp release
#       that follows it, are applied at once; other updates (plugins from git, CounterStrikeSharp alone)
#       wait for AUTOUPDATE_HOUR (default 5, local time). Log: journalctl -u legacyx-cs2-autoupdate
#   sudo ./scripts/cs2-host.sh client-addons [id,id,...]
#       Workshop addon IDs every player downloads on connect (MultiAddonManager mm_client_extra_addons),
#       e.g. the custom Panorama UI. No IDs prints the current value; "none" clears it. Restart the servers after.
#   sudo ./scripts/cs2-host.sh remove <port>        stops and deletes that server's service and config
#   sudo ./scripts/announce.sh setup                 post what each update changed to a Discord channel
#   ./scripts/cs2-host.sh status | logs <port>      what is running / that server's console
#
# Each server knows itself by -port (LegacyX: id srv-<port>, address LEGACYX_SERVER_HOST:<port>), so
# one install and one .env serve them all. About 2-3 GB RAM per running server.
set -euo pipefail
 
CS2_USER="${CS2_USER:-cs2}"
CS2_HOME="/home/$CS2_USER"
STEAMCMD_DIR="$CS2_HOME/steamcmd"
CS2_DIR="$CS2_HOME/cs2"
CSGO_DIR="$CS2_DIR/game/csgo"
CONF_DIR="/etc/legacyx/cs2"
UNIT="/etc/systemd/system/cs2@.service"
STATE_DIR="/var/lib/legacyx/cs2"
AUTOUPDATE_UNIT="/etc/systemd/system/legacyx-cs2-autoupdate"
# Updates that are not forced by a CS2 release (plugins, a newer CounterStrikeSharp) wait for this
# local hour so they do not cut matches short.
AUTOUPDATE_HOUR="${AUTOUPDATE_HOUR:-5}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
 
say() { printf '==> %s\n' "$*"; }
die() { printf '!! %s\n' "$*" >&2; exit 1; }
need_root() { [[ $EUID -eq 0 ]] || die "Run with sudo."; }
# As the cs2 user, in its home, with its HOME (steamcmd writes there).
as_cs2() { runuser -u "$CS2_USER" -- env HOME="$CS2_HOME" bash -c 'cd "$HOME" && exec "$@"' _ "$@"; }
ports() { ls "$CONF_DIR" 2>/dev/null | sed -n 's/^\([0-9]\+\)\.conf$/\1/p' | sort -n; }
 
install_packages() {
  local build_tools="$1"
  say "System packages"
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq curl ca-certificates unzip tar git python3 lib32gcc-s1 lib32stdc++6 >/dev/null
  if [[ "$build_tools" == yes ]]; then
    # Building the plugins here needs .NET 8 SDK and Node 18+ (Ubuntu 24.04 has both; else use --package).
    command -v dotnet >/dev/null || apt-get install -y -qq dotnet-sdk-8.0 >/dev/null || die "No dotnet-sdk-8.0 package here: pass --package legacyx-cs2.zip."
    command -v node >/dev/null || apt-get install -y -qq nodejs >/dev/null || true
    local node_major
    node_major="$(node -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0)"
    [[ "$node_major" -ge 18 ]] || die "Node 18+ is needed to package the plugins (found ${node_major}): pass --package legacyx-cs2.zip."
  fi
  id "$CS2_USER" >/dev/null 2>&1 || useradd --create-home --shell /bin/bash "$CS2_USER"
}
 
install_steamcmd() {
  if [[ ! -x "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    say "steamcmd"
    as_cs2 mkdir -p "$STEAMCMD_DIR"
    curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | as_cs2 tar -xz -C "$STEAMCMD_DIR"
  fi
}
 
update_cs2() {
  say "CS2 dedicated server (first time about 35 GB, takes a while)"
  as_cs2 "$STEAMCMD_DIR/steamcmd.sh" +force_install_dir "$CS2_DIR" +login anonymous +app_update 730 validate +quit
  # CS2 looks for the Steam client library here.
  as_cs2 mkdir -p "$CS2_HOME/.steam/sdk64"
  as_cs2 ln -sf "$STEAMCMD_DIR/linux64/steamclient.so" "$CS2_HOME/.steam/sdk64/steamclient.so"
  [[ -f "$CSGO_DIR/gameinfo.gi" ]] || die "CS2 did not install: $CSGO_DIR/gameinfo.gi is missing."
}
 
# CS2 updates rewrite gameinfo.gi; without this line Metamod (and every plugin) is not loaded.
patch_gameinfo() {
  local gi="$CSGO_DIR/gameinfo.gi"
  if ! grep -q 'csgo/addons/metamod' "$gi"; then
    sed -i '/Game_LowViolence[[:space:]]*csgo_lv/i\			Game	csgo/addons/metamod' "$gi"
    grep -q 'csgo/addons/metamod' "$gi" || die "Could not add Metamod to $gi (its format changed?)."
    say "Metamod hooked into gameinfo.gi"
  fi
}
 
install_metamod() {
  say "Metamod:Source 2.0"
  local name
  name="$(curl -fsSL https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-linux)"
  [[ "$name" =~ ^mmsource-[A-Za-z0-9.+-]+-linux\.tar\.gz$ ]] || die "Unexpected Metamod file name: $name"
  curl -fsSL "https://mms.alliedmods.net/mmsdrop/2.0/$name" | as_cs2 tar -xz -C "$CSGO_DIR"
  patch_gameinfo
}
 
latest_css_release() { curl -fsSL https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest; }
release_tag() { grep -oE '"tag_name": *"[^"]*"' | head -n1 | sed 's/.*"\([^"]*\)"$/\1/'; }
 
install_counterstrikesharp() {
  say "CounterStrikeSharp (with .NET runtime)"
  local release url tag tmp
  release="$(latest_css_release)"
  url="$(printf '%s' "$release" \
    | grep -oE '"browser_download_url": *"[^"]*with-runtime[^"]*linux[^"]*\.zip"' | head -n1 | sed 's/.*"\(https[^"]*\)"/\1/')"
  tag="$(printf '%s' "$release" | release_tag || true)"
  [[ -n "$url" ]] || die "Could not find the CounterStrikeSharp Linux release."
  tmp="$(mktemp -d)"
  curl -fsSL "$url" -o "$tmp/css.zip"
  unzip -oq "$tmp/css.zip" -d "$tmp/css"
  # The zip holds addons/…; merge it into csgo without touching configs that already exist.
  cp -an "$tmp/css/addons/." "$CSGO_DIR/addons/" 2>/dev/null || true
  # Engine-facing parts are replaced on every run (a CS2 update usually needs new gamedata);
  # configs/ and plugins/ keep whatever is already there.
  local part
  for part in api bin dotnet gamedata; do
    [[ -d "$tmp/css/addons/counterstrikesharp/$part" ]] && cp -a "$tmp/css/addons/counterstrikesharp/$part" "$CSGO_DIR/addons/counterstrikesharp/"
  done
  cp -a "$tmp/css/addons/metamod/." "$CSGO_DIR/addons/metamod/" 2>/dev/null || true
  chown -R "$CS2_USER:$CS2_USER" "$CSGO_DIR/addons"
  rm -rf "$tmp"
  [[ -d "$CSGO_DIR/addons/counterstrikesharp" ]] || die "CounterStrikeSharp did not install."
  mkdir -p "$STATE_DIR"
  printf '%s\n' "$tag" > "$STATE_DIR/css-version"
}
 
# MultiAddonManager (Source2ZE): a Metamod plugin that makes clients download Workshop addons on connect
# (mm_client_extra_addons) and can mount server-side ones (mm_extra_addons). Not fatal if it cannot be
# installed: the servers run fine without it, only Workshop addons (custom Panorama UI) are missing.
latest_mam_release() { curl -fsSL https://api.github.com/repos/Source2ZE/MultiAddonManager/releases/latest; }
 
install_multiaddonmanager() {
  say "MultiAddonManager"
  local release url tag tmp file
  release="$(latest_mam_release)" || { say "MultiAddonManager: GitHub not reachable, skipped."; return 0; }
  # Release assets are named per Steam runtime (e.g. ...-steamrt3.tar.gz, ...-steamrt4.tar.gz, ...-windows.zip).
  # steamrt3 needs the oldest glibc, so it loads everywhere; MAM_ASSET_PATTERN picks another (e.g. steamrt4).
  local pat all
  all="$(printf '%s' "$release" | { grep -oiE '"browser_download_url": *"[^"]*"' || true; } | sed 's/.*"\(https[^"]*\)"/\1/')"
  for pat in "${MAM_ASSET_PATTERN:-steamrt3}" linux steamrt4; do
    url="$(printf '%s\n' "$all" | { grep -iE "${pat}[^/]*\.(tar\.gz|zip)$" || true; } | head -n1)"
    [[ -n "$url" ]] && break
  done
  tag="$(printf '%s' "$release" | release_tag || true)"
  if [[ -z "$url" ]]; then say "MultiAddonManager: no Linux package in the latest release, skipped."; return 0; fi
  tmp="$(mktemp -d)"
  file="$tmp/mam.${url##*.}"
  [[ "$url" == *.tar.gz ]] && file="$tmp/mam.tar.gz"
  if ! curl -fsSL "$url" -o "$file"; then rm -rf "$tmp"; say "MultiAddonManager: download failed, skipped."; return 0; fi
  mkdir -p "$tmp/pkg"
  if [[ "$file" == *.tar.gz ]]; then tar -xzf "$file" -C "$tmp/pkg"; else unzip -oq "$file" -d "$tmp/pkg"; fi
  # The package is laid out for game/csgo (addons/, cfg/). Program files are replaced on every run;
  # cfg/ keeps whatever is already there (our mm_client_extra_addons line).
  [[ -d "$tmp/pkg/addons" ]] && cp -a "$tmp/pkg/addons/." "$CSGO_DIR/addons/"
  [[ -d "$tmp/pkg/cfg" ]] && cp -an "$tmp/pkg/cfg/." "$CSGO_DIR/cfg/" 2>/dev/null || true
  chown -R "$CS2_USER:$CS2_USER" "$CSGO_DIR/addons" "$CSGO_DIR/cfg"
  rm -rf "$tmp"
  if [[ -z "$(find "$CSGO_DIR/addons" -iname 'multiaddonmanager*' -print -quit 2>/dev/null)" ]]; then
    say "MultiAddonManager: the package had an unexpected layout, check $CSGO_DIR/addons by hand."
    return 0
  fi
  mkdir -p "$STATE_DIR"
  printf '%s\n' "$tag" > "$STATE_DIR/mam-version"
  say "MultiAddonManager ${tag:-installed} (in the console: meta list)"
}
 
# Workshop addon IDs that every client downloads on connect. Written to MultiAddonManager's own cfg.
cmd_client_addons() {
  need_root
  local cfg="$CSGO_DIR/cfg/multiaddonmanager/multiaddonmanager.cfg" ids="${1:-}"
  if [[ -z "$ids" ]]; then
    grep -E '^[[:space:]]*mm_client_extra_addons' "$cfg" 2>/dev/null || echo "mm_client_extra_addons is not set."
    return 0
  fi
  [[ "$ids" == none ]] && ids=""
  [[ -z "$ids" || "$ids" =~ ^[0-9]+(,[0-9]+)*$ ]] || die "Usage: $0 client-addons 3807024759[,id,...] | none"
  [[ -d "$CSGO_DIR/addons" ]] || die "Run install first."
  mkdir -p "$(dirname "$cfg")"
  touch "$cfg"
  sed -i '/^[[:space:]]*mm_client_extra_addons/d' "$cfg"
  printf 'mm_client_extra_addons "%s"\n' "$ids" >> "$cfg"
  chown -R "$CS2_USER:$CS2_USER" "$(dirname "$cfg")"
  say "mm_client_extra_addons \"$ids\" written to $cfg"
  say "Takes effect after a restart: sudo systemctl restart cs2@<port> (clients that join later download it)."
}
 
install_legacyx() {
  local package="$1" env_file="$2" args=()
  [[ -n "$package" ]] && args+=(--package "$package")
  [[ -n "$env_file" ]] && args+=(--env "$env_file")
  say "LEGACY-X plugins"
  "$REPO/scripts/deploy.sh" "${args[@]}" "$CSGO_DIR"
  chown -R "$CS2_USER:$CS2_USER" "$CSGO_DIR/addons" "$CSGO_DIR/cfg"
}
 
# WeaponPaints' signature can break with a CS2 update: check it against this build's libserver.so and,
# if needed, take a maintained one that matches exactly once (scripts/fix-gamedata.py). Returns 0 when
# the signature is valid, 3 when no source has one for this build yet (retried by the timer).
fix_gamedata() {
  local gamedata="$CSGO_DIR/addons/counterstrikesharp/gamedata/weaponpaints.json" rc=0
  [[ -f "$gamedata" ]] || return 0
  python3 "$REPO/scripts/fix-gamedata.py" "$CSGO_DIR/bin/linuxsteamrt64/libserver.so" "$gamedata" || rc=$?
  chown "$CS2_USER:$CS2_USER" "$gamedata" 2>/dev/null || true
  mkdir -p "$STATE_DIR"
  if [[ $rc -eq 3 ]]; then : > "$STATE_DIR/gamedata-pending"; else rm -f "$STATE_DIR/gamedata-pending"; fi
  return $rc
}
 
write_unit() {
  cat > "$UNIT" <<EOF
[Unit]
Description=LEGACY-X CS2 server on port %i
After=network-online.target
Wants=network-online.target
 
[Service]
Type=simple
User=$CS2_USER
WorkingDirectory=$CS2_DIR
EnvironmentFile=$CONF_DIR/%i.conf
ExecStart=/bin/bash -c 'exec $CS2_DIR/game/cs2.sh -dedicated -port %i -maxplayers \${MAXPLAYERS} +map \${MAP} \${GAME_ARGS} +sv_setsteamaccount \${GSLT}'
Restart=on-failure
RestartSec=10
LimitNOFILE=100000
 
[Install]
WantedBy=multi-user.target
EOF
  systemctl daemon-reload
}
 
game_args() {
  case "$1" in
    competitive) echo "+game_type 0 +game_mode 1" ;;
    wingman) echo "+game_type 0 +game_mode 2" ;;
    casual) echo "+game_type 0 +game_mode 0" ;;
    deathmatch) echo "+game_type 1 +game_mode 2" ;;
    *) die "Mode must be competitive, wingman, casual or deathmatch." ;;
  esac
}
 
# Replaces (or adds) KEY=value in the shared .env; an empty value leaves the file as it is.
set_env_value() {
  local key="$1" value="$2" env="$CSGO_DIR/addons/counterstrikesharp/.env"
  [[ -n "$value" ]] || return 0
  local source="$REPO/.env"
  [[ -f "$env" || -f "$source" ]] || die "No $env yet: run install with --env first."
  # The repository's .env is the one people edit; keep it and the servers' copy in step.
  for file in "$source" "$env"; do
    [[ -f "$file" ]] || continue
    sed -i "/^${key}=/d" "$file"
    printf '%s=%s\n' "$key" "$value" >> "$file"
  done
  [[ -f "$env" ]] || sync_env
  # sed -i writes a new file owned by root; the servers run as $CS2_USER and must still read it.
  chown "$CS2_USER:$CS2_USER" "$env"
  chmod 600 "$env"
  say "$key=$value"
}
 
# The .env in this repository (edit it with `nano .env`) is the servers' settings file: copy it to where
# the plugins read it. deploy and update do this too; this is the quick way after a small edit.
sync_env() {
  local source="$REPO/.env" target="$CSGO_DIR/addons/counterstrikesharp/.env"
  [[ -f "$source" ]] || die "No $source: create it from .env.example first."
  [[ -d "$CSGO_DIR/addons/counterstrikesharp" ]] || die "Run install first."
  install -m 600 -o "$CS2_USER" -g "$CS2_USER" "$source" "$target"
  say "$source -> $target"
}

cmd_env() {
  need_root
  sync_env
  say "Takes effect after a restart: sudo systemctl restart cs2@<port>"
}

open_port() {
  if command -v ufw >/dev/null && ufw status | grep -q "Status: active"; then
    ufw allow "$1" >/dev/null && say "Firewall: port $1 open (TCP and UDP)"
  fi
}
 
cmd_install() {
  need_root
  local package="" env_file=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --package) package="${2:-}"; shift 2 ;;
      --env) env_file="${2:-}"; shift 2 ;;
      *) die "Unknown option: $1" ;;
    esac
  done
  [[ -z "$env_file" || -f "$env_file" ]] || die "--env file not found: $env_file"
  [[ -z "$package" || -f "$package" ]] || die "--package file not found: $package"
  install_packages "$([[ -n "$package" ]] && echo no || echo yes)"
  install_steamcmd
  update_cs2
  install_metamod
  install_counterstrikesharp
  install_multiaddonmanager
  install_legacyx "$package" "$env_file"
  fix_gamedata || true
  mkdir -p "$CONF_DIR"
  write_unit
  cmd_autoupdate on
  say "Installed. Next, add each server: sudo $0 add 27015 <GSLT> competitive de_dust2"
}
 
cmd_add() {
  need_root
  local port="${1:-}" gslt="${2:-}" mode="${3:-competitive}" map="${4:-de_dust2}" maxplayers="${5:-10}" site_mode="${6:-}"
  local name="${*:7}"
  [[ "$port" =~ ^[0-9]{4,5}$ && "$port" -le 65535 ]] || die "Usage: $0 add <port> <GSLT> [mode] [map] [maxplayers] [site-mode] [name…]"
  [[ "$gslt" =~ ^[A-Fa-f0-9]{32}$ ]] || die "The GSLT is a 32-character code from steamcommunity.com/dev/managegameservers (app 730)."
  [[ "$map" =~ ^[a-z0-9_]{2,64}$ ]] || die "Map name like de_dust2."
  [[ "$maxplayers" =~ ^[0-9]{1,2}$ && "$maxplayers" -ge 2 && "$maxplayers" -le 64 ]] || die "maxplayers: 2-64."
  [[ -z "$site_mode" || "$site_mode" =~ ^[a-z0-9_]{1,40}$ ]] || die "site-mode like competitive_5v5, fun, proleague or tournaments."
  name="$(printf '%s' "$name" | tr -d '\r\n"=' | cut -c1-60)"
  [[ -f "$UNIT" ]] || die "Run install first."
  set_env_value "LEGACYX_${port}_SERVER_MODE" "$site_mode"
  set_env_value "LEGACYX_${port}_SERVER_NAME" "$name"
  local args
  args="$(game_args "$mode")"
  mkdir -p "$CONF_DIR"
  umask 077
  cat > "$CONF_DIR/$port.conf" <<EOF
# CS2 server on port $port (systemd unit cs2@$port). Edit, then: sudo systemctl restart cs2@$port
GSLT=$gslt
MAP=$map
MAXPLAYERS=$maxplayers
GAME_ARGS="$args"
EOF
  open_port "$port"
  systemctl enable --now "cs2@$port" >/dev/null
  say "cs2@$port started ($mode, $map, $maxplayers players). Console: $0 logs $port"
  say "On the website it is srv-$port (it appears within 30 s of starting)."
}
 
cmd_update() {
  need_root
  local package=""
  [[ "${1:-}" == "--package" ]] && package="${2:-}"
  local running=() build_before css_before
  build_before="$(installed_cs2_build)"
  css_before="$(cat "$STATE_DIR/css-version" 2>/dev/null || true)"
  for port in $(ports); do
    if systemctl is-active --quiet "cs2@$port"; then running+=("$port"); fi
  done
  for port in "${running[@]}"; do systemctl stop "cs2@$port"; done
  update_cs2
  install_metamod
  install_counterstrikesharp
  install_multiaddonmanager
  if [[ -n "$package" ]] || command -v dotnet >/dev/null; then
    install_legacyx "$package" ""
  else
    say "No .NET SDK here and no --package: the LEGACY-X plugins are left as they are."
  fi
  fix_gamedata || true
  for port in "${running[@]}"; do systemctl start "cs2@$port"; done
  say "Updated. Restarted: ${running[*]:-none}"
  announce_update "$build_before" "$css_before" "${running[*]:-none}"
}
 
# Discord (scripts/announce.sh): what this update changed, from facts only: the CS2 build and
# CounterStrikeSharp version before and after, and the plugin commits pulled since ANNOUNCE_FROM.
ANNOUNCE_FROM=""
announce_update() {
  local build_after css_after lines=()
  build_after="$(installed_cs2_build)"
  css_after="$(cat "$STATE_DIR/css-version" 2>/dev/null || true)"
  if [[ -n "$1" && -n "$build_after" && "$1" != "$build_after" ]]; then lines+=(--line "CS2 build $1 → $build_after"); fi
  if [[ -n "$css_after" && "$2" != "$css_after" ]]; then lines+=(--line "CounterStrikeSharp ${2:-?} → $css_after"); fi
  bash "$REPO/scripts/announce.sh" --title "Game servers updated" "${lines[@]}" \
    --commits "$REPO" "$ANNOUNCE_FROM" "$(git_repo rev-parse HEAD 2>/dev/null)" --footer "Restarted: $3" || true
}
 
# By hand, now: pull this repository (new plugin commits) and run a full update.
cmd_deploy() {
  need_root
  say "Pulling $(git_repo rev-parse --abbrev-ref HEAD)"
  ANNOUNCE_FROM="$(git_repo rev-parse HEAD)"
  git_repo pull --ff-only
  say "Plugins at $(git_repo log -1 --format='%h %s')"
  cmd_update "$@"
}
 
installed_cs2_build() {
  { sed -n 's/^[[:space:]]*"buildid"[[:space:]]*"\([0-9]*\)".*/\1/p' "$CS2_DIR/steamapps/appmanifest_730.acf" 2>/dev/null || true; } | head -n1
}
 
latest_cs2_build() {
  # Empty when Steam cannot be reached: the check then skips CS2 this round.
  { as_cs2 "$STEAMCMD_DIR/steamcmd.sh" +login anonymous +app_info_update 1 +app_info_print 730 +quit 2>/dev/null || true; } \
    | awk '/"branches"/{b=1} b && /"public"/{p=1} p && /"buildid"/{gsub(/[^0-9]/, "", $2); print $2; exit}'
}
 
git_repo() { git -c safe.directory="$REPO" -C "$REPO" "$@"; }
 
# One pass of the automatic updater (the timer runs it every 10 minutes). A new CS2 build is applied at
# once (players on the new client cannot join an outdated server anyway); a CounterStrikeSharp release
# that follows it is applied at once too, since plugins are down until it arrives. Everything else
# (a newer CounterStrikeSharp on its own, new LEGACY-X plugin commits) waits for AUTOUPDATE_HOUR.
cmd_autoupdate_check() {
  need_root
  exec 9>/run/legacyx-cs2-update.lock
  flock -n 9 || exit 0
  mkdir -p "$STATE_DIR"
  local reasons=() urgent=no pull=no
  local have want css_have css_want
  have="$(installed_cs2_build)"
  want="$(latest_cs2_build)"
  css_have="$(cat "$STATE_DIR/css-version" 2>/dev/null || true)"
  css_want="$(latest_css_release 2>/dev/null | release_tag || true)"
 
  if [[ -n "$want" && -n "$have" && "$have" != "$want" ]]; then
    reasons+=("CS2 build $have -> $want")
    urgent=yes
    # Remember which CounterStrikeSharp was current when CS2 moved: the next one is the fix.
    [[ -f "$STATE_DIR/awaiting-css" ]] || printf '%s\n' "$css_want" > "$STATE_DIR/awaiting-css"
  fi
  if [[ -n "$css_want" && "$css_have" != "$css_want" ]]; then
    reasons+=("CounterStrikeSharp ${css_have:-?} -> $css_want")
    if [[ -f "$STATE_DIR/awaiting-css" && "$(cat "$STATE_DIR/awaiting-css")" != "$css_want" ]]; then urgent=yes; fi
  fi
  if git_repo rev-parse --is-inside-work-tree >/dev/null 2>&1 && git_repo fetch -q 2>/dev/null; then
    if [[ "$(git_repo rev-parse HEAD)" != "$(git_repo rev-parse '@{u}' 2>/dev/null || git_repo rev-parse HEAD)" ]]; then
      reasons+=("LEGACY-X plugins $(git_repo rev-parse --short HEAD) -> $(git_repo rev-parse --short '@{u}')")
      pull=yes
    fi
  fi
 
  # Skins were left off by a CS2 update because no maintained signature matched yet: try again. A fix is
  # already written to disk; the servers load it on their next start (at the quiet hour, like any
  # non-urgent change, since skins are cosmetic and a restart would cut matches).
  local restart_only=no
  if [[ -f "$STATE_DIR/gamedata-pending" || -f "$STATE_DIR/gamedata-restart" ]]; then
    if [[ -f "$STATE_DIR/gamedata-restart" ]] || fix_gamedata; then
      : > "$STATE_DIR/gamedata-restart"
      [[ ${#reasons[@]} -eq 0 ]] && restart_only=yes
      reasons+=("WeaponPaints signature fixed")
    fi
  fi
 
  [[ ${#reasons[@]} -gt 0 ]] || exit 0
  if [[ "$urgent" == no && "$(date +%-H)" != "$AUTOUPDATE_HOUR" ]]; then
    say "Update waiting for ${AUTOUPDATE_HOUR}:00: ${reasons[*]}"
    exit 0
  fi
  say "Updating: ${reasons[*]}"
  rm -f "$STATE_DIR/gamedata-restart"
  if [[ "$restart_only" == yes ]]; then
    local restarted=()
    for port in $(ports); do
      if systemctl is-active --quiet "cs2@$port"; then systemctl restart "cs2@$port"; restarted+=("$port"); fi
    done
    bash "$REPO/scripts/announce.sh" --title "Game servers restarted" --line "WeaponPaints signature fixed" \
      --footer "Restarted: ${restarted[*]:-none}" || true
    return 0
  fi
  ANNOUNCE_FROM="$(git_repo rev-parse HEAD)"
  if [[ "$pull" == yes ]]; then git_repo pull -q --ff-only || say "git pull failed; plugins stay at $(git_repo rev-parse --short HEAD)."; fi
  cmd_update
  # A CounterStrikeSharp release newer than the one current at the CS2 update is now installed.
  if [[ -f "$STATE_DIR/awaiting-css" && "$(cat "$STATE_DIR/css-version" 2>/dev/null)" != "$(cat "$STATE_DIR/awaiting-css")" ]]; then
    rm -f "$STATE_DIR/awaiting-css"
  fi
}
 
cmd_autoupdate() {
  need_root
  case "${1:-}" in
    on)
      cat > "$AUTOUPDATE_UNIT.service" <<EOF
[Unit]
Description=LEGACY-X CS2 automatic update check
After=network-online.target
Wants=network-online.target
 
[Service]
Type=oneshot
Environment=AUTOUPDATE_HOUR=$AUTOUPDATE_HOUR
ExecStart=$REPO/scripts/cs2-host.sh autoupdate check
EOF
      cat > "$AUTOUPDATE_UNIT.timer" <<EOF
[Unit]
Description=LEGACY-X CS2 automatic update check every 10 minutes
 
[Timer]
OnBootSec=5min
OnUnitActiveSec=10min
 
[Install]
WantedBy=timers.target
EOF
      systemctl daemon-reload
      systemctl enable --now legacyx-cs2-autoupdate.timer >/dev/null
      say "Automatic updates on: checked every 10 minutes; non-urgent updates at ${AUTOUPDATE_HOUR}:00. Log: journalctl -u legacyx-cs2-autoupdate"
      ;;
    off)
      systemctl disable --now legacyx-cs2-autoupdate.timer >/dev/null 2>&1 || true
      say "Automatic updates off."
      ;;
    check) cmd_autoupdate_check ;;
    *) die "Usage: $0 autoupdate on|off|check" ;;
  esac
}
 
cmd_remove() {
  need_root
  local port="${1:-}"
  [[ -f "$CONF_DIR/$port.conf" ]] || die "No server on port $port."
  systemctl disable --now "cs2@$port" >/dev/null || true
  rm -f "$CONF_DIR/$port.conf"
  say "cs2@$port removed."
}
 
cmd_status() {
  local any=0
  for port in $(ports); do
    any=1
    printf '%-6s %s\n' "$port" "$(systemctl is-active "cs2@$port" 2>/dev/null || true)"
  done
  [[ $any -eq 1 ]] || echo "No servers yet: sudo $0 add <port> <GSLT>"
}
 
case "${1:-}" in
  install) shift; cmd_install "$@" ;;
  add) shift; cmd_add "$@" ;;
  update) shift; cmd_update "$@" ;;
  deploy) shift; cmd_deploy "$@" ;;
  env) cmd_env ;;
  autoupdate) shift; cmd_autoupdate "$@" ;;
  client-addons) shift; cmd_client_addons "$@" ;;
  remove) shift; cmd_remove "$@" ;;
  status) cmd_status ;;
  logs) [[ -n "${2:-}" ]] || die "Usage: $0 logs <port>"; journalctl -u "cs2@$2" -f ;;
  *) sed -n '2,/^set -euo/{/^set -euo/!p}' "$0"; exit 1 ;;
esac
 
