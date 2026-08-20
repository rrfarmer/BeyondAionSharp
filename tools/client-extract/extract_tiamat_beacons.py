"""What a Tiamat breath beacon puts on the ground two seconds after it lands.

WHY THIS EXISTS
---------------
Tiamat's breath is telegraphed in two steps and this port only had the first. The rotation places a
**beacon** -- the marker a raid sees and runs out of -- and the beacon's own pattern then arms a 2000ms
idle timer and spawns the **damage** npcs along the line it marked.

Fifteen beacons exist here and **twelve are on plain `aggressive`**, which does nothing at all. The
other three have a class that casts a skill and never spawns. So every breath in the encounter landed
with the warning and none of the damage.

The shape is uniform across normal and hard mode:

| beacon | spawns | count | live |
|---|---|---|---|
| `*_M4s`, `*_M8s` (middle) | its `_dmg` twin | 11, in a line | 2s |
| `*_L4s`, `*_L8s`, `*_R4s`, `*_R8s` | its `_dmg` twin | 1 | 3s |

Every one uses `SPAWN_LOCATION_ABSOLUTE` and `despawn_at_attack_state=TRUE`, so the coordinates are
carried verbatim rather than derived from the beacon's own position.

CLI:
    python extract_tiamat_beacons.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402

IDLE_RE = re.compile(r"<set_idle_timer>.*?<delay>(\d+)</delay>", re.S)
SPAWN_RE = re.compile(r"<spawn>(.*?)</spawn>", re.S)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    live = {int(m.group(1)) for m in re.finditer(r'<npc_template npc_id="(\d+)"', templates)}
    names = {m.group(2): int(m.group(1))
             for m in re.finditer(r'<npc_template npc_id="(\d+)"[^>]*\bname="([^"]*)"', templates)}

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    # dev name -> npc id, so a pattern's `npc_nameid` can be resolved to something we can spawn.
    from client_npc_names import npc_names
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}

    rows: list[tuple] = []
    skipped_unknown = 0
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named or "Beacon" not in named.group(1):
                continue
            idle = IDLE_RE.search(body)
            if not idle:
                continue
            owners = [n for n in binders.get(named.group(1), []) if n in live]
            if not owners:
                continue
            for spawn in SPAWN_RE.finditer(body):
                block = spawn.group(1)
                target = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", block)
                x = re.search(r"<x>([-\d.]+)</x>", block)
                y = re.search(r"<y>([-\d.]+)</y>", block)
                z = re.search(r"<z>([-\d.]+)</z>", block)
                where = re.search(r"<spawn_location_type>(\w+)</", block)
                # MY_POINT means "at the beacon", and those blocks carry x=y=z=0. Treating them as
                # absolute would put eight of the fifteen breaths at the world origin -- which is what
                # the first emitted table did, visibly, in its own coordinates.
                at_self = 1 if where and where.group(1).endswith("MY_POINT") else 0
                if not target or (not at_self and not (x and y and z)):
                    continue
                spawned = dev.get(target.group(1))
                if spawned is None or spawned not in live:
                    # The damage npc has no template here; a spawn action naming it would throw.
                    skipped_unknown += 1
                    continue
                seconds = re.search(r"<live_time>(\d+)</live_time>", block)
                for owner in owners:
                    rows.append((owner, int(idle.group(1)), spawned,
                                 int(seconds.group(1)) if seconds else 0, at_self,
                                 float(x.group(1)) if x else 0.0,
                                 float(y.group(1)) if y else 0.0,
                                 float(z.group(1)) if z else 0.0,
                                 named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("beacon\tdelay_ms\tdamage_npc\tlive\tat_self\tx\ty\tz\tpattern\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    per = collections.Counter(r[0] for r in rows)
    print(f"{len(rows)} placements across {len(per)} beacons -> {args.out}")
    for beacon, count in sorted(per.items()):
        print(f"    {beacon}  {count:2d} placements")
    if skipped_unknown:
        print(f"    {skipped_unknown} placements name a damage npc with no template here, and are dropped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
