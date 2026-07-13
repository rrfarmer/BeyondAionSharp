# Game-Server Parity-Risk Ownership Trace

Date: 2026-06-07
Supersedes: the 2026-05-29 revision (preserved in git history).

## Purpose

This document separates parity risk into three buckets:

1. **Modeled** — behavior exists as plan-services/formulas with tests, but is not (fully) live.
2. **Live** — wired into the running server.
3. **Absent** — no meaningful C# ownership.

The 2026-05-29 revision tried to convert the services "likely exists but needs tracing" bucket into completion percentages. Those percentages are withdrawn: they conflated *modeled* with *done* and only covered the narrow services surface. Use the [Completion Estimate](Completion-Estimate.md) for the authoritative full-surface picture.

## The Governing Risk: Modeled ≠ Live

~249 of 733 C# service files are `*PlanService` boundaries. They are validated by 787 GameServer tests but explicitly defer live wiring. **The dominant parity risk is no longer "is it absent" — it is "is the modeled behavior actually wired into a running creature/skill/world runtime."** Because the runtime engine itself (controllers, skillengine) is absent, most modeled combat/reward/effect behavior cannot be live yet regardless of how complete the plan-service is.

## Bucket 1 — Modeled, needs live promotion (and an engine to host it)

These have real C# ownership but are non-live or partially live. They cannot reach parity until the runtime layer (controllers, scheduler, KnownList, skillengine) exists to host them.

| Area | C# ownership | Live gap |
| --- | --- | --- |
| `vortex` | ~34 files, almost all plan/dispatch | no live invasion lifecycle |
| `summons` | summon panel/mode/create plan-services | no live summon spawn, owner lifecycle, controller |
| `duel` | request/accept/result plan-services | draw scheduler absent; loss/HP-MP side effects not executed in live death workflow |
| `reward` | bonus/faction/starter-kit/system-mail planners | advent/veteran/web reward ownership still open |
| combat/stat formulas | large `*FormulaService`/`*PlanService` set (damage, crit, resist, hate, AP/XP/DP, drop) | none are live because there is no combat loop or skill engine to call them |

## Bucket 2 — Live or near-live (login → play axis)

These have real, partially-live ownership. Risk here is ownership completeness and untested-against-client behavior, not absence.

| Area | C# ownership | Notes |
| --- | --- | --- |
| enter-world | `PlayerEnterWorldService` + ~40 post-enter packets | deep; client-validation still deferred |
| item actions | enchant, manastone, idian, decompose, assemble, extract, ap-extract, remodel, charge | DB-backed, persistence wired |
| `housing` | world/visibility/door/auction/maintenance services + repos | auctions/bids/rent live; broader visit/instance side-effects open |
| `kisk` | spawn/bind/lifecycle/registry/revive slices | group/alliance resolver, controller/death state pending |
| `mail` | repository + system-mail + in-game list/read/attach/delete | full formatter parity, siege/abyss mail open |
| `broker` | register/buy/search/settle, DB-backed | NPC `OPEN_VENDOR` function validation pending (needs NPC/known-list) |
| friends/blocks/chat | DB-backed reciprocal ops, public/whisper/CS bridge | live within online registry |
| movement | `CM_MOVE` parsing + first known-list broadcast | persistent known-list, anti-hack, flying gates pending |

## Bucket 3 — Absent (no meaningful C# ownership)

Confirmed at or near zero. These are the large-area systems and, more importantly, the engine pillars.

### Service-surface absences
- `siege` (14 Java files; 22 incidental mentions only)
- `panesterra` (4)
- `worldraid` (2)
- `transfers` (4; 1 file)
- `conquerorAndProtectorSystem` (3)

### Engine/content absences (outside the services surface — the real blockers)
- `skillengine` (292 Java files → 0): no effect templates, abnormal effects, or skill application → no live combat
- `controllers` (61 → 2): no live creature behavior
- `ai` (39 src + 43 scripts → 0 live)
- `questEngine` + ~1,153 quest scripts → near zero
- `data/handlers` content (1,732 files) → near zero
- `model` (801 → 89): backbone gap that blocks all of the above

## Resulting Interpretation

- The services surface is genuinely deep along the login/items/housing/social axis and shallow everywhere combat/AI/quest-dependent.
- The previous "this is mostly an ownership-tracing problem" framing was true only for the services surface. At the whole-server level it is a **build-the-engine** problem: the combat/effect runtime and the content layer do not exist yet, and most modeled service behavior is waiting on them.

## Next Deep-Dive Order

1. Runtime layer feasibility: controllers + scheduler + KnownList + world tick (gates everything).
2. `skillengine` scoping (292 files) — the highest-leverage single body.
3. `model` gap audit (801→89) — what specifically blocks 1 and 2.
4. `questEngine` + quest-script strategy (port vs data-drive vs interpret).
5. Plan-service promotion plan: which modeled clusters become live once the engine exists.
