# n8n upstream-port workflow

Use n8n as the queue and orchestration layer, not as the authority that merges code.

1. A daily schedule or GitHub webhook starts the workflow.
2. A command node fetches `java-upstream/4.8` and lists commits after `lastCompletedJavaCommit`.
3. Split commits into items but set workflow concurrency to one and process oldest first.
4. Store the 40-character Java SHA as the idempotency key. Exit when a ledger/PR already exists for it.
5. Fetch commit metadata, full patch, changed-file list, and parent SHA.
6. Create a clean worktree and branch named `codex/upstream-<short-sha>-<slug>`.
7. Invoke the coding agent with `docs/prompts/port-upstream-commit.md` and the exact patch.
8. Run deterministic build/test commands in a separate validation node. Never let the model self-report test success without command output.
9. Open a draft PR containing the upstream link, mapping, validation, and decision.
10. Require human approval and normal CI before merge.
11. On merged PR only, update the ledger and state file. On failure, retain artifacts and mark the queue item Blocked without advancing.

Recommended persisted fields: `upstream_sha`, `parent_sha`, `subject`, `status`, `branch`, `pr_url`, `csharp_sha`, `attempt_count`, `started_at`, `completed_at`, `model`, `prompt_version`, and `test_summary`.

Guardrails: one active port at a time; clean worktree required; branch must start from current `main`; no force-push to `main`; cap retries; redact environment secrets; and require a human merge. Data-only commits still receive a PR because loader compatibility must be reviewed.

