"""The spawn groups retail hides behind a condition, for npcs this port actually has.

WHY THIS EXISTS
---------------
`SpawnCondition` parses the gates and `SpawnVariables` holds the counters, but nothing yet knows *what*
a gate guards. Retail keeps that in the world files: a `<condition_info>` carries the expression and a
`<spawn_group_list>` of real npc placements, and `despawnAtOther="TRUE"` means the group is removed
again when the expression stops holding.

**78,865 npc placements sit behind a gate across 164 worlds.** Most are not portable:

| | placements |
|---|---|
| behind a gate in retail | 78,865 |
| whose name resolves to an npc id | 64,877 |
| **and this port has a template for** | **26,615** |
| and this port already spawns somewhere | 10,337 |

So roughly sixteen thousand placements are content we have and never place, and the rest name npcs this
port has no template for -- later-version and event content, which is why `ab1` alone accounts for 4,236
placements of which 1,258 npcs do not exist here.

This extracts the portable ones. It does **not** decide when they spawn; that is the activation half,
which needs a world's variable store and is not built.

CLI:
    python extract_gated_spawns.py <worlds_dir> <patterns_dir> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import html
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
from client_npc_names import npc_names  # noqa: E402
from extract_client_waypoints import map_ids_by_world  # noqa: E402

INFO_RE = re.compile(r"<condition_info\b([^>]*)>(.*?)</condition_info>", re.S)
NPC_RE = re.compile(r"<npc\b[^>]*>(.*?)</npc>", re.S)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("worlds_dir", type=pathlib.Path)
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    known = {int(m.group(1)) for m in re.finditer(r'<npc_template npc_id="(\d+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}

    # A retail world folder is not a map id. `world_maps.xml` carries the join as `cName`, and a world
    # it does not name cannot be loaded at runtime however good its data is -- those are counted below
    # rather than emitted with a name nothing can resolve.
    maps = map_ids_by_world()

    rows: list[tuple] = []
    skipped_unknown = 0
    skipped_unnamed = 0
    skipped_unmapped = 0
    unmapped_worlds: set[str] = set()
    for world in sorted(args.worlds_dir.glob("*/world.xml")):
        try:
            text = S.read_text(world)
        except Exception:
            continue
        map_id = maps.get(world.parent.name.lower())
        if map_id is None:
            unmapped_worlds.add(world.parent.name)

        for info in INFO_RE.finditer(text):
            gate = re.search(r"<extcondition>(.*?)</extcondition>", info.group(2), re.S)
            if not gate:
                continue
            expression = " ".join(html.unescape(gate.group(1)).split())
            if not expression:
                continue
            # `despawnAtOther` decides whether the group is only added, or added and taken away again.
            despawns = "TRUE" if re.search(r'despawnAtOther="true"', info.group(1), re.I) else "FALSE"

            for npc in NPC_RE.finditer(info.group(2)):
                block = npc.group(1)
                named = re.search(r"<name>([^<]+)</name>", block)
                if not named:
                    skipped_unnamed += 1
                    continue
                npc_id = dev.get(named.group(1))
                if npc_id is None or npc_id not in known:
                    skipped_unknown += 1
                    continue
                spot = [re.search(r"<%s>([-\d.]+)</%s>" % (axis, axis), block) for axis in "xyz"]
                if not all(spot):
                    continue
                heading = re.search(r"<dir>([-\d.]+)</dir>", block)
                respawn = re.search(r"<spawn_time>(\d+)</spawn_time>", block)
                if map_id is None:
                    skipped_unmapped += 1
                    continue
                rows.append((map_id, world.parent.name, npc_id,
                             float(spot[0].group(1)), float(spot[1].group(1)), float(spot[2].group(1)),
                             int(float(heading.group(1))) if heading else 0,
                             int(respawn.group(1)) if respawn else 0,
                             despawns, expression))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("map\tworld\tnpc\tx\ty\tz\tdir\trespawn\tdespawn_at_other\tgate\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    per = collections.Counter(r[1] for r in rows)
    print(f"{len(rows)} portable gated placements across {len(per)} worlds -> {args.out}")
    print(f"    {skipped_unknown} name an npc this port has no template for, and are dropped")
    print(f"    {skipped_unmapped} are in {len(unmapped_worlds)} worlds world_maps.xml does not name")
    if skipped_unnamed:
        print(f"    {skipped_unnamed} carry no name at all")
    for world, count in per.most_common(6):
        print(f"    {count:6d}  {world}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
