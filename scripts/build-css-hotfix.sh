#!/usr/bin/env bash
# Builds CounterStrikeSharp v1.0.377 against an hl2sdk-cs2 that knows the virtual CS2 1.41.9.2 added to
# IVEngineServer2 (IsSpawnGroupLoadedForClient). Without it the release calls every later engine function one
# slot off, and the server crashes in PlayerManager::OnAuthorized when a player (or a bot) joins
# (roflmuffin/CounterStrikeSharp issue #1466, fix proposed in pull request #1468: only the SDK pointer changes).
#
# Used until CounterStrikeSharp ships its own fix: the next release replaces this build by itself
# (cs2-host.sh, apply_css_override). It builds on the server, so the result matches this machine's glibc.
#
#   sudo ./scripts/cs2-host.sh css-hotfix        (calls this script, then installs the result; servers must be stopped)
#
# Needs: git cmake ninja clang protoc and the zlib/OpenSSL headers:
#   apt install -y git cmake ninja-build clang zlib1g-dev libssl-dev libprotobuf-dev protobuf-compiler pkg-config
set -euo pipefail

# Exact sources: a moved tag or branch cannot change what gets built.
CSS_TAG="v1.0.377"
CSS_SHA="428450abb83c4e89cf66ff320f3182a8ffaa6e3a"
SDK_SHA="7d51bfdae400b506b5288e4d42e31c3e827c599c"

STATE_DIR="${STATE_DIR:-/var/lib/legacyx/cs2}"
OUT_DIR="${OUT_DIR:-$STATE_DIR/css-override}"
WORK_DIR="${WORK_DIR:-/var/tmp/css-hotfix}"

say() { printf '==> %s\n' "$*"; }
die() { printf '!! %s\n' "$*" >&2; exit 1; }

for tool in git cmake ninja clang clang++ protoc; do
  command -v "$tool" >/dev/null || die "$tool is missing. Install: apt install -y git cmake ninja-build clang zlib1g-dev libssl-dev libprotobuf-dev protobuf-compiler pkg-config"
done

export GIT_LFS_SKIP_SMUDGE=1
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR"
cd "$WORK_DIR"

say "CounterStrikeSharp $CSS_TAG"
git clone -q --depth 1 --branch "$CSS_TAG" https://github.com/roflmuffin/CounterStrikeSharp src
cd src
[[ "$(git rev-parse HEAD)" == "$CSS_SHA" ]] || die "$CSS_TAG is not commit $CSS_SHA any more; stopping."

say "Libraries"
git submodule update --init --depth 1 --recommend-shallow
git -C libraries/metamod-source submodule update --init --depth 1

say "hl2sdk-cs2 at $SDK_SHA"
git -C libraries/hl2sdk-cs2 fetch -q --depth 1 origin "$SDK_SHA"
git -C libraries/hl2sdk-cs2 checkout -q "$SDK_SHA"
[[ "$(git -C libraries/hl2sdk-cs2 rev-parse HEAD)" == "$SDK_SHA" ]] || die "hl2sdk-cs2 is not at $SDK_SHA; stopping."
grep -q "IsSpawnGroupLoadedForClient" libraries/hl2sdk-cs2/public/eiface.h || die "The SDK does not have the new virtual; stopping."

say "Build (a few minutes)"
CC=clang CXX=clang++ cmake -G Ninja -DCMAKE_BUILD_TYPE=Release -S . -B build >/dev/null
cmake --build build -j"$(nproc)"

SO="build/addons/counterstrikesharp/bin/linuxsteamrt64/counterstrikesharp.so"
[[ -s "$SO" ]] || die "The build did not produce $SO."

mkdir -p "$OUT_DIR"
install -m 755 "$SO" "$OUT_DIR/counterstrikesharp.so"
printf '%s\n' "$CSS_TAG" > "$OUT_DIR/tag"
sha256sum "$OUT_DIR/counterstrikesharp.so" | tee "$OUT_DIR/sha256"
cd /
[[ "${KEEP_WORK:-0}" == 1 ]] || rm -rf "$WORK_DIR"
say "Built for $CSS_TAG: $OUT_DIR/counterstrikesharp.so"
