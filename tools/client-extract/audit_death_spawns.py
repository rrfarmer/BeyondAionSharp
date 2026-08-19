#!/usr/bin/env python3
"""Retail NPCs that put something on the floor when they die, and whether this port can.

WHY THIS EXISTS, AND WHY ONLY NOW
---------------------------------
A death spawn was until recently **untestable** in this repo: `BossAiHarness.Kill` never reached the
dying NPC's death handling, because `NpcController.OnDie` runs `DoReward()` before raising the AI event
inside a `try` whose `catch` only logs, and the reward path threw on holders the harness lacks. Every
`on_die` branch in the port was unpinnable, so there was no point enumerating the ones that were missing.

That is fixed (see docs/retail-ai-fidelity.md), so the category is worth counting.

WHAT IT COUNTS
--------------
Retail spawn actions under `on_die`, `on_killed_by_user` and `on_killed_by_npc` — 4,983 of them across
1,479 patterns before filtering, which is not a work list. The filters, each one already justified by a
row somebody read by hand:

* **unattackable spawns are dropped.** The client's own flag, not a devname guess. This is what proved
  chief gunner koakoa's five "missing tiers" were the transparent markers that place one real bomb.
* **npcs our spawn tables already place are dropped** — retail spawns plenty of things this port simply
  puts in a spawn file.
* **heralds are dropped.** An npc whose own pattern does nothing but announce something and remove
  itself is retail's way of sending a message, and this port sends the message directly. The four
  Draupnir Cave adjutants each "spawn" `IDDF3_BroadNPC_System` on death; its entire pattern is
  `broadcast_message` and `despawn_self`, and `DraupnirCaveInstance.OnDie` already counts the kills,
  sends the four messages and spawns Commander Bakarma. Counting those as missing adds would have sent
  somebody to reimplement a finished encounter.
* **FX markers are matched on the devname AND the AI name.** Both are needed and neither is enough.
  `IDArena_S7_NoShowNPC2_55_Ae` carries the marker in its devname and runs a pattern that does not;
  Padmarashka's `IDDramata_01_NPC_08` is the exact opposite. Matching one field only lets the other
  through, which is how both of these reached a hand-read.
* **`Test_` patterns are dropped** — `Test_JM_Monster_6` and `Test_GHB_ONControl_NPC` are NCSoft's own
  scratch content, not an encounter this server owes anything to.
* **FX markers on the AI name specifically:** Padmarashka's three elite guards
  each "spawn" `IDDramata_01_NPC_08` on death — a devname with no marker in it at all, so the FX-word
  list missed it. Its `ai_name` is `IDDramata_NoShowNPC_08`. The marker was there the whole time, one
  field across. Retail names the *behaviour* honestly even where it names the npc blandly.
* **owners absent from `npc_templates.xml` are dropped**, and so are owners that no spawn file places:
  an encounter this server never runs owes nothing.

WHAT IT CANNOT DECIDE, AND SAYS SO
----------------------------------
Whether a given class *implements* its death spawn is not decidable by grep, so this does not pretend to.
It splits owners by what their `ai` could possibly do:

* **`generic`** — the owner runs a shared AI (`aggressive`, `summoner`, `guard_reinforcement`, ...). Those
  classes have no per-npc death behaviour and `<summons>` is keyed on health percentage with no death
  trigger at all, so a death spawn here **cannot** be happening. These are missing, with confidence.
* **`bespoke`** — the owner runs its own class, which may or may not do it. These need reading, and the
  count is reported rather than guessed at.

Usage:  python audit_death_spawns.py [--xml DIR] [--bespoke] [--limit N]
"""
import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import NAME_RE, PATTERN_RE, read_text  # noqa: E402
from audit_summon_ids import FX_WORDS, spawned_in_our_data  # noqa: E402
from client_npc_names import npc_names, unattackable_ids  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
DEATH_HANDLERS = ("on_die", "on_killed_by_user", "on_killed_by_npc")
HANDLER_RE = re.compile(r"<(on_\w+)>(.*?)</\1>", re.S)
SPAWN_RE = re.compile(r"<(spawn|spawn_on_target|spawn_on_multi_target)>(.*?)</\1>", re.S)
NAMEID_RE = re.compile(r"<npc_nameid>([^<]+)</npc_nameid>")

# Actions that only tell somebody something, or tidy the teller away. A pattern built from nothing but
# these is a message, not a mechanic.
HERALD_ACTIONS = frozenset((
    "broadcast_message", "say_to_all", "display_system_message", "send_system_msg", "despawn_self",
    "do_nothing", "set_flag_var", "unset_flag_var", "set_world_flag_var", "unset_world_flag_var",
))
ACTION_RE = re.compile(r"<(\w+)>")
COUNT_RE = re.compile(r"<num_to_spawn>(\d+)</num_to_spawn>")

# AI names with no room for a per-npc death spawn: shared classes, and the summon data they read is
# keyed on health percentage with no death trigger in the schema at all.
GENERIC_AI = frozenset((
    "aggressive", "general", "summoner", "guard_reinforcement", "abyssguard_reinforcement",
    "simple_abyssguard", "fortress_protector", "artifact_protector", "siege_shieldnpc",
    "gate_squad", "monster", "peace", "npc", "quest_npc", "chest", "door", "static_object",
))


