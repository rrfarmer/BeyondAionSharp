# Upstream Java porting

Port merged Java changes in strict `4.8` history order. One upstream Java commit produces one completed C# commit or one explicit blocked record. Never merge or cherry-pick Java history into C#.

## Repository policy

- C# work happens locally on `main`; do not create port branches or pull requests.
- The C# repository must contain only `main` and must be clean before a package is prepared.
- The Java checkout is read-only behavioral reference data.
- `docs/upstream-port-state.json` is the queue cursor. Do not select a later commit manually.
- `docs/upstream-port-log.md` is the human ledger.
- Generated patches, prompts, reports, and logs belong under ignored `artifacts/upstream/`.

## Per-commit workflow

1. Run `scripts/upstream/scan-upstream.ps1` and confirm the first pending merged commit.
2. Run `scripts/upstream/prepare-next.ps1`; use its generated `prompt.md` and exact Java patch.
3. Explain the behavior, map every affected Java artifact to C#, and port only that commit.
4. Add focused regression coverage and run `scripts/upstream/validate-port.ps1`.
5. Run `scripts/upstream/complete-port.ps1` with the reviewed status and evidence-based notes.
6. Review and stage only the intended files, then commit with the generated `commit-message.txt`.
7. Run `scripts/upstream/verify-port.ps1` before considering the queue item complete.

The detailed commands and n8n setup are in `docs/automation/n8n-upstream-port-workflow.md`.

## Automated workflow

The Docker n8n workflow schedules the same lifecycle without changing its authority model. n8n fetches and scans, then invokes `scripts/upstream/run-next-port.ps1`; that runner locks the queue, generates the exact saved prompt, starts noninteractive Codex CLI, and independently verifies the resulting local commit.

Automation handles at most one merged Java commit per execution. A blocked decision or dirty worktree stops the queue. n8n does not choose commits, call a model directly, update tracker files, or create Git history itself.

## Status rules

| CLI status | Ledger status | Advance cursor | Validation required |
|---|---|---:|---:|
| `ported` | Ported | Yes | Yes |
| `direct-data` | Direct data carryover | Yes | Yes |
| `not-applicable` | Not applicable | Yes | No, but precise evidence is required |
| `blocked` | Blocked | No | No, but the missing prerequisite is required |

A blocked record may be resolved later. The later completed C# commit uses the same Java SHA, replaces the ledger row, and advances the cursor. Later Java commits remain ineligible until then.

## Commit contract

Every tracker decision is a local C# commit with exactly one trailer pair:

```text
Upstream-Java-SHA: <40-character-sha>
Port-Status: ported|direct-data|not-applicable|blocked
```

## Review rules

- Java describes intent and observable behavior; C# uses established local infrastructure.
- Preserve ordering, null behavior, enum and ordinal semantics, numeric overflow, timing, packet layouts, persistence effects, and data compatibility.
- Do not bundle cleanup, redesign, or adjacent Java commits.
- Language-neutral XML, SQL, and configuration may be carried directly only after C# loader and model compatibility is verified.
- A green build alone is insufficient. Require focused regression coverage or a concrete explanation of the validation boundary.
