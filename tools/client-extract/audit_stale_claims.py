#!/usr/bin/env python3
"""Remarks that say we lack something, checked against what the repo now has.

WHY THIS EXISTS
---------------
Researcher Teselik's class carried this, in a `<remarks>` block, for months:

> "the four bonus hands (284457) the death tail places on named server paths — those are spawned by
> nothing anywhere and remain missing" ... "four named server paths ... **which we do not have**"

All four paths were in `npc_walker/retail_pattern_paths.xml` under retail's own names. They had been
added by later route-extraction work, and nothing re-checked the remark that predated it.

> **A stale claim reads as a decision somebody already made.** Nobody re-opens "we do not have X" — they
> route around it, and the routing-around outlives the reason. Teselik's was a hardcoded three-metre
> offset standing in for four walk-in paths.

There are 38 absence claims across the AI classes. Each was true when written and none is re-checked.

WHAT IT CHECKS
--------------
The one shape that is fully decidable: **a class that says it has no route, whose npc's retail pattern
names a route this repo now contains.** Resolving it needs no judgement — the pattern names the path, the
walker file either has that id or does not.

Everything else it can only surface. `--all` lists every absence claim with the file and line so they can
be worked through by hand, because "we have no vocabulary for `is_user_flying`" is not checkable by any
amount of grep.

`--messages` settles the other decidable shape: a class saying "nothing in our tree sends N" or
"nothing listens for N". Whether a message can be sent or heard is a fact about which retail patterns
our own npcs run, and that is a lookup rather than a judgement.

Usage:  python audit_stale_claims.py [--xml DIR] [--all] [--messages]
"""
import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import NAME_RE, PATTERN_RE, read_text  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]

# Phrases that assert an absence. Deliberately narrow: "missing" alone matches half the codebase's
# prose, and a claim like "the shout is not translated" is a decision rather than a gap.
ABSENCE = re.compile(
    r"(we do not have|which we do not|we have no|have no route|no route for|"
    r"not in our data|nothing in our|spawned by nothing|binding to nothing|"
    r"we lack|do not carry|is absent from our)", re.I)

ROUTEISH = re.compile(r"(route|path)", re.I)

# A remark that quotes its own former claim in the act of correcting it is not a claim. Fixed classes
# read "this used to say ... which we do not have", and without this the audit reports them for ever --
# which would train the next person to ignore it.
#
# Matched over a WINDOW of lines, not one line. A correction rarely fits on the line it corrects: the
# gravity tornado's rewrite put "This used to read" two lines above the quoted claim, so a line-local
# test still reported it. The window is the remark, which is the unit a human reads anyway.
CORRECTED = re.compile(r"(used to (say|read|end)|no longer|stopped being true|was true when)", re.I)
AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')
PATHNAME_RE = re.compile(r"<pathname>([^<]+)</pathname>")


def our_route_ids():
    """Every walker route id this repo defines."""
    out = set()
    root = REPO / "game-server" / "data" / "static_data" / "npc_walker"
    for f in root.rglob("*.xml"):
        out.update(re.findall(r'route_id="([^"]+)"', f.read_text(encoding="utf-8", errors="replace")))
    return out


def npcs_by_ainame():
    """ai name -> npc ids carrying it."""
    path = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
    out = {}
    for npc_id, ai in re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]*)"',
                                 path.read_text(encoding="utf-8", errors="replace")):
        out.setdefault(ai, []).append(npc_id)
    return out


def patterns_by_npc():
    out = {}
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 3 and parts[3]:
            out[parts[0]] = parts[3]
    return out


def paths_in_patterns(xml_dir):
    """pattern name -> the route names its spawn actions use."""
    out = {}
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN_RE.finditer(read_text(f)):
            name = NAME_RE.search(m.group(0))
            if not name:
                continue
            found = set(PATHNAME_RE.findall(m.group(0)))
            if found:
                out[name.group(1)] = found
    return out


def claims():
    """(file, line number, text) for every absence claim in a comment."""
    out = []
    for f in sorted((REPO / "src").rglob("*.cs")):
        if "/obj/" in f.as_posix() or "/bin/" in f.as_posix():
            continue
        lines = f.read_text(encoding="utf-8", errors="replace").splitlines()
        for i, line in enumerate(lines, 1):
            stripped = line.strip()
            if not (stripped.startswith("///") or stripped.startswith("//")):
                continue
            if not ABSENCE.search(stripped):
                continue
            # Six lines either side: enough to hold the correcting sentence, short enough that an
            # unrelated remark further down the file cannot silence a real claim.
            window = " ".join(lines[max(0, i - 7):i + 6])
            if CORRECTED.search(window):
                continue
            out.append((f, i, stripped.lstrip("/ ").strip()))
    return out


