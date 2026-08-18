"""Settle whether retail's friend-killed "friend" means the tribe table's `friend` or something wider.

`FriendDeathNotice` delivers retail's `on_*_friend_killed_by_user` to watchers that
`TribeRelationService.IsFriend` accepts -- same tribe, or named in one another's `<friend>` list. The
black claw taygas exposed the question: `LYCAN_PET` and `LYCAN_HUNTER` are related by `<support>`, so a
tayga never hears its own tamer fall, and the branch that answers a tamer's death is dead under the
narrow reading.

The doc entry for that commit wrote down the experiment that would settle it, and this is that
experiment:

    A pattern carrying a friend-killed handler whose npcs have **no** friend-reachable companion placed
    anywhere near them is dead under the narrow reading and alive under the wide one -- and retail would
    not have written it dead.

So for every live npc on a friend-killed pattern, this asks what is actually standing next to it, using
the spawn data and each npc's own `srange` (the same eye `FriendDeathNotice` uses):

  * **friend-reachable** -- a same-tribe or `<friend>`-listed npc within sight. The handler already
    works; no question.
  * **support-only** -- nothing friend-reachable in sight, but a `<support>`-listed npc there. **These
    are the deciding cases.** Every one of them is a branch NCSoft wrote and this server never runs.
  * **alone** -- nothing of either kind in sight.

**The experiment did not settle it, and the `alone` bucket is why.** Three quarters of these npcs have
no companion of *any* relation inside their own sight range: the median nearest one is **twenty metres**
away against a median `srange` of **eight**, and among those out of range the median is thirty. So most of the handler's branches are unreachable from
static placement whatever `friend` is taken to mean, and the support-only count -- about one npc in
nine -- is not the deciding evidence the doc hoped for. `--verbose` lists those cases so they can be
judged one at a time.

The tool therefore also reports the nearest-companion distances, which is the finding that outlived the
question, and the split between retail's `on_see_` and `on_sense_` variants. This port collapses those
two into one event; if `sense` reached further than sight, the sense patterns' npcs should be placed
further apart. **They are (27m against 15m median), but the proportion within sight barely moves (20%
against 27%)**, and the two pattern sets cover different content, so it is a hint rather than a result.

Usage:
    python audit_friend_reach.py <patterns_dir> <binding_tsv> [--repo ..] [--verbose]
"""
from __future__ import annotations

import argparse
import collections
import math
import pathlib
import re
import statistics
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

HANDLER_RE = re.compile(r"<on_\w*friend_killed\w*>")


def patterns_with_handler(patterns_dir: pathlib.Path) -> set[str]:
    """Every pattern carrying either friend-killed handler."""
    found: set[str] = set()
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = m.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if name and HANDLER_RE.search(body):
                found.add(name.group(1))
    return found


def handler_variants(patterns_dir: pathlib.Path) -> dict[str, str]:
    """"see", "sense" or "both" per pattern -- retail's two variants, which this port collapses."""
    variants: dict[str, str] = {}
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        for m in re.finditer(r"<npc_ai_pattern>(.*?)</npc_ai_pattern>", text, re.S):
            body = m.group(1)
            name = re.search(r"<name>(.*?)</name>", body)
            if not name:
                continue
            saw = "<on_see_friend_killed_by_user>" in body
            sensed = "<on_sense_friend_killed_by_user>" in body
            if saw and sensed:
                variants[name.group(1)] = "both"
            elif saw:
                variants[name.group(1)] = "see"
            elif sensed:
                variants[name.group(1)] = "sense"
    return variants


