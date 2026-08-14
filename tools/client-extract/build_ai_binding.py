"""Build the retail AI-pattern -> npc_id binding table.

NCSoft's NpcAIPatterns files name each behavior script but never say which NPC
runs it; that join lived in server data we do not have. The game client does
carry it: every entry in client_npcs_monster.xml / client_npcs_npc.xml has an
<ai_name> naming its retail pattern.

This reads Npcs.pak straight from a client install, decodes both NPC files,
and joins their ai_name against the <name> values in a directory of
NpcAIPatterns XML files.

Matching is case-insensitive: the client is inconsistent about capitalization
(it writes "idnovice_Hameroon" where the dump writes "IDNovice_Hameroon").

CLI:
    python build_ai_binding.py <client_root> <patterns_dir> <out.tsv>
"""
from __future__ import annotations

import argparse
import collections
import csv
import pathlib
import re
from typing import Iterator, NamedTuple

import bxml
from aionpak import read_pak

NPC_FILES = ("client_npcs_monster.xml", "client_npcs_npc.xml")
NAME_RE = re.compile(r"<name>([^<]+)</name>")


class ClientNpc(NamedTuple):
    npc_id: str
    devname: str
    ai_name: str
    quest_ai_name: str
    source: str


def client_npcs(client_root: pathlib.Path) -> Iterator[ClientNpc]:
    """Yield every NPC record from the client's Npcs.pak."""
    pak = client_root / "Data" / "Npcs" / "Npcs.pak"
    if not pak.exists():
        raise SystemExit(f"not found: {pak}")

    for name, data in read_pak(pak):
        if name not in NPC_FILES or not bxml.is_binary_xml(data):
            continue
        for npc in bxml.decode(data):
            fields = {child.tag: (child.text or "") for child in npc}
            npc_id = fields.get("id")
            if npc_id:
                yield ClientNpc(npc_id, fields.get("name", ""),
                                fields.get("ai_name", ""),
                                fields.get("quest_ai_name", ""), name)


def _read_text(path: pathlib.Path) -> str:
    """Read a pattern file, honoring the UTF-16 BOM the raw dumps ship with."""
    raw = path.read_bytes()
    if raw[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return raw.decode("utf-16", "replace")
    return raw.decode("utf-8", "replace")


def pattern_names(patterns_dir: pathlib.Path) -> dict[str, tuple[str, str]]:
    """Map lowercased pattern name -> (original name, source filename)."""
    found: dict[str, tuple[str, str]] = {}
    for path in sorted(patterns_dir.glob("*.xml")):
        for name in NAME_RE.findall(_read_text(path)):
            found.setdefault(name.lower(), (name, path.name))
    return found


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root", help='e.g. "C:/Program Files (x86)/Beyond Aion"')
    ap.add_argument("patterns_dir", help="directory of NpcAIPatterns*.xml (UTF-8)")
    ap.add_argument("out_tsv")
    args = ap.parse_args()

    patterns = pattern_names(pathlib.Path(args.patterns_dir))
    if not patterns:
        raise SystemExit(f"no <name> values found in {args.patterns_dir}")

    rows: list[tuple[str, ...]] = []
    unmatched: collections.Counter[str] = collections.Counter()
    used: set[str] = set()
    with_ai = 0

    for npc in client_npcs(pathlib.Path(args.client_root)):
        if not npc.ai_name:
            continue
        with_ai += 1
        hit = patterns.get(npc.ai_name.lower())
        if hit is None:
            unmatched[npc.ai_name] += 1
            continue
        used.add(hit[0])
        rows.append((npc.npc_id, npc.devname, npc.ai_name,
                     hit[0], hit[1], npc.quest_ai_name))

    rows.sort(key=lambda r: int(r[0]))
    with open(args.out_tsv, "w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh, delimiter="\t")
        writer.writerow(["npc_id", "client_devname", "client_ai_name",
                         "pattern_name", "pattern_file", "quest_ai_name"])
        writer.writerows(rows)

    print(f"client NPCs with an ai_name : {with_ai:,}")
    print(f"  bound to a dump pattern   : {len(rows):,} ({len(rows) / with_ai:.1%})")
    print(f"  ai_name absent from dump  : {with_ai - len(rows):,} "
          f"across {len(unmatched):,} distinct names")
    print(f"distinct patterns bound     : {len(used):,} of {len(patterns):,}")
    print("\nMost common unbound ai_names (mostly engine built-ins, not missing scripts):")
    for name, count in unmatched.most_common(10):
        print(f"  {name:<32} {count:>6,} npcs")
    print(f"\nwrote {args.out_tsv}")


if __name__ == "__main__":
    main()
