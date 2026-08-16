"""Extract the abyss guards' reinforcement tables from the retail patterns.

The `[DL]Guard_*` families are the largest single cluster in the missing-adds
backlog: 212 pattern variants, all the same mechanic — a guard in combat calls up
attackers and a healer on a battle timer, more of them the worse it is doing — with
a different pair of summon npcs per level bracket and per faction.

Hand-writing 79 AI classes for one mechanic would be absurd, and hand-copying 212
sets of npc ids into a table would be worse. This reads the mechanic out of the
patterns and emits it as data, so the AI class carries the structure and the table
carries the facts.

What it emits, one row per (guard npc, band):

    guard_npc_id  pattern  low_hp  high_hp  chance  placement  summons

where `summons` is `npc_id*count` joined by commas, and the band is the retail
`is_hp_in_boundary` / `is_hp_lower_than` guard verbatim.

Rows are only emitted where every devname resolves to an npc id our client knows.
Anything that does not resolve is reported on stderr rather than dropped silently —
a guard whose healer is missing would otherwise look like a guard that never heals.

CLI:
    python extract_guard_reinforcements.py <patterns_dir> <binding_tsv> [--out FILE]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text

GUARD_RE = re.compile(r"^[DL]Guard_")

# The dump is element-based; summarize_pattern.py renders attributes for reading.
# Two ops, not one. `spawn` places at the guard's own point; `spawn_on_target` puts the
# reinforcements on whoever it is fighting. Looking only for the first dropped four variants
# silently -- and a guard whose wave lands on the raid rather than on itself is a different fight.
SPAWN_RE = re.compile(
    r"<(?P<op>spawn|spawn_on_target)>(?P<body>.*?)</(?P=op)>", re.S)
NAMEID_RE = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")
COUNT_RE = re.compile(r"<num_to_spawn>(\d+)</num_to_spawn>")
LIVE_RE = re.compile(r"<live_time>(\d+)</live_time>")
RANGE_RE = re.compile(r"<spawn_range>(\d+)</spawn_range>")
LOCATION_RE = re.compile(r"<spawn_location_type>([^<]+)</spawn_location_type>")

BOUNDARY_RE = re.compile(
    r"<is_hp_in_boundary>.*?<larger_than>(\d+)</larger_than>.*?<less_than>(\d+)</less_than>.*?</is_hp_in_boundary>",
    re.S)
LOWER_RE = re.compile(
    r"<is_hp_lower_than>.*?<percent>(\d+)</percent>.*?</is_hp_lower_than>", re.S)
PROB_RE = re.compile(r"<test_probability>.*?<percent>(\d+)</percent>.*?</test_probability>", re.S)
TIMER_RE = re.compile(
    r"<is_battle_timer_indicator>.*?<btimer_indicator>BTIMERI_INDEX_(\d+)</btimer_indicator>.*?</is_battle_timer_indicator>",
    re.S)

BRANCH_RE = re.compile(r"<pattern>(?P<body>.*?)</pattern>", re.S)
HANDLER_RE = re.compile(r"<(?P<event>on_[a-z_]+)>(?P<body>.*?)</(?P=event)>", re.S)


def branches_of(block: str, event: str) -> list[str]:
    """Every branch body under one event handler of one pattern."""
    out: list[str] = []
    for handler in HANDLER_RE.finditer(block):
        if handler.group("event") != event:
            continue
        out.extend(m.group("body") for m in BRANCH_RE.finditer(handler.group("body")))
    return out


def spawns_in(branch: str) -> list[tuple[str, int, int, int, str]]:
    """(devname, count, live_time, spawn_range, placement) for each spawn in a branch."""
    found = []
    for spawn in SPAWN_RE.finditer(branch):
        body = spawn.group("body")
        on_target = spawn.group("op") == "spawn_on_target"
        name = NAMEID_RE.search(body)
        if not name:
            continue
        count = COUNT_RE.search(body)
        live = LIVE_RE.search(body)
        rng = RANGE_RE.search(body)
        loc = LOCATION_RE.search(body)
        found.append((
            name.group(1),
            int(count.group(1)) if count else 1,
            int(live.group(1)) if live else 0,
            int(rng.group(1)) if rng else 0,
            "TARGET" if on_target else (loc.group(1) if loc else "?"),
        ))
    return found


def band_of(branch: str) -> tuple[int, int] | None:
    boundary = BOUNDARY_RE.search(branch)
    if boundary:
        return int(boundary.group(1)), int(boundary.group(2))
    lower = LOWER_RE.search(branch)
    if lower:
        return 0, int(lower.group(1)) - 1
    # No health guard at all: the call is unconditional. Returning None here dropped every
    # DGuard_PsA row on the floor, which reads as "this guard never calls" rather than
    # "this guard always calls".
    return 0, 100


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--out")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    # devname -> npc id, and pattern -> the npcs that run it.
    by_devname: dict[str, str] = {}
    owners: dict[str, list[str]] = collections.defaultdict(list)
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) < 4 or parts[0] == "npc_id":
            continue
        npc_id, devname, _ai, pattern = parts[0], parts[1], parts[2], parts[3]
        by_devname.setdefault(devname.lower(), npc_id)
        owners[pattern].append(npc_id)

    rows: list[tuple] = []
    unresolved: collections.Counter = collections.Counter()
    shapes: collections.Counter = collections.Counter()
    patterns_seen = 0

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        text = read_text(path)
        for match in PATTERN_RE.finditer(text):
            block = match.group(0)
            name_match = NAME_RE.search(block)
            if not name_match:
                continue
            name = name_match.group(1)
            if not GUARD_RE.match(name):
                continue

            timers: list[tuple] = []
            for branch in branches_of(block, "on_battle_timer"):
                spawns = spawns_in(branch)
                if not spawns:
                    continue
                band = band_of(branch)
                if band is None:
                    continue
                prob = PROB_RE.search(branch)
                timer = TIMER_RE.search(branch)
                timers.append((
                    band[0], band[1],
                    int(prob.group(1)) if prob else 100,
                    int(timer.group(1)) if timer else -1,
                    spawns,
                ))
            if not timers:
                continue
            patterns_seen += 1

            # The shape census: which timer slot, which lifetime, which placement.
            for low, high, _chance, timer, spawns in timers:
                for _dev, _count, live, rng, loc in spawns:
                    shapes[(timer, live, rng, loc)] += 1

            guard_ids = owners.get(name, [])
            if not guard_ids:
                unresolved[f"pattern {name} has no npc"] += 1
                continue

            for low, high, chance, _timer, spawns in timers:
                resolved = []
                placement = "TARGET" if spawns and spawns[0][4] == "TARGET" else "SELF"
                for devname, count, _live, _rng, _loc in spawns:
                    npc_id = by_devname.get(devname.lower())
                    if npc_id is None:
                        unresolved[f"devname {devname}"] += 1
                        resolved = []
                        break
                    resolved.append(f"{npc_id}*{count}")
                if not resolved:
                    continue
                for guard_id in guard_ids:
                    rows.append((guard_id, name, low, high, chance, placement, ",".join(resolved)))

    rows.sort(key=lambda r: (int(r[0]), r[2]))
    lines = ["guard_npc_id\tpattern\tlow_hp\thigh_hp\tchance\tplacement\tsummons"]
    lines += ["\t".join(str(c) for c in r) for r in rows]
    body = "\n".join(lines) + "\n"
    if args.out:
        pathlib.Path(args.out).write_text(body, encoding="utf-8")
    else:
        print(body, end="")

    print(f"\npatterns with reinforcement branches: {patterns_seen}", file=sys.stderr)
    print(f"rows emitted: {len(rows)} for {len({r[0] for r in rows})} guards", file=sys.stderr)
    print("\nshape census (timer, live_time, spawn_range, location) -> branches:", file=sys.stderr)
    for shape, n in shapes.most_common():
        print(f"  {shape} -> {n}", file=sys.stderr)
    if unresolved:
        print("\nunresolved:", file=sys.stderr)
        for what, n in unresolved.most_common(20):
            print(f"  {what} x{n}", file=sys.stderr)


if __name__ == "__main__":
    main()
