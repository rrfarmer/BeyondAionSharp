#!/usr/bin/env python3
"""
Fidelity guardrail (Phase A4 of the Port Fidelity & Remediation Plan).

Mechanically enforces the two structural rules that prose review failed to hold:

  1. No invented abstraction. A C# file under Aion.GameServer whose name contains a
     banned slop token (Plan/Bridge/Adapter/Composition/...) is a violation UNLESS a
     Java class of the same simple name exists (i.e. it is a faithful 1:1 port).
  2. No god-classes. A C# source file over the line threshold is "oversized"; it must
     not grow beyond its baseline, and no new file may cross the threshold.

The whole codebase currently violates rule 1 (~hundreds of files) and rule 2 (6 files),
so this is a RATCHET: a committed baseline freezes today's debt; the check fails only on
NEW violations or growth. Remediation (Phase C) deletes slop and shrinks god-classes,
then `--update-baseline` ratchets the floor down. The baseline must only ever shrink.

Usage:
    python scripts/parity/check_fidelity.py                 # check; exit 1 on new violations
    python scripts/parity/check_fidelity.py --update-baseline   # re-snapshot (only after reducing debt)
"""
from __future__ import annotations
import argparse
import json
import os
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
JAVA_REPO = Path(os.environ.get("BEYOND_AION_JAVA_ROOT", REPO.parent / "aion-server")).resolve()
JAVA_ROOT = JAVA_REPO / "game-server" / "src" / "com" / "aionemu" / "gameserver"
# Faithfully-ported content scripts (quests/AI/instances/zones/chat commands) live in the Java
# data/handlers tree, NOT under src/. They have exact-name C# counterparts under Handlers/. Index
# both Java roots so their real class names (e.g. OphidanBridgeInstance, TheImprisonedExecutor,
# ScaldingExecutorAI) are recognized as faithful 1:1 ports and not mis-flagged as invented slop.
JAVA_HANDLERS_ROOT = JAVA_REPO / "game-server" / "data" / "handlers"
JAVA_ROOTS = [JAVA_ROOT, JAVA_HANDLERS_ROOT]
CS_ROOT = REPO / "src" / "Aion.GameServer"
BASELINE = Path(__file__).resolve().parent / "fidelity-baseline.json"

GODCLASS_LINE_THRESHOLD = 3000

# Invented-layer vocabulary that has no Java counterpart (see parity-verification.md Fidelity Gate).
BANNED_TOKENS = {
    "Plan", "Bridge", "Adapter", "Composition", "Outcome", "Integration",
    "Owner", "Policy", "Executor", "Projection", "Snapshot", "Preview", "Fact",
}

CAMEL_RE = re.compile(r"[A-Z]+(?=[A-Z][a-z])|[A-Z][a-z0-9]*|[A-Z]+")
JAVA_TYPE_RE = re.compile(
    r"^\s*(?:public\s+|final\s+|abstract\s+|sealed\s+|static\s+|private\s+|protected\s+)*"
    r"(?:class|interface|enum|record)\s+([A-Z][A-Za-z0-9_]+)",
    re.MULTILINE,
)


def camel_tokens(name: str) -> list[str]:
    return CAMEL_RE.findall(name)


def normalize(name: str) -> str:
    """Canonical form for cross-language name matching: drop non-alphanumerics, lowercase.
    Collapses Java SNAKE_CASE (SM_HOUSE_OWNER_INFO) and C# PascalCase (SmHouseOwnerInfo) to
    the same key so faithful packet/class ports are not flagged as invented."""
    return re.sub(r"[^a-z0-9]", "", name.lower())


def java_type_norms() -> set[str]:
    norms: set[str] = set()
    for root in JAVA_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*.java"):
            norms.add(normalize(path.stem))
            try:
                for t in JAVA_TYPE_RE.findall(path.read_text(encoding="utf-8", errors="ignore")):
                    norms.add(normalize(t))
            except OSError:
                pass
    return norms


def cs_source_files() -> list[Path]:
    out = []
    for path in CS_ROOT.rglob("*.cs"):
        s = str(path)
        if f"{os.sep}obj{os.sep}" in s or f"{os.sep}bin{os.sep}" in s:
            continue
        if path.name.endswith(".g.cs") or path.name == "AssemblyInfo.cs":
            continue
        out.append(path)
    return out


def rel(path: Path) -> str:
    return str(path.relative_to(REPO)).replace("\\", "/")


