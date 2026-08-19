#!/usr/bin/env python3
"""Check the walker routes this port ships against the client's own route geometry.

Once `extract_client_waypoints.py` could resolve a client world to one of our map ids, a question that
had never been askable became easy: **are the routes we already ship the routes the client has?**

WHAT IT DOES
------------
For every `walker_template` in `npc_walker/<mapid>_*.xml`, it takes the template's first point and looks
for the nearest point of any client route in the same map. Under three metres is a match.

**Nearest point, not nearest first point.** A patrol is a loop, and where our copy starts going round it
has nothing to do with where the client's copy starts. Comparing first-point to first-point called 91
routes suspect; comparing against every point of each candidate cut that to 68, and the difference was
entirely routes that are the same loop entered at a different place. The first version of this tool would
have sent somebody to re-derive two dozen correct routes.

WHAT IT FOUND WHEN WRITTEN
--------------------------
418 of our templates sit in maps the client covers. **350 of them — 83% — begin on a point of a client
route.** That is strong evidence our walker data is client-derived and broadly faithful, which had been
assumed and never checked.

The 68 that do not are concentrated: 31 in Steel Rake (300100000) and 27 in GAb1_03 (400050000), with
the rest scattered one or two to a map. That concentration is itself informative -- a systematic
difference in two instances rather than scattered errors, which is more likely to be a client-version
difference or a hand-authored set than sixty-eight independent mistakes.

WHAT A MISS IS NOT
------------------
It is not automatically a defect. Our route may predate the client dump, belong to a version this data
does not cover, or be deliberately hand-made. The tool reports distance so the size of the disagreement
is visible: a route eight metres out is a different question from one two hundred metres out.

**And a miss says nothing at all where the client's own set is thin.** GAb1_03 looked like the worst row
in this audit -- 27 routes, 1,000 to 1,118 metres from anything. It is not a defect and the map id is not
wrong: the client defines **4 routes** in that world against our 27, our routes cluster in a 284-unit box
where the client's spread across the whole 2,000-unit map, and our spawns for the map cover the same
extent the client's routes do. Ours are a hand-authored set for a fortress interior the client's file
simply does not describe. So the count of client routes per map is printed next to the misses, and maps
with fewer than ten are marked -- a large miss count against a four-route reference is an artefact of the
reference, not a finding.

Usage:  python audit_walker_fidelity.py [--worlds DIR] [--list]
"""
import argparse
import collections
import math
import pathlib
import statistics
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from extract_client_waypoints import client_routes, map_ids_by_world  # noqa: E402

WALKERS = pathlib.Path(__file__).resolve().parents[2] / "game-server" / "data" / "static_data" / "npc_walker"
MATCH_METRES = 3.0
HUG_METRES = 4.0

TEMPLATE = re.compile(r'<walker_template route_id="([^"]+)"(.*?)</walker_template>', re.S)
STEP = re.compile(r'<routestep x="([-0-9.]+)" y="([-0-9.]+)"')


def client_points_by_map(worlds_dir):
    ids = map_ids_by_world()
    out = collections.defaultdict(list)
    for name, copies in client_routes(worlds_dir).items():
        for world, points in copies.items():
            map_id = ids.get(world.lower())
            if map_id:
                out[map_id].append((name, [(float(p[0]), float(p[1])) for p in points]))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    ap.add_argument("--list", action="store_true", help="print every route that does not match")
    args = ap.parse_args()

    by_map = client_points_by_map(args.worlds)
    if not by_map:
        print("no client routes resolved -- check the worlds directory and world_maps.xml cName join")
        return 1

    total = matched = same_path = 0
    misses = []
    for f in sorted(WALKERS.glob("*.xml")):
        named = re.match(r"(\d+)_", f.name)
        if not named:
            continue
        map_id = int(named.group(1))
        candidates = by_map.get(map_id, [])
        if not candidates:
            continue
        for template in TEMPLATE.finditer(f.read_text(encoding="utf-8", errors="replace")):
            steps = STEP.findall(template.group(2))
            if not steps:
                continue
            total += 1
            start = (float(steps[0][0]), float(steps[0][1]))
            best, nearest = 9e9, None
            for name, points in candidates:
                d = min(math.dist(start, p) for p in points)
                if d < best:
                    best, nearest = d, name
            if best < MATCH_METRES:
                matched += 1
                continue
            # A first-point miss does not say the route is wrong -- it may be the same loop drawn with
            # different vertices. Ask the stronger question: averaged over EVERY one of our points, how
            # far does this route sit from the closest client route? Under HUG_METRES it is the same
            # path; well above it, it is a different path that happens to be in the same rooms.
            ours = [(float(a), float(b)) for a, b in steps]
            hug, hugged = 9e9, None
            for name, points in candidates:
                m = statistics.mean(min(math.dist(s, p) for p in points) for s in ours)
                if m < hug:
                    hug, hugged = m, name
            if hug < HUG_METRES:
                same_path += 1
            else:
                misses.append((map_id, template.group(1), round(best, 1), hugged or nearest,
                               len(steps), round(hug, 1)))

    print(f"{total} walker_templates sit in maps the client covers")
    print(f"{matched} begin on a point of a client route ({100 * matched // max(1, total)}%)")
    print(f"{same_path} start elsewhere but trace a client route the whole way"
          f" (mean under {HUG_METRES:.0f}m)")
    print(f"{len(misses)} are a different path\n")
    print("unmatched by map, with how much of that map the client actually covers:")
    for map_id, count in collections.Counter(m[0] for m in misses).most_common():
        routes_here = len(by_map.get(map_id, []))
        points_here = sum(len(pts) for _, pts in by_map.get(map_id, []))
        thin = "   <-- client set is thin; a miss says little" if routes_here < 10 else ""
        print(f"  map {map_id}: {count} unmatched   client has {routes_here} routes"
              f" / {points_here} points{thin}")
    if args.list:
        print()
        for map_id, route, distance, nearest, steps, hug in sorted(misses, key=lambda m: -m[5]):
            print(f"  map {map_id}  {route[:16]:18s} first {distance:7.1f}m  whole-route {hug:7.1f}m "
                  f"from {nearest}  ({steps} steps)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
