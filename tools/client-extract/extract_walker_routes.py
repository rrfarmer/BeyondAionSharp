"""Retail's named walk paths, from the 5.8 server's own map data into `npc_walker`.

The AI patterns name their paths — `pathname=path_tiamatdrakan_1_1` — and this port's walker data holds
**points under forty-character hashes with no name attached**. That missing join blocked five encounters
for six passes: Tiamat's rush wave, Bergrisar's blood wheels, the silikor akaimum's patrol arrival,
Padmarashka's four adds and Kaliga's statues.

**The join is in the retail 5.8 server tree**, at `Server/Map/Worlds/<world>/world.xml`, which carries
`<way_point>` elements holding a name and its points together. 11,050 of them, covering 344 of the 467
names the patterns use.

**Those files are UTF-16.** Every earlier search for these names used byte-level grep and came back empty,
which is why two entries in the fidelity log recorded the mapping as absent and the search as exhausted.
`summarize_pattern.read_text` handles the encoding; this uses it.

**Keyed by retail's own path name**, not by a hash, so a pattern class can call
`GetSpawn().SetWalkerId("path_tiamatdrakan_1_1")` and the route resolves. That is what
`WalkManager.StartRouteWalking` looks up, and it is why the port could already walk adds it had no routes
for.

Usage:
    python extract_walker_routes.py <worlds_dir> <out.xml> [--report]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

WAY_POINT = re.compile(r"<way_point>\s*<name>([^<]+)</name>(.*?)</way_point>", re.S)
POINT = re.compile(r"<x>([-\d.]+)</x>\s*<y>([-\d.]+)</y>\s*<z>([-\d.]+)</z>", re.S)


def routes(worlds: pathlib.Path) -> dict[str, list[tuple[str, str, str]]]:
    """Path name -> its points, from every world file under `worlds`."""
    out: dict[str, list[tuple[str, str, str]]] = {}
    clashes: dict[str, int] = collections.Counter()
    for path in sorted(worlds.rglob("world*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for hit in WAY_POINT.finditer(text):
            name = hit.group(1).strip()
            points = [(x, y, z) for x, y, z in POINT.findall(hit.group(2))]
            if not points:
                continue
            if name in out and out[name] != points:
                # Two worlds naming the same path differently. Keep the first and count it, rather than
                # letting file order decide silently.
                clashes[name] += 1
                continue
            out[name] = points
    if clashes:
        print(f"  {len(clashes)} names defined more than once with different points; first kept",
              file=sys.stderr)
    return out


def wanted(patterns: pathlib.Path) -> set[str]:
    names: set[str] = set()
    for path in patterns.rglob("*.xml"):
        try:
            names |= set(re.findall(r"<pathname>([^<]+)</pathname>", S.read_text(path)))
        except Exception:
            continue
    return names


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("worlds_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--patterns", type=pathlib.Path,
                    default=pathlib.Path("D:/SSDSync/Downloads/5.8 AI Patterns"))
    args = ap.parse_args()

    all_routes = routes(args.worlds_dir)
    need = wanted(args.patterns)
    lower = {n.lower(): n for n in all_routes}

    kept: dict[str, list[tuple[str, str, str]]] = {}
    for name in sorted(need):
        actual = lower.get(name.lower())
        if actual:
            # Written under the name the pattern uses, so lookup needs no case folding at runtime.
            kept[name] = all_routes[actual]

    lines = ['<?xml version="1.0" encoding="UTF-8"?>',
             '<npc_walker xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"'
             ' xsi:noNamespaceSchemaLocation="npc_walker.xsd">']
    for name, points in kept.items():
        lines.append(f'\t<walker_template route_id="{name}">')
        for x, y, z in points:
            lines.append(f'\t\t<routestep x="{x}" y="{y}" z="{z}" />')
        lines.append("\t</walker_template>")
    lines.append("</npc_walker>")
    args.out.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"named waypoints in world data : {len(all_routes)}")
    print(f"path names used by patterns   : {len(need)}")
    print(f"written                       : {len(kept)}")
    print(f"still missing                 : {len(need) - len(kept)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
