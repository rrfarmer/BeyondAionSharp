#!/usr/bin/env python3
"""Compare the summon groups in `spawn_helpers.xml` against the numbers retail's spawn actions use.

WHY THIS EXISTS
---------------
`audit_summon_ids.py` asks whether a C# class *names* every npc its retail pattern spawns. A class that
extends `SummonerAI` names none of them — its adds live in `spawn_helpers.xml` as data — so every one of
those classes looks like it is missing everything, and the audit cannot see the case where **the adds are
there with the wrong numbers**.

Mistress Viloa was that case. Retail spawns her three Primal Nightmares on `on_enter_attack_state` with
`num_to_spawn=3`, `spawn_range=5` and no timer; our data had `minCount="3" distance="3" schedule="5000"`.
The count was right, so nothing looked wrong, and the nightmares arrived **five seconds after the pull**
and huddled two metres too close.

WHAT IT COMPARES
----------------
For every `<summonGroup>` in `spawn_helpers.xml`, against the spawn actions in the summoner's own retail
pattern for that same npc id:

* **count** — `minCount`/`maxCount` against the total one retail **rung** places
* **range** — `distance` against `spawn_range`

**Read the output as candidates, never as defects.** The two sides group differently and there is no
reliable mapping between them: our data is organised by health-percentage block, retail's by rung, and
one boss's "two at seventy-five, two at fifty" is indistinguishable from "two, twice" without reading the
conditions on each rung. Four versions of this comparison were written before that was clear —
per-action (89 agreed, and it was wrong), summed across the pattern (15 agreed, wrong the other way),
per-rung (61), and per-rung with our side aggregated per npc (22, wrong across percentage blocks).
**Per-rung is the version kept**, because it is right about retail's side; the disagreement it reports
still needs a human to look at the percentage blocks.

WHAT IT DOES NOT COMPARE, AND WHY
---------------------------------
**Timing is left out deliberately.** `schedule` is a delay in milliseconds before the group spawns;
retail's equivalent is wherever the rung is hung — `on_enter_attack_state`, a battle timer, an HP rung —
and reducing that to one number would invent a comparison. Viloa's `schedule="5000"` against a rung with
no timer was clear because the rung had no timer at all. A rung hung on `BTIMERI_INDEX_3` cannot be
compared to a millisecond count without reading the timer chain, which is the work this tool exists to
avoid.

So a clean report here does not mean the timing is right. It means the counts and ranges are.

Usage:  python audit_summon_numbers.py [--xml DIR]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402
from audit_summon_ids import devname_to_npc  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
PATTERN = re.compile(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", re.S)
SPAWN_ACTION = re.compile(r"<spawn>(.*?)</spawn>", re.S)
AI_BLOCK = re.compile(r'<ai npcId="(\d+)">(.*?)</ai>', re.S)
GROUP = re.compile(r'<summonGroup ([^/>]*)/?>')


def our_groups():
    """summoner npc id -> [(spawned npc id, minCount, maxCount, distance, schedule)]."""
    path = REPO / "game-server" / "data" / "static_data" / "ai" / "spawn_helpers.xml"
    out = collections.defaultdict(list)
    for block in AI_BLOCK.finditer(path.read_text(encoding="utf-8", errors="replace")):
        owner = block.group(1)
        for group in GROUP.finditer(block.group(2)):
            attrs = dict(re.findall(r'(\w+)="([^"]*)"', group.group(1)))
            if "npcId" not in attrs:
                continue
            lo = int(attrs.get("minCount", 1))
            out[owner].append((attrs["npcId"], lo, int(attrs.get("maxCount", lo)),
                               float(attrs.get("distance", 0)), int(attrs.get("schedule", 0))))
    return out


def retail_spawns(xml_dir, dev):
    """pattern name -> {spawned npc id: (set of per-rung totals, set of ranges)}.

    **The unit is the rung, not the action and not the pattern.** A rung is one `<pattern>` block: its
    conditions pass and everything in it happens, so two actions of two inside one rung place four. Two
    rungs of two are alternatives -- different health bands, different timers -- and place two.

    This is the third version of this comparison and the first that is right. Comparing per action said
    Mistress Viloa's neighbours were wrong when they were not; comparing the sum across the whole pattern
    said the opposite, and dropped agreement from 89 to 15. Both would have sent somebody to change
    correct data.
    """
    out = collections.defaultdict(lambda: collections.defaultdict(lambda: (set(), set())))
    rung = re.compile(r"<pattern>(.*?)</pattern>", re.S)
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN.finditer(read_text(f)):
            for block in rung.finditer(m.group(2)):
                per_rung = collections.Counter()
                ranges = collections.defaultdict(set)
                for action in SPAWN_ACTION.finditer(block.group(1)):
                    body = action.group(1)
                    name = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", body)
                    if not name:
                        continue
                    npc_id = dev.get(name.group(1))
                    if not npc_id:
                        continue
                    count = re.search(r"<num_to_spawn>(\d+)</num_to_spawn>", body)
                    rng = re.search(r"<spawn_range>([\d.]+)</spawn_range>", body)
                    per_rung[npc_id] += int(count.group(1)) if count else 1
                    ranges[npc_id].add(float(rng.group(1)) if rng else 0.0)
                for npc_id, total in per_rung.items():
                    totals, seen = out[m.group(1)][npc_id]
                    totals.add(total)
                    seen.update(ranges[npc_id])
    return out


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
    args = ap.parse_args()

    dev = devname_to_npc(args.xml)
    ours = our_groups()
    theirs = retail_spawns(args.xml, dev)
    of = patterns_by_npc()

    compared = agreed = 0
    rows = []
    for owner, groups in sorted(ours.items()):
        pattern = of.get(owner)
        if not pattern or pattern not in theirs:
            continue
        for npc_id, lo, hi, distance, schedule in groups:
            distances = {distance}
            retail = theirs[pattern].get(npc_id)
            if not retail:
                continue
            totals, ranges = retail
            compared += 1
            notes = []
            # Compare against the TOTAL retail puts down, not each action's count. Retail routinely
            # spawns the same npc from several actions of one -- three separate <spawn> blocks rather
            # than num_to_spawn=3 -- so comparing per action reported a group of two against "retail 1"
            # when retail places two. The first version of this check did exactly that and would have
            # sent somebody to halve a correct group.
            if totals and not any(lo <= x <= hi for x in totals):
                notes.append(f"count {lo}-{hi} vs retail rungs {sorted(totals)}")
            if ranges and not (distances & ranges) and any(r > 0 for r in ranges):
                notes.append(f"range {sorted(distances)} vs retail {sorted(ranges)}")
            if notes:
                rows.append((owner, npc_id, pattern, notes, schedule))
            else:
                agreed += 1

    print(f"{compared} summon groups can be compared against a retail spawn action")
    print(f"{agreed} agree on both count and range")
    print(f"{len(rows)} do not\n")
    for owner, npc_id, pattern, notes, schedule in rows:
        extra = f"   [schedule={schedule}ms, not compared]" if schedule else ""
        print(f"  npc {owner} spawns {npc_id}  [{pattern[:34]}]{extra}")
        for n in notes:
            print(f"      {n}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
