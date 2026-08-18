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


def answer_kinds(patterns_dir: pathlib.Path) -> dict[tuple[str, str], set[str]]:
    """(pattern name, message number) -> {"add", "switch"}.

    **Keyed on the message number, not the pattern.** Keying on the pattern alone was the first
    version and it called almost everything "mixed": a single retail pattern routinely answers several
    numbers in different ways -- `Gab1_Gaurd_An` switches for one call and only notes another -- so the
    union over a pattern says nothing about the branch we actually wrote.
    """
    kinds: dict[tuple[str, str], set[str]] = collections.defaultdict(set)
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
                    listened = re.search(r"<is_message><message_type>(\d+)<", flat)
                    if not listened or "MESSAGE_PARAM" not in flat:
                        continue
                    key = (name.group(1), listened.group(1))
                    if "<add_hate_point>" in flat:
                        kinds[key].add("add")
                    if "<switch_target>" in flat:
                        kinds[key].add("switch")
    return kinds


#: `Do.HateMessageTarget` sitting in a branch, with the `When.Message(...)` that guards it.
BRANCH_RE = re.compile(
    r"When\.Message\(([A-Za-z0-9_.]+)\)(.{0,600}?)Do\.HateMessageTarget\(", re.S)

CONST_RE = re.compile(r"const\s+int\s+(\w+)\s*=\s*(\d+)\s*;")


def constants(handlers: pathlib.Path) -> dict[str, str]:
    """Every `const int NAME = VALUE` in the AI folder, by bare name and by Class.NAME."""
    out: dict[str, str] = {}
    for path in sorted(handlers.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        owner = None
        for line in text.splitlines():
            owner_hit = re.search(r"(?:class|struct)\s+(\w+)", line)
            if owner_hit:
                owner = owner_hit.group(1)
            hit = CONST_RE.search(line)
            if hit:
                out.setdefault(hit.group(1), hit.group(2))
                if owner:
                    out[f"{owner}.{hit.group(1)}"] = hit.group(2)
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    handlers = pathlib.Path(args.repo) / "src/Aion.GameServer/Handlers/AI"
    kinds = answer_kinds(pathlib.Path(args.patterns_dir))
    consts = constants(handlers)
    #: message number -> the kinds any retail pattern uses for it.
    by_number: dict[str, set[str]] = collections.defaultdict(set)
    for (pattern, number), kind in kinds.items():
        by_number[number] |= kind

    rows: list[tuple[str, str, int, list[str]]] = []
    for path in sorted(handlers.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        uses = text.count("Do.HateMessageTarget(")
        if not uses:
            continue
        named = {n for n in NAME_RE.findall(text)}
        detail: list[str] = []
        seen: set[str] = set()
        for token, _between in BRANCH_RE.findall(text):
            number = consts.get(token) or consts.get(token.split(".")[-1])
            if number is None and token.isdigit():
                number = token
            if number is None:
                detail.append(f"{token}=?")
                seen.add("unknown")
                continue
            # ONLY the patterns this file documents. Falling back to every pattern on the number was
            # the second version, and it reported almost everything "mixed" again: a message number is
            # reused across unrelated encounters, so the union over the game says nothing about ours.
            # When a file names no pattern that answers this number, the honest answer is "absent" --
            # the file has not documented the pattern its branch came from.
            here: set[str] = set()
            for n in named:
                here |= kinds.get((n, number), set())
            if not here:
                detail.append(f"{number}=absent")
                seen.add("unknown")
                continue
            label = "/".join(sorted(here))
            detail.append(f"{number}={label}")
            seen.add("switch" if here == {"switch"} else ("add" if here == {"add"} else "mixed"))
        if not detail:
            rows.append((path.name, "unknown", uses, []))
            continue
        verdict = ("mixed" if len(seen) != 1 else seen.pop())
        rows.append((path.name, verdict, uses, sorted(set(detail))))

    order = {"add": 0, "mixed": 1, "unknown": 2, "switch": 3}
    for r in rows:
        order.setdefault(r[1], 4)
    rows.sort(key=lambda r: (order[r[1]], -r[2]))

    totals = collections.Counter(r[1] for r in rows)
    print(f"{sum(r[2] for r in rows)} uses of Do.HateMessageTarget across {len(rows)} classes\n")
    print(f"{'file':<34} {'verdict':<8} {'uses':>4}  patterns")
    for name, verdict, uses, named in rows:
        print(f"{name:<34} {verdict:<8} {uses:>4}  {', '.join(named)}")
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
