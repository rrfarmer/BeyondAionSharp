# Orchestration Rules

How to run one session of the Java→C# parity migration. You are the Orchestrator. The Java implementation is the source of truth. Read this with `csharp-port.md`, `Port-Fidelity-Remediation-Plan.md`, `parity-verification.md`, and `HANDOFF.md`.

## Startup

1. Read the canonical docs listed in `csharp-port.md` (including `HANDOFF.md` for current state).
2. State a one-paragraph plan: current position, the next single Unit of Work, the Java + C# files involved, known risks.
3. Do not run broad builds/tests at startup. Reading docs is not a validation trigger.

## The Unit of Work loop

Work in small units, one Java file (or one tight Java cluster) at a time. Repeat until blocked, context-limited, or no safe unit remains.

1. **Select** the next unit (smallest, in dependency order). For remediation, take it from the top of `Structural-Audit-Scorecard.md`.
2. **Pass the gates** (see `parity-verification.md`): the **Fidelity Gate** (always) and, for new porting work, the **Live Gate**. If a unit can pass neither, do not do it.
3. **Read the Java source fully** — it is the spec. Identify its real dependencies up front.
4. **If a dependency is missing, recurse — do not defer or fake.** Per the hard rule in `parity-verification.md`, stop and port the missing real dependency first (it becomes the next unit), then return. Never stub/placeholder/Plan-layer/TODO your way past it. Build the deepest leaf first, then unwind.
5. **Write/replace** C# that mirrors Java 1:1 (one file, class→class, method→method; no invented abstraction; no packet logic added to `GameServerConnection.cs`). For remediation, delete the slop cluster the new file replaces.
6. **Validate against Java** — golden fixtures for packets/formulas, or line-by-line audit for glue (see Validation below).
7. **Update `HANDOFF.md`** in place and, if structure changed, regenerate the scorecard.
8. **Commit** the unit (code + HANDOFF together) using the repo's configured author. Commit as you go — one commit per completed unit, never let work pile up uncommitted.

## Validation

Default to focused, Java-anchored evidence. Never use a broad run as a heartbeat.

- **Packets / pure formulas → golden pipeline** (the strongest evidence; this is "Java as oracle"):
  ```
  git -C ../aion-server clean -nd
  mvn -f ../aion-server/pom.xml -pl game-server -am test -Dtest=GoldenPacketFixtureGeneratorTest -Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false
  dotnet test tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj --filter "FullyQualifiedName~GoldenPacketFixtureTests"
  ```
  Add the packet/formula to the Java generator and the C# consumer; assert byte/value equality.
- **Other C# units → filtered tests** for the edited class and the closest contract class:
  ```
  dotnet test tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj --filter "FullyQualifiedName~EditedClassTests"
  ```
  A passing filtered `dotnet test` IS the compile signal — do not follow it with a full solution build.
- **Java parity check** → `mvn -pl game-server -am test -Dtest=SpecificJavaTest -Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false` when a narrow Java test exists.
- **Docs-only** → `git diff --check`.
- **Fidelity guardrail** (run before every commit that adds/renames C# files): `python scripts/parity/check_fidelity.py`. It fails on new invented-abstraction files or god-class growth. When a unit *deletes* slop, run `python scripts/parity/check_fidelity.py --update-baseline` to ratchet the floor down and commit the updated `fidelity-baseline.json`. Never hand-edit the baseline to silence a violation — fix the structure instead. (CI also runs this as the `fidelity` job.)
- **Broad run** (`dotnet test` of a whole project/solution, or `dotnet build` the solution) is allowed ONLY when named in `HANDOFF.md` as triggered by: a shared-infrastructure/packet-primitive/crypto/scheduler/persistence change, focused evidence of wider risk, an explicit user request, or a readiness checkpoint. Otherwise narrow the filter; document residual risk.

Note: the Java build sets `maven.test.skip=true`; the `-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false` flags are required to run any Java test. `python` (not `python3`) on this machine.

## Documentation discipline (anti-drift, anti-bloat)

This is a hard rule. Sessions previously generated ~3,500 throwaway docs; that is retired.

- **Do not create new doc files** — no per-UOW design/plan/audit/handoff/readiness/evidence notes.
- The **only** mutable docs are `HANDOFF.md` (every unit, in place) and `Structural-Audit-Scorecard.md` (regenerate when structure changes).
- Record code-level parity with `// Java parity: path::method` breadcrumbs, not prose docs.
- Do not edit `PHASE-*` history docs.
- If you feel the urge to write a design doc, put the 2–3 lines that matter in `HANDOFF.md` instead.

## HANDOFF.md (the cross-session memory)

Keep it short and current. After each unit, overwrite the relevant sections so it always reflects *now* (git history holds the past). It must be useful with zero prior conversation. Required sections:

- **Current position** — phase, what's done, what's in flight.
- **Last unit** — Java file ported, C# file(s) written, slop files deleted, validation run + result, commit hash.
- **Next unit** — the single next Java file/cluster, its gate answers, the exact validation command, expected Java/Maven need (`none` if C#-only).
- **Blockers / risks** — honest, specific.

## Commits

- **Commit as you go** — one commit per completed unit, code + `HANDOFF.md` together. Never leave finished work uncommitted across units.
- Use the repo's configured git author (`rrfarmer <ryanfarmer@mac.com>`). Do **not** set an AI/"claude" author and do **not** add an AI co-author trailer.
- Format: `[Fidelity] <Java path::class> — <action>` (e.g. `[Fidelity] services/teleport/BindPointTeleportService — re-port 1:1, delete 38 slop files`).
- Do not commit broken builds except to explicitly record a documented blocked state.

## Stopping

1. Finish the current safe unit (don't leave risky partial work).
2. Commit.
3. Update `HANDOFF.md` so the next session can start cold.
4. Document blockers honestly.

## Anti-patterns (stop if you catch yourself doing these)

- Adding any abstraction Java does not have (`Plan`/`Bridge`/`Adapter`/`Composition`/`Outcome`/`Owner`/`Policy`/`Executor`/`Projection`/`Preview`/`Fact`…) — this is the slop we are removing.
- Splitting one Java method/class into multiple C# services; or growing `GameServerConnection.cs`.
- Planner-only / preview-only / metadata-only / evidence-only / readiness-only "progress."
- Claiming parity without Java-anchored evidence; treating tests of dry-run models as parity.
- Creating new doc files; letting `HANDOFF.md` go vague.
- Broad builds/tests as reassurance; large uncontrolled rewrites; repo-wide reformatting.
- Following a stale handoff suggestion that points at scaffolding — re-plan from Java + the scorecard.

## Final principle

Mirror Java exactly, validate against Java, write almost no prose. Accuracy beats optimism; the Java source wins.
