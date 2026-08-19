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

**Missing ids are split by whether they could be placed at all.** A spawn action that carries a
`pathname` is not a placement, it is the start of a walk; if the client does not define that route,
spawning the npc leaves it standing where retail has it charging. Those are reported separately, because
"nobody wrote it" and "it cannot be written from this data" are different queues and mixing them makes
the larger one look like work.

Tiamat's hard mode is the case that made this worth reporting: six of its seven unnamed ids are the
nineteen-drakan rush, and **all twelve of its `path_tiamatdrakan_*` routes are absent from the client**.
That was already written by hand in `TiamatDragonHardAI`'s comments; this makes the same check a query
for the other 227 rows.

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
SPAWN_BLOCK = re.compile(r"<spawn>(.*?)</spawn>", re.S)
SPAWN_PATH = re.compile(r"<pathname>([^<]*)</pathname>")
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


def spawns_needing_a_route(xml_dir):
    """devname -> the pathnames its spawn actions hang on it.

    A spawn that carries a `pathname` is not a placement, it is the start of a walk. If the client does
    not define that route, spawning the npc anyway leaves it standing where retail has it charging, and
    the encounter is differently wrong rather than partly right. Tiamat's nineteen-drakan rush is the
    case that made this worth reporting: all twelve of its `path_tiamatdrakan_*` routes are absent.
    """
    out = collections.defaultdict(set)
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for block in SPAWN_BLOCK.finditer(read_text(f)):
            body = block.group(1)
            name = NAMEID.search(body)
            path = SPAWN_PATH.search(body)
            if name and path and path.group(1):
                out[name.group(1)].add(path.group(1))
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


def rank_missing(rows, route_blocked, xml_dir):
    """Sort every unnamed id by whether it looks like an add or an effect.

    Three rows read by hand -- Tiamat hard, Lord Beritra and Laksyaka -- all came back mostly effects, so
    the useful question stopped being "how many ids" and became "which of them is a monster". These are
    the signals that separated them in those three:

    * **a rating.** Effects are NORMAL or carry none; the things players fight are ELITE, HERO or LEGENDARY.
    * **a pattern of its own.** An npc retail gives no AI pattern does nothing but exist and expire.
    * **a name.** Display npcs are blank or carry an untranslated devname full of underscores.
    * **a lifetime.** A few seconds is an effect; live_time 0 is something that stays.

    None is conclusive alone and the ranking says so; it exists to put the twenty worth reading first.
    """
    text = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    rating, named_ai, npc_name = {}, {}, {}
    for m in re.finditer(r'<npc_template ([^>]*)>', text):
        a = m.group(1)
        nid = re.search(r'npc_id="(\d+)"', a)
        if not nid:
            continue
        r = re.search(r'rating="([^"]*)"', a)
        ai = re.search(r'ai="([^"]*)"', a)
        nm = re.search(r'name="([^"]*)"', a)
        rating[nid.group(1)] = r.group(1) if r else ""
        named_ai[nid.group(1)] = ai.group(1) if ai else ""
        npc_name[nid.group(1)] = nm.group(1) if nm else ""

    has_pattern = set()
    for line in (REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv").read_text(
            encoding="utf-8").splitlines()[1:]:
        parts = line.split("	")
        if len(parts) > 3 and parts[3]:
            has_pattern.add(parts[0])

    scored = []
    for _, ai, filename, missing, _ in rows:
        for nid in missing:
            if nid in route_blocked:
                continue
            score, why = 0, []
            if rating.get(nid) in ("ELITE", "HERO", "LEGENDARY"):
                score += 3; why.append(rating[nid])
            if nid in has_pattern:
                score += 2; why.append("has pattern")
            name = npc_name.get(nid, "")
            if name.strip() and "_" not in name:
                score += 1; why.append("named")
            ai_name = named_ai.get(nid)
            # An id absent from npc_templates entirely scores nothing here and is called out instead: it
            # is a different problem from an unimplemented add, and 857599 is one.
            if ai_name is None:
                why.append("NOT IN npc_templates")
            elif ai_name not in ("general", "noaction", "", "aggressive"):
                score += 1; why.append(ai_name)
            scored.append((score, nid, filename, ai, name, ", ".join(why)))

    scored.sort(key=lambda s: (-s[0], s[2]))
    print(f"{len(scored)} unnamed ids that are not route-blocked, ranked")
    print()
    for score, nid, filename, ai, name, why in scored[:30]:
        print(f"  {score}  {nid}  {filename[:34]:36s} {name[:26]:28s} {why}")
    buckets = collections.Counter(s[0] for s in scored)
    print()
    print("score distribution:", dict(sorted(buckets.items(), reverse=True)))


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--class", dest="only", help="report one AI name in full")
    ap.add_argument("--limit", type=int, default=25)
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    ap.add_argument("--rank", action="store_true",
                    help="rank every unnamed id by how likely it is to be a real add rather than an effect")
    ap.add_argument("--max-patterns", type=int, default=3,
                    help="skip AI classes serving more patterns than this (infrastructure)")
    args = ap.parse_args()

    dev2npc = devname_to_npc()
    spawns = pattern_spawns(args.xml)

    # Which of the missing ids could not be placed correctly even if someone wrote the code: their spawn
    # actions carry a pathname the client does not define, so they would stand where retail has them walk.
    from extract_client_waypoints import client_routes
    needs_route = spawns_needing_a_route(args.xml)
    have_routes = set(client_routes(args.worlds))
    route_blocked = {dev2npc[d] for d, paths in needs_route.items()
                     if d in dev2npc and not (paths & have_routes)}
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

    if args.rank:
        rank_missing(rows, route_blocked, args.xml)
        return 0

    focused = len(wanted) - shared
    print(f"{len(wanted)} named AI classes have a retail pattern that spawns something resolvable")
    print(f"{shared} serve more than {args.max_patterns} patterns and are skipped as infrastructure")
    print(f"{focused - len(rows)} of the remaining {focused} name every npc their pattern spawns")
    print(f"{len(rows)} disagree -- read them, do not trust them (see the docstring)")
    all_missing = [x for r in rows for x in r[3]]
    walk_blocked = [x for x in all_missing if x in route_blocked]
    print(f"of {len(all_missing)} unnamed ids, {len(walk_blocked)} need a route "
          f"the client does not define")
    print()

    for count, ai, filename, missing, extra in sorted(rows, key=lambda r: -r[0])[:args.limit]:
        if args.only and ai != args.only:
            continue
        print(f"{filename}  [{ai}]  ({len(patterns_per_ai[ai])} pattern(s))")
        if missing:
            walk = [i for i in missing if i in route_blocked]
            free = [i for i in missing if i not in route_blocked]
            if free:
                print(f"    retail spawns, class never names : {' '.join(free)}")
            if walk:
                print(f"    ...and these need a route the client does not define: {' '.join(walk)}")
        if extra:
            print(f"    class names, retail never spawns : {' '.join(extra[:12])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
