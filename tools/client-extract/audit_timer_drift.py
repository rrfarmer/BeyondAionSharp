"""Find AI classes whose timings are not in their retail pattern.

Three fights in a row this session had the same defect: a cadence picked by hand where retail has a
battle timer. Terath's black hole ran at half rate, Chantra's area attack at a sixth, and both of
Kumbanda's mechanics hung off a five-per-cent roll on every blow he took instead of two timers. None of
those was visible by reading the C# -- the numbers look deliberate. They only showed up against the
pattern.

So this compares the two. For each `[AIName]` class it collects:

* the delays retail gives that npc -- every `<delay>` under `add_battle_timer` and `set_idle_timer`,
  plus every `live_time`, across the patterns of every npc bound to the class;
* the delays this port uses -- every `TimeSpan.From*` and every bare millisecond literal handed to a
  scheduling call.

A port delay with no match in the pattern is **not** automatically wrong. Plenty are legitimate: a
lifetime this port applies where retail leaves the npc standing, a stagger between two spawns retail
does inline, or a number belonging to a mechanic the pattern dump does not cover. What the report gives
is a **ranking of where to look**, and the fights already corrected are the evidence that looking pays.

Read the output as: how much of what this class does on a clock can be found in retail's clock at all.

Usage:
    python audit_timer_drift.py [--patterns DIR] [--repo PATH] [--limit N] [--class NAME]
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

DEFAULT_PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

#: Scheduling calls whose numeric arguments are delays.
SCHEDULE = re.compile(r"(?:Schedule|ScheduleAtFixedRateTask|QueueSkill)\s*\(")

#: TimeSpan.FromSeconds(12) / FromMilliseconds(7100) / FromMinutes(10)
TIMESPAN = re.compile(r"TimeSpan\.From(Seconds|Milliseconds|Minutes)\(\s*([\d_]+(?:\.\d+)?)\s*\)")

#: Bare millisecond literals: 5000L, 30_000L, 1500
MILLIS = re.compile(r"\b(\d[\d_]{2,})L?\b")


#: Actions a timer rung can carry that this port can reproduce without knowing the skill index.
ACTIONABLE = ("<spawn>", "<spawn_on_target>", "<spawn_on_multi_target>", "<despawn", "<broadcast_message>",
              "<teleport_target>", "<set_condition_spawn_variable>")


def pattern_actionable(patterns_dir: pathlib.Path) -> set[str]:
    """
    Patterns whose timer rungs do something other than cast.

    **This is the ceiling on what a timing correction can achieve.** A rung that only carries
    `use_skill SKILLI_INDEX_n` cannot be told from its neighbours without the skill index, and the index
    is unresolved -- so knowing the delay is wrong does not say which of this port's casts owns it.
    Kinquid and Galamat are both like that: every rung is a cast, and their real cadences stay unknown.
    """
    out: set[str] = set()
    for path in patterns_dir.rglob("*.xml"):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", text, re.S):
            body = m.group(2)
            timers = re.search(r"<on_battle_timer>(.*?)</on_battle_timer>", body, re.S)
            if timers and any(tag in timers.group(1) for tag in ACTIONABLE):
                out.add(m.group(1).strip().lower())
    return out


def patterns_with_variable_rungs(patterns_dir: pathlib.Path) -> set[str]:
    """
    Patterns where one battle timer is re-armed with more than one delay.

    **A fixed-rate task cannot express these.** Retail re-arms a timer at the end of whichever rung
    matched, so the same indicator carries different delays under different guards -- Sharik's is
    thirty-seven seconds above half health and thirty below it. `ScheduleAtFixedRateTask` evaluates its
    period once, so a boss who crosses the guard keeps the delay he started with, for ever.

    Four classes in a row needed converting from a fixed rate to a self-re-arming chain before this was
    worth detecting rather than rediscovering: Terath, Kumbanda, Laksyaka and Sharik.
    """
    out: set[str] = set()
    for path in patterns_dir.rglob("*.xml"):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", text, re.S):
            by_timer: dict[str, set[str]] = {}
            for arm in re.finditer(
                    r"<add_battle_timer>\s*<btimer_indicator>([^<]+)</btimer_indicator>\s*"
                    r"<delay>(\d+)</delay>", m.group(2), re.S):
                by_timer.setdefault(arm.group(1), set()).add(arm.group(2))
            if any(len(v) > 1 for v in by_timer.values()):
                out.add(m.group(1).strip().lower())
    return out


def pattern_delays(patterns_dir: pathlib.Path) -> dict[str, set[int]]:
    """Every timer delay and live_time each pattern uses, in milliseconds."""
    out: dict[str, set[int]] = {}
    for path in patterns_dir.rglob("*.xml"):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", text, re.S):
            body = m.group(2)
            delays: set[int] = set()
            for d in re.findall(r"<delay>(\d+)</delay>", body):
                delays.add(int(d))
            for lt in re.findall(r"<live_time>(\d+)</live_time>", body):
                if int(lt):
                    delays.add(int(lt) * 1000)
            if delays:
                out.setdefault(m.group(1).strip().lower(), set()).update(delays)
    return out


def port_delays(source: str, not_delays: set[int]) -> set[int]:
    """
    Every delay this class schedules, in milliseconds.

    A scheduling line carries other numbers too -- the skill it queues, the npc it spawns -- and those
    are filtered out by identity against the real skill and npc tables rather than by guessing at
    magnitudes. Without that the report is mostly skill ids and reads as though every class had drifted.
    """
    found: set[int] = set()
    for unit, value in TIMESPAN.findall(source):
        n = float(value.replace("_", ""))
        found.add(int(n * {"Seconds": 1000, "Milliseconds": 1, "Minutes": 60000}[unit]))
    for line in source.splitlines():
        if not SCHEDULE.search(line):
            continue
        for lit in MILLIS.findall(line):
            value = int(lit.replace("_", ""))
            if value not in not_delays:
                found.add(value)
    return found


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns", default=str(DEFAULT_PATTERNS))
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--class", dest="only", help="report one class in full")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    static = repo / "game-server" / "data" / "static_data"

    delays_of = pattern_delays(pathlib.Path(args.patterns))
    actionable = pattern_actionable(pathlib.Path(args.patterns))
    variable = patterns_with_variable_rungs(pathlib.Path(args.patterns))

    # Numbers that appear on scheduling lines and are not delays.
    not_delays: set[int] = set()
    skills = static / "skills" / "skill_templates.xml"
    if skills.exists():
        text = skills.read_text(encoding="utf-8", errors="replace")
        not_delays.update(int(n) for n in re.findall(r'skill_id="(\d+)"', text))

    pattern_of: dict[int, str] = {}
    binding = repo / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in binding.read_text(encoding="utf-8", errors="replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 3 and parts[0].isdigit():
            pattern_of[int(parts[0])] = parts[2].strip().lower()

    all_npc_ids: set[int] = set()
    npcs_of_ai: dict[str, list[int]] = {}
    templates = (static / "npcs" / "npc_templates.xml").read_text(encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        ai = re.search(r'\bai="([^"]*)"', attrs)
        all_npc_ids.add(int(npc_id))
        if ai:
            npcs_of_ai.setdefault(ai.group(1).lower(), []).append(int(npc_id))

    rows = []
    for path in (repo / "src" / "Aion.GameServer" / "Handlers" / "AI").rglob("*.cs"):
        source = path.read_text(encoding="utf-8", errors="replace")
        name = re.search(r'\[AIName\("([^"]+)"\)\]', source)
        if not name:
            continue
        if args.only and path.stem.lower() != args.only.lower():
            continue

        mine = port_delays(source, not_delays | all_npc_ids)
        if not mine:
            continue

        theirs: set[int] = set()
        for npc_id in npcs_of_ai.get(name.group(1).lower(), []):
            theirs |= delays_of.get(pattern_of.get(npc_id, ""), set())
        if not theirs:
            continue  # no pattern to compare against; audit_missing_patterns covers that

        can_act = any(pattern_of.get(n, "") in actionable for n in npcs_of_ai.get(name.group(1).lower(), []))
        fixed_rate = "ScheduleAtFixedRateTask" in source
        varies = any(pattern_of.get(n, "") in variable for n in npcs_of_ai.get(name.group(1).lower(), []))
        unmatched = sorted(d for d in mine if d not in theirs)
        rows.append((len(unmatched), len(mine), path.stem, unmatched, sorted(theirs), can_act,
                     fixed_rate and varies))

    rows.sort(key=lambda r: (-r[0], r[2]))
    print(f"{len(rows)} classes schedule something and have a retail pattern to compare against.\n")
    print(f"{sum(1 for r in rows if r[5])} of them have a timer rung that does something other than "
          f"cast, which is the set a timing fix can act on.")
    print()
    print(f"{sum(1 for r in rows if r[6])} use a fixed-rate task where retail re-arms the same timer "
          f"with different delays, which a fixed rate cannot express.")
    print()
    for unmatched_n, mine_n, stem, unmatched, theirs, can_act, fixed_wrong in rows[: args.limit]:
        mark = "" if can_act else "   [casts only -- needs the skill index]"
        if fixed_wrong:
            mark += "   [FIXED RATE, retail rung varies]"
        print(f"{stem}: {unmatched_n} of {mine_n} port delays are not in retail's pattern{mark}")
        print(f"    port only: {unmatched}")
        print(f"    retail has: {theirs[:14]}{' ...' if len(theirs) > 14 else ''}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
