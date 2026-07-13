# Parity Verification

Java→C# 1:1 parity. The Java implementation is the source of truth. A Unit of Work is done only when it passes the gates below with objective, Java-anchored evidence. Code that compiles, has matching names, or has passing tests of its own model is **not** parity.

Two failure modes this project has actually hit, both of which the gates exist to stop:
- **Structural slop** — one Java service exploded into dozens of invented C# micro-services (e.g. a 148-line Java service → 38 C# files), or packet logic fused into a 22,907-line god-class. *Behavior was often faithful, but the structure diverged wildly from Java.*
- **Scaffolding-as-progress** — preview/planner/metadata/evidence layers that describe Java behavior without being wired into live runtime.

Both grew from the same habit: **deferring/faking a missing dependency** so the current piece could "progress." The rule below exists to kill that habit at the source.

## Hard rule — no fakery, no deferral: build the dependency first

When a unit needs something that does not exist yet (a class, a service, a data holder, a runtime path), you must **stop and port that real dependency first**, then return to the original unit. Do **not**:

- stub it, fake it, or return placeholder values;
- introduce a "Plan/Adapter/Preview/Bridge…" layer to stand in for it;
- leave a "deferred / wire later / TODO" that lets the current code proceed without it.

A deferral is only acceptable for a behavior that (a) has **no caller**, and (b) genuinely belongs to a **higher, not-yet-built layer** — i.e. you are omitting an upward feature, not faking a downward dependency. Even then: record it as a concrete backlog unit (with its real prerequisite), not a floating TODO. When in doubt, build the dependency.

This means dependency work recurses **downward**: hitting a missing piece is not a blocker to route around — it is the next unit. Finish the leaf, then unwind back up.

## Gate 1 — Fidelity Gate (every Unit of Work)

The C# must mirror the Java structure. Answer before committing:

1. **1:1 shape?** One Java file → one C# file; class→class, method→method, same names (idiomatic casing), same guard/early-return order.
2. **No invented abstraction?** No type/layer that has no Java counterpart. A Java private method stays a private method — it does not become its own service+test. Banned vocabulary unless Java has it: `Plan`, `Bridge`, `Adapter`, `Composition`, `Outcome`, `Integration`, `Owner`, `Policy`, `Executor`, `Projection`, `Snapshot`, `Preview`, `Fact`.
3. **Packets in their own classes?** One C# class per Java `CM_*`/`SM_*`; no packet logic added to `GameServerConnection.cs`.
4. **Net structure reduced or matched?** Remediation deletes the slop it replaces. The unit must not increase divergence from Java.
5. **Breadcrumb present?** `// Java parity: path::method` on ported methods.

Fail any → the work is not acceptable, regardless of behavior.

## Gate 2 — Live Gate (new porting work; Phase D)

New behavior must move live runtime parity forward, not add scaffolding. A unit passes if it does at least one of:

- wires a deferred client/server packet path, or sends a real server packet from live code;
- mutates live player / quest / inventory / world / group / alliance / combat / skill / loot / NPC state;
- persists or restores runtime state through the existing DB shape;
- loads Java XML/static data into a runtime C# structure used by live code;
- executes a quest / AI / zone / instance / command / dynamic-handler path;
- performs a Java/C# golden or runtime comparison that directly unblocks one of the above.

It does NOT pass if the best description is "add/harden a preview, planner, metadata, evidence, readiness, or adapter layer," or "tests for a dry-run model."

**Remediation note (Phase C):** re-porting an existing slop cluster to one faithful, golden-validated C# file is valid progress under the Fidelity Gate even if liveness is unchanged — it is the user's current priority. It must still be golden/audited against Java.

## Validated parity = Java-anchored evidence

Use the strongest available. Ranked:

- **Golden match (preferred for packets/formulas).** C# output equals real Java output captured by the harness (`parity-artifacts/golden/…`), byte-for-byte / value-for-value. This is the gold standard and needs no live client.
- **Runtime comparison.** Same inputs run against Java and C#, outputs compared deterministically.
- **Line-by-line audit.** For glue/control flow that can't be harnessed: C# reproduces the Java method's statements and guard order; reviewer checks against Java. Acceptable for trivial deterministic logic only.
- **Not evidence:** compiles, names match, structure looks similar, tests of a preview/planner model pass.

## Parity status (use in HANDOFF.md, conservatively)

- **Unknown** — not analyzed.
- **Needs Verification** — ported/partial but not compared to Java.
- **Partial Parity** — some behavior matches; known gaps remain (list them).
- **Verified Parity** — golden-matched, runtime-compared, or line-audited against Java.
- **Intentional Difference** — documented divergence (state the exact difference and why).

When uncertain, choose the weaker status. "Done"/"looks good"/"should match" are not statuses.

## Common parity pitfalls (check these where relevant)

Java is the oracle for all of them:
- Numeric: `BigDecimal`→`decimal` (not `double`) for currency; document any float tolerance.
- Date/time: Java `LocalDate`/`Instant`/`Calendar`/`ZonedDateTime` do not map to one C# type; confirm UTC/timezone behavior.
- Strings: case sensitivity and culture — Java string ops vs .NET culture-sensitive defaults.
- Collections: don't assume `HashMap`/`HashSet` ordering unless Java relies on it.
- Null/defaults: Java primitives can't be null; empty-vs-null; missing-field behavior.
- Exceptions: don't silently replace a Java throw with a default/null.
- Serialization/packets: field order, encoding, fixed-length string padding (e.g. `writeS(s, n)` = `(n+1)*2` bytes).
- Threading: Java `synchronized`/`volatile`/futures → C# `lock`/`Task`; don't introduce async races.

## Final principle

Code completion is not parity completion. Mirror Java's structure, prove behavior against Java, keep prose minimal. If several consecutive commits do not re-port, wire, mutate, persist, load, dispatch, or golden-compare against Java — stop and re-plan from the scorecard.