MESSAGE_CLAIM = re.compile(r"(?:sends|listens for|answers|listener for)\D{0,20}?(\d{3,5})", re.I)
MSG_SEND = re.compile(r"<broadcast_message>.*?<message_type>(\d+)</message_type>", re.S)
MSG_HEAR = re.compile(r"<is_message>.*?<message_type>(\d+)</message_type>", re.S)


def message_reach(xml_dir, runs, ai_of):
    """(message -> patterns that send it, message -> patterns that hear it), restricted to OUR npcs.

    A claim like "nothing in our tree sends 6835" is true exactly when no npc this server carries runs a
    retail pattern that broadcasts it. That is decidable, and it changes as the port grows: Orissan's
    death notice was such a claim until his class gained the broadcast.
    """
    ours = {pattern for npc_id, pattern in runs.items() if npc_id in ai_of}
    sends, hears = {}, {}
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN_RE.finditer(read_text(f)):
            body = m.group(0)
            name = NAME_RE.search(body)
            if not name or name.group(1) not in ours:
                continue
            for msg in set(MSG_SEND.findall(body)):
                sends.setdefault(msg, set()).add(name.group(1))
            for msg in set(MSG_HEAR.findall(body)):
                hears.setdefault(msg, set()).add(name.group(1))
    return sends, hears


def report_messages(found, xml_dir, runs, ai_of):
    sends, hears = message_reach(xml_dir, runs, ai_of)
    rows = [(f, line, text, m) for f, line, text in found for m in set(MESSAGE_CLAIM.findall(text))]
    print(f"{len(rows)} absence claims name a message number\n")
    for f, line, text, msg in rows:
        can_send = sorted(sends.get(msg, ()))[:2]
        can_hear = sorted(hears.get(msg, ()))[:2]
        verdict = "STILL TRUE" if not can_send and not can_hear else "CHECK"
        print(f"  {f.relative_to(REPO).as_posix()}:{line}  message {msg}  {verdict}")
        print(f"      claim: {text[:110]}")
        if can_send:
            print(f"      our npcs run patterns that SEND it: {' '.join(can_send)}")
        if can_hear:
            print(f"      our npcs run patterns that HEAR it: {' '.join(can_hear)}")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--all", action="store_true", help="list every absence claim, not only the decidable ones")
    ap.add_argument("--messages", action="store_true",
                    help="settle claims about message numbers against the patterns our npcs run")
    args = ap.parse_args()

    found = claims()
    print(f"{len(found)} absence claims in comments across the AI classes")

    if args.all:
        for f, line, text in found:
            print(f"  {f.relative_to(REPO).as_posix()}:{line}")
            print(f"      {text[:150]}")
        return 0

    routes = our_route_ids()
    by_ai = npcs_by_ainame()
    runs = patterns_by_npc()

    if args.messages:
        templates = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml")
        ours = set(re.findall(r'npc_id="(\d+)"', templates.read_text(encoding="utf-8", errors="replace")))
        return report_messages(found, args.xml, runs, ours)

    pattern_paths = paths_in_patterns(args.xml)

    # Only the route-shaped claims can be settled without judgement.
    routeish = [(f, line, text) for f, line, text in found if ROUTEISH.search(text)]
    print(f"{len(routeish)} of them mention a route or a path\n")

    stale = []
    for f, line, text in routeish:
        source = f.read_text(encoding="utf-8", errors="replace")
        for ai_name in AINAME_RE.findall(source):
            for npc_id in by_ai.get(ai_name, []):
                pattern = runs.get(npc_id)
                if not pattern:
                    continue
                have = sorted(p for p in pattern_paths.get(pattern, ()) if p in routes)
                if have:
                    stale.append((f, line, text, ai_name, npc_id, pattern, have))
                    break

    if not stale:
        print("no class claiming to lack a route runs a pattern naming one this repo has")
        return 0

    print(f"{len(stale)} class(es) say they have no route, and name a pattern whose route WE HAVE:\n")
    seen = set()
    for f, line, text, ai_name, npc_id, pattern, have in stale:
        key = (f, ai_name)
        if key in seen:
            continue
        seen.add(key)
        print(f"  {f.relative_to(REPO).as_posix()}:{line}  [{ai_name}] npc {npc_id} runs {pattern}")
        print(f"      claim:  {text[:130]}")
        print(f"      we have: {' '.join(have)}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
