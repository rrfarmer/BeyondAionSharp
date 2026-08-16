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
    no fallback     a timer slot on which the pattern has a branch whose only
                    condition is that timer and whose only action is re-arming
                    it, where the class reads that slot and has no equivalent.
                    Without it a banded ladder is unreachable: a tick that lands
                    between bands matches nothing, nothing re-arms, and the
                    chain is over for the rest of the fight.

Cast-only branches are ignored on purpose. This work does not translate casts it
cannot map to a skill id, so a band that only casts is legitimately absent; only
a band whose branch spawns or despawns is a gap.

**Precision was bought one false positive at a time**, and each exclusion below
is here because it fired against correct code. A check nobody trusts is worse
than no check, so they are worth knowing before loosening any of them:

    computed bands   `GuardReinforcementAI` and `TiamatDyingRotationAI` build
                     their guards from generated tables, so the source says
                     `When.HpBetween(band.Low, band.High)` and no literal
                     appears. Their bands are right and a text scan cannot say
                     so, so a class with non-literal guards is skipped.
    shared machinery a class's guards are read from its own body plus the file
                     preamble -- where `GuardReinforcementAI` keeps its whole
                     builder -- but never from a sibling class's body, which is
                     what stopped one ND2 boss answering for another's bands.
    helper rungs     half these classes build branches through a local `Step(..)`
                     helper, so the fallback is found by its guard array alone
                     rather than by what follows it.
    a real fallback  does *nothing* but re-arm. Prectaz's three-second
                     `broadcast_message` heartbeat re-arms a timer and announces
                     something, which is a heartbeat with a job.
    the right slot   Princess Karemiwen translates only her minute-long timer 8;
                     retail's fallback is on timer 0, a chain she does not run.

Judgement is still required on what is left. A class may carry a band as
`When.HpBelow` where the pattern's own bands make the two equivalent -- the
deepest rung usually can -- and that reads here as a gap. Treat a hit as a prompt
to read the pattern, and see docs/retail-ai-fidelity.md for the findings already
triaged and deliberately left standing.

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
# `RagingKraterrAI` builds its pattern from `ElementalSummonerPattern.For(...)`, declared in another
# file entirely. Reading only the class and its own preamble reported all three of its bands missing.
DELEGATE_RE = re.compile(r"\b(\w+)\.\w+\(")
DECLARES_RE = re.compile(r"\b(?:static\s+)?class\s+(\w+)")
HPBETWEEN_RE = re.compile(r"When\.HpBetween\((\d+),\s*(\d+)\)")
HPBELOW_RE = re.compile(r"When\.HpBelow\((\d+)\)")
BOUND_RE = re.compile(
    r"<is_hp_in_boundary>.*?<larger_than>(\d+)</larger_than>.*?<less_than>(\d+)</less_than>", re.S)
TIMER_COND_RE = re.compile(r"<is_battle_timer_indicator>")
SLOT_RE = re.compile(r"<btimer_indicator>BTIMERI_INDEX_(\d+)</btimer_indicator>")
OURS_TIMER_RE = re.compile(r"When\.Timer\((\d+)\)")
BARE_TIMER_RE = re.compile(r"\[When\.Timer\((\d+)\)\]")
# The actions this work translates. A branch built only from casts is not a gap.
PLACES_RE = re.compile(r"<(spawn|spawn_on_target|spawn_on_multi_target|despawn)>")


def branches(body: str) -> list[str]:
    return [m.group(1) for m in re.finditer(r"<pattern>(.*?)</pattern>", body, re.S)]


# Every top-level element inside <actions>. A fallback rung has exactly one kind of them.
ACTION_TAG_RE = re.compile(r"<(\w+)>")


def only_rearms(actions: str) -> bool:
    """True when the branch's whole action list is `add_battle_timer`, and nothing else."""
    tags = {t for t in ACTION_TAG_RE.findall(actions)}
    # The tags a single add_battle_timer carries with it.
    tags -= {"btimer_indicator", "delay"}
    return tags == {"add_battle_timer"}


def classes(text: str) -> list[tuple[str, str, str]]:
    """(ai name, the source between its [AIName] and the next one, the file preamble)

    Cheap on purpose: the marker is the only thing that has to be found, and everything a class
    declares sits below its own attribute and above the next class's.
    """
    marks = [(m.group(1), m.start()) for m in AINAME_RE.finditer(text)]
    if not marks:
        return []
    # Everything above the first attribute is shared machinery -- GuardReinforcementAI keeps its
    # whole table-driven builder in an `internal static class` there -- so it counts as part of
    # every class in the file. Other classes' bodies deliberately do not, which is what stopped one
    # boss's guards from answering for another's.
    preamble = text[:marks[0][1]]
    out = []
    for i, (name, start) in enumerate(marks):
        end = marks[i + 1][1] if i + 1 < len(marks) else len(text)
        out.append((name, text[start:end], preamble))
    return out


