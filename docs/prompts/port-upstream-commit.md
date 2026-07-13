# Single upstream commit prompt

```text
Port exactly one upstream Beyond Aion Java commit into BeyondAionSharp.

UPSTREAM COMMIT: {{UPSTREAM_SHA}}
UPSTREAM PATCH:
{{UPSTREAM_PATCH}}

Repository rules:
- Java is the behavioral reference; C# lives at the repository root.
- Do not merge, cherry-pick, or mechanically transliterate Java.
- Do not include adjacent upstream commits, cleanup, redesign, or unrelated refactors.
- First explain the bug/behavioral change and map each affected Java file to existing C# code.
- Preserve null behavior, collection ordering, enum ordinal semantics, numeric behavior, timing, packet layouts, persistence side effects, and data/config compatibility.
- Reuse established C# infrastructure. Add an abstraction only when the existing codebase already requires that pattern.
- For XML, SQL, or configuration changes, carry language-neutral data directly only after verifying the C# loaders/models support it.
- Add focused regression coverage. Run focused tests and any broader test justified by the blast radius.
- If the commit is not applicable, make no code changes and provide a precise evidence-based reason.
- If a prerequisite is missing, stop and mark the commit Blocked; do not port later commits.

Required result:
1. Behavioral summary.
2. Java-to-C# file mapping.
3. Files changed and why.
4. Tests run with results.
5. Residual risks or an explicit Not applicable / Blocked decision.
6. A commit message containing:
   Upstream-Java-SHA: {{UPSTREAM_SHA}}
   Port-Status: ported|direct-data|not-applicable|blocked
```

