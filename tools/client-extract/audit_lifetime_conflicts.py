"""Find npcs whose own AI removes them before retail's lifetime is up.

Retail gives a spawned npc a `live_time`; this port often also gives its AI class a self-delete. When
the two disagree the shorter one wins, and if that is the AI then **retail's number is inert** -- it can
be deleted without changing anything, which is how three of them survived mutation sweeps this session:

* Terath's gravity pair -- 24 seconds, swept by the event at 30;
* Ebonsoul's black hole -- 10 seconds, closed by the add itself at 8 until that was corrected;
* Shabokan's SinkDMG -- 6 seconds, removed by `SinkingSandAI` as soon as it has cast.

Two of those were harmless (the port collapses a retail FX/DMG pair into one npc, so the AI's clock is
the real one). **One was not**: Shabokan's sink is meant to stand for a minute and this port deleted it
after four seconds, turning a field a raid walks around into a flash.

So the report is a question per row: *is this npc's short life the port collapsing two retail npcs into
one, or is it deleting something retail wants left standing?*

Usage:
    python audit_lifetime_conflicts.py [--patterns DIR] [--repo PATH] [--limit N]
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402

DEFAULT_PATTERNS = pathlib.Path("D:/Aion58ServerTesting/Server/Map/XML")

#: `AIActions.DeleteOwner(this)` / `GetController().Delete()` scheduled behind a delay.
SELF_DELETE = re.compile(
    r"(?:AIActions\.DeleteOwner\(this\)|GetOwner\(\)\.GetController\(\)\.Delete\(\))")

#: A scheduled delay in milliseconds, as either a bare literal or a TimeSpan.
DELAY = re.compile(
    r"(?:,\s*(\d[\d_]*)L?\s*\)|TimeSpan\.From(Seconds|Milliseconds|Minutes)\(\s*([\d_]+)\s*\))")

UNIT = {"Seconds": 1000, "Milliseconds": 1, "Minutes": 60000, None: 1}


def retail_lifetimes(patterns_dir: pathlib.Path) -> dict[str, set[int]]:
    """Every `live_time` retail gives each npc devname, in seconds."""
    out: dict[str, set[int]] = {}
    for path in patterns_dir.rglob("*.xml"):
        try:
            text = S.read_text(path)
        except Exception:
            continue
        # Bounded window rather than "up to the closing tag": several fields inside a spawn block close
        # with a tag that begins </spawn -- </spawn_id>, </spawn_range> -- so a non-greedy match to
        # "</spawn" stops before live_time and finds nothing. That is what made the first run of this
        # audit report zero conflicts, which looked like good news.
        for spawn in re.finditer(r"<npc_nameid>([^<]+)</npc_nameid>", text):
            window = text[spawn.end(): spawn.end() + 900]
            cut = window.find("<npc_nameid>")
            if cut > 0:
                window = window[:cut]
            life = re.search(r"<live_time>(\d+)</live_time>", window)
            if life and int(life.group(1)) > 0:
                out.setdefault(spawn.group(1).strip().lower(), set()).add(int(life.group(1)))
    return out


def self_delete_millis(source: str) -> int | None:
    """
    The delay this class waits before removing its own npc **as a lifetime**, in milliseconds.

    Only self-deletes reachable from `HandleSpawned` count. That distinction is the whole difference
    between a lifetime and a tidy-up: `TrapNpcAI` deletes its npc five seconds after the trap fires,
    which says nothing about how long an untriggered trap stands, and comparing that five against
    retail's `live_time=600` produced eighty-odd rows of noise on the first run of this audit -- every
    one of them a trap, and every one wrong.

    So: find the methods `HandleSpawned` calls, and count a delete only if it sits in one of them or in
    `HandleSpawned` itself. Still approximate, and a row remains a prompt to read the class.
    """
    spawned = re.search(r"HandleSpawned\(\)\s*\{(.*?)\n    \}", source, re.S)
    if not spawned:
        return None

    reachable = {"HandleSpawned"}
    reachable |= set(re.findall(r"\b([A-Z][A-Za-z0-9_]*)\(\s*\)\s*;", spawned.group(1)))

    best = None
    for method in re.finditer(
            r"(?:private|protected|public)[^\n]*?\b([A-Za-z0-9_]+)\([^)]*\)\s*\n?\s*\{", source):
        if method.group(1) not in reachable:
            continue
        body = source[method.end(): method.end() + 2500]
        lines = body.splitlines()
        for i, line in enumerate(lines):
            if not SELF_DELETE.search(line):
                continue
            window = "\n".join(lines[max(0, i - 2): i + 6])
            for literal, unit, value in DELAY.findall(window):
                millis = (int(literal.replace("_", "")) if literal
                          else int(value.replace("_", "")) * UNIT[unit])
                if 0 < millis <= 600_000 and (best is None or millis < best):
                    best = millis
    return best


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--patterns", default=str(DEFAULT_PATTERNS))
    ap.add_argument("--repo", default=str(pathlib.Path(__file__).resolve().parents[2]))
    ap.add_argument("--limit", type=int, default=25)
    args = ap.parse_args()

    repo = pathlib.Path(args.repo)
    static = repo / "game-server" / "data" / "static_data"

    lifetimes = retail_lifetimes(pathlib.Path(args.patterns))

    devname_of: dict[int, str] = {}
    binding = repo / "tools" / "client-extract" / "out" / "ai_binding.tsv"
    for line in binding.read_text(encoding="utf-8", errors="replace").splitlines():
        parts = line.split("\t")
        if len(parts) >= 2 and parts[0].isdigit():
            devname_of[int(parts[0])] = parts[1].strip().lower()

    npcs_of_ai: dict[str, list[int]] = {}
    name_of: dict[int, str] = {}
    templates = (static / "npcs" / "npc_templates.xml").read_text(encoding="utf-8", errors="replace")
    for npc_id, attrs in re.findall(r'<npc_template npc_id="(\d+)"([^>]*)>', templates):
        ai = re.search(r'\bai="([^"]*)"', attrs)
        name = re.search(r'\bname="([^"]*)"', attrs)
        name_of[int(npc_id)] = (name.group(1) if name else "").strip()
        if ai:
            npcs_of_ai.setdefault(ai.group(1).lower(), []).append(int(npc_id))

    rows = []
    for path in (repo / "src" / "Aion.GameServer" / "Handlers" / "AI").rglob("*.cs"):
        source = path.read_text(encoding="utf-8", errors="replace")
        ai_name = re.search(r'\[AIName\("([^"]+)"\)\]', source)
        if not ai_name:
            continue
        mine = self_delete_millis(source)
        if mine is None:
            continue

        for npc_id in npcs_of_ai.get(ai_name.group(1).lower(), []):
            theirs = lifetimes.get(devname_of.get(npc_id, ""), set())
            longer = sorted(s for s in theirs if s * 1000 > mine)
            if longer:
                rows.append((max(longer) * 1000 - mine, npc_id, name_of.get(npc_id, ""), path.stem,
                             mine, longer))

    rows.sort(reverse=True)
    print(f"{len(rows)} npcs are removed by their own AI before a retail live_time is up.")
    print("Each is a question: is the port collapsing two retail npcs into one, or deleting something")
    print("retail leaves standing?\n")
    for _gap, npc_id, name, stem, mine, longer in rows[: args.limit]:
        print(f"{npc_id}  {name or '(unnamed)':32s} {stem}")
        print(f"          removes itself after {mine / 1000:g}s; retail live_time {longer}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
