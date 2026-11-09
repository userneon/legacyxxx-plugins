#!/usr/bin/env python3
"""Lists the agent models inside the Workshop addon's VPK and (with --write) puts them on the .env precache line.
Usage: python3 -I lx_agents.py <dir.vpk> <.env> [--write]"""
import struct, sys

def read_tree(path):
    with open(path, "rb") as f:
        data = f.read()
    sig, ver = struct.unpack_from("<II", data, 0)
    if sig != 0x55AA1234:
        sys.exit("not a VPK file")
    if ver == 1:
        tree_size, = struct.unpack_from("<I", data, 8); pos = 12
    elif ver == 2:
        tree_size, = struct.unpack_from("<I", data, 8); pos = 28
    else:
        sys.exit(f"unknown VPK version {ver}")
    end = pos + tree_size
    def cstr():
        nonlocal pos
        stop = data.index(b"\0", pos)
        text = data[pos:stop].decode("utf-8", "replace"); pos = stop + 1
        return text
    files = []
    while pos < end:
        ext = cstr()
        if ext == "": break
        while True:
            directory = cstr()
            if directory == "": break
            while True:
                name = cstr()
                if name == "": break
                _crc, preload, _arch, _off, _len, _term = struct.unpack_from("<IHHIIH", data, pos)
                pos += 18 + preload
                full = ("" if directory == " " else directory + "/") + ("" if name == " " else name) + ("" if ext == " " else "." + ext)
                files.append(full)
    return files

def main():
    if len(sys.argv) < 3: sys.exit(__doc__)
    vpk, env = sys.argv[1], sys.argv[2]
    write = "--write" in sys.argv[3:]
    agents = sorted({f[:-2].lower() for f in read_tree(vpk) if f.lower().endswith(".vmdl_c") and f.lower().startswith("agents/models/bluesen/")})
    print(f"{len(agents)} agent models in the addon")
    for a in agents[:3]: print("  ", a)
    if not write: return
    lines = open(env, encoding="utf-8").read().split("\n")
    key = "LEGACYX_COMMUNITY_MODEL_PRECACHE="
    for i, line in enumerate(lines):
        if line.startswith(key):
            have = [p.strip() for p in line[len(key):].split(",") if p.strip()]
            merged = have + [a for a in agents if a not in have]
            lines[i] = key + ",".join(merged)
            open(env + ".bak", "w", encoding="utf-8").write("\n".join(open(env, encoding="utf-8").read().split("\n")))
            open(env, "w", encoding="utf-8").write("\n".join(lines))
            print(f"precache line now has {len(merged)} paths (backup: {env}.bak)")
            return
    sys.exit("no LEGACYX_COMMUNITY_MODEL_PRECACHE line in the .env")

main()
