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
HP_BOUND_RE = re.compile(
    r"<is_hp_in_boundary>.*?<larger_than>(\d+)</larger_than>.*?<less_than>(\d+)</less_than>", re.S)


def load_binding(path: pathlib.Path) -> dict[str, str]:
    """npc_id -> pattern name"""
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        out[npc_id] = pattern
    return out


def pattern_thresholds(patterns_dir: pathlib.Path) -> dict[str, set[int]]:
    out: dict[str, set[int]] = {}
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            vals = {int(v) for v in HP_LOWER_RE.findall(body)}
            for lo, hi in HP_BOUND_RE.findall(body):
                vals.add(int(hi))
            if vals:
                out.setdefault(m.group(1), set()).update(vals)
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

        for npc_id in by_ai.get(name.group(1), []):
            pattern = binding.get(npc_id)
            retail = thresholds.get(pattern) if pattern else None
            if not retail:
                continue
            missing = [v for v in ours if v not in retail]
            if missing:
                rows.append((path.name, name.group(1), npc_id, pattern,
                             ours, sorted(retail, reverse=True), missing))
            break  # one representative npc_id per AI is enough

    print(f"AI classes using HpPhases with a resolvable retail pattern: "
          f"{len({r[1] for r in rows})} mismatching\n")
    for fname, ai, npc_id, pattern, ours, retail, missing in sorted(rows):
        print(f"{fname}  [{ai}]  npc {npc_id}  pattern {pattern}")
        print(f"    ours   : {ours}")
        print(f"    retail : {retail}")
        print(f"    ours-only (not a retail threshold): {missing}")


if __name__ == "__main__":
    main()
