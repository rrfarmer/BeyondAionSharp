"""Live NPCs left on a stock AI while their pattern-mates got a bespoke class.

Written after finding the gap in my own work. `ND2_Callsoulst` binds fourteen npcs; the lich commit
repointed four of them, because the ids came from the first page of a survey rather than from the
pattern's membership. Three live liches were left doing nothing, and the same turned out to be true of
six other classes shipped in the same fortnight — **thirty-seven npcs in total**.

The mistake is easy to make and invisible afterwards: the class is correct, its pins pass, the encounter
works for the npc it was written against, and the siblings are silent in a way nothing measures.

**Rule: repoint by enumerating the pattern's members, not by copying ids out of a survey's output.**

Usage:
    python audit_missed_siblings.py <binding_tsv> [--repo ..] [--classes a,b,c]

With `--classes`, only those AI names are considered — which is how to ask "did *my* recent work miss
anything" rather than "does the whole tree have this shape". Without it, every pattern that mixes a
bespoke class with live stock-AI npcs is listed, and **most of those are not gaps**: `LMerchant` binds
five hundred npcs of which one is a named quest-giver, and `D2_FnA` is retail's generic monster pattern.
A pattern whose bespoke class was written *for that pattern* is the case worth acting on, and only the
person who wrote it can say which those are.
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

STOCK = {"aggressive", "general", "passive", "monster", "guard", "onedmg_passive",
         "quest_use_item", "dummy", ""}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--classes", default="", help="comma-separated AI names to restrict to")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    wanted = {c.strip() for c in args.classes.split(",") if c.strip()}

    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        m = re.search(r'ai="([^"]*)"', attrs)
        ai_of[npc_id] = m.group(1) if m else ""
        m = re.search(r'name="([^"]*)"', attrs)
        name_of[npc_id] = m.group(1) if m else ""

    placed: set[str] = set()
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        placed.update(re.findall(r'<spawn npc_id="(\d+)"',
                                 path.read_text(encoding="utf-8", errors="replace")))

    with open(args.binding_tsv, encoding="utf-8") as fh:
        rows = [line.rstrip("\n").split("\t") for line in fh]
    col = {c: i for i, c in enumerate(rows[0])}
    by_pattern: dict[str, list[str]] = collections.defaultdict(list)
    for row in rows[1:]:
        by_pattern[row[col["pattern_name"]]].append(row[col["npc_id"]])

    total = 0
    for pattern, ids in sorted(by_pattern.items()):
        bespoke = {ai_of.get(i, "") for i in ids} - STOCK
        if wanted:
            bespoke &= wanted
        if not bespoke:
            continue
        missed = [i for i in ids if i in placed and ai_of.get(i, "") in STOCK]
        if not missed:
            continue
        total += len(missed)
        print(f"{pattern:32} {','.join(sorted(bespoke)):28} {len(missed)} missed")
        for i in missed:
            print(f"        {i}  {name_of.get(i, '')}")

    print()
    print(f"{total} live npcs on a stock AI whose pattern-mates carry a bespoke class.")
    if not wanted:
        print("Unfiltered, most of these are not gaps -- pass --classes to ask about your own work.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
