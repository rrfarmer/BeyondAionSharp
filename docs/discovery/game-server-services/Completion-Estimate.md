# Game-Server Completion Estimate

Date: 2026-06-07 — **with a 2026-06-14 current-state update prepended below.**
Supersedes: the 2026-05-29 revision (which is preserved only in git history).

---

## 2026-06-14 UPDATE — the engine surface is now ported; the gap moved to content + go-live

The body of this doc (below the line) was written 2026-06-07 and is now **substantially out of date**. Between then and now the compile-convergence + concurrent porting effort changed the picture dramatically. Ground-truth counts as of 2026-06-14 (branch `feature/object-spine-bigbang`, HEAD ~`50b8bc0fd`):

| Area | Jun 7 | **Jun 14** | Java | Notes |
| --- | ---: | ---: | ---: | --- |
| Build (all 4 projects + tests) | green on `main`, branch RED-by-design | **0 errors, green** | — | the object-spine big-bang converged; ~5,710 compile errors → 0 |
| Fidelity guardrail (slop / god-class) | 363 / 6 | **0 / 0** | — | `check_fidelity.py` baseline empty |
| `services` files / `*PlanService` | 733 / 249 | **278 / 0** | 169 | plan-service sprawl eliminated |
| `GameServerConnection.cs` god-class | 22,907 lines | **deleted** | — | packet logic moved to per-`CM_*`/`SM_*` classes |
| `model` | 89 (~11%) | **867** | 801 | faithful (844/867 carry `// Java parity:`); some Java classes split into `.PartN.cs` |
| `controllers` | 2 | **61** | 61 | faithful, breadcrumbs |
| `skillengine` | 0 | **292** | 292 | faithful (mirrors Java's own empty-`applyEffect` stubs; 0 `NotImplementedException`) |
| `questEngine` (engine, not scripts) | ~0 | **79** | 79 | faithful |
| `ai` (engine, not scripts) | 0 | **46** | 39 | faithful |
| `dataholders` | 64 | **152** | 100 | partial; many holders deferred-empty (runtime XML load not wired) |
| `network` CM_* / SM_* | 186 / 191 | **188 / 240** | 253 / 268 | |
| **`data/handlers` content scripts** | ~11 | **~84** | **1,732** | **the dominant remaining gap (~5%)** |

**Golden validation (new, Java-oracle byte/value diff harness):** 88 cases, **0 fidelity bugs** — covering the enter-world login flow, both crown-jewel packets (`SM_PLAYER_INFO`, `SM_STATS_INFO` via an integration harness), combat/skill/item packets, and the `StatFunctions` combat-damage math. Every C# writer/formula matches real Java exactly.

### Revised honest answer to "how far"
- **Structural / engine parity: essentially complete and faithful.** The pillars this doc (below) flagged as "absent" — skillengine, controllers, model, questEngine, ai-engine — are now ported 1:1 at file parity with breadcrumbs. The named-slop ("modeled-vs-live plan-service") problem is **resolved**.
- **Playable / live parity: still the gap, now concentrated in two places:**
  1. **Content handlers (~1,648 unported):** 509 AI scripts, ~1,100 quest scripts, instance handlers, ~138 admin/console commands. This is the largest remaining body and is what makes NPCs/quests/instances actually behave.
  2. **Go-live wiring:** runtime data loading is deferred (DataManager holders are empty-default placeholders; per-file faithful XML loaders not wired), and there has been **no real-client/integration validation** (needs live DB + network + client).
- **"Modeled vs live" still applies** to the engine: the code is faithfully ported and compiles, but most of it has not been exercised at runtime. Golden proves the *serialization/formula* surface; it does not prove the effect/AI/quest *runtime behavior*.

### What's needed next (dependency order) — supersedes the "What's Left" list below
1. **Wire runtime data loading** (faithful per-file XML loaders → populate the deferred DataManager holders) — prerequisite for the server actually running and for runtime/integration validation.
2. **Content handlers** — port the `data/handlers` body (quests → AI → instances → admin/console commands); largest remaining surface.
3. **Real-client / integration validation** — stand the three processes up against a client; promote "modeled" engine code to "runtime-proven."
4. Opportunistic: extend golden to remaining packets/formulas as regression coverage (lower priority — 0 bugs across 88 cases so far).

**Stop doing:** treating more `SM_*` packet golden fixtures as the primary work. The protocol is well-validated; the frontier is content + go-live.

---

### (Below: original 2026-06-07 text, retained for history — numbers superseded by the table above.)

## Why This Was Rewritten

The previous revision (2026-05-29) became misleading for two reasons:

1. **Stale.** There have been ~1,173 commits on the `4.8` branch since it was written.
2. **Wrong denominator.** It scored only the 169-file Java `services` surface and reported "≈53% balanced." But `services` is roughly 7% of the 2,324-file Java gameserver, and roughly 4% of the full ~4,056-file gameplay surface once the 1,732 content-handler scripts are included. The largest and hardest parity work lives almost entirely *outside* what that estimate counted.

This revision keeps the original services-only view (it is still useful) but reframes it inside the full gameserver surface and adds a **modeled-vs-live** distinction, which is the single most important correction.

## The Core Distinction: Modeled vs Live

The C# port uses a heavily decomposed **plan-service** pattern. Behavior is first modeled in small, non-live `*PlanService` classes (formulas, packet shapes, guard order) and validated with unit tests; live runtime wiring is wired in selectively and often deferred.

Counts as of this revision (`Aion.GameServer/Services`):

- 733 service files total
- **249 are `*PlanService`** (~34%) — non-live boundaries by design
- 40 `*Runtime*` files
- 5 `*ExecutionService`
- 787 GameServer test files

**Consequence: file-count coverage overstates runtime parity.** An area can have many files and a green test suite while remaining non-live. The clearest example is `vortex`: rated "Partial 30%" in May, it now has 34 files — but they are almost all `*PlanService`/dispatch/composition modeling, with no live invasion lifecycle. Coverage went up ~11×; live parity did not.

Every number below is therefore tagged as **modeled** (planned/tested, not necessarily live) or **live** (wired into the running server) where the distinction matters.

## Full Gameserver Surface (the real denominator)

Java `game-server/src/com/aionemu/gameserver` = 2,324 files. Plus 1,732 content-handler scripts under `game-server/data/handlers` (quests, AI, instances, admin/console commands) = ~4,056 gameplay-relevant Java files.

| Java area | Java files | C# state | Parity |
| --- | ---: | --- | --- |
| `model` | 801 | 89 C# `Model` files | low — ~11% surface, blocks much downstream work |
| `network` | 523 | 398 C# `Network` files | medium-high for packets (see Packet Layer below) |
| `skillengine` | 292 | 0 dedicated files | **none** — no live skill/effect engine |
| `services` | 169 | 733 C# `Services` (decomposed) | mixed — see Services Surface below |
| `dataholders` | 100 | 64 C# `Dataholders` | partial |
| `questEngine` | 79 | ~handful | **near zero** |
| `controllers` | 61 | 2 C# `Controllers` | **none** — no live creature behavior |
| `dao` | 57 | folded into repositories | partial |
| `world` | 41 | 7 C# `World` | low |
| `utils` | 41 | 5 C# `Utils` (+ inline) | partial |
| `configs` | 40 | scattered config classes | partial |
| `ai` | 39 | 0 live (keyword matches were false positives) | **none** |
| `geoEngine` | 29 | 2 | low |
| `taskmanager` | 14 | partial scheduler bridges | low |
| `spawnengine` | 14 | first-pass `SpawnEngine` bridge | low |
| `instance` | 5 (src) + 37 handlers | minimal | **near zero** |
| `data/handlers` content | 1,732 | ~11 reference `QuestHandler`; effectively unported | **near zero** |

The two pillars that gate a playable server — **`skillengine`** (combat/effects) and the **content handlers** (quests/AI/instances) — are effectively absent. `model` (the data backbone for both) is ~11% ported.

## Packet Layer (a genuine strength)

The client-facing protocol surface is well advanced, which is why the login→play path works as far as it does:

- Client→server: **186 of 202** Java `CM_*` packets have C# `Cm*` handlers (~92%)
- Server→client: **191 of 261** Java `SM_*` packets have C# `Sm*` writers (~73%)

Caveat: many of these handlers route into plan-services or intentional no-ops for deferred systems (revive, loot, zone change, channel, etc.), so a ported handler is not the same as a live behavior.

## Services Surface (the original scope, refreshed)

Along the **login → items → housing → social** axis the services surface is genuinely deep now (Phase 6): `CM_ENTER_WORLD` plus ~40 post-enter packets, item actions (enchant, manastone, idian polish, decompose, assemble, extract, AP-extract, remodel, charge), housing auctions/bids/rent, kisk lifecycle, mail/broker, stigma, friends/blocks, chat, movement broadcast, and a large body of combat/reward **formula** services.

The same surface is shallow or absent where it depends on the missing engine: combat resolution, skill effects, NPC AI, quest progression, and the large-area systems below.

### "Not obvious" service areas — still at or near zero

No meaningful progress since May; these remain the same gap:

| Area | Java weight | C# state |
| --- | ---: | --- |
| `siege` | 14 | 0 service files; 22 incidental mentions only (PvP-zone/AP/mail) |
| `panesterra` | 4 | 0 |
| `transfers` | 4 | 1 file |
| `conquerorAndProtectorSystem` | 3 | 0–2 mentions |
| `ban` | 3 | 3 files (partial) |
| `worldraid` | 2 | 0 |
| `event` | 3 | event-drop support only |

## Revised Parity Estimate

Two honest numbers for two different questions:

- **Services surface only (the docs' historical scope):** higher than May's ~53% along the login/items/housing axis — but *live* parity within it is materially lower than file counts imply because ~34% of services are plan-only and the combat-dependent slices are non-live.
- **Full gameplay parity (engine + content + model):** realistically **~15–25%**. `skillengine` and the 1,732 content handlers are effectively untouched and `model` is ~11% ported.

Treat both as bands, not points. The full-gameplay band is the one that matters for "is this a playable server."

## Architectural Risks

- **`GameServerConnection.cs` is 22,907 lines** — a god-class holding the bulk of packet handling. Maintainability and correctness risk that grows with every new handler.
- **Build is green (0 errors)** but carries nullable-reference warnings (13 in GameServer at this revision).
- **No real-client validation yet** — explicitly deferred. Nothing in the plan-service or live surface has been confirmed against a live client.
- **Plan-service sprawl** — extreme decomposition (e.g. multi-level `BindPointTeleportKinah*` chains) raises the cost of promoting modeled behavior to live and of reasoning about ordering/side-effects.

## What's Left, In Dependency Order

1. **Live creature/runtime layer** — controllers (61→2), KnownList persistence, scheduler/task model, movement→combat wiring. Prerequisite for everything below.
2. **SkillEngine** (292 files) — effect templates, abnormal effects, skill application. No live combat without it.
3. **AI** (39 src + 43 scripts + per-area) — NPC behavior.
4. **questEngine + ~1,153 quest scripts** — the single largest content body.
5. **`model` layer** (801→89) — backbone that blocks 1–4.
6. **Instances** (5 src + 37 handlers) and the large-area systems still at zero: **siege, panesterra, worldraid, transfers, conqueror/protector**.
7. **Promote the ~249 plan-services from modeled to live**, then validate against a real client.

## Per-Area Docs

The per-service and per-package docs under `packages/` and `top-level/` are individually still dated 2026-05-29. Their high-level statuses are directionally usable but their completion language predates this revision. Refresh them opportunistically; this document is the authoritative summary.
