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

    guard_npc_id  pattern  low_hp  high_hp  chance  placement  live_seconds  spawn_range
    attack_hate  summons

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

from audit_missing_adds import NAME_RE, PATTERN_RE, SPAWN_RE, read_text

# D, L and Dr. The first two are the Elyos/Asmodian abyss guards; Dr is the drakan side, which runs
# the same mechanic and was missed for one letter. BGuard is deliberately out -- those are the gates,
# a different mechanic with its own extractor -- and so are GwDGuard/GwLGuard, which have their own
# class already.
GUARD_RE = re.compile(r"^(?:(?:D|L|Dr)Guard_|BGuard_Chief)")

# The two ops this can turn into a table row. The other two retail ops place per-target
# (`spawn_on_multi_target`, one add on every valid target, capped) and per-attacker
# (`spawn_on_target_by_attacker_indicator`, on a chosen rank in the hate list). The runtime can
# express both, but each needs fields this table does not carry -- the cap and ordering for one,
# the attacker indicator for the other -- so rows using them are reported and skipped rather than
# flattened into "on the current target", which would put the wave in the wrong place.
EXPRESSIBLE_OPS = {"spawn", "spawn_on_target"}

# The dump is element-based; summarize_pattern.py renders attributes for reading.
#
# SPAWN_RE is imported rather than written here. Retail has four spawn ops and this file
# originally looked for one, which dropped four guard variants silently -- a guard using
# `spawn_on_target` drops its wave on whoever it is fighting, and reading only `<spawn>` made it
# look like a guard that calls nobody. The audit already knew all four; the two had drifted.
# Sharing the pattern is what stops them drifting again.
NAMEID_RE = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")
COUNT_RE = re.compile(r"<num_to_spawn>(\d+)</num_to_spawn>")
LIVE_RE = re.compile(r"<live_time>(\d+)</live_time>")
RANGE_RE = re.compile(r"<spawn_range>(\d+)</spawn_range>")
LOCATION_RE = re.compile(r"<spawn_location_type>([^<]+)</spawn_location_type>")

# `attack_target_after_spawn` with `hatepoints_to_add`: the summon arrives already fighting whoever
# it was dropped on, rather than waiting to be walked into. Carried per band because it changes the
# mechanic, not the decoration -- a trap that engages you is not a trap you can step around.
ATTACK_RE = re.compile(r"<attack_target_after_spawn>([A-Z]+)</attack_target_after_spawn>")
HATE_RE = re.compile(r"<hatepoints_to_add>(\d+)</hatepoints_to_add>")

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


def spawns_in(branch: str) -> list[tuple[str, int, int, int, str, int]]:
    """(devname, count, live_time, spawn_range, placement, hate) for each spawn in a branch.

    `hate` is 0 unless the spawn carries `attack_target_after_spawn=TRUE`, in which case it is
    retail's `hatepoints_to_add` -- what the summon starts with against whoever it landed on.
    """
    found = []
    for spawn in SPAWN_RE.finditer(branch):
        body = spawn.group(2)
        op = spawn.group(1)
        name = NAMEID_RE.search(body)
        if not name:
            continue
        count = COUNT_RE.search(body)
        live = LIVE_RE.search(body)
        rng = RANGE_RE.search(body)
        loc = LOCATION_RE.search(body)
        attack = ATTACK_RE.search(body)
        hate = HATE_RE.search(body)
        found.append((
            name.group(1),
            int(count.group(1)) if count else 1,
            int(live.group(1)) if live else 0,
            int(rng.group(1)) if rng else 0,
            op,
            int(hate.group(1)) if attack and attack.group(1) == "TRUE" and hate else 0,
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
                for _dev, _count, live, rng, loc, hate in spawns:
                    shapes[(timer, live, rng, loc, hate)] += 1

            guard_ids = owners.get(name, [])
            if not guard_ids:
                unresolved[f"pattern {name} has no npc"] += 1
                continue

            for low, high, chance, _timer, spawns in timers:
                resolved = []
                lifetimes = {sp[2] for sp in spawns}
                ranges = {sp[3] for sp in spawns}
                ops = {sp[4] for sp in spawns}
                hates = {sp[5] for sp in spawns}
                if not ops <= EXPRESSIBLE_OPS:
                    for op in ops - EXPRESSIBLE_OPS:
                        unresolved[f"op {op} (placement not expressible yet)"] += 1
                    continue
                placement = "TARGET" if ops == {"spawn_on_target"} else "SELF"
                # A band whose summons disagree about arriving hostile would need one flag per
                # summon rather than one per band. None do; if that ever changes, this reports it
                # instead of quietly taking the larger number.
                if len(hates) > 1:
                    unresolved[f"pattern {name} mixes hate {sorted(hates)} within one band"] += 1
                for devname, count, _live, _rng, _loc, _hate in spawns:
                    npc_id = by_devname.get(devname.lower())
                    if npc_id is None:
                        unresolved[f"devname {devname}"] += 1
                        resolved = []
                        break
                    resolved.append(f"{npc_id}*{count}")
                if not resolved:
                    continue
                for guard_id in guard_ids:
                    rows.append((guard_id, name, low, high, chance, placement,
                                 max(lifetimes), max(ranges), max(hates), ",".join(resolved)))

    rows.sort(key=lambda r: (int(r[0]), r[2]))
    lines = ["guard_npc_id\tpattern\tlow_hp\thigh_hp\tchance\tplacement"
             "\tlive_seconds\tspawn_range\tattack_hate\tsummons"]
    lines += ["\t".join(str(c) for c in r) for r in rows]
    body = "\n".join(lines) + "\n"
    if args.out:
        pathlib.Path(args.out).write_text(body, encoding="utf-8")
    else:
        print(body, end="")

    print(f"\npatterns with reinforcement branches: {patterns_seen}", file=sys.stderr)
    print(f"rows emitted: {len(rows)} for {len({r[0] for r in rows})} guards", file=sys.stderr)
    print(f"rows whose summons arrive fighting: {sum(1 for r in rows if r[8])}", file=sys.stderr)
    print("\nshape census (timer, live_time, spawn_range, location, hate) -> branches:", file=sys.stderr)
    for shape, n in shapes.most_common():
        print(f"  {shape} -> {n}", file=sys.stderr)
    if unresolved:
        print("\nunresolved:", file=sys.stderr)
        for what, n in unresolved.most_common(20):
            print(f"  {what} x{n}", file=sys.stderr)


if __name__ == "__main__":
    main()
