#!/usr/bin/env python3
"""
Structural fidelity audit (Phase A3 of the Port Fidelity & Remediation Plan).

Compares the Java game-server source tree against the C# Aion.GameServer tree and
reports where the C# structure diverges from Java 1:1 fidelity:

  - explosion clusters : many C# files mapping to one (or zero) Java class  -> slop
  - orphan C# stems    : C# name stems with no Java counterpart             -> invented
  - missing Java       : Java classes (engine/services) with no C# match    -> gaps

Output is a Markdown scorecard. It is intentionally heuristic: it ranks where to
look, it does not make final fidelity judgements. Run:

    python scripts/parity/structural_audit.py
    python scripts/parity/structural_audit.py --out docs/Structural-Audit-Scorecard.md
"""
from __future__ import annotations
import argparse
import os
import re
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
JAVA_REPO = Path(os.environ.get("BEYOND_AION_JAVA_ROOT", REPO.parent / "aion-server")).resolve()
JAVA_ROOT = JAVA_REPO / "game-server" / "src" / "com" / "aionemu" / "gameserver"
# Faithfully-ported content scripts (quests/AI/instances/zones/chat commands) live in the Java
# data/handlers tree, NOT under src/. Their exact-name C# counterparts live under Handlers/. Index
# both Java roots so the ~1700 content scripts are recognized as faithful ports, not orphan/invented.
JAVA_HANDLERS_ROOT = JAVA_REPO / "game-server" / "data" / "handlers"
JAVA_ROOTS = [JAVA_ROOT, JAVA_HANDLERS_ROOT]
CS_ROOT = REPO / "src" / "Aion.GameServer"

JAVA_TYPE_RE = re.compile(
    r"^\s*(?:public\s+|final\s+|abstract\s+|sealed\s+|static\s+)*"
    r"(?:class|interface|enum|record)\s+([A-Z][A-Za-z0-9_]+)",
    re.MULTILINE,
)
CAMEL_RE = re.compile(r"[A-Z]+(?=[A-Z][a-z])|[A-Z][a-z0-9]*|[A-Z]+")

