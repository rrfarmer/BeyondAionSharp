"""Guarded, multi-branch `on_idle_timer` rungs: the wave controllers this port never ran.

WHY THIS EXISTS
---------------
`IdleSpawns` handles the flat case -- one unguarded rung that spawns and re-arms. **65 patterns cannot
be said that way**: they carry two to ten branches, guarded by retail's flag idiom or a probability
roll. **They are not where the spawn-engine writers live**, which was the expectation going in: of the
984 actions this extracts, exactly one is a `set_condition_spawn_variable`. The writers are in the 31
patterns refused below. What this does carry is 747 spawns -- the adds themselves.

Everything they need already exists in `PatternAi`: `set_flag_var` is `When.FirstTime`,
`unset_flag_var` is `When.Consuming`, `test_probability` is `When.Chance`, the world-flag forms are
`When.WorldFirstTime` and its neighbours, and every action has a `Do.` helper --
`set_condition_spawn_variable` included, since the conditional spawn engine was built.

FLAG NUMBERING
--------------
Retail gives every npc 32 flag slots and names them `FLAGVARI_<FAMILY>_<n>`. The dump uses six families
-- ALPHA, BETA, DELTA, EPSILON, GAMMA, ZETA -- with `n` from 1 to 5, thirty in all, which is presumably
why the slot count is what it is. Sorted family index times five plus `n-1` numbers them 0..29.

WHAT IS LEFT OUT
----------------
A pattern is skipped whole if any branch needs something this port cannot say: `increase_intvar` (a
counter with a bounds test), `display_system_message` and `say_to_all` (string ids), `use_skill` (skill
indices). Patterns with no wake-up delay to start the cycle are skipped too. Both are counted.

CLI:
    python extract_idle_cycles.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
from client_npc_names import npc_names  # noqa: E402

BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)

#: Classes that do nothing with a timer, plus the one this table feeds -- without which the extractor
#: stops finding its own rows once they are bound and `regen_check` reports drift.
#: Both classes this table feeds are listed, and the passive one is not optional: 67 of these npcs are
#: on it, and leaving it out drops them from their own table on the next regeneration.
GENERIC = {"aggressive", "general", "onedmg_aggressive", "aggressive_noloot", "dummy",
           "no_interaction", "idle_cycle", "idle_cycle_passive"}

FAMILIES = ["ALPHA", "BETA", "DELTA", "EPSILON", "GAMMA", "ZETA"]

#: The flag conditions this port can say. `is_flag_var` is **not** among them: retail's read-only test
#: has no `When` helper here -- `PatternAi` exposes a flag read for diagnostics only -- and emitting a
#: test-and-set in its place would consume a flag the rung only meant to look at.
#: Retail's four counters, in the order it names them.
COUNTERS = ["INTVARI_FIRST", "INTVARI_SECOND", "INTVARI_THIRD", "INTVARI_FOURTH"]

FLAG_KINDS = ("set_flag_var", "unset_flag_var",
              "set_world_flag_var", "unset_world_flag_var", "is_world_flag_var")


def slot(indicator: str) -> int | None:
    """`FLAGVARI_BETA_3` -> a slot in 0..29, or None for a family the dump never used."""
    named = re.fullmatch(r"FLAGVARI_([A-Z]+)_(\d+)", indicator.strip())
    if not named or named.group(1) not in FAMILIES:
        return None
    number = int(named.group(2))
    if not 1 <= number <= 5:
        return None
    return FAMILIES.index(named.group(1)) * 5 + number - 1


def read_guards(block: str) -> list[str] | None:
    """The branch's conditions as `kind:arg` tokens, or None if one cannot be said."""
    out: list[str] = []
    for element in re.finditer(r"<(\w+)>(.*?)</\1>", block, re.S):
        kind, body = element.group(1), element.group(2)
        if kind in FLAG_KINDS:
            indicator = re.search(r"<flagvar_indicator>([^<]+)</flagvar_indicator>", body)
            if not indicator:
                return None
            index = slot(indicator.group(1))
            if index is None:
                return None
            out.append(kind + ":" + str(index))
        elif kind == "test_probability":
            percent = re.search(r"<percent>(\d+)</percent>", body)
            if not percent:
                return None
            out.append("chance:" + percent.group(1))
        elif kind == "increase_intvar":
            # A condition that increments as it tests, like the flag idiom. All 1,409 uses in the dump
            # are conditions and none is an action; see `When.Counting`.
            indicator = re.search(r"<intvar_indicator>([^<]+)</intvar_indicator>", body)
            low = re.search(r"<lower_bound>(-?\d+)</lower_bound>", body)
            high = re.search(r"<upper_bound>(-?\d+)</upper_bound>", body)
            at_bound = re.search(r"<be_true_only_when_hit_the_bound>(\w+)</", body)
            if not (indicator and low and high) or indicator.group(1).strip() not in COUNTERS:
                return None
            out.append("count:%d:%s:%s:%s" % (
                COUNTERS.index(indicator.group(1).strip()), low.group(1), high.group(1),
                "1" if at_bound and at_bound.group(1).upper() == "TRUE" else "0"))
        else:
            return None
    return out


