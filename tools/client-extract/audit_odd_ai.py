#!/usr/bin/env python3
"""Npcs whose `ai` disagrees with the other npcs running the same retail pattern.

WHY THIS EXISTS
---------------
Yamennes' first spawn gate is the case. Retail binds four npcs to `IDAbRe_Core_Summon4`; three of them
carry `ai="yamennes_spawn_gate"` here and the fourth, 281906, carried `ai="portal"` -- a teleporter.

> The corrected encounter produced **two gates instead of three**, and the npc that failed to appear had
> spawned perfectly well. It was simply behaving like something else. An npc with the wrong `ai` is not
> a missing npc and not a missing class: it is an npc doing someone else's job, which is the hardest of
> the three to see.

Retail's own binding is the check. If a pattern's npcs mostly agree on one `ai` and a few do not, the few
are worth reading -- and the majority is evidence rather than proof, which is why this prints both sides.

WHAT IT DOES NOT CLAIM
----------------------
**A pattern bound to thousands of npcs says nothing about any one of them.** The first run of this
reported 1,679 rows, headed by 3,223 npcs sharing `D2_FnA` -- a generic retail pattern that every ordinary
drakan runs. Any npc with a specialised class looks like a dissenter there, and none of it is a defect.

Yamennes' gate was findable because its pattern binds **four** npcs: the binding is a statement about
that encounter. So only small patterns are read, and `--max-siblings` is the knob.

**A disagreement is not automatically a defect.** Retail binds one pattern to npcs this port legitimately
treats differently: a pattern shared by a boss and its statue, or by an attackable npc and its
unattackable twin, will show up here and should. The audit reports the split and leaves the judgement.

Patterns where every npc agrees are silent, which is most of them.

Usage:  python audit_odd_ai.py [--xml DIR] [--min-majority N]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from client_npc_names import npc_names, unattackable_ids  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]

#: An `ai` that expresses no opinion. Shared by both directions of the audit, which disagree about which
#: side of the split is allowed to hold one.
GENERIC = ("aggressive", "general", "noaction", "")


#: The faction tokens retail puts in a dev name. `Li`/`Da`/`Dr` and their short forms `L`/`D`/`DR` mark
#: the Elyos-held, Asmodian-held and balaur-held version of one npc. `An`/`Ae`/`Ah` look similar and are
#: NOT faction -- they appear 18,567 / 10,639 / 3,304 times and are part of the npc's identity, so
#: stripping them would merge npcs that are genuinely different.
FACTIONS = {"L", "D", "DR", "Li", "Da", "Dr", "Lig", "Drk"}


def stem(devname):
    """A dev name with its faction token removed, so one npc's race variants share a key.

    `LDF5_Village_chief01_L`, `_D` and `_DR` all become `LDF5_Village_chief01`; `LDF5_chief_v01_L_61_An`
    becomes `LDF5_chief_v01_61_An`. Those are two different npcs that share a retail pattern, and keeping
    them apart is the entire point.
    """
    return "_".join(part for part in devname.split("_") if part not in FACTIONS)


def our_ai():
    """npc id -> the `ai` on its template here."""
    path = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
    return dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]*)"',
                           path.read_text(encoding="utf-8", errors="replace")))


def patterns_by_npc():
    out = {}
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 3 and parts[3]:
            out[parts[0]] = parts[3]
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--min-majority", type=int, default=0,
                    help="how many npcs must agree before a minority is worth reporting")
    ap.add_argument("--max-siblings", type=int, default=8,
                    help="ignore patterns bound to more npcs than this; they are generic and say nothing")
    ap.add_argument("--reverse", action="store_true",
                    help="the mirror case: a SPECIALISED majority with a GENERIC minority")
    ap.add_argument("--by-suffix", action="store_true",
                    help="compare an npc only against its own race variants, not the whole pattern")
    args = ap.parse_args()

    # A race-variant group holds two or three npcs, so the whole-pattern default would empty it.
    if args.min_majority == 0:
        args.min_majority = 2 if args.by_suffix else 3

    ai_of = our_ai()
    runs = patterns_by_npc()
    devname = {npc_id: name for name, npc_id in npc_names(args.xml).items()}
    furniture = unattackable_ids(args.xml)

    # Group our npcs by the retail pattern they run -- or, with --by-suffix, by the pattern AND the
    # npc's own name with its faction token stripped.
    #
    # **Why the narrower grouping exists.** Kaldor's balaur village chief was reported against
    # `base_protector` because its retail pattern binds three unrelated trios and six of its nine
    # siblings use that class. The right answer was `simple_abyssguard`, which is what its own two race
    # variants use -- outvoted by npcs that merely share a pattern. A retail pattern is not always one
    # npc's behaviour, and where it binds several named groups the majority across it is the wrong
    # denominator.
    by_pattern = collections.defaultdict(list)
    for npc_id, pattern in runs.items():
        if npc_id in ai_of:
            key = (pattern, stem(devname.get(npc_id, npc_id))) if args.by_suffix else pattern
            by_pattern[key].append(npc_id)

    rows = []
    for key, npcs in by_pattern.items():
        pattern = key[0] if args.by_suffix else key
        if len(npcs) > args.max_siblings:
            continue
        counts = collections.Counter(ai_of[n] for n in npcs)
        if len(counts) < 2:
            continue
        (majority, agreed), = counts.most_common(1)
        if agreed < args.min_majority:
            continue

        if args.reverse:
            # The mirror of everything below. Here the majority carries a real class and the odd one out
            # carries none -- 280638 ran `aggressive` while the other three npcs on `Naga_Servant` ran
            # `servant`, same pattern, same "sacred dragon relic" name. The default direction cannot see
            # that: it requires the minority to be specialised, because a lone specialist among generic
            # mooks is the ordinary case and drowned everything else out.
            #
            # This direction is safe to report for the same reason the other one is: the siblings are
            # evidence about what the pattern means, and a family that agrees on a class while one member
            # has none is a member that was missed rather than a member that is different.
            if majority in GENERIC:
                continue
            odd = [n for n in npcs if ai_of[n] in GENERIC]
            for npc_id in odd:
                rows.append((pattern, majority, agreed, npc_id, ai_of[npc_id] or "(none)",
                             devname.get(npc_id, "?"), npc_id in furniture))
            continue

        # The majority must be a real class too. A family whose siblings are all plain `aggressive` and
        # one of which has a class is the ORDINARY case -- a boss among its mooks -- and it accounted
        # for nearly every row before this line existed. What Yamennes' gate looked like is different:
        # both sides specialised, disagreeing about which specialisation.
        if majority in GENERIC:
            continue

        # A generic ai is not a competing opinion, it is the absence of one -- those npcs are the
        # unimplemented-add case that audit_summon_ids already covers, and reporting them here would
        # bury the real finding under hundreds of rows.
        odd = [n for n in npcs
               if ai_of[n] != majority
               and ai_of[n] not in GENERIC]
        for npc_id in odd:
            rows.append((pattern, majority, agreed, npc_id, ai_of[npc_id],
                         devname.get(npc_id, "?"), npc_id in furniture))

    rows.sort(key=lambda r: (-r[2], r[0]))
    print(f"{len(by_pattern)} retail patterns have npcs in this port")
    print(f"{len(rows)} npcs carry a non-generic ai that disagrees with {args.min_majority}+ of their siblings\n")
    for pattern, majority, agreed, npc_id, ai, name, fx in rows:
        print(f"  {npc_id}  ai={ai:26s} but {agreed} siblings on {pattern[:28]} use {majority}")
        print(f"        {name[:60]}{'   [unattackable]' if fx else ''}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
