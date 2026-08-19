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

INERT BLOCKS
------------
**Only `SummonerAI` and `CaptainLakharaAI` read summon groups.** A block whose npc runs some other AI
is never consulted, so both its right values and its wrong ones are equally invisible in play. Nine of
the 73 blocks in `spawn_helpers.xml` are inert that way, and one of them -- adjutant ursanafi, whose
npc runs `guard_reinforcement` -- was reported here as a defect and "fixed" before anyone noticed the
file was not the live path. His real reinforcements live in the generated `GuardReinforcements.cs`,
where a different bug had eaten half of them.

Rows are marked `[INERT]` so that never costs anyone the same hour twice. An inert row is still worth
correcting -- wrong data reads as a fact -- but it is not a behaviour fix, and it should not be pinned
with a test that claims it is.

Usage:  python audit_summon_numbers.py [--xml DIR]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402
from audit_summon_ids import FX_WORDS, devname_to_npc, spawned_in_our_data  # noqa: E402
from client_npc_names import npc_names, unattackable_ids  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
PATTERN = re.compile(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", re.S)
SPAWN_ACTION = re.compile(r"<spawn>(.*?)</spawn>", re.S)
AI_BLOCK = re.compile(r'<ai npcId="(\d+)">(.*?)</ai>', re.S)
PERCENTAGE = re.compile(r'<percentage percent="(\d+)"[^>]*>(.*?)</percentage>', re.S)
GROUP = re.compile(r'<summonGroup ([^/>]*)/?>')


def our_groups():
    """summoner npc id -> [(spawned npc id, min, max, distance, schedule)], summed within each band.

    **THE BAND IS THE UNIT ON OUR SIDE, AS THE RUNG IS ON RETAIL'S.**

    A `<percentage>` block that wants four guards can say so in two ways: one group with
    `minCount="4"`, or four groups of one. Both are used in this file, and the second is not a quirk --
    a group carries a single `x`/`y`/`z`, so a band whose adds each need their own point *has* to be
    written as several groups.

    Comparing group by group therefore reported Grand Chieftain Kasika as fifteen separate defects:
    2, 3, 4 and 6 guards, each written as that many single-count groups at distinct coordinates, each
    read as "count 1-1 vs retail rungs [2]". His data is exactly right and had been corrected earlier in
    this same work.

    Summing **within one band** is not the aggregation this module's header warns about. That warning is
    about summing **across** bands -- merging "two at seventy-five" with "two at fifty" into four --
    which destroyed the comparison and dropped agreement to 22. A band is one health threshold, and
    retail's rung is one condition set; those are the same unit.
    """
    path = REPO / "game-server" / "data" / "static_data" / "ai" / "spawn_helpers.xml"
    out = collections.defaultdict(list)
    for block in AI_BLOCK.finditer(path.read_text(encoding="utf-8", errors="replace")):
        owner = block.group(1)
        for band in PERCENTAGE.finditer(block.group(2)):
            totals = collections.Counter()
            highs = collections.Counter()
            distances = {}
            schedules = {}
            for group in GROUP.finditer(band.group(2)):
                attrs = dict(re.findall(r'(\w+)="([^"]*)"', group.group(1)))
                if "npcId" not in attrs:
                    continue
                npc_id = attrs["npcId"]
                lo = int(attrs.get("minCount", 1))
                totals[npc_id] += lo
                highs[npc_id] += int(attrs.get("maxCount", lo))
                distances.setdefault(npc_id, float(attrs.get("distance", 0)))
                schedules.setdefault(npc_id, int(attrs.get("schedule", 0)))
            for npc_id, lo in totals.items():
                out[owner].append((npc_id, lo, highs[npc_id], distances[npc_id], schedules[npc_id]))
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


def report_tiers(ours, theirs, of, ai_of):
    """Summoners whose retail pattern spawns an npc our data never spawns for them.

    This is a different question from count and range, and the one that found the two worst summon
    defects so far. Both were the same shape: a boss with several **tiers** of add, one per health band,
    where our data uses a single tier for every band and the others are never spawned at all.

    * Grand Chieftain Kasika: retail escalates 280469, 280470, 280471, 280472 by band. **280470 and
      280471 appeared nowhere in our data.**
    * Spirit King Agro: retail summons 280772 between 30 and 75 and 280771 below 30. **280772 appeared
      nowhere.**

    A count comparison cannot see either, because the wrong npc is present in a plausible number. Only
    comparing the *sets* does.

    THE SAME FILTERS APPLY HERE AS IN `audit_summon_ids.py`, AND FOR THE SAME REASON
    ---------------------------------------------------------------------------------
    The first version of this report had none of them and put **Station_NinjaNM at the top of the list**,
    with a fully disjoint set: retail "names 217377 and 217378", ours spawns 217379, 217380 and 217381,
    not one id in common. That looks like the worst tier swap yet.

    > It is not a defect at all. Our block is **exactly right** -- one 217380 at 90, three 217379 at 70,
    > two 217381 at 50, all at range 5. The two ids ours "never spawns" are
    > `IDStation_DrakanNinja_CTRL_1` and `_CTRL_2`: FX controllers, which this port collapses.

    So a set difference means nothing until the FX markers and the ids our spawn tables place directly are
    taken out of it. Both filters were already written for the other audit; not reusing them here would
    have sent somebody to rewrite correct data, which is the failure this whole document keeps recording.
    """
    placed = spawned_in_our_data()
    devname_of = {npc_id: devname for devname, npc_id in npc_names().items()}
    # The client's own unattackable flag, alongside the devname markers rather than instead of them.
    # Chief gunner koakoa's five SumA..SumE devnames carry no FX marker and read exactly like a
    # five-tier collapse; all five are unattackable markers that place the one attackable bomb our data
    # already spawns. See client_npc_names.unattackable_ids.
    furniture = unattackable_ids()
    rows = []
    for owner, groups in sorted(ours.items()):
        pattern = of.get(owner)
        if not pattern or pattern not in theirs:
            continue
        mine = {npc_id for npc_id, *_ in groups}
        retail = set(theirs[pattern])
        unspawned = [n for n in sorted(retail - mine)
                     if n not in placed
                     and n not in furniture
                     and not any(w.lower() in devname_of.get(n, "").lower() for w in FX_WORDS)]
        if unspawned:
            rows.append((owner, pattern, unspawned, sorted(mine)))
    print(f"{len(rows)} summoners whose pattern spawns an npc our data never spawns for them")
    print("Each is a candidate tier swap: check whether ours uses one tier for every health band.")
    print()
    for owner, pattern, unspawned, mine in rows:
        named = " ".join(f"{n}({devname_of.get(n, '?')[:26]})" for n in unspawned)
        dead = "" if ai_of.get(owner) in SUMMON_READING_AI else f" [INERT ai={ai_of.get(owner)}]"
        print(f"  npc {owner}  [{pattern[:34]:36s}]{dead} never spawns {named}")
        print(f"        ours spawn: {' '.join(mine)}")


SUMMON_READING_AI = frozenset((
    # The AI names whose class reaches DataManager.AI_DATA...GetSummons(), i.e. everything deriving
    # from SummonerAI, plus CaptainLakharaAI which reads the groups itself.
    "adjutant_galamat", "ashunatal_shadowslip", "brasseyegrogget", "captain_jarka", "captain_lakhara",
    "commander_bakarma", "dynatoum", "enraged_agent", "eternal_bastion_summoner", "gunnerkoakoa",
    "kaluva", "infernal_dynatoum", "mistressviloa", "queen_serusia", "summoner", "vallakhan",
))


def ai_by_npc():
    """npc id -> the `ai` attribute on its template, for spotting blocks nothing reads."""
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
    ap.add_argument("--tiers", action="store_true",
                    help="list summoners whose retail pattern spawns an npc our data never spawns")
    args = ap.parse_args()

    dev = devname_to_npc(args.xml)
    ours = our_groups()
    theirs = retail_spawns(args.xml, dev)
    of = patterns_by_npc()
    ai_of = ai_by_npc()

    if args.tiers:
        report_tiers(ours, theirs, of, ai_of)
        return 0

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
                rows.append((owner, npc_id, pattern, notes, schedule,
                             ai_of.get(owner) in SUMMON_READING_AI))
            else:
                agreed += 1

    print(f"{compared} summon groups can be compared against a retail spawn action")
    print(f"{agreed} agree on both count and range")
    print(f"{len(rows)} do not\n")
    print(f"  ({sum(1 for r in rows if not r[5])} of them on an npc whose ai never reads this file)\n")
    for owner, npc_id, pattern, notes, schedule, live in rows:
        extra = f"   [schedule={schedule}ms, not compared]" if schedule else ""
        extra += "" if live else "   [INERT]"
        print(f"  npc {owner} spawns {npc_id}  [{pattern[:34]}]{extra}")
        for n in notes:
            print(f"      {n}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
