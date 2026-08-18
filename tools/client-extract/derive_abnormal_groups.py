"""Try to derive retail's ABNSTATEI_*_GROUP names from the game's own dispel categories. It fails.

`on_enter_abnormal_state` is the largest retail handler this port has no event for -- 272 patterns,
1,168 live npcs -- and it is blocked on three names the branches use as guards:

    ABNSTATEI_MENTAL_GROUP        919 live npcs
    ABNSTATEI_CANNOT_ACT_GROUP    206
    ABNSTATEI_PHYSICAL_GROUP        3

Nothing readable defines them: not the Java tree, and not any `.pak` in the client (a scan of all 3,332
for the string `MENTAL_GROUP` returns nothing). The obvious remaining source is the game's own
taxonomy. Aion really does divide debuffs into mental and physical -- the wiki describes mental
conditions as "sleep, fear or paralyze", removed by Cure Mind, against physical ones removed by Dispel,
"with the exception of Stun" which needs Remove Shock. Three player-facing families, three retail group
names, and `skill_templates.xml` carries the split as `dispel_category` on 2,800 skills:
`DEBUFF_MENTAL`, `DEBUFF_PHYSICAL` and `STUN`.

**It does not work, and this script is here to show why rather than to be run for an answer.**

`dispel_category` is a property of the *skill*, not of the state it inflicts, and the same state is
inflicted by skills of different categories. Restricting to skills carrying exactly one state-bearing
effect -- which removes every trace of multi-effect contamination -- the three categories still overlap:

    MENTAL   ^ PHYSICAL   CURSE, DEFORM, PARALYZE, SILENCE, SNARE
    MENTAL   ^ STUN       PARALYZE
    PHYSICAL ^ STUN       OPENAERIAL, PARALYZE, STUN

**PARALYZE is in all three.** So no partition of `AbnormalState` bits can be read out of this table, and
a set built from the dominant members would be a guess dressed as a derivation -- the worst kind,
because it would look sourced.

What the run *does* establish, and what is used:

  * **`STUN` is 90% `STUN`**, and its members are exactly `SPIN | STUN | STUMBLE | STAGGER` plus
    `OPENAERIAL`. Our `AbnormalState.ANY_STUN` is `SPIN | STUN | STUMBLE | STAGGER`. That corroborates
    `ABNSTATEI_STUN_LIKE_GROUP` -> `ANY_STUN` from an independent direction.
  * **The dominant mental states are PARALYZE, SLEEP, FEAR, CONFUSE and DEFORM** -- 87% of the
    unambiguous mental skills, and a superset of the wiki's three examples. Suggestive, not a
    definition.

**What would settle it:** a client build whose string table still carries the group names, an NCSoft
tools dump, or a server writeup that lists the members. Until then the handler stays unbuilt; see
docs/retail-ai-fidelity.md.

Usage:
    python derive_abnormal_groups.py [--repo ..] [--floor N]
"""
from __future__ import annotations

import argparse
import collections
import itertools
import pathlib
import re

CATEGORIES = ("DEBUFF_MENTAL", "DEBUFF_PHYSICAL", "STUN")


def effect_states(repo: pathlib.Path) -> dict[str, str]:
    """Effect element name -> AbnormalState, read from the effect classes themselves."""
    out: dict[str, str] = {}
    for path in (repo / "src/Aion.GameServer/SkillEngine/Effect").glob("*.cs"):
        hit = re.search(r"SetAbnormal\(AbnormalState\.(\w+)\)",
                        path.read_text(encoding="utf-8", errors="replace"))
        if hit:
            out[path.stem.replace("Effect", "").lower()] = hit.group(1)
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--floor", type=int, default=3,
                    help="ignore states carried by fewer than this many skills")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    states = effect_states(repo)

    text = (repo / "game-server/data/static_data/skills/skill_templates.xml").read_text(
        encoding="utf-8", errors="replace")

    clean: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    total: collections.Counter = collections.Counter()
    for skill in re.finditer(r"<skill_template\b.*?</skill_template>", text, re.S):
        body = skill.group(0)
        category = re.search(r'dispel_category="([^"]+)"', body)
        if not category:
            continue
        total[category.group(1)] += 1
        carried = {states[e.lower()] for e in re.findall(r"<(\w+)\s", body) if e.lower() in states}
        # Only skills that inflict exactly one state can attribute it to their category.
        if len(carried) == 1:
            clean[category.group(1)][carried.pop()] += 1

    for category in CATEGORIES:
        counted = sum(clean[category].values())
        print(f"== {category}   ({counted} of {total[category]} skills unambiguous)")
        for state, count in clean[category].most_common():
            print(f"    {state:<16} {count:4}  ({100.0 * count / counted:.0f}%)")
        print()

    sets = {c: {s for s, n in clean[c].items() if n >= args.floor} for c in CATEGORIES}
    print(f"members carried by at least {args.floor} skills:")
    for category, members in sets.items():
        print(f"   {category:<16} {' | '.join(sorted(members))}")
    print()

    print("overlaps -- this is the result:")
    clashes = False
    for a, b in itertools.combinations(CATEGORIES, 2):
        shared = sorted(sets[a] & sets[b])
        clashes = clashes or bool(shared)
        print(f"   {a:<16} ^ {b:<16} {', '.join(shared) or 'none'}")
    print()
    if clashes:
        print("The categories are not disjoint, so they cannot define the ABNSTATEI groups.")
        print("dispel_category is a property of the skill, not of the state. See the module docstring.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
