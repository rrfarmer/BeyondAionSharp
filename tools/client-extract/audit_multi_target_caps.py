"""Compare every ported `spawn_on_multi_target` against retail's cap and order.

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

CONST = re.compile(r"private const int (\w+)\s*=\s*(\d+);")
CALL = re.compile(
    r"SpawnOnEachTarget\(\s*(?P<npc>\w+)\s*,\s*\w+\s*,\s*(?:validDistance:\s*)?[^,]+,\s*"
    r"(?:maxTargets:\s*)?(?P<cap>\w+)\s*,\s*(?:order:\s*)?MultiTargetOrder\.(?P<order>\w+)",
    re.S,
)

ORDER = {"ORDERI_DESCENDING": "Descending", "ORDERI_ASCENDING": "Ascending", "ORDERI_RANDOM": "Random"}


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


def ported_calls() -> list[tuple[str, str, str, str]]:
    """(file, npc id, cap, order) for every SpawnOnEachTarget this port makes."""
    rows = []
    for path in sorted(AI_DIR.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        consts = {name: value for name, value in CONST.findall(text)}
        for m in CALL.finditer(text):
            npc = consts.get(m.group("npc"))
            cap = m.group("cap")
            rows.append((path.name, npc or f"?{m.group('npc')}", consts.get(cap, cap), m.group("order")))
    return rows


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns-dir", type=pathlib.Path, default=DEFAULT_PATTERNS)
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
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
