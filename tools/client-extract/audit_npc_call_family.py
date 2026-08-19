#!/usr/bin/env python3
"""Size retail's npc-to-npc call family (30001/30002/30003) against what this port implements.

`AbyssGuardCallAI` ports message **23000**, the guard's "this one is on me" -- a broadcast naming the
*player* that pulled it, answered with a single hate point. There is a second, separate family that
this port does not implement at all, and it works the other way round:

    30001  an npc broadcasts naming ITSELF; hearers take a MILLION hate points on the sender
    30002  the same, in the opposite direction
    30003  a despawn order; the hearer removes itself

The hate value is the whole difference. 23000's `point_to_add` is 1 -- enough to enter combat and let
the raid's threat decide the rest. 30001/30002 use `points_to_add=1000000`, which is not a nudge but a
command: whoever hears it drops what it is doing and goes for the caller. That is because these are
npc-versus-npc. An artifact guard shouts 30002 and the fortress "killer" comes and kills it; the killer
shouts 30001 on waking and every guard in fifty metres turns on the killer. It is how a fortress
changes hands without a player touching either side.

WHY THIS TOOL EXISTS
--------------------
`AbyssGuardCallAI`'s own remarks estimated 30002 as "sent by fifty-three patterns and answered by four,
of which our data spawns eight npcs", and left it for a later pass. Measured properly the family is
**far larger than that**, and the npc count is out by orders of magnitude -- the artifact protectors
alone are several hundred. A stale estimate in a comment is the kind of thing that gets a mechanic
scheduled as an afternoon's work, so the count is a tool now rather than a number somebody wrote down.

Reports one row per (retail pattern, our AI name), with what that pattern does for each message and how
many of our npcs run it. `ai=(none)` means the npc has no AI attribute at all.

Usage:  python audit_npc_call_family.py [patterns_dir]
"""
import re
import sys
import pathlib
import collections

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from audit_missing_adds import read_text, PATTERN_RE, NAME_RE  # noqa: E402

MESSAGES = ("30001", "30002", "30003")

REPO = pathlib.Path(__file__).resolve().parents[2]
BINDING = REPO / "tools" / "client-extract" / "out" / "ai_binding.tsv"
TEMPLATES = REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml"


def roles_by_pattern(patterns_dir):
    """pattern name -> {message: {'send@N' | 'answer'}}"""
    roles = collections.defaultdict(dict)
    for f in sorted(pathlib.Path(patterns_dir).glob("NpcAIPatterns*.xml")):
        text = read_text(f)
        if not any(m in text for m in MESSAGES):
            continue
        for block in PATTERN_RE.finditer(text):
            body = block.group(1)
            named = NAME_RE.search(body)
            if not named:
                continue
            name = named.group(1).strip()
            for msg in MESSAGES:
                for br in re.finditer(r"<broadcast_message>(.*?)</broadcast_message>", body, re.S):
                    if f"<message_type>{msg}<" in br.group(1):
                        rng = re.search(r"<range_as_meter>(\d+)", br.group(1))
                        roles[name].setdefault(msg, set()).add(
                            "send@%s" % (rng.group(1) if rng else "?"))
                if re.search(rf"<is_message>\s*<message_type>\s*{msg}\s*</message_type>", body):
                    roles[name].setdefault(msg, set()).add("answer")
    return roles


def ai_by_npc():
    text = TEMPLATES.read_text(encoding="utf-8", errors="replace")
    out = {}
    for m in re.finditer(r'<npc_template npc_id="(\d+)"[^>]*>', text):
        found = re.search(r'ai="([^"]*)"', m.group(0))
        out[int(m.group(1))] = found.group(1) if found else ""
    return out


def main():
    patterns_dir = sys.argv[1] if len(sys.argv) > 1 else "D:/Aion58ServerTesting/Server/Map/XML"
    roles = roles_by_pattern(patterns_dir)
    if not roles:
        print("no patterns matched -- suspect the decoding before believing this "
              "(see read_text in audit_missing_adds.py)")
        return 1

    ai = ai_by_npc()
    counts = collections.Counter()
    for line in BINDING.read_text(encoding="utf-8", errors="replace").splitlines():
        fields = line.split("\t")
        if len(fields) < 3 or not fields[0].isdigit():
            continue
        pattern = fields[2].strip()
        if pattern not in roles:
            continue
        key = (pattern, ai.get(int(fields[0]), ""),
               tuple(sorted((k, tuple(sorted(v))) for k, v in roles[pattern].items())))
        counts[key] += 1

    total = sum(counts.values())
    by_ai = collections.Counter()
    for (_, ai_name, _), n in counts.items():
        by_ai[ai_name or "(none)"] += n

    print(f"{len(roles)} retail patterns use 30001/30002/30003; "
          f"{total} of our npcs run one of them\n")
    print("by the AI our npcs are bound to:")
    for name, n in by_ai.most_common():
        print(f"  {n:5d}  {name}")
    print("\nrows, largest first:")
    for (pattern, ai_name, role), n in sorted(counts.items(), key=lambda kv: -kv[1]):
        what = "  ".join(f"{m}:{'/'.join(v)}" for m, v in role)
        print(f"  {n:5d}  ai={ai_name or '(none)':22s} {pattern[:44]:46s} {what}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
