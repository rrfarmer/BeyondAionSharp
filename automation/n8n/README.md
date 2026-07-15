# n8n workflow artifact

`workflows/upstream-monitor.json` is an importable, inactive n8n workflow for the Windows host setup documented in `docs/automation/n8n-upstream-port-workflow.md`.

It runs the repository-owned scanner, records open pull requests separately from merged commits, and prepares a prompt package for only the first pending merged commit. It does not invoke an LLM, edit C# code, update the tracker, stage files, or create commits.

The workflow requires the `Execute Command` node and the two environment variables set by `scripts/automation/start-n8n.ps1`. Do not expose this n8n instance to a network: enabling `Execute Command` gives workflows the same operating-system access as the n8n process.
