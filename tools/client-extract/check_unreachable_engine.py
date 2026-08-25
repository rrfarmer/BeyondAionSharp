"""Conditions and actions the engine offers that nothing can ask for.

The third of the three shapes this port keeps hitting, and the one that hides best. `check_loader_names`
catches a name the extractor emits and the loader cannot read. `check_dropped_fields` catches a field
retail writes and no reader looks at. This one catches the opposite: **a capability that is written,
correct, and unreachable** --- no token in `PatternTableLoader` names it and no hand-written class calls
it, so it sits there looking finished.

Every one found by hand was worth thousands of rows:

* `When.FriendsAttackerIsEnemy` --- 4,481 rows of rescue branches that could not fire.
* `Do.HateFriendsAttacker`, `Do.HateFriendsKiller` --- the rescue actions aimed at the wrong creature
  because the right one had no way to be named.

Needs neither the retail dump nor the committed tables, so it is as cheap as `check_loader_names.py`.

**Reachability here means "can be asked for", not "is used".** A capability named only by a token no
retail pattern currently emits still counts as reachable: the wiring is there and a future table row
will find it. What this catches is the wiring being absent.
"""
from __future__ import annotations

import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PATTERN = ROOT / "src/Aion.GameServer/Ai/Pattern"
HANDLERS = ROOT / "src/Aion.GameServer/Handlers/AI"

#: name -> why it is deliberately unreachable.
EXPLAINED = {
    # Exists to be compared against, not called. See its remark and
    # BattleCycleAiTests.ThePreciseIdleStateIsNotJustNotFighting.
    "Idle": "kept as the contrast that pins how NPC_STATE_IDLE differs from not-fighting",

    # Retail's `is_my_curent_target` (its own spelling) is 20 uses across five subjects, and this port
    # has a condition for one of them. Reading the element would take three rows. Left until the
    # family is worth building rather than built for a third of it.
    "MessageParamIsMyTarget": "is_my_curent_target is 20 uses; only 3 name the message parameter",

    # Part of the Abyss turret-switch cluster, which needs an alias source this port does not have.
    # See section E of docs/retail-ai-backlog.md.
    "TeleportTalker": "teleport_target_alias needs the alias mechanism 4.8 has no source for",
}


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    api = (PATTERN / "AiPattern.cs").read_text(encoding="utf-8")
    offered = {m.group(2): m.group(1)
               for m in re.finditer(r"public static (PatternCondition|PatternAction) (\w+)", api)}

    loader = (PATTERN / "PatternTableLoader.cs").read_text(encoding="utf-8")
    handlers = "\n".join(f.read_text(encoding="utf-8", errors="ignore")
                         for f in HANDLERS.rglob("*.cs"))

    unreachable = []
    for name, kind in offered.items():
        if name in EXPLAINED:
            continue
        if re.search(r"\b(When|Do)\.%s\b" % re.escape(name), loader):
            continue
        if re.search(r"\b(When|Do)?\.?%s\s*\(" % re.escape(name), handlers):
            continue
        unreachable.append((kind, name))

    if unreachable:
        print(f"{len(unreachable)} engine capability(ies) nothing can ask for:")
        for kind, name in unreachable:
            print(f"  {kind:<17} {name}")
        print()
        print("Give it a token in PatternTableLoader, call it from a class, or add it to EXPLAINED.")
        return 1

    print(f"every one of the {len(offered)} conditions and actions the engine offers can be asked for"
          f" ({len(EXPLAINED)} explained)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
