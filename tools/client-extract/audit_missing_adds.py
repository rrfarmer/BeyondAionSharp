"""Report retail encounter adds our server never spawns.

For every NPC we can actually put in the world that has a retail AI pattern,
this resolves the <npc_nameid> devnames its pattern's spawn actions reference
into npc_ids (via client data) and reports the ones nothing in our data ever
spawns. Those are encounter mechanics that silently do not exist -- Kaliga's
temple nagolems and Hamerun's second prisoner were both found this way.

Requires a binding table from build_ai_binding.py.

CLI:
    python audit_missing_adds.py <client_root> <patterns_dir> <binding.tsv> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re

import bxml
from aionpak import read_pak

SPAWN_RE = re.compile(
    r"<(spawn|spawn_on_target|spawn_on_multi_target|spawn_on_target_by_attacker_indicator)>"
    r"(.*?)</\1>", re.S)
NAMEID_RE = re.compile(r"<npc_nameid>([^<]*)</npc_nameid>")
PATTERN_RE = re.compile(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", re.S)
NAME_RE = re.compile(r"<name>([^<]+)</name>")
TEMPLATE_RE = re.compile(r'<npc_template npc_id="(\d+)"([^>]*)>')

BLANK_NAME_ID = "350000"  # the "no name" string; marks invisible control/FX NPCs


def read_text(path: pathlib.Path) -> str:
    raw = path.read_bytes()
    if raw[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return raw.decode("utf-16", "replace")
    return raw.decode("utf-8", "replace")


def attr(attrs: str, name: str) -> str:
    m = re.search(rf'{name}="([^"]*)"', attrs)
    return m.group(1) if m else ""


def is_real_combatant(attrs: str) -> bool:
    """A fightable, player-visible add -- not an invisible FX or control object.

    `type=` is optional in npc_templates and absent on plenty of real monsters
    (282124 "ancient temple nagolem" among them), so it cannot be required. A
    localized display name is the reliable signal: invisible control NPCs carry
    name_id 350000 and a blank name, and unused internal duplicates keep their
    raw devname (which always contains an underscore).
    """
    if attr(attrs, "type") not in ("", "MONSTER"):
        return False
    if attr(attrs, "name_id") == BLANK_NAME_ID:
        return False
    name = attr(attrs, "name").strip()
    if not name or "_" in name:
        return False
    return bool(attr(attrs, "rank"))


# Devname markers that identify an effect object rather than a fightable add, drawn from what the
# designers wrote and not from what the template says. Deliberately conservative:
#
#   fobj    -- "field object", used for ground effects
#   noshow  -- the designers' own "no show NPC"
#   _fx     -- as a suffix only
#
# `invisible` is NOT on this list and must not be added. Captain Xasta's summon is called
# `IDYun_Rasta_Sum_Invisible` and is a perfectly visible level-60 siege artilleryman -- one of the
# first real mechanics this audit found. `_dmg` is out too: `BLF3_NM_DMGhostPrSum2_49_Ae` matches
# it by accident, and it means "DM ghost", not damage.
EFFECT_MARKERS = ("fobj", "noshow")
EFFECT_SUFFIXES = ("_fx",)


def is_effect_object(devname: str) -> bool:
    """True when the pattern's own devname says this is scenery, not an add."""
    low = devname.strip().lower()
    return any(m in low for m in EFFECT_MARKERS) or low.endswith(EFFECT_SUFFIXES)


INVISIBLE_SUFFIX = "_invisible"


def is_invisible_twin(devname: str, dev2id: dict[str, str]) -> bool:
    """True when this is the invisible counterpart of an NPC the client also names.

    Tiamat's hazards each spawn one of these a few seconds after appearing --
    `LDF4b_Tiamat_Rage_Tranq` spawns `LDF4b_Tiamat_Rage_Tranq_invisible`, which lives two
    seconds and carries the damage. The twin is scenery, and its devname says so by being the
    carrier's own devname plus a suffix.

    This is the safe form of a test that is *not* safe in general. A bare `invisible` substring
    would discard Captain Xasta's siege artilleryman (`IDYun_Rasta_Sum_Invisible`), which is a
    perfectly visible level-60 NPC -- and so would a bare `_invisible` suffix, since his devname
    ends that way too. What separates them is that the artilleryman's base name,
    `IDYun_Rasta_Sum`, is not an NPC; the twins' bases are.
    """
    low = devname.strip().lower()
    return low.endswith(INVISIBLE_SUFFIX) and low[: -len(INVISIBLE_SUFFIX)] in dev2id


