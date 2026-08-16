"""Find npcs running a stock AI when a sibling on the same retail pattern has a bespoke class.

The cheapest work in this project, and nothing was looking for it: retail ships an encounter as
several npc ids — a normal-mode boss and a hard-mode one, an Elyos copy and an Asmodian one, three
difficulty variants of one room — all bound to a single pattern. Translate one and the others keep
whatever their template said, which is usually `aggressive`. Macunbello is the case that prompted
this: `MacunbelloAI` has been a complete translation of `IDCT_Boss_LichKing` for some time, and three
live HERO copies of the same boss were still fighting as plain monsters.

It is deliberately conservative, because a false positive here costs a wrong fight:

  narrow patterns only   A pattern bound by more than NARROW npcs is a generic behaviour shared by
                         unrelated monsters, not one encounter. `D2_FnA` alone would otherwise
                         report 994 "orphans" that have nothing to do with each other.
  one class only         If the siblings already run two different bespoke classes, the pattern is
                         being specialised on purpose and this cannot say which one is right.
  stock means stock      The generic set below includes the semi-generic helpers — `servant`,
                         `summoner`, `noaction` — because sharing one of those with a sibling says
                         nothing about the sibling's encounter.

Every hit still needs reading before it is acted on: a class may key on its own npc id, and the
sibling may be the variant that id check excludes. This reports candidates, not conclusions.

Usage:
    python audit_orphan_siblings.py <binding_tsv> [--repo ..] [--narrow 8]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402

# Stock behaviours: sharing one with a sibling tells you nothing about the sibling.
GENERIC = {
    "aggressive", "general", "passive", "guard", "dummy", "ntrap", "trap", "summon",
    "monster", "peace", "questnpc", "npc", "door", "noaction", "servant", "summoner",
    "following", "speaker", "useitem", "simple_abyssguard", "drakanmedic", "naia",
    "aggressive_boss_summon", "onedmg_passive", "onedmg_aggressive", "modified_iron_wall_aggressive",
}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--narrow", type=int, default=8,
                    help="a pattern with more binders than this is a generic behaviour")
    args = ap.parse_args()

    live = A.spawnable_npc_ids(args.repo)
    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")

    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    rating_of: dict[str, str] = {}
    for m in re.finditer(r"<npc_template[^>]*>", templates):
        block = m.group(0)
        npc = A.attr(block, "npc_id")
        ai_of[npc] = A.attr(block, "ai")
        name_of[npc] = A.attr(block, "name")
        rating_of[npc] = A.attr(block, "rating")

    binders: dict[str, list[str]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows = []
    for pattern, ids in binders.items():
        if len(ids) > args.narrow:
            continue
        bespoke = sorted({ai_of[i] for i in ids if ai_of.get(i, "") and ai_of[i] not in GENERIC})
        if len(bespoke) != 1:
            continue
        orphans = [i for i in ids if i in live and ai_of.get(i, "") in GENERIC]
        if orphans:
            rows.append((len(orphans), pattern, bespoke[0], orphans))

    rows.sort(key=lambda r: (-r[0], r[1]))
    for count, pattern, cls, orphans in rows:
        print(f"{count:3}  {pattern:40} -> {cls}")
        for npc in orphans:
            print(f"       {npc}  {rating_of.get(npc, '?'):9} {ai_of.get(npc, '?'):12} {name_of.get(npc, '?')}")

    print()
    print(f"{sum(r[0] for r in rows)} spawned npcs across {len(rows)} narrow patterns are on a stock AI "
          f"while a sibling on the same pattern has a bespoke class.")


if __name__ == "__main__":
    main()
