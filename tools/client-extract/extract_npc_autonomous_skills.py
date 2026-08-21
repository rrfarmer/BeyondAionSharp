"""Retail's own answer to "what does this npc cast when nothing tells it to".

WHY THIS EXISTS
---------------
Retail's `<skills>` block in `npcs.xml` does two jobs. It is the ordered list `SKILLI_INDEX_N` points
into -- which `extract_npc_skill_lists.py` reads -- and it carries `skill_rate`, which marks the
skills the npc **also uses on its own**, outside any AI pattern.

This port reads the first job and not the second. `SkillAttackManager.ChooseNextSkill` prefers a
queued skill and falls through to the npc's own `NpcSkillList` when nothing is queued, so an npc with
no `npc_skills` entry never casts anything a pattern did not ask for. 47,179 npcs in the dump have no
port-side list at all, and 24,822 npcs have at least one skill retail casts autonomously.

WHAT `skill_rate` MEANS, AND HOW THAT WAS SETTLED
------------------------------------------------
This port's `prob` is a percentage: `NpcSkillTemplateEntry.ChanceReady` is `Rnd.Chance() < prob` and
`Rnd.Chance()` returns 0-100. Retail's values run 15, 50, 100, 120, 150, 200, 300, 400, 500, 1000,
2000, so they are plainly not percentages. **Per-mille was not assumed; three independent checks
agree on it:**

1. **The port's own hand-tuned values.** 40,717 (npc, skill) pairs exist in both trees. Most carry
   aionemu's default `prob="25"` and say nothing, but where somebody chose a number, the dominant
   agreement is `prob=100` against `rate=1000` -- 104 pairs. 1000 per-mille is certainty, and an
   independent author reading the same encounter arrived at 100%.
2. **They are not weights.** If `skill_rate` were a pick-one-of-N weight the rates would normalise.
   Of the 8,094 npcs with more than one autonomously-cast skill, the rates sum to 1000 in **63** --
   0.8%. They are independent probabilities.
3. **Only per-mille makes the whole value set meaningful.** The commonest configuration is three
   skills summing to 300. Per-mille that is 10% each, which is a sane cast rate; as percentages it
   would be three guaranteed casts every time the npc could act.

A factor-of-ten error here gives npcs that either spam their skills or never use them, and neither
reads as a bug from a test -- which is why it was measured rather than guessed.

WHAT IS DELIBERATELY LEFT ALONE
-------------------------------
* **Npcs the port already has an entry for.** Those carry aionemu's own tuning, and overwriting it
  would be trading one source for another in encounters nobody asked about. This is purely additive.
* **`cd` comes from `skill_base.xml`'s `delay_time`**, in milliseconds, the same unit the port uses.
  Only 5,037 of 14,457 skills declare one; the rest get 0, which is what a missing entry already
  behaves as.
* **Rate-0 skills are still emitted**, with `prob="0"`. They are never chosen -- `Rnd.Chance() < 0`
  is false -- but they put the npc's real ordered list in front of `When.SkillReady`, which is what
  that condition consults.

CLI:
    python extract_npc_autonomous_skills.py <xml_dir> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import io
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402

#: Retail states rates per mille; this port's `prob` is a percentage. Values above 1000 mean
#: "certain", and are clamped rather than allowed to exceed 100.
PER_MILLE = 10


def skill_delays(path: pathlib.Path) -> dict[str, int]:
    """Skill name -> cooldown in milliseconds. Streamed; the file is 74MB of UTF-16."""
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
                named = re.search(r"<name>([^<]+)</name>", record)
                delay = re.search(r"<delay_time>(\d+)</delay_time>", record)
                if named:
                    out[named.group(1).strip()] = int(delay.group(1)) if delay else 0
    return out


def npc_rates(path: pathlib.Path):
    """(npc id, [(skill name, level, rate)]) for every npc with a list. 518MB of UTF-16, streamed."""
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
                skills = re.search(r"<skills>(.*?)</skills>", record, re.S)
                number = re.search(r"<id>(\d+)</id>", record)
                if not skills or not number:
                    continue
                entries = []
                for entry in re.finditer(r"<data>(.*?)</data>", skills.group(1), re.S):
                    named = re.search(r"<skill_name>([^<]+)</skill_name>", entry.group(1))
                    level = re.search(r"<skill_level>(\d+)</skill_level>", entry.group(1))
                    rate = re.search(r"<skill_rate>(\d+)</skill_rate>", entry.group(1))
                    if named:
                        entries.append((named.group(1).strip(),
                                        int(level.group(1)) if level else 1,
                                        int(rate.group(1)) if rate else 0))
                if entries:
                    yield int(number.group(1)), entries


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("xml_dir", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    delays = skill_delays(args.xml_dir / "skill_base.xml")

    # The name -> id join, already made and checked against two known encounters by
    # `extract_npc_skill_lists.py`. Reusing it rather than redoing it keeps one answer in one place.
    resolved: dict[tuple[int, str], tuple[int, bool]] = {}
    for line in (args.repo / "tools/client-extract/out/npc_skill_lists.tsv").read_text(
            encoding="utf-8").splitlines()[1:]:
        fields = line.split("\t")
        resolved[(int(fields[0]), fields[2])] = (int(fields[3]), fields[5] == "TRUE")

    # Npcs this port already speaks for. Additive only: their tuning stands.
    spoken_for: set[int] = set()
    for path in (args.repo / "game-server/data/static_data").rglob("*.xml"):
        if "npc_skill" not in path.name and "npc_skills" not in str(path.parent):
            continue
        text = A.read_text(path)
        for block in re.finditer(r'<npc_skills\s+npc_ids="([^"]+)"', text):
            spoken_for.update(int(n) for n in block.group(1).split())

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    known = {int(m.group(1)) for m in re.finditer(r'npc_template npc_id="(\d+)"', templates)}

    rows: list[tuple] = []
    refused: collections.Counter = collections.Counter()
    npcs = 0
    for npc, entries in npc_rates(args.xml_dir / "npcs.xml"):
        if npc in spoken_for:
            refused["this port already has a list for it"] += 1
            continue
        if npc not in known:
            refused["no npc template here"] += 1
            continue
        taken = []
        for index, (name, level, rate) in enumerate(entries):
            found = resolved.get((npc, name))
            if found is None or not found[1]:
                # A skill this port has no template for cannot be cast, and an entry naming one would
                # put an unusable skill in front of `ChooseNextSkill`.
                refused["skill this port cannot cast"] += 1
                continue
            taken.append((index, found[0], level, min(100, rate // PER_MILLE),
                          delays.get(name, 0)))
        if not taken:
            continue
        npcs += 1
        for index, skill, level, prob, cooldown in taken:
            rows.append((npc, index, skill, level, prob, cooldown))

    with args.out.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("npc\tindex\tskill\tlevel\tprob\tcooldown\n")
        for row in sorted(rows):
            handle.write("\t".join(str(v) for v in row) + "\n")

    casting = len({r[0] for r in rows if r[4] > 0})
    print(f"{npcs} npcs, {len(rows)} skills -> {args.out}")
    print(f"    {casting} of them cast at least one skill on their own")
    for reason, count in refused.most_common(4):
        print(f"    {count} refused: {reason}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
