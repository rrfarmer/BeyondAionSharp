"""Delete each spawn in an AI class, run that class's pins, and report the ones that stay green.

**Nine pins this session were found to be measuring nothing** — four inert (they passed with the mechanic
deleted) and five flaky (they asserted something random). Every one was found by hand, by someone
happening to run a mutation. That makes nine a lower bound with no idea of the upper.

This does it on purpose. For each spawn call in an AI class it comments the call out, rebuilds, runs only
that class's test filter, and records whether anything went red. **A mutation that survives is a spawn no
pin is really asserting**, whatever the pins may mention by name.

It is slow — a rebuild per mutation, roughly fifteen seconds — so it takes a filter and is meant to be
run over a few classes at a time rather than the whole tree.

**It restores the file after every mutation, including on Ctrl-C.** If it is killed harder than that,
`git status` will show the damage and `git checkout --` will undo it; nothing is written outside the one
file under test.

Usage:
    python mutate_spawns.py YamennesAI StormwingAI
    python mutate_spawns.py --list
"""
from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
AI_DIR = REPO / "src" / "Aion.GameServer" / "Handlers" / "AI"
TEST_DIR = REPO / "tests" / "Aion.GameServer.Tests" / "Ai"
TEST_PROJ = REPO / "tests" / "Aion.GameServer.Tests" / "Aion.GameServer.Tests.csproj"

# Every placement verb, matched at the start of a statement so the whole call can be commented out.
SPAWN_LINE = re.compile(
    r"^(?P<indent>[ \t]*)(?P<body>(?:Do\.)?Spawn"
    r"(?:At|Near|OnTarget|OnAttacker|OnEachTarget|OnKiller|OnSeen|OnPath|Offset|AsMyEnemy|For)?\()")


def neutralise(text: str, index: int) -> str:
    """Return `text` with the spawn statement starting on line `index` made inert.

    **A `Do.Spawn*` call is an argument to `Branch(...)`, so it cannot simply be deleted** — blanking it
    leaves a dangling comma and the file stops compiling, which the first version of this reported as
    "did not compile" for three mutations out of five. It is replaced by another `PatternAction`
    instead: `Do.ArmTimer(31, 1)` arms a timer slot no pattern uses, so it type-checks everywhere a
    spawn does and does nothing observable.

    Hand-written spawns — `Spawn(...)` and `SpawnFor(...)` as statements — are blanked, since there is
    nothing they are an argument to.
    """
    lines = text.split("\n")
    start = lines[index]

    # Walk to the end of the call, counting parens, so a multi-line call is replaced whole. The exact
    # offset matters: the closing line usually reads `...))),` where only the first paren belongs to the
    # spawn and the rest close Branch and Of. An earlier version stripped all the trailing punctuation
    # and put it back after a replacement that carries its own parens, which produced one too many and
    # broke every mutation in the file.
    depth = 0
    end = index
    cut = 0
    seen = False
    while end < len(lines):
        for offset, ch in enumerate(lines[end]):
            if ch == "(":
                depth += 1
                seen = True
            elif ch == ")":
                depth -= 1
                if seen and depth == 0:
                    cut = offset + 1
                    break
        if seen and depth <= 0:
            break
        end += 1

    indent = SPAWN_LINE.match(start).group("indent")
    if "Do.Spawn" in start:
        # Everything after the call's own closing paren is punctuation that has to survive.
        replacement = f"{indent}Do.ArmTimer(31, 1){lines[end][cut:]}"
    else:
        # An empty block rather than a comment: these statements are regularly the whole body of a
        # foreach or an if, and a comment would leave that body missing.
        replacement = f"{indent}{{ }}"

    return "\n".join(lines[:index] + [replacement] + lines[end + 1:])


def test_filter(class_name: str) -> str | None:
    """The xunit filter for a class's pins, or None when it has no test file."""
    stem = class_name.removesuffix("AI")
    for path in TEST_DIR.glob("*.cs"):
        if path.stem.lower().startswith(stem.lower()):
            return path.stem
    return None


def spawn_lines(text: str) -> list[int]:
    return [i for i, line in enumerate(text.split("\n")) if SPAWN_LINE.match(line)]


def run(cmd: list[str]) -> tuple[int, str]:
    done = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
    return done.returncode, done.stdout + done.stderr


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("classes", nargs="*", help="AI class file stems, e.g. YamennesAI")
    ap.add_argument("--list", action="store_true", help="list classes that have a test file")
    args = ap.parse_args()

    if args.list:
        for path in sorted(AI_DIR.glob("*.cs")):
            found = test_filter(path.stem)
            if found and spawn_lines(path.read_text(encoding="utf-8")):
                print(f"{path.stem:<40} -> {found}")
        return 0

    if not args.classes:
        ap.error("name at least one class, or pass --list")

    survivors = 0
    checked = 0
    for stem in args.classes:
        path = AI_DIR / f"{stem}.cs"
        if not path.exists():
            print(f"!! {stem}: no such class")
            continue
        filt = test_filter(stem)
        if filt is None:
            print(f"!! {stem}: no test file, so every spawn in it is unpinned by definition")
            continue

        original = path.read_text(encoding="utf-8")
        targets = spawn_lines(original)
        print(f"\n=== {stem} ({len(targets)} spawns, filter {filt}) ===")

        for index in targets:
            lines = original.split("\n")
            mutated = lines[index]
            path.write_text(neutralise(original, index), encoding="utf-8")
            try:
                code, out = run(["dotnet", "test", str(TEST_PROJ), "--filter",
                                 f"FullyQualifiedName~{filt}", "--nologo", "-v", "q"])
            finally:
                path.write_text(original, encoding="utf-8")

            checked += 1
            label = mutated.strip()[:70]
            if ": error" in out:
                print(f"  [skip ] line {index + 1}: {label}   (did not compile)")
            elif code == 0:
                survivors += 1
                print(f"  [LIVES] line {index + 1}: {label}")
            else:
                print(f"  [dies ] line {index + 1}: {label}")

    print(f"\n{checked} mutations run, {survivors} survived -- each survivor is a spawn no pin asserts")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("\ninterrupted; the file under test was restored before exit", file=sys.stderr)
        raise SystemExit(130)
