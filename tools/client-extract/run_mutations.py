#!/usr/bin/env python3
"""Apply mutations to source files one at a time and report which pins catch each.

Every retail-fidelity fix in this project is verified the same way: deliberately break it, and require
a named pin to fail. Until now that was written fresh each time as a throwaway script, and **one of
those omitted its compile check** -- a mutation that did not build was reported as `passes`, and two
mutations were called survivors on that basis before the real error surfaced.

That failure mode is the dangerous direction: it says "your pin is weak" when the truth is "your
mutation never ran", and the natural response is to write more pins for a hole that does not exist. So
the runner lives here now, and the compile check is not optional.

WHAT IT GUARANTEES
------------------
* Every mutation's anchor must appear **exactly once** in its file, or the run stops. A mutation that
  silently matched nothing used to look like a surviving mutant.
* A build failure is reported as `DID NOT COMPILE`, never as a pass or a survival.
* The original file is restored whatever happens, including on exceptions.
* A baseline run with no mutation is done first: **if the pins are already failing, nothing below it
  means anything**, and the run stops.

USAGE
-----
Write a JSON spec and point at it:

    [
      {"file": "src/.../FooAI.cs",
       "name":  "the wave comes twice as often",
       "old":   "public const long RepeatMillis = 25_000L;",
       "new":   "public const long RepeatMillis = 12_000L;"}
    ]

    python run_mutations.py spec.json --filter "FullyQualifiedName~FooAiTests"

Each row prints `caught by: <pins>`, `*** SURVIVED ***`, or `DID NOT COMPILE`. A survivor is not
automatically a defect -- several in this project were the mutation being self-defeating rather than
the pin being weak -- but it always needs an answer before the work is committed.
"""
import argparse
import json
import pathlib
import subprocess
import sys

TEST_PROJECT = "tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj"


def run_tests(test_filter):
    """Returns (compiled, failing pin names)."""
    result = subprocess.run(
        ["dotnet", "test", TEST_PROJECT, "--filter", test_filter, "--nologo", "-v", "q"],
        capture_output=True, text=True)
    output = result.stdout + result.stderr
    if ": error CS" in output:
        return False, []
    failing = sorted({
        line.split("Ai.")[-1].split("(")[0].split(" ")[0]
        for line in output.splitlines() if "[FAIL]" in line
    })
    return True, failing


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("spec", help="JSON list of {file, name, old, new}")
    parser.add_argument("--filter", required=True, help="dotnet test --filter expression")
    args = parser.parse_args()

    mutations = json.loads(pathlib.Path(args.spec).read_text(encoding="utf-8"))

    compiled, failing = run_tests(args.filter)
    if not compiled:
        print("BASELINE DID NOT COMPILE -- nothing below would mean anything")
        return 1
    if failing:
        print(f"BASELINE IS ALREADY FAILING: {', '.join(failing)}")
        print("Fix the pins before mutating; a failing baseline catches every mutation for free.")
        return 1
    print(f"baseline: {len(mutations)} mutations to try, pins green\n")

    survivors = 0
    for mutation in mutations:
        path = pathlib.Path(mutation["file"])
        original = path.read_text(encoding="utf-8")
        found = original.count(mutation["old"])
        if found != 1:
            print(f"{mutation['name']:44s} ANCHOR MATCHED {found} TIMES -- fix the spec")
            return 1

        try:
            path.write_text(original.replace(mutation["old"], mutation["new"], 1), encoding="utf-8")
            compiled, failing = run_tests(args.filter)
        finally:
            path.write_text(original, encoding="utf-8")

        if not compiled:
            verdict = "DID NOT COMPILE"
        elif failing:
            verdict = "caught by: " + ", ".join(failing[:3])
        else:
            verdict = "*** SURVIVED ***"
            survivors += 1
        print(f"{mutation['name']:44s} {verdict}")

    print(f"\n{len(mutations) - survivors}/{len(mutations)} caught")
    return 0


if __name__ == "__main__":
    sys.exit(main())
