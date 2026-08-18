"""Whole conversations nobody on this server is having.

Five encounters in a row were found by hand: the klaw pack, the black claw lycans, Guardian Vingeveu,
Chaoslord Kalabar, and the fortress guard call at 137 npcs. Every one of them is the same shape --
**a retail message number whose senders and whose listeners are both still on stock AI**, so the call
is never made and would not be heard if it were.

`audit_message_senders.py` asks the opposite question (listeners waiting on a sender nobody has) and
`audit_missing_adds.py` asks about spawns. Neither ranks the conversations that are simply absent at
both ends, which is where the remaining work is.

For each message number this counts:

  * **callers** -- live npcs, on a stock AI, bound to a pattern that broadcasts it;
  * **answerers** -- live npcs, on a stock AI, bound to a pattern that listens for it.

A number with both is a mechanic that exists in the data and nowhere on this server. A number with only
one side is somebody else's question: with no live caller it belongs to `audit_message_senders.py`, and
with no live answerer it is a shout into nothing, which the dead-shout audit covers.

**The count is npcs, not importance.** A number binding four hundred generic monsters ranks above a
named boss and his two adds, and the boss is usually the better hour's work. Read the names.

Usage:
    python audit_silent_conversations.py <patterns_dir> <binding_tsv> [--repo ..] [--limit N] [--min N]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

STOCK = {"aggressive", "general", "passive", "monster", "guard", "onedmg_passive",
         "quest_use_item", "dummy", ""}

SEND_RE = re.compile(r"<broadcast_message>\s*<message_type>(\d+)<")
RECV_RE = re.compile(r"<is_message>\s*<message_type>(\d+)<")


def scan(patterns_dir: pathlib.Path):
    """(senders, listeners) as message number -> set of pattern names."""
    senders: dict[str, set[str]] = collections.defaultdict(set)
    listeners: dict[str, set[str]] = collections.defaultdict(set)
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
            for hit in SEND_RE.finditer(body):
                senders[hit.group(1)].add(name.group(1))
            for hit in RECV_RE.finditer(body):
                listeners[hit.group(1)].add(name.group(1))
    return senders, listeners


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--min", type=int, default=2, help="skip conversations smaller than this")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)

    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        hit = re.search(r'ai="([^"]*)"', attrs)
        ai_of[npc_id] = hit.group(1) if hit else ""
        hit = re.search(r'name="([^"]*)"', attrs)
        name_of[npc_id] = hit.group(1) if hit else ""

    placed: set[str] = set()
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        placed.update(re.findall(r'<spawn npc_id="(\d+)"',
                                 path.read_text(encoding="utf-8", errors="replace")))

    rows = [line.rstrip("\n").split("\t") for line in open(args.binding_tsv, encoding="utf-8")]
    col = {c: i for i, c in enumerate(rows[0])}
    members: dict[str, list[str]] = collections.defaultdict(list)
    for row in rows[1:]:
        members[row[col["pattern_name"]]].append(row[col["npc_id"]])

    def silent(patterns: set[str]) -> list[str]:
        """Live npcs on those patterns that still run a stock AI."""
        out: list[str] = []
        for pattern in patterns:
            out.extend(i for i in members.get(pattern, [])
                       if i in placed and ai_of.get(i, "") in STOCK)
        return out

    senders, listeners = scan(pathlib.Path(args.patterns_dir))

    found = []
    for number in set(senders) & set(listeners):
        callers = silent(senders[number])
        answerers = silent(listeners[number])
        if not callers or not answerers:
            continue
        if len(callers) + len(answerers) < args.min:
            continue
        found.append((len(callers) + len(answerers), number, callers, answerers))

    found.sort(reverse=True)
    print(f"{len(found)} message numbers have live stock-AI npcs on BOTH ends -- a mechanic that exists")
    print("in the data and nowhere on this server.")
    print()
    print(f"{'msg':>8}  {'call':>4} {'ans':>4}   who")
    for total, number, callers, answerers in found[:args.limit]:
        who = ", ".join(dict.fromkeys(name_of.get(i, i) for i in (callers + answerers)[:3]))
        print(f"{number:>8}  {len(callers):>4} {len(answerers):>4}   {who}")
    if len(found) > args.limit:
        print(f"  ... and {len(found) - args.limit} more")
    print()
    print("The count is npcs, not importance -- a number binding four hundred generic monsters ranks")
    print("above a named boss and his two adds, and the boss is usually the better hour's work.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
