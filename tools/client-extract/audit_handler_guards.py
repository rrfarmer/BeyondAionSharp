"""Every guard we wrote, against the retail guard it came from.

`audit_handler_actions.py` compares what our branches *do*. This compares what lets them run, and that is
the larger half: **every "band, not a threshold" and "once, ever" finding in `retail-ai-fidelity.md` was
about a guard rather than an action.** A branch with retail's actions and the wrong guard fires at the
wrong time, as often as it likes, and no audit of actions can see it.

**The alarming column here is the opposite one.** For actions, a `dropped` kind is usually a skill we
cannot cast and already recorded; the danger is what we *added*. For guards it inverts:

  * **dropped** -- retail guards on something we do not. **A missing guard means the branch fires when
    retail would not have let it.** A dropped `flag` is a once-only step firing every tick; a dropped
    `chance` is a coin-toss that always lands; a dropped `hp` is a phase with no floor. **Read these
    first.**
  * **invented** -- we guard on something retail does not, so the branch fires *less* often than retail
    intends. Quieter, but it means a mechanic that never shows up.

Keyed the same way as the action audit: `[AIName]` -> `npc_templates.xml` -> `ai_binding.tsv` gives the
retail patterns a class actually serves, so nothing depends on what a file's remarks happen to mention.

**The condition table is derived from the data**, not written from memory -- the action audit lost eight
findings to a hand-written table that omitted `spawn_on_target`, and there are 41 distinct condition verbs
to get wrong here.

**Caveats, same as the action audit.** A class serving many patterns is compared against the union of
them, which is permissive. Kinds, not counts: a `hp` guard at 50 where retail guards at 35 is invisible
here, and so is a boundary where retail writes a threshold -- `audit_hp_phases.py` is the tool for that.

Usage:
    python audit_handler_guards.py <patterns_dir> <binding_tsv> [--repo ..] [--verdict dropped]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
from audit_handler_actions import HANDLERS, served_patterns  # noqa: E402

#: Retail condition verb -> kind. All 41 that appear as direct children of `<conditions>`.
#:
#: `set_flag_var` and `unset_flag_var` are *conditions* in retail, not actions: they test and mutate in
#: one step, which is what makes a branch once-only. `increase_intvar` and its family are the same shape
#: for counters. Both are the guards this port translates as `When.FirstTime` and `When.Decrement`.
RETAIL_GUARD = {
    "is_battle_timer_indicator": "timer",
    "set_flag_var": "flag",
    "unset_flag_var": "flag",
    "set_world_flag_var": "flag",
    "unset_world_flag_var": "flag",
    "is_world_flag_var": "flag",
    "is_hp_in_boundary": "hp",
    "is_hp_lower_than": "hp",
    "test_probability": "chance",
    "is_message": "message",
    "is_race": "race",
    "is_npc_state": "state",
    "is_user": "user",
    "is_npc": "user",
    "is_tribe": "tribe",
    "is_enemy": "enemy",
    "is_my_curent_target": "enemy",
    "is_skill_count_left": "skillcount",
    "is_user_flying": "flying",
    "is_distance_longer_than": "distance",
    "is_distance_shorter_than": "distance",
    "is_waypoint_index": "waypoint",
    "is_last_waypoint": "waypoint",
    "is_event_skill_id": "eventskill",
    "is_event_skill_category": "eventskill",
    "is_abnormal_state": "abnormal",
    "is_in_abnormal_state": "abnormal",
    "is_obj_in_abnormal_state": "abnormal",
    "is_user_class": "class",
    "is_user_level": "level",
    "is_user_gender": "gender",
    "is_hyperlink_id": "hyperlink",
    "is_target_quest_state": "quest",
    "has_attack_damage_flag": "damageflag",
    "is_on_time": "time",
    "increase_intvar": "counter",
    "decrease_intvar": "counter",
    "add_intvar": "counter",
    "sub_intvar": "counter",
    "set_intvar_if_less_than": "counter",
    "set_intvar_if_larger_than": "counter",
}

#: Our guard -> the same kinds. Longest prefix first, so `HpBetween` is not read as `Hp`.
#:
#: **Enumerated from `AiPattern.When`, not written from memory.** The first version omitted the bare
#: `When.Enemy` and reported the zombie trap as having dropped retail's `is_enemy` -- from a class whose
#: own remarks say "Not translated: nothing. This pattern is complete", and which was right. That is the
#: third table in this directory to lose findings to being hand-listed; the rule is now to derive every
#: one of them.
OUR_GUARD = [
    ("HpBetween", "hp"),
    ("HpBelow", "hp"),
    ("TargetHpBetween", "hp"),
    ("FriendHpBelow", "hp"),
    ("FirstTime", "flag"),
    ("Consuming", "flag"),
    ("Timer", "timer"),
    ("Chance", "chance"),
    ("Message", "message"),
    ("SenderIs", "message"),
    ("MessageParamIsEnemy", "enemy"),
    ("CasterIsEnemy", "enemy"),
    ("FriendsAttackerIsEnemy", "enemy"),
    ("TargetIsEnemy", "enemy"),
    ("Enemy", "enemy"),
    ("MessageParamFartherThan", "distance"),
    ("TargetWithin", "distance"),
    # Added to the engine for the silikor akaimum's near answer and never added here, so the two branches
    # that use it kept reporting retail's distance guard as dropped. A vocabulary that lags the engine
    # produces exactly the same false positive as one that is wrong.
    ("SenderWithin", "distance"),
    ("Idle", "state"),
    ("Fighting", "state"),
    ("Decrement", "counter"),
    ("CountEquals", "counter"),
    ("CountAbove", "counter"),
    ("CountBelow", "counter"),
    ("AttackerRace", "race"),
    ("CasterRace", "race"),
    ("SeenRace", "race"),
    ("TargetRace", "race"),
    ("AttackerClass", "class"),
]


def retail_guard_kinds(patterns_dir: pathlib.Path) -> dict[tuple[str, str, str], set[str]]:
    """(pattern, retail handler, priority) -> kinds of guard THAT BRANCH carries.

    **Keyed on the branch, not the handler.** Comparing whole handlers was the first version and it
    reported 92 dropped guards, most of them an artifact of how this port translates: we deliberately
    leave out retail's skill-only branches, and once a handler is compared as one bag their guards look
    dropped. Retail numbers every branch with an explicit `<priority>`, and this log has preserved those
    numbers in `Branch(priority, ...)` from the start -- so the branches line up one to one, and a
    dropped guard means a guard missing from *the same branch*.
    """
    out: dict[tuple[str, str, str], set[str]] = collections.defaultdict(set)
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
                    key = (name.group(1), handler.group(1), priority.group(1))
                    out[key]
                    for verb, kind in RETAIL_GUARD.items():
                        if f"<{verb}>" in flat or f"<{verb}/>" in flat:
                            out[key].add(kind)
    return out


def our_guard_kinds(text: str) -> dict[str, dict[tuple[str, str], set[str]]]:
    """AI name -> (handler, priority) -> kinds of guard, for one source file."""
    out: dict[str, dict[tuple[str, str], set[str]]] = {}
    parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
    for i in range(1, len(parts), 2):
        name, body = parts[i], parts[i + 1]
        per: dict[tuple[str, str], set[str]] = {}
        for handler in HANDLERS:
            hit = re.search(rf"\b{handler}\s*=\s*(.*?)(?:\n\s*On[A-Z]\w*\s*=|\n\s*\}};)", body, re.S)
            if not hit:
                continue
            # Each Branch(priority, ...) separately, so the comparison is branch to branch.
            for branch in re.finditer(r"Branch\(\s*(\d+)\s*,(.*?)(?=Branch\(|\Z)",
                                      hit.group(1), re.S):
                kinds: set[str] = set()
                for guard in re.findall(r"When\.(\w+)", branch.group(2)):
                    # Longest prefix wins. OUR_GUARD is written in reading order, which put ("Message",
                    # "message") ahead of ("MessageParamIsEnemy", "enemy") -- so every
                    # When.MessageParamIsEnemy was classified as a message guard and its enemy guard
                    # reported as dropped. That was eight of the twenty-three "ready" rows, all of them
                    # already applied in the source.
                    for prefix, kind in sorted(OUR_GUARD, key=lambda pk: -len(pk[0])):
                        if guard.startswith(prefix):
                            kinds.add(kind)
                            break
                per[(handler, branch.group(1))] = kinds
        if per:
            out[name] = per
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--verdict", choices=["dropped", "invented", "all"], default="all")
    #: Kinds this port cannot express at all, recorded throughout retail-ai-fidelity.md. Dropping one of
    #: these is already known, so it is separated out rather than repeated 200 times.
    ap.add_argument("--blocked", default="skillcount,flying,class,race,tribe,waypoint,eventskill,"
                                        "abnormal,level,gender,hyperlink,quest,damageflag,time,user")
    args = ap.parse_args()

    blocked = {k for k in args.blocked.split(",") if k}
    repo = pathlib.Path(args.repo)
    retail = retail_guard_kinds(pathlib.Path(args.patterns_dir))
    serves = served_patterns(repo, pathlib.Path(args.binding_tsv))

    findings: list[tuple[str, str, str, str, str]] = []
    compared = 0
    blocked_hits: collections.Counter = collections.Counter()
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for ai_name, per_handler in our_guard_kinds(text).items():
            patterns = serves.get(ai_name, set())
            if not patterns:
                continue
            for (handler, priority), ours in per_handler.items():
                theirs: set[str] = set()
                found = False
                for pattern in patterns:
                    for retail_handler in HANDLERS[handler]:
                        hit = retail.get((pattern, retail_handler, priority))
                        if hit is not None:
                            theirs |= hit
                            found = True
                if not found:
                    continue
                compared += 1
                dropped = theirs - ours
                for kind in sorted(dropped & blocked):
                    blocked_hits[kind] += 1
                dropped -= blocked
                invented = ours - theirs
                if dropped:
                    findings.append((path.name, ai_name, f"{handler}#{priority}", "dropped",
                                     "/".join(sorted(dropped))))
                if invented:
                    findings.append((path.name, ai_name, f"{handler}#{priority}", "invented",
                                     "/".join(sorted(invented))))

    findings.sort(key=lambda f: (f[3] != "dropped", f[0], f[2]))
    print(f"{compared} branches compared against the retail patterns their npcs actually run\n")
    print(f"{'file':<30} {'ai name':<28} {'handler':<16} {'verdict':<9} kinds")
    for name, ai_name, handler, verdict, kinds in findings:
        if args.verdict != "all" and verdict != args.verdict:
            continue
        print(f"{name:<30} {ai_name:<28} {handler:<16} {verdict:<9} {kinds}")
    counts = collections.Counter(f[3] for f in findings)
    print()
    print(f"  {counts['dropped']:3d} dropped, {counts['invented']:3d} invented, "
          f"{compared - len(findings):3d} clean")
    if blocked_hits:
        print()
        print("  guards this port cannot express, set aside (already recorded):")
        for kind, n in blocked_hits.most_common():
            print(f"    {n:4d} {kind}")
    print()
    print("A 'dropped' row is a branch that can fire where retail would not have let it. That is the")
    print("dangerous direction for a guard, and the opposite of the action audit's.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
