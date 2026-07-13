# Port Fidelity & Remediation Plan

Date: 2026-06-07 (original) · **Deep-analysis status update: 2026-06-17**
Status: AUTHORITATIVE. This is the current loop driver for all game-server porting work. It supersedes the Phase-6 unit-of-work approach for *how* work is done (the phase roadmap in `csharp-port.md` still describes the macro sequence).

---

## 0. STATUS UPDATE — 2026-06-17 (deep analysis; supersedes §5–§8 progress markers)

**Headline: Phases A, B, C are COMPLETE. Phase D (porting the missing surface) is structurally ~complete. The remaining frontier is RUNTIME-PARITY VALIDATION, not porting or de-slopping.**

Branch `feature/object-spine-bigbang` @ HEAD `aee65eae1` · build **0 errors** (all 4 projects + tests) · golden **196/196 byte-exact** vs the Java mvn oracle · full test suite **459/0** · bootstrap **9/9** · full DB-backed boot validated end-to-end.

### What changed since 2026-06-07 (measured)

| Metric | 2026-06-07 (plan origin) | 2026-06-17 | Note |
| --- | --- | --- | --- |
| Fidelity guardrail (`check_fidelity.py`) baseline | 363 banned-vocab slop files + 6 god-classes | **0 / 0** (`fidelity-baseline.json` = `[]` / `{}`) | the ratchet was driven all the way down |
| `GameServerConnection.cs` god-class | **22,907 lines** (≈200 fused packet handlers) | **gone** (GS class deleted; packets are 1 handler/class) | largest GS file now = faithful generated catalogs `SM_SYSTEM_MESSAGE.cs` / `DialogAction.cs`, which mirror equally-large Java files |
| Java types with exact-name C# counterpart | 56 (first audit) → 2265 | **2269 / 2456 (~92%)** | the rest are nested enums, faithful renames, and CM_/SM_ naming-normalization artifacts |
| Reworked duplicate `Sm*` packets | 142 (138 byte-identical twins of faithful `SM_*`) | **0** — `GameServerPacket`/`SerializeFrame` dual-serialization path **dropped**; one faithful `AionServerPacket` hierarchy | |
| Top slop explosion clusters (FindGroup 101, BindPoint 38, WorldNpc 37, PlayerKnown 24, PlayerProtection 42) | all present | **all retired/collapsed to faithful** | |
| Content scripts ported | partial | **quests 1035/1035 · AI 462 · instance 37/37 · zone 3/3 · chat done** | drove C# file count 2735 → 4124 |

### Phase-by-phase

- **Phase A (Foundation) — DONE.** Doctrine (A1), Java golden-capture harness (A2, now extended to formulas + live-object harness seams: Creature/Player/DataManager-holder/live-Npc/item-ItemInfoBlob), structural-audit tool (A3), and the guardrail ratchet (A4) all built. A4 baseline is now empty (zero tracked slop / zero god-classes) — the ratchet reached the floor.
- **Phase B (Audit) — DONE.** `Structural-Audit-Scorecard.md` regenerated 2026-06-17. The 2026-06-07 explosion/orphan clusters are gone; the current scorecard's "orphan" table is now dominated by **false positives** (faithfully-ported quest/AI/instance content under `game-server/data/handlers/`, which the audit tool does not index as Java) plus the LoginServer/ChatServer packet families. See the scorecard's interpretive header.
- **Phase C (Remediate / de-slop) — COMPLETE.** The whole object-spine big-bang landed: object store unified to a single faithful `World._allObjects` (`_objects` dual-store deleted); WorldNpc/Kisk/Rift/drop/DP/HP/combat slop webs retired; all 8 reworked StaticData `*Summary`/`*Table` holder projections + WorldMapSummary + NpcSpawnTable + FlightZone retired; Housing subsystem confirmed retired (faithful `House:VisibleObject`/`HousingService`/`SM_HOUSE_*` is the live path); 126→0 reworked duplicate packets; `GameServerConnection` god-class extracted. **3 latent runtime fidelity bugs were found and fixed during de-slop** (RiftManager instance fan-out, `SM_DIALOG_WINDOW` flat-write, abyss-points silent-no-send). DataManager hollow-holders all wired (NPC/ITEM/SKILL/SKILL_TREE/SPAWNS/etc. load real XML at boot). Both build-zero "real src fidelity bugs" resolved-or-understood.
- **Phase D (Resume porting) — STRUCTURALLY ~COMPLETE.** The §8 missing-surface backlog (model, controllers, skillengine, ai, questEngine, data/handlers content, absent services) is essentially ported: ~92% exact-name parity; pillars complete per memory (clientpacket 190 CM_*, iteminfo 20 blob-writers, WorldMap, team-events, AI-behavior layer, DAO, quest/AI/instance/zone content). Engine/service classes with NO same-named C# file, measured 2026-06-17 = **6**, and all 6 are false positives: `AIState`/`AISubState`/`AIEventType` (nested enums inside AI base files), `CronExpressionTransformer` (faithful-renamed to `CronExpressions`), and `FindGroupMutationPostTraceCaptureHooks` + `PetFeedUnusualStorageArtifactCapture` (stray slop-named files to spot-check/delete — NOT real Aion engine classes).

