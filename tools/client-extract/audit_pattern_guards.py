"""Report translated pattern branches that dropped their retail HP band.

`audit_hp_phases.py` compares hand-written `HpPhases` ladders against their
pattern. This is the same question one layer down, for classes that were already
translated into `PatternAi` tables: does each branch still carry the guard the
retail branch carries?

The failure this exists to catch is specific and was found the hard way. Retail
writes a summoning ladder as battle-timer branches guarded by
`is_hp_in_boundary` -- a *band*, not a threshold -- with a bare-timer branch at
the bottom that only re-arms the clock. Read in a hurry, the bands are easy to
miss, and what is left looks like an unguarded sequence ordered by priority
alone. It runs, it summons, and it is a different fight: waves arrive at full
health instead of at their band, in the reverse order, and a band the raid jumps
over still fires instead of being skipped. Three of the ND2 named bosses shipped
that way.

Two things are reported per class:

    band            a retail branch that spawns or despawns, guarded by a band
                    the class has no `When.HpBetween` for
    no fallback     the pattern has a bottom branch whose only condition is the
                    battle timer and whose only action is re-arming it, and the
                    class has no equivalent. Without it a banded ladder is
                    unreachable: the first heartbeat matches no band, nothing
                    re-arms, and the boss never summons at all.

Cast-only branches are ignored on purpose. This work does not translate casts it
cannot map to a skill id, so a band that only casts is legitimately absent; only
a band whose branch spawns or despawns is a gap.

Judgement is still required. A class may carry a band as `When.HpBelow` where the
pattern's own bands make the two equivalent -- the deepest rung usually can --
and that reads here as a gap. Treat a hit as a prompt to read the pattern.

CLI:
    python audit_pattern_guards.py <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

from audit_missing_adds import PATTERN_RE, NAME_RE, read_text
from audit_hp_phases import load_binding

AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')
HPBETWEEN_RE = re.compile(r"When\.HpBetween\((\d+),\s*(\d+)\)")
HPBELOW_RE = re.compile(r"When\.HpBelow\((\d+)\)")
BOUND_RE = re.compile(
    r"<is_hp_in_boundary>.*?<larger_than>(\d+)</larger_than>.*?<less_than>(\d+)</less_than>", re.S)
TIMER_COND_RE = re.compile(r"<is_battle_timer_indicator>")
# The actions this work translates. A branch built only from casts is not a gap.
PLACES_RE = re.compile(r"<(spawn|spawn_on_target|spawn_on_multi_target|despawn)>")


def branches(body: str) -> list[str]:
    return [m.group(1) for m in re.finditer(r"<pattern>(.*?)</pattern>", body, re.S)]


def classes(text: str) -> list[tuple[str, str]]:
    """(ai name, the source between its [AIName] and the next one)

    Cheap on purpose: the marker is the only thing that has to be found, and everything a class
    declares sits below its own attribute and above the next class's.
    """
    marks = [(m.group(1), m.start()) for m in AINAME_RE.finditer(text)]
    out = []
    for i, (name, start) in enumerate(marks):
        end = marks[i + 1][1] if i + 1 < len(marks) else len(text)
        out.append((name, text[start:end]))
    return out


def pattern_facts(patterns_dir: pathlib.Path) -> dict[str, tuple[set[tuple[int, int]], bool]]:
    """pattern -> (bands whose branch places something, has a bare-timer fallback)"""
    out: dict[str, tuple[set[tuple[int, int]], bool]] = {}
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            named = NAME_RE.search(body)
            if not named:
                continue

            bands: set[tuple[int, int]] = set()
            fallback = False
            for branch in branches(body):
                conditions = re.search(r"<conditions>(.*?)</conditions>", branch, re.S)
                actions = re.search(r"<actions>(.*?)</actions>", branch, re.S)
                conditions = conditions.group(1) if conditions else ""
                actions = actions.group(1) if actions else ""

                found = BOUND_RE.findall(conditions)
                if found and PLACES_RE.search(actions):
                    bands.update((int(lo), int(hi)) for lo, hi in found)

                # The bottom rung: nothing but the timer, and nothing but re-arming it.
                if (TIMER_COND_RE.search(conditions) and not found
                        and "<is_hp_" not in conditions
                        and "<add_battle_timer>" in actions
                        and not PLACES_RE.search(actions)
                        and "<use_skill" not in actions):
                    fallback = True

            if bands or fallback:
                seen_bands, seen_fallback = out.setdefault(named.group(1), (set(), False))
                seen_bands.update(bands)
                out[named.group(1)] = (seen_bands, seen_fallback or fallback)
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    binding = load_binding(pathlib.Path(args.binding_tsv))
    facts = pattern_facts(pathlib.Path(args.patterns_dir))

    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    by_ai = collections.defaultdict(list)
    for npc_id, ai in re.findall(r'<npc_template npc_id="(\d+)"[^>]*?ai="([^"]+)"', tpl):
        by_ai[ai].append(npc_id)

    checked = 0
    rows = []
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs")):
        text = read_text(path)
        if "PatternAi" not in text:
            continue

        # Scoped to the class, not the file. Several files hold three or four bosses -- the ND2 named
        # trio share one -- and a file-wide guard set let one boss's HpBelow answer for another's
        # missing band, which is exactly the confusion this audit is meant to remove.
        for ai_name, body in classes(text):
            ours_bands = {(int(lo), int(hi)) for lo, hi in HPBETWEEN_RE.findall(body)}
            ours_below = {int(p) for p in HPBELOW_RE.findall(body)}
            has_fallback = bool(re.search(r"\[When\.Timer\(\d+\)\],?\s*\n?\s*Do\.ArmTimer\(", body))

            for npc_id in by_ai.get(ai_name, []):
                pattern = binding.get(npc_id)
                if pattern not in facts:
                    continue
                checked += 1
                bands, fallback = facts[pattern]

                # A band whose lower edge is 0-ish is a threshold in disguise; HpBelow covers it.
                missing = sorted(
                    b for b in bands
                    if b not in ours_bands and not any(lo <= p <= hi + 1 for p in ours_below for lo, hi in [b]))
                gap = fallback and not has_fallback
                if missing or gap:
                    rows.append((path.name, ai_name, npc_id, pattern, missing, gap))
                break

    print(f"translated classes checked against a bound pattern: {checked}")
    print(f"  with a band or fallback unaccounted for        : {len(rows)}\n")
    for name, ai_name, npc_id, pattern, missing, gap in rows:
        print(f"{name}  [{ai_name}]  npc {npc_id}  pattern {pattern}")
        if missing:
            print(f"    bands that place something, with no When.HpBetween: "
                  f"{', '.join(f'{lo}-{hi}' for lo, hi in missing)}")
        if gap:
            print("    pattern has a bare-timer fallback branch; class has none")


if __name__ == "__main__":
    main()