# C# type declarations (top-level AND nested). Used ONLY to widen the recognized-name set for the
# "missing Java" gap table: the faithful port keeps Java's one-file-many-types layout, so a Java
# nested/secondary type (e.g. an inner enum/Runnable task) lands inside its parent C# file rather
# than a same-name file. Indexing nested C# type names lets the gap table see those ports. This does
# NOT touch the orphan/explosion slop heuristics (which key off file stems) — slop detection is unchanged.
CS_TYPE_RE = re.compile(
    r"^\s*(?:public\s+|internal\s+|private\s+|protected\s+|file\s+|static\s+|sealed\s+|abstract\s+|"
    r"partial\s+|readonly\s+|new\s+)*"
    r"(?:class|interface|enum|record(?:\s+struct)?|struct)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

# Suffixes that are structural noise when deriving a stem for cluster matching.
NOISE_TOKENS = {
    "Service", "Plan", "Bridge", "Adapter", "Composition", "Outcome",
    "Integration", "Owner", "Policy", "Executor", "Projection", "Snapshot",
    "Preview", "Fact", "Runtime", "Dispatch", "Coordinator", "Contract",
    "Report", "Envelope", "Metadata", "Boundary", "Workflow", "Trace",
    "Resolver", "Builder", "Factory", "Handler", "Helper",
}


def line_count(path: Path) -> int:
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as fh:
            return sum(1 for _ in fh)
    except OSError:
        return 0


def tokens(name: str) -> list[str]:
    return CAMEL_RE.findall(name)


def stem(name: str, n: int = 2) -> str:
    """Leading n CamelCase tokens, used as a cluster key (e.g. BindPointTeleport... -> 'BindPoint')."""
    return "".join(tokens(name)[:n])


def collect_java() -> dict[str, dict]:
    """Map Java simple class name -> {area, path, lines}. First definition wins.

    Indexes both the engine src/ tree and the data/handlers content-script tree so faithfully-ported
    quests/AI/instances are recognized as 1:1 (and excluded from the orphan/missing tables). Content
    scripts are bucketed under a `handlers/...` area so they do not pollute the engine-gap buckets."""
    classes: dict[str, dict] = {}
    for root in JAVA_ROOTS:
        if not root.exists():
            continue
        is_handlers = (root == JAVA_HANDLERS_ROOT)
        for path in root.rglob("*.java"):
            rel = path.relative_to(root)
            area = ("handlers/" + rel.parts[0]) if is_handlers else (
                rel.parts[0] if len(rel.parts) > 1 else "(root)")
            text = path.read_text(encoding="utf-8", errors="ignore")
            names = JAVA_TYPE_RE.findall(text)
            primary = path.stem  # file name == primary public type by Java convention
            for nm in ([primary] + names):
                classes.setdefault(nm, {"area": area, "path": str(rel).replace("\\", "/"),
                                        "lines": line_count(path)})
    return classes


def collect_cs() -> list[dict]:
    files = []
    for path in CS_ROOT.rglob("*.cs"):
        s = str(path)
        if f"{os.sep}obj{os.sep}" in s or f"{os.sep}bin{os.sep}" in s:
            continue
        if path.name.endswith(".g.cs") or path.name == "AssemblyInfo.cs":
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        nested = set(CS_TYPE_RE.findall(text))
        nested.discard(path.stem)
        files.append({"name": path.stem,
                      "path": str(path.relative_to(CS_ROOT)).replace("\\", "/"),
                      "lines": line_count(path),
                      "nested": nested})
    return files


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=None, help="write Markdown scorecard to this path")
    ap.add_argument("--top", type=int, default=40, help="rows per table")
    args = ap.parse_args()

    java = collect_java()
    cs = collect_cs()
    java_names = set(java)

    # Cluster C# files by 2-token stem.
    clusters: dict[str, list[dict]] = defaultdict(list)
    for f in cs:
        clusters[stem(f["name"])].append(f)

    # For each cluster, find Java classes that share the stem prefix.
    rows = []
    for key, members in clusters.items():
        if not key:
            continue
        matched_java = [jn for jn in java_names if stem(jn) == key]
        cs_lines = sum(m["lines"] for m in members)
        java_lines = sum(java[jn]["lines"] for jn in matched_java)
        rows.append({
            "stem": key,
            "cs_files": len(members),
            "cs_lines": cs_lines,
            "java_classes": len(matched_java),
            "java_lines": java_lines,
            "ratio": cs_lines / java_lines if java_lines else float("inf"),
            "matched_java": matched_java,
        })

    # Exact-name fidelity: Java classes that have a C# file of the same name.
    cs_name_set = {f["name"] for f in cs}
    exact = sorted(jn for jn in java_names if jn in cs_name_set)

    # Missing engine/service classes (high-value gaps).
    # A Java type is "ported" if a C# type of the same name exists ANYWHERE — as a top-level file
    # stem OR as a nested/secondary type inside another file — matched case-insensitively (the port
    # PascalCases acronym names, e.g. Java AIState -> C# AiState). This recognizes the faithful
    # one-file-many-types layout and case-rename convention so the gap table reflects only REAL gaps.
    GAP_AREAS = {"skillengine", "controllers", "ai", "questEngine", "services"}
    cs_all_type_names = {f["name"].lower() for f in cs}
    for f in cs:
        cs_all_type_names.update(n.lower() for n in f["nested"])
    missing = [(jn, java[jn]) for jn in java_names
               if java[jn]["area"] in GAP_AREAS and jn.lower() not in cs_all_type_names]

    # Orphan C# stems: stem matches no Java class stem at all.
    java_stems = {stem(jn) for jn in java_names}
    orphans = defaultdict(list)
    for f in cs:
        if stem(f["name"]) not in java_stems and f["name"] not in java_names:
            orphans[stem(f["name"])].append(f)

    out = []
    w = out.append
    w("# Structural Audit Scorecard")
    w("")
    w("Generated by `scripts/parity/structural_audit.py` (Phase A3). Heuristic: ranks where")
    w("the C# structure diverges from Java 1:1 fidelity. Use as the Phase B/C remediation backlog.")
    w("")
    w("## Summary")
    w("")
    w(f"- Java gameserver types indexed: **{len(java)}**")
    w(f"- C# GameServer files indexed: **{len(cs)}**")
    w(f"- Java classes with an exact-name C# counterpart: **{len(exact)}**")
    w(f"- C# stem clusters: **{len(clusters)}**")
    w("")
    w("## Explosion Clusters (remediation priority)")
    w("")
    w("C# stems with the most files, and the Java class(es) sharing that stem. High `cs_files`")
    w("with low `java_classes` is the slop signature (re-port fresh per the plan).")
    w("")
    w("| C# stem | C# files | C# lines | Java classes | Java lines | line ratio | Java match |")
    w("| --- | ---: | ---: | ---: | ---: | ---: | --- |")
    for r in sorted(rows, key=lambda r: r["cs_files"], reverse=True)[:args.top]:
        ratio = "n/a" if r["java_lines"] == 0 else f"{r['ratio']:.0f}x"
        match = ", ".join(r["matched_java"][:2]) + ("…" if len(r["matched_java"]) > 2 else "") or "—(none)"
        w(f"| {r['stem']} | {r['cs_files']} | {r['cs_lines']} | {r['java_classes']} | "
          f"{r['java_lines']} | {ratio} | {match} |")
    w("")
    w("## Orphan C# Stems (no Java counterpart — invented)")
    w("")
    w("| C# stem | files | example file |")
    w("| --- | ---: | --- |")
    for key, members in sorted(orphans.items(), key=lambda kv: len(kv[1]), reverse=True)[:args.top]:
        w(f"| {key} | {len(members)} | {members[0]['name']} |")
    w("")
    w("## Missing High-Value Java (engine/services gaps)")
    w("")
    w(f"Java classes in {sorted(GAP_AREAS)} with no exact-name C# file. Total: **{len(missing)}**.")
    w("")
    by_area = defaultdict(int)
    for _, meta in missing:
        by_area[meta["area"]] += 1
    w("| area | missing classes |")
    w("| --- | ---: |")
    for area, n in sorted(by_area.items(), key=lambda kv: kv[1], reverse=True):
        w(f"| {area} | {n} |")
    w("")

    report = "\n".join(out)
    if args.out:
        Path(args.out).write_text(report, encoding="utf-8")
        print(f"wrote {args.out}")
    else:
        print(report)


if __name__ == "__main__":
    main()
