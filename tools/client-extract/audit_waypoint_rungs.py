#!/usr/bin/env python3
"""Find npcs whose retail pattern acts on reaching a waypoint, where our class does not.

WHY THIS EXISTS
---------------
Three encounters in a row -- Muragan, Engineer Lahulahu, Brass-Eye Grogget -- turned out to have their
route **already in our own spawn data** while the C# class said the mechanic was blocked on missing route
data. Grogget's class still carried "need snif" for coordinates that were in the pattern all along.

That is three times the same discovery was made by hand. This makes it a query.

WHAT IT DOES
------------
For every npc that has all three of

* an `on_arrived_at_waypoint` handler in its retail pattern that **does something** (not just
  `goto_next_waypoint` or `do_nothing`),
* a `walker_id` on its spawn in our own data, so it actually walks here,
* a non-generic C# AI class,

it reports whether the class listens at all, and then **which waypoint indices** it names against the ones
retail's rungs are guarded on.

**Listening means one of two things, and missing the second was this tool's first bug.** A plain AI
overrides `HandleMoveArrived`; a `PatternAi` subclass declares `OnArrivedAtWaypoint` instead and the base
class raises it. Checking only for the override called `OphidanBridgeCallAI` and `SealedAkaimumAI`
unimplemented when both already had their branches -- and `OphidanBridgeCallAI` already had the exact
`is_last_waypoint` despawn the tool was reporting as missing.

READING THE OUTPUT
------------------
`NO LISTENER` is the strongest row: retail acts at a waypoint, the npc walks, and nothing in our code
hears it. `INDEX GAP` is the subtler one and the reason this tool bothers with numbers: the class listens but the
index retail guards on does not appear in it anywhere, which is how a mechanic ends up hanging off the
wrong corner of a patrol. `GreenfingersAI` was in exactly that state while firing a waypoint early.

**It is a weak check on purpose.** Any small integer literal in the class counts as naming that index, so
a class that mentions 4 for an unrelated reason will not be reported. The first version looked only for
`GetStepIndex() == 4` and flagged all four classes here, including the two written in the commits
immediately before it, because they hold their indices in named constants. False negatives are the right
failure direction for a tool whose output is a reading list.

**The indices are compared as raw route steps.** Retail counts waypoints from one and this port's steps
from zero, which `When.AtWaypoint` converts; the numbers scraped out of C# here are therefore shifted by
one before comparison when they come from a `When.AtWaypoint(n)` call, and taken as-is otherwise.

**A missing `walker_id` is not proof the npc does not walk.** Several are put on a route by another AI at
runtime (`DaliaCharlandsAI` does this for its three helpers, `ReianBomberAI` does it to itself in
`HandleSpawned`), so `--unwalked` lists those separately rather than dropping them.

**`--unwalked` also triages whether a route could be found at all.** Three things have ever identified
one: our own spawn table already carrying the `walker_id`; a client route whose name contains the npc's
**devname**; or a client route whose first point sits on the npc's spawn. Where none holds, the route is
absent from both sides — the npc-to-route binding lives in server-side spawn data this dump does not
contain, and guessing a patrol moves an encounter somewhere it has never been.

**Use the devname, not the pattern name.** Doing this by hand I decided Brigade General Vasharti had no
findable route, because his pattern is `IDYun_Nmd6` and the nearest route is `Path_IDYun_Nmd_7Named_60_Ah`
— a different boss, obviously. It is not: **his npc devname is `IDYun_Nmd_7Named_60_Ah`**, so that route
is exactly his and the pattern name simply does not match the npc name. The tool found this because it
compares the right field, and it reversed a conclusion I had already written down.

Usage:  python audit_waypoint_rungs.py [--xml DIR] [--unwalked] [--limit N]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
PATTERN = re.compile(r"<name>([^<]+)</name>(.*?)(?=<name>|\Z)", re.S)
AINAME = re.compile(r'\[AIName\("([^"]+)"\)\]')
GENERIC = {"general", "aggressive", "aggressive_no_loot", "passive_npc", "dummy", "noaction", ""}

# Rungs made only of these do nothing a C# class would need to see.
INERT = {"goto_next_waypoint", "goto_waypoint", "do_nothing"}
ACTION = re.compile(r"<(spawn|use_skill|say_to_all|shout_to_all|broadcast_message|add_battle_timer|"
                    r"despawn_self|switch_target|attack_most_hating|set_condition_spawn_variable|despawn)>")
INDEX = re.compile(r"<is_waypoint_index>\s*<index>(\d+)</index>")
LAST = re.compile(r"<is_last_waypoint>")

# How a C# class can name a route step: the pattern DSL (one-based, converted here) or a raw comparison.
CS_ATWAYPOINT = re.compile(r"When\.AtWaypoint\((\d+)\)")
CS_ATLAST = re.compile(r"When\.AtLastWaypoint|IsLastStep\(\)|AtRouteEnd")
SMALL_INT = re.compile(r"(?<![\w.])(\d{1,2})(?![\w.])")


def acting_waypoint_patterns(xml_dir):
    """pattern name -> what its on_arrived_at_waypoint rungs actually do."""
    out = {}
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN.finditer(read_text(f)):
            body = m.group(2)
            if "<on_arrived_at_waypoint>" not in body:
                continue
            seg = body.split("<on_arrived_at_waypoint>", 1)[1].split("</on_arrived_at_waypoint>", 1)[0]
            acts = collections.Counter(ACTION.findall(seg))
            if not acts:
                continue
            # Indices are collected per rung, and only from rungs that DO something. A rung whose whole
            # body is goto_next_waypoint is retail telling the npc to keep walking, which our walker does
            # by itself -- counting its index made this tool ask why EngineerLahulahuAI never mentions 11
            # or 15, when there is nothing at 11 or 15 to mention.
            idx, wants_last = set(), False
            for rung in re.finditer(r"<pattern>(.*?)</pattern>", seg, re.S):
                body = rung.group(1)
                if not ACTION.search(body):
                    continue
                idx.update(int(i) for i in INDEX.findall(body))
                wants_last = wants_last or bool(LAST.search(body))
            out[m.group(1)] = (acts, idx, wants_last)
    return out


def npc_rows():
    """npc_id -> (pattern, our ai name)."""
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    pat = {}
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 3 and parts[3]:
            pat[parts[0]] = parts[3]
    text = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai = dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]+)"', text))
    return pat, ai


def walkers():
    """npc_id -> walker_id, from every spawn file."""
    out = {}
    root = REPO / "game-server" / "data" / "static_data" / "spawns"
    spawn_open = re.compile(r'<spawn npc_id="(\d+)"')
    walker = re.compile(r'walker_id="([^"]+)"')
    for f in root.rglob("*.xml"):
        current = None
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            m = spawn_open.search(line)
            if m:
                current = m.group(1)
            w = walker.search(line)
            if w and current:
                out.setdefault(current, w.group(1))
    return out


def handlers():
    """our ai name -> (filename, source text)."""
    out = {}
    for f in (REPO / "src" / "Aion.GameServer" / "Handlers" / "AI").glob("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for name in AINAME.findall(text):
            out[name] = (f.name, text)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--unwalked", action="store_true",
                    help="also list npcs with no walker_id, which another AI may still route at runtime")
    ap.add_argument("--limit", type=int, default=40)
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    args = ap.parse_args()

    acting = acting_waypoint_patterns(args.xml)
    npc2pat, npc2ai = npc_rows()
    walk = walkers()
    src = handlers()

    walked, unwalked = {}, {}
    for npc, ai in npc2ai.items():
        if ai in GENERIC or ai not in src:
            continue
        acts = acting.get(npc2pat.get(npc, ""))
        if not acts:
            continue
        bucket = walked if npc in walk else unwalked
        text = src[ai][1]
        listens = "HandleMoveArrived" in text or "OnArrivedAtWaypoint" in text
        row = bucket.setdefault(ai, [src[ai][0], listens, collections.Counter(), [], set(), False])
        row[2].update(acts[0])
        row[3].append(npc)
        row[4].update(acts[1])
        row[5] = row[5] or acts[2]

    missing, gaps = {}, {}
    for ai, row in walked.items():
        filename, listens, acts, npcs, want, wants_last = row
        if not listens:
            missing[ai] = row
            continue
        text = src[ai][1]
        # Indices reach the code three ways and only one of them is a bare comparison: the DSL's
        # one-based AtWaypoint, a literal ==, or -- the style used by the classes written here -- a named
        # constant or an array of them. Every small integer literal in the class therefore counts as a
        # mention. That is generous, and deliberately so: this check should miss real gaps rather than
        # invent them, and it invented four the first time it ran by not looking at constants.
        named = {int(n) - 1 for n in CS_ATWAYPOINT.findall(text)}
        named.update(int(n) for n in SMALL_INT.findall(text))
        unseen = sorted(i for i in want if i not in named)
        missed_last = wants_last and not CS_ATLAST.search(text)
        if unseen or missed_last:
            gaps[ai] = (row, unseen, missed_last)

    print(f"{len(walked)} named AI classes have npcs that walk here AND a pattern that acts at a waypoint")
    print(f"{len(walked) - len(missing)} of them listen for it")
    print(f"{len(missing)} do not -- the mechanic cannot be running")
    print(f"{len(gaps)} listen but no index retail guards on appears anywhere in the class")
    print()

    for ai, row in sorted(missing.items(), key=lambda kv: -sum(kv[1][2].values()))[:args.limit]:
        summary = " ".join(f"{k}x{v}" for k, v in row[2].most_common(5))
        print(f"  NO LISTENER {row[0]:36s} [{ai}]  {len(row[3])} npc(s)  retail indices {sorted(row[4])}")
        print(f"              {summary}")

    for ai, (row, unseen, missed_last) in sorted(gaps.items())[:args.limit]:
        tail = " and is_last_waypoint" if missed_last else ""
        print(f"  INDEX GAP   {row[0]:36s} [{ai}]  never names {unseen}{tail}")

    if args.unwalked:
        # Triage: an npc with no walker_id can still be given one, but only if its route can be
        # IDENTIFIED. Three things have identified one so far, and nothing else has:
        #   1. our own spawn table already carries the walker_id  (handled above, these are the leftovers)
        #   2. a client route whose name contains the npc's devname
        #   3. a client route whose first point is on the npc's spawn
        # Where none of the three holds the route is simply absent: the npc-to-route binding lives in
        # server-side spawn data that is not in this dump, and guessing a patrol moves an encounter
        # somewhere it has never been. Vasharti and Padmarashka were each worked out by hand before this
        # existed; the answer for both was "not findable", and that is a conclusion worth reaching in a
        # second rather than an hour.
        import math
        sys.path.insert(0, str(pathlib.Path(__file__).parent))
        from extract_client_waypoints import client_routes as _routes
        routes = _routes(args.worlds)
        devname = {}
        for line in (REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv").read_text(
                encoding="utf-8").splitlines()[1:]:
            parts = line.split("	")
            if len(parts) > 1:
                devname[parts[0]] = parts[1]
        spawn_xy = {}
        spawn_open = re.compile(r'<spawn npc_id="(\d+)"')
        spot = re.compile(r'<spot x="([-0-9.]+)" y="([-0-9.]+)"')
        for f in (REPO / "game-server" / "data" / "static_data").rglob("*.xml"):
            current = None
            for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
                m = spawn_open.search(line)
                if m:
                    current = m.group(1)
                s = spot.search(line)
                if s and current:
                    spawn_xy.setdefault(current, (float(s.group(1)), float(s.group(2))))

        def identify(npc):
            dev = devname.get(npc, "")
            for name, copies in routes.items():
                if dev and dev in name:
                    return f"name match: {name}"
            xy = spawn_xy.get(npc)
            if xy:
                for name, copies in routes.items():
                    for pts in copies.values():
                        if math.dist(xy, (float(pts[0][0]), float(pts[0][1]))) < 1.0:
                            return f"spawn match: {name}"
            return None

        silent = {a: r for a, r in unwalked.items() if not r[1]}
        print(f"\n{len(silent)} more have no walker_id on their spawn and do not listen either.")
        print("A route can still be attached at runtime by another AI, so these need checking, not fixing:")
        for ai, row in sorted(silent.items())[:args.limit]:
            found = [identify(npc) for npc in row[3]]
            got = [f for f in found if f]
            verdict = got[0] if got else "NO ROUTE FINDABLE"
            print(f"  {row[0]:36s} [{ai}]  {len(row[3])} npc(s)  {verdict}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
