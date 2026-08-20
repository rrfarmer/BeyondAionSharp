#!/usr/bin/env python3
"""What a fortress killer does besides answering messages: its wake call, its walk, and its quarry.

WHY THIS EXISTS
---------------
`FortressKillerAI` had the three-message loop and nothing else, on the assumption that the rest of the
killer patterns was skills. Reading them says otherwise, and says the killers are **not uniform**:

* the wake call is fifty metres for the artifact killers and **twenty** for `BLDF5_Village_Killer`;
* some carry `goto_waypoint` on waking and `goto_next_waypoint` on leaving a fight, and some never move;
* **`LDF4_Advance_Killer_43` hunts on sight.** Its `on_see_npc` rungs test the seen npc's race against
  `gchief_dragon`, `gchief_light` and `gchief_dark` and drop **a million hate** on it. That is what makes
  an Advance killer walk into a garrison and go for the chief rather than wander into players, and it is
  the largest killer family in the game with nineteen npcs.

Three constants in a class would have been wrong for two of the three, which is the same lesson the
30002 cadences taught: read it per pattern.

WHAT IT LEAVES ALONE
--------------------
The cast ladders. Every killer's battle timers interleave `use_skill` with the hand-offs and none of the
casts is translated -- but the **one** translatable rung on that ladder is read: it adds 200,000 hate to
a current target that is a guardian chief, so a killer already fighting keeps choosing the chief over
whatever else joins in. It sits behind a race guard, which the timer walk written for 30002 refuses by
design, so this file walks the chain itself through each timer's unguarded fallback rung.

Usage:  python extract_fortress_killers.py <patterns-dir> <ai_binding.tsv> <out.tsv>
"""
import argparse
import collections
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402
import summarize_pattern as S  # noqa: E402

WAKE_CALL = "30001"

#: Retail's race names for a garrison's chief, and this port's enum names for them.
CHIEF_RACES = {"gchief_light": "GCHIEF_LIGHT",
               "gchief_dark": "GCHIEF_DARK",
               "gchief_dragon": "GCHIEF_DRAGON"}


TIMER_RE = re.compile(r"BTIMERI_INDEX_(\d+)")


def timer_of(node):
    """The battle-timer index a condition or action names, or None."""
    found = TIMER_RE.search("".join(node.itertext())) if node is not None else None
    return int(found.group(1)) if found else None


def arms(branch):
    """Every (timer index, delay) this branch sets."""
    out = []
    for action in list(branch.find("actions") or []):
        if action.tag == "add_battle_timer":
            index = timer_of(action)
            delay = action.find("delay")
            if index is not None and delay is not None:
                out.append((index, int(delay.text)))
    return out


def branches(handlers, name):
    found = handlers.find(name)
    return sorted(found.findall("pattern"), key=lambda b: -int(b.findtext("priority", "0"))) \
        if found is not None else []


def wake_range(handlers):
    """The range this killer shouts 30001 at as it wakes, or 0 if it does not."""
    for branch in branches(handlers, "on_wake_up"):
        for action in list(branch.find("actions") or []):
            if action.tag != "broadcast_message":
                continue
            kind = action.find("message_type")
            if kind is not None and kind.text.strip() == WAKE_CALL:
                reach = action.find("range_as_meter")
                return int(reach.text) if reach is not None else 0
    return 0


def walks(handlers):
    """True when retail sends this killer along its route rather than leaving it where it spawned."""
    for name in ("on_wake_up", "on_leave_attack_state"):
        for branch in branches(handlers, name):
            for action in list(branch.find("actions") or []):
                if action.tag in ("goto_waypoint", "goto_next_waypoint"):
                    return True
    return False


def hunts_on_sight(handlers):
    """(hate, races) this killer drops on a garrison chief it sees, or None."""
    hate, races = 0, set()
    for branch in branches(handlers, "on_see_npc"):
        seen = None
        for condition in list(branch.find("conditions") or []):
            if condition.tag != "is_race":
                continue
            kind = condition.findtext("race_type", "").strip()
            if kind in CHIEF_RACES:
                seen = kind
        if seen is None:
            continue
        for action in list(branch.find("actions") or []):
            if action.tag == "add_hate_point" and "OBJI_SEEN" in "".join(action.itertext()):
                points = action.findtext("point_to_add")
                if points:
                    hate = max(hate, int(points))
                    races.add(CHIEF_RACES[seen])
    return (hate, sorted(races)) if races else None


