"""Extract the fortress gate guards' squad sequences from the retail patterns.

Sibling to `extract_guard_reinforcements.py` and a different mechanic despite the
name. A `BGuard_*Gate*` npc is not a guard that calls for help as it is worn down —
it is a **gate**: something attacks it, it puts a squad out in waves on a fixed
timer chain, and then it removes itself. No health bands, no coin flips.

    on_enter_attack  -> arm T0
    T0               -> arm T1 after N ms, spawn this wave
    T1               -> arm T2 after M ms, spawn the next wave
    T2               -> despawn_self
    on_leave_attack  -> despawn the squad, despawn_self

Emits one row per (gate npc, step), in chain order:

    gate_npc_id  pattern  step  delay_ms  placement  summons

`delay_ms` is how long after the previous step this one fires — the opening delay
for step 0 comes from `on_enter_attack_state`. `summons` is `npc_id*count` joined
by commas.

Rows are emitted only where every devname resolves and every spawn op is one this
runtime can place. Anything else is reported on stderr: a gate whose second wave is
missing looks like a gate with one wave, which is the failure this must not have.

CLI:
    python extract_gate_squads.py <patterns_dir> <binding_tsv> [--out FILE]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text
from extract_guard_reinforcements import (
    EXPRESSIBLE_OPS, TIMER_RE, branches_of, spawns_in)

GATE_RE = re.compile(r"^BGuard_.*Gate")
BASE_RE = re.compile(r"_L\d+M?$")

ARM_RE = re.compile(
    r"<add_battle_timer>.*?<btimer_indicator>BTIMERI_INDEX_(\d+)</btimer_indicator>"
    r".*?<delay>(\d+)</delay>.*?</add_battle_timer>", re.S)
DESPAWN_SELF_RE = re.compile(r"<despawn_self>")


def opening_delay(block: str) -> tuple[int, int] | None:
    """(timer slot, delay) the gate arms when something attacks it."""
    for branch in branches_of(block, "on_enter_attack_state"):
        armed = ARM_RE.search(branch)
        if armed:
            return int(armed.group(1)), int(armed.group(2))
    return None


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--out")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    by_devname: dict[str, str] = {}
    owners: dict[str, list[str]] = collections.defaultdict(list)
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) < 4 or parts[0] == "npc_id":
            continue
        by_devname.setdefault(parts[1].lower(), parts[0])
        owners[parts[3]].append(parts[0])

    # A level variant stores only what differs from its base: BGuard_DGate_L50 carries the timer
    # branches and nothing else, while BGuard_DGate carries the opener, the leave handler and the
    # message handler. Reading a variant on its own therefore finds a chain nothing ever starts --
    # fourteen of them, reported as "nothing armed on entering combat" before this existed.
    openings: dict[str, tuple[int, int]] = {}
    blocks: dict[str, str] = {}

    rows: list[tuple] = []
    skipped: collections.Counter = collections.Counter()
    inherited: collections.Counter = collections.Counter()
    chain_lengths: collections.Counter = collections.Counter()
    patterns_seen = 0

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        text = read_text(path)
        for match in PATTERN_RE.finditer(text):
            block = match.group(0)
            name_match = NAME_RE.search(block)
            if not name_match or not GATE_RE.match(name_match.group(1)):
                continue
            blocks[name_match.group(1)] = block
            found = opening_delay(block)
            if found is not None:
                openings[name_match.group(1)] = found

    for name, block in blocks.items():
            opening = openings.get(name)
            if opening is None:
                base = BASE_RE.sub("", name)
                opening = openings.get(base)
                if opening is None:
                    skipped[f"{name}: nothing armed on entering combat"] += 1
                    continue
                inherited[base] += 1

            # timer slot -> (spawns, next slot, delay to it)
            steps: dict[int, tuple] = {}
            for branch in branches_of(block, "on_battle_timer"):
                slot = TIMER_RE.search(branch)
                if slot is None:
                    continue
                spawns = spawns_in(branch)
                if not spawns:
                    continue
                nxt = ARM_RE.search(branch)
                steps[int(slot.group(1))] = (
                    spawns,
                    int(nxt.group(1)) if nxt else None,
                    int(nxt.group(2)) if nxt else 0,
                )
            if not steps:
                continue
            patterns_seen += 1

            gate_ids = owners.get(name, [])
            if not gate_ids:
                skipped[f"{name}: no npc runs it"] += 1
                continue

            # Walk the chain from whatever entering combat armed, so the order is retail's.
            walked: list[tuple] = []
            slot, delay = opening
            seen_slots: set[int] = set()
            while slot in steps and slot not in seen_slots:
                seen_slots.add(slot)
                spawns, nxt, nxt_delay = steps[slot]
                walked.append((delay, spawns))
                if nxt is None:
                    break
                slot, delay = nxt, nxt_delay
            if not walked:
                skipped[f"{name}: chain never reaches a spawning step"] += 1
                continue

            chain_lengths[len(walked)] += 1

            resolved_steps: list[tuple] = []
            broken = False
            for delay_ms, spawns in walked:
                ops = {sp[4] for sp in spawns}
                if not ops <= EXPRESSIBLE_OPS:
                    for op in ops - EXPRESSIBLE_OPS:
                        skipped[f"op {op} (placement not expressible yet)"] += 1
                    broken = True
                    break
                pairs = []
                for devname, count, _live, _rng, _op in spawns:
                    npc_id = by_devname.get(devname.lower())
                    if npc_id is None:
                        skipped[f"devname {devname}"] += 1
                        broken = True
                        break
                    pairs.append(f"{npc_id}*{count}")
                if broken:
                    break
                placement = "TARGET" if ops == {"spawn_on_target"} else "SELF"
                resolved_steps.append((delay_ms, placement, ",".join(pairs)))
            if broken or not resolved_steps:
                continue

            for gate_id in gate_ids:
                for index, (delay_ms, placement, summons) in enumerate(resolved_steps):
                    rows.append((gate_id, name, index, delay_ms, placement, summons))

    rows.sort(key=lambda r: (int(r[0]), r[2]))
    header = "gate_npc_id\tpattern\tstep\tdelay_ms\tplacement\tsummons"
    body = "\n".join([header] + ["\t".join(str(c) for c in r) for r in rows]) + "\n"
    if args.out:
        pathlib.Path(args.out).write_text(body, encoding="utf-8")
    else:
        print(body, end="")

    print(f"\ngate patterns with a squad: {patterns_seen}", file=sys.stderr)
    print(f"rows emitted: {len(rows)} for {len({r[0] for r in rows})} gates", file=sys.stderr)
    print(f"chain lengths: {dict(sorted(chain_lengths.items()))}", file=sys.stderr)
    if inherited:
        print(f"opening delay inherited from a base pattern: {sum(inherited.values())} variants",
              file=sys.stderr)
        for base, n in inherited.most_common():
            print(f"  from {base} x{n}", file=sys.stderr)
    if skipped:
        print("\nskipped:", file=sys.stderr)
        for what, n in skipped.most_common(20):
            print(f"  {what} x{n}", file=sys.stderr)


if __name__ == "__main__":
    main()
