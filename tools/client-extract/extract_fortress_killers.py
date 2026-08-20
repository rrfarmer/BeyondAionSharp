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
The battle-timer ladder. Every killer has one and it is almost entirely `use_skill`; the one translatable
rung on it adds 200,000 hate to a *current target* that is a guardian chief, and it sits behind a race
guard, so the timer walk used for 30002 refuses it by design. Recorded in the docs, not here.

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
            for npc_id in binders.get(named.group(1), []):
                rows.append((int(npc_id), reach, "true" if walks(handlers) else "false",
                             hunt[0] if hunt else 0,
                             "|".join(hunt[1]) if hunt else "",
                             named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\twake_range\twalks\tsight_hate\tsight_races\tpattern\n")
        for row in rows:
            out.write("\t".join(str(field) for field in row) + "\n")

    hunters = sum(1 for r in rows if r[3])
    walkers = sum(1 for r in rows if r[2] == "true")
    print(f"{len(rows)} fortress killers -> {args.out}")
    print(f"    {walkers} walk their route, {hunters} hunt a garrison chief on sight")
    return 0


if __name__ == "__main__":
    sys.exit(main())
