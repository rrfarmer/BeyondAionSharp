"""Extract the abyss guards' call-for-help protocol from the retail patterns.

Message `23000` is the largest untranslated mechanic left in the dump by npc count: eighteen patterns
across fifty-six live NPCs broadcast it as they are pulled, and **ninety-four patterns across seven
hundred and seventy live NPCs answer it**. The answer is uniform to a degree nothing else in this
project has been — forty-seven patterns carry the fighting half and forty-seven the idle half, with no
third shape and no variation in the hate value at all:

    on_message 23000, param is an enemy, I am already fighting  -> switch_target OBJI_MESSAGE_PARAM
    on_message 23000, param is an enemy                         -> add_hate_point 1, attack_most_hating

The send half is not uniform, and this records what varies: the range runs 20, 25 or 50 metres, and the
parameter is either the guard's current target or the player that pulled it — the same player at the
moment of the pull, which is the only moment fifteen of the eighteen send it.

Writes a TSV a human can read against the patterns, which `emit_guard_calls_table.py` then transcribes.
The split is deliberate: the extraction is the claim about what retail does, and the emitter is only a
transcription of it.

CLI:
    python extract_guard_calls.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402

CALL = "23000"

# The guard's own event that carries the broadcast. Three patterns send it from a battle timer
# instead, inside cast chains we are not porting; they are counted and skipped rather than folded
# in, because a send on a timer is a different cadence and would need that timer built.
SEND_EVENT = "on_enter_attack_state"


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    live = A.spawnable_npc_ids(args.repo)
    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    name_of = {A.attr(m.group(0), "npc_id"): A.attr(m.group(0), "name")
               for m in re.finditer(r"<npc_template[^>]*>", templates)}

    binders: dict[str, list[str]] = collections.defaultdict(list)
    for line in A.read_text(args.binding_tsv).splitlines():
        fields = line.split("\t")
        if len(fields) > 3:
            binders[fields[3]].append(fields[0])

    rows: list[tuple[str, str, int, int]] = []
    skipped_timer_sends = 0
    for path in sorted(args.patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in S.PATTERN_RE.finditer(text):
            block = m.group(0)
            if CALL not in block:
                continue
            named = S.NAME_RE.search(block)
            if not named:
                continue
            owners = [n for n in binders.get(named.group(1), []) if n in live]
            if not owners:
                continue

            try:
                root = ET.fromstring(f"<ai_pattern>{S.lowercase_tags(block)}</ai_pattern>")
            except ET.ParseError:
                continue
            handlers = root.find(".//event_handlers")
            if handlers is None:
                continue

            send_range = 0
            receives = 0
            for event in handlers:
                for branch in event.findall("pattern"):
                    actions = branch.find("actions")
                    conditions = branch.find("conditions")
                    if actions is None:
                        continue

                    for node in actions:
                        if node.tag != "broadcast_message":
                            continue
                        if (node.findtext("message_type") or "").strip() != CALL:
                            continue
                        if event.tag != SEND_EVENT:
                            skipped_timer_sends += 1
                            continue
                        send_range = max(send_range, int((node.findtext("range_as_meter") or "0").strip()))

                    if conditions is None:
                        continue
                    heard = any(y.tag == "is_message" and (y.findtext("message_type") or "").strip() == CALL
                                for y in conditions)
                    if heard and any(z.tag == "add_hate_point" for z in actions):
                        receives = 1

            if not send_range and not receives:
                continue
            for npc in owners:
                rows.append((npc, named.group(1), send_range, receives))

    rows.sort(key=lambda r: int(r[0]))
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc_id\tpattern\tsend_range\treceives\tname\n")
        for npc, pattern, send_range, receives in rows:
            out.write(f"{npc}\t{pattern}\t{send_range}\t{receives}\t{name_of.get(npc, '')}\n")

    senders = sum(1 for r in rows if r[2])
    listeners = sum(1 for r in rows if r[3])
    print(f"{len(rows)} guards: {senders} send the call, {listeners} answer it; "
          f"{len({r[1] for r in rows})} patterns. "
          f"{skipped_timer_sends} broadcasts skipped for sitting on a battle timer.")


if __name__ == "__main__":
    main()
