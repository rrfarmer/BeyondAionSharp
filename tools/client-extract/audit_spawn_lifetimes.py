"""Spawns that retail takes away and we do not.

Retail writes `live_time` on a spawn to say how long it stands. The pattern classes have honoured that
since they were written; the Java-parity classes could not, because `AbstractAI` had no timed spawn until
`SpawnFor` was added. **So every Java-parity class serving a pattern with a timed spawn is a candidate for
adds that never leave.**

Stormwing was the first one read by hand and it was exactly that: seven HP bands and four escalation waves
of twisters, none of which ever despawned, so a long pull ended surrounded by dozens. **The mechanic was
strictly harsher than retail's and got harsher the longer the fight ran.**

**The narrowing is the whole tool.** "Retail has a timed spawn" is not "we are missing it" -- a class may
not spawn that npc at all, or may already remove it some other way. Three verdicts:

  * **NO LIFETIME** -- the class spawns and never expires anything. **Read these first.**
  * **partial** -- the class uses `SpawnFor` somewhere but also plain `Spawn`, so some adds are timed and
    some are not. Usually a real gap, occasionally deliberate.
  * **self-timed** -- the class schedules its own deletes, so its adds already expire by another route.
    **Almost always nothing to do.** This verdict exists because two consecutive passes spent their
    reading budget on rows of exactly this kind before discovering they were already correct.
  * **timed** -- every spawn in the class carries a lifetime.
  * **no spawns** -- the class does not spawn at all, so retail's timed spawn lives in a branch we never
    ported. That is a different backlog (see the unported-flag-branch list) and is reported separately.

**Caveats.** This reads call sites, not behaviour: a class that spawns plainly and deletes on its own
schedule -- as `FortressInstanceDukeAI` did for its summons before this pass -- reads as `NO LIFETIME`
and is a false positive. It also cannot tell which of a class's spawns corresponds to which retail branch,
so the count is a signal, not a work item. **Read the pattern before changing anything**, exactly as
Stormwing needed: a histogram of its lifetimes said "30 seconds, mostly" and was wrong for three of seven
bands, and only the branch order gave the real mapping.

Usage:
    python audit_spawn_lifetimes.py <patterns_dir> <binding_tsv> [--repo ..] [--verdict "no lifetime"]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402


def retail_timed_spawns(patterns_dir: pathlib.Path) -> dict[str, list[tuple[str, int]]]:
    """Pattern name -> [(spawned devname, live_time)] for every spawn carrying a lifetime."""
    out: dict[str, list[tuple[str, int]]] = collections.defaultdict(list)
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
            for spawn in re.finditer(r"<spawn>(.*?)</spawn>", body, re.S):
                life = re.search(r"<live_time>(\d+)</live_time>", spawn.group(1))
                who = re.search(r"<npc_nameid>(.*?)</npc_nameid>", spawn.group(1))
                if life and int(life.group(1)) > 0:
                    out[name.group(1)].append((who.group(1) if who else "?", int(life.group(1))))
    return out


def our_classes(repo: pathlib.Path) -> dict[str, tuple[bool, int, int]]:
    """AI name -> (is a pattern class, plain Spawn calls, SpawnFor calls)."""
    out: dict[str, tuple[bool, int, int]] = {}
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
        for i in range(1, len(parts), 2):
            name, body = parts[i], parts[i + 1]
            # Whole body, not the first 3000 characters. The truncated version misread every pattern
            # class whose documentation runs longer than that -- Tahabata, Calindi and rm_56c are all
            # fully-built PatternAi tables that this audit reported as making no spawns at all, because
            # their remarks push the class declaration past the cut.
            pattern_class = "AiPattern" in body or ": PatternAi" in body
            # Do.SpawnAt / SpawnNear on the pattern side already carry liveSeconds; on the Java side the
            # bare Spawn( overloads do not.
            # Every way a class puts an npc in the world, not just Spawn(. Counting the bare call alone
            # hid four of this audit's real findings -- drakanmedic, rm_1337, brigadegenerallaksyaka and
            # alukina_emp all summon through a helper and read as spawning nothing at all, and each was
            # caught by hand rather than by this tool.
            plain = len(re.findall(
                r"(?<![A-Za-z])(?:Spawn|RndSpawnInRange|RndSpawn|SpawnServants|SpawnEnemyServant)\(",
                body))
            # A class that schedules its own deletes is already expiring its adds by another route.
            # Two passes running, the largest rows on this report were exactly that -- balaurbarricade
            # and unstableyamennes both wrote the schedule by hand before SpawnFor existed -- and each
            # cost a pass to read before turning out to be nothing. Detected rather than re-discovered.
            # The delete has to be INSIDE a scheduled body, not merely somewhere in the same file.
            # The first version asked only that both tokens appeared, and passed four classes whose
            # only cleanup ran in HandleDied or HandleBackHome -- death cleanup bounds an add after the
            # fight and does nothing during it, which is the distinction that has now decided seven rows.
            self_timed = any(
                re.search(r"DeleteIfAliveOrCancelRespawn|GetController\(\)\.Delete\(",
                          body[m.end():m.end() + 400])
                for m in re.finditer(r"Schedule\w*\(", body))
            # Expire( counts too: it is SpawnFor's other half, used where the spawn comes from
            # one of the other helpers rather than from Spawn directly.
            timed = len(re.findall(r"SpawnFor\(|Expire\(", body))
            out[name] = (pattern_class, plain, timed, self_timed)
    # Fold each class's base into it. DrakanMedicAI summons through SpawnServants, which lives in
    # DrakanPriestAI along with the Expire that bounds it, so read on its own the subclass looks like a
    # class that spawns and never expires -- the exact shape this audit exists to flag. It was flagged,
    # after the gap had already been fixed one file up.
    bodies: dict[str, str] = {}
    bases: dict[str, str] = {}
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
        for i in range(1, len(parts), 2):
            bodies[parts[i]] = parts[i + 1]
            hit = re.search(r"class\s+(\w+)\s*:\s*(\w+)", parts[i + 1])
            if hit:
                bases[parts[i]] = hit.group(2)

    by_class = {}
    for name, body in bodies.items():
        hit = re.search(r"class\s+(\w+)", body)
        if hit:
            by_class[hit.group(1)] = body

    for name in list(out):
        base = bases.get(name)
        seen = set()
        while base and base in by_class and base not in seen:
            seen.add(base)
            extra = by_class[base]
            pattern_class, plain, timed, self_timed = out[name]
            plain += len(re.findall(
                r"(?<![A-Za-z])(?:Spawn|RndSpawnInRange|RndSpawn|SpawnServants|SpawnEnemyServant)\(",
                extra))
            timed += len(re.findall(r"SpawnFor\(|Expire\(", extra))
            out[name] = (pattern_class, plain, timed, self_timed)
            hit = re.search(r"class\s+\w+\s*:\s*(\w+)", extra)
            base = hit.group(1) if hit else None
    return out


def devnames(binding_tsv: pathlib.Path) -> dict[str, str]:
    """Client devname -> npc id, so a retail spawn can be matched against our source."""
    rows = [line.rstrip("\n").split("\t") for line in open(binding_tsv, encoding="utf-8")]
    col = {c: i for i, c in enumerate(rows[0])}
    out = {}
    for row in rows[1:]:
        if len(row) > col["client_devname"] and row[col["client_devname"]]:
            out[row[col["client_devname"]]] = row[col["npc_id"]]
    return out


def spawned_ids(repo: pathlib.Path) -> dict[str, set[str]]:
    """AI name -> every npc id that appears literally in its source."""
    out: dict[str, set[str]] = {}
    for path in sorted((repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        parts = re.split(r'\[AIName\("([^"]+)"\)\]', text)
        for i in range(1, len(parts), 2):
            body = parts[i + 1]
            # Only ids the class actually spawns. An id that appears solely in a DeleteNpcs call is not
            # spawned here -- brigade_general_vasharti deletes three glove controllers defensively and
            # deliberately never summons them, and matching bare ids reported that as a missing lifetime.
            ids = set()
            for line in body.split("\n"):
                if "Spawn" not in line:
                    continue
                ids |= set(re.findall(r"\b(\d{6})\b", line))
                for word in re.findall(r"\b([A-Za-z_]\w*)\b", line):
                    hit = re.search(r"\b" + word + r"\s*=\s*(\d{6})\b", body)
                    if hit:
                        ids.add(hit.group(1))
            out[parts[i]] = ids
    return out


def served(repo: pathlib.Path, binding_tsv: pathlib.Path) -> dict[str, set[str]]:
    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    ai_of = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        hit = re.search(r'ai="([^"]*)"', attrs)
        if hit:
            ai_of[npc_id] = hit.group(1)

    rows = [line.rstrip("\n").split("\t") for line in open(binding_tsv, encoding="utf-8")]
    col = {c: i for i, c in enumerate(rows[0])}
    out: dict[str, set[str]] = collections.defaultdict(set)
    for row in rows[1:]:
        if len(row) <= col["pattern_name"]:
            continue
        name = ai_of.get(row[col["npc_id"]])
        if name and row[col["pattern_name"]]:
            out[name].add(row[col["pattern_name"]])
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding_tsv", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    ap.add_argument("--verdict", default=None)
    args = ap.parse_args()

    timed = retail_timed_spawns(args.patterns_dir)
    devname_ids = devnames(args.binding_tsv)
    body_ids = spawned_ids(args.repo)
    classes = our_classes(args.repo)
    serves = served(args.repo, args.binding_tsv)

    rows, counts = [], collections.Counter()
    for name, patterns in sorted(serves.items()):
        if name in ("aggressive", "general") or name not in classes:
            continue
        pattern_class, plain, spawn_for, self_timed = classes[name]
        if pattern_class:
            continue
        # Only lifetimes on npcs this class actually spawns. Without this the audit reports every
        # timed spawn in the pattern, and the first row read by hand -- macunbello -- was a false
        # positive of exactly that shape: its ten-second spawns are all BIDTP_NoShowNPC markers, while
        # the soul reapers it really summons carry live_time 0 and are permanent in retail too.
        lives = [life for p in patterns for who, life in timed.get(p, ())
                 if devname_ids.get(who) and devname_ids[who] in body_ids.get(name, set())]
        if not lives:
            continue
        if plain == 0 and spawn_for == 0:
            verdict = "no spawns"
        elif plain == 0:
            verdict = "timed"
        elif spawn_for:
            verdict = "partial"
        elif self_timed:
            verdict = "self-timed"
        else:
            verdict = "NO LIFETIME"
        counts[verdict] += 1
        rows.append((verdict, name, len(lives), sorted(set(lives)), plain, spawn_for))

    print(__doc__.splitlines()[0])
    print()
    order = {"NO LIFETIME": 0, "partial": 1, "self-timed": 2, "timed": 3, "no spawns": 4}
    for verdict, name, n, lives, plain, spawn_for in sorted(
            rows, key=lambda r: (order[r[0]], -r[2])):
        if args.verdict and args.verdict.lower() not in verdict.lower():
            continue
        print(f"{verdict:<12} {name:<34} retail timed spawns={n:<4} "
              f"lives={lives[:6]} ours: Spawn={plain} SpawnFor={spawn_for}")
    print()
    print("  ".join(f"{k}={v}" for k, v in sorted(counts.items())))
    print()
    print("CAVEAT: call sites, not behaviour. A class that deletes its own spawns on a schedule reads as")
    print("        NO LIFETIME. Counts are a signal; read the pattern's branch order before editing.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
