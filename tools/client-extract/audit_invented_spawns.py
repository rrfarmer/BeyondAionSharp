"""Find npcs our AI classes summon that no retail pattern summons anywhere.

Deleting the invented golem from `UnstableYamennesAI` turned up a sharper filter than "unpinned". That
add was found because no pin named it — but what made it *wrong* was that **no pattern in the 5.8 dump
spawns that npc at all**. It was a mechanic somebody invented, running alongside the real one.

This looks for the rest. For every npc an AI class places, it asks whether any of the ~2,900 retail
patterns places it too. An npc ours summons and retail's never does is one of:

- **invented** — a mechanic written before the pattern data was available, which is what this hunts;
- **summoned elsewhere in retail** — by an instance script, a quest or a spawn file, which the pattern
  dump does not cover and this tool cannot see;
- **a devname our binding table does not resolve**, since 12,000 templates are still unbound.

So a row here is a question, not a verdict. **Read the class and the pattern before deleting anything.**
The three categories are told apart by hand, and the second and third are common.

Usage:
    python audit_invented_spawns.py [--patterns-dir DIR]
"""
from __future__ import annotations

import argparse
import io
import pathlib
import re

import audit_unpinned_spawns as U

REPO = pathlib.Path(__file__).resolve().parents[2]
BINDING = pathlib.Path(__file__).parent / "out" / "ai_binding.tsv"
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
DEFAULT_PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

NAMEID = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")


def devname_to_id() -> dict[str, str]:
    out: dict[str, str] = {}
    with open(BINDING, encoding="utf-8") as fh:
        next(fh)
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) > 1 and cols[1]:
                out.setdefault(cols[1], cols[0])
    return out


def id_to_devname() -> dict[str, str]:
    out: dict[str, str] = {}
    with open(BINDING, encoding="utf-8") as fh:
        next(fh)
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) > 1 and cols[1]:
                out[cols[0]] = cols[1]
    return out


def retail_spawned(directory: pathlib.Path) -> tuple[set[str], set[str], int]:
    """
    Every npc id retail places, every devname it places, and how many did not resolve.

    **Resolved in both directions, and the second one is what makes this usable.** Going
    devname -> id leaves 1,132 spawn devnames unresolved, because the binding table is missing
    12,000 templates -- and every one of those would read as "retail never spawns this". Going
    id -> devname instead answers the question actually being asked: *this* npc, whose devname we do
    know, is it named by any pattern? That is how the invented golem was confirmed by hand, and it is
    exact for any npc the binding covers.
    """
    ids = devname_to_id()
    placed_ids: set[str] = set()
    placed_names: set[str] = set()
    unresolved = 0
    for path in sorted(directory.glob("NpcAIPatterns*.xml")):
        text = io.open(path, encoding="utf-16", errors="replace").read()
        for devname in NAMEID.findall(text):
            devname = devname.strip()
            placed_names.add(devname.lower())
            npc_id = ids.get(devname)
            if npc_id:
                placed_ids.add(npc_id)
            else:
                unresolved += 1
    return placed_ids, placed_names, unresolved


def names() -> dict[str, str]:
    text = TEMPLATES.read_text(encoding="utf-8")
    return dict(re.findall(r'npc_id="(\d+)"[^>]*?\bname="([^"]*)"', text))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns-dir", type=pathlib.Path, default=DEFAULT_PATTERNS)
    args = ap.parse_args()

    retail, retail_names, unresolved = retail_spawned(args.patterns_dir)
    devnames = id_to_devname()
    label = names()
    unknown_devname = 0

    rows = []
    for path in sorted(U.AI_DIR.glob("*.cs")):
        for class_name, unit in U.units(path.read_text(encoding="utf-8")):
            consts = {n: v for n, v in U.CONST.findall(unit)}
            for m in U.SPAWN.finditer(unit):
                raw = m.group("npc")
                value = consts.get(raw, raw if raw.isdigit() else None)
                if not value or int(value) < U.MIN_NPC_ID or value in retail:
                    continue

                devname = devnames.get(value)
                if devname is None:
                    # No devname for this npc, so the question cannot be asked either way.
                    unknown_devname += 1
                    continue
                if devname.lower() in retail_names:
                    continue
                rows.append((path.name, class_name, value, devname))

    seen = set()
    for filename, class_name, npc_id, devname in rows:
        if (class_name, npc_id) in seen:
            continue
        seen.add((class_name, npc_id))
        print(f"{filename:<36} {class_name:<32} {npc_id}  {label.get(npc_id, '?'):<28} {devname}")

    print(f"\n{len(seen)} (class, npc) pairs our AI summons that no retail pattern summons")
    print(f"{len(retail)} npc ids and {len(retail_names)} devnames are placed by retail patterns; "
          f"{unresolved} spawn devnames did not resolve through the binding table, and "
          f"{unknown_devname} of ours have no devname to check")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
