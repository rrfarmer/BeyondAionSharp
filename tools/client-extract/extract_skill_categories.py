"""Retail's own skill categories, from `skill_base.xml`.

Retail's AI asks `is_event_skill_category` on `on_friend_spelled`: "was my friend just hit with a
physical debuff, a mental one, or healed". Eleven patterns ask it and **147 npcs run them**, and the
condition could not be read because this port had no category for a skill.

**This port's own skill data cannot answer it, and the gap is not close.** Measured against retail's
categories, `skilltype`/`skillsubtype` disagree on the word as well as the membership:

    SKILLCTG_PHYSICAL_DEBUFF   159 of 324 are skilltype=MAGICAL, only 119 PHYSICAL
    SKILLCTG_MENTAL_DEBUFF      63 of 81 are MAGICAL/DEBUFF -- but MAGICAL/DEBUFF is 1,382 skills
                                here, of which 1,248 have no retail category at all

Every port signature is dominated by `SKILLCTG_NONE`, so deriving the category would be wrong for the
overwhelming majority of skills. Retail names it outright, so the field is ported rather than guessed.

`SKILLCTG_NONE` is dropped: it is 12,341 of the 14,393 records and means "no category", which an
absent row already says.
"""
from __future__ import annotations

import argparse
import collections
import io
import pathlib
import re
import sys


def categories(path: pathlib.Path):
    """(skill id, category) for every skill retail gives one. Streamed: 74MB of UTF-16."""
    buffered = ""
    with io.open(path, "r", encoding="utf-16", errors="replace") as handle:
        while True:
            block = handle.read(1 << 22)
            if not block:
                break
            buffered += block
            records = buffered.split("</skill_base>")
            buffered = records.pop()
            for record in records:
                number = re.search(r"<id>(\d+)</id>", record)
                named = re.search(r"<skill_category>([^<]*)</skill_category>", record)
                if not number or not named:
                    continue
                category = named.group(1).strip()
                if not category.startswith("SKILLCTG_") or category == "SKILLCTG_NONE":
                    continue
                yield int(number.group(1)), category[len("SKILLCTG_"):]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("xml_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    rows = sorted(categories(args.xml_dir / "skill_base.xml"))
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("skill\tcategory\n")
        for skill, category in rows:
            out.write(f"{skill}\t{category}\n")

    tally = collections.Counter(category for _, category in rows)
    print(f"{len(rows)} skills carry a retail category -> {args.out}")
    for category, count in tally.most_common():
        print(f"    {count:5d}  {category}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
