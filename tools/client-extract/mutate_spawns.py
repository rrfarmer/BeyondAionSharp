"""Delete each spawn in an AI class, run that class's pins, and report the ones that stay green.

**Nine pins this session were found to be measuring nothing** — four inert (they passed with the mechanic
deleted) and five flaky (they asserted something random). Every one was found by hand, by someone
happening to run a mutation. That makes nine a lower bound with no idea of the upper.

This does it on purpose. For each spawn call in an AI class it comments the call out, rebuilds, runs only
that class's test filter, and records whether anything went red. **A mutation that survives is a spawn no
pin is really asserting**, whatever the pins may mention by name.

`--mode thresholds` widens each `When.HpBelow`/`HpBetween` guard to the whole range instead. That is the
sharper mutation for a band, and the one that matters most: **three of the four inert pins this session
were about a threshold or a window**, not a spawn.

## A survivor is not always a defect

Three reasons a mutation lives, and only the first is worth fixing:

1. **The pin is weak.** Nothing asserts the mechanic, or asserts it from outside the band it is named
   for. Four of Miladi's five spawns and both of Tiamat's death effects were this.
2. **The guard is unreachable.** Miladi's repeating low clock is guarded `HpBelow(30)`, and the timer it
   waits on is armed only inside branches that already require it — so above thirty the branch cannot
   fire whatever the guard says. Retail writes the guard; it is simply not independently observable.
3. **The effect is not modelled.** The Dark Poeta generators' three skill-clock bands differ only in the
   delay they re-arm, and the casts that would make that visible are unresolved skill indices. There is
   nothing for a pin to see.

Writing a pin for the second or third kind means inventing an assertion the port cannot support. **Read
the branch before treating a survivor as a gap.**

It is slow — a rebuild per mutation, roughly fifteen seconds — so it takes a filter and is meant to be
run over a few classes at a time rather than the whole tree.

**It restores the file after every mutation, including on Ctrl-C.** If it is killed harder than that,
`git status` will show the damage and `git checkout --` will undo it; nothing is written outside the one
file under test.

**Do not run anything else against this tree while it works.** It edits sources in place, so a test run
started alongside it compiles whatever mutation happens to be on disk. A full-suite run during one batch
reported five failures in one class and two in another on separate runs, which read exactly like newly
flaky pins and were nothing of the kind. It also rewrites line endings on every file it touches, so
`git status` will list them even though `git diff` shows nothing.

Usage:
    python mutate_spawns.py YamennesAI StormwingAI
    python mutate_spawns.py --mode thresholds ChiefMaidMiladiAI
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


# HP guards. Widening one to the whole range is the sharpest mutation available for a band: a pin that
# does not notice is measuring "something happened", not "it happened in this band". THREE OF THE FOUR
# INERT PINS this session were exactly that -- a health chosen outside the band under test, or a window
# that let a neighbouring branch supply the evidence.
HP_BELOW = re.compile(r"When\.HpBelow\((?P<n>\d+)\)")
HP_BETWEEN = re.compile(r"When\.HpBetween\((?P<lo>\d+),\s*(?P<hi>\d+)\)")


def threshold_sites(text: str) -> list[tuple[int, str]]:
    """(line index, what it says) for every HP guard in the file."""
    out = []
    for i, line in enumerate(text.split("\n")):
        for m in (HP_BELOW.search(line), HP_BETWEEN.search(line)):
            if m:
                out.append((i, m.group(0)))
                break
    return out


def widen(text: str, index: int) -> str:
    """Return `text` with the HP guard on line `index` opened to the whole range."""
    lines = text.split("\n")
    line = lines[index]
    # 101 and not 100: the engine tests `HpPercent < percent`, so widening to 100 still excludes a
    # boss at exactly full health -- and a pin that measures "nothing happens at 100%" would then pass
    # against the mutation and be scored as strong when it is not. Cost an under-report on the Dark
    # Poeta generators before it was noticed.
    line = HP_BELOW.sub("When.HpBelow(101)", line, count=1)
    line = HP_BETWEEN.sub("When.HpBetween(0, 101)", line, count=1)
    lines[index] = line
    return "\n".join(lines)


def test_filter(class_name: str) -> str | None:
    """The xunit filter for a class's pins, or None when it has no test file of its own.

    **Matched on the reference, not the filename.** Three rules were tried and only this one is both
    sound and complete:

    - *prefix* handed `AhserionAI` -- which has no pins -- its neighbour `AhserionTrooperAiTests`,
      reporting five survivors where the truth was "no coverage at all";
    - *exact filename* still handed `CalindiFlamelordAI` a file testing `DarkPoetaCalindiFlamelordAI`,
      whose name merely ends the same way;
    - *exact filename plus a reference* then went wrong the other way, calling `YamennesSpawnGateAI`
      unpinned when `UnstableYamennesAiTests` exercises it thoroughly under a different name.

    So: whichever test files name the class, whatever they are called. **A borrowed filter turns a class
    with no coverage into one with bad coverage; too strict a filter does the reverse**, and both are
    read as fact.
    """
    source = AI_DIR / f"{class_name}.cs"
    # A file's stem is not always a class it declares: `DeathDropBossesAI.cs` holds `DeathDropBossAI`
    # and `TakahanAI`, and looking for `typeof(DeathDropBossesAI)` finds nothing though both are pinned.
    declared = re.findall(r"^(?:public|internal)\s+(?:sealed\s+)?class\s+(\w+)",
                          source.read_text(encoding="utf-8"), re.M) if source.exists() else []
    wanted = {class_name, *declared}

    hits = [path.stem for path in sorted(TEST_DIR.glob("*.cs"))
            if any(f"typeof({name})" in path.read_text(encoding="utf-8") for name in wanted)]
    if not hits:
        return None
    # Several files may exercise one class; the filter is a substring match, so the shared prefix of
    # their names would be wrong. Run them as alternatives instead.
    return "|FullyQualifiedName~".join(hits)


def spawn_lines(text: str) -> list[int]:
    return [i for i, line in enumerate(text.split("\n")) if SPAWN_LINE.match(line)]


def run(cmd: list[str]) -> tuple[int, str]:
    done = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True)
    return done.returncode, done.stdout + done.stderr


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("classes", nargs="*", help="AI class file stems, e.g. YamennesAI")
    ap.add_argument("--list", action="store_true", help="list classes that have a test file")
    ap.add_argument("--mode", choices=("spawns", "thresholds"), default="spawns",
                    help="what to mutate: each spawn call, or each HP guard widened to 0-100")
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
        if args.mode == "spawns":
            targets = [(i, original.split("\n")[i].strip()[:70]) for i in spawn_lines(original)]
        else:
            targets = threshold_sites(original)
        print(f"\n=== {stem} ({len(targets)} {args.mode}, filter {filt}) ===")

        for index, mutated in targets:
            path.write_text(
                neutralise(original, index) if args.mode == "spawns" else widen(original, index),
                encoding="utf-8")
            try:
                code, out = run(["dotnet", "test", str(TEST_PROJ), "--filter",
                                 f"FullyQualifiedName~{filt}", "--nologo", "-v", "q"])
            finally:
                path.write_text(original, encoding="utf-8")

            checked += 1
            label = mutated
            if ": error" in out:
                print(f"  [skip ] line {index + 1}: {label}   (did not compile)")
            elif code == 0:
                survivors += 1
                print(f"  [LIVES] line {index + 1}: {label}")
            else:
                print(f"  [dies ] line {index + 1}: {label}")

    what = "spawn" if args.mode == "spawns" else "HP guard"
    print(f"\n{checked} mutations run, {survivors} survived -- "
          f"each survivor is a {what} no pin asserts")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("\ninterrupted; the file under test was restored before exit", file=sys.stderr)
        raise SystemExit(130)
