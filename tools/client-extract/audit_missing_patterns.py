"""What the client asks for that neither leaked pattern dump carries.

Chaoslord Kalabar spawns a wheel of death whose client `ai_name` is `ND2_RnJ` -- and `ND2_RnJ` is in
**neither** the 2.7 nor the 5.8 files. That was the first time a pattern named by the client turned out
to be missing from both dumps rather than mis-grepped, and the obvious next question was how often it
happens. Often:

    client npcs naming a pattern: 63,244; present in a dump: 49,134 (77.7%)

So **roughly one client AI reference in five points at behavior no dump we have describes.** That is a
ceiling on what this port can ever reach from these files, and it was not written down anywhere.

**The head of the list is not the interesting part.** `NPC`, `NoAction`, `npc`, `Resurrect`,
`ReturnToEntrance`, `FOBJ_NormalDrop` are the client's built-in AI types rather than pattern scripts --
an npc on `NoAction` is not missing a pattern, it has none. They are filtered out by default, which is
what `--all` turns off.

What is left is the honest list: pattern-shaped names, npcs our data actually places, and no behavior to
port. Each one is a gap in the source. Nothing here is actionable against the dumps -- the point is to
answer "is this really absent, or did I mis-grep?" in one command, and to stop a future reader from
concluding that a silent npc is a porting oversight.

Usage:
    python audit_missing_patterns.py <client_root> <patterns_dir>... [--repo ..] [--all] [--limit N]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import bxml  # noqa: E402
import summarize_pattern as S  # noqa: E402
from aionpak import read_pak  # noqa: E402

NPC_FILES = ("client_npcs_monster.xml", "client_npcs_npc.xml")

#: The client's built-in AI types. An npc on one of these is not missing a pattern -- it has none.
BUILTIN = {
    "npc", "noaction", "resurrect", "returntoentrance", "fobj_normaldrop", "dummy", "none",
    "guard", "monster", "general", "passive", "aggressive",
}


def dumped_names(dirs: list[pathlib.Path]) -> set[str]:
    names: set[str] = set()
    for d in dirs:
        for path in d.rglob("*.xml"):
            try:
                text = S.read_text(path)
            except Exception:
                continue
            names.update(m.group(1).lower() for m in re.finditer(r"<name>([^<]+)</name>", text))
    return names


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir", nargs="+")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--all", action="store_true", help="include the client's built-in AI types")
    ap.add_argument("--limit", type=int, default=25)
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    known = dumped_names([pathlib.Path(d) for d in args.patterns_dir])

    placed: set[str] = set()
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        placed.update(re.findall(r'<spawn npc_id="(\d+)"',
                                 path.read_text(encoding="utf-8", errors="replace")))

    name_of: dict[str, str] = {}
    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        hit = re.search(r'name="([^"]*)"', attrs)
        name_of[npc_id] = hit.group(1) if hit else ""

    total = have = 0
    live = collections.Counter()
    behind = collections.Counter()
    examples: dict[str, list[str]] = collections.defaultdict(list)

    pak = pathlib.Path(args.client_root) / "Data" / "Npcs" / "Npcs.pak"
    for filename, data in read_pak(pak):
        if filename not in NPC_FILES or not bxml.is_binary_xml(data):
            continue
        for npc in bxml.decode(data):
            fields = {child.tag: (child.text or "") for child in npc}
            ai = fields.get("ai_name", "").strip()
            npc_id = fields.get("id", "")
            if not ai:
                continue
            total += 1
            if ai.lower() in known:
                have += 1
                continue
            if not args.all and ai.lower() in BUILTIN:
                continue
            behind[ai] += 1
            if npc_id in placed:
                live[ai] += 1
                if len(examples[ai]) < 2:
                    examples[ai].append(f"{npc_id} {name_of.get(npc_id, '')}")

    print(f"client npcs naming a pattern: {total}; present in a dump: {have} "
          f"({100.0 * have / total:.1f}%)")
    print()
    label = "names" if args.all else "pattern-shaped names (built-in AI types filtered out)"
    print(f"{len(behind)} {label} the client uses that neither dump carries;")
    print(f"{sum(behind.values())} npcs behind them, of which {sum(live.values())} are placed here.")
    print()

    for ai, count in live.most_common(args.limit):
        print(f"  {ai:<32} {count:4} live   e.g. {', '.join(examples[ai])}")
    if len(live) > args.limit:
        print(f"  ... and {len(live) - args.limit} more")
    print()
    print("Every line is a gap in the source, not in the port. Nothing here is actionable against the")
    print("dumps -- see the module docstring.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
