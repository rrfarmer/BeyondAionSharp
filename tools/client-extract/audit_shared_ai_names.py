"""Find AI classes shared by NPCs whose retail patterns disagree about what to spawn.

The missing-adds audit answers "does anything reach this npc id". It cannot see the
opposite failure: an AI class that reaches the *wrong* ids. That one is invisible from
inside the class, because everything it does is consistent -- it is just consistent with
somebody else's pattern.

The shape is always the same. Two NPCs share an `ai_name`, so they share a class. The
class hardcodes one set of add ids, taken from whichever pattern was read when it was
written. The other NPC's pattern names a different set, and those ids end up reachable by
nobody while its gate, boss or guard pours out the first NPC's adds.

That is exactly what happened to the illusion gates: 281226 and 284978 both carry
`ai_name="illusion_gate"`, their patterns are the same mechanic with different guard ids,
and the duke's gate spawned the chamber lord's guards. It was found by accident, through
the three unreachable ids it left behind. This finds the family on purpose.

What it reports, per shared `ai_name`:

  * the npcs on it and the retail pattern each one binds
  * the spawn devnames each pattern names, and which npc ids those resolve to
  * whether the sets differ

A difference is a *candidate*, not a bug. Plenty of shared names are generic behaviours
(`aggressive`, `servant`) where no class hardcodes anything, and plenty of others are one
mechanic whose variants legitimately share a table. What it buys is a short list to read.

CLI:
    python audit_shared_ai_names.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import (
    NAMEID_RE, NAME_RE, PATTERN_RE, SPAWN_RE, TEMPLATE_RE,
    attr, client_devname_to_id, is_effect_object, is_real_combatant, read_text,
)

# An `ai_name` on this many NPCs is a generic behaviour, not one encounter's class. The illusion
# gates are on two; the guard families reach into the hundreds and are table-driven by design.
MAX_SHARED = 12


def spawn_devnames_by_pattern(patterns_dir: pathlib.Path) -> dict[str, set[str]]:
    """pattern name -> the devnames its spawn actions reference."""
    out: dict[str, set[str]] = collections.defaultdict(set)
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            for action in SPAWN_RE.finditer(body):
                for dev in NAMEID_RE.findall(action.group(2)):
                    dev = dev.strip()
                    if dev and not is_effect_object(dev):
                        out[m.group(1)].add(dev)
    return out


def classes_by_ai_name(repo: pathlib.Path) -> dict[str, str]:
    """`ai_name` -> the file that declares it, for the names our own code implements."""
    out: dict[str, str] = {}
    for path in (repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs"):
        for name in re.findall(r'\[AIName\("([^"]+)"\)\]', read_text(path)):
            out[name] = path.name
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    repo = pathlib.Path(args.repo)
    implemented = classes_by_ai_name(repo)

    pattern_of: dict[str, str] = {}
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) >= 4:
            pattern_of[parts[0]] = parts[3]

    dev2id = client_devname_to_id(pathlib.Path(args.client_root))
    spawns = spawn_devnames_by_pattern(pathlib.Path(args.patterns_dir))

    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(
        read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml"))}

    on_name: dict[str, list[str]] = collections.defaultdict(list)
    for npc_id, attrs in templates.items():
        ai = attr(attrs, "ai")
        if ai in implemented:
            on_name[ai].append(npc_id)

    print(f"ai_names our code implements : {len(implemented):,}")
    print(f"  of those, shared by 2..{MAX_SHARED} npcs : "
          f"{sum(1 for v in on_name.values() if 2 <= len(v) <= MAX_SHARED)}\n")

    flagged = 0
    for ai_name in sorted(on_name):
        npcs = sorted(on_name[ai_name], key=int)
        if not 2 <= len(npcs) <= MAX_SHARED:
            continue

        # What each npc's own pattern says it spawns, as npc ids we can compare.
        sets: dict[str, frozenset[str]] = {}
        for npc_id in npcs:
            pattern = pattern_of.get(npc_id)
            if not pattern:
                continue
            ids = {dev2id[d.lower()] for d in spawns.get(pattern, ()) if d.lower() in dev2id}
            ids = {i for i in ids if i in templates and is_real_combatant(templates[i])}
            if ids:
                sets[npc_id] = frozenset(ids)

        if len(set(sets.values())) < 2:
            continue

        flagged += 1
        print(f"{ai_name}  ({implemented[ai_name]})")
        for npc_id in npcs:
            if npc_id not in sets:
                continue
            name = attr(templates[npc_id], "name")
            print(f"    {npc_id}  {name:28s} {pattern_of.get(npc_id, '?'):38s} "
                  f"-> {','.join(sorted(sets[npc_id]))}")
        print()

    print(f"shared names whose patterns disagree about what to spawn: {flagged}")


if __name__ == "__main__":
    main()
