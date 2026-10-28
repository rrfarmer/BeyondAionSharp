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
   `AION_RETAIL_STATS_BUFF_SMOKE=1`. This method uses the same map selector,
   defaulting to the original three maps when it is unset.

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

The initial smoke changed no runtime code, data, routing, bots, quests or gear.
Its commit can be reverted independently of the expansion below.

## Asmodian expansion and added instances (2026-10-28)

The maintainer subsequently authorized the remaining supported Asmodian maps,
starting with Morheim, plus Haramel, NTC and Fire Temple. The new coverage is
3,456 selected profiles across 14 maps. A final rerun included the original
417 profiles: 3,873 profiles across 17 maps in total.

| Map | Profiles | Supported buff cases | Unverified buff cases |
| --- | ---: | ---: | ---: |
| Morheim | 687 | 201 | 3 |
| Beluslan | 616 | 162 | 0 |
| Brusthonin | 302 | 46 | 5 |
| Gelkmaros | 918 | 263 | 17 |
| Enshar | 561 | 347 | 31 |
| Habrok | 1 | 0 | 0 |
| Idian Depths | 85 | 55 | 1 |
| Pandaemonium | 123 | 0 | 6 |
| Convent of Marchutan | 9 | 0 | 0 |
| Marchutan Priory | 2 | 0 | 0 |
| Pernon | 67 | 2 | 1 |
| Haramel | 29 | 24 | 0 |
| Nochsana Training Camp | 19 | 8 | 0 |
| Fire Temple | 37 | 12 | 0 |

Fatebound Abbey and the alternate Pernon map have no selected combat profiles
in this package; they are recorded as not applicable rather than tested empty.

All 3,873 HP/normal fixtures passed. All 3,670 authored normal profiles caused
positive final HP loss; 203 have no authored normal. The receipt audit checked
4,132 normal input traces and 7,969 HP events. All 1,179 supported buff cases
across 916 NPC types passed application, numeric getters and removal, with
normal/HP delivery checked for direct cases. The three added instances passed
all 85 profiles and 44 buff cases without a buff boundary.

### Corrections

- Existing host buff entries can use a pinned constant retail numeric program
  when their exact host entry/level and native name/stack identity agree.
  Sixteen cases now use the verified program, including Dragon's Blessing's
  previously omitted dark/light defense modifiers. This retains the existing
  cast admission and emits an explicit admission-compatibility warning. It
  does not add a skill or certify the native AI's decision to cast it.
- Native `ActiveDefend` expands to evasion, parry and block. Attribute98's
  dispatch and 13 writer instructions were byte-checked against the pinned
  Main binary; eight focused defensive-buff cases now pass.
- The smoke checks exact integer HP, including large bosses. Its earlier float
  expectation was stale; production already retained the exact retail integer.
- Buff fixtures restore their controlled stand after resurrection and explicitly
  wake seated actors. Each method uses a fresh private process/database; mixing
  them exposed a second-login restriction in the shared fixture.

These numeric departures are logged in `retail-ai-fidelity.md` and the local
`retail-mob-system.md` record. Java BufEffect/NPC skill-list behavior was reviewed
for integration. Cast admission, durations, shields and other lifecycle owners
retain their existing behavior. No routing, speed, bots, quests or gear changed.

### Remaining boundaries

The 64 unverified cases are **not** reported as retail buff parity:

- 34 rank/stack identity cases already have the same flat magical-resist value
  in both sources; the strict identity guard remains closed.
- Seven cases lack a skill source in the selected manifest.
- Five mixed HP/abnormal-resistance programs require the unresolved `ArAll`
  context and remain whole-program compatibility cases.
- Eighteen cases involve speed (7), attack delay (8), general abnormal resistance
  (2), or concentration (1). These were neither approximated nor partially applied.

Detailed per-NPC cases, source identities, failed setup attempts, full final
receipts and rollback notes are retained outside Git at
`C:\Users\ryanf\Documents\Aion-NPC-Discovery\implementation\asmodian-stat-smoke-20261028-02`.
Final SIM receipts are `runs/attempt-11` (buffs) and `runs/attempt-12` (HP/normals).
All 22 targeted regressions passed without skips. The full warning baseline,
clock-read guard, null-logger ratchet and scoped whitespace check passed.

No push or deployment is authorized. Roll back the expansion by reverting its
scoped commit at an appropriate local integration point; no database migration
is involved. For a separately authorized runtime rollback, deselect both retail
statistics manifest variables and restart normally. Preserve unrelated edits.
