"""Compare every ported spawn placement against retail's own numbers.

`spawn_on_multi_target` reads as "one add on everybody". **It almost never is.** Retail caps it with
`total_set_to_spawn` — at *one* in several fights — and `order_in_attacker_list` decides whether the cap
takes the top of the hate list or a random slice. Get either wrong and the fight is a different fight:
the cap by a factor of the raid size, the order by who gets hit.

Both fields were missing from `summarize_pattern.py` until recently, so every one of these had to be
read out of the raw XML by hand. That was done four times and it nearly shipped Stormwing's single
lightning as a raid-wide wave. This checks all of them at once instead.

**What it can and cannot do.** It matches a retail op to a C# call site by npc id, which is exact. It
cannot tell which *branch* a call site translates when a class spawns the same npc from several, so a
class whose op appears with two different caps in retail is reported as ambiguous rather than guessed
at. Read those by hand — the caps are in the output.

It now covers the single-target placements too — `spawn_on_target` and
`spawn_on_target_by_attacker_indicator` — where the fields that go wrong are `live_time`,
`spawn_range` and `valid_distance` rather than a cap. Those three were cross-checked by nothing at all,
and the first two mistakes this log records about them (Miladi's scatter, Yamennes' conditional sliver)
were both found by hand, one of them four passes late.

Usage:
    python audit_multi_target_caps.py [--patterns-dir DIR]
"""
from __future__ import annotations

import argparse
import collections
import io
import pathlib
import re

REPO = pathlib.Path(__file__).resolve().parents[2]
AI_DIR = REPO / "src" / "Aion.GameServer" / "Handlers" / "AI"
BINDING = pathlib.Path(__file__).parent / "out" / "ai_binding.tsv"
DEFAULT_PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

PATTERN = re.compile(r"<npc_ai_pattern>\s*<name>([^<]+)</name>(.*?)</npc_ai_pattern>", re.S)
MULTI = re.compile(r"<spawn_on_multi_target>(.*?)</spawn_on_multi_target>", re.S)
SINGLE = re.compile(
    r"<(spawn_on_target|spawn_on_target_by_attacker_indicator)>(.*?)</\1>", re.S)

CONST = re.compile(r"private const int (\w+)\s*=\s*(\d+);")
CALL = re.compile(
    r"SpawnOnEachTarget\(\s*(?P<npc>\w+)\s*,\s*\w+\s*,\s*(?:validDistance:\s*)?[^,]+,\s*"
    r"(?:maxTargets:\s*)?(?P<cap>\w+)\s*,\s*(?:order:\s*)?MultiTargetOrder\.(?P<order>\w+)",
    re.S,
)

# The single-target calls, whose arguments are all named after the first one or two. Captured as a
# blob and picked apart by name, because their order varies from class to class.
SINGLE_CALL = re.compile(
    r"Do\.Spawn(?P<kind>OnTarget|OnAttacker)\((?P<args>[^;]*?)\)\s*[,)]", re.S)
NAMED = re.compile(r"(\w+):\s*([\w.]+)")

ORDER = {"ORDERI_DESCENDING": "Descending", "ORDERI_ASCENDING": "Ascending", "ORDERI_RANDOM": "Random"}


CLASS = re.compile(r"^(?:public|internal)\s+(?:sealed\s+)?class\s+\w+", re.M)


def units(text: str) -> list[str]:
    """One chunk of source per class in the file.

    **Constants have to be scoped per class, not per file.** BollvigBlackheartAI.cs holds two classes
    and both define `VampireLife` -- 24000 in the boss, 2400 in the bat -- so a file-wide dictionary
    lets the second overwrite the first and reports the boss as wrong by a factor of ten. That is a
    false positive this tool produced on its first run against the single-target ops, and it is the
    same "two classes in one file" mode already recorded for report_dropped_guards.py.
    """
    starts = [m.start() for m in CLASS.finditer(text)]
    if not starts:
        return [text]
    bounds = starts + [len(text)]
    # Everything before the first class -- usings, file-scoped namespace -- belongs to nothing.
    return [text[bounds[i]:bounds[i + 1]] for i in range(len(starts))]


