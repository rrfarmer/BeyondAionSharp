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
`--gaps` is the one to use: for every rung it says whether the C# class for those npcs already answers
that skill id, and whether the skill's own template carries a hate or damage effect that may deliver the
behaviour without the AI needing to.

WHAT `is_event_skill_id` IS
---------------------------
"the skill that just hit me is this one". It appears only inside `on_spelled` and `on_see_spell`, and the
actions hung off it are `despawn_self`, `use_skill`, `say_to_all`, `spawn_id` and `broadcast_message` --
that is, an npc that dies to one specific player skill, or answers it, or shouts about it.

**This port already expresses the condition**, under aionemu's name for it: `Effect.ApplyEffect` calls
`OnEffectApplied`, which fires for every skill that lands and carries the skill id. `YumeAI`,
`IceChunkAI`, `AhserionAI` and the `Lv*HumanBeritraAI` classes switch on `effect.GetSkillId()` there
already. **Do not use the `Spelled` AI event for this** -- it is raised from the damage path only, so a
skill that deals no damage never raises it, and those are exactly the skills these rungs answer.

**"Affected" is not "missing", and this cost a commit to learn.** Some rungs are already implemented in a
C# class; others are delivered by the skill's own data rather than by any AI -- Laksyaka's provoke rung is
`switch_target ... points_to_add=2147483647`, and skill 20866 carries `<hostileup value="4000" .../>`,
which is the same thing. `--gaps` exists so that neither has to be rediscovered by writing the code twice.

WHAT `--gaps` FOUND WHEN WRITTEN
--------------------------------
```
232 is_event_skill_id rungs resolved to a skill
    8 already answered by the C# class for those npcs
   80 plausibly carried by the skill's own effects (hostileup / damage)
  144 OPEN -- neither
      of the OPEN ones, 14 sit on bound npcs running a named AI class
```

**14, not 379.** Most OPEN rungs are on patterns no npc is bound to, or on npcs running a generic AI where
a missing reaction is invisible. The 14 are concentrated in the Drakenspire/Seal instances -- the twin
protectors and Orissan answering `IDSeal_PCGuard_Dispel_All`, the Beritra ladder answering
`IDSeal_PCGuard_Dispel` and `IDSeal_SealGuard_Bomb`, `orissans_summon` on the glacier pair, and the
fortress gates on `IDRaksha_Invincible_Shield` and its dispel.

The "carried by skill data" test is deliberately loose -- it asks only whether the skill template has a
`hostileup`, `damage`, `spellatk` or `hate` element. It is a prompt to go and read the skill, not a
verdict, and 80 rows is far too many to take on trust.

Usage:  python extract_client_skills.py [--xml DIR] [--index | --resolve | --npcs | --gaps [--verbose]]
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
    ap.add_argument("--verbose", action="store_true", help="with --gaps, also print every row")
    ap.add_argument("--gaps", action="store_true",
                    help="per rung: is it already answered in C#, or carried by the skill's own data?")
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

    if args.gaps:
        uses, bind, classes = pattern_uses(args.xml), bindings(), ai_classes()
        handlers = REPO / "src" / "Aion.GameServer" / "Handlers" / "AI"
        sources = {f.stem.lower(): f.read_text(encoding="utf-8", errors="replace") for f in handlers.glob("*.cs")}
        by_ainame = {}
        for stem, text in sources.items():
            for name in re.findall(r'\[AIName\("([^"]+)"\)\]', text):
                by_ainame[name] = text
        skills = (REPO / "game-server" / "data" / "static_data" / "skills" / "skill_templates.xml").read_text(
            encoding="utf-8", errors="replace")
        answered = carried = open_rows = 0
        rows = []
        for p, devs in sorted(uses.items()):
            npcs = bind.get(p, [])
            names = sorted({classes.get(n) for n in npcs if classes.get(n)} - {None})
            for d in sorted(set(devs)):
                sid = index.get(d)
                if sid is None:
                    continue
                word = re.compile(r"\b" + str(sid) + r"\b")
                in_cs = any(word.search(by_ainame.get(a, "")) for a in names)
                start = skills.find(f'skill_id="{sid}"')
                block = ""
                if start >= 0:
                    open_tag = skills.rfind("<skill_template", 0, start)
                    block = skills[open_tag:skills.find("</skill_template>", start) + 17]
                self_carried = any(k in block for k in ("<hostileup", "<damage", "<spellatk", "<hate"))
                state = "answered in C#" if in_cs else ("carried by skill data" if self_carried else "OPEN")
                answered += in_cs
                carried += (not in_cs) and self_carried
                open_rows += state == "OPEN"
                rows.append((state, p, d, sid, ",".join(names) or "-", len(npcs)))
        print()
        print(f"{len(rows)} is_event_skill_id rungs resolved to a skill")
        print(f"  {answered} already answered by the C# class for those npcs")
        print(f"  {carried} plausibly carried by the skill's own effects (hostileup / damage)")
        print(f"  {open_rows} OPEN -- neither")

        # An OPEN rung on a pattern no npc is bound to, or on npcs that all run a generic AI, cannot be
        # acted on and should not be counted as work. Only the rest is a queue.
        generic = {"general", "aggressive", "aggressive_no_loot", "passive_npc", "dummy", "noaction", "-"}
        def actionable(row):
            return row[0] == "OPEN" and row[5] > 0 and any(
                a and a not in generic for a in row[4].split(","))
        queue = [r for r in rows if actionable(r)]
        print(f"  of the OPEN ones, {len(queue)} sit on bound npcs running a named AI class -- "
              f"the only ones worth opening")
        print()
        for state, p, d, sid, names, n in sorted(queue, key=lambda r: r[1]):
            print(f"  [{state:21s}] {p[:34]:36s} {d[:38]:40s} {sid:>6}  {names[:34]:36s} {n} npcs")
        if args.verbose:
            print()
            for state, p, d, sid, names, n in sorted(rows, key=lambda r: (r[0], r[1])):
                print(f"  [{state:21s}] {p[:34]:36s} {d[:38]:40s} {sid:>6}  {names[:34]:36s} {n} npcs")

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
