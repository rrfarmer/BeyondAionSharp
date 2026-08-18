"""Which message numbers are encounter-scoped, and which are a shared vocabulary.

Retail's `broadcast_message` numbers have no global registry, and this log has assumed throughout that
a number belongs to one encounter. Mostly it does. **Some do not**, and every time that has been
discovered it cost a wasted investigation:

  * `1007` — the mumu farmers' call for help. Its only listener patterns are one bound to zero npcs and
    one bound to five that do not stand near mumus.
  * `10018` — a pet's death cry. Its listeners are two `Reward` patterns and Kistenian, all other
    encounters.
  * `10000` — the surkana feeder's five HP-banded broadcasts. Every listener is a `BIDF5_U01_Runaway_*`
    pattern from a different instance.
  * `5001`, `6001`, `10011` — low numbers used by doors, Dramata guards and unrelated bosses, which the
    Bakarma commit had to reason about before binding classes to them.

Three wasted investigations and one near-miss is enough to make it a check rather than a habit.

The measure is **how many pattern files a number appears in**. The dump is split by area and by
designer -- `NpcAIPatterns_IDSeal_Twin_YJH.xml`, `NpcAIPatterns_LDF5_D2_YJH.xml` -- so a number confined
to one file is confined to one designer's encounter, and a number spread across a dozen is a shared
vocabulary whose meaning is "somebody hit me" or "come here" rather than anything specific.

**This is a prompt to check, not a verdict**, and it produces false alarms by design. `6981` -- the
Beshmundir decoy-lich call -- spans five files and was built successfully, because reading its senders
showed all of them were the same mechanic. The list says "read the senders before binding a class to
this"; it does not say the number is unusable.

It is a proxy in the other direction too: `NpcAIPatterns.xml` is enormous and holds many unrelated
encounters, so a number living only there still needs reading. What it catches is the opposite error --
treating a number that spans the whole dump as if it belonged to the fight in front of you.

The threshold is three files rather than four because `1007` and `10011`, two of the four cases that
prompted this, span exactly three.

Usage:
    python audit_generic_messages.py <patterns_dir> [--min-files 3]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

MESSAGE_RE = re.compile(r"<message_type>(\d+)</message_type>")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("--min-files", type=int, default=3,
                    help="how many pattern files a number must span to count as shared")
    args = ap.parse_args()

    files: dict[str, set[str]] = collections.defaultdict(set)
    uses: collections.Counter[str] = collections.Counter()

    for path in sorted(pathlib.Path(args.patterns_dir).rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for number in MESSAGE_RE.findall(text):
            files[number].add(path.name)
            uses[number] += 1

    shared = sorted(((len(f), uses[n], n) for n, f in files.items() if len(f) >= args.min_files),
                    reverse=True)

    print(f"{len(files)} distinct message numbers in the dump; "
          f"{len(shared)} appear in {args.min_files} or more pattern files.\n")
    print(f"{'files':>5} {'uses':>5}  number")
    for count, use, number in shared:
        print(f"{count:5} {use:5}  {number}")
    print()
    print("A number in this list may be a shared vocabulary rather than an encounter's own. It is a")
    print("prompt to read the senders, not a verdict: 6981 spans five files and was built correctly,")
    print("because every sender turned out to be the same mechanic. What the list is for is the other")
    print("case -- a listener built against a number whose callers belong to somebody else's fight.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
