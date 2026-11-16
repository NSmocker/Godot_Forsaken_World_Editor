# -*- coding: utf-8 -*-
"""Manifest of the first 100 checklist entries: ID, relative GFX path, full path, SHA-256.

Reads GFX_CPP_UNITY_CHECKLIST.md rows GFX-00001..GFX-00100 and verifies the files
exist under the asset root B:/Git/PW146RUUnpackedAssets/gfx/.
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

CHECKLIST = Path(r"B:\Git\Godot_Forsaken_World_Editor\GFX_CPP_UNITY_CHECKLIST.md")
GFX_ROOT = Path(r"B:\Git\PW146RUUnpackedAssets\gfx")
OUT = Path(r"B:\Git\Godot_Forsaken_World_Editor\.validation\gfx-cpp-unity-check")
COUNT = 100

ROW_RE = re.compile(
    r"^\| (GFX-\d{5}) \| \[( |x)\] \| `([^`]+)` \| (Не перевірено|У роботі|Перевірено|Заблоковано)"
)


def main() -> int:
    text = CHECKLIST.read_text(encoding="utf-8")
    entries = []
    for line in text.splitlines():
        match = ROW_RE.match(line.strip())
        if not match:
            continue
        gfx_id, _done, rel, state = match.groups()
        if gfx_id >= "GFX-%05d" % (COUNT + 1):
            break
        if state != "Не перевірено":
            print(f"skip {gfx_id}: already {state}")
            continue
        full = GFX_ROOT / rel
        if not full.is_file():
            print(f"MISSING {gfx_id}: {full}")
            continue
        sha = hashlib.sha256(full.read_bytes()).hexdigest()
        entries.append({"id": gfx_id, "rel": rel, "full": str(full), "sha256": sha})
    if len(entries) != COUNT:
        print(f"expected {COUNT} entries, got {len(entries)}")
        return 1
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "manifest.json").write_text(
        json.dumps(
            {
                "generated": "2026-11-16",
                "source": "GFX_CPP_UNITY_CHECKLIST.md GFX-00001..GFX-00100",
                "gfx_root": str(GFX_ROOT),
                "count": len(entries),
                "entries": entries,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"manifest written: {len(entries)} entries")
    return 0


if __name__ == "__main__":
    sys.exit(main())
