#!/usr/bin/env python3
"""The client's own devname -> npc id table, for every npc rather than only those carrying AI.

WHY THIS EXISTS
---------------
Everything built in this work resolves `npc_nameid` devnames through `ai_binding.tsv`. That table is
derived from the AI patterns, so **it only knows npcs that carry one**. Gates, flags, portals, treasure
boxes, healing objects and effect markers carry none, and every spawn action naming one of those silently
resolved to nothing.

> **509 devnames — 1,132 spawn actions, 6% of every spawn in the pattern set — were invisible to every
> audit here.** Not reported as missing: reported as nothing at all.

Berserk Anoha is how it surfaced. `audit_summon_ids.py` claimed his class was missing an id, and the
pattern that supposedly spawned it turned out to spawn two faction commanders whose devnames resolved to
nothing, so the audit was reasoning about a set it had built with holes in it.

WHERE THE TABLE IS
------------------
`Map/XML/npcs.xml`, `npcs_monsters.xml` and `npcs_std_monsters.xml`, as plain pairs:

```xml
<id>200000</id><name>SkillZone</name>
```

**87,734 names**, against `ai_binding.tsv`'s 69,184.

WHY IT IS SAFE TO PREFER
------------------------
Checked before adopting, not after:

* it resolves **all 509** of the devnames `ai_binding.tsv` could not — 100%;
* where both tables know a name they **never disagree** — 0 clashes across the 5,948 names in common.

So it is a strict superset, not a competing opinion. `ai_binding.tsv` is still the source for which
*pattern* an npc runs; this is only about turning a devname into an id.

Usage:  python client_npc_names.py [--xml DIR] [--check]
"""
import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402

FILES = ("npcs.xml", "npcs_monsters.xml", "npcs_std_monsters.xml")
PAIR = re.compile(r"<id>(\d+)</id>\s*<name>([^<]+)</name>")

_cache: dict = {}


def npc_names(xml_dir="D:/Aion58ServerTesting/Server/Map/XML"):
    """devname -> npc id, from the client's npc tables. Cached; the files are 750MB of UTF-16."""
    key = str(xml_dir)
    if key in _cache:
        return _cache[key]
    out = {}
    for name in FILES:
        path = pathlib.Path(xml_dir) / name
        if not path.exists():
            continue
        for npc_id, devname in PAIR.findall(read_text(path)):
            out.setdefault(devname, npc_id)
    _cache[key] = out
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--check", action="store_true",
                    help="re-run the two checks that justified preferring this table")
    args = ap.parse_args()

    table = npc_names(args.xml)
    print(f"{len(table)} devnames in the client's npc tables")

    if args.check:
        repo = pathlib.Path(__file__).resolve().parents[2]
        binding = {}
        for line in (repo / "tools" / "client-extract" / "out" / "ai_binding.tsv").read_text(
                encoding="utf-8").splitlines()[1:]:
            parts = line.split("\t")
            if len(parts) > 1 and parts[1]:
                binding.setdefault(parts[1], parts[0])
        referenced = set()
        for f in sorted(pathlib.Path(args.xml).glob("NpcAIPatterns*.xml")):
            referenced.update(re.findall(r"<npc_nameid>([^<]+)</npc_nameid>", read_text(f)))

        gaps = [n for n in referenced if n not in binding]
        closed = [n for n in gaps if n in table]
        both = [n for n in referenced if n in binding and n in table]
        clashes = [n for n in both if binding[n] != table[n]]
        print(f"{len(gaps)} devnames ai_binding.tsv cannot resolve; this table resolves {len(closed)}")
        print(f"{len(both)} known to both; {len(clashes)} disagreements")
        for n in clashes[:10]:
            print(f"   {n}: binding={binding[n]} client={table[n]}")
        return 0 if not clashes else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
