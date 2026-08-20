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
    return 0


if __name__ == "__main__":
    sys.exit(main())
