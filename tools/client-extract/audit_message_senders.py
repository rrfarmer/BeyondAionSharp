"""Find listeners whose senders exist in the dump and not on the server.

`audit_message_reach.py` asks whether any pattern we can spawn broadcasts a given message. That is the
right question about the *data* and the wrong one about the *server*, and the difference has now
bitten twice by hand:

  * Vengeful Modor's idean obscura answer `444`, whose only sender binds to Modor -- who runs a
    Java-parity class rather than a pattern, so the message existed on both ends and nobody was
    holding the wire.
  * The Sauro Supply Base guards answer `22251`, whose senders are Brigade General Sheba and Guard
    Captain Ahuradim, both of them Java-parity classes too.

Both were found by reading. This finds the rest, by asking of every message number: **is there anybody
on this server who would actually say it?**

A message has a real sender when either

  * some live npc that broadcasts it is still on a stock AI -- unported, so building its pattern would
    supply the send; or
  * some live npc that broadcasts it runs a bespoke class whose source mentions the number.

Where neither holds, the reason is worth separating: a sender whose npcs exist as templates but are
never placed is a **spawn** gap and fixable from the spawn data, while a number nothing anywhere
broadcasts is dead. The klaw gatherers -- the largest stranded group in the dump -- turned out to be
the first kind.

The second test is a grep, and a grep is a proxy: a class that sends a message through a shared
constant named elsewhere will be missed, and a class that merely mentions a number in a comment will
be counted. It is reported as "mentions" rather than "sends" for that reason, and the point of the
audit is the third bucket -- listeners with **no** candidate sender at all -- where no proxy is needed.

Usage:
    python audit_message_senders.py <patterns_dir> <binding_tsv> [--repo ..] [--min 1]
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


def scan(patterns_dir: pathlib.Path):
    """(sends, answers) per pattern name: message numbers it broadcasts, and answers with payload."""
    sends: dict[str, set[str]] = collections.defaultdict(set)
    answers: dict[str, set[str]] = collections.defaultdict(set)

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
            handlers = root.find(".//event_handlers")
            if handlers is None:
                continue

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
                                sends[named.group(1)].add(number)

                    if conditions is None:
                        continue
                    heard = {(y.findtext("message_type") or "").strip()
                             for y in conditions if y.tag == "is_message"}
                    if heard and any(node.tag in T.PAYLOAD for node in actions):
                        answers[named.group(1)] |= {h for h in heard if h}

    return sends, answers


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--min", type=int, default=1, help="least stranded listeners worth listing")
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

    # Every message number our own AI sources mention, which is the best proxy available for
    # "a ported class actually sends this".
    mentioned: set[str] = set()
    for source in (args.repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs"):
        for number in re.findall(r"\b(\d{3,5})\b", source.read_text(encoding="utf-8", errors="replace")):
            mentioned.add(number)

    sends, answers = scan(args.patterns_dir)

    def live_owners(pattern: str) -> list[str]:
        return [n for n in binders.get(pattern, []) if n in live]

    # For each message: who could say it, and who is waiting for it.
    unported_senders: dict[str, int] = collections.Counter()
    ported_senders: dict[str, int] = collections.Counter()
    unspawned_senders: dict[str, int] = collections.Counter()
    for pattern, numbers in sends.items():
        owners = live_owners(pattern)
        for number in numbers:
            for npc in owners:
                if ai_of.get(npc, "") in T.GENERIC_AI:
                    unported_senders[number] += 1
                else:
                    ported_senders[number] += 1
            # A sender whose npcs exist as templates and are never placed is a spawn gap rather than
            # a dead message -- the klaw brood-mothers are the case that made this worth separating.
            unspawned_senders[number] += len([n for n in binders.get(pattern, []) if n not in live])

    rows = []
    for pattern, numbers in answers.items():
        waiting = [n for n in live_owners(pattern) if ai_of.get(n, "") in T.GENERIC_AI]
        if not waiting:
            continue
        for number in numbers:
            if unported_senders.get(number):
                continue  # a sender we could still build; audit_message_reach already counts it
            verdict = ("mentioned in a class" if number in mentioned
                       else "ported class, not mentioned" if ported_senders.get(number)
                       else "sender is never spawned" if unspawned_senders.get(number)
                       else "no sender at all")
            if verdict == "mentioned in a class":
                continue
            rows.append((len(waiting), number, verdict, pattern, waiting))

    rows.sort(key=lambda r: (-r[0], r[1]))
    print(f"{'npcs':>4} {'msg':>6}  {'why':28} {'listener pattern':40} owners")
    for count, number, verdict, pattern, waiting in rows:
        if count < args.min:
            continue
        who = ", ".join(f"{n} {name_of.get(n, '?')}" for n in waiting[:2])
        if len(waiting) > 2:
            who += f" (+{len(waiting) - 2})"
        print(f"{count:4} {number:>6}  {verdict:28} {pattern:40} {who}")

    stranded = {r[1] for r in rows}
    print()
    print(f"{len(rows)} listener patterns wait on {len(stranded)} messages nobody on this server sends; "
          f"{sum(r[0] for r in rows)} npcs behind them.")


if __name__ == "__main__":
    main()