def herald_ids(xml_dir, dev):
    """Npcs whose own pattern only announces something and goes away.

    Retail spawns a short-lived npc to carry a broadcast; this port broadcasts directly. Detected from
    the spawned npc's *own* pattern rather than from its name, so it does not depend on a naming
    convention the way the FX-word list does.
    """
    by_name = {}
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN_RE.finditer(read_text(f)):
            name = NAME_RE.search(m.group(0))
            if not name:
                continue
            actions = {a for a in ACTION_RE.findall(m.group(0))
                       if a.startswith(("say_", "broadcast_", "display_", "send_", "spawn", "use_skill",
                                        "despawn", "set_", "unset_", "goto_", "attack", "switch_",
                                        "move_", "control_", "add_battle_timer", "do_nothing"))}
            if actions and actions <= HERALD_ACTIONS:
                by_name[name.group(1)] = True
    return {npc_id for devname, npc_id in dev.items() if by_name.get(devname)}


def owner_templates():
    """npc id -> ai name, from our own templates."""
    path = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"
    return dict(re.findall(r'npc_id="(\d+)"[^>]*?\bai="([^"]*)"',
                           path.read_text(encoding="utf-8", errors="replace")))


def patterns_by_npc():
    """npc id -> the pattern it runs."""
    out = {}
    tsv = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in tsv.read_text(encoding="utf-8").splitlines()[1:]:
        parts = line.split("\t")
        if len(parts) > 3 and parts[3]:
            out[parts[0]] = parts[3]
    return out


def death_spawns(xml_dir, dev):
    """pattern name -> {spawned npc id: total placed by its death handlers}."""
    out = collections.defaultdict(collections.Counter)
    for f in sorted(pathlib.Path(xml_dir).glob("NpcAIPatterns*.xml")):
        for m in PATTERN_RE.finditer(read_text(f)):
            block = m.group(0)
            name = NAME_RE.search(block)
            if not name:
                continue
            for handler in HANDLER_RE.finditer(block):
                if handler.group(1) not in DEATH_HANDLERS:
                    continue
                for spawn in SPAWN_RE.finditer(handler.group(2)):
                    body = spawn.group(2)
                    devname = NAMEID_RE.search(body)
                    if not devname:
                        continue
                    npc_id = dev.get(devname.group(1))
                    if not npc_id:
                        continue
                    count = COUNT_RE.search(body)
                    out[name.group(1)][npc_id] += int(count.group(1)) if count else 1
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--xml", default="D:/Aion58ServerTesting/Server/Map/XML")
    ap.add_argument("--bespoke", action="store_true",
                    help="list the owners running their own class, which need reading rather than counting")
    ap.add_argument("--limit", type=int, default=40)
    args = ap.parse_args()

    dev = npc_names(args.xml)
    furniture = unattackable_ids(args.xml)
    placed = spawned_in_our_data()
    heralds = herald_ids(args.xml, dev)
    ai_of = owner_templates()
    runs = patterns_by_npc()
    # The pattern each spawned npc runs, so an FX marker on its behaviour is visible even when
    # its devname carries none. See the Padmarashka note in this module's docstring.
    fx_by_ai = {npc_id for npc_id, pattern in runs.items()
                if any(w.lower() in pattern.lower() for w in FX_WORDS)}
    fx_by_devname = {npc_id for devname, npc_id in dev.items()
                     if any(w.lower() in devname.lower() for w in FX_WORDS)}
    theirs = death_spawns(args.xml, dev)

    # An owner is only interesting if this server actually runs it.
    live_owners = [npc_id for npc_id in runs if npc_id in ai_of and npc_id in placed]

    generic, bespoke = [], []
    for npc_id in live_owners:
        if runs[npc_id].startswith("Test_"):
            continue
        spawns = theirs.get(runs[npc_id])
        if not spawns:
            continue
        owed = {sid: n for sid, n in spawns.items()
                if sid not in furniture and sid not in placed and sid not in heralds and sid not in fx_by_ai
                and sid not in fx_by_devname}
        if not owed:
            continue
        (generic if ai_of[npc_id] in GENERIC_AI else bespoke).append((npc_id, runs[npc_id], owed))

    generic.sort(key=lambda r: -sum(r[2].values()))
    bespoke.sort(key=lambda r: -sum(r[2].values()))

    print(f"{len(live_owners)} npcs this server spawns run a retail pattern")
    print(f"{len(generic) + len(bespoke)} of them have a death spawn our data does not already place\n")
    print(f"  {len(generic):4d} on a shared AI -- CANNOT be doing it, no death trigger exists for them")
    print(f"  {len(bespoke):4d} on their own class -- may or may not; needs reading\n")

    rows = bespoke if args.bespoke else generic
    label = "bespoke" if args.bespoke else "generic"
    print(f"--- {label}, worst first ---")
    for npc_id, pattern, owed in rows[:args.limit]:
        placed_str = " ".join(f"{sid}x{n}" for sid, n in sorted(owed.items()))
        print(f"  npc {npc_id:8s} ai={ai_of[npc_id]:24s} [{pattern[:30]}]  {placed_str}")
    if len(rows) > args.limit:
        print(f"  ... and {len(rows) - args.limit} more (--limit)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
