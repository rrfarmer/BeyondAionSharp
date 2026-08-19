"""Find npcs that are asked to cast and have nothing to cast.

`UseSkillAndDieAI` reads `getSkillList().getNpcSkills()`; **when that list is empty it calls
`Delete()` immediately** and the npc is gone before anyone sees it. Every other cast-driven AI has some
version of the same hole. So an npc bound to one of those AIs with no row in `static_data/npc_skills/`
is not a quiet data gap -- it is a mechanic that silently does nothing.

This was found twice in one session from opposite directions:

* The boss harness loaded only the top-level `npc_skills.xml`, so **every instance npc's list was empty
  inside tests** and the Eternal Bastion assault pod's strike npc vanished before any assertion could
  see it. That was a harness bug, not a data one.
* Vasharti's glove smashes and Terath's black hole both have a **b-prefixed second-generation twin**
  (856345/856346 against 283008/283009). Retail's pattern names the b-prefixed pair; our data carries
  skills only for the older one. Adopting retail's id there would have replaced a working cast-and-die
  with an npc that stands and melees.

So the question this audit answers is: **which npcs would go silent if something spawned them?**

Usage:
    python audit_skilless_casters.py [--repo PATH] [--spawned-only]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

#: AI names whose whole behaviour is "cast something". An npc on one of these with no skill row does
#: nothing at all; several delete themselves outright.
CASTER_AIS = {
    "useskillanddie": "deletes itself immediately when the list is empty",
    "useskillonspawn": "casts nothing",
    "skillarea": "casts nothing",
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--spawned-only", action="store_true",
                    help="only npcs some AI class or spawn file actually places")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    static = repo / "game-server" / "data" / "static_data"

    # Every npc id that has a skill row, from every file in the directory -- which is how the server
    # merges them.
    with_skills: set[int] = set()
    for path in (static / "npc_skills").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for group in re.findall(r'npc_ids="([^"]+)"', text):
            for npc_id in group.split():
                if npc_id.isdigit():
                    with_skills.add(int(npc_id))

    # Every npc bound to a caster AI.
    templates = (static / "npcs" / "npc_templates.xml").read_text(encoding="utf-8", errors="replace")
    casters: dict[int, tuple[str, str]] = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        ai = re.search(r'\bai="([^"]*)"', attrs)
        if not ai or ai.group(1).lower() not in CASTER_AIS:
            continue
        name = re.search(r'\bname="([^"]*)"', attrs)
        casters[int(npc_id)] = (ai.group(1), (name.group(1) if name else "").strip())

    # Who places them: AI handler sources, and the static spawn tables.
    placed_by: dict[int, set[str]] = collections.defaultdict(set)
    for path in (repo / "src" / "Aion.GameServer" / "Handlers").rglob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for npc_id in set(re.findall(r"\b(\d{6})\b", text)):
            if int(npc_id) in casters:
                placed_by[int(npc_id)].add(path.name)
    for path in (static / "spawns").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for npc_id in set(re.findall(r'<spawn npc_id="(\d+)"', text)):
            if int(npc_id) in casters:
                placed_by[int(npc_id)].add("(spawn table)")

    silent = sorted(npc for npc in casters if npc not in with_skills)
    reachable = [npc for npc in silent if placed_by.get(npc)]

    print(f"{len(casters)} npcs on a caster AI, {len(silent)} of them with no skill row.")
    print(f"{len(reachable)} of those are placed by something in this port.\n")

    for npc_id in (reachable if args.spawned_only else silent):
        ai, name = casters[npc_id]
        where = ", ".join(sorted(placed_by.get(npc_id, ()))) or "nothing places it"
        print(f"{npc_id}  {name or '(unnamed)':38s} ai={ai}")
        print(f"          {CASTER_AIS[ai.lower()]}; {where}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
