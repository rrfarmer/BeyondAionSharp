"""Our npc tribe against retail's, for npcs whose tribe decides who they can fight.

WHY THIS EXISTS
---------------
`talle` (802383) answers a guard's call for help in retail, and could not here even once it was wired
to listen: our template gives it `tribe="GENERAL"`, so `IsEnemy` is false against every player and the
hate is refused before it lands. Retail gives it `ProtectGuard_Light`.

**Tribe is the third place a mechanic can be missing**, after the `ai` binding and the spawn point. An
npc can have the right class, the right pattern and a spawn, and still do nothing because it is at war
with nobody.

This reports only npcs where OUR tribe is one of the neutral catch-alls and retail's is not, which is
the direction that silently disables behaviour. The reverse -- ours specific, retail's general -- is
left alone: it does not disable anything and would need a different argument.

CLI:
    python audit_npc_tribe.py [--npcs <npcs.xml>] [--limit N] [--apply]
"""
from __future__ import annotations

import argparse
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from client_npc_names import npc_names  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
TRIBES = REPO / "game-server" / "data" / "static_data" / "tribe" / "tribe_relations.xml"

NAME_RE = re.compile(r"<name>([^<]+)</name>")
TRIBE_RE = re.compile(r"<tribe>([^<]+)</tribe>")
OURS_RE = re.compile(r'<npc_template npc_id="(\d+)"[^>]*?\btribe="([A-Z0-9_]+)"')

#: The catch-alls. An npc on one of these is at war with nobody in particular.
NEUTRAL = {"GENERAL", "NONE"}


def retail_tribes(path: pathlib.Path) -> dict[str, str]:
    """npc dev name -> tribe, streamed: the file is over five hundred megabytes."""
    out: dict[str, str] = {}
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
                tribe = TRIBE_RE.search(record)
                if named and tribe:
                    out[named.group(1)] = tribe.group(1)
    return out


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--npcs", default="D:/Aion58ServerTesting/Server/Map/XML/npcs.xml")
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--limit", type=int, default=25)
    ap.add_argument("--only", type=int, action="append",
                    help="restrict to these npc ids; repeatable")
    args = ap.parse_args()

    known = {name.upper() for name in re.findall(r'name="([^"]+)"', TRIBES.read_text(encoding="utf-8",
                                                                                    errors="replace"))}
    ours = {int(m.group(1)): m.group(2)
            for m in OURS_RE.finditer(TEMPLATES.read_text(encoding="utf-8", errors="replace"))}
    ids = {dev: int(npc_id) for dev, npc_id in npc_names(pathlib.Path(args.xml)).items()}
    retail = retail_tribes(pathlib.Path(args.npcs))

    rows = []
    unknown = 0
    for dev, tribe in retail.items():
        npc_id = ids.get(dev)
        if npc_id is None or npc_id not in ours:
            continue
        if args.only and npc_id not in args.only:
            continue
        mine = ours[npc_id]
        if mine not in NEUTRAL or tribe.upper() in NEUTRAL:
            continue
        if tribe.upper() not in known:
            # Retail names a tribe our relations data has never heard of. Reporting it as a repair
            # would produce a template that fails to load, so it is counted and shown separately.
            unknown += 1
            continue
        rows.append((npc_id, mine, tribe.upper(), dev))

    rows.sort()
    print(f"{len(rows)} npcs are neutral here and have a real tribe in retail")
    print(f"{unknown} more name a tribe our relations data does not define, and are not repairable\n")
    for npc_id, mine, tribe, dev in rows[:args.limit]:
        print(f"   {npc_id}  ours={mine:<8} retail={tribe:<32} {dev[:40]}")
    if len(rows) > args.limit:
        print(f"   ... and {len(rows) - args.limit} more")
    return 0


if __name__ == "__main__":
    sys.exit(main())
