#!/usr/bin/env bash
set -euo pipefail
# Builds dist/legacyx-cs2 (runtime files only). Copy its contents into game/csgo/ on the server.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
node "$root/scripts/package-plugins.mjs" "$@"
