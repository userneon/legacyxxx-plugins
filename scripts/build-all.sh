#!/usr/bin/env bash
set -euo pipefail

# Source-only build helper. It does not install a game server or deploy DLLs.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
projects=("LegacyX-Admin/LegacyX-Admin.csproj" "LegacyX-AFKManager/LegacyX-AFKManager.csproj" "LegacyX-Community/LegacyX-Community.csproj" "LegacyX-MatchZy/LegacyX-MatchZy.csproj" "LegacyX-Spectator/LegacyX-Spectator.csproj" "LegacyX-WeaponPaints/LegacyX-WeaponPaints.csproj")
for project in "${projects[@]}"; do dotnet build "$root/$project" --configuration Release; done
