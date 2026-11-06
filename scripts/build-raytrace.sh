#!/usr/bin/env bash
set -euo pipefail

# Makes LegacyX-AntiWallhack/libs/RayTraceApi.dll, the shared assembly of the Ray-Trace module (github.com/FUNPLAY-pro-CS2/Ray-Trace, GPL-3.0).
# The plugin only references it to be built (it is never copied into the plugin package). Needs the .NET 10 SDK and git.
#
# The module itself is a separate install on every game server that runs the anti-wallhack; this script does not install it:
#   addons/metamod/RayTrace.vdf + the RayTrace native library          (from the module's releases)
#   addons/counterstrikesharp/plugins/RayTraceImpl/RayTraceImpl.dll
#   addons/counterstrikesharp/shared/RayTraceApi/RayTraceApi.dll       (the file this script makes)
# Build RayTraceImpl.dll the same way if the release has none:  dotnet publish managed/RayTrace/RayTraceImpl -c Release

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
git clone --depth 1 https://github.com/FUNPLAY-pro-CS2/Ray-Trace "$work/ray-trace"
dotnet build "$work/ray-trace/managed/RayTrace/RayTraceApi/RayTraceApi.csproj" -c Release -o "$work/out"
mkdir -p "$root/LegacyX-AntiWallhack/libs"
cp "$work/out/RayTraceApi.dll" "$root/LegacyX-AntiWallhack/libs/RayTraceApi.dll"
echo "LegacyX-AntiWallhack/libs/RayTraceApi.dll is ready. Build the plugins again (scripts/build-all.sh)."
