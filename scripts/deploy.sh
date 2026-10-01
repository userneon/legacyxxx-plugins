#!/usr/bin/env bash
# Installs or updates the LEGACY-X plugins on a Linux CS2 host, in one command.
#
#   ./scripts/deploy.sh [--package legacyx-cs2.zip] [--env legacyx-srv-HOST.env] <csgo-dir> [<csgo-dir> …]
#
#   <csgo-dir>   a CS2 server's game/csgo folder (Metamod and CounterStrikeSharp already installed).
#                Several servers sharing one install: give that one folder. Separate installs: list each.
#   --package    use this prebuilt zip instead of building from source (building needs .NET 8 SDK and Node).
#   --env        the .env from `scripts/create-game-server.mjs` in legacyxxx-backend; installed as
#                addons/counterstrikesharp/.env in every folder. Without it, a `.env` in this repository's root
#                (the file `nano .env` edits there) is installed; without that, an existing .env is kept.
#
# Copies only the plugin files (addons/…, cfg/MatchZy/…); nothing else in the server folder is touched,
# an existing .env is never overwritten unless --env is given, and servers are not restarted.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package=""
env_file=""
targets=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --package) package="${2:-}"; shift 2 ;;
    --env) env_file="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,17p' "$0"; exit 0 ;;
    *) targets+=("$1"); shift ;;
  esac
done
if [[ ${#targets[@]} -eq 0 ]]; then
  sed -n '2,17p' "$0" >&2
  exit 1
fi
if [[ -z "$env_file" && -f "$root/.env" ]]; then
  env_file="$root/.env"
  echo "==> Using $env_file as the servers' .env"
fi
if [[ -n "$env_file" && ! -f "$env_file" ]]; then
  echo "!! --env file not found: $env_file" >&2
  exit 1
fi

# Check every target before changing anything.
for dir in "${targets[@]}"; do
  if [[ ! -d "$dir/addons/counterstrikesharp" ]]; then
    echo "!! $dir is not a CS2 csgo folder with CounterStrikeSharp installed (no addons/counterstrikesharp)." >&2
    exit 1
  fi
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
if [[ -n "$package" ]]; then
  [[ -f "$package" ]] || { echo "!! --package file not found: $package" >&2; exit 1; }
  echo "==> Using $package"
  unzip -q "$package" -d "$work/pkg"
  src="$work/pkg"
else
  command -v dotnet >/dev/null || { echo "!! .NET 8 SDK is needed to build (or pass --package legacyx-cs2.zip)." >&2; exit 1; }
  command -v node >/dev/null || { echo "!! Node.js is needed to package (or pass --package legacyx-cs2.zip)." >&2; exit 1; }
  echo "==> Building and packaging from $(git -C "$root" log -1 --format='%h %s' 2>/dev/null || echo "$root")"
  "$root/scripts/build-all.sh" >/dev/null
  "$root/scripts/package.sh"
  src="$root/dist/legacyx-cs2"
fi
[[ -d "$src/addons/counterstrikesharp/plugins" ]] || { echo "!! The package has no addons/counterstrikesharp/plugins." >&2; exit 1; }

for dir in "${targets[@]}"; do
  echo "==> $dir"
  # Plugin files only; the package never carries a real .env.
  cp -a "$src/addons/." "$dir/addons/"
  mkdir -p "$dir/cfg"
  # The server writes these itself (practice lineups players saved, the whitelist, the database
  # choice): the package only provides them when missing. The rest of cfg/MatchZy follows the repo.
  kept="$work/kept-cfg"
  rm -rf "$kept"
  for own in MatchZy/savednades.json MatchZy/whitelist.cfg MatchZy/database.json; do
    if [[ -f "$dir/cfg/$own" ]]; then
      mkdir -p "$kept/$(dirname "$own")"
      cp -a "$dir/cfg/$own" "$kept/$own"
    fi
  done
  cp -a "$src/cfg/." "$dir/cfg/"
  if [[ -d "$kept" ]]; then cp -a "$kept/." "$dir/cfg/"; fi
  env_target="$dir/addons/counterstrikesharp/.env"
  if [[ -n "$env_file" && "$env_target" -ef "$env_file" ]]; then
    echo "    .env linked to $env_file"
  elif [[ -n "$env_file" ]]; then
    install -m 600 "$env_file" "$env_target"
    echo "    .env installed"
  elif [[ -f "$env_target" ]]; then
    echo "    .env kept"
  else
    echo "    !! no .env yet: pass --env <file from create-game-server.mjs>"
  fi
  # Website skins and Tab rank icons write item/rank fields that CounterStrikeSharp blocks while
  # FollowCS2ServerGuidelines is on (the default). Turn it off in core.json, creating it if needed.
  core="$dir/addons/counterstrikesharp/configs/core.json"
  if [[ ! -f "$core" && -f "$dir/addons/counterstrikesharp/configs/core.example.json" ]]; then
    cp "$dir/addons/counterstrikesharp/configs/core.example.json" "$core"
  fi
  if [[ -f "$core" ]] && grep -q '"FollowCS2ServerGuidelines"[[:space:]]*:[[:space:]]*true' "$core"; then
    sed -i 's/"FollowCS2ServerGuidelines"[[:space:]]*:[[:space:]]*true/"FollowCS2ServerGuidelines": false/' "$core"
    echo "    FollowCS2ServerGuidelines set to false (needed for website skins and rank icons)"
  fi
  ls "$dir/addons/counterstrikesharp/plugins" | grep '^LegacyX-' | sed 's/^/    /'
done

echo "==> Done. Restart the CS2 servers to load the plugins."