def num(value: str) -> float | None:
    """A number, or None when the call site computes it instead of naming a constant."""
    try:
        return float(value or 0)
    except ValueError:
        return None


def field(block: str, tag: str) -> str:
    hit = re.search(f"<{tag}>([^<]*)</{tag}>", block)
    return hit.group(1).strip() if hit else ""


def devname_to_id() -> dict[str, str]:
    out: dict[str, str] = {}
    with open(BINDING, encoding="utf-8") as fh:
        next(fh)
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) > 1 and cols[1]:
                out.setdefault(cols[1], cols[0])
    return out


def retail_ops(directory: pathlib.Path) -> dict[str, set[tuple[str, str]]]:
    """npc id -> {(cap, order)} across every multi-target op that spawns it."""
    ids = devname_to_id()
    out: dict[str, set[tuple[str, str]]] = collections.defaultdict(set)
    for path in sorted(directory.glob("NpcAIPatterns*.xml")):
        text = io.open(path, encoding="utf-16", errors="replace").read()
        for _name, body in PATTERN.findall(text):
            for block in MULTI.findall(body):
                npc_id = ids.get(field(block, "npc_nameid"))
                if not npc_id:
                    continue
                cap = field(block, "total_set_to_spawn") or "?"
                order = ORDER.get(field(block, "order_in_attacker_list"), "?")
                out[npc_id].add((cap, order))
    return out


def retail_single(directory: pathlib.Path) -> dict[str, set[tuple[str, str, str]]]:
    """npc id -> {(spawn_range, live_time, valid_distance)} across every single-target op."""
    ids = devname_to_id()
    out: dict[str, set[tuple[str, str, str]]] = collections.defaultdict(set)
    for path in sorted(directory.glob("NpcAIPatterns*.xml")):
        text = io.open(path, encoding="utf-16", errors="replace").read()
        for _name, body in PATTERN.findall(text):
            for _tag, block in SINGLE.findall(body):
                npc_id = ids.get(field(block, "npc_nameid"))
                if not npc_id:
                    continue
                out[npc_id].add((field(block, "spawn_range") or "0",
                                 field(block, "live_time") or "0",
                                 field(block, "valid_distance") or "0"))
    return out


def ported_single() -> list[tuple[str, str, str, str, str]]:
    """(file, npc id, range, liveSeconds, validDistance) for every single-target spawn we make."""
    rows = []
    for path in sorted(AI_DIR.glob("*.cs")):
      for text in units(path.read_text(encoding="utf-8")):
        consts = {name: value for name, value in CONST.findall(text)}
        floats = dict(re.findall(r"private const float (\w+)\s*=\s*([\d.]+)f;", text))
        for m in SINGLE_CALL.finditer(text):
            args = m.group("args")
            named = dict(NAMED.findall(args))
            # The npc id is the first positional argument, after the aggro target on SpawnOnAttacker.
            positional = [a.strip() for a in args.split(",") if ":" not in a]
            npc = next((p for p in positional if not p.startswith("AggroTarget")), None)
            if npc is None:
                continue

            def resolve(key: str, default: str = "0") -> str:
                raw = named.get(key)
                if raw is None:
                    return default
                raw = raw.rstrip("f")
                return consts.get(raw, floats.get(raw, raw)).rstrip("f")

            rows.append((path.name, consts.get(npc, f"?{npc}"),
                         resolve("range"), resolve("liveSeconds"), resolve("validDistance")))
    return rows


