"""Print one NPC AI pattern as a dense digest.

A boss pattern runs 200-2,000 lines of XML in which most of the volume is
boilerplate: every action repeats the same eleven placement fields, and every
`do_nothing` guard repeats its condition. Reading that raw is slow and easy to
skim past, which is how the Blaze half of Captain Xasta's beat got missed on a
first pass.

This collapses each branch to one line per condition and one per action, keeping
only the fields that carry meaning, so a whole fight fits on a screen. Branches
print in the order they are evaluated -- highest priority first -- because these
are first-match-wins chains and the order is the behaviour.

Two things to read carefully in the output:

- A gate marked `!` mutates when it passes -- `set_flag_var` is a test-and-set, so that
  branch fires once; `unset_flag_var` is its mirror. **A pair of branches holding a set and
  an unset copy of one flag alternates for ever**, which is how retail writes a two-state
  rotation. A branch whose only gate is `is_hp_lower_than` fires on every tick below the
  threshold instead.
- `total_set_to_spawn` on a `spawn_on_multi_target` is the cap, and it is often 1. The op name
  says "everybody"; the field says how many.
- `SKILLI_INDEX_n` is an index into the NPC's server-side skill list, which no
  client file carries; `audit_skill_index_reach.py` says whether ours is long
  enough to resolve it.

CLI:
    python summarize_pattern.py <patterns_dir> <pattern_name> [--raw]
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text

# Fields worth printing per action. Everything else is placement boilerplate that is either
# zero or implied (x/y/z/dir on a MY_POINT spawn, except_specialize, is_aerial_spawn).
KEEP = {
    "skill", "skill_level", "target", "who", "percent", "delay", "string_id", "string",
    "btimer_indicator", "flagvar_indicator", "intvar_indicator", "npc_nameid", "num_to_spawn",
    "spawn_range", "spawn_location_type", "live_time", "spawn_id", "message_type",
    "range_as_meter", "param_obj", "target_obj", "state", "set", "modify", "value",
    "min", "max", "pathname", "emotion", "walk_type", "hatepoints_to_add", "npc_indicator",
    "attack_target_after_spawn", "despawn_at_attack_state", "valid_distance", "sec",
    # A SPAWN_LOCATION_ABSOLUTE placement carries its coordinates here, and an HP-boundary
    # guard its bounds; both are the whole content of the branch they appear in.
    "x", "y", "z", "dir", "larger_than", "less_than",
    # `flee_from` carries its duration in `seconds`, and the summariser dropped it -- so every flee
    # printed as a bare `from=`. The klaw sentinels flee for three seconds when hit and four when cast
    # at, and the black claw tamers for three; none of that was visible until the raw XML was read by
    # hand. `seconds` is the whole content of the action it appears in, exactly like `delay`.
    "seconds", "push_state",
    # `is_race` was read as an argumentless guard for months and treated as unusable because of it.
    # Every one of the 2,879 `is_race` conditions in the 5.8 files carries a `race_type`, and the
    # value is the whole content of the guard: `gchief_light` and `gchief_dark` are what makes a
    # village killer hunt a garrison rather than a player. It was invisible only because this list
    # did not name it. Third time a dropped value has produced a wrong conclusion -- see
    # docs/retail-ai-fidelity.md.
    "race_type", "from",
    # `point_to_add` on add_hate_point and `points_to_add` on switch_target: the weight of a call is
    # the mechanic, not decoration. Read out of the raw XML by hand three times before this.
    "point_to_add", "points_to_add", "percent_to_add",
    # The two fields that decide what a multi-target spawn actually does. `spawn_on_multi_target` reads
    # as "one on everybody" and is almost never that: `total_set_to_spawn` caps it, at one in several
    # fights, and `order_in_attacker_list` says whether the cap takes the top of the hate list or a
    # random slice. Both were dropped here, so every multi-target op had to be re-read out of the raw
    # XML by hand -- four times, and one of those nearly shipped Stormwing's single lightning as a
    # raid-wide wave. Fourth dropped value to produce a wrong conclusion; see docs/retail-ai-fidelity.md.
    "total_set_to_spawn", "order_in_attacker_list",
}

# Condition tags that mutate when they pass. These read as guards and behave as actions, which is the
# whole of how a retail pattern alternates: a branch pair holding a set and an unset copy of one flag
# ping-pongs between two behaviours for ever. Marking only `set_flag_var` made `unset_flag_var` look
# like a passive read, and Stormwing's escalation was ported as a four-wave sequence that stopped
# because of it.
MUTATING_GUARDS = {"set_flag_var", "unset_flag_var", "set_world_flag_var", "increase_intvar",
                   "set_int_var", "add_intvar"}
DROP_VALUES = {"", "0", "FALSE", "OBJI_NONE", "NPCI_NONE"}


TAG_RE = re.compile(r"<(/?)([A-Za-z_]\w*)(\s|>|/>)")


def lowercase_tags(xml: str) -> str:
    """Normalise tag case so the dump parses.

    The shipped data is not well-formed: `<skill_level>0</skill_Level>` appears throughout,
    which every strict parser rejects at the closing tag. Case is not otherwise meaningful
    here -- no two tags differ only by case -- so folding it is a safe repair.
    """
    return TAG_RE.sub(lambda m: f"<{m.group(1)}{m.group(2).lower()}{m.group(3)}", xml)


def fields(node: ET.Element) -> str:
    out = []
    for child in node:
        if child.tag not in KEEP:
            continue
        value = (child.text or "").strip()
        if value in DROP_VALUES and child.tag not in ("percent", "delay", "num_to_spawn"):
            continue
        out.append(f"{child.tag}={value}")
    return " ".join(out)


def is_pure_guard(branch: ET.Element) -> bool:
    """True when the branch does nothing at all -- not merely when it ends with do_nothing."""
    actions = branch.find("actions")
    if actions is None:
        return True
    real = [op for op in actions if op.tag != "do_nothing"]
    return not real


def render(branch: ET.Element, indent: str = "    ") -> list[str]:
    priority = branch.findtext("priority", "?").strip()
    category = branch.findtext("action_category", "?").strip()
    comment = (branch.findtext("comment") or "").strip()
    lines = [f"{indent}[p{priority:>3} {category:<7}] {comment}"]
    for section in ("conditions", "actions"):
        block = branch.find(section)
        if block is None:
            continue
        for op in block:
            mark = "?" if section == "conditions" else ">"
            detail = fields(op)
            # A gate that mutates when it passes reads like a condition and behaves like an action,
            # so call it out. See MUTATING_GUARDS.
            if section == "conditions" and op.tag in MUTATING_GUARDS:
                mark = "!"
            lines.append(f"{indent}  {mark} {op.tag}{(' ' + detail) if detail else ''}")
    return lines


def main() -> None:
    # Branch comments are not all ASCII -- some carry the designer's own Korean -- and Windows
    # consoles default to cp1252, which kills the whole dump partway through the first boss that
    # has one. Replacing the odd character is better than losing the rest of the pattern.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("pattern_name")
    ap.add_argument("--raw", action="store_true", help="print the pattern XML instead of the digest")
    args = ap.parse_args()

    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            m = NAME_RE.search(block.group(1))
            if not m or m.group(1) != args.pattern_name:
                continue

            body = block.group(1)
            if args.raw:
                print(re.sub(r"\n\s*\n", "\n", body))
                return

            print(f"# {args.pattern_name}  ({path.name})")
            root = ET.fromstring(f"<ai_pattern>{lowercase_tags(body)}</ai_pattern>")
            handlers = root.find("event_handlers")
            for event in handlers if handlers is not None else []:
                branches = sorted(event.findall("pattern"),
                                  key=lambda b: -int(b.findtext("priority", "0").strip()))
                # The do_nothing guards repeat verbatim across a dozen events; say so once. Only
                # collapse a branch whose actions are *nothing but* do_nothing: several patterns end a
                # real branch with one, and treating that as a guard hides the spawn in front of it.
                # Jurdin the Cursed's wake-up smoke was invisible here for exactly that reason.
                if branches and all(is_pure_guard(b) for b in branches):
                    print(f"  {event.tag}: do_nothing guard only")
                    continue
                print(f"  {event.tag}:")
                for branch in branches:
                    print("\n".join(render(branch, "    ")))
            return

    raise SystemExit(f"pattern {args.pattern_name!r} not found under {args.patterns_dir}")


if __name__ == "__main__":
    main()
