#!/usr/bin/env python3
"""Size the gap between npcs whose retail AI walks a route and npcs this port gives a route to.

Three separate mechanics have now been left unwritten because the npc that drives them has no path:

* Muragan the Loyal's escort walk, whose six waypoints are a route our spawn data does not carry;
* Engineer Lahulahu's summon wave, armed from four `on_arrived_at_waypoint` rungs;
* the fortress killers' `goto_waypoint` walk to the guards they have come to kill.

Each looked like a one-off. This counts them.

WHAT IT COMPARES
----------------
* **Retail side**: every AI pattern using `goto_waypoint`, `on_arrived_at_waypoint`,
  `is_waypoint_index` or `SPAWN_LOCATION_WAY_POINT_START`, and the npcs bound to those patterns.
* **Our side**: every npc whose spawn data gives a `<spot>` a `walker_id`.

The overlap is what matters: an npc whose pattern walks *and* that we route. Everything else in the
retail column is a pattern whose movement -- and anything hanging off it -- cannot run here.

WHAT IT FOUND WHEN WRITTEN
--------------------------
9,612 of our npcs run a pattern that uses waypoints. **46** of them have a walker route.

The 2,213 npcs we do route are, with those 46 exceptions, **not** the ones retail walks from AI: our
route data covers ambient patrols, and retail's waypoint-driven encounters are a different set almost
entirely. So this is not "our routes are incomplete" -- it is that the two bodies of data are about
different npcs.

Usage:  python audit_waypoint_gap.py [patterns_dir]
"""
import re
import sys
import pathlib
import collections

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text, PATTERN_RE, NAME_RE  # noqa: E402

OPS = ("goto_waypoint", "on_arrived_at_waypoint", "is_waypoint_index", "SPAWN_LOCATION_WAY_POINT_START")

REPO = pathlib.Path(__file__).resolve().parents[2]
BINDING = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
SPAWNS = REPO / "game-server" / "data" / "static_data" / "spawns"


def walking_patterns(patterns_dir):
    """pattern name -> Counter of which waypoint ops it uses."""
    found = {}
    for f in sorted(pathlib.Path(patterns_dir).glob("NpcAIPatterns*.xml")):
        text = read_text(f)
        for block in PATTERN_RE.finditer(text):
            body = block.group(1)
            named = NAME_RE.search(body)
            if not named:
                continue
            used = collections.Counter({op: body.count(op) for op in OPS if op in body})
            if used:
                found[named.group(1).strip()] = used
    return found


def routed_npcs():
    """npc ids whose spawn data gives a spot a walker_id."""
    routed = set()
    for f in SPAWNS.rglob("*.xml"):
        text = f.read_text(encoding="utf-8", errors="replace")
        current = None
        for segment in re.split(r'(<spawn\s+npc_id="\d+")', text):
            head = re.match(r'<spawn\s+npc_id="(\d+)"', segment or "")
            if head:
                current = int(head.group(1))
            elif current is not None and segment and "walker_id=" in segment:
                routed.add(current)
    return routed


def main():
    patterns_dir = sys.argv[1] if len(sys.argv) > 1 else "D:/Aion58ServerTesting/Server/Map/XML"
    walking = walking_patterns(patterns_dir)
    if not walking:
        print("no patterns matched -- suspect the decoding before believing this "
              "(see read_text in audit_missing_adds.py)")
        return 1

    ours = collections.defaultdict(set)   # pattern -> our npc ids
    for line in BINDING.read_text(encoding="utf-8", errors="replace").splitlines():
        fields = line.split("\t")
        if len(fields) >= 3 and fields[0].isdigit() and fields[2].strip() in walking:
            ours[fields[2].strip()].add(int(fields[0]))

    routed = routed_npcs()
    every = {npc for ids in ours.values() for npc in ids}
    covered = every & routed

    totals = collections.Counter()
    for used in walking.values():
        totals.update(used)

    print(f"{len(walking)} retail patterns use a waypoint op; {len(ours)} of them are bound to our npcs\n")
    for op, n in totals.most_common():
        print(f"  {n:6d}  {op}")
    print(f"\n{len(every)} of our npcs run one of those patterns")
    print(f"{len(covered)} of them have a walker_id in our spawn data")
    print(f"{len(every - routed)} do not -- their pattern walks and our data does not\n")
    print(f"for scale: {len(routed)} npcs have a walker_id anywhere in our spawn data, so the two")
    print("bodies of data are largely about different npcs rather than one being incomplete.\n")

    print("patterns with the most of our npcs behind them, largest first:")
    for pattern, ids in sorted(ours.items(), key=lambda kv: -len(kv[1]))[:15]:
        missing = len(ids - routed)
        print(f"  {len(ids):5d} npcs  {missing:5d} unrouted  {pattern[:56]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
