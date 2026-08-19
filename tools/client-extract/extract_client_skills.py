#!/usr/bin/env python3
"""Resolve the skill devnames the AI patterns use into the skill ids this port ships.

WHY THIS EXISTS
---------------
The AI patterns name skills two ways. `SKILLI_INDEX_N` is an index into an npc's own skill list and has
blocked several audits for a long time. The other way is a **devname string**:

    <is_event_skill_id><skill_id>Q_IDLF1_BrownieBomb</skill_id></is_event_skill_id>

which is not an index and not an id -- until you find that `Map/XML/skill_base.xml` carries the join:

    <skill_base><id>18130</id><name>Q_IDLF1_BrownieBomb</name>...

That file is 38 million characters of UTF-16, which is why nothing had matched in it before: `grep`
returns nothing at all on these files rather than failing, so an absent result reads as an answer. Use
`read_text` (from `audit_missing_adds`) for anything under `Map/XML`.

WHAT IT DOES
------------
`--index` builds and reports the devname -> id table.
`--resolve` takes every `is_event_skill_id` in the AI patterns, resolves it, and says whether the id
exists in our own `skill_templates.xml`.
`--npcs` lists the npcs on a pattern that reacts to a named skill, with the AI class each one runs.

WHAT `is_event_skill_id` IS
---------------------------
"the skill that just hit me is this one". It appears only inside `on_spelled` and `on_see_spell`, and the
actions hung off it are `despawn_self`, `use_skill`, `say_to_all`, `spawn_id` and `broadcast_message` --
that is, an npc that dies to one specific player skill, or answers it, or shouts about it.

**This port cannot express the condition yet**, whatever this tool resolves. `CreatureController` raises
`AiEventType.Spelled` with the attacker and nothing else, so the AI is told that it was spelled but not
with what. Closing that needs the skill id threaded through the event, which is engine work.

Usage:  python extract_client_skills.py [--xml DIR] [--index | --resolve | --npcs]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
SKILL_BASE = re.compile(r"<skill_base>\s*<id>(\d+)</id>\s*<name>([^<]+)</name>", re.S)
EVENT_SKILL = re.compile(r"<is_event_skill_id>\s*<skill_id>([^<]+)</skill_id>", re.S)
PATTERN = re.compile(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", re.S)


def devname_index(xml_dir):
    """devname -> client skill id, from skill_base.xml."""
    text = read_text(pathlib.Path(xml_dir) / "skill_base.xml")
    out = {}
    for skill_id, name in SKILL_BASE.findall(text):
        out.setdefault(name, int(skill_id))
    return out


def our_skill_ids():
    text = (REPO / "game-server" / "data" / "static_data" / "skills" / "skill_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    return {int(s) for s in re.findall(r'skill_id="(\d+)"', text)}


def pattern_uses(xml_dir):
    """pattern name -> [devname, ...] for every is_event_skill_id in it."""
    out = collections.defaultdict(list)
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN.finditer(read_text(f)):
            names = EVENT_SKILL.findall(m.group(2))
            if names:
                out[m.group(1)].extend(names)
    return out


def bindings():
    out = collections.defaultdict(list)
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in tsv.read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) > 2:
            out[parts[2]].append(parts[0])
    return out


def ai_classes():
    text = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    return dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]+)"', text))


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--index", action="store_true", help="report the devname -> id table")
    ap.add_argument("--resolve", action="store_true", help="resolve every is_event_skill_id use")
    ap.add_argument("--npcs", action="store_true", help="list affected npcs and their AI classes")
    args = ap.parse_args()

    index = devname_index(args.xml)
    if not index:
        print("skill_base.xml yielded no names -- check the path, and that read_text decoded UTF-16")
        return 1

    if args.index or not (args.resolve or args.npcs):
        print(f"{len(index)} skill devnames in skill_base.xml")
        ours = our_skill_ids()
        known = sum(1 for v in index.values() if v in ours)
        print(f"{known} of them name a skill this port ships ({100 * known // len(index)}%)")

    if args.resolve:
        uses = pattern_uses(args.xml)
        ours = our_skill_ids()
        flat = [(p, d) for p, ds in uses.items() for d in ds]
        resolved = [(p, d, index[d]) for p, d in flat if d in index]
        shipped = [r for r in resolved if r[2] in ours]
        print(f"\n{len(flat)} is_event_skill_id uses across {len(uses)} patterns")
        print(f"{len(resolved)} resolve to a client skill id ({100 * len(resolved) // max(1, len(flat))}%)")
        print(f"{len(shipped)} of those ids exist in our skill_templates.xml")
        missing = sorted({d for p, d in flat if d not in index})
        if missing:
            print(f"\n{len(missing)} devnames not in skill_base.xml:")
            for d in missing[:20]:
                print(f"   {d}")

    if args.npcs:
        uses, bind, classes = pattern_uses(args.xml), bindings(), ai_classes()
        generic = {"general", "aggressive", "aggressive_no_loot", "passive_npc", "dummy", ""}
        rows = collections.Counter()
        for p in uses:
            for npc in bind.get(p, []):
                ai = classes.get(npc)
                if ai and ai not in generic:
                    rows[ai] += 1
        total = sum(len(bind.get(p, [])) for p in uses)
        print(f"\n{total} npcs sit on a pattern that reacts to a named skill; "
              f"{sum(rows.values())} of them run a non-generic AI class")
        for ai, n in rows.most_common():
            print(f"   {ai:44s} {n}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
