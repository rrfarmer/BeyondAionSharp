"""Rebuild `ai_binding.tsv` from retail's own npc table.

`ai_binding.tsv` maps an npc id to the client devname, the AI name that npc runs, and the pattern that AI
name resolves to. **Roughly ten audits key on it**, and it arrived in this repository fully formed with no
generator — so when it was incomplete, nothing said so.

**It is incomplete.** The shipped table covers 49,134 npc ids; `npc_templates.xml` holds 63,287. The 14,153
it does not mention are invisible to every audit that reads it, and their absence looks exactly like an
npc with no AI.

That cost a real mechanic: Tiamat's eight rush drakan were reported for seven passes as "not in our data
at all", because a pass looked them up here and found nothing. **They were in `npc_templates.xml` the whole
time** — ids 219532-219539 — and only the binding lacked them.

Source: `XMLdata/China/npcs_monsters.xml` inside the 5.8 static-data archive. It carries `<id>`, `<name>`,
`<ai_name>` and `<quest_ai_name>` per npc — every column this table needs — in a **457 MB UTF-16** file,
which is streamed rather than read whole.

Usage:
    python build_ai_binding.py <npcs_monsters.xml> <patterns_dir> <out.tsv>
"""
from __future__ import annotations

import argparse
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

NPC = re.compile(r"<npc>(.*?)</npc>", re.S)


def field(block: str, tag: str) -> str:
    hit = re.search(f"<{tag}>([^<]*)</{tag}>", block)
    return hit.group(1).strip() if hit else ""


def npcs(path: pathlib.Path):
    """Stream (id, devname, ai_name, quest_ai_name). Overlapping tail keeps split records whole."""
    with io.open(path, encoding="utf-16", errors="replace") as fh:
        buf = ""
        for chunk in iter(lambda: fh.read(1 << 22), ""):
            buf = buf + chunk
            last = 0
            for m in NPC.finditer(buf):
                last = m.end()
                block = m.group(1)
                npc_id = field(block, "id")
                if npc_id:
                    yield npc_id, field(block, "name"), field(block, "ai_name"), field(block, "quest_ai_name")
            buf = buf[last:] if last else buf[-(1 << 16):]


def patterns(directory: pathlib.Path) -> dict[str, str]:
    """AI/pattern name -> the file that defines it."""
    out: dict[str, str] = {}
    for path in sorted(directory.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for name in re.findall(r"<npc_ai_pattern>\s*<name>([^<]+)</name>", text):
            out.setdefault(name, path.name)
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("npc_tables", type=pathlib.Path, nargs="+")
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    defined = patterns(args.patterns_dir)
    # Retail splits its npcs across several tables -- monsters, abyss monsters, standard monsters -- and
    # reading only the largest gives 29,805 of them. The shipped binding covered 49,134, which is how a
    # partial rebuild could have looked like an improvement while dropping twenty thousand rows.
    seen: set[str] = set()
    rows, with_ai, with_pattern = [], 0, 0
    for table in args.npc_tables:
      for npc_id, devname, ai_name, quest_ai in npcs(table):
        if npc_id in seen:
            continue
        seen.add(npc_id)
        if ai_name:
            with_ai += 1
        # The AI name is the pattern name wherever a pattern defines it; where none does, the npc runs an
        # AI the pattern data does not describe, and the column is left empty rather than guessed.
        pattern = ai_name if ai_name in defined else ""
        if pattern:
            with_pattern += 1
        rows.append((npc_id, devname, ai_name, pattern, defined.get(pattern, ""), quest_ai))

    rows.sort(key=lambda r: int(r[0]))
    with io.open(args.out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("npc_id\tclient_devname\tclient_ai_name\tpattern_name\tpattern_file\tquest_ai_name\n")
        for row in rows:
            fh.write("\t".join(row) + "\n")

    print(f"npcs in retail table : {len(rows)}")
    print(f"  with an ai_name    : {with_ai}")
    print(f"  resolving to a pattern: {with_pattern}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
