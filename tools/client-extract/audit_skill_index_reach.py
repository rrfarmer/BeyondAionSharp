"""Report whether a boss's retail skill indices can be resolved against our data.

Patterns address skills as `SKILLI_INDEX_n` into the NPC's client-side list, which
we do not have (see docs/retail-ai-fidelity.md). Our own `npc_skills.xml` list is
the only stand-in, so an index beyond its length cannot be identified at all.

That is the gate on porting a timer-driven boss: a rotation whose highest index
sits outside our list can be reproduced in shape but not in content, and writing
it would mean inventing the casts. This reports the reach per boss so that is
visible before the work starts rather than halfway through it.

The gate is necessary, not sufficient. Passing it means our list is *long enough*
to hold the indices a pattern names; it says nothing about whether our list is in
retail's order. Several of ours are aionemu chain constructions ordered by
`chain_id` and HP band, which is not a retail skill list at all. So a boss that
passes here still needs its mapping corroborated from something else -- the
branch comment, the skill's stack name, the `skill_no` in npc_shouts, or what the
branch spawns alongside the cast -- before any index is written down as a skill.

CLI:
    python audit_skill_index_reach.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

from audit_missing_adds import NAME_RE, PATTERN_RE, TEMPLATE_RE, attr, read_text
from triage_missing_adds import devname_to_id

SKILL_INDEX_RE = re.compile(r"<skill>SKILLI_INDEX_(\d+)</skill>")
NAMEID_VALUE_RE = re.compile(r"<npc_nameid>([^<]*)</npc_nameid>")
BLANK_NAME_ID = "350000"  # the "no name" string, which marks an invisible control NPC
TIMER_RE = re.compile(r"<btimer_indicator>")
SPAWN_RE = re.compile(r"<npc_nameid>")
AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')


SKILL_ID_RE = re.compile(r'<npc_skill[^>]*\bid="(\d+)"')


def our_skill_counts(repo: pathlib.Path) -> dict[str, int]:
    """npc_id -> how many *distinct* skills we list for it, across every npc_skills file.

    Distinct, not entries. Many of our lists are aionemu chain constructions rather than
    flat skill lists: Tahabata Pyrelord has fifteen entries built from nine skills, with
    18225 repeated four times across four `chain_id` sequences. Counting entries made his
    list look long enough to resolve a retail index of 10 when it is not, and the whole
    point of this audit is to refuse exactly that.
    """
    skills: dict[str, set[str]] = collections.defaultdict(set)
    for path in (repo / "game-server/data/static_data/npc_skills").rglob("*.xml"):
        text = read_text(path)
        for block in re.finditer(r'<npc_skills npc_ids="([^"]+)">(.*?)</npc_skills>', text, re.S):
            found = set(SKILL_ID_RE.findall(block.group(2)))
            for npc_id in block.group(1).split():
                skills[npc_id].update(found)
    return {npc_id: len(found) for npc_id, found in skills.items()}


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    binding: dict[str, str] = {}
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        binding[npc_id] = pattern

    reach: dict[str, tuple[int, int, int]] = {}
    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            indices = [int(i) for i in SKILL_INDEX_RE.findall(body)]
            reach[m.group(1)] = (
                max(indices) if indices else -1,
                len(TIMER_RE.findall(body)),
                len(SPAWN_RE.findall(body)),
                [d.strip() for d in NAMEID_VALUE_RE.findall(body) if d.strip()],
            )

    counts = our_skill_counts(repo)
    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(tpl)}
    dev2id = devname_to_id(pathlib.Path(args.client_root))
    by_ai = collections.defaultdict(list)
    for npc_id, ai in re.findall(r'<npc_template npc_id="(\d+)"[^>]*?ai="([^"]+)"', tpl):
        by_ai[ai].append(npc_id)

    rows = []
    for path in (repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs"):
        name = AINAME_RE.search(read_text(path))
        if not name:
            continue
        for npc_id in by_ai.get(name.group(1), []):
            entry = reach.get(binding.get(npc_id, ""))
            if not entry or entry[1] < 10:  # only timer-driven bosses
                continue
            top, timers, spawns, devnames = entry
            ours = counts.get(npc_id, 0)
            # A mechanic routed through an invisible control NPC cannot be ported by spawning that NPC:
            # it does nothing without its own pattern, so the chain has to be ported with it. Judge that
            # from name_id, not from the devname: Captain Xasta's summon is called
            # IDYun_Rasta_Sum_Invisible and is a perfectly visible level-60 siege artilleryman.
            invisible = sum(1 for d in devnames
                            if attr(templates.get(dev2id.get(d.lower(), ""), ""), "name_id") == BLANK_NAME_ID)
            rows.append((path.name, npc_id, binding[npc_id], top, ours, timers, spawns, invisible))
            break

    portable = [r for r in rows if r[3] >= 0 and r[3] < r[4]]
    partial = [r for r in rows if r[3] >= 0 and r[3] >= r[4]]

    clean = [r for r in portable if r[7] == 0]
    controllers = [r for r in portable if r[7] > 0]

    print(f"timer-driven bosses with an AI class : {len(rows)}")
    print(f"  every index within our skill list  : {len(portable)}")
    print(f"    of those, no invisible controller: {len(clean)}")
    print(f"    routed through controllers       : {len(controllers)}")
    print(f"  reaches past our skill list        : {len(partial)}\n")

    for label, group in (("PORTABLE, NO CONTROLLER CHAIN", clean),
                         ("PORTABLE BUT ROUTED THROUGH INVISIBLE CONTROLLERS", controllers),
                         ("INDEXES BEYOND OUR SKILL LIST", partial)):
        print(f"== {label} ({len(group)}) ==")
        for fname, npc_id, pattern, top, ours, timers, spawns, invis in sorted(
                group, key=lambda r: r[3] - r[4]):
            note = f", {invis} invisible" if invis else ""
            print(f"  {fname:<34} npc {npc_id}  top index {top}, our list {ours}, "
                  f"{timers} timers, {spawns} spawns{note}  [{pattern}]")
        print()


if __name__ == "__main__":
    main()
