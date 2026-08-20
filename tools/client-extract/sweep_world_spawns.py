#!/usr/bin/env python3
"""Every world at once: which maps are missing retail spawn points this port could carry today.

WHY THIS EXISTS
---------------
`audit_world_spawns.py` found that Drakenspire Depths was missing sixteen npcs that retail places with
**no condition at all** -- including the one whose whole job is to seed the instance's spawn variables.
It had only ever been run against that one world. This runs it against all of them.

> The question this answers is narrow on purpose: *what does retail spawn unconditionally that we do
> not spawn at all?* Everything behind an `extcondition` is excluded, because those are supposed to be
> absent until something happens, and the progression engine that would make them appear does not exist
> here yet.

So every row below is work that needs **no new engine**: coordinates exist, the gate is empty, and the
npc simply is not placed.

WHAT A ROW DOES NOT MEAN
------------------------
**Not every absent npc is a defect.** Three honest reasons an ungated retail spawn is missing here:

* this port places it from an instance handler in code rather than from spawn data;
* it is a client-side decoration with no server behaviour;
* it is furniture whose `ai` here would make it hostile -- the case `audit_world_spawns.py --emit`
  already holds back, and this sweep counts separately under `held`.

The sweep reports; the reading is still a person's job. What it removes is the guessing about *where*
to look.

MATCHING WORLDS TO MAPS
-----------------------
Retail names a world by its folder (`IDSeal`); this port names it by map id. `world_maps.xml` carries
both -- its `cName` is exactly the retail folder name -- so the join is direct and no guessing is
involved. Worlds with no `cName` entry here are reported as unmapped rather than silently skipped.

Usage:  python sweep_world_spawns.py [--limit N] [--min N]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402
from audit_world_spawns import AGGRESSIVE, our_ai, spawns_in  # noqa: E402

GENERIC = ("", "general", "noaction", "aggressive", "aggressive_no_loot", "guard", "monster",
           "dummy", "npc", "quest_npc")
from client_npc_names import npc_names, unattackable_ids  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]


def maps_by_cname():
    """cName -> (map id, readable name), straight from world_maps.xml."""
    text = (REPO / "game-server" / "data" / "static_data" / "world_maps.xml").read_text(
        encoding="utf-8", errors="replace")
    out = {}
    for map_id, name, cname in re.findall(
            r'<map id="(\d+)"\s+name="([^"]*)"\s+cName="([^"]*)"', text):
        # **Keyed lower-case on purpose.** Retail's folder is `ab1` and this file says `Ab1`; the first
        # version of this sweep joined them case-sensitively and silently dropped 125 of the 161 maps,
        # reporting a quarter of the data as though it were all of it. Windows hides the difference at
        # the filesystem and Python does not hide it in a set.
        out[cname.lower()] = (map_id, name)
    return out


def spawned_ids_by_map():
    """map id -> the npc ids this port spawns there, across every spawn file."""
    out = collections.defaultdict(set)
    for path in (REPO / "game-server" / "data" / "static_data" / "spawns").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        # One file can hold several spawn_map blocks, so split rather than assume one id per file.
        for block in re.split(r"(?=<spawn_map\b)", text):
            m = re.match(r'<spawn_map\s+map_id="(\d+)"', block)
            if m:
                out[m.group(1)].update(re.findall(r'npc_id="(\d+)"', block))
    return out


def ids_named_in_code():
    """Every six-digit literal appearing in the C# sources.

    **A heuristic, and deliberately a generous one.** Some npcs are placed by an instance handler in code
    rather than by spawn data -- The Shugo Emperor's Vault spawns its stage adds that way -- and those are
    not missing at all. Rather than caveat that in prose, this subtracts them, and errs towards
    subtracting too much: a six-digit number in a source file might be a skill id or a coordinate, so an
    npc listed as *absent and not named in code* has cleared a bar that is harder than it needs to be.
    That is the right direction for a list meant to be worked through.
    """
    found = set()
    for path in (REPO / "src").rglob("*.cs"):
        found.update(re.findall(r"\b(\d{6})\b", path.read_text(encoding="utf-8", errors="replace")))
    return found


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--limit", type=int, default=40)
    ap.add_argument("--min", type=int, default=1, help="only report worlds owing at least this many")
    args = ap.parse_args()

    ids = npc_names(args.xml)
    furniture = unattackable_ids(args.xml)
    ai_of = our_ai()
    cnames = maps_by_cname()
    spawned = spawned_ids_by_map()
    in_code = ids_named_in_code()

    rows = []
    unmapped = []
    for folder in sorted(pathlib.Path(args.worlds).iterdir()):
        world_xml = folder / "world.xml"
        if not world_xml.exists():
            continue
        if folder.name.lower() not in cnames:
            unmapped.append(folder.name)
            continue

        map_id, readable = cnames[folder.name.lower()]
        have = spawned.get(map_id, set())

        ungated, gated_ids = set(), set()
        try:
            for _, gate, dev, _, _, _ in spawns_in(world_xml):
                npc_id = ids.get(dev)
                if npc_id is None:
                    continue
                (gated_ids if gate else ungated).add(npc_id)
        except Exception as exc:                      # a world that will not parse is a finding, not a crash
            rows.append((-1, 0, 0, folder.name, readable, map_id, f"unreadable: {exc}", []))
            continue

        absent = {n for n in ungated if n not in have}
        held = {n for n in absent if n in furniture and ai_of.get(n, "") in AGGRESSIVE}
        # The strongest signal in the whole sweep: an npc this port gave a BESPOKE AI class and then
        # never placed. Somebody read the retail pattern, wrote the behaviour and tested it, and the
        # npc cannot appear -- which is precisely what happened to all twenty-seven of Drakenspire
        # Depths' wave npcs. A generic `ai` says nothing; a named class says the work is already done.
        classed = sorted(n for n in absent - held
                         if ai_of.get(n, "") not in GENERIC and n not in in_code)
        rows.append((len(absent - held), len(held), len(gated_ids - have),
                     folder.name, readable, map_id, "", classed))

    rows.sort(key=lambda r: (-r[0], r[3]))
    owing = [r for r in rows if r[0] >= args.min]
    print(f"{len(rows)} retail worlds matched to a map here; {len(unmapped)} had no cName entry")
    print(f"{len(owing)} of them are missing at least {args.min} ungated npc(s)\n")
    print(f"  {'absent':>6} {'held':>5} {'gated':>6}  {'map':<11} world / name")
    for absent, held, gated, folder, readable, map_id, note, classed in owing[:args.limit]:
        if note:
            print(f"  {'?':>6} {'':>5} {'':>6}  {map_id:<11} {folder} -- {note}")
            continue
        flag = f"  <-- {len(classed)} with a bespoke class here" if classed else ""
        print(f"  {absent:>6} {held:>5} {gated:>6}  {map_id:<11} {folder} / {readable[:34]}{flag}")
    if len(owing) > args.limit:
        print(f"  ... and {len(owing) - args.limit} more")

    print(f"\ntotals: {sum(r[0] for r in rows)} ungated npcs absent and placeable, "
          f"{sum(r[1] for r in rows)} held back as hostile furniture, "
          f"{sum(r[2] for r in rows)} gated npcs awaiting the progression engine")
    worst = [r for r in rows if r[7]]
    if worst:
        total = sum(len(r[7]) for r in worst)
        print(f"\n{total} npcs across {len(worst)} worlds have a bespoke AI class "
              "here and no spawn point:")
        devname = {v: k for k, v in ids.items()}
        for row in sorted(worst, key=lambda r: -len(r[7]))[:12]:
            print(f"  {row[3]} / {row[4]}")
            for npc_id in row[7][:8]:
                print(f"      {npc_id}  ai={ai_of.get(npc_id):<28} {devname.get(npc_id, '?')[:40]}")
            if len(row[7]) > 8:
                print(f"      ... and {len(row[7]) - 8} more")

    if unmapped:
        print(f"\nunmapped retail worlds ({len(unmapped)}): {', '.join(unmapped[:20])}"
              f"{' ...' if len(unmapped) > 20 else ''}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
