"""Flagged branches whose unflagged twin we did not port.

`set_flag_var` in a retail condition list is a test-and-set: the branch runs once and never again. It is
this port's `When.FirstTime`, and 218 of them are applied across 78 handler files.

**But a `set_flag_var` branch is very often half of a pair.** Retail's idiom for "do this every time, and
something extra the first time" is two branches with identical non-flag conditions: the flagged one at a
higher priority carrying the extra step, and an unflagged twin immediately below it carrying the rest.
First-match-wins does the rest -- the flagged branch wins once, the twin wins forever after.

**Port only the flagged half and the step stops happening.** That is not a subtle fidelity loss. Where the
branch re-arms a timer, the whole rotation it belongs to dies on its second lap, which is exactly what
`BIDF5_U01_Middle_Boss_Fire` did: a boss above 71 percent stopped acting after twenty seconds because its
top band's ring was never re-armed. It shipped, and two separate readings of that branch missed it,
because a branch read on its own looks correct.

This finds the pairs mechanically. For every retail branch with `set_flag_var` in its conditions, it looks
for a lower-priority branch **in the same pattern and handler whose non-flag conditions are identical**,
and reports whether our port carries both priorities.

  * **MISSING TWIN** -- retail has the pair, we have only the flagged branch. **Read these first**; each
    is a step that happens once instead of always.
  * **BOTH** -- we carry both priorities. Nothing to do.
  * **UNPORTED** -- the flagged priority is not in our file either, so the pair is moot here.

**Caveats.** Priorities are the key, and that key holds only where this port preserved retail's numbering
(see `report_dropped_guards.py` -- the same caveat, and it has been wrong once). Identical *conditions*
are compared by verb and value; a twin that retail wrote with a slightly wider band is not matched and is
a false negative. This finds the exact-twin idiom, which is the common one, not every possible pairing.

Usage:
    python audit_flag_twins.py <patterns_dir> <binding_tsv> [--repo ..] [--verdict missing]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
from audit_handler_actions import HANDLERS, served_patterns  # noqa: E402

FLAG_VERBS = {"set_flag_var", "unset_flag_var", "set_world_flag_var", "unset_world_flag_var"}


def branch_conditions(branch_xml: str) -> tuple[list[str], bool]:
    """The branch's non-flag conditions as sorted verb=value strings, and whether it carries a flag."""
    hit = re.search(r"<conditions>(.*?)</conditions>", branch_xml, re.S)
    if not hit:
        return [], False
    body = hit.group(1)
    flagged = False
    out = []
    for node in re.finditer(r"<(\w+)>(.*?)</\1>", body, re.S):
        verb, inner = node.group(1), node.group(2)
        if verb in FLAG_VERBS:
            flagged = True
            continue
        vals = re.findall(r"<(\w+)>([^<]*)</\1>", inner)
        out.append(verb + "(" + ",".join(f"{k}={v.strip()}" for k, v in vals) + ")")
    return sorted(out), flagged


def retail_pairs(patterns_dir: pathlib.Path):
    """(pattern, handler, flagged_priority, twin_priority, conditions) for every exact-twin pair."""
    pairs = []
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for pat in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = pat.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if not name:
                continue
            for handler in re.finditer(r"<(on_\w+)>(.*?)</\1>", body, re.S):
                hname, hbody = handler.group(1), handler.group(2)
                branches = []
                for br in re.finditer(r"<pattern>(.*?)</pattern>", hbody, re.S):
                    pri = re.search(r"<priority>(\d+)</priority>", br.group(1))
                    if not pri:
                        continue
                    conds, flagged = branch_conditions(br.group(1))
                    branches.append((int(pri.group(1)), conds, flagged))
                branches.sort(key=lambda b: -b[0])
                for i, (pri, conds, flagged) in enumerate(branches):
                    if not flagged or not conds:
                        continue
                    for lower_pri, lower_conds, lower_flagged in branches[i + 1:]:
                        if lower_flagged:
                            continue
                        if lower_conds == conds:
                            pairs.append((name.group(1), hname, pri, lower_pri, conds))
                            break
    return pairs


def our_priorities(repo: pathlib.Path) -> dict[tuple[str, str], set[int]]:
    """(AI name, retail handler) -> the branch priorities our class writes in that handler.

    Keyed by handler, not just by class. The first version of this tool pooled every priority in a file,
    and a `p100` written in one handler then vouched for a `p100` the class never wrote in another --
    which is how it reported a missing twin in a handler our silikor does not implement at all.
    """
    out: dict[tuple[str, str], set[int]] = collections.defaultdict(set)
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
        for i in range(1, len(parts), 2):
            name, body = parts[i], parts[i + 1]
            for ours, retails in HANDLERS.items():
                start = re.search(rf"\b{ours}\s*=\s*Of\(", body)
                if not start:
                    continue
                # Balanced scan from the opening paren. A regex cannot find the end of one of these
                # tables: they close Of( on the same line as their last branch -- "Do.ArmTimer(0,
                # 5000)))," -- so a line-anchored pattern matches nothing at all, which is how the
                # first run of this tool reported every pair as unported, including one it had
                # verified as present a minute earlier.
                depth, i = 0, start.end() - 1
                while i < len(body):
                    if body[i] == "(":
                        depth += 1
                    elif body[i] == ")":
                        depth -= 1
                        if depth == 0:
                            break
                    i += 1
                block = body[start.end():i]
                pris = {int(x) for x in re.findall(r"Branch\((\d+)\s*,", block)}
                for retail in retails:
                    out[(name, retail)] |= pris
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--verdict", default=None)
    args = ap.parse_args()

    pairs = retail_pairs(args.patterns_dir)
    served = served_patterns(args.repo, args.binding_tsv)
    ours = our_priorities(args.repo)
    pattern_to_ai = collections.defaultdict(set)
    for ai, pats in served.items():
        for p in pats:
            pattern_to_ai[p].add(ai)

    rows, counts = [], collections.Counter()
    for pattern, handler, flagged, twin, conds in pairs:
        for ai in sorted(pattern_to_ai.get(pattern, ())):
            have = ours.get((ai, handler), set())
            if flagged not in have:
                verdict = "unported"
            elif twin in have:
                verdict = "both"
            else:
                verdict = "MISSING TWIN"
            counts[verdict] += 1
            rows.append((verdict, ai, pattern, handler, flagged, twin, conds))

    print(__doc__.split("Usage:")[0].strip().splitlines()[0])
    print()
    for verdict, ai, pattern, handler, flagged, twin, conds in sorted(rows):
        if args.verdict and args.verdict.lower() not in verdict.lower():
            continue
        print(f"{verdict:<12} {ai:<28} {pattern:<34} {handler:<22} p{flagged} needs p{twin}")
        print(f"{'':<12}   when {' '.join(conds)}")
    print()
    print("  ".join(f"{k}={v}" for k, v in sorted(counts.items())))
    print()
    print("CAVEAT: keyed on branch priority, which holds only where this port kept retail's numbering.")
    print("        Exact-condition twins only; a twin retail wrote with a wider band is a false negative.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
