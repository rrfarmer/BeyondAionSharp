"""Which NPCs does no retail branch name, and which does only our data forget?

Written to settle a wrong claim in `docs/retail-ai-fidelity.md`. That entry said
`BIDSeal_Twin_P_Sum_Crater` (855623) was "named by no branch in the 5.8 files", filed it beside
Watchman Hokuruki's gunners, and was false: the magma glutten spawns it. The mistake was the *search*
-- it looked for the **npc id**, and retail's spawn actions never carry npc ids. They carry

    <npc_nameid>BIDSeal_Twin_P_Sum_Crater</npc_nameid>

a **devname**, whose id exists only in the client binding table. A grep for the number across the dump
therefore returns nothing for every npc in the game, spawned or not, and any conclusion drawn from it
is unfalsifiable in the wrong direction.

So the claim has to be made against devnames, and it splits in two, which the old wording conflated:

  * **unnamed** -- no retail spawn branch anywhere names this devname. Nothing in the AI data
    summons it, so if we do not place it, it does not exist. A genuine "scenery, or an encounter
    nobody wired up" finding.
  * **unplaced** -- retail branches *do* name it, and our spawn data has no spot for it. That is not
    scenery; that is a summon whose caller we have or have not ported, and it belongs to
    `audit_missing_adds.py`'s question rather than this one.

Only the first justifies "nothing spawns this". This reports both so the difference is on the page.

Usage:
    python audit_unnamed_templates.py <client_root> <patterns_dir> <binding_tsv> [--repo ..]
    python audit_unnamed_templates.py ... --check 235649,236083   # settle specific ids
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import audit_missing_adds as A  # noqa: E402


def named_devnames(patterns_dir: pathlib.Path) -> set[str]:
    """Every devname any retail spawn action anywhere in the dump names."""
    named: set[str] = set()
    for path in sorted(patterns_dir.rglob("*.xml")):
        try:
            text = A.read_text(path)
        except Exception:
            continue
        for block in A.PATTERN_RE.findall(text):
            for _tag, body in A.SPAWN_RE.findall(block):
                for dev in A.NAMEID_RE.findall(body):
                    dev = dev.strip()
                    if dev:
                        named.add(dev.lower())
    return named


def placed_ids(repo: pathlib.Path) -> set[str]:
    """Every npc id our spawn data puts in the world."""
    placed: set[str] = set()
    spawns = repo / "game-server/data/static_data/spawns"
    for path in spawns.rglob("*.xml"):
        placed.update(re.findall(r'<spawn npc_id="(\d+)"', path.read_text(encoding="utf-8", errors="replace")))
    return placed


def template_ratings(repo: pathlib.Path) -> dict[str, str]:
    """npc id -> rating, for every template we ship."""
    text = (repo / "game-server/data/static_data/npcs/npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    out: dict[str, str] = {}
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', text):
        m = re.search(r'rating="([^"]*)"', attrs)
        out[npc_id] = m.group(1) if m else ""
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("client_root")
    ap.add_argument("patterns_dir")
    ap.add_argument("binding_tsv")
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--check", default="", help="comma-separated npc ids to settle individually")
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    patterns = pathlib.Path(args.patterns_dir)

    dev2id = A.client_devname_to_id(pathlib.Path(args.client_root))
    id2dev = {v: k for k, v in dev2id.items()}

    named = named_devnames(patterns)
    placed = placed_ids(repo)
    templates = template_ratings(repo)

    if args.check:
        for npc_id in [s.strip() for s in args.check.split(",") if s.strip()]:
            dev = id2dev.get(npc_id, "")
            if not dev:
                print(f"{npc_id}: no devname in the client binding -- cannot be settled either way")
                continue
            is_named = dev.lower() in named
            print(f"{npc_id}  {dev}")
            print(f"    named by a retail spawn branch: {'YES' if is_named else 'no'}")
            print(f"    placed by our spawn data:       {'YES' if npc_id in placed else 'no'}")
            print(f"    has a template on our server:   {'YES' if npc_id in templates else 'no'}")
        return 0

    # The population worth reporting: npcs we have a template for, that carry a retail AI pattern.
    with open(args.binding_tsv, encoding="utf-8") as fh:
        rows = [line.rstrip("\n").split("\t") for line in fh]
    header, rows = rows[0], rows[1:]
    col = {c: i for i, c in enumerate(header)}

    unnamed: list[tuple[str, str]] = []
    unplaced: list[tuple[str, str]] = []
    by_rating: collections.Counter[str] = collections.Counter()
    for row in rows:
        npc_id = row[col["npc_id"]]
        dev = row[col["client_devname"]]
        # A retail AI pattern is the price of admission: an npc with no pattern has no encounter
        # behind it, so "nothing spawns it" says nothing worth acting on.
        if not row[col["pattern_name"]]:
            continue
        if npc_id not in templates or not dev:
            continue
        if npc_id in placed:
            continue
        if dev.lower() in named:
            unplaced.append((npc_id, dev))
        else:
            unnamed.append((npc_id, dev))
            by_rating[templates.get(npc_id) or "(none)"] += 1

    print(f"{len(unnamed)} npcs no retail spawn branch names and our data never places "
          f"-- \"nothing spawns this\" is true of these")
    # The bulk of that number is ordinary field population the client ships a pattern for and our
    # world simply does not use. The ratings split is what makes it readable: a NORMAL npc nobody
    # spawns is a map we have not populated, while a HERO one is an encounter nobody wired up.
    for rating, count in by_rating.most_common():
        print(f"    {rating:12} {count}")
    print(f"{len(unplaced)} npcs retail branches DO name and our data never places "
          f"-- summons, not scenery; audit_missing_adds.py's question")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
