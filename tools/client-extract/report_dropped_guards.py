"""What each dropped guard actually says in retail, and whether it is safe to apply.

`audit_handler_guards.py` says *that* a guard is missing. Acting on it needs two more things, and the
`chance` pass learned both the hard way:

  * **the retail text** -- the percentage, the threshold, the flag slot. Without it a fix is a guess, and
    the two kerubiel answers differ (80 and 50) where a guess would have flattened them.
  * **whether it is uniform across the patterns the class serves.** One AI class can serve dozens of
    npcs, and a guard may belong to only some. `tiamats_incarnation` serves seven incarnations and one
    carries the roll; `abyss_guard_call` serves forty-three and one does. Applying either to the class
    would have been a regression for the rest, and both were caught only after a pin broke.

So this prints, per dropped guard: the retail condition verbs with their values, how many served patterns
carry that guard, and how many have the same branch without it.

**Read the `MIXED` rows as blocked, not as work.** They need the class split per npc first.

**And `UNIFORM` is not the same as safe to apply blind.** The whole comparison keys on branch priority,
which holds only where this port preserved retail's numbering -- most of the log does, and some of it does
not. `RatmanCampAI` numbers its two branches 2 and 1 while retail puts the broadcast at 4 and a skill-only
step at 2, so the row that reads "OnAttacked#2 wants an hp guard" is describing a different branch from
the one it would be applied to. **Check that our branch and retail's branch at that priority are the same
step before touching anything.** A whole batch of nineteen was applied and reverted for want of that
check.

**Two known false-positive shapes**, both found by applying rows and watching pins go red:

  * **a guard wrapped in a local helper is invisible.** The scan looks for `When.*`, so
    `SuspiciousCoffinAI`'s `Hears(slot)` -- which tests `ai.CurrentMessage` and is exactly the message
    guard -- reads as five dropped `message` guards. Any file with its own condition helper will do the
    same.
  * **a retail handler with an idle branch and an attack branch** collapses onto one branch of ours. The
    alignment check cannot separate them: both carry `hate`, so both look like the same step. Four
    `state` rows on the Anuhart pets and subordinates were applied, silenced the encounter, and were
    reverted.

Usage:
    python report_dropped_guards.py <patterns_dir> <binding_tsv> [--repo ..] [--kind flag]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_handler_guards as G  # noqa: E402
import audit_handler_actions as H  # noqa: E402
from audit_invented_actions import OUR_KIND, RETAIL_KIND  # noqa: E402

#: The verbs that make up each kind, so a row can show what retail actually wrote.
KIND_VERBS = collections.defaultdict(set)
for _verb, _kind in G.RETAIL_GUARD.items():
    KIND_VERBS[_kind].add(_verb)


def branch_conditions(patterns_dir: pathlib.Path) -> dict[tuple[str, str, str], str]:
    """(pattern, handler, priority) -> the flattened <conditions> text of that branch."""
    out: dict[tuple[str, str, str], str] = {}
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = match.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if not name:
                continue
            for handler in re.finditer(r"<(on_\w+)>(.*?)</\1>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(2), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    priority = re.search(r"<priority>(\d+)<", flat)
                    if not priority:
                        continue
                    conditions = re.search(r"<conditions>(.*?)</conditions>", flat)
                    out[(name.group(1), handler.group(1), priority.group(1))] = \
                        conditions.group(1) if conditions else ""
    return out


def branch_actions(patterns_dir: pathlib.Path) -> dict[tuple[str, str, str], set[str]]:
    """(pattern, handler, priority) -> kinds of action THAT BRANCH carries."""
    out: dict[tuple[str, str, str], set[str]] = {}
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = match.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if not name:
                continue
            for handler in re.finditer(r"<(on_\w+)>(.*?)</\1>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(2), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    priority = re.search(r"<priority>(\d+)<", flat)
                    if not priority:
                        continue
                    kinds = {kind for verb, kind in RETAIL_KIND.items() if f"<{verb}>" in flat}
                    out[(name.group(1), handler.group(1), priority.group(1))] = kinds
    return out


def our_branch_actions(text: str) -> dict[str, dict[tuple[str, str], set[str]]]:
    """AI name -> (handler, priority) -> kinds of action, mirroring `our_guard_kinds`."""
    out: dict[str, dict[tuple[str, str], set[str]]] = {}
    parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
    for i in range(1, len(parts), 2):
        name, body = parts[i], parts[i + 1]
        per: dict[tuple[str, str], set[str]] = {}
        for handler in G.HANDLERS:
            hit = re.search(rf"\b{handler}\s*=\s*(.*?)(?:\n\s*On[A-Z]\w*\s*=|\n\s*\}};)", body, re.S)
            if not hit:
                continue
            for branch in re.finditer(r"Branch\(\s*(\d+)\s*,(.*?)(?=Branch\(|\Z)", hit.group(1), re.S):
                kinds: set[str] = set()
                for action in re.findall(r"Do\.(\w+)", branch.group(2)):
                    for prefix, kind in OUR_KIND:
                        if action.startswith(prefix):
                            kinds.add(kind)
                            break
                per[(handler, branch.group(1))] = kinds
        if per:
            out[name] = per
    return out


def aligned(ours: set[str], theirs: set[str]) -> bool:
    """Whether our branch and retail's at the same priority are plausibly the same step.

    Permissive on the two known substitutions -- a `spawn` or `despawn` of ours standing in for a
    retail `skill` -- and on retail actions we drop entirely, which is nearly always a skill. What it
    refuses is the case that bit: **no overlap at all**, which is how a branch numbered 2 here and a
    different branch numbered 2 there show up.
    """
    if not ours or not theirs:
        return True
    if ours & theirs:
        return True
    return bool(ours & {"spawn", "despawn"} and "skill" in theirs)


def describe(conditions: str, kind: str) -> str:
    """The retail text for one kind, verbs and values, short enough to read in a table."""
    bits: list[str] = []
    for verb in sorted(KIND_VERBS[kind]):
        for hit in re.finditer(rf"<{verb}>(.*?)</{verb}>", conditions):
            values = re.findall(r"<\w+>([^<]+)</\w+>", hit.group(1))
            bits.append(f"{verb}({','.join(values)})" if values else verb)
        if f"<{verb}/>" in conditions or f"<{verb}></{verb}>" in conditions:
            bits.append(verb)
    return "; ".join(dict.fromkeys(bits)) or "?"


def hp_range(text: str):
    """The health window a snippet of our source allows, or None if it names no health guard."""
    between = re.search(r"When\.HpBetween\((\d+),\s*(\d+)\)", text)
    if between:
        return int(between.group(1)), int(between.group(2))
    below = re.search(r"When\.HpBelow\((\d+)\)", text)
    if below:
        return 0, int(below.group(1)) - 1
    return None


def contradicts(proposed, ours):
    """True when the two windows cannot both hold, so the branches cannot be the same step."""
    if proposed is None or ours is None:
        return False
    return proposed[1] < ours[0] or ours[1] < proposed[0]


def our_branch_text(source: str, ai_name: str, handler: str, priority: str) -> str:
    """The source of one Branch, plus any branch that arms the timer it waits on.

    **The arming branch matters as much as the branch itself.** The silikor guard's peel repeat carries no
    health guard of its own; it waits on a timer armed by a step gated `HpBelow(30)`, so a proposed
    `HpBetween(31,100)` on the repeat can never hold. Reading the branch alone misses that entirely.
    """
    start = source.find(f'[AIName("{ai_name}")]')
    if start < 0:
        return ""
    end = source.find("[AIName(", start + 1)
    scope = source[start:end if end > 0 else len(source)]
    hit = re.search(rf"Branch\(\s*{priority}\s*,(.*?)(?=Branch\(|\Z)", scope, re.S)
    if not hit:
        return ""
    text = hit.group(1)
    waits = re.search(r"When\.Timer\((\w+)\)", text)
    if waits:
        for arming in re.finditer(rf"Branch\(.*?Do\.ArmTimer\(\s*{waits.group(1)}\s*,", scope, re.S):
            text += scope[max(0, arming.start()):arming.end()]
    return text


#: The silikor guard's shape, kept as fixture data because no live row exercises the contradiction check.
#:
#: Its peel repeat carries no health guard of its own: it waits on a timer armed by an opening step gated
#: `HpBelow(30)`. Retail's branch at that priority wants `is_hp_in_boundary(31,100)`, which can never hold
#: at the same time -- so the two are not the same step and the row must be refused. The live row is now
#: blocked one stage earlier as `MIXED`, which is why this fixture exists at all.
SELFTEST_SOURCE = '''
[AIName("silikor_guard")]
public class SilikorGuardAI : PatternAi
{
    OnBattleTimer = Of(
        Branch(6, "below 30 opens the peel", [When.Timer(Heartbeat), When.HpBelow(30), When.FirstTime(Below30)],
            Do.ArmTimer(Heartbeat, 5000),
            Do.ArmTimer(Low, 23000),
            Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

        Branch(4, "and keeps peeling", [When.Timer(Low)],
            Do.ArmTimer(Low, 15000),
            Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)));
}
'''


def selftest() -> int:
    """Pins `contradicts` and `our_branch_text`, which nothing on the live board reaches."""
    failures = []

    def check(name, got, want):
        if got != want:
            failures.append(f"{name}: got {got!r}, wanted {want!r}")

    check("hp_range reads a boundary", hp_range("When.HpBetween(31, 100)"), (31, 100))
    check("hp_range reads a threshold", hp_range("When.HpBelow(30)"), (0, 29))
    check("hp_range on no guard", hp_range("When.Timer(Low)"), None)

    check("disjoint windows contradict", contradicts((31, 100), (0, 29)), True)
    check("overlapping windows do not", contradicts((31, 100), (20, 40)), False)
    check("an absent guard never contradicts", contradicts((31, 100), None), False)

    # The branch itself carries no health guard; the step that arms its timer does.
    branch = our_branch_text(SELFTEST_SOURCE, "silikor_guard", "OnBattleTimer", "4")
    check("the arming step is pulled in", hp_range(branch), (0, 29))
    check("so the row is refused", contradicts((31, 100), hp_range(branch)), True)

    # And a branch whose arming step agrees is not refused.
    check("an agreeing pair survives", contradicts((0, 51), hp_range(branch)), False)

    for line in failures:
        print("  FAIL", line)
    print(f"selftest: {'FAILED' if failures else 'ok'}, {len(failures)} failure(s)")
    return 1 if failures else 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", nargs="?")
    ap.add_argument("binding_tsv", nargs="?")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--kind", default=None, help="only this guard kind")
    ap.add_argument("--selftest", action="store_true",
                    help="pin the contradiction check, which no live row exercises")
    args = ap.parse_args()

    if args.selftest:
        return selftest()

    repo = pathlib.Path(args.repo)
    patterns_dir = pathlib.Path(args.patterns_dir)
    retail = G.retail_guard_kinds(patterns_dir)
    conditions = branch_conditions(patterns_dir)
    retail_actions = branch_actions(patterns_dir)
    serves = H.served_patterns(repo, pathlib.Path(args.binding_tsv))
    blocked = {"skillcount", "flying", "class", "race", "tribe", "waypoint", "eventskill",
               "abnormal", "level", "gender", "hyperlink", "quest", "damageflag", "time", "user"}

    rows: list[tuple[str, str, str, str, str, str, int, int, bool]] = []
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        our_actions = our_branch_actions(text)
        for ai_name, per_branch in G.our_guard_kinds(text).items():
            patterns = serves.get(ai_name, set())
            if not patterns:
                continue
            for (handler, priority), ours in per_branch.items():
                theirs: set[str] = set()
                present: dict[str, list[str]] = collections.defaultdict(list)
                absent: dict[str, list[str]] = collections.defaultdict(list)
                for pattern in patterns:
                    for retail_handler in G.HANDLERS[handler]:
                        hit = retail.get((pattern, retail_handler, priority))
                        if hit is None:
                            continue
                        theirs |= hit
                        for kind in set(hit) | (theirs - hit):
                            (present if kind in hit else absent)[kind].append(pattern)
                for kind in sorted(theirs - ours - blocked):
                    if args.kind and kind != args.kind:
                        continue
                    detail = "?"
                    for pattern in present.get(kind, []):
                        for retail_handler in G.HANDLERS[handler]:
                            text_of = conditions.get((pattern, retail_handler, priority))
                            if text_of:
                                detail = describe(text_of, kind)
                                break
                        if detail != "?":
                            break
                    ours_do = our_actions.get(ai_name, {}).get((handler, priority), set())
                    clash = kind == "hp" and contradicts(
                        hp_range(detail.replace("is_hp_in_boundary(OBJI_SELF,", "When.HpBetween(")
                                      .replace("is_hp_lower_than(OBJI_SELF,", "When.HpBelow(")),
                        hp_range(our_branch_text(text, ai_name, handler, priority)))
                    ok = not clash and all(aligned(ours_do,
                                     retail_actions.get((pattern, rh, priority), set()))
                             for pattern in present.get(kind, [])
                             for rh in G.HANDLERS[handler]
                             if (pattern, rh, priority) in retail_actions)
                    rows.append((path.name, ai_name, handler, priority, kind, detail,
                                 len(present.get(kind, [])), len(absent.get(kind, [])), ok))

    rows.sort(key=lambda r: (r[7] > 0 or not r[8], r[4], r[0]))
    print(f"{len(rows)} dropped guards with a readable retail condition\n")
    print(f"{'file':<28} {'ai name':<24} {'branch':<18} {'kind':<9} {'safe':<7} retail")
    for name, ai_name, handler, priority, kind, detail, has, hasnt, ok in rows:
        verdict = ("MISALIGNED" if not ok
                   else "UNIFORM" if hasnt == 0 else f"MIXED{has}/{has + hasnt}")
        print(f"{name:<26} {ai_name:<22} {handler + '#' + priority:<17} {kind:<8} {verdict:<10} {detail}")
    mixed = sum(1 for r in rows if r[7] and r[8])
    bad = sum(1 for r in rows if not r[8])
    print()
    print(f"  {len(rows) - mixed - bad} ready, {mixed} mixed and blocked on splitting the class, "
          f"{bad} misaligned")
    print()
    print("MISALIGNED means our branch at that priority and retail's do different things, so the number")
    print("is a coincidence and the guard belongs to some other branch. Those need reading by hand.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
