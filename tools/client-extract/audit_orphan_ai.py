"""AI classes this port declares and binds to no npc, split by whether Java binds them.

WHY THIS EXISTS
---------------
A class with no npc is one of three things and they need opposite responses:

* **a lost binding** -- Java gives the class npcs and we do not, so a mechanic that exists in code
  never runs. This is the one worth finding.
* **a deliberate replacement** -- we rebound its npcs to a class built from the retail patterns, which
  is the sanctioned exception in CLAUDE.md. Restoring the binding would undo that work.
* **dead in Java too** -- event AIs, custom-instance helpers, classes bound at runtime rather than by
  `ai=`. Nothing to do.

A raw list of unused classes cannot tell them apart, and 28 of this port's 725 AI names are unused.
Comparing against the Java tree separates them: at the time of writing **none is a lost binding**.

CLI:
    python audit_orphan_ai.py [--java <npc_templates.xml>]
"""
from __future__ import annotations

import argparse
import collections
import os
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
OURS = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
HANDLERS = REPO / "src" / "Aion.GameServer" / "Handlers" / "AI"

#: Classes kept as the reference their retail-sourced replacement was measured against. Each names its
#: successor in its own remark; this list exists so the audit can report them as intended rather than
#: as findings.
REPLACED = {
    "wave_attacker": "seal_wave_attacker / seal_wave_leader",
    "hm_tiamat_weakened_dragon": "tiamat_dying_rotation",
    "tiamat_weakened_dragon": "tiamat_dying_rotation",
}


def bound(text: str) -> collections.Counter:
    return collections.Counter(m.group(1) for m in re.finditer(r'\bai="([\w_]+)"', text))


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    java_root = os.environ.get("BEYOND_AION_JAVA_ROOT", str(REPO.parent / "aion-server"))
    ap.add_argument("--java", default=str(pathlib.Path(java_root)
                                          / "game-server/data/static_data/npcs/npc_templates.xml"))
    args = ap.parse_args()

    ours = bound(OURS.read_text(encoding="utf-8", errors="replace"))
    java_path = pathlib.Path(args.java)
    java = bound(java_path.read_text(encoding="utf-8", errors="replace")) if java_path.exists() else None

    declared: dict[str, str] = {}
    for path in sorted(HANDLERS.glob("*.cs")):
        for name in re.findall(r'AIName\("([\w_]+)"\)', path.read_text(encoding="utf-8", errors="replace")):
            declared[name] = path.name

    unused = sorted(n for n in declared if ours.get(n, 0) == 0)
    print(f"{len(declared)} AI names declared, {len(unused)} bound to no npc")
    if java is None:
        print(f"  (no Java tree at {java_path}; cannot classify)")
        for name in unused:
            print(f"   {name:38s} {declared[name]}")
        return 0

    lost = [n for n in unused if java.get(n, 0) > 0 and n not in REPLACED]
    replaced = [n for n in unused if n in REPLACED]
    dead = [n for n in unused if java.get(n, 0) == 0 and n not in REPLACED]

    print(f"\n  LOST BINDINGS -- Java binds them, we do not: {len(lost)}")
    for name in lost:
        print(f"   {name:38s} {declared[name]}  ({java[name]} npcs in Java)")
    if not lost:
        print("   -- none")

    print(f"\n  replaced on purpose: {len(replaced)}")
    for name in replaced:
        print(f"   {name:38s} -> {REPLACED[name]}")

    print(f"\n  unused in Java too, nothing to do: {len(dead)}")
    print("   " + ", ".join(dead))
    return 1 if lost else 0


if __name__ == "__main__":
    sys.exit(main())
