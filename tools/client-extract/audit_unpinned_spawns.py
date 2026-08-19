"""Find adds a class spawns that no pin ever names.

**Four inert pins turned up in one session** — pins that passed with the mechanic they were named for
deleted. Two were found by running a mutation by hand, one by an unrelated audit, one by a mutation
again. Nothing checks this systematically, so four is a lower bound on how many exist.

Real mutation testing — delete each spawn, rebuild, run that class's tests, see if anything goes red —
is the honest answer and costs a rebuild per mutation. This is the cheap proxy: **for every npc id an AI
class spawns, does any test file so much as mention it?** An id that appears in no pin cannot possibly be
asserted, so the answer is a sound lower bound on what is unpinned.

**What it does not tell you.** A mentioned id may still be unpinned — Omega's clones were named by a pin
that dropped him to a health no branch matched, and Kurmata's cap was named by a pin asserting the wrong
number. This finds mechanics with *no* coverage, not mechanics with *bad* coverage. Those need the
mutation.

Usage:
    python audit_unpinned_spawns.py [--all]
"""
from __future__ import annotations

import argparse
import pathlib
import re

REPO = pathlib.Path(__file__).resolve().parents[2]
AI_DIR = REPO / "src" / "Aion.GameServer" / "Handlers" / "AI"
TEST_DIR = REPO / "tests" / "Aion.GameServer.Tests" / "Ai"

# Every way this codebase places an npc: the pattern-table verbs and the two hand-written helpers the
# Java-parity classes use.
SPAWN = re.compile(
    r"\b(?:Do\.)?Spawn(?:At|Near|OnTarget|OnAttacker|OnEachTarget|OnKiller|OnSeen|OnPath|Offset|"
    r"AsMyEnemy|For)?\(\s*(?:AggroTarget\.\w+\s*,\s*)?(?P<npc>\w+)")
CONST = re.compile(r"private const int (\w+)\s*=\s*(\d+);")
CLASS = re.compile(r"^(?:public|internal)\s+(?:sealed\s+)?class\s+(\w+)", re.M)

# An id below this is a skill or a message, not an npc. Npc ids in this data start in the 200,000s.
MIN_NPC_ID = 200000


def units(text: str) -> list[tuple[str, str]]:
    """(class name, source) per class, so constants do not leak between classes in one file."""
    hits = list(CLASS.finditer(text))
    if not hits:
        return []
    bounds = [h.start() for h in hits] + [len(text)]
    return [(hits[i].group(1), text[bounds[i]:bounds[i + 1]]) for i in range(len(hits))]


def test_text() -> str:
    return "\n".join(p.read_text(encoding="utf-8") for p in TEST_DIR.glob("*.cs"))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true",
                    help="list every class, not only those with unnamed spawns")
    args = ap.parse_args()

    tests = test_text()
    tested_ids = set(re.findall(r"\b(\d{6})\b", tests))

    rows = []
    for path in sorted(AI_DIR.glob("*.cs")):
        for class_name, unit in units(path.read_text(encoding="utf-8")):
            consts = {name: value for name, value in CONST.findall(unit)}
            spawned = set()
            for m in SPAWN.finditer(unit):
                raw = m.group("npc")
                value = consts.get(raw, raw if raw.isdigit() else None)
                if value and int(value) >= MIN_NPC_ID:
                    spawned.add(value)
            if not spawned:
                continue

            # A class with no test file at all is a different and larger problem; both are reported.
            named = {i for i in spawned if i in tested_ids}
            missing = sorted(spawned - named, key=int)
            if missing or args.all:
                rows.append((path.name, class_name, sorted(spawned, key=int), missing))

    total_spawns = sum(len(s) for _f, _c, s, _m in rows)
    total_missing = sum(len(m) for _f, _c, _s, m in rows)
    for filename, class_name, spawned, missing in rows:
        if not missing:
            continue
        print(f"{filename}  {class_name}")
        print(f"    spawns {len(spawned)}, unnamed by any pin: {' '.join(missing)}")

    print(f"\n{len(rows)} classes reported, {total_spawns} distinct npcs spawned, "
          f"{total_missing} of them named by no pin")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
