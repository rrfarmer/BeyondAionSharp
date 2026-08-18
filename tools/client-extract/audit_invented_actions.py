"""What this port does that retail does not.

Every other tool here asks the same question in a different shape: **what does retail have that we do
not?** Missing adds, missing patterns, dead shouts, silent conversations, unreachable skills. Nothing
asks the reverse, and the reverse is where the worst bugs hide, because **an invented behaviour comes
with a pin that agrees with it**. Two were found by hand:

  * the Illusion of Melancholy answered its call with a zero-point hate on the message parameter, where
    retail casts `attack_most_hating`. The remark reasoned that an empty aggro list makes those the same
    thing. It does not, and the pin was named after the invention.
  * the silikor guards answer `6655` and `6656` with a hate point. **Every retail listener on both
    numbers answers with a single `use_skill` and no hate at all.**

Neither is findable by asking what is missing. Both are obvious once you ask the other question.

This compares, for every `on_message` branch we wrote, the *kinds* of action in our branch against the
kinds in the retail branches answering the same message number, restricted to the patterns the class
documents. It reports:

  * **invented** -- a kind we have that no retail answerer on that number has. **Read these first.**
  * **dropped** -- a kind retail has that we do not. Usually a skill, and usually already recorded as
    not translated; listed for contrast rather than alarm.
  * **match** -- same kinds both ways.

It is deliberately coarse. Kinds, not counts or arguments: a branch that adds 100 where retail adds 10
is a different question, and `audit_message_answers.py` is the tool that knows about hate versus switch.
**This one only asks whether we are doing something retail never asked for.**

Usage:
    python audit_invented_actions.py <patterns_dir> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_message_answers as M  # noqa: E402

#: Retail op -> the kind this tool reasons about.
RETAIL_KIND = {
    "add_hate_point": "hate",
    "switch_target": "hate",
    # Picks a NEW target by indicator, which is what our Do.SwitchTarget does -- so it belongs with
    # attack_most_hating rather than with switch_target, which names an object instead.
    "switch_target_by_attacker_indicator": "attack",
    "attack_most_hating": "attack",
    "use_skill": "skill",
    "spawn": "spawn",
    "despawn": "despawn",
    "despawn_self": "despawn",
    "flee_from": "flee",
    "broadcast_message": "broadcast",
    "add_battle_timer": "timer",
    "say": "say",
    "say_to_all": "say",
    "display_system_message": "say",
}

#: Our action -> the same kinds. Ordered longest-first so `HateMessageParam` beats `Hate`.
OUR_KIND = [
    ("HateMessageParam", "hate"),
    ("HateMessageTarget", "hate"),
    ("HateMessageSender", "hate"),
    ("TargetMessageParam", "hate"),
    ("HateAttacker", "hate"),
    ("HateTarget", "hate"),
    ("SwitchTarget", "attack"),
    ("AttackMostHated", "attack"),
    ("BroadcastAbout", "broadcast"),
    ("Broadcast", "broadcast"),
    ("DespawnSelf", "despawn"),
    ("Despawn", "despawn"),
    ("SpawnAt", "spawn"),
    ("Spawn", "spawn"),
    ("Flee", "flee"),
    ("ArmTimer", "timer"),
    ("Say", "say"),
]


def retail_kinds(patterns_dir: pathlib.Path) -> dict[tuple[str, str], set[str]]:
    """(pattern, message number) -> kinds of action its answering branches carry."""
    out: dict[tuple[str, str], set[str]] = collections.defaultdict(set)
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for match in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = match.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if not name:
                continue
            for handler in re.finditer(r"<on_message>(.*?)</on_message>", body, re.S):
                for branch in re.finditer(r"<pattern>(.*?)</pattern>", handler.group(1), re.S):
                    flat = re.sub(r"\s+", "", branch.group(1))
                    listened = re.search(r"<is_message><message_type>(\d+)<", flat)
                    if not listened:
                        continue
                    key = (name.group(1), listened.group(1))
                    for op, kind in RETAIL_KIND.items():
                        if f"<{op}>" in flat or f"<{op}/>" in flat:
                            out[key].add(kind)
    return out


def our_branches(text: str, consts: dict[str, str]):
    """Yield (message number, kinds) for each `When.Message(...)` branch in one AI file."""
    for hit in re.finditer(r"When\.Message\(([A-Za-z0-9_.]+)\)(.*?)\)\)[,;]", text, re.S):
        token, body = hit.group(1), hit.group(2)
        number = consts.get(token) or consts.get(token.split(".")[-1])
        if number is None and token.isdigit():
            number = token
        if number is None:
            continue
        kinds: set[str] = set()
        for action in re.findall(r"Do\.(\w+)", body):
            for prefix, kind in OUR_KIND:
                if action.startswith(prefix):
                    kinds.add(kind)
                    break
        if kinds:
            yield number, kinds


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    args = ap.parse_args()

    handlers = pathlib.Path(args.repo) / "src/Aion.GameServer/Handlers/AI"
    retail = retail_kinds(pathlib.Path(args.patterns_dir))
    consts = M.constants(handlers)

    findings: list[tuple[str, str, str, str]] = []
    checked = 0
    unnamed = 0
    for path in sorted(handlers.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        named = set(M.NAME_RE.findall(text))
        for number, ours in our_branches(text, consts):
            theirs: set[str] = set()
            for pattern in named:
                theirs |= retail.get((pattern, number), set())
            if not theirs:
                unnamed += 1
                continue
            checked += 1
            invented = ours - theirs
            dropped = theirs - ours
            if invented:
                findings.append((path.name, number, "invented", "/".join(sorted(invented))))
            elif dropped:
                findings.append((path.name, number, "dropped", "/".join(sorted(dropped))))

    findings.sort(key=lambda f: (f[2] != "invented", f[0]))
    print(f"{checked} message answers compared against the patterns their class documents")
    print(f"({unnamed} skipped: the class names no retail pattern that answers that number)\n")
    print(f"{'file':<34} {'msg':>7}  {'verdict':<9} kinds")
    for name, number, verdict, kinds in findings:
        print(f"{name:<34} {number:>7}  {verdict:<9} {kinds}")
    counts = collections.Counter(f[2] for f in findings)
    print()
    print(f"  {counts['invented']:3d} invented, {counts['dropped']:3d} dropped, "
          f"{checked - len(findings):3d} matching")
    print()
    print("'invented' is the list that matters: an action retail never asked for, which no audit of")
    print("missing pieces can find and which usually arrives with a pin that agrees with it.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
