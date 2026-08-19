#!/usr/bin/env python3
"""Find pins that assert an absence across a window long enough for the thing to remove itself.

Three of these were found by hand in as many days, always the same shape:

    harness.Clock.Advance(TimeSpan.FromMinutes(2));
    Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == Something));

If the npc removes itself inside the window it is gone by the time the count runs, so the assertion
holds whether or not it was ever spawned and the pin can never fail.

WHAT THIS TOOL IS WORTH, STATED HONESTLY
----------------------------------------
It reports a *shape*, not a defect, and the shape is common in sound pins. Of the highest-ranked rows
examined when it was written, **none turned out to be hollow**: each caught the mutation it was meant
to catch, usually because the same test also asserted something persistent. The three real ones were
found while working on the classes, not by reading this list. Treat a row as a prompt to check, and
expect most of them to be fine.

Deciding needs two things the tool cannot see: whether the npc self-removes, and what else the test
asserts. Read the AI and read the whole pin.

DO NOT REACH FOR WatchNew AS THE FIX
------------------------------------
`WatchNew` is `Watch(countExisting: false)` -- it ignores anything already alive when the watch opens.
So for "nothing was ever placed here", it is **weaker** than counting survivors, because whatever was
placed during `Spawn` itself is invisible to it. That was tried on `TheSpawnerIsSilentForEightMinutes`
and made the pin stop catching the exact regression it exists to catch: a spawner that places on
spawning. `Watch` (which counts existing) is the one that answers "did any ever exist".

`WatchNew` is right for "how many arrived during this window", which is a different question.

Rows where the absence is already checked through a watch, and rows whose test asserts the same
subject present earlier (a lifetime pin -- it stands, then it is gone), are filtered out.

Usage:  python audit_hollow_absence_pins.py [tests_dir] [--min-seconds=N]
"""
import re
import sys
import pathlib

ABSENCE = re.compile(
    r'Assert\.(?:Equal\(\s*0\s*,|Empty\(|False\()[^;]*?;',
    re.S)
ADVANCE = re.compile(
    r'Clock\.Advance\(\s*TimeSpan\.From(Seconds|Minutes|Milliseconds)\(\s*([0-9_.]+)')
WATCH = re.compile(r'Watch(?:New|Each)?\(')

TO_SECONDS = {"Milliseconds": 0.001, "Seconds": 1.0, "Minutes": 60.0}


def windows_before(body, at):
    """Total seconds advanced on the clock since the start of the enclosing test."""
    total = 0.0
    for m in ADVANCE.finditer(body, 0, at):
        total += float(m.group(2).replace("_", "")) * TO_SECONDS[m.group(1)]
    return total


# What is being counted -- the npc constant or expression inside the assertion.
SUBJECT = re.compile(r'(?:GetNpcId\(\)\s*==\s*|Count\(\s*harness\s*,\s*|Count\(\s*)([A-Za-z_][\w.]*|\d+)')


def asserted_present_earlier(body, at, stmt):
    """True if the same subject was asserted PRESENT earlier in this test.

    That is a lifetime pin -- "it stands, then it is gone" -- and the absence at the end is the
    measurement, not a hollow one. Those are sound and are not worth reporting.
    """
    subj = SUBJECT.search(stmt)
    if not subj:
        return False
    key = subj.group(1)
    for m in re.finditer(r'Assert\.(?:Equal|NotEmpty|Single|True)\([^;]*?;', body[:at], re.S):
        prior = " ".join(m.group(0).split())
        if key in prior and not re.search(r'Assert\.Equal\(\s*0\s*,', prior):
            return True
    return False


def split_tests(text):
    """Yield (name, body, offset) for each [Fact]/[Theory] method in a test file."""
    for m in re.finditer(r'public void (\w+)\([^)]*\)\s*\{', text):
        start = m.end()
        depth, i = 1, start
        while i < len(text) and depth:
            if text[i] == '{':
                depth += 1
            elif text[i] == '}':
                depth -= 1
            i += 1
        yield m.group(1), text[start:i], start


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    root = pathlib.Path(args[0] if args else "tests/Aion.GameServer.Tests/Ai")
    min_s = 5.0
    for a in sys.argv[1:]:
        if a.startswith("--min-seconds"):
            min_s = float(a.split("=", 1)[1])

    rows = []
    for f in sorted(root.rglob("*Tests.cs")):
        text = f.read_text(encoding="utf-8", errors="replace")
        for name, body, off in split_tests(text):
            for m in ABSENCE.finditer(body):
                stmt = " ".join(m.group(0).split())
                # An absence checked through a watch window is exactly the right shape already.
                if WATCH.search(stmt):
                    continue
                secs = windows_before(body, m.start())
                if secs < min_s:
                    continue
                if asserted_present_earlier(body, m.start(), stmt):
                    continue
                line = text.count("\n", 0, off + m.start()) + 1
                rows.append((secs, f, line, name, stmt))

    rows.sort(key=lambda r: -r[0])
    print(f"{len(rows)} absence assertions made after a clock advance of {min_s}s or more\n")
    print("A long window is what makes an absence hollow: anything that removes itself inside the")
    print("window is gone by the time the count runs, so the pin holds whether or not it ever existed.")
    print("Read the npc's AI before deciding -- a row is a prompt, not a verdict.\n")
    for secs, f, line, name, stmt in rows:
        rel = f.as_posix().split("tests/")[-1]
        print(f"[{secs:7.1f}s] {rel}:{line}")
        print(f"           {name}")
        print(f"           {stmt[:150]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
