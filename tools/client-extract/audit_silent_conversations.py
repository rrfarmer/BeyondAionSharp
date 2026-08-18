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

**And a big number can be worth nothing at all.** Twice now a high-ranking row has turned out to be a
conversation whose *answer* is a single `use_skill` -- `3302` at 157 npcs, where the naga casters name
themselves and their bodyguards answer with a cast, and `23005` at 41, where the only live answerers
answer Captain Wigthor with a skill. Skill indices are this port's oldest blocker, so those rows are
unreachable however many npcs sit on them. The listing therefore marks what the answering branches
actually do:

  * **`hate`** -- at least one answer adds hate or switches target. Buildable.
  * **`skill-only`** -- every answering branch is a `use_skill` and nothing else. **Not buildable**, and
    the npc count is a mirage.
  * **`empty`** -- the answers carry no actions the summariser can see at all.
  * **`self-named`** -- the answer *is* a hate action, and it cannot land. When every caller broadcasts
    with `param_obj=OBJI_SELF`, the object the answer hates is the **caller**, which is a friend --
    and `AggroList.AddHate` drops hate aimed at a non-enemy. `60001` is the example: four tejhi call
    their camp and sixty-one answer with a thousand points on the caller, which retail evidently reads
    as "join the fight" (its own comment says so) and this port reads as nothing at all. **A hate
    action is not the same as a reachable one**, which is why this bucket exists beside `hate`.

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

SELF_SEND_RE = re.compile(
    r"<broadcast_message><message_type>(\d+)</message_type>.*?<param_obj>(\w+)</param_obj>")

SEND_RE = re.compile(r"<broadcast_message>\s*<message_type>(\d+)<")
RECV_RE = re.compile(r"<is_message>\s*<message_type>(\d+)<")

#: Actions an answer can carry that this port can translate without a skill index.
REACHABLE = ("add_hate_point", "switch_target", "attack_most_hating", "despawn", "spawn",
             "flee_from", "broadcast_message")


def scan(patterns_dir: pathlib.Path):
    """(senders, listeners, answer kinds) as message number -> set of pattern names / kind."""
    senders: dict[str, set[str]] = collections.defaultdict(set)
    listeners: dict[str, set[str]] = collections.defaultdict(set)
    answers: dict[tuple[str, str], set[str]] = collections.defaultdict(set)
    # message number -> the set of param_obj values its callers broadcast with.
    params: dict[str, set[str]] = collections.defaultdict(set)
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
            for hit in SELF_SEND_RE.finditer(re.sub(r"\s+", "", body)):
                params[hit.group(1)].add(hit.group(2))
            for hit in RECV_RE.finditer(body):
                listeners[hit.group(1)].add(name.group(1))
            # What the answering branches actually do -- see the module docstring.
            for handler in re.finditer(r"<on_\w+>(.*?)</on_\w+>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(1), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    listened = re.search(r"<is_message><message_type>(\d+)<", flat)
                    if not listened:
                        continue
                    key = (listened.group(1), name.group(1))
                    if any("<%s>" % a in flat for a in REACHABLE):
                        answers[key].add("hate")
                    elif "<use_skill" in flat:
                        answers[key].add("skill-only")
                    else:
                        answers[key].add("empty")
    return senders, listeners, answers, params


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

    def live_patterns(patterns: set[str]) -> set[str]:
        """Only the patterns that actually have a live stock-AI npc on them."""
        return {p for p in patterns
                if any(i in placed and ai_of.get(i, "") in STOCK for i in members.get(p, []))}

    senders, listeners, answers, params = scan(pathlib.Path(args.patterns_dir))

    found = []
    for number in set(senders) & set(listeners):
        callers = silent(senders[number])
        answerers = silent(listeners[number])
        if not callers or not answerers:
            continue
        if len(callers) + len(answerers) < args.min:
            continue
        # Only what the LIVE answerers do. Classifying across every pattern on the number was the
        # first version, and it marked 23005 buildable on the strength of a pattern nobody places --
        # exactly the over-promise the classifier exists to stop.
        kinds: set[str] = set()
        for pattern in live_patterns(listeners[number]):
            kinds |= answers.get((number, pattern), set())
        kind = "hate" if "hate" in kinds else ("skill-only" if "skill-only" in kinds else "empty")
        # A hate answer that can only ever name the caller cannot land -- see the module docstring.
        if kind == "hate" and params.get(number) == {"OBJI_SELF"}:
            kind = "self-named"
        found.append((len(callers) + len(answerers), number, callers, answerers, kind))

    found.sort(reverse=True)
    print(f"{len(found)} message numbers have live stock-AI npcs on BOTH ends -- a mechanic that exists")
    print("in the data and nowhere on this server.")
    print()
    print(f"{'msg':>8}  {'call':>4} {'ans':>4}  {'answer':<10} who")
    for total, number, callers, answerers, kind in found[:args.limit]:
        who = ", ".join(dict.fromkeys(name_of.get(i, i) for i in (callers + answerers)[:3]))
        print(f"{number:>8}  {len(callers):>4} {len(answerers):>4}  {kind:<10} {who}")
    if len(found) > args.limit:
        print(f"  ... and {len(found) - args.limit} more")
    print()
    reachable = sum(1 for f in found if f[4] == "hate")
    print(f"{reachable} of them have an answer this port can translate; the rest answer with a skill")
    print("index and are unreachable however many npcs sit on them.")
    print()
    print("The count is npcs, not importance -- a number binding four hundred generic monsters ranks")
    print("above a named boss and his two adds, and the boss is usually the better hour's work.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
