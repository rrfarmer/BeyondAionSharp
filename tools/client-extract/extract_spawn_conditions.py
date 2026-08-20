"""Every distinct `extcondition` a retail world uses to gate a spawn group.

WHY THIS EXISTS
---------------
The conditional spawn engine is the largest mechanic this port has never had, and it has sat in the
backlog as a number rather than a shape. It has two halves and neither is built:

* **Writers.** `set_condition_spawn_variable` in the AI patterns -- 12,446 uses over 2,122 variable
  names, each carrying a `<string>` name, a `<set>` value and a `<modify>` mode.
* **Readers.** `<condition_info><condition><extcondition>` in the world files -- **54,388 gates across
  163 worlds**, each a boolean expression over those variables deciding whether a spawn group exists.
  `despawnAtOther="true"` means the group is removed again when the expression stops holding.

This extracts the reader half's *expressions*, deduplicated, as the corpus a parser has to handle.

THE GRAMMAR IS SMALL
--------------------
Measured over all 54,388: `==` 68,163, `&&` 25,225, `>` 10,237, `>=` 4,677, `||` 2,756, `!=` 1,504,
`<` 2,314, `<=` 1,244, parentheses, and integer literals with the occasional negative. **There is no
arithmetic.** A comparison of one variable against one integer, combined with `&&`, `||` and brackets,
covers every gate in the dump.

CLI:
    python extract_spawn_conditions.py <worlds_dir> <out.tsv>
"""
from __future__ import annotations

import argparse
import collections
import html
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("worlds_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--patterns", type=pathlib.Path,
                    help="pattern dump; with it, --inputs can say which variables no pattern writes")
    ap.add_argument("--inputs", type=pathlib.Path,
                    help="write the server-supplied variables here")
    args = ap.parse_args()

    uses: collections.Counter = collections.Counter()
    worlds: dict[str, str] = {}
    for world in sorted(args.worlds_dir.glob("*/world.xml")):
        try:
            text = S.read_text(world)
        except Exception:
            continue
        for raw in re.findall(r"<extcondition>(.*?)</extcondition>", text, re.S):
            # The files carry &amp;&amp; and &gt;, so an un-unescaped scan reports `gt` as a variable
            # name 10,237 times and misses every greater-than in the grammar.
            expression = " ".join(html.unescape(raw).split())
            if not expression:
                continue
            uses[expression] += 1
            worlds.setdefault(expression, world.parent.name)

    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("uses\tworld\texpression\n")
        for expression, count in sorted(uses.items(), key=lambda kv: (-kv[1], kv[0])):
            out.write(f"{count}\t{worlds[expression]}\t{expression}\n")

    print(f"{sum(uses.values())} gates, {len(uses)} distinct expressions -> {args.out}")

    if args.inputs and args.patterns:
        report_inputs(args.patterns, args.inputs, uses)
    return 0


def report_inputs(patterns: pathlib.Path, out: pathlib.Path, uses) -> None:
    """The gate variables no AI pattern ever writes -- the engine has to supply those.

    **39% of all gates depend on them.** `GAb1_PvPStatus`, `SpecialServer_Cond`, `InterServer_Cond`,
    the `DirectPortalDest_*` family and the transform rewards are read by the world files and written by
    nothing in the AI dump, so they are server state rather than npc state: siege and PvP status, portal
    wiring, event rewards. A store that only carried what patterns write would leave every one of those
    gates reading zero.
    """
    written: set[str] = set()
    for path in sorted(patterns.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in re.finditer(
                r"<set_condition_spawn_variable>.*?<string>([^<]*)</string>", text, re.S):
            written.add(match.group(1))

    read: collections.Counter = collections.Counter()
    for expression, count in uses.items():
        for match in re.finditer(r"(\[SAVE\])?([A-Za-z_][A-Za-z_0-9]*)\s*(?:==|!=|>=|<=|>|<)",
                                 expression):
            read[(match.group(1) or "") + match.group(2)] += count
        bare = re.fullmatch(r"\s*([A-Za-z_][A-Za-z_0-9]*)\s*", expression)
        if bare:
            read[bare.group(1)] += count

    supplied = {name: count for name, count in read.items()
                if name.replace("[SAVE]", "") not in written}
    with out.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("uses\tvariable\n")
        for name, count in sorted(supplied.items(), key=lambda kv: (-kv[1], kv[0])):
            handle.write(f"{count}\t{name}\n")

    print(f"    {len(supplied)} of {len(read)} gate variables are never written by a pattern "
          f"({sum(supplied.values())} gate uses) -> {out}")


if __name__ == "__main__":
    sys.exit(main())