### What GENUINELY remains (all non-slop; mostly validation, some user-gated)

1. **Runtime-parity validation depth (the real frontier).** Structural 1:1 ≠ proven runtime parity. Per `parity-state-modeled-vs-live`, end-to-end *live* gameplay actually exercised is still well short of 100% — the engine pillars exist and are golden-validated where harnessable, but the full live loop is not yet client-proven.
   - **Golden coverage:** 196 cases byte-exact, **0 fidelity bugs across every probe**, but ~104 live-object `SM_*` packets remain un-golden'd because they need a heavier **integration harness** (live World/DB/connection/Player-graph). This is a bounded sub-project, ~1–2 packets/tick — diminishing returns given the 0-bug record, but it is the only path to maximal packet validation without a client.
   - **Front-A live-client enter-world test:** the one test that proves the real loop. **Environment-gated — needs the user's actual Aion 4.8 client.** The autonomous loop cannot perform it.
2. **Spot-clean 2 stray slop-named Java files** (`FindGroupMutationPostTraceCaptureHooks`, `PetFeedUnusualStorageArtifactCapture`) — confirm dead and delete; tiny.
3. **Audit-tool hygiene (optional):** teach `structural_audit.py`/`check_fidelity.py` to index `game-server/data/handlers/` so faithfully-ported content scripts stop showing as "orphan" / tripping the banned-vocab heuristic (the 6 false-positive guardrail flags on `OphidanBridgeInstance`/`TheImprisonedExecutor`/`NewResearchPlan`/etc.).