def load_binding(path: pathlib.Path) -> dict[str, list[str]]:
    by_pattern = collections.defaultdict(list)
    for line in path.read_text(encoding="utf-8").splitlines()[1:]:
        npc_id, _dev, _ai, pattern = line.split("\t")[:4]
        by_pattern[pattern].append(npc_id)
    return by_pattern


def client_devname_to_id(client_root: pathlib.Path) -> dict[str, str]:
    out = {}
    for name, data in read_pak(client_root / "Data" / "Npcs" / "Npcs.pak"):
        if name not in ("client_npcs_monster.xml", "client_npcs_npc.xml"):
            continue
        for npc in bxml.decode(data):
            f = {c.tag: (c.text or "") for c in npc}
            if f.get("id") and f.get("name"):
                out[f["name"].lower()] = f["id"]
    return out


def spawnable_npc_ids(repo: pathlib.Path) -> set[str]:
    """Every npc_id our data or code can put into the world.

    The handler sweep is a regex approximation, so a code-driven spawn written
    in an unusual shape can produce a false positive here. Spot-check findings
    with a repo-wide grep before acting on them.
    """
    static = repo / "game-server/data/static_data"
    by_ai = npc_ids_by_ai_name(repo)
    ids: set[str] = set()
    for path in (static / "spawns").rglob("*.xml"):
        ids.update(re.findall(r'npc_id="(\d+)"', read_text(path)))
    # `<summonGroup npcId>` is a spawn; `<ai npcId>` is only the owner of a summon table and says
    # nothing about whether anything places that NPC. Counting the wrapper made Jurdin the Cursed --
    # who exists in no spawn file, no instance handler and no code -- look like a live encounter, and
    # brought his whole pattern's adds into the backlog.
    ids.update(re.findall(r'<summonGroup[^>]*npcId="(\d+)"',
                          read_text(static / "ai/spawn_helpers.xml")))
    for path in (static / "npc_skills").rglob("*.xml"):
        ids.update(re.findall(r'<spawn_npc[^>]*npc_id="(\d+)"', read_text(path)))
    for path in (repo / "src/Aion.GameServer/Handlers").rglob("*.cs"):
        text = read_text(path)
        ids.update(re.findall(SPAWN_CALL + r"\s*(\d{5,6})\b", text))
        ids.update(re.findall(r"\bnpcId\s*[:=]\s*(\d{5,6})\b", text))
        ids.update(spawned_via_constants(text))
        ids.update(spawned_relative_to_self(text, by_ai))
    return ids


AINAME_RE = re.compile(r'\[AIName\("([^"]+)"\)\]')
RELATIVE_SPAWN_RE = re.compile(r"\b\w*Spawn\w*\(\s*GetNpcId\(\)\s*([+-])\s*(\d+)")


def npc_ids_by_ai_name(repo: pathlib.Path) -> dict[str, list[str]]:
    """ai_name -> the npc_ids whose template points at it."""
    tpl = read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml")
    out: dict[str, list[str]] = collections.defaultdict(list)
    for npc_id, ai in re.findall(r'<npc_template npc_id="(\d+)"[^>]*?ai="([^"]+)"', tpl):
        out[ai].append(npc_id)
    return out


def spawned_relative_to_self(text: str, by_ai: dict[str, list[str]]) -> set[str]:
    """Ids reached as `Spawn(GetNpcId() + 1, ...)`, resolved per NPC that uses the class.

    A real and easily-missed idiom: `TiamatSkillHelperAI` spawns its own id plus one, which is
    how every "infinite pain" and "sinking sand" damage twin reaches the world. Nothing at the
    call site names those ids, so they read as never spawned. Resolving it takes the AI name off
    the class and the npc_ids pointing at that name out of npc_templates.
    """
    offsets = [(sign, int(n)) for sign, n in RELATIVE_SPAWN_RE.findall(text)]
    if not offsets:
        return set()

    ids: set[str] = set()
    for ai_name in AINAME_RE.findall(text):
        for owner in by_ai.get(ai_name, ()):
            for sign, n in offsets:
                ids.add(str(int(owner) + (n if sign == "+" else -n)))
    return ids


