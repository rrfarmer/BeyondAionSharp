"""Report NPC messages our AI classes send with nobody listening, and listen for with nobody sending.

Retail wires the two halves of an encounter together with `broadcast_message` and
`on_message`. Message numbers are chosen per encounter and have no global registry,
so the two halves must be ported together or the mechanic is silent -- a broadcast
nothing hears and a listener nothing sends to look identical to working code.

Nothing checked that. Every port in this work was verified by asking "does this add
spawn?", which is a question about adds; the illusion gate shipped with a listener
for message 10009 that its chamber lord never broadcast, and the two classes were
committed one after the other.

Exit code is 1 when anything is unpaired, so this can gate a build.

CLI:
    python audit_ai_messages.py [--repo PATH]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

AI_DIR = "src/Aion.GameServer/Handlers/AI"

CONST_RE = re.compile(r"\bconst int (\w+)\s*=\s*(\d{3,7})\b")

# Pattern-table listeners, and the hand-rolled kind. Classes that do not extend PatternAi
# implement INpcMessageListener directly and switch on the message, so a scan that only knows
# `When.Message` reports their senders as unpaired -- which is exactly backwards.
LISTEN_RE = re.compile(r"When\.Message\(([\w.]+)\)")
CASE_RE = re.compile(r"case\s+([\w.]+)\s*:")
ON_MESSAGE_RE = re.compile(r"void OnNpcMessage\([^)]*\)\s*\{", re.S)
SEND_RES = (
    re.compile(r"Do\.Broadcast\(([\w.]+)"),
    re.compile(r"NpcMessageBus\.Broadcast\([^,]+,\s*([\w.]+)"),
)


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    files = sorted((pathlib.Path(args.repo) / AI_DIR).glob("*.cs"))
    texts = {f: f.read_text(encoding="utf-8", errors="replace") for f in files}

    consts: dict[str, str] = {}
    for text in texts.values():
        for name, value in CONST_RE.findall(text):
            consts[name] = value

    def resolve(token: str) -> str | None:
        token = token.strip()
        if token.isdigit():
            return token
        return consts.get(token.rsplit(".", 1)[-1])

    listens: dict[str, set[str]] = collections.defaultdict(set)
    sends: dict[str, set[str]] = collections.defaultdict(set)
    for path, text in texts.items():
        tokens = [(t, listens) for t in LISTEN_RE.findall(text)]
        # Hand-rolled listeners switch on the message, but so does every other switch in the file --
        # OnEndUseSkill cases on skill ids, mercenaries on state numbers. Only the body of
        # OnNpcMessage counts, so it is carved out by brace depth rather than scanned whole.
        for match in ON_MESSAGE_RE.finditer(text):
            depth, i = 1, match.end()
            while i < len(text) and depth:
                if text[i] == "{":
                    depth += 1
                elif text[i] == "}":
                    depth -= 1
                i += 1
            tokens += [(t, listens) for t in CASE_RE.findall(text[match.end():i])]
        tokens += [(t, sends) for r in SEND_RES for t in r.findall(text)]

        for token, table in tokens:
            value = resolve(token)
            if value:
                table[value].add(path.stem)

    unpaired = 0
    for title, missing, present in (
        ("listened for, with no sender in our code", set(listens) - set(sends), listens),
        ("broadcast, with no listener in our code", set(sends) - set(listens), sends),
    ):
        print(f"=== {title} ===")
        for msg in sorted(missing, key=int):
            print(f"  {msg:<8} {', '.join(sorted(present[msg]))}")
            unpaired += 1
        print()

    print("=== paired ===")
    for msg in sorted(set(sends) & set(listens), key=int):
        print(f"  {msg:<8} {','.join(sorted(sends[msg]))} -> {','.join(sorted(listens[msg]))}")

    print(f"\n{unpaired} unpaired message(s).")
    raise SystemExit(1 if unpaired else 0)


if __name__ == "__main__":
    main()
