#!/usr/bin/env python3
"""Compare explicit 4.8 NPC HP with explicit HP in a supplied retail XML.

Read-only by default. Match the direct name and max_hp children of the same npc;
missing HP remains absent. Ordered attack/skill data is not a record boundary.
The former </data> splitter could borrow a following NPC's HP, so its historical
counts and missing-field comparisons must not be reused as evidence.

This legacy audit selects values by developer name. The reviewed npc-audit
pipeline supplies region/version identities and native C# profile calculations
for the supported combat-stat adapter. This script does not prove constructor,
owner/scaling, active encounter or effective combat behavior.

The historical --apply mode replaces only host HP<=200 with explicit retail
HP>=1000. Current stat work uses reviewed coherent packages; do not invoke this
repair mode for that rollout.

Usage: python audit_npc_health.py [--npcs PATH] [--limit N]
"""
import argparse
import collections
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from client_npc_names import npc_names  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"

OURS_RE = re.compile(r'<npc_template npc_id="(\d+)"[^>]*>\s*<stats maxHp="(\d+)"')

#: Below this, our value is a token rather than a health pool.
PLACEHOLDER = 200

#: Above this, retail's value is certainly a real one.
REAL = 1000


def retail_health(path):
    """Explicit root HP by NPC name; never borrow from another/nested record.

    iterparse reads the XML encoding declaration/BOM and bounds retained payload
    to each NPC, including records whose ordered lists contain nested `data`.
    Missing HP stays absent; constructor or script defaults belong to a separate
    evidence-backed construction audit, not this explicit-value comparison.
    """
    out = {}
    for _, record in ET.iterparse(path, events=("end",)):
        if record.tag != "npc":
            continue
        names, values = record.findall("name"), record.findall("max_hp")
        if len(names) > 1 or len(values) > 1:
            raise ValueError("Ambiguous NPC name/max_hp fields")
        if names and values:
            name = names[0].text
            value = values[0].text
            if not name or value is None or not re.fullmatch(r"[0-9]+", value.strip()):
                raise ValueError("Invalid NPC name/max_hp field")
            health = int(value)
            if name in out and out[name] != health:
                raise ValueError(f"Conflicting explicit HP for NPC {name}")
            out[name] = health
        record.clear()
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--npcs", default="D:/Aion58ServerTesting/Server/Map/XML/npcs.xml")
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--apply", action="store_true",
                    help="rewrite the placeholder values to retail's, and nothing else")
    ap.add_argument("--limit", type=int, default=15)
    args = ap.parse_args()

    ids = npc_names(args.xml)
    text = TEMPLATES.read_text(encoding="utf-8", errors="replace")
    ours = {m.group(1): int(m.group(2)) for m in OURS_RE.finditer(text)}
    retail = retail_health(args.npcs)

    tally = collections.Counter()
    fixable = []
    for dev, health in retail.items():
        npc_id = ids.get(dev)
        if npc_id is None or npc_id not in ours:
            continue
        mine = ours[npc_id]
        if mine == health:
            tally["identical"] += 1
            continue
        if health == 0:
            tally["retail says zero"] += 1
        elif mine > health:
            tally["ours higher"] += 1
        elif mine / health >= 0.5:
            tally["within half"] += 1
        else:
            tally["under half"] += 1
            if mine <= PLACEHOLDER and health >= REAL:
                fixable.append((mine / health, npc_id, mine, health, dev))

    print(f"{sum(tally.values())} npcs carry a max_hp in both places")
    for kind, count in tally.most_common():
        print(f"   {count:7d}  {kind}")
    print(f"\n{len(fixable)} are a placeholder here ({PLACEHOLDER} or less) against a real retail value")

    fixable.sort()
    for ratio, npc_id, mine, health, dev in fixable[:args.limit]:
        print(f"   {npc_id}  ours={mine:>5}  retail={health:>10}  {dev[:44]}")
    if len(fixable) > args.limit:
        print(f"   ... and {len(fixable) - args.limit} more")

    if not args.apply:
        print("\n(--apply rewrites exactly these, and nothing else)")
        return 0

    wanted = {npc_id: health for _, npc_id, _, health, _ in fixable}
    def swap(match):
        npc_id = match.group(1)
        if npc_id in wanted:
            return match.group(0).replace(f'maxHp="{match.group(2)}"', f'maxHp="{wanted[npc_id]}"')
        return match.group(0)

    TEMPLATES.write_text(OURS_RE.sub(swap, text), encoding="utf-8")
    print(f"\nrewrote {len(wanted)} templates")
    return 0


if __name__ == "__main__":
    sys.exit(main())
