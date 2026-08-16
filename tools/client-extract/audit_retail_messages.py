"""Report retail message handlers a ported AI class does not implement.

`audit_ai_messages.py` checks our classes against each other -- a broadcast with
no listener, a listener with no sender. That catches an encounter wired to itself
wrongly, but not one wired to itself correctly while missing half of what retail
does. This is the other axis: for every class that says it was translated from a
retail pattern, what messages does that pattern use that we never touch?

Each finding is tagged with why it is probably absent, because most are:

    cast-only   every action in the retail branch is a use_skill, and this work
                does not translate casts it cannot map to a skill id
    unheard     a broadcast whose every listener in the corpus only casts or does
                nothing -- omitting it drops an announcement and nothing else
    no audience a broadcast whose listeners would act, but every NPC bound to
                those listener patterns is one our world never spawns. Real in
                retail, unreachable here, and for a reason that is neither a
                skill index nor our AI's shape: the audience is missing from our
                spawn data. Needs --binding. See docs/retail-ai-fidelity.md.
    no speaker  the mirror -- a handler worth implementing for a message whose
                every retail sender is an NPC our world never places. Also needs
                --binding.
    diff world  the message number is reused by an unrelated encounter: every
                NPC that would answer it stands on maps this class's NPCs never
                appear on, so the two could not be in range of each other. Not a
                gap, an artifact of numbering -- retail assigns message numbers
                per encounter with no registry, and low numbers collide freely.
                Needs --binding.
    acts        a handler that spawns, moves or arms a timer, or a broadcast some
                other pattern really does listen for -- a real gap worth reading

Only `acts` findings need triage. Two earlier versions got this wrong and both are
worth knowing. Classifying a broadcast by what its *own* branch does is wrong -- a
shout beside a spawn still only shouts. Classifying it by whether a listener merely
*exists* is also wrong: the gateway guards' rung announcements have listeners in
retail, and every one of those listeners only casts, so the shouts really were
announcements after all. What matters is whether some listener *acts*.

CLI:
    python audit_retail_messages.py <patterns_dir> [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text, spawnable_npc_ids
from audit_hp_phases import load_binding
from summarize_pattern import lowercase_tags

AI_DIR = "src/Aion.GameServer/Handlers/AI"
# Two blind spots this scan had, both found when it started reporting phantom gaps against
# correct code -- which is how a check loses its usefulness:
#
#   name collisions   `CallForMore` is declared in KistenianPetAI as 10016 and in LordLannokAI
#                     as 6607. A flat name->value map lets one silently win, so a const is
#                     resolved against its own file first and only then globally.
#   table-held ids    SuspiciousCoffinAI keeps each coffin's three message numbers in a record
#                     rather than in When.Message, so a scan looking only at call sites cannot
#                     see them. A file that reads CurrentMessage is doing its own matching, so
#                     its bare four-to-five digit literals count as messages it handles.
CONST_RE = re.compile(r"\bconst int (\w+)\s*=\s*(\d{3,7})\b")
USES = (re.compile(r"When\.Message\(([\w.]+)\)"), re.compile(r"Do\.Broadcast\(([\w.]+)"),
        re.compile(r"NpcMessageBus\.Broadcast\([^,]+,\s*([\w.]+)"), re.compile(r"case\s+([\w.]+)\s*:"),
        # A listener for a single message is a comparison, in either direction and either sense --
        # `ExedilGhostAI` writes it as an early-return guard. Same widening audit_ai_messages.py
        # needed, and for the same reason: assuming a listener declares itself the way the last one
        # did makes the audit report finished work.
        re.compile(r"messageType\s*[!=]=\s*([\w.]+)"),
        re.compile(r"([\w.]+)\s*[!=]=\s*messageType"))

# A class whose pattern comes from a shared builder keeps its broadcasts there. RagingKraterrAI's
# live in ElementalSummonerPattern, declared in FrostmaneLestinAI.cs, and reading only its own file
# reported the order it sends as missing.
DELEGATE_RE = re.compile(r"\b(\w+)\.\w+\(")
DECLARES_RE = re.compile(r"\b(?:static\s+)?class\s+(\w+)")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("--binding", help="ai_binding.tsv; enables the `no audience` verdict")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    ai_files = sorted((repo / AI_DIR).glob("*.cs"))

    texts = {f.stem: f.read_text(encoding="utf-8", errors="replace") for f in ai_files}
    per_file = {stem: dict(CONST_RE.findall(text)) for stem, text in texts.items()}

    # Where each helper type is declared, so a class that delegates can be read together with it.
    declared: dict[str, str] = {}
    for stem, text in texts.items():
        for name in DECLARES_RE.findall(text):
            declared.setdefault(name, stem)
    globals_: dict[str, str] = {}
    for table in per_file.values():
        globals_.update(table)

    def resolve(token: str, stem: str) -> str | None:
        token = token.strip()
        if token.isdigit():
            return token
        # See audit_ai_messages.py: a qualified token belongs to the class it names.
        if "." in token:
            owner, name = token.rsplit(".", 1)
            if owner in per_file and name in per_file[owner]:
                return per_file[owner][name]
        name = token.rsplit(".", 1)[-1]
        return per_file[stem].get(name) or globals_.get(name)

    # A class names its retail patterns in its doc comment; that is the only binding we have.
    #
    # Only the lines that introduce them count. A doc that explains "10016 is broadcast by
    # DGuard_KistenianPet" mentions a pattern this class does not implement, and taking every <c>
    # token made KistenianAI answerable for its pet's handlers.
    claimed: dict[str, set[str]] = {}
    for f in ai_files:
        head = f.read_text(encoding="utf-8", errors="replace")[:4000]
        pats: set[str] = set()
        for line in head.splitlines():
            if not re.search(r"[Rr]etail pattern", line):
                continue
            pats.update(re.findall(r"<c>([A-Za-z0-9_]{6,})</c>", line))
        if pats:
            claimed[f.stem] = pats

    # Messages some pattern answers with more than a cast. A broadcast outside this set is heard
    # only by branches that cast or do nothing, so dropping it drops an announcement.
    #
    # `answering_patterns` is the same relation kept by name, so a broadcast can be checked against
    # *who* would answer it and not merely whether anyone would. See the `no audience` verdict below.
    answered_by_action: set[str] = set()
    answering_patterns: dict[str, set[str]] = collections.defaultdict(set)
    sending_patterns: dict[str, set[str]] = collections.defaultdict(set)
    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            try:
                root = ET.fromstring("<r>" + lowercase_tags(block.group(1)) + "</r>")
            except ET.ParseError:
                continue
            named = NAME_RE.search(block.group(1))
            for branch in root.iter("pattern"):
                actions = branch.find("actions")
                tags = [a.tag for a in actions] if actions is not None else []
                # Every sender counts, whatever else its branch does. A broadcast beside a cast is
                # still a broadcast, and the question `no speaker` asks is only whether anything in
                # the world could send it. Recorded above the cast-only skip for that reason: with it
                # below, the Seal of Destruction's `_Source` NPCs did not count as senders and the
                # time-over rescue was reported unreachable against an instance that spawns them.
                for m in branch.iter("broadcast_message"):
                    if m.findtext("message_type") and named:
                        sending_patterns[m.findtext("message_type")].add(named.group(1))
                if not tags or all(t in ("use_skill", "do_nothing") for t in tags):
                    continue
                for m in branch.iter("is_message"):
                    if m.findtext("message_type"):
                        answered_by_action.add(m.findtext("message_type"))
                        if named:
                            answering_patterns[m.findtext("message_type")].add(named.group(1))

    kind: dict[tuple[str, str], str] = {}
    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            name = NAME_RE.search(block.group(1))
            if not name:
                continue
            try:
                root = ET.fromstring("<r>" + lowercase_tags(block.group(1)) + "</r>")
            except ET.ParseError:
                continue
            for branch in root.iter("pattern"):
                actions = branch.find("actions")
                acts = [a.tag for a in actions] if actions is not None else []
                verdict = ("cast-only" if acts and all(a in ("use_skill", "do_nothing") for a in acts)
                           else "acts")
                heard = {m.findtext("message_type") for m in branch.iter("is_message")}
                for msg in filter(None, heard):
                    key = (name.group(1), msg)
                    if kind.get(key) != "acts":
                        kind[key] = verdict

                for m in branch.iter("broadcast_message"):
                    msg = m.findtext("message_type")
                    if not msg:
                        continue
                    key = (name.group(1), msg)
                    sent_verdict = "acts" if msg in answered_by_action else "unheard"
                    if kind.get(key) != "acts":
                        kind[key] = sent_verdict

    # A broadcast whose every listener is an NPC our world never places. It is a real mechanic in
    # retail and unreachable here for a reason that is neither a skill index nor our AI's shape:
    # the audience is missing from our spawn data. Found on the Abyssal Reliquary chamber lords,
    # whose death helpers relay to twelve drakan warp guards that no spawn file contains.
    # Which maps our own spawn data puts each npc on. Only spawn files count: an npc placed by an
    # instance handler has no map in the data, and guessing one would be worse than abstaining.
    maps_of: dict[str, set[str]] = collections.defaultdict(set)
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        text = read_text(path)
        for block in re.finditer(r'<spawn_map[^>]*map_id="(\d+)"(.*?)</spawn_map>', text, re.S):
            world = block.group(1)
            for npc in re.findall(r'npc_id="(\d+)"', block.group(2)):
                maps_of[npc].add(world)

    silent: set[str] = set()
    unspoken: set[str] = set()
    elsewhere: set[tuple[str, str]] = set()
    if args.binding:
        binding = load_binding(pathlib.Path(args.binding))
        owners: dict[str, set[str]] = collections.defaultdict(set)
        for npc_id, pattern in binding.items():
            owners[pattern].add(npc_id)
        live = spawnable_npc_ids(repo)

        def all_unspawned(patterns: set[str]) -> bool:
            npcs = {npc for p in patterns for npc in owners.get(p, ())}
            return bool(npcs) and not (npcs & live)

        for msg, patterns in answering_patterns.items():
            if all_unspawned(patterns):
                silent.add(msg)
        # The mirror: a handler we could implement, for a message whose every retail sender is an
        # NPC our world never places. Found on the same encounter -- the Abyssal Reliquary awakened
        # lord answers 6682 by dismissing itself, and the weakened lord that announces it is in no
        # spawn file either.
        for msg, patterns in sending_patterns.items():
            if all_unspawned(patterns):
                unspoken.add(msg)

        def worlds(patterns: set[str]) -> set[str]:
            return {w for p in patterns
                    for npc in owners.get(p, ())
                    for w in maps_of.get(npc, ())}

        # A class's own maps, against the maps of whoever would answer its broadcast.
        for cls_name, cls_pats in claimed.items():
            mine = worlds(cls_pats)
            if not mine:
                continue
            for msg, listeners in answering_patterns.items():
                theirs = worlds(listeners - cls_pats)
                if theirs and not (theirs & mine):
                    elsewhere.add((cls_name, msg))

    rows: list[tuple[str, str, str, str]] = []
    for cls, pats in sorted(claimed.items()):
        text = texts[cls]
        # The class plus whatever it delegates to, wherever that is declared.
        reachable = [(cls, text)]
        for helper in set(DELEGATE_RE.findall(text)):
            owner = declared.get(helper)
            if owner is not None and owner != cls:
                reachable.append((owner, texts[owner]))

        ours = {resolve(t, stem)
                for stem, body in reachable
                for regex in USES
                for t in regex.findall(body)}
        if "CurrentMessage" in text:
            ours |= set(re.findall(r"(?<![\w.])(\d{4,5})(?![\w.])", text))
        ours.discard(None)
        for pat in sorted(pats):
            for (pattern, msg), verdict in kind.items():
                if pattern == pat and msg not in ours:
                    if verdict == "acts" and msg in silent:
                        verdict = "no audience"
                    elif verdict == "acts" and msg in unspoken:
                        verdict = "no speaker"
                    elif verdict == "acts" and (cls, msg) in elsewhere:
                        verdict = "diff world"
                    rows.append((verdict, cls, pat, msg))

    for want in ("acts", "no audience", "no speaker", "diff world", "cast-only", "unheard"):
        chosen = [r for r in rows if r[0] == want]
        print(f"=== {want} ({len(chosen)}) ===")
        for _, cls, pat, msg in sorted(chosen, key=lambda r: (r[1], r[2], int(r[3]))):
            print(f"  {cls:<32} {pat:<40} {msg}")
        print()


if __name__ == "__main__":
    main()
