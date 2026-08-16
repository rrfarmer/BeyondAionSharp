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

# Pattern-table listeners, and the hand-rolled kind. Classes that do not extend PatternAi
# implement INpcMessageListener directly and switch on the message, so a scan that only knows
# `When.Message` reports their senders as unpaired -- which is exactly backwards.
LISTEN_RE = re.compile(r"When\.Message\(([\w.]+)\)")
CASE_RE = re.compile(r"case\s+([\w.]+)\s*:")
# A listener for a single message is written as a comparison, not a switch. Reading only `case`
# reported IDSweepStageAddAI's sender as unpaired against a listener sitting right there, which is
# the false negative this whole audit exists to prevent.
EQ_RE = re.compile(r"(?:messageType\s*==\s*([\w.]+)|([\w.]+)\s*==\s*messageType)")
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

    per_file = {path: dict(CONST_RE.findall(text)) for path, text in texts.items()}
    globals_: dict[str, str] = {}
    for table in per_file.values():
        globals_.update(table)

    by_stem = {p.stem: table for p, table in per_file.items()}

    def resolve(token: str, path: pathlib.Path) -> str | None:
        token = token.strip()
        if token.isdigit():
            return token
        # `KistenianPetAI.CallForMore` means that class's constant, not this file's and not
        # whichever file happened to be read last. Without this, LordLannokAI's own CallForMore
        # (6607) answered for the pet's (10016) and the report paired two unrelated encounters.
        if "." in token:
            owner, name = token.rsplit(".", 1)
            if owner in by_stem and name in by_stem[owner]:
                return by_stem[owner][name]
        name = token.rsplit(".", 1)[-1]
        return per_file[path].get(name) or globals_.get(name)

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
            body = text[match.end():i]
            tokens += [(t, listens) for t in CASE_RE.findall(body)]
            tokens += [(a or b, listens) for a, b in EQ_RE.findall(body)]
        tokens += [(t, sends) for r in SEND_RES for t in r.findall(text)]

        # A file that inspects CurrentMessage matches messages itself, so its bare literals are
        # message numbers it handles -- SuspiciousCoffinAI holds three per coffin in a record.
        if "CurrentMessage" in text:
            tokens += [(t, listens) for t in re.findall(r"(?<![\w.])(\d{4,5})(?![\w.])", text)]

        for token, table in tokens:
            value = resolve(token, path)
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