def hate_rung(handlers):
    """(first delay, period, hate, races) for the timer rung that piles hate on a garrison chief.

    **This is the one translatable rung on a killer's battle-timer ladder**, and it is guarded by a race
    test, so the plain timer walk written for 30002 refuses it by design. Here the race guard is exactly
    what is being looked for, so it is allowed -- and the chain is followed through the *unguarded*
    fallback rung on each timer, which retail provides beside the guarded ones and which arms the same
    next timer with the same delay. Following the fallback and reading the guarded twin is what keeps
    "when does it come round" separate from "what does it do when it lands".
    """
    on_timer = handlers.find("on_battle_timer")
    entering = handlers.find("on_enter_attack_state")
    if on_timer is None or entering is None:
        return None

    ordered = sorted(on_timer.findall("pattern"), key=lambda b: -int(b.findtext("priority", "0")))
    chain, focus = {}, {}
    for branch in ordered:
        conditions = list(branch.find("conditions") or [])
        kinds = [c.tag for c in conditions]
        index = timer_of(branch.find("conditions")) if branch.find("conditions") is not None else None
        if index is None:
            continue
        if kinds == ["is_battle_timer_indicator"] and index not in chain:
            chain[index] = branch
        elif "is_race" in kinds:
            for condition in conditions:
                if condition.tag != "is_race":
                    continue
                race = condition.findtext("race_type", "").strip()
                if race not in CHIEF_RACES:
                    continue
                for action in list(branch.find("actions") or []):
                    if action.tag == "add_hate_point" and "OBJI_CUR_TARGET" in "".join(action.itertext()):
                        points = action.findtext("point_to_add")
                        if points:
                            hate, races, again = focus.get(index, (0, set(), 0))
                            # The rung may carry the loop itself: LDF4_Advance_Killer_43 has no
                            # unguarded fallback on its first timer, so every rung for it is race-
                            # guarded and each re-arms its own timer at five seconds. While the target
                            # is a chief it comes round every five; the moment it is not, nothing
                            # re-arms and the ladder stops. That is the behaviour, not a gap in it.
                            mine = [d for i, d in arms(branch) if i == index]
                            focus[index] = (max(hate, int(points)),
                                            races | {CHIEF_RACES[race]},
                                            max(again, mine[0] if mine else 0))
    if not focus:
        return None

    for branch in sorted(entering.findall("pattern"), key=lambda b: -int(b.findtext("priority", "0"))):
        if [c.tag for c in list(branch.find("conditions") or [])] not in ([], ["is_battle_timer_indicator"]):
            continue
        for start, first in arms(branch):
            elapsed, at, seen = first, start, set()
            while at is not None and at not in seen:
                seen.add(at)
                if at in focus:
                    hate, races, again = focus[at]
                    return elapsed, again or loop_length(chain, at), hate, sorted(races)
                branch_at = chain.get(at)
                if branch_at is None:
                    break
                following = arms(branch_at)
                if not following:
                    break
                at, delay = following[0]
                elapsed += delay
        break
    return None


def loop_length(chain, target):
    """Milliseconds from one visit to a timer back round to it."""
    total, at, seen = 0, target, set()
    while True:
        branch = chain.get(at)
        if branch is None:
            return 0
        following = arms(branch)
        if not following:
            return 0
        at, delay = following[0]
        total += delay
        if at == target:
            return total
        if at in seen:
            return 0
        seen.add(at)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    binders = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows = []
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            if f"<message_type>{WAKE_CALL}</message_type>" not in body:
                continue
            named = S.NAME_RE.search(body)
            if not named:
                continue
            try:
                root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(body)}</ai_pattern>")
            except ET.ParseError:
                continue
            handlers = root.find("event_handlers")
            if handlers is None:
                continue

            reach = wake_range(handlers)
            if not reach:
                continue                       # hears 30001, does not send it: not a killer

            hunt = hunts_on_sight(handlers)
            focus = hate_rung(handlers)
            for npc_id in binders.get(named.group(1), []):
                rows.append((int(npc_id), reach, "true" if walks(handlers) else "false",
                             hunt[0] if hunt else 0,
                             "|".join(hunt[1]) if hunt else "",
                             focus[0] if focus else 0,
                             focus[1] if focus else 0,
                             focus[2] if focus else 0,
                             "|".join(focus[3]) if focus else "",
                             named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\twake_range\twalks\tsight_hate\tsight_races\tpattern\n")
        for row in rows:
            out.write("\t".join(str(field) for field in row) + "\n")

    hunters = sum(1 for r in rows if r[3])
    walkers = sum(1 for r in rows if r[2] == "true")
    print(f"{len(rows)} fortress killers -> {args.out}")
    focused = sum(1 for r in rows if r[7])
    print(f"    {walkers} walk their route, {hunters} hunt a garrison chief on sight, "
          f"{focused} keep piling hate on one while they fight")
    return 0


if __name__ == "__main__":
    sys.exit(main())
