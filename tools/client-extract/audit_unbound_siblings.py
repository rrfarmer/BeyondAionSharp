"""Find npcs left on a generic AI while a sibling sharing their retail pattern has a real class.

**This is the shape three separate finds took**, each caught by hand and each one npc away from work that
was already done:

- **216952**, the normal Yamennes, on `aggressive` while 216960 ran `yamennes` off the same fight.
- **236280** and **856032**, both Wrathclaws, on `aggressive` while their three siblings — in each of two
  hard-mode id sets — were bound to `tiamats_incarnation`.

In every case the pattern was already translated and the class already existed. Nothing had to be
written; a template attribute had to be changed. **That is the cheapest kind of missing mechanic there
is, and nothing in this repository looked for it.**

The rule this applies: an npc whose retail `pattern_name` is shared with another npc that runs a
*specific* AI class is very likely meant to run it too. Not certainly — a pattern name can be shared by
npcs that differ in ways a class legitimately gates on, as the hard/normal Yamennes golems do — so this
reports candidates for reading, not edits to make.

**Read the pattern and the class before acting on a row.** Two npcs on one pattern can still want
different behaviour, and the standing rule that keeps paying is to open the file first.

Usage:
    python audit_unbound_siblings.py [--min-shared 1]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
BINDING = pathlib.Path(__file__).parent / "out" / "ai_binding.tsv"

# The AI names that mean "nothing was ported for this npc". Everything else is a real class or a
# purpose-built shared one (portal, trap, servant...) and is not evidence of an omission.
GENERIC = {
    "aggressive",
    "aggressive_no_loot",
    "general",
    "noaction",
    "dummy",
    "peace",
    "passive",
}

TEMPLATE = re.compile(r'npc_id="(\d+)"[^>]*?\bai="([a-z_0-9]+)"')
NAME = re.compile(r'npc_id="(\d+)"[^>]*?\bname="([^"]*)"')


def templates() -> tuple[dict[str, str], dict[str, str]]:
    text = TEMPLATES.read_text(encoding="utf-8")
    return (
        {npc_id: ai for npc_id, ai in TEMPLATE.findall(text)},
        {npc_id: name for npc_id, name in NAME.findall(text)},
    )


def patterns() -> dict[str, str]:
    """npc id -> retail pattern name, for the rows that resolve to one."""
    out: dict[str, str] = {}
    with open(BINDING, encoding="utf-8") as fh:
        next(fh)
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) > 3 and cols[3]:
                out[cols[0]] = cols[3]
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--min-shared", type=int, default=1,
                    help="only report patterns with at least this many bound siblings")
    # A pattern with two hundred bound npcs is siege or artifact infrastructure, where the loose ones are
    # a different faction's or a different fortress's copy and the AI name difference is deliberate. The
    # finds this tool is for look like sibling groups: a handful of npcs, one fight.
    ap.add_argument("--max-bound", type=int, default=0,
                    help="only report patterns with at most this many bound npcs (0 = no limit)")
    args = ap.parse_args()

    ai_by_npc, name_by_npc = templates()
    pattern_by_npc = patterns()

    by_pattern: dict[str, list[str]] = collections.defaultdict(list)
    for npc_id, pattern in pattern_by_npc.items():
        if npc_id in ai_by_npc:
            by_pattern[pattern].append(npc_id)

    rows = []
    for pattern, npc_ids in by_pattern.items():
        bound = [n for n in npc_ids if ai_by_npc[n] not in GENERIC]
        loose = [n for n in npc_ids if ai_by_npc[n] in GENERIC]
        if not loose or len(bound) < args.min_shared:
            continue
        if args.max_bound and len(bound) > args.max_bound:
            continue
        # One class may be reached by several AI names; report every one so the reader can tell whether
        # the siblings agree with each other before assuming which the loose npc should take.
        classes = sorted({ai_by_npc[n] for n in bound})
        rows.append((len(loose), pattern, classes, sorted(bound, key=int), sorted(loose, key=int)))

    rows.sort(key=lambda r: (len(r[3]), r[0], r[1]))

    print(f"{len(rows)} patterns have both a bound npc and a loose one\n")
    for _, pattern, classes, bound, loose in rows:
        print(f"{pattern}  ->  {', '.join(classes)}")
        print(f"    bound : {' '.join(bound)}")
        for npc_id in loose:
            print(f"    LOOSE : {npc_id}  {ai_by_npc[npc_id]:<20} {name_by_npc.get(npc_id, '?')}")
        print()

    print(f"total loose npcs: {sum(r[0] for r in rows)}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
