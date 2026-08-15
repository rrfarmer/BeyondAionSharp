"""Report whether a boss's retail skill indices can be resolved against our data.

Patterns address skills as `SKILLI_INDEX_n` into the NPC's client-side list, which
we do not have (see docs/retail-ai-fidelity.md). Our own `npc_skills.xml` list is
the only stand-in, so an index beyond its length cannot be identified at all.

That is the gate on porting a timer-driven boss: a rotation whose highest index
sits outside our list can be reproduced in shape but not in content, and writing
it would mean inventing the casts. This reports the reach per boss so that is
visible before the work starts rather than halfway through it.

CLI:
    python audit_skill_index_reach.py <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text

SKILL_INDEX_RE = re.compile(r"<skill>SKILLI_INDEX_(\d+)</skill>")
TIMER_RE = re.compile(r"<btimer_indicator>")
SPAWN_RE = re.compile(r"<npc_nameid>")
AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')


def our_skill_counts(repo: pathlib.Path) -> dict[str, int]:
    """npc_id -> number of skills we list for it, across every npc_skills file."""
    counts: dict[str, int] = collections.defaultdict(int)
    for path in (repo / "game-server/data/static_data/npc_skills").rglob("*.xml"):
        text = read_text(path)
        for block in re.finditer(r'<npc_skills npc_ids="([^"]+)">(.*?)</npc_skills>', text, re.S):
            n = len(re.findall(r"<npc_skill\b", block.group(2)))
            for npc_id in block.group(1).split():
                counts[npc_id] += n
    return counts


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
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
            )

    counts = our_skill_counts(repo)
    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
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
            top, timers, spawns = entry
            ours = counts.get(npc_id, 0)
            rows.append((path.name, npc_id, binding[npc_id], top, ours, timers, spawns))
            break

    portable = [r for r in rows if r[3] >= 0 and r[3] < r[4]]
    partial = [r for r in rows if r[3] >= 0 and r[3] >= r[4]]

    print(f"timer-driven bosses with an AI class: {len(rows)}")
    print(f"  every index within our skill list : {len(portable)}")
    print(f"  reaches past it                   : {len(partial)}\n")
    for label, group in (("PORTABLE", portable), ("INDEXES BEYOND OUR LIST", partial)):
        print(f"== {label} ==")
        for fname, npc_id, pattern, top, ours, timers, spawns in sorted(group, key=lambda r: r[3] - r[4]):
            print(f"  {fname:<34} npc {npc_id}  top index {top}, our list {ours}, "
                  f"{timers} timer branches, {spawns} spawns  [{pattern}]")
        print()


if __name__ == "__main__":
    main()
