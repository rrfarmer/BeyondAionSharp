"""NPCs that wake, wait, and place something — the portable subset of that shape.

WHY THIS EXISTS
---------------
`audit_idle_spawns.py` found 132 retail patterns that spawn from `on_idle_timer` whose npcs run a
generic class here, doing nothing. Most need more than a table: they carry several branches, or flag
guards, or actions this port has no answer for (`set_condition_spawn_variable` is the big one, 105 uses,
and the conditional spawn engine behind it is unbuilt).

**Nineteen are expressible exactly**: one unguarded rung, a wake-up delay, and nothing in the rung but
spawns and the timer. Those are what this extracts. Every field is carried per npc because none of them
is constant across the set -- the wait runs 2 to 600 seconds, the placements 1 to 11, and the re-arm is
absent, zero, or a real period.

THE THREE THINGS THAT GO WRONG
------------------------------
* **`SPAWN_LOCATION_MY_POINT` carries no coordinates.** Reading those as absolute puts the spawn at the
  world origin, which the Tiamat beacon table did before it was caught.
* **A re-arm of `delay=0` stops the timer**, it does not repeat -- see `PatternAi.SetIdleTimer`.
* **`num_to_spawn` is not the number of spawn blocks.** A rung may carry one block asking for four.

CLI:
    python extract_idle_spawns.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
from client_npc_names import npc_names  # noqa: E402

BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)

#: Classes that do nothing with a timer, so an npc on one cannot run the rung whatever retail says --
#: **and the class this table feeds**.
#:
#: Without `idle_spawner` in this set the extractor is self-defeating: binding the npcs it found removes
#: them from its own search, the next run emits nothing, and `regen_check` reports the committed table
#: as drift. A generated table has to keep finding the rows it already produced.
GENERIC = {"aggressive", "general", "onedmg_aggressive", "aggressive_noloot", "dummy", "no_interaction",
           "idle_spawner"}


def top_level(block: str) -> list[str]:
    """Action names at depth one. Nested children are stripped first.

    Matching `<(\w+)>` against the raw block returns the *spawn block's own children* -- `npc_nameid`,
    `spawn_location_type` and the rest -- and reads as a rung full of unknown actions. That mistake put
    the count of portable patterns at zero on the first pass.
    """
    flattened = re.sub(r"<(\w+)>.*?</\1>", lambda m: "<%s/>" % m.group(1), block, flags=re.S)
    return [m.group(1) for m in re.finditer(r"<(\w+)/>", flattened)]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows: list[tuple] = []
    dropped = 0
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            idle = re.search(r"<on_idle_timer>(.*?)</on_idle_timer>", body, re.S)
            if not idle or "<spawn>" not in idle.group(1):
                continue
            owners = [n for n in binders.get(named.group(1), []) if ai.get(n) in GENERIC]
            if not owners:
                continue

            branches = list(BRANCH_RE.finditer(idle.group(1)))
            wake = re.search(r"<on_wake_up>(.*?)</on_wake_up>", body, re.S)
            waited = re.search(r"<set_idle_timer>\s*<delay>(\d+)</delay>", wake.group(1)) if wake else None
            if len(branches) != 1 or not waited:
                continue
            guards = re.search(r"<conditions>(.*?)</conditions>", branches[0].group(1), re.S)
            if guards and guards.group(1).strip():
                continue
            actions = re.search(r"<actions>(.*?)</actions>", branches[0].group(1), re.S)
            if not actions:
                continue
            if [a for a in top_level(actions.group(1)) if a not in ("spawn", "set_idle_timer")]:
                continue

            rearm = re.findall(r"<set_idle_timer>\s*<delay>(\d+)</delay>", actions.group(1))
            for spawn in re.finditer(r"<spawn>(.*?)</spawn>", actions.group(1), re.S):
                block = spawn.group(1)
                target = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", block)
                placed = dev.get(target.group(1)) if target else None
                if placed is None or placed not in ai:
                    dropped += 1
                    continue
                where = re.search(r"<spawn_location_type>(\w+)</", block)
                kind = where.group(1) if where else "SPAWN_LOCATION_ABSOLUTE"
                # Three of them, and reading any as another puts the spawn somewhere else entirely:
                # MY_POINT carries no coordinates, RELATIVE carries an OFFSET from the npc, and only
                # ABSOLUTE carries world coordinates. The first emitted table read RELATIVE as absolute
                # and placed four arena adds at x=1,y=1 -- the corner of the map.
                place = ("self" if kind.endswith("MY_POINT")
                         else "offset" if kind.endswith("RELATIVE")
                         else "absolute")
                count = re.search(r"<num_to_spawn>(\d+)</", block)
                seconds = re.search(r"<live_time>(\d+)</", block)
                spot = [re.search(r"<%s>([-\d.]+)</%s>" % (axis, axis), block) for axis in "xyz"]
                for owner in owners:
                    rows.append((owner, int(waited.group(1)),
                                 int(rearm[0]) if rearm else -1,
                                 placed, int(count.group(1)) if count else 1,
                                 int(seconds.group(1)) if seconds else 0, place,
                                 float(spot[0].group(1)) if spot[0] else 0.0,
                                 float(spot[1].group(1)) if spot[1] else 0.0,
                                 float(spot[2].group(1)) if spot[2] else 0.0,
                                 named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\twake_ms\trearm_ms\tplaced\tcount\tlive\tplace\tx\ty\tz\tpattern\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    per = collections.Counter(r[0] for r in rows)
    print(f"{len(rows)} placements across {len(per)} npcs -> {args.out}")
    if dropped:
        print(f"    {dropped} name an npc with no template here, and are dropped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
