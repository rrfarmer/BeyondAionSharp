"""Transcribe Tiamat's dying-phase rotation out of the retail pattern.

`IDTiamat_Tiamat_Dragon_Dying_Named_60_Al` is the largest single-encounter table in
this work: four health bands, each a chain of four to seventeen battle-timer steps,
every step placing hazards at fixed coordinates. Our class picks its breath with
`Rnd.NextInt(3)` and places no telegraph at all — see docs/retail-ai-fidelity.md.

The transcription is emitted as data rather than read by eye because it is 40-odd
steps and several hundred coordinates, and because getting one step's delay or one
beacon's heading wrong is not something review would catch.

One row per (band, step, spawn):

    band  step  timer  next_timer  delay_ms  label  skill_indices  npc_id  count  x  y  z  dir  live

Steps that only cast carry a single row with an empty npc_id, so the chain's shape
survives even where the cast cannot be translated.

CLI:
    python extract_tiamat_rotation.py <patterns_dir> <binding_tsv> [--out FILE]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import NAME_RE, PATTERN_RE, SPAWN_RE, read_text
from extract_guard_reinforcements import BOUNDARY_RE, TIMER_RE, branches_of

PATTERN = "IDTiamat_Tiamat_Dragon_Dying_Named_60_Al"
COMMENT_RE = re.compile(r"<comment>([^<]*)</comment>")
INDEX_RE = re.compile(r"<skill>SKILLI_INDEX_(\d+)</skill>")
ARM_RE = re.compile(
    r"<add_battle_timer>.*?BTIMERI_INDEX_(\d+).*?<delay>(\d+)</delay>.*?</add_battle_timer>", re.S)
NAMEID_RE = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")
COUNT_RE = re.compile(r"<num_to_spawn>(\d+)</num_to_spawn>")
LIVE_RE = re.compile(r"<live_time>(\d+)</live_time>")
DIR_RE = re.compile(r"<dir>(-?[\d.]+)</dir>")
AXIS_RE = {a: re.compile(rf"<{a}>(-?[\d.]+)</{a}>") for a in ("x", "y", "z")}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--out")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    by_devname: dict[str, str] = {}
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) < 4 or parts[0] == "npc_id":
            continue
        by_devname.setdefault(parts[1].lower(), parts[0])

    rows: list[tuple] = []
    unresolved: collections.Counter = collections.Counter()
    steps_per_band: collections.Counter = collections.Counter()

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        text = read_text(path)
        for match in PATTERN_RE.finditer(text):
            block = match.group(0)
            name = NAME_RE.search(block)
            if not name or name.group(1) != PATTERN:
                continue

            for order, branch in enumerate(branches_of(block, "on_battle_timer")):
                slot = TIMER_RE.search(branch)
                if slot is None:
                    continue
                band = BOUNDARY_RE.search(branch)
                band_label = f"{band.group(1)}-{band.group(2)}" if band else "any"
                arm = ARM_RE.search(branch)
                label = COMMENT_RE.search(branch)
                indices = ",".join(INDEX_RE.findall(branch))
                common = (
                    band_label, order, int(slot.group(1)),
                    int(arm.group(1)) if arm else -1,
                    int(arm.group(2)) if arm else 0,
                    label.group(1) if label else "",
                    indices,
                )
                steps_per_band[band_label] += 1

                placed = False
                for spawn in SPAWN_RE.finditer(branch):
                    body = spawn.group(2)
                    dev = NAMEID_RE.search(body)
                    if not dev:
                        continue
                    npc_id = by_devname.get(dev.group(1).lower())
                    if npc_id is None:
                        unresolved[dev.group(1)] += 1
                        continue
                    count = COUNT_RE.search(body)
                    live = LIVE_RE.search(body)
                    heading = DIR_RE.search(body)
                    coords = {a: AXIS_RE[a].search(body) for a in ("x", "y", "z")}
                    if not all(coords.values()):
                        unresolved[f"{dev.group(1)} without coordinates"] += 1
                        continue
                    rows.append(common + (
                        npc_id,
                        int(count.group(1)) if count else 1,
                        coords["x"].group(1), coords["y"].group(1), coords["z"].group(1),
                        heading.group(1) if heading else "0",
                        int(live.group(1)) if live else 0,
                    ))
                    placed = True

                if not placed:
                    rows.append(common + ("", 0, "", "", "", "", 0))

    header = ("band\tstep\ttimer\tnext_timer\tdelay_ms\tlabel\tskill_indices\t"
              "npc_id\tcount\tx\ty\tz\tdir\tlive")
    body = "\n".join([header] + ["\t".join(str(c) for c in r) for r in rows]) + "\n"
    if args.out:
        pathlib.Path(args.out).write_text(body, encoding="utf-8")
    else:
        print(body, end="")

    print(f"\nrows: {len(rows)}", file=sys.stderr)
    print(f"steps per band: {dict(steps_per_band)}", file=sys.stderr)
    print(f"distinct npcs placed: {len({r[7] for r in rows if r[7]})}", file=sys.stderr)
    if unresolved:
        print("\nunresolved:", file=sys.stderr)
        for what, n in unresolved.most_common(10):
            print(f"  {what} x{n}", file=sys.stderr)


if __name__ == "__main__":
    main()
