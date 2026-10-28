# Focused retail mob-stat SIM smoke

On 2026-10-28 the maintainer authorized focused SIM checks of Three Keys,
Ishalgen and Altgard, with local commits. The earlier static-only goal remains
complete. This follow-up does not expand routing or deployment scope.

The production statistics-only loader was used with the separately pinned
`package-full-12` manifest, SHA256
`28013685a95b0394b312c1541a9453d8121ac3109dff67e629c0be1b0f645c58`.
The optional pilot catalog and experimental skill/player-state readers were
not enabled.

## Acceptance

| Map | Profiles checked | Buff cases checked |
| --- | ---: | ---: |
| Three Keys / Sky Temple Interior | 25 | 4 |
| Ishalgen | 103 | 10 |
| Altgard | 289 | 45 |

- All 417 selected profile identities, loaded HP and defensive getters passed.
- All 408 profiles with authored normal attacks produced positive final HP
  loss. Nine source profiles have no authored normal attack; they remained
  visible in the receipt rather than receiving an invented attack.
- The audit checked 554 normal input traces and 971 HP events, including
  zero-damage misses, the player-to-NPC production dispatcher and controller
  damage delivery.
- All 59 supported numeric-buff cases across 47 NPC types passed application,
  numeric getters and removal. The 57 direct cases also checked attack modifier
  consumption and final HP delivery. The two additional Ishalgen child cases
  were reached through the parent observers.
  No missing numeric-buff binding was found in this focused inventory.

No production stat/combat defect was exposed. The changes strengthen regression
coverage: the existing normal smoke uses `AttackUtil` and requires positive HP
loss after bounded retries; the new buff smoke checks actual Effect ownership,
parent notifications and the production normal/HP path.

The 93 selected combat regressions passed without skips. The full warning
baseline, null-logger ratchet, clock-read guard and staged whitespace check passed.

## Repeating the checks

Use a fresh private copy of the reviewed ordinary-character snapshot. Do not
point these destructive world fixtures at a live server or a shared journey DB.
The tests retain the source character's level, gear and skills. Copied staff
effects must be absent. End arrival protection through its existing controller.

Build before acquiring the SIM marker. Set the usual Docker-backed SIM variables
and a private `AION_SIM_NI08_DATABASE`; set `AION_RETAIL_STATS_MANIFEST` and its
separate `AION_RETAIL_STATS_MANIFEST_SHA256`. Leave optional pilot/fixture catalog
variables unset. Keep the dashboard on port 17880 when available.

Run separately, using `dotnet test tests/Aion.Simulation.Tests --no-build
--no-restore --filter FullyQualifiedName~<method>`:

1. `RetailStatisticsUseSourceBasesAndFinalHpInSim`, with
   `AION_RETAIL_STATS_SIM=1` and
   `AION_RETAIL_STATS_SIM_MAPS=320050000,220010000,220030000`.
2. `RetailStatisticsFocusedBuffSmokeUsesProductionEffectsAndTriggers`, with
   `AION_RETAIL_STATS_BUFF_SMOKE=1`. This method scans those three maps explicitly.

Set `AION_E2E_RUN_DIR` to a fresh external directory. Check the TRX for an actual
pass, source/assembly identities, receipt completeness and compatibility warnings.
Never build or run another check while a SIM owns these DLLs.

The retained runner, source snapshots, failed test-setup attempt, successful
receipts and verification script are outside Git at
`C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\sim-stat-smoke-20261028-01`.

## Limits and rollback

These are controlled integration fixtures with normal production combat/HP
owners. Buff admission and attack notifications are supplied; AI selection,
proc probabilities, natural encounters, routing and rendered-client play are
not certified. Two existing compatibility warnings from an out-of-scope scripted
NPC occur during global bootstrap and remain recorded in the external receipt.

No runtime code, data, routing, bots, quests or gear changed. Reverting this
follow-up removes only its tests and documentation; no server restart or data
migration is needed. No push or deployment was performed.