### Recommendation
The de-slop + structural-port program defined by this plan is **delivered**. Further autonomous looping yields only the diminishing-return golden long-tail (#1 golden) or re-derives "done." The high-value next step is the **user-driven live-client enter-world test (#1 Front-A)**; absent that, pause or green-light the integration-harness sub-project. The original §5–§8 phase text below is retained for history; this §0 is the current truth.

---

## 1. Mission (North Star)

Convert the legacy Java game server to C# **1:1**. The Java implementation is the **absolute source of truth** — it has worked in production for years. The C# port must reproduce Java behavior exactly, with **no invented abstractions** ("no slop").

The single rule that makes this concrete:

> **The C# structure mirrors the Java structure.** One Java file → one C# file. One Java class → one C# class. One Java method → one C# method. One Java packet → one C# handler. Nothing more.

This rule simultaneously resolves both architectural problems we have today (see §2).

---

## 2. Diagnosis: why this plan exists

A June 2026 parity re-evaluation found the port had drifted into two **opposite** anti-patterns that share one root cause — *the C# structure stopped mirroring the Java structure.*

**Anti-pattern A — plan-service sprawl (too many files).** Simple Java services were exploded into dozens of micro "services" with invented vocabulary (`Plan`, `Bridge`, `Adapter`, `Composition`, `Outcome`, `Integration`, `Owner`, `Policy`, `Executor`). Measured:

| Java source | Java size | C# result | Explosion |
| --- | --- | --- | --- |
| `services/teleport/BindPointTeleportService.java` | 1 file, 148 lines | 38 files, 4,921 lines, 38 test files | ~33× lines |
| `services/findgroup/` | 2 files, 453 lines | 101 files, 19,914 lines, 103 test files | ~44× lines |

Across `Aion.GameServer/Services`, **~249 of 733 files are non-live `*PlanService`**.

**Anti-pattern B — the god-class (too few files).** `GameServerConnection.cs` is **22,907 lines**, fusing packet handlers that Java keeps as ~200 individual `CM_*`/`SM_*` classes.

**The crucial nuance:** the *behavior* in the existing work is largely **faithful** — formulas are traced to Java, tests assert Java-derived values, files carry `Java parity:`/`JavaSource` breadcrumbs. The problem is **structure, not logic**. That is why remediation is "re-port fresh from the Java" (cheap, because the logic is simple and already understood) rather than "redesign behavior."

**Why the rules didn't prevent this:** `parity-verification.md` *already* states the correct doctrine ("planner-only tests … do not move the C# server toward replacement … treat such artifacts as scaffolding only, not Java parity completion"). The doctrine was right; **adherence and enforcement failed.** This plan therefore adds *mechanical guardrails* (§5), not just principles.

---

## 3. The Fidelity Doctrine (hard constraints)

Every unit of work must satisfy all of these. A reviewer/agent may reject work that violates any.

1. **File mapping.** One Java file → one C# file. Mirror the Java package tree as the C# folder/namespace tree (`services/teleport/BindPointTeleportService.java` → `Services/Teleport/BindPointTeleportService.cs`).
2. **Member mapping.** Java class → C# class; Java method → C# method, same names (idiomatic casing), same order, same guard order, same early-returns.
3. **No abstraction without a Java counterpart.** If the Java has no `Plan`/`Bridge`/`Adapter`/`Composition`/`Outcome`/`Integration`/`Owner`/`Policy`/`Executor`/`Projection`/`Snapshot`/`Preview`/`Fact` type, the C# must not introduce one. A private method in Java stays a private method in C# — it does **not** become its own service+test.
4. **Packets.** One C# handler/writer class per Java `CM_*`/`SM_*` class, under a namespace mirroring Java's network tree. **No packet-handling logic in `GameServerConnection.cs`.**
5. **Minimal idiom translation only.** `ThreadPoolManager.schedule` → the C# scheduler equivalent; Java statics → C# statics; `Map` → `Dictionary`. Translate the mechanism, never restructure the design.
6. **Breadcrumbs.** Keep a short `// Java parity: path::method` comment on each ported method.
7. **Done means live-or-deferred, never planner-only.** A unit is "done" only if it is wired into live runtime behavior, **or** it is explicitly marked deferred with a Java-referenced reason and a tracked follow-up. "Modeled + tested" is scaffolding, not completion.
8. **Conflict resolution = Java wins (user, 2026-06-07).** When a faithful new port conflicts with already-existing C# code (a type/shape built earlier for the packet path that diverges from the Java design), **default to 1:1 Java parity and replace/fix the existing code** — prior work may be inaccurate or wrongly stubbed and does not win by seniority. **Exception:** when the conflict is a genuine *C#-vs-Java foundational language difference* (something one language can express idiomatically and the other cannot), choose the best path forward that stays *as close to 1:1 as possible*. (First application: the `WorldPosition` `readonly record struct` → faithful Java mutable class, big-bang replace + fix all 64 consumers; struct-vs-class is a C# idiom choice, so pick the class = closest to Java.)

---

## 4. Validation strategy (Java as oracle, no live client)

We cannot use a live game client until the port is complete, so we validate against the **Java implementation directly**. Two mechanisms, layered by where they fit (decision: golden for packets+formulas, audit for glue):

**4.1 Golden / differential tests (gold standard — packets & pure formulas).**
- The Java server compiles and runs. `SM_*.writeImpl(ByteBuffer)` emits exact bytes; formula methods are pure functions.
- Build a Java capture harness (§5, A2) that, for chosen inputs, dumps **real Java output** (packet byte arrays, formula return values) to fixture files.
- The C# test asserts its output **equals the captured Java bytes/values** byte-for-byte. This converts "author's reading of Java" into "provably equals Java."

**4.2 Structural fidelity audit (mechanical — every unit).**
- A tool (§5, A3) maps every Java file/class/method to its C# counterpart and reports: faithful, orphan (C# with no Java parent = slop), or gap (Java with no C# child = missing).

**4.3 Glue / control-flow (audit + Java-reading).**
- For logic that's hard to harness (handler orchestration, scheduling order), require the C# to cite the Java method and reproduce its statement/guard order; reviewer checks against Java line-by-line.

**Definition of "validated parity"** (binding, per `parity-verification.md`): live runtime behavior that is golden-matched or line-by-line audited against Java. Readiness reports, previews, and planner-only tests are **not** parity.

---

## 5. Phase A — Foundation (one-time, do first)

- **A1. Doctrine.** This document. **DONE.**

- **A2. Java golden-capture harness.** **DONE (proven end-to-end).** The Java half is
  `game-server/test/com/aionemu/gameserver/network/aion/serverpackets/GoldenPacketFixtureGeneratorTest.java`:
  it runs real packets through `writeImpl` (reflectively, to reach the protected method generically)
  and writes shared fixtures to `parity-artifacts/golden/packets/*.json` (inputs + Java-emitted `payloadHex`).
  The C# half is `dotnetConversion/tests/Aion.GameServer.Tests/GoldenPacketFixtureTests.cs`:
  it reads the same fixtures, reconstructs each packet from the recorded inputs, serializes, and asserts
  byte-for-byte equality. The Java bytes are the single source of truth.

  Commands:
  ```
  # 1. (Re)generate fixtures from real Java:
  mvn -pl game-server -am test -Dtest=GoldenPacketFixtureGeneratorTest \
      -Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false
  # 2. Assert C# matches:
  dotnet test dotnetConversion/tests/Aion.GameServer.Tests/Aion.GameServer.Tests.csproj \
      --filter "FullyQualifiedName~GoldenPacketFixtureTests"
  ```
  Note: the Java build sets `maven.test.skip=true` by default; the two `-D` flags above are required to
  compile/run Java tests. Add a packet by capturing it in the generator, then adding a reconstruction case
  in the C# test's `Reconstruct` switch. Only deterministic, constructor-driven packets belong here;
  packets whose `writeImpl` reads singletons/time (e.g. `SM_VERSION_CHECK` compatible path) need a
  deterministic config harness first (later task). Formula golden capture (e.g. `StatFunctions`) is the
  next extension of this harness and is not yet built.

- **A3. Structural-audit tool.** **DONE.** `scripts/parity/structural_audit.py` walks both trees and emits
  the Phase B backlog scorecard to `docs/Structural-Audit-Scorecard.md`:
  ```
  python scripts/parity/structural_audit.py --out docs/Structural-Audit-Scorecard.md
  ```
  First run (2026-06-07): 2,456 Java types vs 1,316 C# files; only **56** Java classes have an exact-name
  C# counterpart. Top explosion clusters: `FindGroup` (101 files / 44×), `GameServer` god-class stem
  (24,236 lines / 66×), `BindPoint` (38 / 16×), `NpcDialog` (29×), `ItemPurification` (14×). Heuristic
  caveat: the orphan table over-reports because of `CM_`→`Cm`/`SM_`→`Sm` naming conventions — use the
  explosion-cluster table (with Java line counts) as the authoritative remediation ranking.

- **A4. Guardrails (enforcement).** **DONE.** `scripts/parity/check_fidelity.py` mechanically enforces two
  structural rules as a **ratchet** (the whole codebase currently violates them, so a committed baseline freezes
  today's debt and the check fails only on NEW debt or growth):
  1. **No invented abstraction** — a C# file whose name contains a banned token (§3.3) with no Java class of the
     same (naming-normalized) name is a violation.
  2. **No god-classes** — a C# source file over 3,000 lines must not grow beyond its baseline; no new file may
     cross the threshold.
  ```
  python scripts/parity/check_fidelity.py                  # check (exit 1 on new violations)
  python scripts/parity/check_fidelity.py --update-baseline    # ratchet the floor DOWN after deleting slop
  ```
  Baseline: `scripts/parity/fidelity-baseline.json` (363 known slop files, 6 god-classes at 2026-06-07). Wired into
  CI as the `fidelity` job in `.github/workflows/run-tests.yml`. **Rule: only run `--update-baseline` after reducing
  debt; never hand-add entries to silence a new violation.** Remediation (Phase C) deletes slop, then ratchets.

  Still open in the foundation: **formula golden capture** (extend the A2 harness to pure `StatFunctions`-style
  methods).

---

## 6. Phase B — Audit existing work (validation)

Run A3 + A4 across the whole game-server. Produce a per-Java-file **backlog scorecard** classifying every area as:
- **faithful** — keep as-is (spot-check with golden tests).
- **slop-cluster** — over-decomposed; queue for re-port (§7).
- **god-fragment** — logic living in `GameServerConnection.cs`; queue for extraction (§7).
- **missing** — no C# counterpart; queue for porting (§8).

This scorecard *is* the loop backlog. Seed it in dependency order; the worst known clusters today are: `findgroup` (101 files), `PlayerProtection` (42), `WorldNpc` (37), `BindPoint` (38), `PlayerKnown` (24), plus `GameServerConnection.cs` (god-class).

---

## 7. Phase C — Remediate (re-port fresh; the loop)

### Remediation order: substrate-first, NOT slop-size-first

Finding (2026-06-07, from attempting BindPointTeleport): the largest slop clusters cannot be remediated in isolation, because **the slop exists to fake unported runtime substrate.** BindPointTeleport (38 files) depends on teleport, the player-controller channeling-task registry (`addTask/hasTask/cancelTask(TaskId)`), and a hotspot dataholder — all unported (every teleport `CM_` handler is a deferred no-op; there is no `TeleportService`; hotspot data lives only inside the slop cluster). Re-porting it faithfully now would force inventing the banned abstractions.

Therefore the `Structural-Audit-Scorecard.md` is a **magnitude map, not a work order.** Remediate in Java dependency order:

- **Track 1 — substrate (unblocks the big clusters):** port the missing runtime layer the slop fakes — `model` gaps, the `CreatureController` task registry (`TaskId` + task map), `TeleportService` + hotspot dataholder, KnownList, scheduler ownership. Once a substrate lands, the clusters that faked it (BindPointTeleport, etc.) collapse to faithful ~150-line services naturally.
- **Track 2 — substrate-free slop (safe now):** collapse clusters that are pure computation/static-data with an existing Java source and no runtime dependency — e.g. `StatFunctions`-derived combat/reward formula services and enum lookups. These are immediately golden-validatable with the formula harness and remove real slop without waiting on substrate.

Do Track 2 units to prove the loop and trim debt; do Track 1 to actually unblock the big clusters. Before selecting a cluster, check whether its substrate exists; if not, port the substrate first or pick a Track 2 unit.

### Definition of "foundation" + the no-defer rule

**Foundation = units with zero dependencies and zero fakery** — true leaves you can port faithfully right now without stubbing anything. Build leaves first, then the layer above them, and so on upward.

When a unit hits a missing dependency, **do not defer or fake it — recurse and build the real dependency first**, then return (see the hard rule in `parity-verification.md`). Deferral is how the slop was born; a "wire later / TODO / placeholder / Plan-layer" is not allowed as a way to keep the current piece moving. The only acceptable omission is an *upward* behavior of a leaf that has no caller and genuinely belongs to a higher unbuilt layer — recorded as a concrete backlog unit, not a floating TODO (e.g. `AionObject`'s GC objectId auto-release, which belongs to the respawn/id-release layer).

### The loop

For each backlog item (one Java file or one tight Java cluster per iteration):

1. **Select** the next Java file (smallest, in dependency order).
2. **Read the Java source fully** — it is the spec.
3. **Harvest** expected values from the existing C# tests/plan-services (they encode Java-derived values) and, where possible, regenerate them from the A2 harness as golden fixtures.
4. **Write ONE C# file** mirroring the Java class 1:1 (Doctrine §3).
5. **Write golden/diff tests** anchored to Java output (§4).
6. **Delete the slop cluster** the new file replaces (and its now-redundant tests).
7. **Build + test green**, structural-audit clean. **Commit** with a `[Fidelity]` tag and the Java path.

**God-class extraction** uses the same loop, one packet at a time: move one `CM_*`/`SM_*`'s logic out of `GameServerConnection.cs` into its own handler class mirroring the Java packet class, leaving only thin dispatch behind. Stop when `GameServerConnection.cs` is dispatch-only.

---

## 8. Phase D — Resume porting (only after C is underway)

Port the still-missing surface in Java **dependency order**, each unit through the §7 loop and §4 validation:

1. `model` (801→89) — the data backbone that blocks everything.
2. `controllers` + runtime (KnownList, scheduler, world tick) — live creature behavior.
3. `skillengine` (292→0) — effects/skills → live combat.
4. `ai` (39 + scripts).
5. `questEngine` + quest scripts.
6. `data/handlers` content (instances, commands).
7. Remaining absent services: `siege`, `panesterra`, `worldraid`, `transfers`, `conquerorAndProtectorSystem`.

---

## 9. Agent loop protocol (the handoff)

An agent running this plan repeats:

```
1. Read this doc + the backlog scorecard (§6).
2. Pick the single next unit (one Java file). Prefer Phase C over D until C is clear.
3. Apply the §7 loop. Honor the Doctrine (§3) and Validation (§4) exactly.
4. Definition of done (ALL required):
   - C# mirrors Java 1:1 (file/class/method).
   - Golden or line-audited against Java; no planner-only "done".
   - Slop it replaces is deleted; structural audit clean for the area.
   - Build green; tests green; committed with [Fidelity] + Java path.
5. If a unit needs an unported dependency, record the dependency and pick the dependency instead (don't stub with a new abstraction).
6. Stop / escalate if: Java behavior is ambiguous, a unit can't be golden-validated, or the doctrine would have to be broken to proceed.
```

Commit message format: `[Fidelity] <Java path::class> — re-port 1:1 (was <N> slop files)`.

---

## 10. Definition of project done

Every file under `game-server/src/com/aionemu/gameserver` (and the `data/handlers` content) has **exactly one faithful C# counterpart**, golden-validated or explicitly deferred with a Java-referenced reason — and the C# server reproduces Java behavior with no abstraction that Java does not have.

---

## 11. Related docs (keepers)

- `csharp-port.md` — START HERE: orientation, roadmap, the canonical doc list, hard contracts.
- `orchestration-rules.md` — how to run a session: the loop, gates, validation, documentation discipline.
- `parity-verification.md` — the two gates (Fidelity + Live) and what counts as validated parity.
- `HANDOFF.md` — the single rolling state doc, updated in place every Unit of Work (replaces the retired per-session handoff files).
- `Structural-Audit-Scorecard.md` — the live remediation backlog (regenerated by `scripts/parity/structural_audit.py`).
- `docs/discovery/game-server-services/Completion-Estimate.md` — full-surface parity picture and the modeled-vs-live framing.
- `PHASE-*-COMPLETION.md` / `PHASE-*-PROGRESS.md` — frozen record of work completed (history only).
