"""What a retail npc leaves behind when it dies, for the encounters no rotation table can reach.

WHY THIS EXISTS
---------------
`audit_missing_adds.py` -- the audit this whole effort began from -- is down from 812 missing adds to
266, and the remainder was classified by the handler retail uses to place them:

| handler | encounters |
|---|---|
| `on_battle_timer` | 82 |
| **`on_die`** | **77** |
| `on_leave_attack_state` | 39 |
| `on_wake_up` | 34 |
| `on_killed_by_user` | 30 |

`BattleCycles` now reads `on_die`, and it bought almost nothing, for a structural reason: **179 of the
196 encounters have no battle-timer rotation at all**, so there is nothing in that table to hang their
death handler off. A death spawn is not part of a rotation; it needs a table keyed on dying.

`DeathSpawnAI` already exists with nine npcs, hand-read from the patterns. Those keep their entries --
they carry curated comments and one of them (`ND2_ReA_1`) encodes a judgement about a betrayer npc that
is worth not regenerating over. Everything else comes from here.

THREE HANDLERS, ONE SLOT
------------------------
Retail splits `on_die` from `on_killed_by_user` and `on_killed_by_npc`: the first fires however the npc
died, the other two ask who did it. This port has one `OnDie` slot plus `When.KilledByPlayer` and
`When.KilledByNpc`, which is exactly the distinction, so each handler is emitted as `OnDie` with its
guard. `DeathSpawnAI`'s hand table drew the same line with a `PlayerKillOnly` flag before there was a
third case to carry.

WHAT IS LEFT OUT
----------------
The same rule as the rotation table: a pattern is taken only if **every** branch of the handler is
sayable in full, because branch lists are first-match-wins and dropping a rung promotes the next one.
The parsing is imported from `extract_battle_cycles` rather than rewritten, so the two tables cannot
drift on what an action means.

CLI:
    python extract_death_spawns.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
from client_npc_names import npc_names  # noqa: E402
from extract_idle_cycles import string_ids  # noqa: E402
from extract_battle_cycles import Unsayable, read_handler  # noqa: E402

#: Every class an npc may already be on and still acquire generated pattern rows.
#:
#: **This used to be per table, and that is what made the tables mutually exclusive.** Nothing here
#: excluded another table's npcs on purpose; the sets simply disagreed, and binding order did the rest
#: -- once the battle table moved an npc from `aggressive` to `battle_cycle`, no other extractor could
#: see it any more. The measured cost was 533 npcs holding a retail rotation their owning table could
#: not read, among much else.
#:
#: The set is now the same everywhere, so a pattern's handlers are read by every table that can read
#: them, and `GeneratedPattern` composes what each npc ends up with. The class an npc is bound to still
#: decides only one thing -- whether it fights -- which is why the generated class names are listed
#: here too: rebinding must be idempotent, or a second run would drop everything the first run took.
#:
#: `wake_variable` and `wake_variable_aggressive` are here, which they were not when the accepted set
#: was first unified: they were held back because those classes descended from `GeneralNpcAI` and
#: `AggressiveNpcAI` rather than `PatternAi`, so an npc bound there would carry rows that never ran.
#: They are `PassivePatternAi` and `PatternAi` now, keeping the aggression each was written to protect,
#: so the exclusion has no reason left. Their spawn-time variable write is an override and survives
#: unchanged -- **the tables do not subsume it**, and nothing here claims they do.
GENERIC = {"aggressive", "general", "battle_cycle", "death_spawn", "idle_cycle",
           "idle_cycle_passive", "aggressive_pattern", "passive_pattern",
           "wake_variable", "wake_variable_aggressive"}

#: Retail's three death handlers, and the guard each one becomes here. `on_die` fires however the npc
#: died; the other two ask who did it. Carrying the guard by name rather than as a flag is what let the
#: third be added without touching the shape of anything.
#:
#: `on_killed_by_npc` was worth adding on its own: variables written there gate **9,280** of retail's
#: placements, and this port had no way to say it until `When.KilledByNpc` existed.
HANDLERS = [("on_die", ""), ("on_killed_by_user", "KilledByPlayer"),
            ("on_killed_by_npc", "KilledByNpc")]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}
    strings = string_ids(args.repo)

    # Npcs an encounter class already models, including DeathSpawnAI's own hand-read nine.
    spoken_for: set[int] = set()
    for source in (args.repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs"):
        for found in re.finditer(r"=\s*(\d{6})\s*;",
                                 source.read_text(encoding="utf-8", errors="replace")):
            spoken_for.add(int(found.group(1)))
    hand = A.read_text(args.repo / "src/Aion.GameServer/Handlers/AI/DeathSpawnAI.cs")
    spoken_for.update(int(m.group(1)) for m in re.finditer(r"\[(\d{6})\] = new Bequest", hand))

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows: list[tuple] = []
    refused: collections.Counter = collections.Counter()
    patterns = 0
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            # A spawn or a spawn-variable write is enough to be worth taking: the writes are what
            # feed the conditional spawn engine, and this handler family is where 9,280 placements'
            # worth of them live.
            interesting = ("<spawn>", "<set_condition_spawn_variable>")
            if not any(any(k in found.group(1) for k in interesting)
                       for h, _ in HANDLERS
                       for found in [re.search(r"<%s>(.*?)</%s>" % (h, h), body, re.S)] if found):
                continue

            owners = [n for n in binders.get(named.group(1), [])
                      if ai.get(n) in GENERIC and n not in spoken_for]
            if not owners:
                refused["no npc here that is free to run it"] += 1
                continue

            try:
                read = {h: read_handler(body, h, dev, ai.keys(), strings) for h, _ in HANDLERS}
            except Unsayable as stopper:
                refused[str(stopper)] += 1
                continue

            if not any(read.values()):
                refused["nothing sayable in either handler"] += 1
                continue

            patterns += 1
            for npc in owners:
                for handler, killer in HANDLERS:
                    for index, priority, guards, actions in read[handler]:
                        for order, action in enumerate(actions):
                            rows.append((npc, named.group(1), handler, killer or "ANY",
                                         index, priority, "|".join(guards), order) + action)

    rows.sort(key=lambda r: (r[0], r[2], r[4], r[7]))
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\tpattern\thandler\tkiller\tbranch\tpriority\tguards\torder\t"
                  "kind\ta1\ta2\ta3\tplace\tx\ty\tz\tgroup\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    print(f"{patterns} death patterns across {len(npcs)} npcs, {len(rows)} actions -> {args.out}")
    for reason, count in refused.most_common(8):
        print(f"    {count:4d} refused: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
