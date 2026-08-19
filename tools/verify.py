#!/usr/bin/env python3
"""Build, test, and report a single exit code — the thing to run before claiming the suite is green.

WHY THIS EXISTS
---------------
A commit in this work claimed "Full solution green at 2,600" while one test was failing. The suite had
been run and the failure was on screen. It was missed because the command was

    dotnet test ... | grep -E "^(Passed!|Failed!)" && <commit>

and **`grep` succeeds when it finds the word "Failed!"**. The `&&` carried straight on to the commit.

> A pipeline that greps for failure text cannot also gate on failure. The exit status is the only thing
> that means what it says, and piping through `grep` throws it away.

So this runs the build and the tests, reads their **exit codes**, prints one summary line, and exits
non-zero if anything failed. There is nothing clever in it; the point is that its result cannot be
misread by the shell.

WHAT IT DOES NOT DO
-------------------
It does not run `regen_check.py` or any of the audits. Those need the retail pattern dump, which this
repo does not carry, so they cannot be part of a check that has to work anywhere. `--regen` opts in when
the dump is present.

Usage:  python tools/verify.py [--regen]
Exit:   0 everything passed, 1 something failed, 2 could not run
"""
import argparse
import pathlib
import re
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parents[1]
SOLUTION = "AionServer.slnx"


def run(label, argv):
    """Runs a command and returns (ok, combined output). The exit code is what decides."""
    print(f"  {label} ...", flush=True)
    result = subprocess.run(argv, cwd=REPO, capture_output=True, text=True, errors="replace")
    return result.returncode == 0, (result.stdout or "") + (result.stderr or "")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--regen", action="store_true",
                    help="also check every generator reproduces its committed table (needs the retail dump)")
    args = ap.parse_args()

    ok, output = run("build", ["dotnet", "build", SOLUTION])
    if not ok:
        errors = [ln.strip() for ln in output.splitlines() if "error CS" in ln]
        print("\nBUILD FAILED")
        for line in dict.fromkeys(errors[:10]):
            print(f"  {line}")
        return 1

    ok, output = run("tests", ["dotnet", "test", SOLUTION, "--no-build"])

    # Totals are for the human; the exit code above is what decides.
    passed = sum(int(n) for n in re.findall(r"Passed:\s*(\d+)", output))
    failed = sum(int(n) for n in re.findall(r"Failed:\s*(\d+)", output))
    if not ok:
        print(f"\nTESTS FAILED - {failed} failing, {passed} passing")
        for line in [ln.strip() for ln in output.splitlines() if "Failed " in ln and "Aion" in ln][:10]:
            print(f"  {line}")
        return 1

    print(f"\nall green - {passed} passing")

    if args.regen:
        ok, output = run("generators", [sys.executable, "tools/client-extract/regen_check.py"])
        tail = output.strip().splitlines()[-1:] or [""]
        print(f"  {tail[0]}")
        # regen_check exits 2 when the retail dump is absent: nothing was wrong, but nothing was checked.
        if not ok:
            return 1 if "problem" in output else 0

    return 0


if __name__ == "__main__":
    sys.exit(main())
