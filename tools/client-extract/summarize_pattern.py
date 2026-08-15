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

- `set_flag_var` in a *condition* is a test-and-set, so that branch fires once.
  A branch whose only gate is `is_hp_lower_than` without one fires on every tick
  below the threshold instead.
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
}
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
            # A test-and-set gate reads like a condition but mutates, so call it out.
            if section == "conditions" and op.tag in ("set_flag_var", "set_int_var"):
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
                # The do_nothing guards repeat verbatim across a dozen events; say so once.
                if all(b.find("actions/do_nothing") is not None for b in branches) and branches:
                    print(f"  {event.tag}: do_nothing guard only")
                    continue
                print(f"  {event.tag}:")
                for branch in branches:
                    print("\n".join(render(branch, "    ")))
            return

    raise SystemExit(f"pattern {args.pattern_name!r} not found under {args.patterns_dir}")


if __name__ == "__main__":
    main()
