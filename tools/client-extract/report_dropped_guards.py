"""What each dropped guard actually says in retail, and whether it is safe to apply.

`audit_handler_guards.py` says *that* a guard is missing. Acting on it needs two more things, and the
`chance` pass learned both the hard way:

  * **the retail text** -- the percentage, the threshold, the flag slot. Without it a fix is a guess, and
    the two kerubiel answers differ (80 and 50) where a guess would have flattened them.
  * **whether it is uniform across the patterns the class serves.** One AI class can serve dozens of
    npcs, and a guard may belong to only some. `tiamats_incarnation` serves seven incarnations and one
    carries the roll; `abyss_guard_call` serves forty-three and one does. Applying either to the class
    would have been a regression for the rest, and both were caught only after a pin broke.

So this prints, per dropped guard: the retail condition verbs with their values, how many served patterns
carry that guard, and how many have the same branch without it.

**Read the `MIXED` rows as blocked, not as work.** They need the class split per npc first.

**And `UNIFORM` is not the same as safe to apply blind.** The whole comparison keys on branch priority,
which holds only where this port preserved retail's numbering -- most of the log does, and some of it does
not. `RatmanCampAI` numbers its two branches 2 and 1 while retail puts the broadcast at 4 and a skill-only
step at 2, so the row that reads "OnAttacked#2 wants an hp guard" is describing a different branch from
the one it would be applied to. **Check that our branch and retail's branch at that priority are the same
step before touching anything.** A whole batch of nineteen was applied and reverted for want of that
check.

Usage:
    python report_dropped_guards.py <patterns_dir> <binding_tsv> [--repo ..] [--kind flag]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_handler_guards as G  # noqa: E402
import audit_handler_actions as H  # noqa: E402

#: The verbs that make up each kind, so a row can show what retail actually wrote.
KIND_VERBS = collections.defaultdict(set)
for _verb, _kind in G.RETAIL_GUARD.items():
    KIND_VERBS[_kind].add(_verb)


def branch_conditions(patterns_dir: pathlib.Path) -> dict[tuple[str, str, str], str]:
    """(pattern, handler, priority) -> the flattened <conditions> text of that branch."""
    out: dict[tuple[str, str, str], str] = {}
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
            for handler in re.finditer(r"<(on_\w+)>(.*?)</\1>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(2), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    priority = re.search(r"<priority>(\d+)<", flat)
                    if not priority:
                        continue
                    conditions = re.search(r"<conditions>(.*?)</conditions>", flat)
                    out[(name.group(1), handler.group(1), priority.group(1))] = \
                        conditions.group(1) if conditions else ""
    return out


def describe(conditions: str, kind: str) -> str:
    """The retail text for one kind, verbs and values, short enough to read in a table."""
    bits: list[str] = []
    for verb in sorted(KIND_VERBS[kind]):
        for hit in re.finditer(rf"<{verb}>(.*?)</{verb}>", conditions):
            values = re.findall(r"<\w+>([^<]+)</\w+>", hit.group(1))
            bits.append(f"{verb}({','.join(values)})" if values else verb)
        if f"<{verb}/>" in conditions or f"<{verb}></{verb}>" in conditions:
            bits.append(verb)
    return "; ".join(dict.fromkeys(bits)) or "?"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--kind", default=None, help="only this guard kind")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    patterns_dir = pathlib.Path(args.patterns_dir)
    retail = G.retail_guard_kinds(patterns_dir)
    conditions = branch_conditions(patterns_dir)
    serves = H.served_patterns(repo, pathlib.Path(args.binding_tsv))
    blocked = {"skillcount", "flying", "class", "race", "tribe", "waypoint", "eventskill",
               "abnormal", "level", "gender", "hyperlink", "quest", "damageflag", "time", "user"}

    rows: list[tuple[str, str, str, str, str, str, int, int]] = []
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for ai_name, per_branch in G.our_guard_kinds(text).items():
            patterns = serves.get(ai_name, set())
            if not patterns:
                continue
            for (handler, priority), ours in per_branch.items():
                theirs: set[str] = set()
                present: dict[str, list[str]] = collections.defaultdict(list)
                absent: dict[str, list[str]] = collections.defaultdict(list)
                for pattern in patterns:
                    for retail_handler in G.HANDLERS[handler]:
                        hit = retail.get((pattern, retail_handler, priority))
                        if hit is None:
                            continue
                        theirs |= hit
                        for kind in set(hit) | (theirs - hit):
                            (present if kind in hit else absent)[kind].append(pattern)
                for kind in sorted(theirs - ours - blocked):
                    if args.kind and kind != args.kind:
                        continue
                    detail = "?"
                    for pattern in present.get(kind, []):
                        for retail_handler in G.HANDLERS[handler]:
                            text_of = conditions.get((pattern, retail_handler, priority))
                            if text_of:
                                detail = describe(text_of, kind)
                                break
                        if detail != "?":
                            break
                    rows.append((path.name, ai_name, handler, priority, kind, detail,
                                 len(present.get(kind, [])), len(absent.get(kind, []))))

    rows.sort(key=lambda r: (r[7] > 0, r[4], r[0]))
    print(f"{len(rows)} dropped guards with a readable retail condition\n")
    print(f"{'file':<28} {'ai name':<24} {'branch':<18} {'kind':<9} {'safe':<7} retail")
    for name, ai_name, handler, priority, kind, detail, has, hasnt in rows:
        verdict = "UNIFORM" if hasnt == 0 else f"MIXED{has}/{has + hasnt}"
        print(f"{name:<28} {ai_name:<24} {handler + '#' + priority:<18} {kind:<9} {verdict:<7} {detail}")
    mixed = sum(1 for r in rows if r[7])
    print()
    print(f"  {len(rows) - mixed} uniform, {mixed} mixed and blocked on splitting the class")
    print()
    print("UNIFORM means every served pattern agrees, NOT that the row can be applied unread: the key is")
    print("branch priority, and it only lines up where this port kept retail's numbering. Confirm our")
    print("branch and retail's branch at that priority are the same step first -- see the docstring.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
