#!/usr/bin/env python3
"""Our npc health against retail's, and the placeholder values hiding in it.

WHY THIS EXISTS
---------------
A fortress killer loaded with **140 max HP**. Retail gives it **327,127**. That was found by accident,
while working out why a pin could not hold two npcs in a fight -- the killer was being one-shot by the
garrison chief it was supposed to be hunting.

> `<stats maxHp="140">` is in our data explicitly. It is not a loader default and not a missing field:
> it is a value, and it is wrong by a factor of two thousand.

Retail's `npcs.xml` carries `<max_hp>` for every npc in the game. Comparing the two:

| | |
|---|---|
| comparable npcs | 62,592 |
| **identical** | **39,512** |
| ours less than half retail's | 10,703 |
| ours higher than retail's | 7,723 |
| within half | 4,652 |

So the bulk imported correctly and a large minority did not.

THE PLACEHOLDER SHAPE
---------------------
**4,273 of the understated sit at 200 HP or less**, and the values cluster the way a spread of
placeholders does -- roughly a hundred npcs each at 100, 105, 110, 114, 128, 130, 135, 145. The worst
of them are not small npcs:

    BLDF5_Fortress_GuardianHead   ours 128   retail 290,150,400
    BGAB1_Door_Li_4_lv65_BigHP    ours 113   retail 168,000,000
    BGAB1_LGuardianChief_65_Al    ours 108   retail 156,553,560

A fortress boss with 128 HP is not a balance choice. A siege against one is over before it starts, and
every npc-versus-npc mechanic in the abyss is being fought by npcs that cannot survive a hit.

WHAT `--apply` CHANGES, AND WHAT IT WILL NOT TOUCH
--------------------------------------------------
Only the unambiguous case: **ours is 200 or less and retail is at least a thousand.** That is a
placeholder against a real value, and copying retail's number is the whole fix.

It deliberately leaves alone:

* the 7,723 where **ours is higher** -- that direction is not a placeholder and may be a deliberate
  choice made here; it needs reading, not a sweep;
* everything between half and one -- a genuine scaling decision looks like that, and this tool cannot
  tell one from an import that rounded;
* npcs with no retail row at all.

Usage:  python audit_npc_health.py [--apply] [--limit N]
"""
import argparse
import collections
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from client_npc_names import npc_names  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"

NAME_RE = re.compile(r"<name>([^<]+)</name>")
HP_RE = re.compile(r"<max_hp>(\d+)</max_hp>")
OURS_RE = re.compile(r'<npc_template npc_id="(\d+)"[^>]*>\s*<stats maxHp="(\d+)"')

#: Below this, our value is a token rather than a health pool.
PLACEHOLDER = 200

#: Above this, retail's value is certainly a real one.
REAL = 1000


def retail_health(path):
    """npc dev name -> max_hp, streamed: the file is over five hundred megabytes."""
    out = {}
    buffered = ""
    with io.open(path, "r", encoding="utf-16", errors="replace") as handle:
        while True:
            block = handle.read(1 << 22)
            if not block:
                break
            buffered += block
            records = buffered.split("</data>")
            buffered = records.pop()
            for record in records:
                named = NAME_RE.search(record)
                health = HP_RE.search(record)
                if named and health:
                    out[named.group(1)] = int(health.group(1))
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
