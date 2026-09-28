#!/usr/bin/env python3
"""Keeps WeaponPaints' signature valid after CS2 updates, without a human.

    fix-gamedata.py <libserver.so> <weaponpaints.json>

Checks whether the Linux signature in weaponpaints.json matches exactly one place in libserver.so.
If not, it tries the signatures maintained upstream (WeaponPaints, swiftlys2) and writes the first one
that matches exactly once. Exactly once matters: a pattern that matches elsewhere points at the wrong
function and would crash the server, one that matches nowhere only leaves skins off.

Exit codes: 0 = the file holds a valid signature (unchanged or fixed), 3 = none of the candidates
matches this CS2 build yet (try again later), 2 = usage or file error.
"""
import json
import re
import sys
import urllib.request

KEY = "CAttributeList_SetOrAddAttributeValueByName"
SOURCES = [
    # (url, how to find the Linux signature in the text)
    ("https://raw.githubusercontent.com/Nereziel/cs2-WeaponPaints/main/gamedata/weaponpaints.json",
     r'"CAttributeList_SetOrAddAttributeValueByName"[^}]*?"linux"\s*:\s*"([0-9A-Fa-f? ]+)"'),
    ("https://raw.githubusercontent.com/swiftly-solution/swiftlys2/master/plugin_files/gamedata/cs2/core/signatures.jsonc",
     r'"CAttributeList::SetOrAddAttributeValueByName"[^}]*?"linux"\s*:\s*"([0-9A-Fa-f? ]+)"'),
]


def pattern(signature):
    """'55 48 ? F3' -> a bytes regex; '?' / '??' match any byte."""
    parts = []
    for token in signature.split():
        if token.strip("?") == "":
            parts.append(b".")
        else:
            parts.append(re.escape(bytes([int(token, 16)])))
    return re.compile(b"".join(parts), re.DOTALL)


def matches(binary, signature):
    """How many places match (stops counting at 2)."""
    count = 0
    for _ in pattern(signature).finditer(binary):
        count += 1
        if count > 1:
            break
    return count


def candidates():
    for url, expression in SOURCES:
        try:
            with urllib.request.urlopen(url, timeout=20) as response:
                text = response.read().decode("utf-8", "replace")
        except Exception as error:  # network, 404: just skip this source
            print(f"  {url}: unreachable ({type(error).__name__})")
            continue
        found = re.search(expression, text, re.DOTALL)
        if found:
            yield url, found.group(1).strip()


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    library, gamedata_path = sys.argv[1], sys.argv[2]
    try:
        with open(library, "rb") as handle:
            binary = handle.read()
        with open(gamedata_path, encoding="utf-8-sig") as handle:
            gamedata = json.load(handle)
    except (OSError, ValueError) as error:
        print(f"!! {error}")
        return 2

    signatures = gamedata[KEY]["signatures"]
    current = signatures.get("linux", "")
    if current and matches(binary, current) == 1:
        print("WeaponPaints signature is valid for this CS2 build.")
        return 0

    print("WeaponPaints signature does not match this CS2 build; trying maintained ones.")
    for url, signature in candidates():
        count = matches(binary, signature)
        print(f"  {signature}  ({url.split('/')[4]}): {count if count < 2 else '2+'} match(es)")
        if count == 1:
            signatures["linux"] = signature
            with open(gamedata_path, "w", encoding="utf-8") as handle:
                json.dump(gamedata, handle, indent="\t")
                handle.write("\n")
            print("WeaponPaints signature updated.")
            return 0
    print("No maintained signature matches this CS2 build yet; skins stay off until one does.")
    return 3


if __name__ == "__main__":
    sys.exit(main())
