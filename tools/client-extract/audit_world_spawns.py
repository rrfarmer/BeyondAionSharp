#!/usr/bin/env python3
"""What retail spawns in a world, what gates it, and what this port is missing.

WHY THIS EXISTS
---------------
Drakenspire Depths' whole wave system was ported -- twenty-seven npcs, three AI classes, forty-two pins
-- and then none of it could ever run, because **not one of those npcs has a spawn entry**. The port's
`301390000_Drakenspire_Depths.xml` names fifty-six npcs and none of them is a wave attacker, a wave
leader, a forward guard, the arrow target or the darkness.

> An encounter can be missing in three places and this project had only ever audited two. A missing
> class is visible in the ai attribute; a missing add is visible in the summon data; **a missing spawn
> point is visible in neither**, and an npc that never appears looks exactly like one whose AI is wrong.

Retail's `Worlds/<name>/world.xml` is the answer. Every spawn point in the game is there, with exact
coordinates, and -- crucially -- with the condition that gates it.

WHAT THE CONDITIONS ARE
-----------------------
A `territory` carries a `condition_info_list`, and each `condition_info` holds an `extcondition`: a
boolean expression over named spawn variables, such as

    (PLAY_LEVEL == 3) && (WAVE_LEADER == 5) && (LEVEL_CHECK_3 == 5)

The variables are written by AI patterns through `set_condition_spawn_variable` -- **12,446 of those
across the 5.8 files, over 2,122 distinct names** -- and read only here. That pairing is the whole
instance-progression engine: an npc dies, a counter goes up, and a spawn group that was waiting on the
counter appears.

**This port implements neither half.** `set_condition_spawn_variable` has no vocabulary in the pattern
DSL, and the spawn loader has no notion of a gated spawn group. There are **54,388 gated spawn groups
across 163 worlds**, so this is not a corner of the data.

WHAT THIS TOOL DOES NOT DO
--------------------------
**It does not tell you an npc should spawn unconditionally.** A retail spawn point behind
`WAVE_LEADER == 5` is *meant* to be absent until four other things have happened, and copying it into
the port's spawn file without the gate would put every wave in the room at once -- worse than the
current emptiness, and harder to notice. So the report separates the two:

* **ungated** -- retail spawns these with no condition at all. The port can carry them today.
* **gated** -- these need the progression engine first, and the report names the condition so the size
  of that job is visible rather than guessed.

Usage:  python audit_world_spawns.py <world-name> [--spawns FILE] [--gated] [--limit N]
        python audit_world_spawns.py IDSeal --spawns game-server/data/static_data/spawns/Instances/301390000_Drakenspire_Depths.xml
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text  # noqa: E402
from client_npc_names import npc_names, unattackable_ids  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]

TERRITORY_RE = re.compile(r"<territory\b.*?</territory>", re.S)
NAME_RE = re.compile(r"<name>([^<]*)</name>")
COND_RE = re.compile(r"<condition_info\b.*?</condition_info>", re.S)
EXT_RE = re.compile(r"<extcondition>(.*?)</extcondition>", re.S)
NPC_RE = re.compile(r"<npc\b[^>]*>(.*?)</npc>", re.S)


def unescape(text):
    return (text.replace("&amp;", "&").replace("&lt;", "<")
                .replace("&gt;", ">").replace("&quot;", '"')).strip()


def spawns_in(world_xml):
    """Yields (territory name, condition or None, npc dev name, count) for every retail spawn point."""
    text = read_text(world_xml)
    for territory in TERRITORY_RE.finditer(text):
        block = territory.group(0)
        tname = NAME_RE.search(block)
        tname = tname.group(1) if tname else "?"

        conditions = list(COND_RE.finditer(block))
        if not conditions:
            # A territory with no condition_info spawns unconditionally; its npcs sit directly inside.
            for npc in NPC_RE.finditer(block):
                yield (tname, abyss_gate(npc.group(1)), *npc_of(npc.group(1)),
                       *placements(npc.group(1)))
            continue

        for cond in conditions:
            ext = EXT_RE.search(cond.group(0))
            gate = unescape(ext.group(1)) if ext else None
            for npc in NPC_RE.finditer(cond.group(0)):
                yield (tname, gate or abyss_gate(npc.group(1)), *npc_of(npc.group(1)),
                       *placements(npc.group(1)))


ABYSS_GATE = re.compile(r'<abyss_owner_grade|<abyss_related_race')


def abyss_gate(body):
    """Retail's *other* gate, and the one that nearly turned a tool into a false alarm.

    Reshanta's artifact bosses list five protectors per race in a territory with no extcondition at all,
    and this port spawns one. That read as a missing-spawn defect at scale -- 171 npcs in Reshanta alone,
    the largest row in the sweep -- until the entries themselves were read: each carries
    <abyss_owner_grade start="n" end="n"/>, so the five are the SAME protector at five artifact
    ranks and exactly one is correct at any time. Spawning one was right all along.

    A territory-level extcondition is not the only way retail says "not yet". Anything carrying an
    abyss grade or race is gated on the abyss state, and counting it as ungated over-reports precisely
    the maps with the most of it.
    """
    return "abyss owner grade / race" if ABYSS_GATE.search(body) else None


def npc_of(body):
    name = NAME_RE.search(body)
    count = re.search(r"<count>(\d+)</count>", body)
    return (name.group(1) if name else "?"), int(count.group(1)) if count else 1


def placements(body):
    """Every <pos> in one <npc>, with retail's dir. A spawn point may carry several."""
    out = []
    for pos in re.finditer(r"<pos>(.*?)</pos>", body, re.S):
        got = {axis: re.search(r"<%s>([-\d.]+)</%s>" % (axis, axis), pos.group(1)) for axis in "xyz"}
        if all(got.values()):
            out.append(tuple(float(got[a].group(1)) for a in "xyz"))
    direction = re.search(r"<dir>([-\d.]+)</dir>", body)
    return out, float(direction.group(1)) if direction else 0.0