def line_count(path: Path) -> int:
    with open(path, "r", encoding="utf-8", errors="ignore") as fh:
        return sum(1 for _ in fh)


def java_oversized_norms() -> set[str]:
    """Normalized stems of Java files that are themselves over the god-class threshold.
    A C# file that mirrors one of these 1:1 is NOT a fused god-class — Java intentionally keeps it
    as a single large file (e.g. DialogAction.java, 6246 lines: enum->class for the JVM 64KB limit).
    Exempting these encodes the 1:1 rule; it does NOT exempt true fusions (e.g. GameServerConnection),
    which have no large Java counterpart of the same name."""
    norms: set[str] = set()
    for root in JAVA_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*.java"):
            if line_count(path) > GODCLASS_LINE_THRESHOLD:
                norms.add(normalize(path.stem))
    return norms


def compute() -> dict:
    java_norms = java_type_norms()
    java_big = java_oversized_norms()
    banned_files: list[str] = []
    oversized: dict[str, int] = {}
    for path in cs_source_files():
        stem = path.stem
        if normalize(stem) not in java_norms and any(t in BANNED_TOKENS for t in camel_tokens(stem)):
            banned_files.append(rel(path))
        n = line_count(path)
        # A faithful 1:1 port of an intentionally-large Java file is not a god-class.
        if n > GODCLASS_LINE_THRESHOLD and normalize(stem) not in java_big:
            oversized[rel(path)] = n
    return {
        "_comment": "Fidelity ratchet baseline. Only ever shrink this (via --update-baseline after "
                    "deleting slop / splitting god-classes). Never hand-add entries to silence new debt.",
        "godclass_line_threshold": GODCLASS_LINE_THRESHOLD,
        "banned_vocab_files": sorted(banned_files),
        "oversized_files": dict(sorted(oversized.items())),
    }


def load_baseline() -> dict:
    if not BASELINE.exists():
        return {"banned_vocab_files": [], "oversized_files": {}}
    return json.loads(BASELINE.read_text(encoding="utf-8"))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--update-baseline", action="store_true",
                    help="re-snapshot the baseline (use only after reducing debt)")
    args = ap.parse_args()

    current = compute()

    if args.update_baseline:
        BASELINE.write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
        print(f"Baseline updated: {len(current['banned_vocab_files'])} banned-vocab files, "
              f"{len(current['oversized_files'])} oversized files.")
        return 0

    base = load_baseline()
    base_banned = set(base.get("banned_vocab_files", []))
    base_oversized = base.get("oversized_files", {})

    failures: list[str] = []

    new_banned = sorted(set(current["banned_vocab_files"]) - base_banned)
    for f in new_banned:
        failures.append(f"NEW invented-abstraction file (banned vocabulary, no Java counterpart): {f}")

    for f, n in current["oversized_files"].items():
        if f not in base_oversized:
            failures.append(f"NEW god-class: {f} has {n} lines (> {GODCLASS_LINE_THRESHOLD}). "
                            f"Mirror Java's per-packet/per-class split instead.")
        elif n > base_oversized[f]:
            failures.append(f"God-class GREW: {f} now {n} lines (baseline {base_oversized[f]}). "
                            f"It must shrink, not grow.")

    # Informational: debt that was removed (good — remember to --update-baseline).
    removed = sorted(base_banned - set(current["banned_vocab_files"]))
    shrunk = [f for f, n in current["oversized_files"].items()
              if f in base_oversized and n < base_oversized[f]]
    gone = sorted(set(base_oversized) - set(current["oversized_files"]))

    if removed or shrunk or gone:
        print(f"Progress since baseline: {len(removed)} slop files removed, "
              f"{len(shrunk)} god-classes shrunk, {len(gone)} no longer oversized.")
        print("  -> run `python scripts/parity/check_fidelity.py --update-baseline` to ratchet the floor down.\n")

    if failures:
        print("FIDELITY GUARDRAIL FAILED - new structural debt introduced:\n")
        for msg in failures:
            print("  - " + msg)
        print("\nThe C# must mirror Java 1:1 (one Java file -> one C# file, no invented layers, no god-class).")
        print("See docs/parity-verification.md (Fidelity Gate). Do NOT add baseline entries to silence this.")
        return 1

    print(f"Fidelity guardrail OK. Baseline: {len(base_banned)} known slop files, "
          f"{len(base_oversized)} known god-classes; no new violations.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
