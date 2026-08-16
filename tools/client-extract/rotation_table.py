"""Print a boss's battle-timer rotation as one row per branch.

`summarize_pattern.py` shows a pattern faithfully, which for a big boss is still
hundreds of lines. When the boss is a timer chain -- and most of the un-ported
ones are -- what you actually need in order to write the table is much narrower:
which timer each branch answers, which it arms next, after how long, and what it
does. That fits on one line.

Tiamat's dying phase is 45 branches across four health regimes and reads as a
wall of XML; as a table it is obviously a left/middle/right breath rotation that
grows extra steps as she weakens, and can be transcribed into a `PatternAi` table
directly.

Columns: the branch's own comment, the health regime guarding it, the timer that
triggers it, the timer it arms and the delay, then the spawn and skill it uses.

CLI:
    python rotation_table.py <patterns_dir> <pattern_name>
"""
from __future__ import annotations

import argparse
import pathlib
import sys
import xml.etree.ElementTree as ET

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text
from summarize_pattern import lowercase_tags

TIMER_PREFIX = "BTIMERI_INDEX_"


def regime_of(conditions: ET.Element | None) -> str:
    """The health band guarding this branch, in the terms the pattern states it."""
    if conditions is None:
        return ""
    for op in conditions:
        if op.tag == "is_hp_in_boundary":
            return f"{op.findtext('larger_than')}-{op.findtext('less_than')}"
        if op.tag == "is_hp_lower_than":
            return f"<{op.findtext('percent')}"
    return ""


def fired_timer(conditions: ET.Element | None) -> str:
    if conditions is None:
        return ""
    for op in conditions:
        if op.tag == "is_battle_timer_indicator":
            return (op.findtext("btimer_indicator") or "").replace(TIMER_PREFIX, "T")
    return ""


def actions_of(actions: ET.Element | None) -> tuple[str, str, str, str]:
    """(timer armed, delay, everything it spawns, what it casts).

    Every spawn, not the last one: a branch that drops four hazards at four points is common, and
    keeping only one of them silently halves the mechanic. Vanuka Infernus was nearly ported off a
    table that showed one flame center per branch where the pattern has up to four.
    """
    arms = delay = skill = ""
    spawned: list[str] = []
    for op in actions if actions is not None else []:
        if op.tag == "add_battle_timer":
            arms = (op.findtext("btimer_indicator") or "").replace(TIMER_PREFIX, "T")
            delay = op.findtext("delay") or ""
        elif op.tag.startswith("spawn"):
            name = (op.findtext("npc_nameid") or "").strip()
            where = ""
            if (op.findtext("spawn_location_type") or "").strip() == "SPAWN_LOCATION_ABSOLUTE":
                where = f"@{op.findtext('x')},{op.findtext('y')}"
            facing = op.findtext("dir") or "0"
            spawned.append(name + where + (f" dir={facing}" if facing not in ("0", "") else ""))
        elif op.tag.startswith("use_skill"):
            skill = (op.findtext("skill") or "").replace("SKILLI_INDEX_", "idx")
    return arms, delay, " + ".join(spawned), skill


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("pattern_name")
    args = ap.parse_args()

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            m = NAME_RE.search(block.group(1))
            if not m or m.group(1) != args.pattern_name:
                continue

            root = ET.fromstring(f"<r>{lowercase_tags(block.group(1))}</r>")
            timers = root.find("event_handlers/on_battle_timer")
            if timers is None:
                raise SystemExit(f"{args.pattern_name} has no on_battle_timer")

            print(f"# {args.pattern_name}  ({path.name})")
            print(f"{'branch':<20} {'regime':<10} {'on':<5} {'arms':<5} {'delay':>6}  spawns / casts")
            for branch in sorted(timers.findall("pattern"),
                                 key=lambda b: -int(b.findtext("priority", "0").strip())):
                arms, delay, spawns, skill = actions_of(branch.find("actions"))
                print(f"{(branch.findtext('comment') or '').strip():<20} "
                      f"{regime_of(branch.find('conditions')):<10} "
                      f"{fired_timer(branch.find('conditions')):<5} {arms:<5} {delay:>6}  "
                      f"{spawns}{'  ' + skill if skill else ''}")
            return

    raise SystemExit(f"pattern {args.pattern_name!r} not found under {args.patterns_dir}")


if __name__ == "__main__":
    main()