def ours(spawn_file):
    if not spawn_file or not pathlib.Path(spawn_file).exists():
        return None
    text = pathlib.Path(spawn_file).read_text(encoding="utf-8", errors="replace")
    return set(re.findall(r'npc_id="(\d+)"', text))


def our_ai():
    path = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
    return dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]*)"',
                           path.read_text(encoding="utf-8", errors="replace")))


AGGRESSIVE = ("aggressive", "aggressive_no_loot", "guard", "monster")


def emit(spots, have, world, furniture):
    """Spawn rows for what retail places with no condition and this port does not place at all.

    **Skips anything the client marks unattackable that still carries an aggressive `ai` here.** Retail
    places these as controllers driven entirely by `on_message`; with an aggressive class they would
    acquire and attack players from the moment they appear -- and because the client will not let a
    player target them back, the result is an unkillable thing hitting the raid. Drakenspire Depths'
    eight `BIDSeal_Skill_RapidBreath` npcs are the case: eight LEGENDARY "beritra" stacked in a column
    in the boss room, all on `aggressive_no_loot`. Placing them before their `ai` is settled would be
    strictly worse than leaving them out, so the emitter names them and leaves them.
    """
    ai_of = our_ai()
    held = [n for n in sorted(spots)
            if n not in have and n in furniture and ai_of.get(n, "") in AGGRESSIVE]
    print(f"		<!-- Retail Worlds/{world}/world.xml, ungated territories: placed by nothing else. -->")
    for npc_id in sorted(spots):
        if npc_id in have or npc_id in held:
            continue
        rows = spots[npc_id]
        print(f"		<!-- {rows[0][5]} -->")
        print(f'		<spawn npc_id="{npc_id}">')
        for x, y, z, direction, _, _ in rows:
            # Retail's dir is degrees; this port's h is a 120-step heading. PositionUtil.ConvertAngleToHeading.
            heading = int(direction / 3) % 120
            print(f'			<spot x="{x:.2f}" y="{y:.2f}" z="{z:.2f}" h="{heading}" />')
        print("		</spawn>")

    for npc_id in held:
        print(f"		<!-- HELD BACK {npc_id} {spots[npc_id][0][5]}: unattackable in retail but "
              f'ai="{ai_of.get(npc_id)}" here. -->', file=sys.stderr)
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("world", help="folder name under Worlds/, e.g. IDSeal")
    ap.add_argument("--worlds", default="D:/Aion58ServerTesting/Server/Map/Worlds")
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--spawns", help="this port's spawn file for the same map")
    ap.add_argument("--gated", action="store_true", help="list the gated spawns and their conditions")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--emit", action="store_true",
                    help="print spawn XML for the UNGATED npcs this port is missing, and nothing else")
    args = ap.parse_args()

    world_xml = pathlib.Path(args.worlds) / args.world / "world.xml"
    if not world_xml.exists():
        print(f"no such world: {world_xml}")
        return 2

    ids = npc_names(args.xml)
    have = ours(args.spawns)
    furniture = unattackable_ids(args.xml)

    ungated = collections.Counter()
    gated = collections.defaultdict(collections.Counter)
    unknown = set()

    spots = collections.defaultdict(list)
    for territory, gate, dev, count, points, direction in spawns_in(world_xml):
        npc_id = ids.get(dev)
        if npc_id is None:
            unknown.add(dev)
            continue
        if gate:
            gated[gate][npc_id] += count
        else:
            ungated[npc_id] += count
            for x, y, z in points:
                spots[npc_id].append((x, y, z, direction, territory, dev))

    if args.emit:
        return emit(spots, ours(args.spawns) or set(), args.world, furniture)

    gated_ids = {n for g in gated.values() for n in g}
    print(f"# {args.world}  ({world_xml})")
    print(f"  {sum(ungated.values())} ungated spawn points over {len(ungated)} npcs")
    print(f"  {sum(sum(g.values()) for g in gated.values())} gated spawn points over {len(gated_ids)} "
          f"npcs, behind {len(gated)} distinct conditions")
    if unknown:
        print(f"  {len(unknown)} dev names this dump cannot resolve to an npc id")

    if have is None:
        print("\n  (no --spawns given, so nothing to compare against)")
        return 0

    missing_ungated = sorted(n for n in ungated if n not in have)
    missing_gated = sorted(n for n in gated_ids if n not in have and n not in ungated)
    print(f"\n  this port's spawn file names {len(have)} npcs")
    print(f"  {len(missing_ungated)} of the ungated npcs are absent here -- these need no engine work")
    print(f"  {len(missing_gated)} of the gated npcs are absent here -- these need the progression engine")

    devname = {v: k for k, v in ids.items()}
    if missing_ungated:
        print("\n  ungated and absent:")
        for npc_id in missing_ungated[:args.limit]:
            print(f"    {npc_id}  x{ungated[npc_id]:<4} {devname.get(npc_id, '?')[:52]}")
        if len(missing_ungated) > args.limit:
            print(f"    ... and {len(missing_ungated) - args.limit} more")

    if args.gated:
        print("\n  gated, by condition:")
        for gate, group in sorted(gated.items(), key=lambda kv: -sum(kv[1].values()))[:args.limit]:
            absent = sum(c for n, c in group.items() if n not in have)
            print(f"    [{sum(group.values()):3d} points, {absent} absent here]  {gate[:88]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
