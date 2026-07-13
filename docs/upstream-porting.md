# Upstream Java porting

Port upstream changes in strict Java history order. One upstream Java commit produces one C# commit or one explicit ledger decision. Never merge or cherry-pick the Java branch into `main`.

## Per-commit workflow

1. Run `scripts/upstream/list-pending.ps1` and select the first commit only.
2. Read the complete patch with `git show --find-renames <sha>` and identify the behavior being fixed.
3. Map every affected Java type, configuration key, SQL object, and data file to its C# equivalent.
4. Create `codex/upstream-<short-sha>-<slug>` from current `main`.
5. Port only that behavior. Language-neutral XML/SQL/config changes may be carried directly after checking C# compatibility.
6. Add focused regression coverage that fails before the port and passes after it.
7. Run focused tests, then broader tests when the change touches shared infrastructure or data loading.
8. Commit with the trailers below and open a PR for human review.
9. After merge, update the ledger and `lastCompletedJavaCommit` in `upstream-port-state.json`.

```text
Upstream-Java-SHA: <40-character-sha>
Port-Status: ported
```

Allowed ledger statuses are `Pending`, `In progress`, `Ported`, `Direct data carryover`, `Not applicable`, and `Blocked`. A skipped commit must explain why; do not advance the machine-readable state past a blocked commit because later fixes may depend on it.

## Review rules

- Java describes intent and observable behavior; C# should use established local infrastructure.
- Preserve ordering, null behavior, enum/ordinal semantics, numeric overflow, timing, packet layouts, and persistence side effects.
- Do not bundle cleanup or adjacent upstream commits.
- A green build alone is insufficient. Require a regression test or a concrete explanation of the validation boundary.
- Keep automation concurrency at one so commits are never reordered.

