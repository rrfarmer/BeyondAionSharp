# n8n upstream-port workflow

This automation monitors the Java `4.8` branch and prepares one commit at a time for a reviewed C# port. The repository scripts own ordering and state. n8n only schedules and displays their results.

## Safety boundary

The initial workflow does:

- fetch `upstream/4.8` in the separate Java checkout;
- list merged commits after `lastCompletedJavaCommit` in strict history order;
- report open pull requests separately as a watchlist;
- prepare patch, metadata, and prompt artifacts for the first merged commit only.

It does not invoke an LLM, edit C# code, update the tracker, stage files, create commits, push, or create pull requests. Those steps remain reviewed local work on `main`.

## Repository components

| Path | Purpose |
|---|---|
| `scripts/upstream/scan-upstream.ps1` | Fetch and emit the merged queue plus open-PR watchlist. |
| `scripts/upstream/prepare-next.ps1` | Enforce clean single-`main` state and package the first pending commit. |
| `scripts/upstream/validate-port.ps1` | Run diff checks, restore, warning rebuild, tests, and fidelity checks. |
| `scripts/upstream/complete-port.ps1` | Record one reviewed decision and create the required commit message. |
| `scripts/upstream/verify-port.ps1` | Verify clean state, commit trailers, ledger status, and all completed mappings. |
| `scripts/upstream/test-upstream-automation.ps1` | Exercise ordering and state transitions in temporary Git repositories. |
| `scripts/automation/start-n8n.ps1` | Start a pinned local n8n with the required environment and node policy. |
| `automation/n8n/workflows/upstream-monitor.json` | Inactive workflow to import into n8n. |

Generated files live below `artifacts/upstream/` and are ignored by Git.

## Run without n8n

From the C# repository root:

```powershell
pwsh -NoProfile -File .\scripts\upstream\scan-upstream.ps1 -OutputFormat Text
pwsh -NoProfile -File .\scripts\upstream\prepare-next.ps1 -OutputFormat Text
```

The scanner may show open PRs even when `Pending merged commits` is zero. An open PR is not eligible for the port queue until its commit is reachable from Java `upstream/4.8`.

## First-time n8n setup

The launcher uses `npx`, so no global n8n installation is required. It pins n8n `2.26.8` and accepts Node.js `20.19` through `24.x`.

1. Start n8n from the C# repository root:

   ```powershell
   pwsh -NoProfile -File .\scripts\automation\start-n8n.ps1
   ```

2. Open `http://localhost:5678` and create the local owner account when prompted.
3. In the workflow menu, choose **Import from File** and select `automation/n8n/workflows/upstream-monitor.json`.
4. Run the workflow manually once. Inspect the final `Monitor result` node.
5. Activate the workflow only after that manual run succeeds. Its schedule is every six hours.

n8n data is stored at `%USERPROFILE%\.n8n-beyond-aion-sharp` by default. The launcher binds to `127.0.0.1`, disables diagnostics, sets both repository paths, and enables `Execute Command` while leaving `Local File Trigger` disabled.

The `Execute Command` node can run commands as the current Windows user. Keep this n8n instance local and do not expose port `5678` to the network. See the official [npm installation](https://docs.n8n.io/hosting/installation/npm/), [Execute Command](https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-base.executecommand/), and [workflow import](https://docs.n8n.io/workflows/export-import/) documentation.

## Per-commit lifecycle

### 1. Prepare

The scheduled workflow prepares only the first pending merged commit. To do it manually:

```powershell
pwsh -NoProfile -File .\scripts\upstream\prepare-next.ps1 -OutputFormat Text
```

The package contains `metadata.json`, `changed-files.json`, `commit.patch`, `diff.patch`, `prompt.md`, and `README.md`. Give `prompt.md` to Codex while the C# repository is the active workspace.

### 2. Implement

Port the behavior into established C# structures. Do not edit the Java checkout, include adjacent Java commits, create a branch, or commit yet. Add focused regression coverage.

### 3. Validate

```powershell
pwsh -NoProfile -File .\scripts\upstream\validate-port.ps1 `
  -UpstreamSha <40-character-java-sha> `
  -OutputFormat Text
```

For a focused test first:

```powershell
pwsh -NoProfile -File .\scripts\upstream\validate-port.ps1 `
  -UpstreamSha <sha> `
  -FocusedTestProject .\tests\Some.Tests\Some.Tests.csproj `
  -TestFilter 'FullyQualifiedName~RelevantTests' `
  -OutputFormat Text
```

The default validation sequence is `git diff --check`, restore, optional focused tests, the warning-baseline rebuild, all solution tests, and the structural fidelity check. The report fingerprints tracked and untracked worktree content so completion refuses changes made after validation.

### 4. Record the decision

Use one of `ported`, `direct-data`, `not-applicable`, or `blocked`:

```powershell
pwsh -NoProfile -File .\scripts\upstream\complete-port.ps1 `
  -UpstreamSha <sha> `
  -Status ported `
  -Notes 'Behavioral fix and focused regression coverage; full validation passed.' `
  -OutputFormat Text
```

`ported` and `direct-data` require a current passing validation report. `not-applicable` and `blocked` require evidence in `-Notes`. A blocked decision updates the ledger but does not advance `lastCompletedJavaCommit`.

### 5. Commit locally

Review `git status` and stage only the intended implementation, tests, `docs/upstream-port-log.md`, and `docs/upstream-port-state.json`. Then use the generated message:

```powershell
git commit -F "<package-path>\commit-message.txt"
```

This creates the required `Upstream-Java-SHA` and `Port-Status` trailers. Do not create another local branch or push the C# repository.

### 6. Verify

```powershell
pwsh -NoProfile -File .\scripts\upstream\verify-port.ps1 -OutputFormat Text
```

Verification requires a clean worktree, only the local `main` branch, an exact ledger row, a valid HEAD trailer pair, and one completed C# mapping for every Java commit through the state cursor. Earlier blocked records are allowed, but only one completed mapping may exist.

## Follow-up automation tasks

The next automation phase should be added only after this monitor runs reliably:

1. Choose a notification destination and add an n8n node for newly merged commits or changed PR watchlist state.
2. Choose the supported Codex or LLM execution surface and credential model. Insert it after package preparation, with the exact `prompt.md` as input.
3. Run agent edits in an isolated temporary worktree or require explicit human approval before the agent touches local `main`.
4. Keep deterministic validation outside the model and require a passing `validation.json` before tracker advancement.
5. Add retry limits and a durable single-item lock before allowing unattended execution.
6. Back up the n8n user folder and its generated encryption configuration before adding credentials.
