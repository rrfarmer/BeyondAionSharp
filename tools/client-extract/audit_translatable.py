"""Rank the unported retail patterns by how much of them we could actually write.

The other audits answer "what is missing". This one answers "what is worth doing next", which is a
different question and had been eyeballed rather than measured: the adds audit ranks by adds, and a
boss can carry a rich mechanic with no spawn in it at all — the Balaur officers' peel, the elemental
lords' band ladder — while a pattern with three spawns can be nothing but waypoint furniture.

An action is *translatable* when the pattern runtime has vocabulary for it. Everything else is
blocked, and the two big blockers are named rather than lumped together, because they are blocked for
different reasons and only one of them is ever going to be fixable in bulk:

  skill    `use_skill` and friends address SKILLI_INDEX_n against a per-npc skill list the dump does
           not carry. Resolving one index does not resolve another npc's.
  shout    `say_to_all` names a client string id with no row in npc_shouts.xml.
  path     `goto_waypoint`, `random_move` and the waypoint arrival event: no vocabulary, and the
           spawn data already carries walker routes for the npcs that need them.
  script   `set_condition_spawn_variable` and the instance-progression verbs, which belong to an
           instance handler rather than to an AI pattern.

Usage:
    python audit_translatable.py <patterns_dir> <binding_tsv> [--repo ..] [--min 4]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402

# Actions that *do* something a player can see. These are what a translation is worth.
PAYLOAD = {
    "spawn", "spawn_on_target", "spawn_on_target_by_attacker_indicator", "spawn_on_multi_target",
    "despawn", "despawn_self", "despawn_by_nameid",
    "broadcast_message",
    "switch_target", "switch_target_by_attacker_indicator",
    "add_hate_point", "attack_most_hating",
    "flee_from",
}

# Translatable, but only ever a means to an end. A pattern whose "translatable" actions are all
# timer arms is a cast chain with scaffolding, and porting it changes nothing at all -- the Belsagos
# trio scored 27, 33 and 34 that way and turned out to be one broadcast each. Counted separately so
# the ranking cannot be fooled by scaffolding again.
SCAFFOLDING = {"add_battle_timer", "set_idle_timer"}

TRANSLATABLE = PAYLOAD | SCAFFOLDING

BLOCKED = {
    "use_skill": "skill",
    "use_skill_by_attacker_indicator": "skill",
    "say_to_all": "shout",
    "say_to_all_str": "shout",
    "goto_waypoint": "path",
    "goto_next_waypoint": "path",
    "random_move": "path",
    "set_condition_spawn_variable": "script",
}

# An npc whose template names one of these has no bespoke class -- it is running a stock behaviour.
GENERIC_AI = {
    "aggressive", "general", "passive", "guard", "dummy", "ntrap", "trap", "summon",
    "monster", "peace", "questnpc", "npc", "door",
}

ACTION_RE = re.compile(r"<(\w+)>")


def dead_payload(block: str) -> int:
    """How many payload actions in this pattern sit on a timer nothing can arm.

    Imported inside the function because `audit_timer_reach` needs this module's PAYLOAD and
    GENERIC_AI sets, and a module-level import here would close the cycle.
    """
    import xml.etree.ElementTree as ET

    import audit_timer_reach as R

    try:
        root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(block)}</ai_pattern>")
    except ET.ParseError:
        return 0
    return R.analyse(root)[1]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--min", type=int, default=4, help="least payload actions worth listing")
    args = ap.parse_args()

    live = A.spawnable_npc_ids(args.repo)
    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai_of: dict[str, str] = {}
    name_of: dict[str, str] = {}
    for m in re.finditer(r"<npc_template[^>]*>", templates):
        block = m.group(0)
        npc = A.attr(block, "npc_id")
        ai_of[npc] = A.attr(block, "ai")
        name_of[npc] = A.attr(block, "name")

    binders: dict[str, list[str]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows = []
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in S.PATTERN_RE.finditer(text):
            block = m.group(0)
            named = S.NAME_RE.search(block)
            if not named:
                continue
            pattern = named.group(1)

            owners = [n for n in binders.get(pattern, []) if n in live]
            unported = [n for n in owners if ai_of.get(n, "") in GENERIC_AI]
            if not unported:
                continue

            counts = collections.Counter(ACTION_RE.findall(block))
            good = sum(v for k, v in counts.items() if k in PAYLOAD)
            scaffold = sum(v for k, v in counts.items() if k in SCAFFOLDING)

            # Payload on a battle timer that nothing reachable ever arms cannot run, so it is
            # not work we could do. Kaliga the Unjust ranked third on this list with five of
            # his nineteen actions behind timers armed only by a waypoint arrival.
            good -= dead_payload(block)
            blocked = collections.Counter()
            for tag, why in BLOCKED.items():
                if counts.get(tag):
                    blocked[why] += counts[tag]
            if good < args.min:
                continue

            rows.append((good, scaffold, blocked, pattern, unported))

    rows.sort(key=lambda r: (-r[0], r[3]))
    print(f"{'do':>3} {'arm':>4}  {'blocked':22} {'pattern':40} owners")
    for good, scaffold, blocked, pattern, unported in rows:
        why = " ".join(f"{k}:{v}" for k, v in sorted(blocked.items())) or "-"
        who = ", ".join(f"{n} {name_of.get(n, '?')}" for n in unported[:3])
        if len(unported) > 3:
            who += f" (+{len(unported) - 3})"
        print(f"{good:3} {scaffold:4}  {why:22} {pattern:40} {who}")
    print()
    print(f"{len(rows)} unported patterns with at least {args.min} payload actions; "
          f"{sum(len(r[4]) for r in rows)} npcs behind them.")


if __name__ == "__main__":
    main()
