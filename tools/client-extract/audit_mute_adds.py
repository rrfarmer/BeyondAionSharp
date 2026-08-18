"""Adds our bosses already summon, that retail gives a pattern and we leave on a stock AI.

Two commits found the same thing twice by hand -- Queen Serusia's eggs and Ashunatal Shadowslip's
shadows. Both bosses had a correct summon table in `ai/spawn_helpers.xml`, both put their adds in the
world, and in both cases everything that made the adds *do* anything lived in a retail pattern nobody
had translated. The eggs never hatched and the three shadows were all the same shadow.

That is a seam rather than two coincidences, so this walks it:

  * take every npc our data actually summons -- from `ai/spawn_helpers.xml`, and from `bombs.xml`
  * keep the ones that carry a retail AI pattern
  * drop the ones already on a bespoke class
  * report what is left, with what its pattern actually contains

An add on `aggressive` whose retail pattern is one line of scenery is nothing. An add whose pattern
has timers, messages and spawns in it is a mechanic that arrives in the world and does nothing, which
is worse than a missing add: it looks present.

Usage:
    python audit_mute_adds.py <client_root> <patterns_dir> <binding_tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

#: AI names that mean "nobody wrote anything for this npc".
STOCK = {"aggressive", "general", "passive", "monster", "guard", "summoner", "onedmg_passive",
         "quest_use_item", "dummy", ""}

PAYLOAD = re.compile(
    r"<(use_skill|spawn|spawn_on_target|spawn_on_multi_target|broadcast_message|despawn_self"
    r"|add_battle_timer|set_idle_timer|add_hate_point|switch_target|say_to_all|despawn"
    r"|use_skill_by_attacker_indicator|switch_target_by_attacker_indicator)[ >]")


def summoned_ids(repo: pathlib.Path) -> dict[str, set[str]]:
    """npc id -> the bosses whose summon tables name it."""
    out: dict[str, set[str]] = collections.defaultdict(set)
    for name in ("spawn_helpers.xml", "bombs.xml"):
        path = repo / "game-server/data/static_data/ai" / name
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for block in re.findall(r'<ai npcId="(\d+)"(.*?)</ai>', text, re.S):
            boss, body = block
            for add in re.findall(r'npcId="(\d+)"', body):
                out[add].add(boss)
    return out


def placed_ids(repo: pathlib.Path) -> set[str]:
    """Every npc id something on this server puts in the world.

    Spawn data is only half of it: instance handlers place bosses in C#, and Commander Bakarma --
    the first row this filter was tried on -- is one of them. Reading the spawn xml alone would have
    called his encounter unreachable and buried a live Draupnir Cave mechanic at the bottom of the
    report. The C# side is a grep for `Spawn(<id>,` and is a proxy: it over-reports an id that merely
    appears as a literal and under-reports one built from a variable. It is reported as "placed"
    rather than "spawned" for that reason.
    """
    placed: set[str] = set()
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        placed.update(re.findall(r'<spawn npc_id="(\d+)"',
                                 path.read_text(encoding="utf-8", errors="replace")))
    for path in (repo / "src/Aion.GameServer").rglob("*.cs"):
        if "/obj/" in path.as_posix() or "/bin/" in path.as_posix():
            continue
        placed.update(re.findall(r"Spawn\(\s*(\d{5,6})\s*,",
                                 path.read_text(encoding="utf-8", errors="replace")))
    return placed


def pattern_bodies(patterns_dir: pathlib.Path) -> dict[str, str]:
    bodies: dict[str, str] = {}
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for block in re.split(r"<npc_ai_pattern>", text):
            m = re.search(r"<name>([^<]+)</name>", block)
            if m:
                bodies.setdefault(m.group(1), block)
    return bodies


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    summoned = summoned_ids(repo)
    placed = placed_ids(repo)
    bodies = pattern_bodies(pathlib.Path(args.patterns_dir))

    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        m = re.search(r'ai="([^"]*)"', attrs)
        ai_of[npc_id] = m.group(1) if m else ""
        m = re.search(r'name="([^"]*)"', attrs)
        name_of[npc_id] = m.group(1) if m else ""

    with open(args.binding_tsv, encoding="utf-8") as fh:
        rows = [line.rstrip("\n").split("\t") for line in fh]
    col = {c: i for i, c in enumerate(rows[0])}
    pattern_of = {r[col["npc_id"]]: r[col["pattern_name"]] for r in rows[1:]}

    findings = []
    for add, bosses in summoned.items():
        pattern = pattern_of.get(add)
        if not pattern or pattern not in bodies:
            continue
        if ai_of.get(add, "") not in STOCK:
            continue
        body = bodies[pattern]
        payload = len(PAYLOAD.findall(body))
        handlers = sorted(set(re.findall(r"<(on_[a-z_]+)>", body)))
        # A boss our spawn data never places cannot show anybody its adds, so its row is a
        # different job -- a spawn gap -- and mixing the two would put unreachable work at the top.
        live = any(b in placed for b in bosses)
        findings.append((live, payload, add, name_of.get(add, ""), pattern, sorted(bosses), handlers))

    findings.sort(reverse=True)
    reachable = sum(1 for f in findings if f[0])
    print(f"{len(findings)} adds our bosses summon that carry a retail pattern and sit on a stock AI; "
          f"{reachable} of them behind a boss something on this server places\n")
    for live, payload, add, name, pattern, bosses, handlers in findings:
        print(f"{'LIVE' if live else '  --'} {payload:3} payload  {add}  {name[:32]:32} "
              f"{pattern:30} boss {','.join(bosses)}")
        print(f"                {' '.join(h[3:] for h in handlers)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
