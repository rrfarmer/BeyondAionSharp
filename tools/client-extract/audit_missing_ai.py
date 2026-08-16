"""Report NPCs that have a real retail fight and no AI class at all.

`audit_missing_adds.py` finds mechanics whose *adds* never spawn. It cannot see
the case where an NPC has no behaviour whatsoever: its template points at a
generic handler, so nothing it does is missing in the sense of a specific NPC not
appearing -- the whole fight simply is not there.

That turned out to be the most productive category in this work. Wrathclaw, the
fourth of Tiamat's incarnations, sat on plain `aggressive` while his three
siblings shared a class. Icaronix the Betrayer was spawned by his own first form
and then driven by nothing. Lost Balor is a world boss on a four-hour respawn
that auto-attacked. All three were found by accident while chasing something
else.

This looks for them directly: an NPC our data actually spawns, whose template
names a generic AI, and whose retail pattern is substantial -- battle timers,
spawns, message handlers. Ranked by how much of a fight is going unused.

CLI:
    python audit_missing_ai.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

from audit_missing_adds import (NAME_RE, PATTERN_RE, TEMPLATE_RE, attr, read_text,
                                spawnable_npc_ids)

# Handlers that are not an encounter: the engine's defaults, plus the shared behaviours that a
# boss-shaped NPC would never be given on purpose. An NPC on one of these has no fight of its own.
GENERIC_AI = {"aggressive", "general", "aggressive_no_loot", "peace", "npc", "noaction",
              "summoned", "guard", "dummy", "invisible"}

TIMER_RE = re.compile(r"<btimer_indicator>")
SPAWN_RE = re.compile(r"<npc_nameid>")
MESSAGE_RE = re.compile(r"<is_message>")
SKILL_RE = re.compile(r"<skill>SKILLI_INDEX_")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--min-timers", type=int, default=6,
                    help="ignore patterns with fewer battle timers than this (default 6)")
    ap.add_argument("--max-binders", type=int, default=4,
                    help="ignore patterns shared by more NPCs than this (default 4)")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    pattern_of = {}
    binders: collections.Counter[str] = collections.Counter()
    for line in pathlib.Path(args.binding_tsv).read_text(encoding="utf-8").splitlines()[1:]:
        f = line.split("\t")
        pattern_of[f[0]] = f[3]
        binders[f[3]] += 1

    weight = {}
    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        text = read_text(path)
        for block in PATTERN_RE.finditer(text):
            body = block.group(1)
            m = NAME_RE.search(body)
            if m:
                weight[m.group(1)] = (len(TIMER_RE.findall(body)), len(SPAWN_RE.findall(body)),
                                      len(MESSAGE_RE.findall(body)), len(SKILL_RE.findall(body)))

    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(tpl)}
    spawnable = spawnable_npc_ids(repo)

    # Only NPCs our data really places, so this cannot report a boss nobody can fight -- the trap
    # Jurdin the Cursed set for the adds audit.
    # NCSoft ships developer maps -- tag-match arenas, time-attack rigs, zone tests -- and their spawn
    # files sit alongside the real ones. An NPC placed only there is not content anyone can reach, so
    # counting it as live puts phantoms at the top of this report. Ahserion 297189 led it for several
    # runs on the strength of a single placement in 900190000_Tag_Match_Test_Level, while the Ahserion
    # players actually fight (277224) has had an AI class all along.
    TEST_MAPS = re.compile(r"test|_dev|sample", re.IGNORECASE)

    live: set[str] = set()
    for p in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        if TEST_MAPS.search(p.stem):
            continue
        live.update(re.findall(r'<spawn npc_id="(\d+)"', read_text(p)))
    handler_text = {p: read_text(p) for p in
                    (repo / "src/Aion.GameServer/Handlers/Instance").rglob("*.cs")}
    live.update(re.findall(r"\b(\d{6})\b", "\n".join(handler_text.values())))

    # Which NPCs an instance handler already names. Retail packs doors, system messages and score into
    # the monster's own pattern because that is the only place it has; our server splits them across
    # instance handlers, which is the Java-parity arrangement and the correct one. So a pattern action
    # with no counterpart in an AI class is not automatically missing -- it may already live in the
    # handler. Researcher Teselik was written up as needing door control that SauroSupplyBaseInstance
    # had implemented all along, for that exact npc id, with the same system message.
    handled: dict[str, str] = {}
    for path, text in handler_text.items():
        for npc_id in set(re.findall(r"\b(\d{6})\b", text)):
            handled.setdefault(npc_id, path.stem)

    rows = []
    for npc_id, attrs in templates.items():
        if npc_id not in spawnable or npc_id not in live:
            continue
        if attr(attrs, "ai").lower() not in GENERIC_AI:
            continue
        pattern = pattern_of.get(npc_id)
        if not pattern or pattern not in weight:
            continue
        timers, spawns, messages, skills = weight[pattern]
        if timers < args.min_timers:
            continue
        # A pattern shared by a crowd is a generic behaviour, not a fight somebody forgot to write.
        # Without this the report is 4,930 rows, most of them ordinary monsters.
        if binders[pattern] > args.max_binders:
            continue
        rows.append((timers, spawns, messages, skills, npc_id, attr(attrs, "name"),
                     attr(attrs, "level"), attr(attrs, "rating"), attr(attrs, "ai"), pattern,
                     handled.get(npc_id, "")))

    rows.sort(reverse=True)
    print(f"NPCs we spawn that have a retail fight and no AI class: {len(rows)}\n")
    print(f"{'timers':>6} {'spawns':>6} {'msgs':>5} {'skills':>6}  {'npc':<8} {'lv':>3} "
          f"{'rating':<10} {'name':<28} {'pattern':<36} handler")
    for timers, spawns, messages, skills, npc_id, name, level, rating, ai, pattern, handler in rows:
        print(f"{timers:>6} {spawns:>6} {messages:>5} {skills:>6}  {npc_id:<8} {level:>3} "
              f"{rating:<10} {name:<28} {pattern:<36} {handler}")
    print("\nhandler = an instance handler already names this npc id, so part of its retail pattern"
          "\n(doors, system messages, score) may already be implemented there. Check that before"
          "\nwriting 'not translated' against anything.")


if __name__ == "__main__":
    main()
