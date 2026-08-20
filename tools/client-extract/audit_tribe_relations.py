"""Our tribe relations against retail's, which the client ships as `npc_tribe_relation.xml`.

WHY THIS EXISTS
---------------
Tribe is the third place a mechanic can be missing, after the `ai` binding and the spawn point: an npc
can have the right class, the right pattern and a spawn and still do nothing, because `IsAware` refuses
hate toward something it is not at war with. `talle` is the case that proved it -- `tribe="GENERAL"`, at
war with nobody, in Java and in our data alike.

Retail ships the whole relation table and this port has its own copy, so the question is answerable
rather than a guess. **The formats differ and the comparison is not a diff:**

| | retail | ours |
|---|---|---|
| element | `<tribe Tribe="guard_Dragon">` | `<tribe name="GUARD_DRAGON" base="...">` |
| lists | comma-separated | space-separated |
| case | mixed | upper |
| kinds | aggressive, friendly, support, hostile, none, neutral, base_tribe | aggro, friend, support, hostile, none, neutral, base |

WHAT IT FOUND
-------------
**The relation table is not where guard hostility lives, and that is the thing to know before reading a
diff of it.** `guard` versus `guard_Dragon` is declared in neither file -- both give those tribes a
`friendly` list and nothing else -- and the two are enemies anyway, because `TribeRelationService`
hardcodes guard-versus-guard aggression by *base tribe*, in this port and in Java identically. A tribe
missing from this table is therefore not automatically an npc that cannot fight.

CLI:
    python audit_tribe_relations.py [--retail <npc_tribe_relation.xml>] [--limit N]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

REPO = pathlib.Path(__file__).resolve().parents[2]
OURS = REPO / "game-server" / "data" / "static_data" / "tribe" / "tribe_relations.xml"

#: Retail's name for each relation, against ours. `base_tribe` is an attribute here and an element
#: there, so it is handled separately.
SAME = {"aggressive": "aggro", "friendly": "friend", "support": "support",
        "hostile": "hostile", "none": "none", "neutral": "neutral"}


def retail_relations(path: pathlib.Path) -> dict[str, dict[str, set[str]]]:
    text = S.read_text(path)
    body = text[text.index("]>") + 2:]
    out: dict[str, dict[str, set[str]]] = {}
    for tribe in re.finditer(r'<tribe Tribe="([^"]+)">(.*?)</tribe>', body, re.S):
        rel: dict[str, set[str]] = {}
        for entry in re.finditer(r"<(\w+)>([^<]*)</\1>", tribe.group(2)):
            rel[entry.group(1)] = {v.strip().upper() for v in entry.group(2).split(",") if v.strip()}
        out[tribe.group(1).upper()] = rel
    return out


def our_relations(path: pathlib.Path) -> dict[str, dict[str, set[str]]]:
    text = path.read_text(encoding="utf-8", errors="replace")
    out: dict[str, dict[str, set[str]]] = {}
    # `<tribe .../>` is legal here and 29 tribes use it. A body pattern that assumes a closing tag
    # swallows the NEXT tribe whole, which hid 29 of them -- and this tool's first --apply run then
    # "added" 27 tribes the file already had. The self-closing form is matched first.
    for tribe in re.finditer(r'<tribe name="([^"]+)"([^>]*?)(?:/>|>(.*?)</tribe>)', text, re.S):
        rel: dict[str, set[str]] = {}
        for entry in re.finditer(r"<(\w+)>([^<]*)</\1>", tribe.group(3) or ""):
            rel[entry.group(1)] = {v.strip().upper() for v in entry.group(2).split() if v.strip()}
        base = re.search(r'base="([^"]+)"', tribe.group(2))
        if base:
            rel["base"] = {base.group(1).upper()}
        out[tribe.group(1).upper()] = rel
    return out


def apply_missing(retail, ours) -> int:
    """Add the tribes our own npcs are bound to and our relations file never declared.

    **It currently finds nothing, and that is the answer rather than a bug.** Every tribe named by
    `tribe=` on our npc templates is already declared; the 237 retail declares and we do not are for
    npcs this port has no template for, so adding them would be a large change nothing exercises.

    Kept because the first run of it was wrong in a way worth guarding against. `our_relations` could
    not see self-closing `<tribe .../>` entries, so it reported 28 tribes missing and this function
    duly appended 27 that the file already had. The full test suite passed with the duplicates in
    place. **Only the ones in use, and only when they are genuinely absent.**

    **References are filtered to tribes we know.** Retail's lists name tribes we do not carry, and a
    relation pointing at a tribe the loader has never heard of is not a relation -- it is a parse risk
    for no gain. What is dropped is counted and printed rather than left silent.
    """
    templates = (REPO / "game-server" / "data" / "static_data" / "npcs" / "npc_templates.xml").read_text(
        encoding="utf-8", errors="replace")
    used = {m.group(1).upper() for m in re.finditer(r'<npc_template[^>]*\btribe="([^"]+)"', templates)}
    wanted = sorted((used - set(ours)) & set(retail))
    if not wanted:
        print("nothing to add")
        return 0

    known = set(ours) | set(wanted)
    dropped = 0
    blocks = []
    for name in wanted:
        lines = []
        base = retail[name].get("base_tribe", set())
        attrs = f' base="{sorted(base)[0]}"' if base and sorted(base)[0] in known else ""
        for kind, mine in SAME.items():
            values = sorted(v for v in retail[name].get(kind, set()) if v in known)
            dropped += len(retail[name].get(kind, set())) - len(values)
            if values:
                lines.append(f"        <{mine}>{' '.join(values)}</{mine}>")
        opened = f'    <tribe name="{name}"{attrs}>'
        blocks.append(chr(10).join([opened, *lines, "    </tribe>"]))

    text = OURS.read_text(encoding="utf-8", errors="replace")
    marker = text.rindex("</tribe_relations>")
    OURS.write_text(text[:marker] + chr(10).join(blocks) + chr(10) + text[marker:], encoding="utf-8")
    print(f"added {len(blocks)} tribes our npcs use and our file never declared -> {OURS}")
    print(f"    {dropped} references dropped, naming tribes this port does not carry")
    return 0


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--retail", default="D:/Aion58ServerTesting/Server/Map/XML/npc_tribe_relation.xml")
    ap.add_argument("--limit", type=int, default=20)
    ap.add_argument("--tribe", action="append", help="report just these tribes; repeatable")
    ap.add_argument("--apply", action="store_true",
                    help="add the tribes our npcs use that our relations file does not declare")
    args = ap.parse_args()

    retail = retail_relations(pathlib.Path(args.retail))
    ours = our_relations(OURS)
    print(f"retail tribes: {len(retail)}   ours: {len(ours)}")

    if args.tribe:
        for name in (t.upper() for t in args.tribe):
            print(f"\n== {name}")
            print(f"   retail: {retail.get(name, '<absent>')}")
            print(f"   ours  : {ours.get(name, '<absent>')}")
        return 0

    if args.apply:
        return apply_missing(retail, ours)

    missing = sorted(set(retail) - set(ours))
    extra = sorted(set(ours) - set(retail))
    print(f"\n{len(missing)} tribes retail declares and we do not")
    for name in missing[:args.limit]:
        kinds = ", ".join(f"{k}={len(v)}" for k, v in sorted(retail[name].items()))
        print(f"   {name:38s} {kinds}")
    if len(missing) > args.limit:
        print(f"   ... and {len(missing) - args.limit} more")
    print(f"\n{len(extra)} tribes we declare and retail does not (first {min(args.limit, len(extra))})")
    for name in extra[:args.limit]:
        print(f"   {name}")

    # Where both know the tribe, which named relations does retail have that we do not?
    thinner: collections.Counter = collections.Counter()
    examples: dict[str, tuple[str, str]] = {}
    for name in sorted(set(retail) & set(ours)):
        for kind, mine in SAME.items():
            gap = retail[name].get(kind, set()) - ours[name].get(mine, set())
            if gap:
                thinner[kind] += len(gap)
                examples.setdefault(kind, (name, ", ".join(sorted(gap)[:4])))
    print(f"\nrelations retail lists and we do not, by kind:")
    for kind, count in thinner.most_common():
        where, sample = examples[kind]
        print(f"   {count:6d}  {kind:11s} e.g. {where}: {sample}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
