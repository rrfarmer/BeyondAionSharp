"""Find npcs retail asks to cast that this port never asks.

`audit_skilless_casters.py` answers the narrow question -- npcs **bound** to a cast-and-die AI with no
skill row. Raksang's torment blaze slipped straight through it: retail's `IDRaksha_NoshowNPC_15` casts
`SKILLI_INDEX_0` on waking, on seeing a player and on a broadcast, and our 282459 is bound `general`
with no skill row, so it is scenery. Nothing was wrong with its binding *as written* -- the fault is
that retail's pattern casts and ours has no way to.

So this asks the wider question: **for every npc whose retail pattern contains a `use_skill`, does this
port give it any means of casting?** An npc is listed when its AI here is inert (`general`, `noaction`,
`dummy`, `passive`) or a caster AI, and it has no row anywhere under `static_data/npc_skills/`.

**A listing is not a defect on its own.** Two other routes supply a cast and neither touches
`npc_skills`, so each hit is annotated rather than filtered out:

* the arena bonus table in `PvPArenaScore.GetNpcBonusSkill`, which holds `(skill << 8) | level` per npc
  id -- this is how the Harmony arena traps fire, and their pattern is full of `use_skill` while their
  skill list is legitimately empty;
* any handler that names the id and casts through `SkillEngine` -- Tiamat's Eye is cast *for* by
  `BrigadeGeneralLaksyakaAI`, for instance.

Annotating rather than excluding is deliberate, and was a correction: the first version of this audit
filtered those out, the filter silently matched nothing, and the totals looked the same either way.
An exclusion that is subtly wrong removes real defects from the report and nothing ever says so; a
wrong annotation sits visibly on the line next to the row.

**Read the totals with care.** Most hits are ordinary world npcs -- guards, quest mobs, wildlife --
whose aionemu templates simply carry no skill list. That is a broad data gap rather than a broken
mechanic. The rows worth reading are the ones a boss or instance class places, which is what
`--placed-by-code` selects.

Usage:
    python audit_silent_hazards.py [--patterns DIR] [--repo PATH] [--placed-by-code] [--all]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

DEFAULT_PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

#: AI names that cannot cast on their own initiative.
INERT = {"general", "noaction", "dummy", "passive", "none", "npc"}

#: AI names whose whole job is to cast; silent when the skill list is empty.
CASTERS = {"useskillanddie", "useskillonspawn", "skillarea"}


def patterns_that_cast(patterns_dir: pathlib.Path) -> set[str]:
    """Every pattern name whose body contains a use_skill action."""
    casting: set[str] = set()
    for path in patterns_dir.rglob("*.xml"):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", text, re.S):
            if "<use_skill>" in m.group(2) or "<use_skill_by_attacker_indicator>" in m.group(2):
                casting.add(m.group(1).strip().lower())
    return casting


def ids_with_skill_rows(static: pathlib.Path) -> set[int]:
    found: set[int] = set()
    for path in (static / "npc_skills").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for group in re.findall(r'npc_ids="([^"]+)"', text):
            for npc_id in group.split():
                if npc_id.isdigit():
                    found.add(int(npc_id))
    return found


def arena_bonus_ids(repo: pathlib.Path) -> set[int]:
    """Npc ids the arena bonus-skill table casts for."""
    score = repo / "src/Aion.GameServer/Model/Instance/Instancescore/PvPArenaScore.cs"
    if not score.exists():
        return set()
    body = score.read_text(encoding="utf-8", errors="replace")
    start = body.find("GetNpcBonusSkill")
    if start < 0:
        return set()
    end = body.find("public ", start + 1)
    return {int(n) for n in re.findall(r"case (\d+):", body[start:end if end > 0 else len(body)])}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns", default=str(DEFAULT_PATTERNS))
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--placed-by-code", action="store_true",
                    help="only npcs an AI or instance handler places, which is where mechanics live")
    ap.add_argument("--all", action="store_true", help="include npcs nothing in this port places")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    static = repo / "game-server" / "data" / "static_data"
    handlers = repo / "src" / "Aion.GameServer" / "Handlers"

    casting = patterns_that_cast(pathlib.Path(args.patterns))
    with_skills = ids_with_skill_rows(static)
    bonus_table = arena_bonus_ids(repo)

    ai_of: dict[int, str] = {}
    name_of: dict[int, str] = {}
    templates = (static / "npcs" / "npc_templates.xml").read_text(encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        ai = re.search(r'\bai="([^"]*)"', attrs)
        name = re.search(r'\bname="([^"]*)"', attrs)
        ai_of[int(npc_id)] = ai.group(1) if ai else ""
        name_of[int(npc_id)] = (name.group(1) if name else "").strip()

    pattern_of: dict[int, str] = {}
    binding = repo / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in binding.read_text(encoding="utf-8", errors="replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 3 and parts[0].isdigit():
            pattern_of[int(parts[0])] = parts[2].strip()

    placed_by: dict[int, set[str]] = collections.defaultdict(set)
    casts_in: dict[int, set[str]] = collections.defaultdict(set)
    by_code: set[int] = set()
    for path in handlers.rglob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        mentioned = {int(n) for n in re.findall(r"\b(\d{6})\b", text)}
        can_cast = "SkillEngine" in text or "UseSkill" in text or "QueueSkill" in text
        posix = path.as_posix()
        is_mechanic = "/AI/" in posix or "/Instance/" in posix
        for npc_id in mentioned:
            placed_by[npc_id].add(path.name if is_mechanic else f"{path.name} (script)")
            if is_mechanic:
                by_code.add(npc_id)
            if can_cast:
                casts_in[npc_id].add(path.name)
    for path in (static / "spawns").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for npc_id in set(re.findall(r'<spawn npc_id="(\d+)"', text)):
            placed_by[int(npc_id)].add("(spawn table)")

    silent = []
    for npc_id, pattern in pattern_of.items():
        if pattern.lower() not in casting or npc_id in with_skills:
            continue
        ai = ai_of.get(npc_id, "")
        if ai.lower() not in INERT and ai.lower() not in CASTERS:
            continue
        silent.append((npc_id, ai, pattern))

    rows = sorted(silent)
    if not args.all:
        rows = [r for r in rows if placed_by.get(r[0])]
    if args.placed_by_code:
        rows = [r for r in rows if r[0] in by_code]

    print(f"{len(casting)} retail patterns cast a skill.")
    print(f"{len(silent)} npcs on those patterns have no skill row and no AI that casts.")
    print(f"{sum(1 for r in silent if placed_by.get(r[0]))} are placed by something here; "
          f"{sum(1 for r in silent if r[0] in by_code)} by an AI or instance handler.")
    print("Rows marked CAST-FOR are answered elsewhere and are probably fine.\n")

    for npc_id, ai, pattern in rows:
        where = ", ".join(sorted(placed_by.get(npc_id, ()))[:3]) or "nothing places it"
        note = ""
        if npc_id in bonus_table:
            note = "   CAST-FOR: arena bonus-skill table"
        elif casts_in.get(npc_id):
            note = "   CAST-FOR? " + ", ".join(sorted(casts_in[npc_id])[:2])
        print(f"{npc_id}  {name_of.get(npc_id, '') or '(unnamed)':34s} ai={ai:16s} {pattern}{note}")
        print(f"          {where}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
