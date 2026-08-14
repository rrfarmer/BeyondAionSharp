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
    ids: set[str] = set()
    for path in (static / "spawns").rglob("*.xml"):
        ids.update(re.findall(r'npc_id="(\d+)"', read_text(path)))
    ids.update(re.findall(r'npcId="(\d+)"', read_text(static / "ai/spawn_helpers.xml")))
    for path in (static / "npc_skills").rglob("*.xml"):
        ids.update(re.findall(r'<spawn_npc[^>]*npc_id="(\d+)"', read_text(path)))
    for path in (repo / "src/Aion.GameServer/Handlers").rglob("*.cs"):
        text = read_text(path)
        ids.update(re.findall(r"\bSpawn\w*\(\s*(\d{5,6})\b", text))
        ids.update(re.findall(r"\bnpcId\s*[:=]\s*(\d{5,6})\b", text))
    return ids


def pattern_spawn_targets(patterns_dir: pathlib.Path) -> dict[str, set[str]]:
    out = collections.defaultdict(set)
    for path in sorted(patterns_dir.glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            body = block.group(1)
            m = NAME_RE.search(body)
            if not m:
                continue
            for action in SPAWN_RE.finditer(body):
                for dev in NAMEID_RE.findall(action.group(2)):
                    if dev.strip():
                        out[m.group(1)].add(dev.strip())
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    by_pattern = load_binding(pathlib.Path(args.binding_tsv))
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
        for dev in sorted(devnames):
            add_id = dev2id.get(dev.lower())
            if not add_id or add_id in spawnable:
                continue
            attrs = templates.get(add_id)
            if attrs is None:
                continue  # content our server does not have at all
            if is_real_combatant(attrs):
                missing.append((add_id, attr(attrs, "name"), attr(attrs, "level")))
        if missing:
            findings.append((pattern, owners, missing))

    total = sum(len(f[2]) for f in findings)
    print(f"Fightable retail adds our server never spawns: {total} "
          f"across {len(findings)} encounters\n")
    for pattern, owners, missing in sorted(findings, key=lambda f: -len(f[2])):
        print(f"{pattern}  (live npc_ids: {','.join(owners[:4])})")
        for add_id, name, level in missing:
            print(f"    {add_id}  lv{level:<3} {name}")


if __name__ == "__main__":
    main()
