"""What `SKILLI_INDEX_N` actually means: every npc's ordered skill list, resolved to skill ids.

WHY THIS EXISTS
---------------
This has been the largest single blocker in the project. Retail's `use_skill` names a skill as
`SKILLI_INDEX_1` -- a 0-based index into the npc's own ordered skill list -- and that list was recorded
here as *server-side data we do not have*, verified by indexing all 525,657 entries of the client pak.
164 battle rotations and 209 patterns by the looser audit are refused for this one reason.

**The list is in the 5.8 server dump.** The earlier check was against the client; nobody had looked in
`npcs.xml`, where every `<npc>` carries a `<skills>` block of `<skill_name>` entries **in order**, and
`skill_base.xml` carries the `name` -> `id` join for all of them. Two files nobody had read, and the
chain closes:

    SKILLI_INDEX_N  ->  npcs.xml <skills> entry N  ->  skill_base.xml <name>  ->  skill id

WHY THIS IS NOT JUST ASSUMED TO WORK
------------------------------------
The order in this port's own `npc_skills.xml` is **known wrong** for some bosses -- Tiamat's
incarnations list 20105, 20145, 20146 while the pattern's indices are 0=20145, 1=20146, 2=20105 -- so a
resolver that merely produced *some* answer would be indistinguishable from the bug it replaces. Two
facts were established here by other means, and `NpcSkillListTests` checks this table against both:

* Tiamat's incarnations, settled from `stack=` names matching the branch comments.
* Haramel's Hameroon, index 1 = 19210, settled from a self-buff firing where he spawns his adds and
  corroborated by `skill_no="2"` in the shout data.

If either disagrees, the resolver is wrong and the table must not be used.

WHAT THE COLUMNS MEAN
---------------------
* `index` -- the number retail writes in `SKILLI_INDEX_<index>`.
* `skill` -- resolved **two independent ways**, which is the point. `skill_base.xml` gives retail's own
  id for the name; separately, this port's `skill_templates.xml` carries the same retail name in its
  `stack=` attribute, uppercased, giving an id without trusting that 5.8 and 4.8 number skills alike.
  Where both answer they agree on 99%+ of entries, and the `stack=` answer wins because it is this
  port's own numbering. Disagreements are reported, not silently resolved.
* `here` -- whether this port has a template for the id. Retail is 5.8 and this port 4.8, so a skill
  that postdates 4.8 resolves to a number nothing here can cast; those are marked rather than dropped.

CLI:
    python extract_npc_skill_lists.py <xml_dir> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402

SKILLS_RE = re.compile(r"<skills>(.*?)</skills>", re.S)
NAME_RE = re.compile(r"<skill_name>([^<]+)</skill_name>")
LEVEL_RE = re.compile(r"<skill_level>(\d+)</skill_level>")


def skill_ids(path: pathlib.Path) -> dict[str, int]:
    """name -> id, streamed: skill_base.xml is thirty-eight megabytes of UTF-16."""
    out: dict[str, int] = {}
    buffered = ""
    with io.open(path, "r", encoding="utf-16", errors="replace") as handle:
        while True:
            block = handle.read(1 << 22)
            if not block:
                break
            buffered += block
            records = buffered.split("</skill_base>")
            buffered = records.pop()
            for record in records:
                number = re.search(r"<id>(\d+)</id>", record)
                named = re.search(r"<name>([^<]+)</name>", record)
                if number and named:
                    out[named.group(1).strip()] = int(number.group(1))
    return out


def npc_skills(path: pathlib.Path):
    """(npc id, [(name, level)]) for every npc that has a skill list. Streamed: 518MB of UTF-16."""
    buffered = ""
    with io.open(path, "r", encoding="utf-16", errors="replace") as handle:
        while True:
            block = handle.read(1 << 22)
            if not block:
                break
            buffered += block
            records = buffered.split("</npc>")
            buffered = records.pop()
            for record in records:
                found = SKILLS_RE.search(record)
                if not found:
                    continue
                number = re.search(r"<id>(\d+)</id>", record)
                if not number:
                    continue
                entries = []
                for entry in re.finditer(r"<data>(.*?)</data>", found.group(1), re.S):
                    named = NAME_RE.search(entry.group(1))
                    if not named:
                        continue
                    level = LEVEL_RE.search(entry.group(1))
                    entries.append((named.group(1).strip(),
                                    int(level.group(1)) if level else 1))
                if entries:
                    yield int(number.group(1)), entries


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("xml_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    ids = skill_ids(args.xml_dir / "skill_base.xml")
    templates = A.read_text(args.repo / "game-server/data/static_data/skills/skill_templates.xml")
    known = {int(m.group(1)) for m in re.finditer(r'<skill_template skill_id="(\d+)"', templates)}
    # This port's own number for the same skill: retail's name, uppercased, in `stack=`.
    ours: dict[str, int] = {}
    for found in re.finditer(r'<skill_template skill_id="(\d+)"[^>]*?\bstack="([^"]+)"', templates):
        ours.setdefault(found.group(2).strip().upper(), int(found.group(1)))

    rows: list[tuple] = []
    unresolved = 0
    agreed = disagreed = only_ours = 0
    for npc, entries in npc_skills(args.xml_dir / "npcs.xml"):
        for index, (name, level) in enumerate(entries):
            retail = ids.get(name, 0)
            mine = ours.get(name.upper(), 0)
            if retail and mine:
                if retail == mine:
                    agreed += 1
                else:
                    disagreed += 1
            elif mine:
                only_ours += 1
            skill = mine or retail
            if not skill:
                unresolved += 1
            rows.append((npc, index, name, skill, level,
                         "TRUE" if skill in known else "FALSE"))

    rows.sort()
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\tindex\tname\tskill\tlevel\there\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    here = sum(1 for r in rows if r[5] == "TRUE")
    print(f"{len(rows)} skill entries across {len(npcs)} npcs -> {args.out}")
    print(f"    {len(ids)} names in skill_base.xml; {unresolved} entries name a skill it does not have")
    print(f"    {here} resolve to a skill this port can cast, {len(rows) - here} do not")
    print(f"    the two joins agree on {agreed}, disagree on {disagreed}; "
          f"{only_ours} are named only by this port's own templates")
    return 0


if __name__ == "__main__":
    sys.exit(main())
