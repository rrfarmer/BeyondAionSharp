#!/usr/bin/env python3
"""When a siege protector calls its killer: retail's 30002, and the timer chain that reaches it.

WHY THIS EXISTS
---------------
Retail's siege fight is a three-message loop. **The middle message is the one nothing here sends.**

* **30001** — the killer wakes and every protector within fifty metres comes for it. Ported.
* **30002** — a protector calls the killer to *itself*, with `points_to_add=1000000`. **753 npcs
  broadcast this in retail and nothing in this port does.**
* **30003** — a protector dies and the killer hunting it stands down. Ported.

So the fight starts and can end, and the thing that moves it never happens: `FortressKillerAI` answers
30002 and that answer has never been reachable.

WHY IT NEEDS A GRAPH WALK
-------------------------
The broadcast is not on a handler. It hangs off a battle timer at the end of a chain, and the chain is
per pattern. For the artifact guards:

    on_enter_attack  ->  BT1 @7000
    BT1              ->  BT2 @8500
    BT2              ->  BT3 @6000
    BT3              ->  broadcast 30002, and BT1 @7500

which is **first call at 21.5 seconds, then every 22** — the delays are the whole of it once the
`use_skill` rungs alongside them are dropped, and they are dropped either way. But
`BGuard_ChiefA_Renew_Li` runs its chain on timer indices 6, 8 and 11-13 at 30 and 60 seconds, so a
two-number model fitted to the artifact guards is simply wrong elsewhere. The delays have to be read.

WHAT IT REFUSES TO GUESS
------------------------
**Only rungs whose sole condition is the timer are followed.** Retail guards many of them on health, on
`is_user_flying`, on a flag — and a chain that reaches the broadcast only through a health band does not
have one cadence, it has two. Those patterns are reported as unresolved and emit nothing, rather than
being flattened into a number that looks measured and is not.

For the same reason the walk takes the **highest-priority** rung for a timer index, which is retail's own
first-match-wins order, and stops if that rung is guarded.

Usage:  python extract_protector_calls.py <patterns-dir> <ai_binding.tsv> <out.tsv> [--report]
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

CALL = "30002"
TIMER_RE = re.compile(r"BTIMERI_INDEX_(\d+)")


def timer_of(node):
    """The battle-timer index a condition or action names, or None."""
    found = TIMER_RE.search("".join(node.itertext()))
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


def broadcasts_call(branch):
    """The range this branch broadcasts 30002 at, or None."""
    for action in list(branch.find("actions") or []):
        if action.tag != "broadcast_message":
            continue
        kind = action.find("message_type")
        if kind is not None and kind.text.strip() == CALL:
            reach = action.find("range_as_meter")
            return int(reach.text) if reach is not None else 0
    return None


def only_guard_is_the_timer(branch):
    """True when this branch's conditions are nothing but its battle-timer test."""
    conditions = branch.find("conditions")
    if conditions is None:
        return True
    kinds = [c.tag for c in conditions]
    return kinds == ["is_battle_timer_indicator"]


def chain(root):
    """(first delay, period, range) in milliseconds, or None when the chain cannot be read."""
    handlers = root.find("event_handlers")
    if handlers is None:
        return None

    # Seed: what entering combat arms -- and whether it also calls straight away.
    #
    # **The village chiefs broadcast in the enter-attack rung itself**, then again on a five-second
    # timer; retail's own comment on that rung says "repeat every 5 seconds". A walk that only followed
    # timers reported their first call at five seconds instead of zero, which is a whole beat of a
    # twelve-beat fight and was found by reading one pattern that looked too fast.
    seeds, opening = [], None
    entering = handlers.find("on_enter_attack_state")
    if entering is not None:
        for branch in sorted(entering.findall("pattern"),
                             key=lambda b: -int(b.findtext("priority", "0"))):
            if not only_guard_is_the_timer(branch):
                continue
            seeds = arms(branch)
            opening = broadcasts_call(branch)
            break
    if not seeds:
        return None

    # The highest-priority rung per timer index, which is the one retail runs.
    rungs = {}
    on_timer = handlers.find("on_battle_timer")
    if on_timer is None:
        return None
    for branch in sorted(on_timer.findall("pattern"),
                         key=lambda b: -int(b.findtext("priority", "0"))):
        conditions = branch.find("conditions")
        index = timer_of(conditions) if conditions is not None else None
        if index is not None and index not in rungs:
            rungs[index] = branch

    for start, first in seeds:
        elapsed, at, seen = first, start, set()
        while at is not None and at not in seen:
            seen.add(at)
            branch = rungs.get(at)
            if branch is None or not only_guard_is_the_timer(branch):
                break
            reach = broadcasts_call(branch)
            if reach is not None:
                # The period is what this rung re-arms, plus the way back round to it.
                period = loop_length(rungs, at)
                return (0 if opening is not None else elapsed), period, reach
            following = arms(branch)
            if not following:
                break
            at, delay = following[0]
            elapsed += delay
    return None


def loop_length(rungs, broadcasting):
    """Milliseconds from one broadcast to the next, walking back to the same rung."""
    total, at, seen = 0, broadcasting, set()
    while True:
        branch = rungs.get(at)
        if branch is None or not only_guard_is_the_timer(branch):
            return 0
        following = arms(branch)
        if not following:
            return 0
        at, delay = following[0]
        total += delay
        if at == broadcasting:
            return total
        if at in seen:
            return 0
        seen.add(at)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--report", action="store_true", help="name the patterns whose chain cannot be read")
    args = ap.parse_args()

    binders = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows, unresolved = [], []
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            if f"<message_type>{CALL}</message_type>" not in body:
                continue
            named = S.NAME_RE.search(body)
            if not named:
                continue
            try:
                root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(body)}</ai_pattern>")
            except ET.ParseError:
                continue
            handlers = root.find("event_handlers")
            if not any(broadcasts_call(b) is not None
                       for h in (list(handlers) if handlers is not None else [])
                       for b in h.findall("pattern")):
                continue                       # hears it, does not send it

            read = chain(root)
            if read is None:
                unresolved.append(named.group(1))
                continue
            first, period, reach = read
            for npc_id in binders.get(named.group(1), []):
                rows.append((int(npc_id), first, period, reach, named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\tfirst_ms\tperiod_ms\trange\tpattern\n")
        for npc_id, first, period, reach, pattern in rows:
            out.write(f"{npc_id}\t{first}\t{period}\t{reach}\t{pattern}\n")

    print(f"{len(rows)} npcs with a readable 30002 chain -> {args.out}")
    print(f"{len(set(unresolved))} patterns broadcast it through a chain this refuses to guess at")
    if args.report:
        for name in sorted(set(unresolved)):
            print(f"    {name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
