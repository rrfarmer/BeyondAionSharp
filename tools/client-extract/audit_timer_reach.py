"""Find pattern payload that cannot run because the timer carrying it is never armed.

The audits so far look at one branch at a time. That is enough for a spawn placed by a
waypoint arrival -- `audit_missing_adds.py` already reports those as
`[BLOCKED: only a waypoint arrival spawns it]` -- but it is not enough for the shape this
audit exists for, which is one level removed and invisible to every earlier check:

    on_enter_attack_state    goto_waypoint 2
    on_arrived_at_waypoint   index 2 -> goto_waypoint 4
    on_arrived_at_waypoint   index 4 -> add_battle_timer 0, add_battle_timer 1
    on_battle_timer          timer 0, below 80 -> spawn two statues
    on_battle_timer          timer 0, below 50 -> spawn two more
    on_battle_timer          timer 1, below 50 -> spawn a hazard on the target

Every one of those spawn branches sits under `on_battle_timer`, which is an ordinary
reachable handler, so they all read as implementable. They are not: timers 0 and 1 are
armed **nowhere but the waypoint arrival**, and our runtime has no such event and our spawn
data gives these NPCs a single static spot. The whole ladder is dead, and it was ranked
third on the worth-doing list.

The method is a reachability fixpoint over timer indices:

  * every handler except the two in `UNREACHABLE_HANDLERS` can run, so the timers its branches
    arm are reachable;
  * an `on_battle_timer` branch runs only if the timer it is guarded on is reachable, and
    then the timers *it* arms become reachable too;
  * repeat until nothing changes.

Payload sitting in a branch guarded on an unreachable timer is dead. A battle-timer branch
with no `is_battle_timer_indicator` guard answers every timer, so it is dead only when no
timer is reachable at all.

Usage:
    python audit_timer_reach.py <patterns_dir> <binding_tsv> [--repo ..] [--min 1]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
import audit_translatable as T  # noqa: E402

# Handlers our runtime can never fire, because the action that raises them is one we cannot
# perform. Both are movement: an NPC that never walks a route never arrives at a waypoint, and an
# NPC that never wanders never stops wandering. Darkblade Ovanuka is the case that added the second
# -- two whole phases of his fight hang off timers that only `on_stop_to_random_move` arms.
UNREACHABLE_HANDLERS = {"on_arrived_at_waypoint", "on_stop_to_random_move"}

TIMER_RE = re.compile(r"BTIMERI_INDEX_(\d+)")


def timer_of(node: ET.Element | None) -> int | None:
    """The timer index named by an `is_battle_timer_indicator` or `add_battle_timer` node."""
    if node is None:
        return None
    text = node.findtext("btimer_indicator", "") or ""
    m = TIMER_RE.search(text)
    return int(m.group(1)) if m else None


def branch_facts(branch: ET.Element) -> tuple[int | None, list[int], int]:
    """(timer this branch is guarded on, timers it arms, how much payload it carries)."""
    conditions = branch.find("conditions")
    guard = None
    if conditions is not None:
        for node in conditions:
            if node.tag == "is_battle_timer_indicator":
                guard = timer_of(node)

    arms: list[int] = []
    payload = 0
    actions = branch.find("actions")
    if actions is not None:
        for node in actions:
            if node.tag == "add_battle_timer":
                armed = timer_of(node)
                if armed is not None:
                    arms.append(armed)
            elif node.tag in T.PAYLOAD:
                payload += 1

    return guard, arms, payload


def analyse(root: ET.Element) -> tuple[int, int, set[int]]:
    """(payload, dead payload, unreachable timers) for one pattern."""
    # `.//` because the caller may have wrapped the whole `<npc_ai_pattern>` element rather
    # than its body; both shapes reach the handlers this way.
    handlers = root.find(".//event_handlers")
    if handlers is None:
        return 0, 0, set()

    # (handler tag, guard timer, armed timers, payload) for every branch in the pattern.
    branches = [(event.tag, *branch_facts(b)) for event in handlers for b in event.findall("pattern")]

    every_timer = {g for _, g, _, _ in branches if g is not None}
    every_timer |= {t for _, _, arms, _ in branches for t in arms}

    reachable: set[int] = set()
    changed = True
    while changed:
        changed = False
        for tag, guard, arms, _ in branches:
            if tag in UNREACHABLE_HANDLERS:
                continue
            if tag == "on_battle_timer":
                # An unguarded battle-timer branch answers whichever timer fired, so it can
                # run as soon as any timer can.
                if guard is not None and guard not in reachable:
                    continue
                if guard is None and not reachable:
                    continue
            for armed in arms:
                if armed not in reachable:
                    reachable.add(armed)
                    changed = True

    dead = 0
    total = 0
    for tag, guard, _, payload in branches:
        total += payload
        if not payload or tag != "on_battle_timer":
            continue
        if (guard is not None and guard not in reachable) or (guard is None and not reachable):
            dead += payload

    return total, dead, every_timer - reachable


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--min", type=int, default=1, help="least dead payload worth listing")
    ap.add_argument("--all", action="store_true", help="include patterns we have already ported")
    args = ap.parse_args()

    live = A.spawnable_npc_ids(args.repo)
    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    for m in re.finditer(r"<npc_template[^>]*>", templates):
        block = m.group(0)
        npc = A.attr(block, "npc_id")
        ai_of[npc] = A.attr(block, "ai")
        name_of[npc] = A.attr(block, "name")

    binders: dict[str, list[str]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows = []
    dead_npcs = 0
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in S.PATTERN_RE.finditer(text):
            block = m.group(0)
            named = S.NAME_RE.search(block)
            if not named:
                continue
            pattern = named.group(1)

            owners = [n for n in binders.get(pattern, []) if n in live]
            unported = [n for n in owners if ai_of.get(n, "") in T.GENERIC_AI]
            if not args.all and not unported:
                continue

            try:
                root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(block)}</ai_pattern>")
            except ET.ParseError:
                continue

            total, dead, stranded = analyse(root)
            if dead < args.min:
                continue

            rows.append((dead, total, sorted(stranded), pattern, unported or owners))
            dead_npcs += len(unported or owners)

    rows.sort(key=lambda r: (-r[0], r[3]))
    print(f"{'dead':>4} {'of':>4}  {'timers':10} {'pattern':40} owners")
    for dead, total, stranded, pattern, owners in rows:
        who = ", ".join(f"{n} {name_of.get(n, '?')}" for n in owners[:3])
        if len(owners) > 3:
            who += f" (+{len(owners) - 3})"
        print(f"{dead:4} {total:4}  {','.join(map(str, stranded)):10} {pattern:40} {who}")
    print()
    print(f"{len(rows)} patterns carry payload on a timer nothing arms; "
          f"{sum(r[0] for r in rows)} actions across {dead_npcs} npcs.")


if __name__ == "__main__":
    main()