# Any method whose name *contains* Spawn, not only one that starts with it. `RndSpawnInRange`
# is the helper most AI classes actually use to place an add, and anchoring on \bSpawn missed
# every call to it -- RM-1337's sparks of darkness were reported as never spawned while the
# class had been spawning eight to twelve of them per phase all along.
SPAWN_CALL = r"\b\w*Spawn\w*\("

CONST_RE = re.compile(r"\bconst int (\w+)\s*=\s*(\d{5,6})\b")
CONST_ARRAY_RE = re.compile(r"\bint\[\] (\w+)\s*=\s*(?:new int\[\]\s*)?\{([^}]*)\}")


def spawned_via_constants(text: str) -> set[str]:
    """Ids reached through a named constant, e.g. `Spawn(MagicFlame, ...)`.

    Resolving the name rather than accepting any constant matters: skill ids are the same
    width as npc ids and sit in the same classes, so harvesting every `const int` would
    quietly mark real gaps as covered. Only names actually passed to a spawn count.
    """
    consts = {name: value for name, value in CONST_RE.findall(text)}
    for name, body in CONST_ARRAY_RE.findall(text):
        values = re.findall(r"\b(\d{5,6})\b", body)
        if values:
            consts[name] = values

    ids: set[str] = set()
    for used in set(re.findall(SPAWN_CALL + r"\s*(\w+)", text)) | set(re.findall(r"\bnpcId\s*[:=]\s*(\w+)", text)):
        value = consts.get(used)
        if isinstance(value, list):
            ids.update(value)
        elif value:
            ids.add(value)
            continue
        # Not a constant, so it is a local carrying one -- `int npcId = odd ? Sharp : Root;`, or a
        # loop variable over an id array. Harvest whatever its assignments mention.
        for rhs in re.findall(rf"\b{re.escape(used)}\s*=\s*([^;]+);", text):
            ids.update(re.findall(r"\b(\d{5,6})\b", rhs))
            for name in re.findall(r"\b([A-Z]\w+)\b", rhs):
                value = consts.get(name)
                if isinstance(value, list):
                    ids.update(value)
                elif value:
                    ids.add(value)
    return ids


LOCATION_RE = re.compile(r"<spawn_location_type>([^<]*)</spawn_location_type>")
PATHNAME_RE = re.compile(r"<pathname>([^<]*)</pathname>")

# Adds placed at a named designer waypoint path cannot be positioned: those paths were
# server-side data and appear in neither the client's level files nor our repos. Every other
# placement is self-contained -- at the spawner, at a target, or at coordinates the pattern
# itself carries.
BLOCKED_LOCATION = "SPAWN_LOCATION_WAY_POINT_START"


def pattern_spawn_targets(patterns_dir: pathlib.Path) -> dict[str, dict[str, bool]]:
    """pattern name -> {devname: positionable?}"""
    out: dict[str, dict[str, bool]] = collections.defaultdict(dict)
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            for action in SPAWN_RE.finditer(body):
                loc = LOCATION_RE.search(action.group(2))
                positionable = not (loc and loc.group(1).strip() == BLOCKED_LOCATION)
                for dev in NAMEID_RE.findall(action.group(2)):
                    dev = dev.strip()
                    if not dev:
                        continue
                    # If any spawn of this add is positionable, the add is implementable.
                    was, walked = out[m.group(1)].get(dev, (False, False))
                    pn = PATHNAME_RE.search(action.group(2))
                    out[m.group(1)][dev] = (was or positionable,
                                            walked or bool(pn and pn.group(1).strip()))
    return out


# A pattern bound by more NPCs than this is a generic behaviour shared across unrelated monsters,
# and says nothing about two of them being interchangeable.
NARROW_PATTERN = 8


