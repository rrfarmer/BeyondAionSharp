"""Full retail patterns for the npcs that do not fight: markers, controllers and scenery.

WHY THIS EXISTS
---------------
Three tables already read pieces of these npcs' behaviour, each stopping where its own machinery ran
out. `WakeVariables` takes an unguarded list of spawn-variable writes and a `despawn_self`; anything
with a guard, a timer, a message or a spawn beside it was counted and left. That is **207 patterns
across 682 npcs**, and every one of them is a `general` npc -- a flag, a wave controller, a piece of
scenery -- doing something a full pattern runtime could say without any new vocabulary at all.

What blocked it was not the vocabulary but the class. `PatternAi` extends `AggressiveNpcAI`, and
binding a passive npc to it makes it attack players on sight -- which this project did once, to 67 wave
controllers, and did not notice for a dozen entries because every pin stayed green. `PassivePatternAi`
puts `AggressiveNpcAI`'s three overrides back the way `GeneralNpcAI` has them, and is pinned by a test
that spawns the same npc under both classes and watches only one of them take an aggro event.

WHAT IT READS
-------------
`on_wake_up` and `on_idle_timer`, with the same parser `IdleCycles` uses, so the two tables cannot drift
on what an action means. The wake handler here carries **actions**, not just a delay: that is the whole
difference from `IdleCycles`, whose wake rung is only ever `set_idle_timer`.

WHAT IS LEFT OUT
----------------
A pattern is taken only if every branch of both handlers is sayable in full, for the usual reason --
branch lists are first-match-wins and dropping a rung promotes the next. Npcs already driven by another
table keep it, so nothing is bound twice.

CLI:
    python extract_passive_patterns.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
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
from extract_idle_cycles import read_actions, read_guards, string_ids  # noqa: E402

BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)

#: `general`, the class this table feeds, and `wake_variable` -- which is `general` underneath.
#:
#: The wake table took the simple cases first, and **93 npcs on it were running a partial pattern**:
#: their spawn-variable writes without the timer, message or despawn standing beside them. This table
#: is authoritative wherever it can say more, and `extract_wake_variables` gives those patterns up.
#: `wake_variable_aggressive` is deliberately absent -- those npcs fight, and there is no aggressive
#: pattern class for them yet.
GENERIC = {"general", "passive_pattern", "wake_variable"}

HANDLERS = ["on_wake_up", "on_idle_timer"]


def read_handler(body: str, name: str, dev, known, strings):
    """Every branch of one handler, or None if any part of it cannot be said."""
    block = re.search(r"<%s>(.*?)</%s>" % (name, name), body, re.S)
    if not block:
        return []
    branches = []
    for index, branch in enumerate(BRANCH_RE.finditer(block.group(1))):
        guards: list[str] = []
        found = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
        if found:
            guards = read_guards(found.group(1))
            if guards is None:
                return None
        actions: list[tuple] = []
        found = re.search(r"<actions>(.*?)</actions>", branch.group(1), re.S)
        if found:
            actions = read_actions(found.group(1), dev, known, strings)
            if actions is None:
                return None
        if not actions:
            continue
        priority = re.search(r"<priority>(\d+)</priority>", branch.group(1))
        branches.append((index, int(priority.group(1)) if priority else 0, guards, actions))
    return branches


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}
    strings = string_ids(args.repo)

    spoken_for: set[int] = set()
    for source in (args.repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs"):
        for found in re.finditer(r"=\s*(\d{6})\s*;",
                                 source.read_text(encoding="utf-8", errors="replace")):
            spoken_for.add(int(found.group(1)))

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows: list[tuple] = []
    refused: collections.Counter = collections.Counter()
    patterns = 0
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            if not any(re.search(r"<%s>" % handler, body) for handler in HANDLERS):
                continue

            owners = [n for n in binders.get(named.group(1), [])
                      if ai.get(n) in GENERIC and n not in spoken_for]
            if not owners:
                continue

            read = {handler: read_handler(body, handler, dev, ai.keys(), strings)
                    for handler in HANDLERS}
            if any(rungs is None for rungs in read.values()):
                refused["a branch this port cannot say"] += 1
                continue
            if not any(read.values()):
                continue

            # An npc already on the wake table only moves here if this says *more* about it. The two
            # rules have to be the same rule read from both ends, or the tables overlap and every npc
            # in the intersection is claimed twice -- which is how 390 of them ended up here when 93
            # had anything to gain.
            total = sum(len(a) for rungs in read.values() for _, _, _, a in rungs)
            writes = sum(1 for rungs in read.values() for _, _, _, a in rungs
                         for action in a if action[0] == "var")
            # Unconditionally, not "unless the npc is currently on the wake table": both extractors
            # have to reach the same verdict from the pattern alone, or the answer depends on which
            # table happens to hold the npc today and a regeneration moves it back and forth.
            if total <= writes:
                refused["the wake table says as much about it"] += 1
                continue

            patterns += 1
            for npc in owners:
                for handler in HANDLERS:
                    for index, priority, guards, actions in read[handler]:
                        for order, action in enumerate(actions):
                            rows.append((npc, named.group(1), handler, index, priority,
                                         "|".join(guards), order) + action)

    rows.sort(key=lambda r: (r[0], r[2], r[3], r[6]))
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\tpattern\thandler\tbranch\tpriority\tguards\torder\t"
                  "kind\ta1\ta2\ta3\tplace\tx\ty\tz\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    print(f"{patterns} passive patterns across {len(npcs)} npcs, {len(rows)} actions -> {args.out}")
    for reason, count in refused.most_common(5):
        print(f"    {count:4d} refused: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
