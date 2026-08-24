#!/usr/bin/env bash
set -euo pipefail

# Source-only build helper. It does not install a game server or deploy DLLs.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
projects=("adminplus/plugin/AdminPlus/AdminPlus.csproj" "afkmanager/AFKManager.csproj" "community/LegacyXCommunity.csproj" "matchzy/MatchZy.csproj" "reconnect/LegacyXReconnect.csproj" "spectator-comms/LegacyXSpectatorComms.csproj" "weaponpaints-legacyx/WeaponPaints.csproj")
for project in "${projects[@]}"; do dotnet build "$root/$project" --configuration Release; done
