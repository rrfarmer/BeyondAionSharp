"""Report NPCs left mute because their shout lines sit on a twin we never spawn.

Retail content routinely ships an NPC twice: one npc_id the world actually
places, and a near-identical one that is unused. Both carry the same
<ai_name>, so both run the same retail AI pattern, but our npc_shouts.xml often
binds the encounter's spoken lines to only one of them -- and sometimes that is
the twin nothing spawns. The live NPC is then silent for the whole fight, and
the shout data is dead weight.

Hamerun the Bleeder was found this way: all five of his lines were bound to
282040, which no spawn places, while players fight 216922.

For every shout group whose NPCs are all unspawnable, this reports any live
NPC that shares the same retail pattern and could be given those lines.

CLI:
    python audit_dead_shouts.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

from audit_missing_adds import TEMPLATE_RE, attr, read_text, spawnable_npc_ids

GROUP_RE = re.compile(r'<shout_group\b[^>]*client_ai="([^"]*)"[^>]*>(.*?)</shout_group>', re.S)
NPCS_RE = re.compile(r'<shout_npcs\b[^>]*npc_ids="([^"]*)"[^>]*>(.*?)</shout_npcs>', re.S)
SHOUT_RE = re.compile(r"<shout\b")


def load_binding(path: pathlib.Path) -> dict[str, list[str]]:
    """lowercased pattern name -> npc_ids the client says run it."""
    out = collections.defaultdict(list)
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        out[pattern.lower()].append(npc_id)
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    static = repo / "game-server/data/static_data"
    by_pattern = load_binding(pathlib.Path(args.binding_tsv))
    spawnable = spawnable_npc_ids(repo)
    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(
        read_text(static / "npcs/npc_templates.xml"))}

    shouts = read_text(static / "npc_shouts/npc_shouts.xml")
    findings = []
    groups = 0

    for group in GROUP_RE.finditer(shouts):
        client_ai, body = group.group(1), group.group(2)
        if not client_ai.strip():
            continue
        groups += 1

        bound: list[str] = []
        line_count = 0
        for block in NPCS_RE.finditer(body):
            ids = block.group(1).split()
            bound.extend(ids)
            line_count += len(SHOUT_RE.findall(block.group(2)))
        if not bound or line_count == 0:
            continue
        if any(npc_id in spawnable for npc_id in bound):
            continue  # at least one bound NPC is live, so the lines can be heard

        # Nothing bound is ever spawned. Does a live NPC run the same pattern?
        live = [n for n in by_pattern.get(client_ai.lower(), []) if n in spawnable]
        if live:
            findings.append((client_ai, bound, line_count, live))

    print(f"shout groups examined            : {groups:,}")
    print(f"groups whose lines can never play: {len(findings)}\n")
    for client_ai, bound, line_count, live in sorted(findings, key=lambda f: -f[2]):
        print(f"{client_ai}  ({line_count} lines, bound to {','.join(bound)} which never spawn)")
        for npc_id in live[:6]:
            name = attr(templates.get(npc_id, ""), "name") or "?"
            print(f"    live: {npc_id}  {name}")


if __name__ == "__main__":
    main()
