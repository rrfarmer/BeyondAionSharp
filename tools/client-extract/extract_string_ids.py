"""The symbolic string ids retail's AI patterns use, resolved to the numbers the client expects.

WHY THIS EXISTS
---------------
"Blocked on string ids" has been the stated reason for leaving a mechanic unported in this log more
times than any other single cause: `display_system_message`, `say_to_all`, and every shout an NPC makes
name a string symbolically -- `STR_MSG_Ab1_Crotan_Named_Spawn_In` -- while this port's own
`npc_shouts.xml` is keyed by number.

**The client ships the mapping.** `strings.xml` is 118MB of `<id>`/`<name>` pairs, 371,981 of them, and
it resolves the pattern dump completely:

| | |
|---|---|
| distinct string ids used by patterns | 3,492 |
| uses | 8,820 |
| **resolved by `strings.xml`** | **3,492 / 8,820 -- all of them** |

Every one of them belongs to a message element: `say_to_all` (932 uses), `display_system_message` (375)
and `send_system_msg` (6). `Do.Say` already sends the first kind, so the shouts are portable the moment
the numbers are available; the other two need a helper.

Only the ids the patterns actually use are written out, because the full table is two orders of
magnitude larger and nothing here needs the rest.

The body text is deliberately **not** carried. The server sends a string id and the client renders it
from its own locale files, so the Korean text in the dump is of no use to this port and would only make
the artefact large and encoding-fragile.

CLI:
    python extract_string_ids.py <xml_dir> <out.tsv>
"""
from __future__ import annotations

import argparse
import collections
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402


def mapping(path: pathlib.Path) -> dict[str, int]:
    """name -> id, streamed: the file is over a hundred megabytes of UTF-16."""
    out: dict[str, int] = {}
    buffered = ""
    with io.open(path, "r", encoding="utf-16", errors="replace") as handle:
        while True:
            block = handle.read(1 << 22)
            if not block:
                break
            buffered += block
            records = buffered.split("</string>")
            buffered = records.pop()
            for record in records:
                number = re.search(r"<id>(\d+)</id>", record)
                named = re.search(r"<name>([^<]+)</name>", record)
                if number and named:
                    out[named.group(1)] = int(number.group(1))
    return out


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("xml_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    names = mapping(args.xml_dir / "strings.xml")

    used: collections.Counter = collections.Counter()
    for path in sorted(args.xml_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for found in re.finditer(r"<string_id>([^<]+)</string_id>", text):
            used[found.group(1)] += 1

    missing = sorted(name for name in used if name not in names)
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("string_id\tname\tuses\n")
        for name, count in sorted(used.items()):
            if name in names:
                out.write(f"{names[name]}\t{name}\t{count}\n")

    print(f"{len(used) - len(missing)} of {len(used)} pattern string ids resolved "
          f"({sum(used.values())} uses) -> {args.out}")
    if missing:
        print(f"    {len(missing)} unresolved, e.g. {missing[:4]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
