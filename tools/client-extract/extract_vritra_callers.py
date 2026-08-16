"""Extract the Runatorium's Vritra-calling controllers.

`BIDRuneWP_Main_CallVritra*` are invisible controllers standing in Infinity Shard
(300800000). Something spawns one; on waking it puts a Vritra trooper on the floor
and removes itself two seconds later. Eight of them, in two shapes:

  * a **weighted cascade** — ten branches at equal priority, each with its own
    `test_probability`, plus one unguarded branch beneath them. Retail evaluates in
    order and stops at the first that passes, so this is a weighted pick with a
    guaranteed fallback, not ten independent rolls.
  * a **squad** — one unguarded branch spawning three troopers at once.

Emits one row per (controller, option, spawn):

    caller_npc_id  pattern  option  chance  npc_id  count  x  y  z

`option` is the branch's position in the cascade, so the AI can reproduce the
evaluation order. `chance` is 100 for the unguarded fallback.

CLI:
    python extract_vritra_callers.py <patterns_dir> <binding_tsv> [--out FILE]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import NAME_RE, PATTERN_RE, SPAWN_RE, read_text
from extract_guard_reinforcements import PROB_RE, branches_of

CALLER_RE = re.compile(r"^BIDRuneWP_Main_CallVritra")
NAMEID_RE = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")
COUNT_RE = re.compile(r"<num_to_spawn>(\d+)</num_to_spawn>")
COORD_RE = {axis: re.compile(rf"<{axis}>(-?[\d.]+)</{axis}>") for axis in ("x", "y", "z")}
PRIORITY_RE = re.compile(r"<priority>(\d+)</priority>")


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

    rows: list[tuple] = []
    skipped: collections.Counter = collections.Counter()

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        text = read_text(path)
        for match in PATTERN_RE.finditer(text):
            block = match.group(0)
            name_match = NAME_RE.search(block)
            if not name_match or not CALLER_RE.match(name_match.group(1)):
                continue
            name = name_match.group(1)
            caller_ids = owners.get(name, [])
            if not caller_ids:
                skipped[f"{name}: no npc runs it"] += 1
                continue

            # Retail evaluates highest priority first and, within a priority, in document order.
            options: list[tuple] = []
            for branch in branches_of(block, "on_wake_up"):
                spawns = []
                for spawn in SPAWN_RE.finditer(branch):
                    body = spawn.group(2)
                    dev = NAMEID_RE.search(body)
                    if not dev:
                        continue
                    npc_id = by_devname.get(dev.group(1).lower())
                    if npc_id is None:
                        skipped[f"devname {dev.group(1)}"] += 1
                        spawns = []
                        break
                    count = COUNT_RE.search(body)
                    coords = {a: COORD_RE[a].search(body) for a in ("x", "y", "z")}
                    if not all(coords.values()):
                        skipped[f"{name}: spawn without coordinates"] += 1
                        spawns = []
                        break
                    spawns.append((
                        npc_id,
                        int(count.group(1)) if count else 1,
                        coords["x"].group(1), coords["y"].group(1), coords["z"].group(1),
                    ))
                if not spawns:
                    continue
                prob = PROB_RE.search(branch)
                prio = PRIORITY_RE.search(branch)
                options.append((
                    int(prio.group(1)) if prio else 0,
                    int(prob.group(1)) if prob else 100,
                    spawns,
                ))

            if not options:
                continue
            # Sort by priority descending, keeping document order within a priority.
            order = sorted(range(len(options)), key=lambda i: (-options[i][0], i))
            for caller_id in caller_ids:
                for index, source in enumerate(order):
                    _prio, chance, spawns = options[source]
                    for npc_id, count, x, y, z in spawns:
                        rows.append((caller_id, name, index, chance, npc_id, count, x, y, z))

    rows.sort(key=lambda r: (int(r[0]), r[2]))
    header = "caller_npc_id\tpattern\toption\tchance\tnpc_id\tcount\tx\ty\tz"
    body = "\n".join([header] + ["\t".join(str(c) for c in r) for r in rows]) + "\n"
    if args.out:
        pathlib.Path(args.out).write_text(body, encoding="utf-8")
    else:
        print(body, end="")

    print(f"\nrows: {len(rows)} for {len({r[0] for r in rows})} controllers", file=sys.stderr)
    guaranteed = {(r[0], r[2]) for r in rows if r[3] == 100}
    print(f"controllers with a guaranteed fallback: "
          f"{len({c for c, _ in guaranteed})}", file=sys.stderr)
    if skipped:
        print("\nskipped:", file=sys.stderr)
        for what, n in skipped.most_common(10):
            print(f"  {what} x{n}", file=sys.stderr)


if __name__ == "__main__":
    main()
