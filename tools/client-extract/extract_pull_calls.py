#!/usr/bin/env python3
"""Who shouts when pulled, and how far it carries.

WHY THIS EXISTS
---------------
Ashunatal's guards answer each other. A guard that is pulled broadcasts on entering combat, and the
guards around it turn: retail's `41100` and `41000` are the ordinary guard's call, `41101` and `41001`
the captain's, and the difference is real — **41101 is a `switch_target` in the answering pattern and
41100 only adds hate.** A captain is obeyed; a guard is noted.

The answering half is ported (`PanesterraGuardAI` and its five classes). The sending half is not:

| call | npcs whose retail pattern sends it | on a class that sends it here |
|---|---|---|
| `41100` @13m | 387 | 88 |
| `41100` @25m | 129 | 32 |
| `41000` @13m | 132 | 78 |
| `41000` @25m | 132 | 80 |
| `41101` @13m | 67 | 16 |
| `41001` @13m | 17 | 4 |

**Roughly five hundred guards are pulled and say nothing**, and 552 npcs are listening for 41101 alone.

WHY A TABLE
-----------
The call and its range are per npc, not per class: the same `panesterra_patrol` class covers npcs that
shout at thirteen metres and npcs that shout at twenty-five, and `base_protector` covers npcs that shout
41101 and npcs that shout nothing at all. A constant in a class cannot express that, and this is the
third mechanic in this port to need the same shape — see `ProtectorCalls` and `SiegeDeathCalls`.

Usage:  python extract_panesterra_pulls.py <patterns-dir> <ai_binding.tsv> <out.tsv>
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402
import summarize_pattern as S  # noqa: E402

#: Every enter-combat call this port can act on. Ashunatal's captain and guard pairs, and the fortress
#: guards' `23200`.
#:
#: **These are the ones something answers.** 178 distinct messages are broadcast on entering combat
#: across the 5.8 files and most are heard by nothing in the dump -- shouts to the client rather than npc
#: mechanics. The five here have 549 to 552 listeners apiece, which is why they are worth a table and the
#: other 173 are not.
CALLS = {"41000", "41001", "41100", "41101", "23200"}

ENTER_RE = re.compile(r"<on_enter_attack_state>(.*?)</on_enter_attack_state>", re.S)
CAST_RE = re.compile(r"<broadcast_message>(.*?)</broadcast_message>", re.S)
BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)


def fallback_branch(handler):
    """The rung retail runs when no condition applies -- highest priority, no guards.

    **The alternative is a table that makes a guard shout twice.** A patrol's enter-combat handler holds
    two branches: `is_user_flying` calls at thirteen metres and the unguarded fallback at twenty-five.
    Reading casts across the whole handler collects both and reads like one npc with two calls, which is
    what the first version of this did -- 129 npcs came out shouting at both ranges.
    `PanesterraPatrolAI`'s own remark had already recorded the split and the reason; the extractor had
    not been told.

    This port cannot evaluate `is_user_flying`, so it takes the same branch the class does: retail's
    fallback, which is also the overwhelmingly common case.
    """
    best = None
    for branch in BRANCH_RE.finditer(handler):
        body = branch.group(1)
        conditions = re.search(r"<conditions>(.*?)</conditions>", body, re.S)
        if conditions and conditions.group(1).strip():
            continue
        priority = re.search(r"<priority>(-?\d+)</priority>", body)
        rank = int(priority.group(1)) if priority else 0
        if best is None or rank > best[0]:
            best = (rank, body)
    return best[1] if best else ""


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    args = ap.parse_args()

    binders = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows = set()
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            entering = ENTER_RE.search(body)
            if not entering:
                continue
            for cast in CAST_RE.finditer(fallback_branch(entering.group(1))):
                kind = re.search(r"<message_type>(\d+)</message_type>", cast.group(1))
                if not kind or kind.group(1) not in CALLS:
                    continue
                reach = re.search(r"<range_as_meter>(\d+)</range_as_meter>", cast.group(1))
                # `param_obj` decides whether the call names the caller or whoever pulled it, and the
                # answering rungs read the parameter, so it is carried rather than assumed.
                names = re.search(r"<param_obj>(\w+)</param_obj>", cast.group(1))
                for npc_id in binders.get(named.group(1), []):
                    # A set, because the dump carries some patterns in two files and an npc would
                    # otherwise shout twice at the same range. Two DIFFERENT ranges is not a duplicate:
                    # `Gab1_Gaurd_Ra_An_Broad` broadcasts 41100 at thirteen metres and again at
                    # twenty-five, a near call and a far one, and both are retail.
                    rows.add((int(npc_id), int(kind.group(1)),
                              int(reach.group(1)) if reach else 0,
                              "target" if names and "CUR_TARGET" in names.group(1) else "self",
                              named.group(1)))

    rows = sorted(rows)
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\tcall\trange\tnames\tpattern\n")
        for row in rows:
            out.write("\t".join(str(field) for field in row) + "\n")

    kinds = collections.Counter(r[1] for r in rows)
    print(f"{len(rows)} pull calls across {len({r[0] for r in rows})} npcs -> {args.out}")
    print("    " + ", ".join(f"{n} npcs send {k}" for k, n in sorted(kinds.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