def pattern_facts(patterns_dir: pathlib.Path) -> dict[str, tuple[set[tuple[int, int]], set[int]]]:
    """pattern -> (bands whose branch places something, timers carrying a bare-timer fallback)

    The fallback is recorded *per timer slot*, not as a flag. Princess Karemiwen translates only her
    minute-long timer 8 and retail's fallback sits on timer 0, so a boolean reported a rung missing
    from a chain the class does not run at all.
    """
    out: dict[str, tuple[set[tuple[int, int]], set[int]]] = {}
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            named = NAME_RE.search(body)
            if not named:
                continue

            bands: set[tuple[int, int]] = set()
            fallback: set[int] = set()
            for branch in branches(body):
                conditions = re.search(r"<conditions>(.*?)</conditions>", branch, re.S)
                actions = re.search(r"<actions>(.*?)</actions>", branch, re.S)
                conditions = conditions.group(1) if conditions else ""
                actions = actions.group(1) if actions else ""

                found = BOUND_RE.findall(conditions)
                if found and PLACES_RE.search(actions):
                    bands.update((int(lo), int(hi)) for lo, hi in found)

                # The bottom rung: nothing but the timer, and *nothing but* re-arming it.
                #
                # "Nothing but" has to be literal. A first version only excluded spawns and casts,
                # and matched Prectaz's three-second `broadcast_message` heartbeat -- a branch that
                # re-arms a timer and announces something is a heartbeat with a job, not a rung
                # that exists to keep another rung reachable. Five of the six fallback findings on
                # the first run were that shape.
                if (TIMER_COND_RE.search(conditions) and not found
                        and "<is_hp_" not in conditions
                        and only_rearms(actions)):
                    slot = SLOT_RE.search(conditions)
                    if slot:
                        fallback.add(int(slot.group(1)))

            if bands or fallback:
                seen_bands, seen_fallback = out.setdefault(named.group(1), (set(), set()))
                seen_bands.update(bands)
                seen_fallback.update(fallback)
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

    # Where each helper type is declared, so a class that delegates its pattern can be read with it.
    ai_paths = sorted((repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs"))
    sources = {path: read_text(path) for path in ai_paths}
    declared: dict[str, str] = {}
    for path, text in sources.items():
        for name in DECLARES_RE.findall(text):
            declared.setdefault(name, text)

    checked = 0
    rows = []
    for path in ai_paths:
        text = sources[path]
        if "PatternAi" not in text:
            continue

        # Scoped to the class, not the file. Several files hold three or four bosses -- the ND2 named
        # trio share one -- and a file-wide guard set let one boss's HpBelow answer for another's
        # missing band, which is exactly the confusion this audit is meant to remove.
        for ai_name, own, preamble in classes(text):
            body = own + preamble
            # Follow whatever the class delegates its pattern to, wherever that is declared.
            for helper in set(DELEGATE_RE.findall(own)):
                if helper in declared and declared[helper] is not text:
                    body += declared[helper]
            ours_bands = {(int(lo), int(hi)) for lo, hi in HPBETWEEN_RE.findall(body)}
            ours_below = {int(p) for p in HPBELOW_RE.findall(body)}

            # Two shapes this scan cannot read, and both were false positives on the first run.
            #
            # computed   GuardReinforcementAI builds its bands from a generated table, so the guards
            #            are `When.HpBetween(band.Low, band.High)` and no literal appears anywhere.
            #            Its bands are right; a text scan simply cannot say so.
            # no timers  a class with no ArmTimer runs no battle-timer chain at all, so it has
            #            nothing for a fallback rung to carry. Reporting one is noise.
            computed = "When.HpBetween(" in body and not HPBETWEEN_RE.search(body)
            runs_timers = "Do.ArmTimer(" in body
            # A guard array holding one condition and that condition a timer: the class handles a
            # bare tick on that slot. Matched on the array rather than on what follows it, because
            # half these classes build their rungs through a local `Step(...)` helper, and a version
            # that looked for `Do.ArmTimer` right after the array reported Tahabata and Asaratu as
            # missing a rung each had written, with a comment about this exact hazard beside it.
            ours_timers = {int(n) for n in OURS_TIMER_RE.findall(body)}
            bare_timers = {int(n) for n in BARE_TIMER_RE.findall(body)}
            if "When.Timer(" in body and not ours_timers:
                computed = True

            for npc_id in by_ai.get(ai_name, []):
                pattern = binding.get(npc_id)
                if pattern not in facts:
                    continue
                checked += 1
                bands, fallback = facts[pattern]

                # A band whose lower edge is 0-ish is a threshold in disguise; HpBelow covers it.
                missing = [] if computed else sorted(
                    b for b in bands
                    if b not in ours_bands and not any(lo <= p <= hi + 1 for p in ours_below for lo, hi in [b]))
                # Only a slot the class actually reads. A fallback on a chain it never translated
                # is retail's business, not a gap in ours.
                gap = sorted(
                    (fallback & ours_timers) - bare_timers) if runs_timers and not computed else []
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
            print("    timers whose bare-timer fallback rung the class has no equivalent for: "
                  + ", ".join(str(t) for t in gap))


if __name__ == "__main__":
    main()