def sibling_we_already_spawn(add_id: str, name: str, by_pattern: dict[str, list[str]],
                             pattern_of: dict[str, str], spawnable: set[str],
                             templates: dict[str, str]) -> str | None:
    """Another npc_id filling the same role that our data does spawn, if there is one.

    Retail sometimes names one id where our data uses a sibling for the same job: Yamennes's
    spawn gates are 283203/283222/283223 in the pattern and 219567/219579/219580 in our
    instance, and only ours carry the portal AI that makes a gate do anything. Such an add
    reads as missing while the mechanic is fully implemented.

    Deliberately a flag and not an exclusion. Telling the two apart meant checking which id had
    a working AI behind it, which no heuristic here can do.
    """
    binders = by_pattern.get(pattern_of.get(add_id, ""), [])
    if not binders or len(binders) > NARROW_PATTERN:
        return None
    for sib in binders:
        if sib != add_id and sib in spawnable and attr(templates.get(sib, ""), "name") == name:
            return sib
    return None


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    by_pattern = load_binding(pathlib.Path(args.binding_tsv))
    pattern_of = {npc: pat for pat, npcs in by_pattern.items() for npc in npcs}
    dev2id = client_devname_to_id(pathlib.Path(args.client_root))
    spawnable = spawnable_npc_ids(repo)
    spawns = pattern_spawn_targets(pathlib.Path(args.patterns_dir))
    templates = {m.group(1): m.group(2) for m in TEMPLATE_RE.finditer(
        read_text(repo / "game-server/data/static_data/npcs/npc_templates.xml"))}

    print(f"patterns with spawn actions : {len(spawns):,}")
    print(f"npc_ids our data can spawn  : {len(spawnable):,}\n")

    findings = []
    for pattern, devnames in spawns.items():
        owners = [n for n in by_pattern.get(pattern, []) if n in spawnable]
        if not owners:
            continue  # we never spawn anything running this pattern
        missing = []
        for dev, (positionable, walks) in sorted(devnames.items()):
            add_id = dev2id.get(dev.lower())
            if not add_id or add_id in spawnable:
                continue
            attrs = templates.get(add_id)
            if attrs is None:
                continue  # content our server does not have at all
            if is_real_combatant(attrs) and not is_effect_object(dev)                     and not is_invisible_twin(dev, dev2id):
                name = attr(attrs, "name")
                sib = sibling_we_already_spawn(add_id, name, by_pattern, pattern_of, spawnable, templates)
                missing.append((add_id, name, attr(attrs, "level"), positionable, walks, sib))
        if missing:
            findings.append((pattern, owners, missing))

    total = sum(len(f[2]) for f in findings)
    blocked = sum(1 for f in findings for m in f[2] if not m[3])
    # Positionable, but the add exists in order to walk a named path we do not have. Spawning one
    # leaves it standing where it appeared, which for a marching column is worse than leaving it out.
    walkers = sum(1 for f in findings for m in f[2] if m[3] and m[4])
    siblings = len({m[0] for f in findings for m in f[2] if m[5]})
    print(f"Fightable retail adds our server never spawns: {total} "
          f"across {len(findings)} encounters")
    print(f"  fully self-contained                       : {total - blocked - walkers}")
    print(f"  positionable, but walk a server-side path  : {walkers}")
    print(f"  blocked on server-side waypoint paths      : {blocked}")
    print(f"  (of all the above, {siblings} have a sibling npc we already spawn -- spot-check)\n")
    for pattern, owners, missing in sorted(findings, key=lambda f: -len(f[2])):
        print(f"{pattern}  (live npc_ids: {','.join(owners[:4])})")
        for add_id, name, level, positionable, walks, sib in missing:
            flag = "" if positionable else "  [BLOCKED: waypoint-placed]"
            if positionable and walks:
                flag = "  [walks a server-side path]"
            if sib:
                flag += f"  [we spawn {sib} for this role -- check before porting]"
            print(f"    {add_id}  lv{level:<3} {name}{flag}")


if __name__ == "__main__":
    main()
