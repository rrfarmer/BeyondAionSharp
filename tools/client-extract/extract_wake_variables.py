"""Spawn variables retail sets simply by an npc existing, and the gates they open.

WHY THIS EXISTS
---------------
The conditional spawn engine has both halves now -- gates that read and death handlers that write -- but
the join reaches only 5,082 of retail's 21,096 gated placements. Classifying the rest by *who writes the
variable* says where the remaining reach is:

| handler that writes a gated variable | gated placements |
|---|---|
| **`on_wake_up`** | **15,327** |
| `on_killed_by_user` | 9,360 |
| `on_killed_by_npc` | 9,280 |
| `on_message` | 7,130 |

`on_wake_up` is the largest by a wide margin and the simplest thing in the list: an npc announcing that
it exists. 553 patterns write a spawn variable there and do **nothing else** -- no guard, no spawn, no
timer -- and those alone reach **11,121 gated placements**.

WHY THIS IS NOT A PATTERN TABLE
-------------------------------
**719 of the 1,031 npcs involved are on `general`**, which is not aggressive. Every other table here
feeds a `PatternAi` subclass, and `PatternAi` extends `AggressiveNpcAI`, so binding one of those npcs to
a pattern class would make a passive npc attack players on sight -- the behaviour change this project
has refused four times already. `WakeVariableAI` extends `GeneralNpcAI` instead and does exactly one
thing, which is all these patterns do.

The npcs on `aggressive` get `WakeVariableAggressiveAI`, which descends from `AggressiveNpcAI` and
shares the same write. Binding them to the passive class would have taken their aggression away, which
is the mirror of the reason the passive class exists; two thin classes over one shared write is cheaper
than either mistake.

WHAT IS LEFT OUT
----------------
A pattern is taken only if every branch of `on_wake_up` is an unguarded list of variable writes. 136
carry a guard and 488 carry another action -- a message, a broadcast, a timer, a despawn -- and those
belong in a pattern table, not here.

CLI:
    python extract_wake_variables.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
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
from audit_idle_spawns import flatten  # noqa: E402
from client_npc_names import npc_names  # noqa: E402
from extract_idle_cycles import string_ids  # noqa: E402
import extract_wake_idle_patterns as PP  # noqa: E402

#: `general` and `aggressive`, each of which keeps what it is. The binder picks the class from the npc's
#: current one -- `wake_variable` descends from `GeneralNpcAI` and `wake_variable_aggressive` from
#: `AggressiveNpcAI` -- so neither group gains or loses aggression by acquiring a job.
#: `passive_pattern` is listed too, so a re-run sees the npcs that table took and re-decides on the
#: same evidence rather than on which table happens to hold them today. The split between the two is
#: made by the "does the other table say more" rule below, and it has to reach the same answer whichever
#: side the npc is sitting on, or a regeneration moves npcs back and forth for ever.
GENERIC = {"general", "aggressive", "wake_variable", "wake_variable_aggressive", "passive_pattern"}

#: Kept for the record: this used to exempt fighting npcs from the "the other table says more" rule,
#: because the only pattern class that could take them was passive and would have removed their
#: aggression. `AggressivePatternAI` closed that, so the exemption is gone and the rule applies to
#: every npc alike -- which is what makes the two tables disjoint whichever one currently holds it.


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
            wake = re.search(r"<on_wake_up>(.*?)</on_wake_up>", body, re.S)
            if not wake or "<set_condition_spawn_variable>" not in wake.group(1):
                continue

            writes: list[tuple[str, int, int]] = []
            vanishes = False
            blocked = None
            for branch in re.finditer(r"<pattern>(.*?)</pattern>", wake.group(1), re.S):
                guards = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
                if guards and flatten(guards.group(1)):
                    blocked = "a guarded branch"
                    break
                actions = re.search(r"<actions>(.*?)</actions>", branch.group(1), re.S)
                if not actions:
                    continue
                # `despawn_self` is taken as well: 75 of these patterns write a variable and then
                # remove the npc, which is a marker that exists only to announce a state and go. It
                # needs nothing a passive npc cannot do, unlike the timers and spawns below it.
                others = set(flatten(actions.group(1))) - {"set_condition_spawn_variable",
                                                           "despawn_self"}
                if others:
                    blocked = f"action {sorted(others)[0]}"
                    break
                if "despawn_self" in flatten(actions.group(1)):
                    vanishes = True
                for write in re.finditer(
                        r"<set_condition_spawn_variable>(.*?)</set_condition_spawn_variable>",
                        actions.group(1), re.S):
                    name = re.search(r"<string>([^<]*)</string>", write.group(1))
                    value = re.search(r"<set>(-?\d+)</set>", write.group(1))
                    modify = re.search(r"<modify>(-?\d+)</modify>", write.group(1))
                    if not name or not name.group(1).strip():
                        blocked = "a write with no variable name"
                        break
                    writes.append((name.group(1).strip(),
                                   int(value.group(1)) if value else 0,
                                   int(modify.group(1)) if modify else 0))
            if blocked:
                refused[blocked] += 1
                continue
            if not writes:
                continue

            # If the passive pattern table can say the whole pattern and that is more than these
            # writes, it takes the npc: running the writes alone leaves the timer, the message or the
            # despawn beside them unported, which reads as done and is not. Aggressive owners stay
            # here regardless, there being no aggressive pattern class for them.
            full = {h: PP.read_handler(body, h, dev, ai.keys(), strings) for h in PP.HANDLERS}
            richer = (not any(v is None for v in full.values())
                      and sum(len(a) for v in full.values() for _, _, _, a in v) > len(writes))

            owners = [n for n in binders.get(named.group(1), [])
                      if ai.get(n) in GENERIC and n not in spoken_for
                      and not richer]
            if not owners:
                # About our data rather than retail's: the npc is either absent here or already
                # modelled by an encounter class that must keep it.
                refused["no npc here that is free to run it"] += 1
                continue

            patterns += 1
            for npc in owners:
                for order, (name, value, modify) in enumerate(writes):
                    rows.append((npc, named.group(1), order, name, value, modify,
                                 "TRUE" if vanishes else "FALSE"))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\tpattern\torder\tname\tset\tmodify\tvanishes\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    print(f"{patterns} wake patterns across {len(npcs)} npcs, {len(rows)} writes -> {args.out}")
    for reason, count in refused.most_common(6):
        print(f"    {count:4d} refused: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
