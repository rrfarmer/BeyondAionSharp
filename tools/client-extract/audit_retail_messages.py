"""Report retail message handlers a ported AI class does not implement.

`audit_ai_messages.py` checks our classes against each other -- a broadcast with
no listener, a listener with no sender. That catches an encounter wired to itself
wrongly, but not one wired to itself correctly while missing half of what retail
does. This is the other axis: for every class that says it was translated from a
retail pattern, what messages does that pattern use that we never touch?

Each finding is tagged with why it is probably absent, because most are:

    cast-only   every action in the retail branch is a use_skill, and this work
                does not translate casts it cannot map to a skill id
    unheard     a broadcast no pattern anywhere listens for -- an announcement to
                the client, harmless to omit
    acts        a handler that spawns, moves or arms a timer, or a broadcast some
                other pattern really does listen for -- a real gap worth reading

Only `acts` findings need triage. Classifying a broadcast by what its own branch
does is wrong and was the tool's first mistake: a shout that sits in a branch which
also spawns still only shouts, and every gateway-guard rung looked like a gap.

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

from audit_missing_adds import NAME_RE, PATTERN_RE, read_text
from summarize_pattern import lowercase_tags

AI_DIR = "src/Aion.GameServer/Handlers/AI"
CONST_RE = re.compile(r"\bconst int (\w+)\s*=\s*(\d{3,7})\b")
USES = (re.compile(r"When\.Message\(([\w.]+)\)"), re.compile(r"Do\.Broadcast\(([\w.]+)"),
        re.compile(r"NpcMessageBus\.Broadcast\([^,]+,\s*([\w.]+)"), re.compile(r"case\s+([\w.]+)\s*:"))


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    ai_files = sorted((repo / AI_DIR).glob("*.cs"))

    consts: dict[str, str] = {}
    for f in ai_files:
        consts.update(CONST_RE.findall(f.read_text(encoding="utf-8", errors="replace")))

    def resolve(token: str) -> str | None:
        token = token.strip()
        return token if token.isdigit() else consts.get(token.rsplit(".", 1)[-1])

    # A class names its retail patterns in its doc comment; that is the only binding we have.
    claimed: dict[str, set[str]] = {}
    for f in ai_files:
        head = f.read_text(encoding="utf-8", errors="replace")[:4000]
        if "Retail-sourced" not in head and "Retail pattern" not in head:
            continue
        pats = set(re.findall(r"<c>([A-Za-z0-9_]{6,})</c>", head))
        if pats:
            claimed[f.stem] = pats

    # Every message any pattern in the corpus listens for. A broadcast outside this set has no
    # listener anywhere in retail, so omitting it drops an announcement and nothing else.
    listened_anywhere: set[str] = set()
    for path in sorted(pathlib.Path(args.patterns_dir).glob("*.xml")):
        for block in PATTERN_RE.finditer(read_text(path)):
            try:
                root = ET.fromstring("<r>" + lowercase_tags(block.group(1)) + "</r>")
            except ET.ParseError:
                continue
            for m in root.iter("is_message"):
                if m.findtext("message_type"):
                    listened_anywhere.add(m.findtext("message_type"))

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
                verdict = "cast-only" if acts and all(a == "use_skill" for a in acts) else "acts"
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
                    sent_verdict = "acts" if msg in listened_anywhere else "unheard"
                    if kind.get(key) != "acts":
                        kind[key] = sent_verdict

    rows: list[tuple[str, str, str, str]] = []
    for cls, pats in sorted(claimed.items()):
        text = (repo / AI_DIR / f"{cls}.cs").read_text(encoding="utf-8", errors="replace")
        ours = {resolve(t) for regex in USES for t in regex.findall(text)}
        ours.discard(None)
        for pat in sorted(pats):
            for (pattern, msg), verdict in kind.items():
                if pattern == pat and msg not in ours:
                    rows.append((verdict, cls, pat, msg))

    for want in ("acts", "cast-only", "unheard"):
        chosen = [r for r in rows if r[0] == want]
        print(f"=== {want} ({len(chosen)}) ===")
        for _, cls, pat, msg in sorted(chosen, key=lambda r: (r[1], r[2], int(r[3]))):
            print(f"  {cls:<32} {pat:<40} {msg}")
        print()


if __name__ == "__main__":
    main()
