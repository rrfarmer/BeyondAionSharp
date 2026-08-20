"""How much of retail's conditional spawn content can actually appear, and what the rest waits on.

WHY THIS EXISTS
---------------
Several entries in `docs/retail-ai-fidelity.md` quote a number of "reachable" gated placements, and the
number meant *"gated on a variable one of our tables writes"*. That is not the same as "can appear", and
the difference runs both ways:

* **A gate reading a variable nobody writes is not necessarily shut.** A missing variable reads as 0, so
  `SpecialServer_Cond == 0` -- 1,380 placements -- is satisfied at cold start and always was. Supplying
  that flag as 0 would be a no-op, which is worth knowing before building the mechanism to supply it.
* **A gate mentioning a variable we write is not necessarily openable.** Compound gates are 9,360
  `&&` uses deep; one writable name among three does not open the group.

So this evaluates every gate rather than pattern-matching names, at cold start (everything zero) and
against the set of variables the AI tables actually write.

WHAT THE ANSWER LOOKS LIKE
--------------------------
Roughly: 1,476 placements are open the moment the world loads, 5,501 of the shut ones read only
variables we write, 6,931 read some of ours and some of nobody's, and 7,181 read none of ours at all.
That last group is the ceiling on this whole approach and it is dominated by three names --
`GAb1_PvPStatus` (6,070 placements), `SpecialServer_Cond` (1,484) and `InterServer_Cond` (216) -- which
are server flags rather than anything an npc writes.

`SpawnVariables` already takes a server-flag dictionary and `SpawnVariableRegistry.Supply` exists to
fill it. **Nothing calls it.** For the two `*_Cond` flags that does not matter, because their common
case is `== 0` and zero is what a missing name already reads as; for `GAb1_PvPStatus` it matters a great
deal, and the value is owned by siege state rather than by a constant.

CLI:
    python audit_gate_reach.py [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys
import warnings

#: The AI tables that write spawn variables, and the column each keeps the name in.
#: Every table that writes a spawn variable. Missing one understates the reach silently -- adding
#: `passive_patterns` moved the answer by 363 placements, in a direction that looked like a regression.
TABLES = [("death_spawns.tsv", "place"), ("battle_cycles.tsv", "place"),
          ("idle_cycles.tsv", "place"), ("wake_idle_patterns.tsv", "place"),
          ("wake_variables.tsv", "name")]

NAME = re.compile(r"(?:\[SAVE\])?[A-Za-z_][A-Za-z_0-9]*")


def written_variables(out: pathlib.Path) -> set[str]:
    """Every spawn variable the generated tables write."""
    names: set[str] = set()
    for table, column in TABLES:
        path = out / table
        if not path.exists():
            continue
        lines = path.read_text(encoding="utf-8").splitlines()
        header = {name: index for index, name in enumerate(lines[0].split("\t"))}
        for line in lines[1:]:
            fields = line.split("\t")
            if table == "wake_variables.tsv":
                names.add(fields[header[column]])
            elif fields[header["kind"]] == "var":
                names.add(fields[header[column]])
    return names


def holds(gate: str, values: dict[str, int]) -> bool | None:
    """Whether the gate is true given these values; None if retail's text will not parse.

    Nine of the dump's gates are truncated or pasted into themselves, and `GatedSpawnData` refuses those
    the same way rather than guessing at them.
    """
    expression = NAME.sub(lambda m: str(values.get(m.group(0), 0)), gate)
    expression = expression.replace("&&", " and ").replace("||", " or ")
    try:
        # `Race == 2(Race == 2) && ...` is one of retail's pasted-into-itself gates; compiling it warns
        # about a call that is not one before failing, and the failure is the answer we want.
        warnings.simplefilter("ignore", SyntaxWarning)
        return bool(eval(expression, {"__builtins__": {}}, {}))  # noqa: S307 - fixed grammar, no names
    except Exception:
        return None


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    written = written_variables(args.repo / "tools/client-extract/out")
    lines = (args.repo / "game-server/data/static_data/spawns/gated/gated_spawns.tsv").read_text(
        encoding="utf-8").splitlines()
    header = {name: index for index, name in enumerate(lines[0].split("\t"))}

    counts: collections.Counter = collections.Counter()
    blocked: collections.Counter = collections.Counter()
    for line in lines[1:]:
        gate = line.split("\t")[header["gate"]]
        verdict = holds(gate, {})
        if verdict is None:
            counts["retail's own text will not parse"] += 1
            continue
        if verdict:
            counts["open at cold start"] += 1
            continue

        names = set(NAME.findall(gate))
        if names <= written:
            counts["shut, and every variable is one we write"] += 1
        elif names & written:
            counts["shut, and some variables are ours"] += 1
        else:
            counts["shut, and none of the variables are ours"] += 1
            for name in names - written:
                blocked[name] += 1

    print(f"{len(lines) - 1} gated placements, {len(written)} variables written by the ai tables\n")
    for reason, count in counts.most_common():
        print(f"   {count:6d}  {reason}")

    print("\nthe names holding the most placements shut, that nothing here writes:")
    for name, count in blocked.most_common(8):
        print(f"   {count:6d}  {name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
