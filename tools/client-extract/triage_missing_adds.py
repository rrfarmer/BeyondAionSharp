"""Bucket the missing encounter adds by how retail spawns them.

`audit_missing_adds.py` says *which* adds never reach the world. This says *how*
they would get there, which is what decides the cost of fixing each one:

- an HP-threshold spawn at the spawner's own position is expressible in
  `ai/spawn_helpers.xml`, so it is a data change and needs no code at all;
- a battle-timer spawn needs a timer-driven AI class;
- an on-death or on-despawn spawn belongs to the encounter's instance handler.

CLI:
    python triage_missing_adds.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

import bxml
from aionpak import read_pak
from audit_missing_adds import (
    NAME_RE, PATTERN_RE, TEMPLATE_RE, attr, is_real_combatant, read_text, spawnable_npc_ids,
)

EVENT_RE = re.compile(r"<(on_[a-z_]+)>(.*?)</\1>", re.S)
STEP_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)
SPAWN_RE = re.compile(
    r"<(spawn|spawn_on_target|spawn_on_multi_target|spawn_on_target_by_attacker_indicator)>"
    r"(.*?)</\1>", re.S)
NAMEID_RE = re.compile(r"<npc_nameid>([^<]*)</npc_nameid>")
LOCATION_RE = re.compile(r"<spawn_location_type>([^<]*)</spawn_location_type>")
HP_LOWER_RE = re.compile(r"<is_hp_lower_than>.*?<percent>(\d+)</percent>", re.S)

BLOCKED_LOCATION = "SPAWN_LOCATION_WAY_POINT_START"

# Positions expressible by ai/spawn_helpers.xml: at the spawner, or scattered around it.
SELF_RELATIVE = {"SPAWN_LOCATION_MY_POINT", "SPAWN_LOCATION_RELATIVE", ""}


def load_binding(path: pathlib.Path) -> dict[str, list[str]]:
    out = collections.defaultdict(list)
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        out[pattern].append(npc_id)
    return out


def devname_to_id(client_root: pathlib.Path) -> dict[str, str]:
    out = {}
    for name, data in read_pak(client_root / "Data" / "Npcs" / "Npcs.pak"):
        if name not in ("client_npcs_monster.xml", "client_npcs_npc.xml"):
            continue
        for npc in bxml.decode(data):
            f = {c.tag: (c.text or "") for c in npc}
            if f.get("id") and f.get("name"):
                out[f["name"].lower()] = f["id"]
    return out


def classify(event: str, step_body: str, location: str) -> str:
    """How this spawn is triggered, in the terms that decide what fixing it costs."""
    if location == BLOCKED_LOCATION:
        return "blocked: waypoint-placed"
    if event == "on_battle_timer":
        return "timer: needs a timer-driven AI class"
    if event in ("on_die", "on_killed_by_user", "on_despawn"):
        return "death/despawn: instance handler"
    if HP_LOWER_RE.search(step_body) and location in SELF_RELATIVE:
        return "hp threshold at spawner: spawn_helpers (data only)"
    if HP_LOWER_RE.search(step_body):
        return "hp threshold, fixed position: AI class or instance handler"
    if event in ("on_attacked", "on_spelled"):
        return "on hit/spell: AI class"
    return f"other ({event})"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    by_pattern = load_binding(pathlib.Path(args.binding_tsv))
    dev2id = devname_to_id(pathlib.Path(args.client_root))
    spawnable = spawnable_npc_ids(repo)
    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(
        read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml"))}

    buckets: dict[str, list[tuple[str, str, str, str]]] = collections.defaultdict(list)
    seen: set[tuple[str, str]] = set()

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            pattern = m.group(1)
            owners = [n for n in by_pattern.get(pattern, []) if n in spawnable]
            if not owners:
                continue

            for ev in EVENT_RE.finditer(body):
                event = ev.group(1)
                for step in STEP_RE.finditer(ev.group(2)):
                    step_body = step.group(1)
                    for action in SPAWN_RE.finditer(step_body):
                        loc = LOCATION_RE.search(action.group(2))
                        location = loc.group(1).strip() if loc else ""
                        for dev in NAMEID_RE.findall(action.group(2)):
                            dev = dev.strip()
                            add_id = dev2id.get(dev.lower())
                            if not add_id or add_id in spawnable:
                                continue
                            attrs = templates.get(add_id)
                            if attrs is None or not is_real_combatant(attrs):
                                continue
                            key = (pattern, add_id)
                            if key in seen:
                                continue
                            seen.add(key)
                            buckets[classify(event, step_body, location)].append(
                                (pattern, owners[0], add_id, attr(attrs, "name")))

    total = sum(len(v) for v in buckets.values())
    print(f"missing adds classified: {total}\n")
    for name, rows in sorted(buckets.items(), key=lambda kv: -len(kv[1])):
        print(f"{len(rows):>4}  {name}")
    print()

    key = "hp threshold at spawner: spawn_helpers (data only)"
    print(f"== the data-only bucket ({len(buckets.get(key, []))}) ==")
    for pattern, owner, add_id, name in sorted(buckets.get(key, []))[:40]:
        print(f"  {owner} <- {add_id} {name:<34} ({pattern})")


if __name__ == "__main__":
    main()
