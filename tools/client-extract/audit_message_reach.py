"""Find pattern payload that cannot matter because nobody is at the other end of the message.

`audit_timer_reach.py` asks whether a branch can ever run. This asks the next question: whether the
branch's *effect* can reach anybody. Two shapes, both of which the worth-doing ranking was counting at
full price:

  a broadcast nobody answers   `broadcast_message` is payload, and it is worth nothing if no pattern we
                               can spawn receives that number and does something visible with it. The
                               cast-only rule in the log is this rule applied one message at a time --
                               "do not send a broadcast whose only listeners answer with a cast".
  a receive nobody sends       an `on_message` branch full of hate points and target switches is worth
                               nothing if no pattern we can spawn ever broadcasts that number. The
                               tayga pack answers 2302 and 2304, and nothing alive says either.

"Alive" means the pattern has at least one owner our spawn data actually places. A listener that exists
only in the dump is the same dead end as a listener that answers with a cast.

**One level only.** A broadcast counts as answered if some live pattern replies with a payload action,
even where that reply is itself a broadcast into a dead end. Chasing the chain to a fixpoint would be
more correct and would need the same care `audit_timer_reach.py` takes over cycles; the one-level
answer is already enough to stop the ranking paying full price for an empty room, and the difference is
recorded rather than hidden.

Usage:
    python audit_message_reach.py <patterns_dir> <binding_tsv> [--repo ..] [--min 1]
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

_index: dict[str, set[str]] | None = None


def _walk(patterns_dir: pathlib.Path):
    """Every (pattern name, parsed root) in the dump, once."""
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in S.PATTERN_RE.finditer(text):
            block = m.group(0)
            named = S.NAME_RE.search(block)
            if not named:
                continue
            try:
                root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(block)}</ai_pattern>")
            except ET.ParseError:
                continue
            yield named.group(1), root


def sends_and_answers(root: ET.Element) -> tuple[set[str], set[str]]:
    """(message numbers this pattern broadcasts, numbers it answers with a payload action)."""
    handlers = root.find(".//event_handlers")
    if handlers is None:
        return set(), set()

    sends: set[str] = set()
    answers: set[str] = set()
    for event in handlers:
        for branch in event.findall("pattern"):
            actions = branch.find("actions")
            conditions = branch.find("conditions")
            if actions is None:
                continue

            for node in actions:
                if node.tag == "broadcast_message":
                    number = (node.findtext("message_type") or "").strip()
                    if number:
                        sends.add(number)

            if conditions is None:
                continue
            heard = {(y.findtext("message_type") or "").strip()
                     for y in conditions if y.tag == "is_message"}
            if heard and any(node.tag in T.PAYLOAD for node in actions):
                answers |= {h for h in heard if h}

    return sends, answers


def build_index(patterns_dir: pathlib.Path, live_patterns: set[str]) -> dict[str, set[str]]:
    """{"senders": …, "answerers": …} over the patterns we can actually spawn."""
    senders: set[str] = set()
    answerers: set[str] = set()
    for name, root in _walk(patterns_dir):
        if name not in live_patterns:
            continue
        sends, answers = sends_and_answers(root)
        senders |= sends
        answerers |= answers
    return {"senders": senders, "answerers": answerers}


def dead_message_payload(root: ET.Element, index: dict[str, set[str]]) -> tuple[int, int]:
    """(broadcasts nobody answers, payload actions in branches nobody triggers)."""
    handlers = root.find(".//event_handlers")
    if handlers is None:
        return 0, 0

    unheard = 0
    unasked = 0
    for event in handlers:
        for branch in event.findall("pattern"):
            actions = branch.find("actions")
            conditions = branch.find("conditions")
            if actions is None:
                continue

            for node in actions:
                if node.tag == "broadcast_message":
                    number = (node.findtext("message_type") or "").strip()
                    if number and number not in index["answerers"]:
                        unheard += 1

            if conditions is None:
                continue
            heard = {(y.findtext("message_type") or "").strip()
                     for y in conditions if y.tag == "is_message"}
            heard.discard("")
            if heard and not (heard & index["senders"]):
                unasked += sum(1 for node in actions if node.tag in T.PAYLOAD)

    return unheard, unasked


def live_pattern_names(repo: pathlib.Path, binding_tsv: pathlib.Path) -> set[str]:
    live = A.spawnable_npc_ids(repo)
    names: set[str] = set()
    for line in A.read_text(binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0] in live:
            names.add(fields[3])
    return names


def cached_index(repo: pathlib.Path, patterns_dir: pathlib.Path,
                 binding_tsv: pathlib.Path) -> dict[str, set[str]]:
    """Built once per process; the whole dump is walked to build it."""
    global _index
    if _index is None:
        _index = build_index(patterns_dir, live_pattern_names(repo, binding_tsv))
    return _index


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--min", type=int, default=1, help="least dead actions worth listing")
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

    index = cached_index(args.repo, args.patterns_dir, args.binding_tsv)

    rows = []
    for name, root in _walk(args.patterns_dir):
        owners = [n for n in binders.get(name, []) if n in live]
        unported = [n for n in owners if ai_of.get(n, "") in T.GENERIC_AI]
        if not unported:
            continue
        unheard, unasked = dead_message_payload(root, index)
        if unheard + unasked < args.min:
            continue
        rows.append((unheard + unasked, unheard, unasked, name, unported))

    rows.sort(key=lambda r: (-r[0], r[3]))
    print(f"{'dead':>4} {'said':>5} {'heard':>6}  {'pattern':40} owners")
    for total, unheard, unasked, name, unported in rows:
        who = ", ".join(f"{n} {name_of.get(n, '?')}" for n in unported[:2])
        if len(unported) > 2:
            who += f" (+{len(unported) - 2})"
        print(f"{total:4} {unheard:5} {unasked:6}  {name:40} {who}")
    print()
    print(f"{len(rows)} unported patterns talk to nobody somewhere; "
          f"{sum(r[1] for r in rows)} broadcasts nobody answers, "
          f"{sum(r[2] for r in rows)} actions nobody triggers.")


if __name__ == "__main__":
    main()
