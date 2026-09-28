#!/usr/bin/env bash
# LEGACY-X CS2 host: runs several CS2 servers on one Linux VPS (Ubuntu 22.04/24.04, x86_64),
# all from one shared install, each as its own systemd service named by its port.
#
#   sudo ./scripts/cs2-host.sh install --env legacyx-srv-HOST.env [--package legacyx-cs2.zip]
#       steamcmd, the CS2 dedicated server, Metamod:Source, CounterStrikeSharp, the LEGACY-X plugins
#       and the .env from create-game-server.mjs (legacyxxx-backend). Safe to run again.
#   sudo ./scripts/cs2-host.sh add <port> <GSLT> [competitive|wingman|casual|deathmatch] [map] [maxplayers] [site-mode] [name…]
#       a server on <port>: config, firewall, systemd service, started now and at boot. site-mode picks the
#       website Play page (competitive_5v5, fun, proleague, tournaments; default from the .env) and name
#       the name shown there; both are written to the .env as LEGACYX_<port>_SERVER_MODE / _NAME.
#       GSLT: a Steam game server login token, one per server (steamcommunity.com/dev/managegameservers, app 730).
#   sudo ./scripts/cs2-host.sh update [--package legacyx-cs2.zip]
#       stops the servers, updates CS2, Metamod, CounterStrikeSharp and the plugins, starts them again.
#   sudo ./scripts/cs2-host.sh remove <port>        stops and deletes that server's service and config
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
  apt-get install -y -qq curl ca-certificates unzip tar lib32gcc-s1 lib32stdc++6 >/dev/null
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

install_counterstrikesharp() {
  say "CounterStrikeSharp (with .NET runtime)"
  local url tmp
  url="$(curl -fsSL https://api.github.com/repos/roflmuffin/CounterStrikeSharp/releases/latest \
    | grep -oE '"browser_download_url": *"[^"]*with-runtime[^"]*linux[^"]*\.zip"' | head -n1 | sed 's/.*"\(https[^"]*\)"/\1/')"
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
}

install_legacyx() {
  local package="$1" env_file="$2" args=()
  [[ -n "$package" ]] && args+=(--package "$package")
  [[ -n "$env_file" ]] && args+=(--env "$env_file")
  say "LEGACY-X plugins"
  "$REPO/scripts/deploy.sh" "${args[@]}" "$CSGO_DIR"
  chown -R "$CS2_USER:$CS2_USER" "$CSGO_DIR/addons" "$CSGO_DIR/cfg"
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
  [[ -f "$env" ]] || die "No $env yet: run install with --env first."
  sed -i "/^${key}=/d" "$env"
  printf '%s=%s\n' "$key" "$value" >> "$env"
  # sed -i writes a new file owned by root; the servers run as $CS2_USER and must still read it.
  chown "$CS2_USER:$CS2_USER" "$env"
  chmod 600 "$env"
  say "$key=$value"
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
  install_legacyx "$package" "$env_file"
  mkdir -p "$CONF_DIR"
  write_unit
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
  local running=()
  for port in $(ports); do systemctl is-active --quiet "cs2@$port" && running+=("$port"); done
  for port in "${running[@]}"; do systemctl stop "cs2@$port"; done
  update_cs2
  install_metamod
  install_counterstrikesharp
  install_legacyx "$package" ""
  for port in "${running[@]}"; do systemctl start "cs2@$port"; done
  say "Updated. Restarted: ${running[*]:-none}"
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
  remove) shift; cmd_remove "$@" ;;
  status) cmd_status ;;
  logs) [[ -n "${2:-}" ]] || die "Usage: $0 logs <port>"; journalctl -u "cs2@$2" -f ;;
  *) sed -n '2,21p' "$0"; exit 1 ;;
esac
