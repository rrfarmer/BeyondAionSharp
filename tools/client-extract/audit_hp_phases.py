"""Compare our hand-written boss HP phases against the retail pattern's thresholds.

aionemu's phase thresholds were derived by watching fights, and the oddly
specific numbers give it away -- HpPhases(100, 81, 77, 61, 50) is an
observation, not a spec. The retail patterns state the real values in
is_hp_lower_than / is_hp_in_boundary conditions.

For every AI class using HpPhases, this resolves its [AIName] to npc_ids, those
to a retail pattern, and reports where our thresholds disagree with the
pattern's.

Judgement is still required on each hit: a pattern's conditions include
thresholds for things other than phase transitions, so treat a mismatch as a
prompt to read the pattern, not as a verdict.

CLI:
    python audit_hp_phases.py <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

from audit_missing_adds import PATTERN_RE, NAME_RE, read_text

AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')
HPPHASES_RE = re.compile(r"new HpPhases\(([^)]*)\)")
HP_LOWER_RE = re.compile(r"<is_hp_lower_than>.*?<percent>(\d+)</percent>", re.S)
# At or above this many battle-timer branches, the fight is driven by timers rather than HP steps.
TIMER_HEAVY = 10

HP_BOUND_RE = re.compile(
    r"<is_hp_in_boundary>.*?<larger_than>(\d+)</larger_than>.*?<less_than>(\d+)</less_than>", re.S)


def load_binding(path: pathlib.Path) -> dict[str, str]:
    """npc_id -> pattern name"""
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        out[npc_id] = pattern
    return out


def pattern_thresholds(patterns_dir: pathlib.Path) -> dict[str, tuple[set[int], set[tuple[int, int]]]]:
    """pattern -> (one-shot phase thresholds, regime boundaries)

    These are different constructs and must not be conflated. `is_hp_lower_than`
    latched behind a flag variable is a phase transition, the thing HpPhases
    models. `is_hp_in_boundary` is a regime guard: it gates which branch of a
    timer runs while HP sits inside a band, and fires repeatedly. A boss whose
    pattern is built from boundaries has no phase list to compare against, and
    porting it means restructuring the fight rather than editing numbers.
    """
    out: dict[str, tuple[set[int], set[tuple[int, int]], int]] = {}
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            # Only count a step that actually does something. Patterns carry latched steps
            # with empty <actions> as sequence markers or paired guards; Watchman Hokuruki
            # has five HP steps of which only two spawn anything, so counting all five
            # made a structural difference look like a renumber.
            timer_branches = len(re.findall(r"<btimer_indicator>", body))
            phases = set()
            for step in re.finditer(r"<pattern>(.*?)</pattern>", body, re.S):
                s = step.group(1)
                hp = HP_LOWER_RE.search(s)
                if not hp:
                    continue
                acts = re.search(r"<actions>(.*?)</actions>", s, re.S)
                if acts and re.search(r"<(use_skill\w*|spawn\w*|say_to_all|despawn\w*|"
                                      r"broadcast_message|teleport_target\w*|control_door|"
                                      r"flee_from|add_hate_point|switch_target\w*)>", acts.group(1)):
                    phases.add(int(hp.group(1)))
            regimes = {(int(lo), int(hi)) for lo, hi in HP_BOUND_RE.findall(body)}
            if phases or regimes:
                p, r, _ = out.setdefault(m.group(1), (set(), set(), timer_branches))
                p.update(phases)
                r.update(regimes)
                out[m.group(1)] = (p, r, max(out[m.group(1)][2], timer_branches))
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    binding = load_binding(pathlib.Path(args.binding_tsv))
    thresholds = pattern_thresholds(pathlib.Path(args.patterns_dir))

    # ai name -> npc_ids
    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    by_ai = collections.defaultdict(list)
    for npc_id, ai in re.findall(r'<npc_template npc_id="(\d+)"[^>]*?ai="([^"]+)"', tpl):
        by_ai[ai].append(npc_id)

    rows = []
    for path in (repo / "src/Aion.GameServer/Handlers/AI").rglob("*.cs"):
        text = read_text(path)
        name = AINAME_RE.search(text)
        phases = HPPHASES_RE.search(text)
        if not name or not phases:
            continue
        try:
            ours = [int(v.strip()) for v in phases.group(1).split(",") if v.strip().isdigit()]
        except ValueError:
            continue
        if not ours:
            continue

        # Several classes use HpPhases as a start-of-fight trigger rather than a phase ladder:
        # HpPhases(95) with a HandleHpPhase that ignores its argument and just starts a skill
        # loop. Renumbering those to a retail threshold would delay the whole fight -- Ebonsoul
        # and Rukril would not begin casting until 7% HP. Detect them by the handler not
        # switching on the percent at all.
        handler = re.search(r"HandleHpPhase\(int \w+\)(.*?)\n    }", text, re.S)
        if handler and not re.search(r"\bcase \d+:", handler.group(1)):
            continue

        for npc_id in by_ai.get(name.group(1), []):
            pattern = binding.get(npc_id)
            entry = thresholds.get(pattern) if pattern else None
            if not entry:
                continue
            phases, regimes, timers = entry
            missing = [v for v in ours if v not in phases]
            if missing:
                rows.append((path.name, name.group(1), npc_id, pattern, ours,
                             sorted(phases, reverse=True), sorted(regimes, reverse=True),
                             missing, timers))
            break  # one representative npc_id per AI is enough

    tweakable = [r for r in rows if r[5]]
    restructure = [r for r in rows if not r[5]]

    print(f"AI classes whose HpPhases disagree with their retail pattern: {len(rows)}")
    print(f"  threshold mismatch (retail has a phase list to copy) : {len(tweakable)}")
    print(f"  no retail phase list at all (regime-guarded fight)   : {len(restructure)}\n")

    print("== threshold mismatches ==")
    for fname, ai, npc_id, pattern, ours, phases, regimes, missing, timers in sorted(tweakable):
        print(f"{fname}  [{ai}]  npc {npc_id}  pattern {pattern}")
        print(f"    ours          : {ours}")
        print(f"    retail phases : {phases}")
        print(f"    ours-only     : {missing}")
        # A pattern built mostly from battle timers is not a threshold problem at all: aionemu turned
        # a timed rotation into a ladder of invented HP steps, and matching it means writing a
        # timer-driven AI class rather than renumbering. TheFlamelord reads as a plain threshold
        # mismatch here and is really a 25s spawn rotation spread across four timers.
        if timers >= TIMER_HEAVY:
            print(f"    NOTE          : {timers} battle-timer branches, so this is a timer-driven "
                  f"rotation; renumbering will not match it")

    print("\n== regime-guarded: reimplementation, not a threshold edit ==")
    for fname, ai, npc_id, pattern, ours, phases, regimes, missing, timers in sorted(restructure):
        print(f"{fname}  [{ai}]  ours {ours}  retail regimes {regimes[:4]}  timer branches {timers}")


if __name__ == "__main__":
    main()
