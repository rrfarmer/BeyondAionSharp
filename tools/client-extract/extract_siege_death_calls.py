#!/usr/bin/env python3
"""Which npcs announce their own death with message 30003, according to retail.

WHY THIS EXISTS
---------------
`AbstractSiegeProtectorAI.HandleDied` broadcasts **30003** — the protector-down order — at fifty metres,
for every npc bound to it. 1,219 npcs are, across 93 retail patterns, and **exactly two of those
patterns actually carry the broadcast**.

> 877 protectors announce their death to every siege npc within fifty metres and retail does not.
> `FortressKillerAI` answers 30003 by standing down, so this is not a spare message: it is fortress
> killers being called off by npcs that in retail die quietly.

The class is a faithful port of the Java one, which is why nothing looked wrong from inside the C#. It
only shows up when the death handler is checked against the patterns of the npcs bound to it.

WHAT COUNTS AS A DEATH BROADCAST
--------------------------------
Retail spells the same thing three ways and all three count: `on_die`, and the pair
`on_killed_by_user` / `on_killed_by_npc` — the village chiefs use the pair, the artifact guards use
`on_die`. **A pattern that only *listens* for 30003 does not count**; the killers do that, and counting
them would hand the message to the npcs that answer it.

The range is retail's `range_as_meter` and is fifty everywhere it appears, but it is carried through
rather than assumed, because the one place this project hard-coded a range it was wrong twice.

Usage:  python extract_siege_death_calls.py <patterns-dir> <ai_binding.tsv> <out.tsv>
"""
import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402
import summarize_pattern as S  # noqa: E402

#: Retail's protector-down order.
DEATH_CALL = "<message_type>30003</message_type>"

#: Every handler retail uses to mean "when this npc dies".
DEATH_HANDLERS = ("on_die", "on_killed_by_user", "on_killed_by_npc")

HANDLER_RE = re.compile(r"<(on_\w+)>(.*?)</\1>", re.S)
BROADCAST_RE = re.compile(r"<broadcast_message>(.*?)</broadcast_message>", re.S)
RANGE_RE = re.compile(r"<range_as_meter>(\d+)</range_as_meter>")


def death_call_range(body):
    """The range this pattern broadcasts 30003 at when it dies, or None if it does not."""
    for handler in HANDLER_RE.finditer(body):
        if handler.group(1) not in DEATH_HANDLERS:
            continue
        for cast in BROADCAST_RE.finditer(handler.group(2)):
            if DEATH_CALL in cast.group(1):
                found = RANGE_RE.search(cast.group(1))
                return int(found.group(1)) if found else 0
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    binders = {}
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders.setdefault(fields[3], []).append(fields[0])

    rows = []
    patterns = 0
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            if DEATH_CALL not in body:
                continue
            named = S.NAME_RE.search(body)
            if not named:
                continue
            reach = death_call_range(body)
            if reach is None:
                continue                       # listens for it, does not send it
            patterns += 1
            for npc_id in binders.get(named.group(1), []):
                rows.append((int(npc_id), reach, named.group(1)))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\trange\tpattern\n")
        for npc_id, reach, pattern in rows:
            out.write(f"{npc_id}\t{reach}\t{pattern}\n")

    print(f"{len(rows)} npcs across {patterns} patterns broadcast 30003 when they die -> {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
