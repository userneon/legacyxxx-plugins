#!/usr/bin/env bash
# legacyx-host.sh: the ONLY thing the website's SSH key may run on this host (owner request 2026-11-07: "check what is loaded, and let the site act directly").
#
# It is meant to be forced in authorized_keys, so the key can do nothing else (no shell, no forwarding, no other command):
#   restrict,command="/root/legacyxxx-plugins/scripts/legacyx-host.sh" ssh-ed25519 AAAA... legacyx-site
# The website's API asks with one of these words as the SSH command (see legacyxxx-backend docs/HOST_ACCESS.md):
#   status            what is installed, loaded and running, as JSON (read only)
#   deploy            git pull, build, package and install the plugins into the CS2 server folder (does not restart anything)
#   restart <port>    systemctl restart cs2@<port>  (only a port that has a server config here)
# Anything else is refused. Every answer is one JSON object on stdout.
#
# Settings (for tests; the defaults are this host's): CS2_HOME CS2_DIR CSGO_DIR STATE_DIR CONF_DIR REPO
set -uo pipefail

REPO="${REPO:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
CS2_HOME="${CS2_HOME:-/home/cs2}"
CS2_DIR="${CS2_DIR:-$CS2_HOME/cs2}"
CSGO_DIR="${CSGO_DIR:-$CS2_DIR/game/csgo}"
STATE_DIR="${STATE_DIR:-/var/lib/legacyx/cs2}"
CONF_DIR="${CONF_DIR:-/etc/legacyx/cs2}"
export REPO CS2_HOME CS2_DIR CSGO_DIR STATE_DIR CONF_DIR

line="${SSH_ORIGINAL_COMMAND:-$*}"
read -r action arg extra <<<"$line"

fail() { printf '{"ok":false,"error":"%s"}\n' "$1"; exit "${2:-2}"; }
[[ -z "${extra:-}" ]] || fail "too many words"

