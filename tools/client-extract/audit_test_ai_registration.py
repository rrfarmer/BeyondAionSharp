"""Tests whose spawned adds need an AI name the harness never registers.



An npc whose template names an AI the test did not register does not fail loudly. `AIEngine.NewAI`

throws `No AI found for name X` inside the spawn, the exception is swallowed somewhere in the AI path,

and **the add is simply absent** -- no error, no clue, and a pin that counts it reads zero.



That cost four commits on Padmarashka's acid bomb, whose template names `ai="general"` while

`PadmarashkaRockfallTests` registered three other classes. The mechanic had been correct since the

second attempt.



For every AI handler this resolves the npc ids it spawns, looks up the AI name each id declares, finds

the matching test file, and reports any name the test does not register.



**Limits worth knowing.** It resolves only ids that are `const int` literals in the same file, and it

matches a handler to its test by filename stem -- so an encounter whose ids come from a table, or whose

tests are named differently, is not covered. A clean run is not proof.



Usage:

    python audit_test_ai_registration.py [--repo ..]

"""

from __future__ import annotations



import argparse

import collections

import pathlib

import re





def main() -> int:

    ap = argparse.ArgumentParser()

    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))

    args = ap.parse_args()

    repo = pathlib.Path(args.repo)



    ai: dict[str, str] = {}

    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(

        encoding="utf-8", errors="replace")

    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):

        hit = re.search(r'ai="([^"]*)"', attrs)

        ai[npc_id] = hit.group(1) if hit else ""



    handlers = repo / "src/Aion.GameServer/Handlers/AI"

    name_of: dict[str, str] = {}

    consts: dict[str, dict[str, str]] = collections.defaultdict(dict)

    for path in handlers.glob("*.cs"):

        text = path.read_text(encoding="utf-8", errors="replace")

        for hit in re.finditer(r'\[AIName\("([^"]+)"\)\]\s*public\s+(?:sealed\s+)?class\s+(\w+)', text):

            name_of[hit.group(2)] = hit.group(1)

        for hit in re.finditer(r"const\s+int\s+(\w+)\s*=\s*(\d+)\s*;", text):

            consts[path.name][hit.group(1)] = hit.group(2)



    needs: dict[str, set[str]] = collections.defaultdict(set)

    for path in handlers.glob("*.cs"):

        text = path.read_text(encoding="utf-8", errors="replace")

        for hit in re.finditer(r"Do\.Spawn\w*\(\s*([A-Za-z_]\w*|\d+)", text):

            token = hit.group(1)

            npc_id = token if token.isdigit() else consts[path.name].get(token)

            if npc_id and ai.get(npc_id):

                needs[path.name].add(ai[npc_id])



    rows: list[tuple[str, list[str]]] = []

    tests = list((repo / "tests/Aion.GameServer.Tests/Ai").glob("*.cs"))

    for handler, names in sorted(needs.items()):

        stem = handler[:-3].replace("AI", "")

        for test in tests:

            if not test.stem.startswith(stem):

                continue

            text = test.read_text(encoding="utf-8", errors="replace")

            registered = {name_of.get(c) for c in re.findall(r"typeof\((\w+)\)", text)}

            missing = sorted(n for n in names if n not in registered)

            if missing:

                rows.append((test.name, missing))



    print(f"{len(needs)} handlers spawn adds with a declared AI name")

    print(f"{len(rows)} tests are missing one:\n")
    for name, missing in rows:

        print(f"  {name:<44} {', '.join(missing)}")

    if not rows:

        print("  (none -- but see the limits in this file's docstring)")

    return 0





if __name__ == "__main__":

    raise SystemExit(main())

