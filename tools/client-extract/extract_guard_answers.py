"""Who answers a guard's call for help, and with what weight.

WHY THIS EXISTS
---------------
`extract_guard_calls.py` records the *sending* half of `23000` and `PullCalls` the sending half of
`23200` and the Ashunatal pairs. This is the answering half of the whole family, which no table carried.

The answer is the most uniform shape in the dump. Across `23000`, `23100` and `23200` there are exactly
two rungs and no third:

    on_message N, param is an enemy, is_npc_state NPC_STATE_ATTACK -> switch_target 100
    on_message N, param is an enemy                                -> add_hate_point 1, attack_most_hating

85 of each for `23000`, 12 for `23100`, 6 for `23200`. The values are carried per npc anyway, because
they are not *quite* invariant -- a handful answer with 1000 or 5000 points, and sixteen answer with
`do_nothing`, which is a guard that hears the call and deliberately ignores it. Flattening those to the
common value would be inventing uniformity the dump does not have.

WHAT IT IS FOR
--------------
Measured against our spawnable npcs, the family divides cleanly:

| call | spawnable answerers | on a class that answers here |
|---|---|---|
| `23200` | 102 | 102 |
| `23000` | 385 | 361 |
| `23100` | 154 | 47 |

`23200` is done. The `23100` shortfall is 107 npcs, **102 of them `artifact_protector`** -- a class with
its own pattern and no `on_message` at all, so the answering rungs fold in additively. The remaining
24 on `23000` sit on five bespoke classes and are listed by `--gaps` rather than bound here.

CLI:
    python extract_guard_answers.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..] [--gaps]
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

#: The calls with an answering half worth binding. `23101` and `23109` have answering rungs whose
#: actions are empty in the dump, so they are read and dropped rather than emitted as no-ops.
#:
#: `30001` and `30002` are the npc-versus-npc half of the family and are here for a different reason:
#: not to bind new listeners -- every one of retail's 135 is already on a protector class -- but to
#: bound the ones we have. `AbstractSiegeProtectorAI` answered `30001` for **every** npc on the class,
#: 282 of them, so 147 protectors that retail leaves standing dropped everything and charged a waking
#: killer. `30003` is carried for membership only: its answer is `despawn_self`, not a hate rung, so it
#: has no points and emits no rung. Its four listeners are exactly the four npcs on the killer class --
#: which was exact by coincidence rather than by construction until this table could say so.
CALLS = {"23000", "23100", "23200", "30001", "30002", "30003"}

MSG_RE = re.compile(r"<on_message>(.*?)</on_message>", re.S)
BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)


def rung(body: str) -> tuple[str, int, bool] | None:
    """The action pair a rung answers with: ('switch'|'add'|'nothing', points, targets_the_sender).

    The two halves of the family aim at different objects and it is not incidental: `23xxx` names a
    player in `OBJI_MESSAGE_PARAM`, `3000x` names the caller in `OBJI_MESSAGE_SENDER`. Applying one
    with the other's target would put hate on the wrong creature entirely.
    """
    actions = re.search(r"<actions>(.*?)</actions>", body, re.S)
    if actions is None:
        return None
    text = actions.group(1)
    points = re.search(r"<point[s]?_to_add>(-?\d+)</", text)
    sender = "OBJI_MESSAGE_SENDER" in text
    if "<switch_target>" in text:
        return ("switch", int(points.group(1)) if points else 0, sender)
    if "<add_hate_point>" in text:
        return ("add", int(points.group(1)) if points else 0, sender)
    if "<do_nothing>" in text:
        # A guard that hears the call and deliberately ignores it. Recorded as "no points" rather than
        # "zero points": zero would emit a rung, and `AggroInfo.AddHate` floors hate at 1, so the npc
        # retail tells to stand still would join the fight with a single point and attack.
        return ("nothing", -1, sender)
    if "<despawn_self>" in text:
        # No hate at all: `30003` is answered by standing down. Recorded so the table can say WHO
        # answers it -- the rung itself belongs to the killer class, which already has it.
        return ("despawn", -1, sender)
    return None


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--gaps", action="store_true", help="print the npcs that answer and are not bound")
    args = ap.parse_args()

    # Every npc this port defines, NOT just the ones with a static spawn point. The distinction is
    # load-bearing: fortress protectors and their killers are placed by the siege system rather than by
    # world spawns, so `spawnable_npc_ids` cannot see them. Filtering on it built a table that named 4
    # killers where retail names 34, and that table was then used as a GATE -- which silenced 504
    # protectors that retail does give the rung. A table used to bound behaviour must be filtered on
    # whether the npc exists, not on whether this port happens to place it.
    live = set(re.findall(r'<npc_template npc_id="(\d+)"',
                          A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")))
    binders: dict[str, list[str]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows: set[tuple[int, int, int, int, int, str]] = set()
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            handler = MSG_RE.search(body)
            if not handler:
                continue
            owners = [n for n in binders.get(named.group(1), []) if n in live]
            if not owners:
                continue

            # call -> (idle points, busy points). A missing half stays -1, which is not the same as a
            # zero: `do_nothing` answers with nothing and is recorded as 0.
            found: dict[str, list[int]] = {}
            aims: dict[str, bool] = {}
            for branch in BRANCH_RE.finditer(handler.group(1)):
                conditions = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
                if not conditions:
                    continue
                kind = re.search(r"<message_type>(\d+)</message_type>", conditions.group(1))
                if not kind or kind.group(1) not in CALLS:
                    continue
                answer = rung(branch.group(1))
                if answer is None:
                    continue
                busy = "NPC_STATE_ATTACK" in conditions.group(1)
                slot = found.setdefault(kind.group(1), [-1, -1])
                if answer[1] >= 0:
                    slot[1 if busy else 0] = answer[1]
                aims[kind.group(1)] = aims.get(kind.group(1), False) or answer[2]

            for call, (idle, busy) in found.items():
                for npc in owners:
                    rows.add((int(npc), int(call), idle, busy,
                              1 if aims.get(call) else 0, named.group(1)))

    ordered = sorted(rows)
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\tcall\tidle_points\tbusy_points\taims_at_sender\tpattern\n")
        for row in ordered:
            out.write("\t".join(str(field) for field in row) + "\n")

    per_call = collections.Counter(r[1] for r in ordered)
    print(f"{len(ordered)} answers across {len({r[0] for r in ordered})} spawnable npcs -> {args.out}")
    for call, count in sorted(per_call.items()):
        print(f"    {count:5d} answer {call}")

    if args.gaps:
        templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
        bound = {int(m.group(1)): m.group(2)
                 for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
        # Only the player-targeted calls have a single class that answers them. `3000x` is answered by
        # several classes through `GuardAnswers.Answers`, so "not on class X" says nothing about it.
        answers = {"23000": "abyss_guard_call", "23100": "garrison_guard_answer",
                   "23200": "fortress_guard_answer"}
        for call in sorted(c for c in per_call if str(c) in answers):
            deaf = collections.Counter(bound.get(r[0], "<none>") for r in ordered
                                       if r[1] == call and bound.get(r[0]) != answers[str(call)])
            print(f"\n  {call}: {sum(deaf.values())} answer in retail and are not on {answers[str(call)]}")
            for name, count in deaf.most_common(10):
                print(f"      {count:5d}  {name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
