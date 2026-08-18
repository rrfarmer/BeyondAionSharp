"""Which of our message answers switch a target retail would have left alone.

Retail answers a `broadcast_message` two ways, and they are not interchangeable:

  * **`add_hate_point target=OBJI_MESSAGE_PARAM`** — note the call, keep fighting whoever you were.
  * **`switch_target target=OBJI_MESSAGE_PARAM`** — drop what you were doing and go.

Across the 5.8 files the plain form is the common one: **700 answering branches use it alone against
349 that switch**, with 54 doing both. For most of the game an NPC that hears a neighbour call does not
turn around.

This port had only the switching form -- `Do.HateMessageTarget` -- so every answer written with it
turned. That is right for a third of them and wrong for the rest, and the error is invisible in a pin
unless the answerer already had a target worth keeping.

For every AI class in `src/Aion.GameServer/Handlers/AI` that answers a message, this reads the retail
pattern names the class documents, looks up what those patterns' `on_message` branches actually do, and
reports:

  * **switch** -- every named pattern switches. `Do.HateMessageTarget` is correct.
  * **add** -- every named pattern only adds hate. **Should be `Do.HateMessageParam`.**
  * **mixed** -- the class covers patterns that disagree, or a pattern that does both. Needs a human.
  * **unknown** -- no retail pattern name found in the file, so nothing can be said.

Usage:
    python audit_message_answers.py <patterns_dir> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

#: A retail pattern name as the AI files write them -- inside <c>...</c>, CamelCase with underscores.
NAME_RE = re.compile(r"<c>([A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+)</c>")


def answer_kinds(patterns_dir: pathlib.Path) -> dict[str, set[str]]:
    """Pattern name -> {"add", "switch"} for its on_message branches."""
    kinds: dict[str, set[str]] = collections.defaultdict(set)
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
            for handler in re.finditer(r"<on_message>(.*?)</on_message>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(1), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    if "<is_message>" not in flat or "MESSAGE_PARAM" not in flat:
                        continue
                    if "<add_hate_point>" in flat:
                        kinds[name.group(1)].add("add")
                    if "<switch_target>" in flat:
                        kinds[name.group(1)].add("switch")
    return kinds


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    kinds = answer_kinds(pathlib.Path(args.patterns_dir))
    handlers = pathlib.Path(args.repo) / "src/Aion.GameServer/Handlers/AI"

    rows: list[tuple[str, str, int, list[str]]] = []
    for path in sorted(handlers.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        uses = text.count("Do.HateMessageTarget(")
        if not uses:
            continue
        named = {n for n in NAME_RE.findall(text) if n in kinds}
        if not named:
            rows.append((path.name, "unknown", uses, []))
            continue
        seen: set[str] = set()
        for n in named:
            seen |= kinds[n]
        verdict = "mixed" if len(seen) != 1 else seen.pop()
        rows.append((path.name, verdict, uses, sorted(named)))

    order = {"add": 0, "mixed": 1, "unknown": 2, "switch": 3}
    rows.sort(key=lambda r: (order[r[1]], -r[2]))

    totals = collections.Counter(r[1] for r in rows)
    print(f"{sum(r[2] for r in rows)} uses of Do.HateMessageTarget across {len(rows)} classes\n")
    print(f"{'file':<34} {'verdict':<8} {'uses':>4}  patterns")
    for name, verdict, uses, named in rows:
        print(f"{name:<34} {verdict:<8} {uses:>4}  {', '.join(named[:3])}")
    print()
    for k in ("add", "mixed", "unknown", "switch"):
        if totals[k]:
            print(f"  {totals[k]:3d} classes: {k}")
    print()
    print("'add' is the list to fix: those answers should not be moving a target. 'mixed' needs the")
    print("branch read by hand, because one class covers patterns that disagree.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
