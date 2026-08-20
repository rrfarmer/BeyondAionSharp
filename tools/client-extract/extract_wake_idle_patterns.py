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
    python extract_wake_idle_patterns.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
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
from audit_idle_spawns import flatten  # noqa: E402
from extract_battle_cycles import ROLES, TARGETS  # noqa: E402
from extract_idle_cycles import FLAG_KINDS  # noqa: E402

#: What the shared parser can say, used only to name the first thing that stops a pattern. The parser
#: refuses by returning None and says nothing about why, and "381 refused" is a number rather than a
#: work plan.
SAYABLE_CONDITIONS = set(FLAG_KINDS) | {"test_probability", "increase_intvar"}
SAYABLE_ACTIONS = {"spawn", "set_idle_timer", "set_condition_spawn_variable", "despawn_self",
                   "broadcast_message", "say_to_all", "display_system_message", "use_skill"}

#: The skill targets this table can say: the hate-list ones plus retail's role targets, which the queue
#: carries as a creature. The same map the battle table uses, so the two cannot disagree about what
#: `OBJI_SELF` means -- and it is very nearly the only one that matters here, 2,387 of the 2,389 casts
#: in these handlers being self-targeted.
SKILL_TARGETS = dict(TARGETS) | {name: "@" + role for name, role in ROLES.items()}

BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)

#: Every class an npc may already be on and still acquire generated pattern rows.
#:
#: **This used to be per table, and that is what made the tables mutually exclusive.** Nothing here
#: excluded another table's npcs on purpose; the sets simply disagreed, and binding order did the rest
#: -- once the battle table moved an npc from `aggressive` to `battle_cycle`, no other extractor could
#: see it any more. The measured cost was 533 npcs holding a retail rotation their owning table could
#: not read, among much else.
#:
#: The set is now the same everywhere, so a pattern's handlers are read by every table that can read
#: them, and `GeneratedPattern` composes what each npc ends up with. The class an npc is bound to still
#: decides only one thing -- whether it fights -- which is why the generated class names are listed
#: here too: rebinding must be idempotent, or a second run would drop everything the first run took.
#:
#: `wake_variable` and `wake_variable_aggressive` are **deliberately absent**, and it cost 143 npcs
#: with real death rungs. Those two classes descend from `GeneralNpcAI` and `AggressiveNpcAI`, not
#: from `PatternAi`, so they have no slots to compose into -- an npc bound there would carry rows that
#: never run, which is the exact failure mode this whole change exists to remove. Folding them in means
#: making `extract_wake_variables` give those npcs up under its own richer-wins rule and rebinding
#: them, which is a separate change with its own thing to verify.
GENERIC = {"aggressive", "general", "battle_cycle", "death_spawn", "idle_cycle",
           "idle_cycle_passive", "aggressive_pattern", "passive_pattern"}

HANDLERS = ["on_wake_up", "on_idle_timer"]


def blocking_element(body: str, handlers) -> str:
    """The first element in these handlers the parser cannot say, for counting refusals by cause."""
    for handler in handlers:
        block = re.search(r"<%s>(.*?)</%s>" % (handler, handler), body, re.S)
        if not block:
            continue
        for branch in re.finditer(r"<pattern>(.*?)</pattern>", block.group(1), re.S):
            for tag, allowed in (("conditions", SAYABLE_CONDITIONS), ("actions", SAYABLE_ACTIONS)):
                found = re.search(r"<%s>(.*?)</%s>" % (tag, tag), branch.group(1), re.S)
                if not found:
                    continue
                for name in flatten(found.group(1)):
                    if name not in allowed:
                        return f"{tag[:-1]} {name}"
    # Vocabulary is fine, so it is the data: a spawn naming an npc with no template here, a message
    # whose string id does not resolve, or a skill target this table cannot say.
    return "an npc, string or skill target this port does not have"


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
            actions = read_actions(found.group(1), dev, known, strings, SKILL_TARGETS)
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

    # npc -> index -> skill id, castable here. Retail names a skill by its place in that npc's own list,
    # so one pattern resolves differently for each npc running it.
    skills: dict[int, dict[int, int]] = collections.defaultdict(dict)
    for line in (args.repo / "tools/client-extract/out/npc_skill_lists.tsv").read_text(
            encoding="utf-8").splitlines()[1:]:
        fields = line.split("\t")
        if fields[5] == "TRUE":
            skills[int(fields[0])][int(fields[1])] = int(fields[3])

    # An npc gives up its own wake pattern when **a hand-written account of it already exists**, and
    # only then. `spawn_helpers.xml` is that account: a curated file, comments and all, where somebody
    # decided what an encounter's adds are and how many come per health band. Kasika's fourth-tier guard
    # is there, and retail also gives it a hazard pattern that casts once and vanishes; both cannot be
    # authoritative, so the curated one wins.
    #
    # **Being placed by a generated table is not such an account.** Those tables place an npc and say
    # nothing else about it -- the Tiamat beacon lays a tornado and has no opinion on what the tornado
    # does -- and excluding those npcs is how the hazard this work exists for stayed dead through two
    # entries that claimed to have fixed it.
    placed: set[int] = set()
    helpers = args.repo / "game-server/data/static_data/ai/spawn_helpers.xml"
    if helpers.exists():
        placed = {int(m.group(1))
                  for m in re.finditer(r'npcId="(\d+)"', A.read_text(helpers))}

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
    dropped_owners = 0
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
                      if ai.get(n) in GENERIC and n not in spoken_for and n not in placed]
            if not owners:
                continue

            read = {handler: read_handler(body, handler, dev, ai.keys(), strings)
                    for handler in HANDLERS}
            if any(rungs is None for rungs in read.values()):
                refused[blocking_element(body, HANDLERS)] += 1
                continue
            if not any(read.values()):
                continue

            # An npc already on the wake table only moves here if this says *more* about it. The two
            # rules have to be the same rule read from both ends, or the tables overlap and every npc
            # in the intersection is claimed twice -- which is how 390 of them ended up here when 93
            # had anything to gain.
            # An owner whose skill list cannot answer every index is dropped, not the pattern: one
            # npc missing a skill says nothing about the others running the same script.
            wanted = {action[1] for rungs in read.values() for _, _, _, a in rungs
                      for action in a if action[0] == "skill"}
            if wanted:
                able = [n for n in owners if all(i in skills.get(n, {}) for i in wanted)]
                dropped_owners += len(owners) - len(able)
                owners = able
                if not owners:
                    refused["no npc here whose skill list answers the indices"] += 1
                    continue

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
                        # A branch that casts and then removes the npc is a hazard, and the queued
                        # path would lose the cast: the queue is drained by the attack loop and the npc
                        # is gone first. Marked here so the emitter can choose the immediate helper.
                        hazard = any(a[0] == "despawn_self" for a in actions)
                        for order, action in enumerate(actions):
                            if action[0] == "skill":
                                action = ("skill_now" if hazard and action[4] == "ME" else "skill",
                                          skills[npc][action[1]]) + action[2:]
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
    if dropped_owners:
        print(f"    {dropped_owners} npcs dropped from a pattern their skill list cannot answer")
    for reason, count in refused.most_common(6):
        print(f"    {count:4d} refused: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
