"""Every handler we wrote, against the retail handler it came from.

`audit_invented_actions.py` asks the right question — **what do we do that retail does not** — and can
only ask it about `on_message`, because a message number is the one key it can match on both sides. That
left every other handler in the port unchecked, which is the large majority of what has been written.

**There is a better key, and it does not depend on documentation.** An AI class declares `[AIName]`;
`npc_templates.xml` says which npcs carry that name; `ai_binding.tsv` says which retail pattern each of
those npcs runs. So the patterns a class *actually serves* are derivable, and they are derivable for every
handler, not just the ones with numbers in them.

Using the binding rather than the `<c>Pattern_Name</c>` mentions in a file's remarks also fixes a quieter
problem: a file's remarks name the patterns somebody chose to write down, which is not the same set, and
is exactly the set that goes stale.

For each AI class and each handler, this compares the *kinds* of action our branches carry against the
kinds in the same handler of the retail patterns that class serves:

  * **invented** -- a kind we have that no served pattern has in that handler. **Read these first.**
  * **dropped**  -- a kind they have that we do not. Mostly skills; listed for scale, not alarm.

Kinds, not counts or arguments -- see `audit_invented_actions.py` for why that line is drawn there.

**Two caveats worth knowing before believing a row.** A class may serve many patterns, and the union over
them is permissive: if any served pattern spawns in `on_die`, our spawning in `on_die` is not flagged.
And a handler our engine folds together -- `HandleBackHome` runs both `OnLeaveAttack` and `OnEnterIdle` --
is compared against both retail handlers for the same reason.

Usage:
    python audit_handler_actions.py <patterns_dir> <binding_tsv> [--repo ..] [--verdict invented]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
from audit_invented_actions import OUR_KIND, RETAIL_KIND  # noqa: E402

#: Our handler -> the retail handlers it stands for. Several are one-to-many by design.
HANDLERS = {
    "OnWakeUp": ["on_wake_up"],
    "OnEnterAttack": ["on_enter_attack_state"],
    "OnAttacked": ["on_attacked", "on_damaged"],
    "OnSpelled": ["on_spelled"],
    "OnBattleTimer": ["on_battle_timer"],
    "OnLeaveAttack": ["on_leave_attack_state"],
    "OnEnterIdle": ["on_enter_idle_state"],
    "OnDie": ["on_die", "on_killed_by_user", "on_killed_by_npc"],
    "OnDespawn": ["on_despawn"],
    "OnMessage": ["on_message"],
    "OnStopFleeing": ["on_stop_to_flee"],
    "OnIdleTimer": ["on_idle_timer"],
    "OnSeeNpc": ["on_see_npc", "on_see_npc_move"],
    "OnSeeUser": ["on_see_user", "on_see_user_move"],
    "OnFriendKilled": ["on_see_friend_killed_by_user"],
    "OnFriendAttacked": ["on_see_friend_attacked", "on_see_friend_attacking"],
    "OnFriendSpelled": ["on_friend_spelled", "on_friend_spelling"],
}


#: Patterns whose spawns carry `despawn_at_attack_state=TRUE` -- their adds clean themselves up on the
#: state change, so a handler of ours that despawns them explicitly is doing retail's work by another
#: route rather than inventing one. Silikor and the dying Tiamat both looked invented for this reason.
SELF_CLEANING: set[str] = set()


def retail_handler_kinds(patterns_dir: pathlib.Path) -> dict[tuple[str, str], set[str]]:
    """(pattern, retail handler) -> kinds of action its branches carry."""
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
            if "<despawn_at_attack_state>TRUE" in re.sub(r"\s+", "", body):
                SELF_CLEANING.add(name.group(1))
            for handler in re.finditer(r"<(on_\w+)>(.*?)</\1>", body, re.S):
                flat = re.sub(r"\s+", "", handler.group(2))
                key = (name.group(1), handler.group(1))
                for op, kind in RETAIL_KIND.items():
                    if f"<{op}>" in flat or f"<{op}/>" in flat:
                        out[key].add(kind)
    return out


def served_patterns(repo: pathlib.Path, binding_tsv: pathlib.Path) -> dict[str, set[str]]:
    """AI name -> the retail patterns the npcs carrying it actually run."""
    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai_of: dict[str, str] = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        hit = re.search(r'ai="([^"]*)"', attrs)
        if hit:
            ai_of[npc_id] = hit.group(1)

    rows = [line.rstrip("\n").split("\t") for line in open(binding_tsv, encoding="utf-8")]
    col = {c: i for i, c in enumerate(rows[0])}
    out: dict[str, set[str]] = collections.defaultdict(set)
    for row in rows[1:]:
        npc_id = row[col["npc_id"]]
        pattern = row[col["pattern_name"]]
        name = ai_of.get(npc_id)
        if name and pattern:
            out[name].add(pattern)
    return out


def our_handler_kinds(text: str) -> dict[str, dict[str, set[str]]]:
    """AI name -> handler -> kinds, for one source file (a file may hold several classes)."""
    out: dict[str, dict[str, set[str]]] = {}
    # Split on [AIName("...")] so a file with several classes is attributed correctly.
    parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
    for i in range(1, len(parts), 2):
        name, body = parts[i], parts[i + 1]
        per: dict[str, set[str]] = {}
        for handler in HANDLERS:
            hit = re.search(rf"\b{handler}\s*=\s*(.*?)(?:\n\s*On[A-Z]\w*\s*=|\n\s*\}};)", body, re.S)
            if not hit:
                continue
            kinds: set[str] = set()
            for action in re.findall(r"Do\.(\w+)", hit.group(1)):
                for prefix, kind in OUR_KIND:
                    if action.startswith(prefix):
                        kinds.add(kind)
                        break
            if kinds:
                per[handler] = kinds
        if per:
            out[name] = per
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--verdict", choices=["invented", "substituted", "dropped", "all"],
                    default="all")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    retail = retail_handler_kinds(pathlib.Path(args.patterns_dir))
    serves = served_patterns(repo, pathlib.Path(args.binding_tsv))
    handlers_dir = repo / "src/Aion.GameServer/Handlers/AI"

    findings: list[tuple[str, str, str, str, str]] = []
    compared = 0
    unbound: set[str] = set()
    for path in sorted(handlers_dir.glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for ai_name, per_handler in our_handler_kinds(text).items():
            patterns = serves.get(ai_name, set())
            if not patterns:
                unbound.add(ai_name)
                continue
            for handler, ours in per_handler.items():
                theirs: set[str] = set()
                for pattern in patterns:
                    for retail_handler in HANDLERS[handler]:
                        theirs |= retail.get((pattern, retail_handler), set())
                if not theirs:
                    continue
                compared += 1
                invented = ours - theirs
                # A spawn or a despawn where retail casts a skill is this port's standing substitution,
                # not an invention: we cannot cast a summon or a suicide, so the adds are spawned and the
                # self-destructs are despawned. Every such case in this log is documented. Reporting them
                # beside real findings buried the one that mattered under twenty that did not, which is
                # the same failure the first version of audit_invented_actions.py had.
                # Retail's own cleanup: adds flagged despawn_at_attack_state go on the state change,
                # and our engine has no such flag, so the pattern despawns them in the handler instead.
                if ("despawn" in invented
                        and handler in ("OnLeaveAttack", "OnDie", "OnEnterIdle", "OnDespawn")
                        and any(p in SELF_CLEANING for p in patterns)):
                    findings.append((path.name, ai_name, handler, "substituted", "despawn"))
                    invented = invented - {"despawn"}
                if "skill" in theirs:
                    substituted = invented & {"spawn", "despawn"}
                    if substituted:
                        findings.append((path.name, ai_name, handler, "substituted",
                                         "/".join(sorted(substituted))))
                    invented = invented - substituted
                if invented:
                    findings.append((path.name, ai_name, handler, "invented",
                                     "/".join(sorted(invented))))
                elif ours - theirs != ours and theirs - ours:
                    findings.append((path.name, ai_name, handler, "dropped",
                                     "/".join(sorted(theirs - ours))))

    order = {"invented": 0, "substituted": 1, "dropped": 2}
    findings.sort(key=lambda f: (order.get(f[3], 3), f[0], f[2]))
    print(f"{compared} handlers compared against the retail patterns their npcs actually run")
    print(f"({len(unbound)} AI names bound to no npc that runs a known pattern, so unchecked)\n")
    print(f"{'file':<30} {'ai name':<28} {'handler':<16} {'verdict':<9} kinds")
    shown = 0
    for name, ai_name, handler, verdict, kinds in findings:
        if args.verdict != "all" and verdict != args.verdict:
            continue
        print(f"{name:<30} {ai_name:<28} {handler:<16} {verdict:<9} {kinds}")
        shown += 1
    counts = collections.Counter(f[3] for f in findings)
    print()
    print(f"  {counts['invented']:3d} invented, {counts['substituted']:3d} substituted, "
          f"{counts['dropped']:3d} dropped, {compared - len(findings):3d} matching")
    print()
    print("An 'invented' row is an action retail never asked for in that handler. It is the only kind of")
    print("finding here that no other audit in this directory can reach.")
    print()
    print("TWO ROWS ARE EXPECTED AND EXPLAINED, both labelled in their own source:")
    print("  captain_xasta / OnEnterAttack / say       -- Java's CaptainXastaAI broadcasts 1500388, and")
    print("                                               Java is the spec except where retail AI data")
    print("                                               explicitly outranks it.")
    print("  sheban_mystical_tyrhund / OnMessage /     -- stands in for retail's suicide skill, which")
    print("  broadcast                                    kills the hand and so fires its on_die.")
    print("Anything else in that column is new and unread.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