def ported_calls() -> list[tuple[str, str, str, str]]:
    """(file, npc id, cap, order) for every SpawnOnEachTarget this port makes."""
    rows = []
    for path in sorted(AI_DIR.glob("*.cs")):
      for text in units(path.read_text(encoding="utf-8")):
        consts = {name: value for name, value in CONST.findall(text)}
        for m in CALL.finditer(text):
            npc = consts.get(m.group("npc"))
            cap = m.group("cap")
            rows.append((path.name, npc or f"?{m.group('npc')}", consts.get(cap, cap), m.group("order")))
    return rows


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns-dir", type=pathlib.Path, default=DEFAULT_PATTERNS)
    ap.add_argument("--show-omissions", action="store_true",
                    help="list the placements that agree but do not pass retail's valid_distance")
    args = ap.parse_args()

    retail = retail_ops(args.patterns_dir)
    calls = ported_calls()

    mismatched = unknown = ambiguous = agreed = 0
    for filename, npc_id, cap, order in calls:
        want = retail.get(npc_id)
        if not want:
            print(f"NO RETAIL OP  {filename:<32} npc={npc_id} ours cap={cap} {order}")
            unknown += 1
        elif len(want) > 1:
            print(f"AMBIGUOUS     {filename:<32} npc={npc_id} ours cap={cap} {order} "
                  f"retail {sorted(want)}")
            ambiguous += 1
        elif (cap, order) in want:
            agreed += 1
        else:
            (rcap, rorder), = want
            print(f"MISMATCH      {filename:<32} npc={npc_id} ours cap={cap} {order} "
                  f"retail cap={rcap} {rorder}")
            mismatched += 1

    print(f"\n{len(calls)} ported multi-target ops: {agreed} agree, {mismatched} mismatch, "
          f"{ambiguous} ambiguous, {unknown} with no retail op found")

    print("\n--- single-target placements ---")
    retail1 = retail_single(args.patterns_dir)
    s_ok = s_bad = s_amb = s_unknown = s_unread = s_novalid = 0
    for filename, npc_id, rng, live, valid in ported_single():
        want = retail1.get(npc_id)
        if not want:
            s_unknown += 1
            continue

        ours = (num(rng), num(live), num(valid))
        if ours[0] is None or ours[1] is None:
            # A call whose argument is computed rather than named -- a per-band table, say. The tool
            # cannot read those, and says so rather than reporting a false mismatch.
            print(f"NOT CONSTANT  {filename:<32} npc={npc_id} ours range/live={rng}/{live}")
            s_unread += 1
            continue

        norm = {(num(a), num(b), num(c)) for a, b, c in want}

        # `valid_distance` is unmodelled on most placements: the parameter arrived late and only seven
        # classes pass it. Reporting every one of the rest as a mismatch would bury the rows that
        # matter, so a call agreeing on range and lifetime and simply omitting the guard is its own
        # category -- a known gap, not a wrong number.
        if ours in norm:
            s_ok += 1
        elif ours[2] == 0 and any(a == ours[0] and b == ours[1] for a, b, _ in norm):
            want_valid = sorted({c for a, b, c in norm if a == ours[0] and b == ours[1]})
            if args.show_omissions:
                print(f"NO GUARD      {filename:<32} npc={npc_id} retail valid_distance={want_valid}")
            s_novalid += 1
        elif len(norm) > 1:
            print(f"AMBIGUOUS     {filename:<32} npc={npc_id} ours {ours} retail {sorted(norm)}")
            s_amb += 1
        else:
            print(f"MISMATCH      {filename:<32} npc={npc_id} ours range/live/valid={ours} "
                  f"retail {sorted(norm)[0]}")
            s_bad += 1

    total = s_ok + s_bad + s_amb + s_unknown + s_unread + s_novalid
    print(f"\n{total} ported single-target ops: {s_ok} agree, {s_bad} mismatch, {s_amb} ambiguous, "
          f"{s_novalid} agree but omit valid_distance, {s_unread} not constant, "
          f"{s_unknown} with no retail op found")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
