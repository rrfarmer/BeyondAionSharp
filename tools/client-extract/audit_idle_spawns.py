"""Retail npcs that wait on a timer and then put something on the ground, which this port ignores.

WHY THIS EXISTS
---------------
Tiamat's breath was missing for exactly this reason and it took a chased loose end to find it. The
rotation placed the beacons; each beacon's own pattern then armed a 2000ms idle timer and spawned the
damage along the line it marked, and twelve of the fifteen beacons here were on plain `aggressive`,
which does nothing. The warning appeared and the breath never landed.

**That shape is not unique to Tiamat.** 132 retail patterns spawn from `on_idle_timer`, and 153 of the
npcs bound to them run a generic class here. Each is an add, a hazard or a controller that retail places
on a delay and this port does not place at all.

WHAT THE COLUMNS MEAN
---------------------
* `spawns` -- how many spawn actions the idle rung carries. One is usually a hazard; forty is a
  rotation controller laying out a whole encounter.
* `at_self` -- `SPAWN_LOCATION_MY_POINT`, which carries no coordinates. Reading those as absolute puts
  the spawn at the world origin; the beacon table did exactly that before it was caught.
* `rearms` -- the rung ends with another `set_idle_timer`, so it is a loop rather than a one-shot. A
  port that drops the re-arm gets one wave of a mechanic that should repeat.
* `here` -- whether the spawned npc has a template in this port. A spawn naming one that does not is
  not portable, and those are counted rather than emitted.

CLI:
    python audit_idle_spawns.py [--limit N] [--pattern NAME]
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

REPO = pathlib.Path(__file__).resolve().parents[2]
PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

#: Classes that do nothing with a timer. An npc on one of these cannot run the rung no matter what its
#: retail pattern says.
GENERIC = {"aggressive", "general", "onedmg_aggressive", "aggressive_noloot", "dummy", "no_interaction"}


#: What this port can already express in an `on_idle_timer` rung. Conditions first, then actions.
#:
#: `set_flag_var` is retail's test-and-set and `unset_flag_var` its test-and-unset -- the alternating
#: idiom this project has ported by hand many times. `test_probability` is a roll. Everything on the
#: action side has a `Do.` helper, `set_condition_spawn_variable` included since the conditional spawn
#: engine was built.
SPEAKABLE_CONDITIONS = {"set_flag_var", "unset_flag_var", "is_flag_var", "test_probability"}
SPEAKABLE_ACTIONS = {"spawn", "set_idle_timer", "set_condition_spawn_variable", "despawn_self",
                     "broadcast_message"}


def report_vocabulary(patterns_dir, binders, ai, dev) -> int:
    """Which unported patterns are expressible now, and what the rest are waiting on.

    The point is that the answer moves. When the conditional spawn engine was built,
    `set_condition_spawn_variable` became speakable and one more pattern came into reach; the 105 uses of
    it turned out to be blocked by branch structure rather than by the action. Re-running this after any
    new `Do.` helper says whether it was worth building.
    """
    branch_re = re.compile(r"<pattern>(.*?)</pattern>", re.S)
    speakable: set[str] = set()
    speakable_npcs: set[int] = set()
    blocked: collections.Counter = collections.Counter()

    for path in sorted(patterns_dir.rglob("NpcAIPatterns*.xml")):
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

            wake = re.search(r"<on_wake_up>(.*?)</on_wake_up>", body, re.S)
            if not (wake and re.search(r"<set_idle_timer>\s*<delay>(\d+)</delay>", wake.group(1))):
                blocked["no wake delay to start the cycle"] += 1
                continue

            missing = None
            for branch in branch_re.finditer(idle.group(1)):
                guards = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
                if guards:
                    for name in flatten(guards.group(1)):
                        if name not in SPEAKABLE_CONDITIONS:
                            missing = missing or f"condition {name}"
                actions = re.search(r"<actions>(.*?)</actions>", branch.group(1), re.S)
                if actions:
                    for name in flatten(actions.group(1)):
                        if name not in SPEAKABLE_ACTIONS:
                            missing = missing or f"action {name}"

            if missing:
                blocked[missing] += 1
            else:
                speakable.add(named.group(1))
                speakable_npcs.update(owners)

    print(f"{len(speakable)} patterns ({len(speakable_npcs)} npcs) are expressible with what this port "
          f"already has")
    print()
    print("the rest, by the first thing each needs:")
    for reason, count in blocked.most_common():
        print(f"   {count:4d}  {reason}")
    return 0


def flatten(block: str) -> list[str]:
    """Element names at depth one; nested children stripped first."""
    return [m.group(1) for m in
            re.finditer(r"<(\w+)/>", re.sub(r"<(\w+)>.*?</\1>",
                                            lambda m: "<%s/>" % m.group(1), block, flags=re.S))]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--patterns", type=pathlib.Path, default=PATTERNS)
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--pattern", help="show one pattern's npcs in full")
    ap.add_argument("--vocabulary", action="store_true",
                    help="what each unported pattern still needs, by the one thing that blocks it")
    args = ap.parse_args()

    templates = A.read_text(REPO / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns).items()}

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(REPO / "tools/client-extract/out/ai_binding.tsv").splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows = []
    for path in sorted(args.patterns.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            idle = re.search(r"<on_idle_timer>(.*?)</on_idle_timer>", body, re.S)
            if not idle or "<spawn>" not in idle.group(1):
                continue
            owners = [n for n in binders.get(named.group(1), []) if n in ai]
            deaf = [n for n in owners if ai[n] in GENERIC]
            if not deaf:
                continue

            spawns = re.findall(r"<spawn>(.*?)</spawn>", idle.group(1), re.S)
            at_self = sum(1 for s in spawns if "MY_POINT" in s)
            targets = [re.search(r"<npc_nameid>([^<]+)</npc_nameid>", s) for s in spawns]
            known = sum(1 for t in targets if t and dev.get(t.group(1)) in ai)
            armed = [int(d) for d in
                     re.findall(r"<set_idle_timer>\s*<delay>(\d+)</delay>", idle.group(1))]
            # A re-arm with a real delay is portable today. One with delay=0 is not: retail uses that
            # 1,006 times inside on_idle_timer and this port has never settled what zero means, so a
            # class porting one could spin rather than repeat. See IdleTimerSemanticsTests.
            rearms = "zero" if armed and all(d == 0 for d in armed) else bool(armed)
            rows.append((len(deaf), named.group(1), deaf, len(spawns), at_self, known, rearms))

    if args.vocabulary:
        return report_vocabulary(args.patterns, binders, ai, dev)

    rows.sort(key=lambda r: (-r[0], -r[3]))
    if args.pattern:
        for row in rows:
            if row[1] == args.pattern:
                print(f"{row[1]}: npcs {row[2]}  spawns={row[3]} at_self={row[4]} here={row[5]} "
                      f"rearms={row[6]}")
        return 0

    npcs = {n for row in rows for n in row[2]}
    print(f"{len(rows)} retail patterns spawn from on_idle_timer and have npcs on a generic class here")
    print(f"{len(npcs)} npcs affected\n")
    print(f"  {'pattern':46s} {'npcs':>4} {'spawns':>6} {'at_self':>7} {'here':>4}  rearms")
    for _, pattern, deaf, spawns, at_self, known, rearms in rows[:args.limit]:
        print(f"  {pattern[:44]:46s} {len(deaf):4d} {spawns:6d} {at_self:7d} {known:4d}  {rearms}")
    if len(rows) > args.limit:
        print(f"  ... and {len(rows) - args.limit} more")

    portable = [r for r in rows if r[5] == r[3]]
    print(f"\n{len(portable)} of the {len(rows)} spawn only npcs this port has templates for")
    print(f"{sum(1 for r in rows if r[6] is True)} re-arm with a real delay -- loops portable today")
    print(f"{sum(1 for r in rows if r[6] == 'zero')} re-arm with delay=0, which this port has not "
          f"settled and cannot port safely")
    return 0


if __name__ == "__main__":
    sys.exit(main())
