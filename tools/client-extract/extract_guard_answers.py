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
Every npc this port defines whose retail pattern answers one of these calls:

| call | answerers |
|---|---|
| `23000` | 2,451 |
| `23100` | 766 |
| `23200` | 282 |
| `30001` | 698 |
| `30002` | 33 |
| `30003` | 12 |

**All 3,499 player-targeted answers now reach the npc**, by one of four routes -- an inherited listener,
a hand-off, `AnswerCall` applied directly, or rungs folded into a class's own pattern. `--gaps` checks
that rather than asserting a single expected class, because an earlier version did the latter and
reported a 717-npc shortfall on `23100` of which 512 were already answering through the fold.

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
#: not to bind new listeners but to **bound** the ones we have. Two classes answered `30001` for every
#: npc they held -- `AbstractSiegeProtectorAI` and `BaseProtectorAI` -- where retail names a subset, so
#: protectors it leaves at their posts charged every waking killer. `30003` is carried for membership
#: only: its answer is `despawn_self`, not a hate rung, so it has no points and emits no rung. It is
#: what shows that the advance village killer answers `30002` and not `30003`, which its class had
#: given it anyway.
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
    print(f"{len(ordered)} answers across {len({r[0] for r in ordered})} npcs -> {args.out}")
    for call, count in sorted(per_call.items()):
        print(f"    {count:5d} answer {call}")

    if args.gaps:
        report_gaps(args.repo, ordered)
    return 0


def delivering_ai_names(repo: pathlib.Path) -> tuple[set[str], set[str]]:
    """AI names that can deliver a table answer, and the ones that swallow it.

    **Not "is it bound to the answering class".** An earlier version of this report asked exactly that
    and claimed a 717-npc shortfall on `23100`, of which 512 were `artifact_protector` -- a class that
    answers perfectly well, through rungs folded into `ProtectorCalls`. Four ways an answer reaches an
    NPC now, and a report that knows only one of them invents work that is already done.

    A class delivers if it does **not** declare `OnNpcMessage` at all (it inherits `GeneralNpcAI`'s,
    which consults the table), or hands off to the inherited one, or applies the rungs itself through
    `AnswerCall`, or folds them into its pattern. Anything else declares the method and swallows every
    message the table holds -- which is the defect this project hit three times.
    """
    delivers: set[str] = set()
    swallows: set[str] = set()
    for path in (repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        named = re.findall(r'AIName\("([\w_]+)"\)', text)
        if not named:
            continue
        reaches = ("void OnNpcMessage" not in text
                   or "base.OnNpcMessage" in text
                   or "GuardAnswers.AnswerCall" in text
                   or "GuardAnswers.RungsFor" in text
                   or "ProtectorCalls.PatternFor" in text)
        (delivers if reaches else swallows).update(named)
    return delivers, swallows


def report_gaps(repo: pathlib.Path, ordered) -> None:
    templates = A.read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    bound = {int(m.group(1)): m.group(2)
             for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    delivers, swallows = delivering_ai_names(repo)

    # `3000x` names the caller and is answered by class-owned actions gated on `GuardAnswers.Answers`,
    # so "can this class deliver a rung" is not the question for it and it is reported separately.
    deaf: collections.Counter = collections.Counter()
    covered = 0
    for npc_id, call, _idle, _busy, _sender, _pattern in ordered:
        if call >= 30000:
            continue
        name = bound.get(npc_id, "<none>")
        if name in delivers:
            covered += 1
        else:
            deaf[(call, name)] += 1

    print(f"\n  player-targeted answers: {covered} reach the npc, {sum(deaf.values())} do not")
    for (call, name), count in deaf.most_common(12):
        print(f"      {count:5d}  {call} on {name}")
    if not deaf:
        print("      -- every npc whose retail pattern answers a guard call is on a class that can")
    print(f"\n  {len(swallows)} AI names declare OnNpcMessage and swallow it: "
          + ", ".join(sorted(swallows)))


if __name__ == "__main__":
    sys.exit(main())