status() {
python3 - <<'PY'
import datetime, glob, json, os, re, shutil, subprocess

REPO = os.environ["REPO"]; CS2_DIR = os.environ["CS2_DIR"]; CSGO = os.environ["CSGO_DIR"]
STATE = os.environ["STATE_DIR"]; CONF = os.environ["CONF_DIR"]
checks, info = [], {}

def run(cmd, timeout=20):
    try:
        return subprocess.run(cmd, capture_output=True, text=True, timeout=timeout).stdout.strip()
    except Exception:
        return ""

def chk(cid, label, status, detail=""):
    checks.append({"id": cid, "label": label, "status": status, "detail": detail})

def read(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except Exception:
        return ""

# --- the repository and what was deployed from it ---
head = run(["git", "-C", REPO, "rev-parse", "--short", "HEAD"])
subject = run(["git", "-C", REPO, "log", "-1", "--format=%s"])
run(["git", "-C", REPO, "fetch", "-q", "origin", "main"], 25)
behind_text = run(["git", "-C", REPO, "rev-list", "--count", "HEAD..origin/main"])
behind = int(behind_text) if behind_text.isdigit() else None
dirty = bool(run(["git", "-C", REPO, "status", "--porcelain", "--untracked-files=no"]))
info["repo"] = {"commit": head, "subject": subject, "behind": behind, "dirty": dirty}
if behind is None: chk("repo", "Repository", "warn", "could not compare with GitHub")
elif behind > 0: chk("repo", "Repository", "warn", f"{behind} newer commit(s) on GitHub: run deploy")
else: chk("repo", "Repository", "ok", f"{head} is the latest")
try:
    deployed = json.loads(read(os.path.join(CSGO, "addons/counterstrikesharp/legacyx-deployed.json")) or "{}")
except Exception:
    deployed = {}
info["deployed"] = deployed
if not deployed.get("commit"): chk("deployed", "Deployed plugins", "warn", "no deploy marker yet (it is written by the next deploy)")
elif head and deployed.get("commit") != head: chk("deployed", "Deployed plugins", "warn", f"deployed {deployed.get('commit')}, repository {head}: run deploy")
else: chk("deployed", "Deployed plugins", "ok", f"{deployed.get('commit')} at {deployed.get('at', '?')}")

# --- the servers on this host and what each one logged since it started ---
ports = sorted(int(m.group(1)) for f in glob.glob(os.path.join(CONF, "*.conf")) if (m := re.match(r"(\d+)\.conf$", os.path.basename(f))))
loaded, failed, meta_failed, metamod_version, servers = set(), set(), [], "", []
for port in ports:
    unit = f"cs2@{port}"
    active = run(["systemctl", "is-active", unit]) == "active"
    since = run(["systemctl", "show", unit, "-p", "ActiveEnterTimestamp", "--value"])
    text = ""
    if active and since:
        parts = since.split()
        stamp = " ".join(parts[1:3]) if len(parts) >= 3 else since
        text = run(["journalctl", "-u", unit, "--since", stamp, "--no-pager", "-o", "cat"], 30)
    current, finished, server_loaded, server_failed = None, True, set(), set()
    for entry in text.splitlines():
        m = re.search(r"Loading plugin (\S+)", entry)
        if m and "Finished" not in entry:
            if current and not finished: server_failed.add(current)
            current, finished = m.group(1), False
            continue
        if "Finished loading plugin" in entry and current:
            server_loaded.add(current); finished = True
            continue
        m = re.search(r"\[META\] Failed to load plugin (.+?): (.*)", entry)
        if m: meta_failed.append(f"{m.group(1)}: {m.group(2)}"[:200])
        m = re.search(r"Metamod:Source version (\S+)", entry)
        if m: metamod_version = m.group(1)
    if current and not finished: server_failed.add(current)
    loaded |= server_loaded; failed |= server_failed
    servers.append({"port": port, "active": active, "since": since, "plugins": len(server_loaded), "failed": sorted(server_failed)})
    chk(f"server-{port}", f"Server {port}", "ok" if active else "fail", f"running, {len(server_loaded)} plugins loaded" if active else "not running")
info["servers"] = servers
info["metaFailures"] = meta_failed[:10]

# --- the plugins the repository ships, installed and loaded ---
package_js = read(os.path.join(REPO, "scripts/package-plugins.mjs"))
m = re.search(r"PLUGINS\s*=\s*\[(.*?)\]", package_js, re.S)
required = re.findall(r"'([^']+)'", m.group(1)) if m else []
plugins_dir = os.path.join(CSGO, "addons/counterstrikesharp/plugins")
plugin_info = []
for name in required:
    dll = os.path.join(plugins_dir, name, f"{name}.dll")
    if not os.path.isfile(dll):
        chk(f"plugin-{name}", name, "fail", "not installed (run deploy)"); plugin_info.append({"name": name, "installed": False}); continue
    stat = os.stat(dll)
    state = "loaded" if name in loaded else ("failed" if name in failed else ("unknown" if not any(s["active"] for s in servers) else "not loaded"))
    plugin_info.append({"name": name, "installed": True, "sizeKb": stat.st_size // 1024, "modified": datetime.datetime.fromtimestamp(stat.st_mtime, datetime.timezone.utc).isoformat(), "state": state})
    detail = f"{stat.st_size // 1024} KB, {state}"
    chk(f"plugin-{name}", name, "ok" if state == "loaded" else ("warn" if state == "unknown" else "fail"), detail)
info["plugins"] = plugin_info
for item in meta_failed[:5]:
    chk("meta-" + re.sub(r"\W+", "-", item)[:24], "Metamod plugin", "fail", item)

# --- the framework: Metamod, CounterStrikeSharp, MultiAddonManager, the .NET runtime ---
gameinfo = read(os.path.join(CSGO, "gameinfo.gi"))
metamod_ok = os.path.isdir(os.path.join(CSGO, "addons/metamod")) and "csgo/addons/metamod" in gameinfo
chk("metamod", "Metamod:Source", "ok" if metamod_ok else "fail", (f"version {metamod_version}" if metamod_version else "installed") if metamod_ok else "not installed or not in gameinfo.gi")
css_version = read(os.path.join(STATE, "css-version")).strip()
css_ok = os.path.isdir(os.path.join(CSGO, "addons/counterstrikesharp")) and os.path.isfile(os.path.join(CSGO, "addons/metamod/counterstrikesharp.vdf"))
chk("css", "CounterStrikeSharp", "ok" if css_ok else "fail", (css_version or "installed") if css_ok else "not installed")
mam_version = read(os.path.join(STATE, "mam-version")).strip()
mam_ok = bool(glob.glob(os.path.join(CSGO, "addons/metamod/multiaddonmanager*")))
chk("mam", "MultiAddonManager", "ok" if mam_ok else "warn", (mam_version or "installed") if mam_ok else "not installed: players do not get the Workshop addon")
runtimes = set()
for path in glob.glob(os.path.join(CSGO, "addons/counterstrikesharp/dotnet/shared/Microsoft.NETCore.App/*")):
    mm = re.match(r"(\d+)\.", os.path.basename(path)); runtimes |= {int(mm.group(1))} if mm else set()
for entry in run(["dotnet", "--list-runtimes"]).splitlines():
    mm = re.match(r"Microsoft\.NETCore\.App (\d+)\.", entry); runtimes |= {int(mm.group(1))} if mm else set()
needed = {}
for csproj in glob.glob(os.path.join(REPO, "LegacyX-*/LegacyX-*.csproj")):
    mm = re.search(r"<TargetFramework>net(\d+)\.\d+</TargetFramework>", read(csproj))
    if mm: needed[os.path.basename(os.path.dirname(csproj))] = int(mm.group(1))
want = max(needed.values()) if needed else 8
info["framework"] = {"metamod": metamod_version, "counterstrikesharp": css_version, "multiaddonmanager": mam_version, "dotnetRuntimes": sorted(runtimes), "needsDotnet": want}
if not runtimes: chk("dotnet", ".NET runtime", "warn", "could not read the runtimes")
elif max(runtimes) >= want: chk("dotnet", ".NET runtime", "ok", f"runtimes {', '.join(map(str, sorted(runtimes)))}; the plugins need {want}")
else: chk("dotnet", ".NET runtime", "fail", f"runtimes {', '.join(map(str, sorted(runtimes)))}; a plugin needs {want}")

# --- the Workshop addon the players download ---
cfg = read(os.path.join(CSGO, "cfg/multiaddonmanager/multiaddonmanager.cfg"))
mm = re.search(r'^\s*mm_client_extra_addons\s+"?(\d+)', cfg, re.M)
if not mm:
    chk("addon", "Workshop addon", "warn", "mm_client_extra_addons is not set")
else:
    addon_id = mm.group(1)
    vpk_dir = os.path.join(CS2_DIR, f"game/bin/linuxsteamrt64/steamapps/workshop/content/730/{addon_id}/{addon_id}_dir.vpk")
    layouts = set()
    for source in glob.glob(os.path.join(REPO, "LegacyX-Hud/*.cs")):
        layouts |= set(re.findall(r"(legacyx_[a-z_]+)\.vxml_c", read(source)))
    if not os.path.isfile(vpk_dir):
        chk("addon", "Workshop addon", "fail", f"item {addon_id} is not downloaded on this host yet")
    else:
        data = open(vpk_dir, "rb").read()
        missing = sorted(name for name in layouts if name.encode() not in data)
        modified = datetime.datetime.fromtimestamp(os.stat(vpk_dir).st_mtime, datetime.timezone.utc).isoformat()
        info["addon"] = {"id": addon_id, "modified": modified, "layouts": sorted(layouts), "missing": missing}
        chk("addon", "Workshop addon", "fail" if missing else "ok", f"item {addon_id} is missing layouts: {', '.join(missing)}: publish a new build" if missing else f"item {addon_id}, all {len(layouts)} layouts present")

# --- the settings the plugins read (names only, never a value) ---
env_text = read(os.path.join(CSGO, "addons/counterstrikesharp/.env"))
bad = []
for key in ("LEGACYX_PLUGIN_TOKEN", "LEGACYX_API_BASE_URL"):
    mm = re.search(rf"^{key}=(.*)$", env_text, re.M)
    if not mm or not mm.group(1).strip() or mm.group(1).strip().startswith("REPLACE"): bad.append(key)
chk("env", "Plugin settings (.env)", "fail" if bad else "ok", f"not set: {', '.join(bad)}" if bad else "the required settings are set")

# --- the host ---
free_gb = round(shutil.disk_usage(CS2_DIR if os.path.isdir(CS2_DIR) else "/").free / 1e9, 1)
chk("disk", "Disk space", "fail" if free_gb < 5 else ("warn" if free_gb < 15 else "ok"), f"{free_gb} GB free")
info["host"] = {"diskFreeGb": free_gb}
info["actions"] = [{"action": "deploy", "target": None, "label": "Deploy plugins"}] + [{"action": "restart", "target": str(item["port"]), "label": f"Restart {item['port']}"} for item in servers]

print(json.dumps({"ok": True, "generatedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(), "checks": checks, "info": info}))
PY
}

run_locked() {
  exec 9>/var/lock/legacyx-host.lock 2>/dev/null || exec 9>/tmp/legacyx-host.lock
  flock -n 9 || fail "another action is running" 3
}

json_log() { python3 -c 'import json,sys; print(json.dumps({"ok": sys.argv[1] == "0", "log": sys.stdin.read()[-4000:]}))' "$1"; }

case "$action" in
  status) status ;;
  deploy)
    run_locked
    out="$({
      set -e
      git -C "$REPO" pull --ff-only
      cd "$REPO"
      scripts/deploy.sh "$CSGO_DIR" 2>&1 | tail -n 25
      chown -R "$(stat -c '%U:%G' "$CS2_DIR")" "$CSGO_DIR/addons" "$CSGO_DIR/cfg" 2>/dev/null || true
      echo "Deployed. Restart a server to load it."
    } 2>&1)"; rc=$?
    printf '%s' "$out" | json_log "$rc" ;;
  restart)
    [[ "${arg:-}" =~ ^[0-9]{4,5}$ && -f "$CONF_DIR/$arg.conf" ]] || fail "no server on that port"
    run_locked
    out="$(systemctl restart "cs2@$arg" 2>&1 && echo "cs2@$arg restarted")"; rc=$?
    printf '%s' "$out" | json_log "$rc" ;;
  *) fail "unknown action" ;;
esac