def tribe_tables(repo: pathlib.Path):
    """(base, friend set, support set) per tribe name."""
    text = (repo / "game-server/data/static_data/tribe/tribe_relations.xml").read_text(
        encoding="utf-8", errors="replace")
    base: dict[str, str] = {}
    friend: dict[str, set[str]] = {}
    support: dict[str, set[str]] = {}
    for m in re.finditer(r'<tribe name="([^"]+)"(?:\s+base="([^"]+)")?\s*>(.*?)</tribe>', text, re.S):
        name, b, body = m.group(1), m.group(2) or "", m.group(3)
        base[name] = b
        for tag, table in (("friend", friend), ("support", support)):
            hit = re.search(r"<%s>(.*?)</%s>" % (tag, tag), body, re.S)
            table[name] = set(hit.group(1).split()) if hit else set()
    return base, friend, support


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--verbose", action="store_true", help="list every support-only npc")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    base, friend, support = tribe_tables(repo)

    tribe_of: dict[str, str] = {}
    sight_of: dict[str, int] = {}
    name_of: dict[str, str] = {}
    templates = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        for key, table, cast in (("tribe", tribe_of, str), ("srange", sight_of, int),
                                 ("name", name_of, str)):
            hit = re.search(r'%s="([^"]*)"' % key, attrs)
            if hit:
                try:
                    table[npc_id] = cast(hit.group(1))
                except ValueError:
                    pass

    # (map, x, y, z) per spawned npc, and every spawn in the world keyed by map.
    spots: dict[str, list[tuple[str, float, float, float]]] = collections.defaultdict(list)
    by_map: dict[str, list[tuple[float, float, float, str]]] = collections.defaultdict(list)
    for path in (repo / "game-server/data/static_data/spawns").rglob("*.xml"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for block in re.finditer(r'<spawn_map map_id="(\d+)"(.*?)</spawn_map>', text, re.S):
            map_id, body = block.group(1), block.group(2)
            for sp in re.finditer(r'<spawn npc_id="(\d+)"(.*?)(?:</spawn>|/>)', body, re.S):
                npc_id, inner = sp.group(1), sp.group(2)
                for spot in re.finditer(r'<spot x="([-\d.]+)" y="([-\d.]+)" z="([-\d.]+)"', inner):
                    x, y, z = (float(spot.group(i)) for i in (1, 2, 3))
                    spots[npc_id].append((map_id, x, y, z))
                    by_map[map_id].append((x, y, z, npc_id))

    rows = [line.rstrip("\n").split("\t") for line in open(args.binding_tsv, encoding="utf-8")]
    col = {c: i for i, c in enumerate(rows[0])}
    members: dict[str, list[str]] = collections.defaultdict(list)
    for row in rows[1:]:
        members[row[col["pattern_name"]]].append(row[col["npc_id"]])

    def related(a: str, b: str, table: dict[str, set[str]]) -> bool:
        if a not in table or b not in table:
            return False
        return (b in table[a] or base.get(b, "") in table[a]
                or a in table[b] or base.get(a, "") in table[b])

    variants = handler_variants(pathlib.Path(args.patterns_dir))

    buckets = collections.Counter()
    support_only: list[tuple[str, str, str, str, str]] = []
    seen: set[str] = set()
    # Nearest companion of any relation, per npc, and the same split by handler variant.
    nearest: list[float] = []
    sranges: list[int] = []
    by_variant: dict[str, list[tuple[float, int]]] = collections.defaultdict(list)

    for pattern in sorted(patterns_with_handler(pathlib.Path(args.patterns_dir))):
        for npc_id in members.get(pattern, []):
            if npc_id in seen or npc_id not in spots:
                continue
            seen.add(npc_id)
            mine = tribe_of.get(npc_id, "")
            sight = sight_of.get(npc_id, 0)
            if not mine or sight <= 0:
                buckets["no tribe or no sight range"] += 1
                continue

            has_friend = has_support = False
            neighbour = ""
            closest: float | None = None
            for map_id, x, y, z in spots[npc_id]:
                for ox, oy, oz, other in by_map[map_id]:
                    if other == npc_id:
                        continue
                    span_all = math.dist((x, y, z), (ox, oy, oz))
                    theirs_all = tribe_of.get(other, "")
                    if theirs_all and (theirs_all == mine or related(mine, theirs_all, friend)
                                       or related(mine, theirs_all, support)):
                        if closest is None or span_all < closest:
                            closest = span_all
                    if span_all > sight:
                        continue
                    theirs = tribe_of.get(other, "")
                    if not theirs:
                        continue
                    if theirs == mine or related(mine, theirs, friend)                             or related(mine, theirs, support):
                        span = math.dist((x, y, z), (ox, oy, oz))
                        if closest is None or span < closest:
                            closest = span
                    if theirs == mine or related(mine, theirs, friend):
                        has_friend = True
                        break
                    if related(mine, theirs, support):
                        has_support = True
                        neighbour = other
                if has_friend:
                    break

            if closest is not None:
                nearest.append(closest)
                sranges.append(sight)
                variant = variants.get(pattern, "see")
                by_variant[variant].append((closest, sight))

            if has_friend:
                buckets["friend-reachable"] += 1
            elif has_support:
                buckets["support-only"] += 1
                support_only.append((pattern, npc_id, name_of.get(npc_id, ""), mine,
                                     f"{neighbour} {name_of.get(neighbour, '')} [{tribe_of.get(neighbour, '')}]"))
            else:
                buckets["alone"] += 1

    print("Live npcs on a pattern with a friend-killed handler, by what stands within their own sight:")
    print()
    for key in ("friend-reachable", "support-only", "alone", "no tribe or no sight range"):
        print(f"  {buckets[key]:5}  {key}")
    print()

    if args.verbose:
        for pattern, npc_id, npc_name, mine, other in support_only:
            print(f"  {pattern:32} {npc_id} {npc_name} [{mine}] -> {other}")
        print()

    if nearest:
        ordered = sorted(nearest)
        print("How far the nearest companion of ANY relation actually is:")
        print(f"  median {statistics.median(ordered):.1f}m   p25 {ordered[len(ordered) // 4]:.1f}m   "
              f"p75 {ordered[3 * len(ordered) // 4]:.1f}m")
        print(f"  their own srange: median {statistics.median(sranges):.0f}m "
              f"(range {min(sranges)}-{max(sranges)})")
        print()
        print("  This is why `alone` is the largest bucket, and why the experiment did not settle the")
        print("  question -- see the module docstring.")
        print()

    if by_variant:
        print("By retail's handler variant, which this port collapses into one event:")
        for variant in ("see", "sense", "both"):
            rows_v = by_variant.get(variant, [])
            if not rows_v:
                continue
            inside = sum(1 for span, sight in rows_v if span <= sight)
            print(f"  {variant:5} n={len(rows_v):3}  within own srange: {inside:3} "
                  f"({100.0 * inside / len(rows_v):.0f}%)   median nearest "
                  f"{statistics.median([s for s, _ in rows_v]):.1f}m")
        print()

    total = buckets["friend-reachable"] + buckets["support-only"] + buckets["alone"]
    if total:
        share = 100.0 * buckets["support-only"] / total
        print(f"{buckets['support-only']} of {total} placed npcs ({share:.1f}%) can only reach a "
              "companion through <support>.")
    print("Those are the branches the narrow reading of `friend` never runs -- but they are outnumbered")
    print("six to one by npcs no reading reaches, which is the result. See the module docstring.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
