# Single upstream commit prompt

Prompt version: 2

Port exactly one merged Beyond Aion Java commit into BeyondAionSharp and finish the reviewed local lifecycle.

## Upstream commit

`{{UPSTREAM_SHA}}`

## Upstream patch

```diff
{{UPSTREAM_PATCH}}
```

## Repository rules

- Treat Java as the behavioral reference and keep the Java checkout read-only.
- Work directly on the existing local C# `main`. Do not create a branch, remote, pull request, merge, or cherry-pick.
- Port only this Java commit. Do not include adjacent commits, cleanup, redesign, or unrelated refactors.
- First explain the bug or behavioral change and map every affected Java file, configuration key, SQL object, and data file to existing C# code.
- Preserve null behavior, collection ordering, enum and ordinal semantics, numeric behavior, timing, packet layouts, persistence effects, and data compatibility.
- Reuse established C# infrastructure. Add an abstraction only where the existing codebase warrants it.
- Carry language-neutral XML, SQL, or configuration directly only after verifying C# loaders and models support it.
- Add focused regression coverage that fails without the fix. Run focused tests and all broader validation justified by the blast radius.
- If the commit is not applicable, make no product-code changes and provide precise evidence.
- If a prerequisite is missing, record the commit as blocked and stop. Do not port a later Java commit.

## Required lifecycle

1. Implement and review the single-commit port.
2. Run `scripts/upstream/validate-port.ps1` for `{{UPSTREAM_SHA}}` when the status is `ported` or `direct-data`.
3. Run `scripts/upstream/complete-port.ps1` with `ported`, `direct-data`, `not-applicable`, or `blocked` and concise evidence in `-Notes`.
4. Review `git status`, stage only intended implementation and tracker files, and commit locally with the generated `commit-message.txt`.
5. Run `scripts/upstream/verify-port.ps1`. Leave the repository clean on `main` with no other local branches.

## Required report

1. Behavioral summary.
2. Java-to-C# file mapping.
3. Files changed and why.
4. Tests and validation commands with results.
5. Residual risks or the evidence for `not-applicable` or `blocked`.
6. Local C# commit SHA and the exact upstream Java SHA.
