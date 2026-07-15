# n8n Docker scheduler

This directory contains the local Docker image, Compose service, and inactive n8n workflow for Java-to-C# upstream porting.

n8n only schedules repository commands. `scripts/upstream/run-next-port.ps1` selects the first merged Java commit, generates the versioned prompt, invokes Codex CLI, and accepts only one locally committed and independently verified C# result.

Quick start from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\automation\start-n8n.ps1
pwsh -NoProfile -File .\scripts\automation\login-codex.ps1
pwsh -NoProfile -File .\scripts\automation\import-workflow.ps1
```

Create the local n8n owner, run the imported workflow manually, and publish it only after the repository safety checks pass. The full setup, state model, logs, and recovery procedure are in `docs/automation/n8n-upstream-port-workflow.md`.

Do not expose this n8n service to a network. Its Execute Command node and bind mounts grant workflows access to both local repositories.
