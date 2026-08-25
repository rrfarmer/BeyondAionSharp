"""Fields retail writes inside an element that no reader ever looks for.

**This shape has cost more than any other single mistake in this port.** The reader searches an
element for the two or three fields it expects, and anything else retail wrote is dropped in silence
--- no refusal, no tally line, nothing. Three found by hand, all of them large:

* `broadcast_message param_obj` -- **all 6,822 uses carry one**, and 12,362 table rows reached their
  listeners with a null parameter while 1,247 rows on the listening side read exactly that parameter.
* `switch_target_by_attacker_indicator points_to_add` -- 7,169 rows that turned an npc's head without
  moving the hate list, so retail's peel onto the second or third most hated could not hold.
* `is_world_flag_var flag_expected` -- **16 of its 19 uses expect FALSE**, and the field was unread, so
  those sixteen guards asked the opposite question and fired exactly when they should not.

So the check is mechanical: list what retail writes inside each element, list what the reader searches
for, and report the difference.

It is deliberately crude, in the same way `check_loader_names.py` is. It splits the reader on its
`elif kind ==` clauses and looks for a literal `<tag>` in the clause text, so a field read through a
helper -- `timer_slot`, `SKILLI_INDEX_n` -- looks unread. Those are listed in `EXPLAINED` with the
reason, which is the point: **every field retail writes is either read, or written down here as a
decision.** A new one shows up as a new line.
"""
from __future__ import annotations

import collections
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent

#: (element, field) -> why it is not searched for literally. Anything not here is a finding.
EXPLAINED = {
    # Read through a helper rather than a literal tag search.
    ("add_battle_timer", "btimer_indicator"): "read by timer_slot()",
    ("is_battle_timer_indicator", "btimer_indicator"): "read by timer_slot()",
    ("add_hate_point", "point_to_add"): "read by the <point[s]?_to_add> pattern",
    ("use_skill", "skill"): "read as SKILLI_INDEX_n",
    ("attack_most_hating", "skill"): "read as SKILLI_INDEX_n",
    ("is_skill_count_left", "skill"): "read as SKILLI_INDEX_n",
    ("use_skill_by_attacker_indicator", "skill"): "read as SKILLI_INDEX_n",
    ("spawn_on_target", "despawn_at_attack_state"): "read; the clause split hides it",
    ("spawn_on_multi_target", "despawn_at_attack_state"): "read; the clause split hides it",
    ("spawn_on_target_by_attacker_indicator", "despawn_at_attack_state"): "read; clause split",
    ("spawn_on_target_by_attacker_indicator", "num_to_spawn"): "read; clause split",

    # Retail writes it and it carries nothing.
    ("broadcast_message", "param1"): "always 0 in all 6,822 uses",
    ("broadcast_message", "param2"): "always 0 in all 6,822 uses",

    # Deliberately unread, with the reason recorded in docs/retail-ai-backlog.md.
    ("switch_target", "percent_to_add"): "the element does not say what the percentage is of",
    ("switch_target_by_attacker_indicator", "percent_to_add"): "same",
    ("switch_target_by_attacker_indicator", "restricted_range"): "unstated over valid_distance",
    ("display_system_message", "area_name"): "message substitution this port does not take",
    ("display_system_message", "string_param1"): "message substitution",
    ("display_system_message", "string_param2"): "message substitution",
    ("display_system_message", "string_param3"): "message substitution",
    ("flee_from", "push_state"): "unstated",
    ("spawn_on_multi_target", "num_to_spawn"): "read; carried in the kind when above one",

    # The bare <spawn> element is read by a different pass entirely.
    **{("spawn", tag): "read by the spawn pass"
       for tag in ("y", "z", "dir", "spawn_range", "is_aerial_spawn", "except_specialize")},
}

#: Fields that are never a finding anywhere: retail's own noise.
IGNORED_TAGS = {"skill_level", "skill_Level"}


def clauses(source: str) -> dict[str, str]:
    """Each `elif kind == "x":` clause of the readers, by the kinds it handles."""
    out: dict[str, str] = {}
    for part in re.split(r'\n        (?:el)?if kind (?:==|in) ', source)[1:]:
        head, _, rest = part.partition(":\n")
        body = rest.split("\n        elif kind ")[0]
        for name in re.findall(r'"(\w+)"', head):
            out[name] = body
    return out


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    dump = pathlib.Path(sys.argv[1] if len(sys.argv) > 1
                        else "D:/Aion58ServerTesting/Server/Map/XML")
    if not dump.is_dir():
        print(f"retail pattern dump not found at {dump}", file=sys.stderr)
        return 2

    handlers = clauses((HERE / "extract_battle_cycles.py").read_text(encoding="utf-8"))
    writes: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    for f in dump.glob("NpcAIPatterns*.xml"):
        text = f.read_text(encoding="utf-16", errors="ignore")
        for kind in handlers:
            for m in re.finditer(r"<%s>(.*?)</%s>" % (kind, kind), text, re.S):
                for tag in re.findall(r"<(\w+)>", m.group(1)):
                    writes[kind][tag] += 1

    findings = []
    checked = 0
    for kind in sorted(writes):
        for tag, uses in writes[kind].most_common():
            if tag in IGNORED_TAGS:
                continue
            checked += 1
            if f"<{tag}>" in handlers[kind] or (kind, tag) in EXPLAINED:
                continue
            findings.append((kind, tag, uses))

    if findings:
        print(f"{len(findings)} field(s) retail writes that no reader looks for:")
        for kind, tag, uses in sorted(findings, key=lambda f: -f[2]):
            print(f"  {kind}.{tag}: {uses} uses")
        print()
        print("Read it, or add it to EXPLAINED with the reason.")
        return 1

    print(f"every one of the {checked} fields retail writes is read, or explained")
    return 0


if __name__ == "__main__":
    sys.exit(main())
