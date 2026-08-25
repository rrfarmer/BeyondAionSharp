"""Every role name the extractors can emit, the loader must be able to read.

**This class of bug has been found twice by accident and is invisible until it is expensive.** The
extractors map retail's subject indicators onto condition and action names -- `OBJI_MESSAGE_SENDER` ->
`MessageSenderRace`, `OBJI_ATTACKER` -> `AttackerHpBelow` -- and `PatternTableLoader` turns those names
back into engine calls with a `switch`. Adding a name to a map without adding a case to the switch
costs nothing at all until a pattern using it becomes live, and then it costs everything:
`PatternTableLoader` refuses a token it cannot translate by design, and a refused token takes the
**whole file** down rather than one branch.

So this compares the two sides directly. It is deliberately crude -- the loader is C# and this is
Python, so the check is "does the loader mention this name at all" rather than a parse. A name that
appears in a comment would pass wrongly; that is the price of not writing a C# parser here, and it
still catches the case that actually happens, which is a name nobody wrote down twice.
"""
from __future__ import annotations

import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
LOADER = HERE.parents[1] / "src/Aion.GameServer/Ai/Pattern/PatternTableLoader.cs"

#: The maps whose *values* are names the loader has to answer. Keyed by the module they live in.
MAPS = {
    "extract_battle_cycles": [
        "ROLES", "RACE_ROLES", "ABNORMAL_ROLES", "HP_ROLES", "HP_BAND_ROLES",
        "ENEMY_ROLES", "NEAR_ROLES", "DISTANCE_ROLES", "CLASS_SUBJECTS",
        "USER_ROLES", "NPC_ROLES", "SWITCH_ROLES", "HATE_ROLES", "FLEE_ROLES",
        # Keyed by (handler, role) rather than by role, which the value scan does not care about.
        "FRIEND_HATE_ROLES",
    ],
}


def main() -> int:
    sys.path.insert(0, str(HERE))
    loader = LOADER.read_text(encoding="utf-8")

    missing: list[str] = []
    checked = 0
    for module_name, maps in MAPS.items():
        module = __import__(module_name)
        for map_name in maps:
            table = getattr(module, map_name, None)
            if table is None:
                missing.append(f"{module_name}.{map_name} does not exist")
                continue
            for indicator, name in table.items():
                # Some maps store a token prefix rather than a bare name, e.g. "flee_FleeFromSeen"
                # and "switch_to:TargetKiller". The name is the part the loader answers.
                bare = re.split(r"[:_]", name)[-1] if name.startswith(("flee_", "switch_to:")) else name
                checked += 1
                if bare not in loader:
                    missing.append(
                        f"{map_name}[{indicator}] = {name!r}: PatternTableLoader has no case for it")

    if missing:
        print(f"{len(missing)} name(s) the extractor can emit and the loader cannot read:")
        for line in missing:
            print(f"  {line}")
        return 1

    print(f"every one of the {checked} role names the extractors emit is answered by the loader")
    return 0


if __name__ == "__main__":
    sys.exit(main())
