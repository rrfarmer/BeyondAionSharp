#!/usr/bin/env python3
"""Check the npcs our boss AIs summon against the npcs their retail pattern actually names.

WHY THIS IS POSSIBLE NOW
------------------------
Retail's `spawn` action names its target by devname, not id:

    <spawn><npc_nameid>BIDSeal_Twin_P_Source</npc_nameid><num_to_spawn>1</num_to_spawn>...

which was unusable until `ai_binding.tsv` turned out to be a devname -> npc_id table in its own right --
69,184 of them, because it lists every npc that carries an AI pattern. **6,457 distinct devnames are
referenced by spawn actions across 17,869 uses, and 92% of them resolve.**

That makes "which npc does this boss summon, and how many" answerable from the pattern data for the first
time. Every add, every wave, every hazard twin.

WHAT IT DOES
------------
For each C# AI class in `Handlers/AI`, it collects the npc ids that appear as integer literals in the
source, and compares them against the ids the retail pattern for that class's npcs spawns.

- **missing**: retail spawns an npc id that never appears in the class.
- **extra**: the class names an npc id retail's spawn actions never mention.

WHAT IT IS NOT
--------------
Not a defect list. Three reasons a clean class shows up here:

- An id can reach the class from spawn data or a template rather than a literal, and this only reads
  literals.
- `num_to_spawn` and the id can be right while the *trigger* is wrong, which this does not look at.
- Retail often spawns an FX controller and a damage twin where this port collapses both into one npc --
  the "FX/DMG collapse" noted throughout `docs/retail-ai-fidelity.md`. Those show as **missing** and are
  correct as they stand.

So it is a reading list ordered by how much a class disagrees with its pattern, not a to-do list.

Usage:  python audit_summon_ids.py [--xml DIR] [--class NAME] [--limit N]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
NAMEID = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")
PATTERN = re.compile(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", re.S)
AINAME = re.compile(r'\[AIName\("([^"]+)"\)\]')
LITERAL = re.compile(r"\b(\d{6})\b")


def devname_to_npc():
    out = {}
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 1 and parts[1]:
            out.setdefault(parts[1], parts[0])
    return out


def pattern_spawns(xml_dir):
    """pattern name -> set of devnames it spawns."""
    out = collections.defaultdict(set)
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN.finditer(read_text(f)):
            names = NAMEID.findall(m.group(2))
            if names:
                out[m.group(1)].update(names)
    return out


def npc_pattern_and_ai():
    """npc_id -> pattern name, and npc_id -> our ai name."""
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    pat = {}
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 3 and parts[3]:
            pat[parts[0]] = parts[3]
    text = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai = dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]+)"', text))
    return pat, ai


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--class", dest="only", help="report one AI name in full")
    ap.add_argument("--limit", type=int, default=25)
    ap.add_argument("--max-patterns", type=int, default=3,
                    help="skip AI classes serving more patterns than this (infrastructure)")
    args = ap.parse_args()

    dev2npc = devname_to_npc()
    spawns = pattern_spawns(args.xml)
    npc2pat, npc2ai = npc_pattern_and_ai()

    sources = {}
    for f in (REPO / "src" / "Aion.GameServer" / "Handlers" / "AI").glob("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for name in AINAME.findall(text):
            sources[name] = (f.name, text)

    generic = {"general", "aggressive", "aggressive_no_loot", "passive_npc", "dummy", "noaction"}
    wanted = collections.defaultdict(set)
    patterns_per_ai = collections.defaultdict(set)
    for npc, ai in npc2ai.items():
        if ai in generic or ai not in sources:
            continue
        pattern = npc2pat.get(npc)
        if not pattern:
            continue
        patterns_per_ai[ai].add(pattern)
        for dev in spawns.get(pattern, ()):
            resolved = dev2npc.get(dev)
            if resolved:
                wanted[ai].add(resolved)

    # A class shared by dozens of patterns is fortress or event infrastructure: its npc ids come from
    # spawn data keyed by race and location, never from literals, so every one of them reads as missing
    # and drowns the report. The signal is in classes serving a handful of patterns -- a named boss and
    # its adds, where the ids ARE written down in the class.
    rows, shared = [], 0
    for ai, ids in wanted.items():
        if len(patterns_per_ai[ai]) > args.max_patterns:
            shared += 1
            continue
        filename, text = sources[ai]
        literals = set(LITERAL.findall(text))
        missing = sorted(ids - literals)
        extra = sorted(i for i in literals if i not in ids and i in npc2ai)
        if missing or extra:
            rows.append((len(missing), ai, filename, missing, extra))

    focused = len(wanted) - shared
    print(f"{len(wanted)} named AI classes have a retail pattern that spawns something resolvable")
    print(f"{shared} serve more than {args.max_patterns} patterns and are skipped as infrastructure")
    print(f"{focused - len(rows)} of the remaining {focused} name every npc their pattern spawns")
    print(f"{len(rows)} disagree -- read them, do not trust them (see the docstring)\n")

    for count, ai, filename, missing, extra in sorted(rows, key=lambda r: -r[0])[:args.limit]:
        if args.only and ai != args.only:
            continue
        print(f"{filename}  [{ai}]  ({len(patterns_per_ai[ai])} pattern(s))")
        if missing:
            print(f"    retail spawns, class never names : {' '.join(missing)}")
        if extra:
            print(f"    class names, retail never spawns : {' '.join(extra[:12])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