def string_ids(repo: pathlib.Path) -> dict[str, int]:
    """Symbolic string id -> the number the client expects, from `out/string_ids.tsv`.

    The client's own `strings.xml` resolves every one of the 3,492 the patterns use; this reads the
    extracted subset rather than the 118MB original.
    """
    out: dict[str, int] = {}
    path = repo / "tools/client-extract/out/string_ids.tsv"
    if not path.exists():
        return out
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        fields = line.split("	")
        if len(fields) >= 2 and fields[0].isdigit():
            out[fields[1]] = int(fields[0])
    return out


def read_actions(block: str, dev: dict[str, int], known: set[int],
                 strings: dict[str, int]) -> list[tuple] | None:
    """The branch's actions in order, or None if one cannot be said."""
    out: list[tuple] = []
    for element in re.finditer(r"<(\w+)>(.*?)</\1>", block, re.S):
        kind, body = element.group(1), element.group(2)
        if kind == "spawn":
            named = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", body)
            npc_id = dev.get(named.group(1)) if named else None
            if npc_id is None or npc_id not in known:
                return None
            where = re.search(r"<spawn_location_type>(\w+)</", body)
            place = ("self" if where and where.group(1).endswith("MY_POINT")
                     else "offset" if where and where.group(1).endswith("RELATIVE")
                     else "absolute")
            spot = [re.search(r"<%s>([-\d.]+)</%s>" % (axis, axis), body) for axis in "xyz"]
            if place == "absolute" and not all(spot):
                return None
            count = re.search(r"<num_to_spawn>(\d+)</", body)
            live = re.search(r"<live_time>(\d+)</", body)
            # `despawn_at_attack_state`: the add is the controller's, not the world's. 3,129 of the
            # 3,294 spawns inside `on_idle_timer` carry it and 2,267 of those are permanent. A wave
            # controller rarely fights, so this fires when it dies or despawns -- killing the thing
            # that placed a wave takes the wave with it, which is exactly what it is for.
            transient = re.search(r"<despawn_at_attack_state>(\w+)</", body)
            if transient and transient.group(1).upper() == "TRUE":
                place = "for_the_fight_" + place
            out.append(("spawn", npc_id, int(count.group(1)) if count else 1,
                        int(live.group(1)) if live else 0, place,
                        float(spot[0].group(1)) if spot[0] else 0.0,
                        float(spot[1].group(1)) if spot[1] else 0.0,
                        float(spot[2].group(1)) if spot[2] else 0.0))
        elif kind == "set_idle_timer":
            delay = re.search(r"<delay>(\d+)</delay>", body)
            out.append(("timer", int(delay.group(1)) if delay else 0, 0, 0, "", 0.0, 0.0, 0.0))
        elif kind == "set_condition_spawn_variable":
            name = re.search(r"<string>([^<]*)</string>", body)
            value = re.search(r"<set>(-?\d+)</set>", body)
            modify = re.search(r"<modify>(-?\d+)</modify>", body)
            if not name or not name.group(1).strip():
                return None
            out.append(("var", int(value.group(1)) if value else 0,
                        int(modify.group(1)) if modify else 0, 0,
                        name.group(1).strip(), 0.0, 0.0, 0.0))
        elif kind in ("say_to_all", "display_system_message"):
            # Both name a string symbolically; the client's table turns it into the number the packet
            # carries. They are different packets -- a shout within fifty metres against a line to the
            # whole instance -- and are kept apart.
            named = re.search(r"<string_id>([^<]+)</string_id>", body)
            message = strings.get(named.group(1).strip()) if named else None
            if message is None:
                return None
            delay = re.search(r"<delay>(\d+)</delay>", body)
            out.append(("say" if kind == "say_to_all" else "sysmsg", message,
                        int(delay.group(1)) if delay else 0, 0, "", 0.0, 0.0, 0.0))
        elif kind == "despawn_self":
            out.append(("despawn_self", 0, 0, 0, "", 0.0, 0.0, 0.0))
        elif kind == "broadcast_message":
            message = re.search(r"<message_type>(\d+)</message_type>", body)
            reach = re.search(r"<range_as_meter>(\d+)</", body)
            if not message:
                return None
            out.append(("broadcast", int(message.group(1)),
                        int(reach.group(1)) if reach else 0, 0, "", 0.0, 0.0, 0.0))
        else:
            return None
    return out


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    known = set(ai)
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}
    strings = string_ids(args.repo)

    # An npc some hand-ported encounter already models must not be taken over. Kalindi's dispel worm
    # is the case that proved it: its own retail pattern removes it after two seconds, `CalindiFlamelordAI`
    # gives it ten, and rebinding it broke three pins that had it right for that encounter. Which of the
    # two is faithful is a real question and not one to answer by accident.
    spoken_for: set[int] = set()
    for source in (args.repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs"):
        for found in re.finditer(r"=\s*(\d{6})\s*;", source.read_text(encoding="utf-8", errors="replace")):
            spoken_for.add(int(found.group(1)))

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows: list[tuple] = []
    seen: set[str] = set()
    skipped: collections.Counter = collections.Counter()
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named or named.group(1) in seen:
                continue
            idle = re.search(r"<on_idle_timer>(.*?)</on_idle_timer>", body, re.S)
            if not idle or "<spawn>" not in idle.group(1):
                continue
            owners = [n for n in binders.get(named.group(1), [])
                      if ai.get(n) in GENERIC and n not in spoken_for]
            if not owners:
                skipped_spoken = [n for n in binders.get(named.group(1), []) if n in spoken_for]
                if skipped_spoken:
                    skipped["an encounter already models this npc"] += 1
                continue

            wake = re.search(r"<on_wake_up>(.*?)</on_wake_up>", body, re.S)
            waited = (re.search(r"<set_idle_timer>\s*<delay>(\d+)</delay>", wake.group(1))
                      if wake else None)
            if not waited:
                skipped["no wake delay to start the cycle"] += 1
                continue

            branches: list[tuple[int, list[str], list[tuple]]] = []
            refused = False
            for branch in BRANCH_RE.finditer(idle.group(1)):
                guards_block = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
                actions_block = re.search(r"<actions>(.*?)</actions>", branch.group(1), re.S)
                if not actions_block:
                    continue
                guards = read_guards(guards_block.group(1)) if guards_block else []
                actions = read_actions(actions_block.group(1), dev, known, strings)
                if guards is None or actions is None or not actions:
                    # One branch this port cannot say makes the whole pattern unsafe: the branches are
                    # first-match-wins, so dropping a high-priority rung silently promotes the next.
                    refused = True
                    break
                priority = re.search(r"<priority>(-?\d+)</priority>", branch.group(1))
                branches.append((int(priority.group(1)) if priority else 0, guards, actions))

            if refused or not branches:
                skipped["a branch this port cannot say"] += 1
                continue

            seen.add(named.group(1))
            for owner in owners:
                for index, (priority, guards, actions) in enumerate(branches):
                    for order, action in enumerate(actions):
                        # `order` is load-bearing. Sorting the rows without it puts the actions of a
                        # branch in alphabetical order by kind, so a rung that spawns and then despawns
                        # itself comes out despawning first and never spawning.
                        rows.append((owner, int(waited.group(1)), index, order, priority,
                                     "|".join(guards)) + action + (named.group(1),))

    rows.sort()
    header = ["npc", "wake_ms", "branch", "order", "priority", "guards", "kind", "a1", "a2", "a3",
              "place", "x", "y", "z", "pattern"]
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("\t".join(header) + "\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    print(f"{len(seen)} patterns, {len(npcs)} npcs, {len(rows)} actions -> {args.out}")
    for reason, count in skipped.most_common():
        print(f"    {count} skipped: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
