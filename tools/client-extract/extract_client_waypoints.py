#!/usr/bin/env python3
"""Index the client's own npc routes, and convert one into this port's walker format.

`audit_waypoint_gap.py` counted 9,566 npcs whose retail AI walks a route and whose spawn data here
gives them none, and closed with "the honest options are to extract the routes from the client's own
data ... or to leave these mechanics out". This is the first half of that: the routes exist, and this
finds them.

WHERE THEY ARE
--------------
Every world directory under `Map/Worlds/<world>/` carries `world_N_WayPoint_*.xml`, holding named
routes in a shape that maps almost directly onto our `npc_walker` templates:

    <way_point>
      <name>BIDShulack_EngineerSum_NPCPath</name>
      <points>
        <data><x>..</x><y>..</y><z>..</z><stay_duration>10000</stay_duration></data>
        ...

against our

    <walker_template route_id="...">
      <routestep x=".." y=".." z=".." rest_time="10000"/>

`stay_duration` is `rest_time`; everything else is a rename. `stay_motion` has no counterpart here and
is dropped.

WHAT IS THERE
-------------
When written: **7,418 named routes with points, across 120 worlds** (a further 432 `way_point` blocks
name a route and define no points at all), and **285 of the 467 `<pathname>` values the AI patterns
reference** are among them -- 61%. The remaining 182 are named by patterns whose world file does not
define them, which needs its own look before anyone concludes they are absent.

**1,188 of those names are defined in more than one world, with different geometry.** A name is not an
identifier: `IDAbRe_Up3_DoorNPC4` exists in nine worlds. Any conversion has to carry the world through,
and `--show` refuses to guess.

WHAT THIS DOES NOT DO
---------------------
It does not write walker data. Two things are missing before that is safe:

* **A world directory is not a map id.** `idshulackship` has to become 300100000 by hand or by a
  mapping nobody has built; guessing it puts a route in the wrong instance.
* **Route ids are ours, not the client's.** Our templates are keyed by number and referenced from spawn
  data by `walker_id`; assigning those is a decision about our data, not a translation of theirs.

Converting the geometry is mechanical. Deciding which map each route belongs to, and which npc should
walk it, is not -- and a route attached to the wrong npc is a worse defect than no route at all,
because it moves an encounter somewhere it never goes.

Usage:
    python extract_client_waypoints.py --coverage
    python extract_client_waypoints.py --show BIDShulack_EngineerSum_NPCPath
"""
import argparse
import re
import sys
import pathlib

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402

WAY_POINT = re.compile(r"<way_point>\s*<name>([^<]+)</name>(.*?)</way_point>", re.S)
POINT = re.compile(r"<data>(.*?)</data>", re.S)


def client_routes(worlds_dir):
    """route name -> {world directory: [(x, y, z, rest_time or None), ...]}

    Keyed by world as well as name on purpose: **1,188 of the 7,418 names are defined in more than one
    world**, with different geometry -- `IDAbRe_Up3_DoorNPC4` exists in nine. An earlier version of this
    kept the first one it walked past, so `--show BIDShulack_EngineerSum_NPCPath` printed the arena's
    copy of a Steel Rake path without saying so. A name is not an identifier here.
    """
    routes = {}
    for f in pathlib.Path(worlds_dir).rglob("*WayPoint*.xml"):
        text = read_text(f)
        for block in WAY_POINT.finditer(text):
            points = []
            for point in POINT.finditer(block.group(2)):
                body = point.group(1)
                got = {axis: re.search(rf"<{axis}>([-0-9.]+)</{axis}>", body) for axis in "xyz"}
                if not all(got.values()):
                    continue
                rest = re.search(r"<stay_duration>(\d+)</stay_duration>", body)
                points.append((got["x"].group(1), got["y"].group(1), got["z"].group(1),
                               rest.group(1) if rest else None))
            if points:
                routes.setdefault(block.group(1).strip(), {})[f.parent.name] = points
    return routes


def referenced_paths(patterns_dir):
    wanted = set()
    for f in sorted(pathlib.Path(patterns_dir).glob("NpcAIPatterns*.xml")):
        wanted |= {p.strip() for p in re.findall(r"<pathname>([^<]+)</pathname>", read_text(f))}
    return wanted


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    ap.add_argument("--patterns", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--coverage", action="store_true", help="how much of what the AI needs is present")
    ap.add_argument("--show", help="print one route as a walker_template")
    ap.add_argument("--world", help="which world's copy of --show to print, when the name is shared")
    ap.add_argument("--missing", action="store_true", help="list the referenced paths not found")
    args = ap.parse_args()

    routes = client_routes(args.worlds)
    if not routes:
        print("no routes found -- suspect the decoding before believing this "
              "(see read_text in audit_missing_adds.py)")
        return 1

    if args.show:
        if args.show not in routes:
            print(f"{args.show}: not defined in any world file")
            return 1
        copies = routes[args.show]
        if len(copies) > 1 and not args.world:
            print(f"{args.show} is defined in {len(copies)} worlds with different geometry:")
            for w in sorted(copies):
                print(f"  --world {w}   ({len(copies[w])} points)")
            print()
            print("Pick one. A name is not an identifier in this data.")
            return 1
        world = args.world or next(iter(copies))
        if world not in copies:
            print(f"{args.show} is not defined in {world}")
            return 1
        points = copies[world]
        print(f"<!-- {args.show}, from Map/Worlds/{world}. route_id is ours to choose; see the")
        print("     module docstring on why this is not written into npc_walker automatically. -->")
        print('<walker_template route_id="TODO">')
        for x, y, z, rest in points:
            rest_attr = f' rest_time="{rest}"' if rest else ""
            print(f'\t<routestep x="{x}" y="{y}" z="{z}"{rest_attr}/>')
        print("</walker_template>")
        return 0

    wanted = referenced_paths(args.patterns)
    present = wanted & set(routes)
    if args.missing:
        for name in sorted(wanted - set(routes)):
            print(name)
        return 0

    worlds = {w for copies in routes.values() for w in copies}
    shared = sum(1 for copies in routes.values() if len(copies) > 1)
    print(f"{len(routes)} named routes across {len(worlds)} worlds")
    print(f"{len(wanted)} distinct pathnames referenced by AI patterns")
    print(f"{len(present)} present ({100 * len(present) // max(1, len(wanted))}%), "
          f"{len(wanted - set(routes))} not")
    print(f"{shared} of the {len(routes)} names are defined in more than one world, with different")
    print("geometry -- a name does not identify a route, and any conversion has to carry the world.")
    print()
    print("longest of the ones the AI asks for:")
    def longest(name):
        return max(len(pts) for pts in routes[name].values())

    for name in sorted(present, key=lambda n: -longest(n))[:10]:
        copies = routes[name]
        world = max(copies, key=lambda w: len(copies[w]))
        points = copies[world]
        rests = sum(1 for p in points if p[3])
        note = f" (+{len(copies) - 1} other worlds)" if len(copies) > 1 else ""
        print(f"  {len(points):4d} points  {rests:2d} rests  {name[:44]:46s} world={world}{note}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
