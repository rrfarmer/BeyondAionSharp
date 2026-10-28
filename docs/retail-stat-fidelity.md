# Retail NPC stat fidelity

Maintainer authority: supplied 5.8 retail binaries and correctly selected data
win for matched NPC stats; Java is read for integration. Preserve 4.8 content,
protocol and unrelated player/PvP rules. Local scoped commits were authorized
by the 2026-10-28 mob-stats goal; pushes and deployments remain unauthorized.

## Ordinary construction and scalar projection

The Java `NpcData` loader fills several zero-valued stats from level/rating/rank
tables. Java's shared stat layer also clamps primary attributes to a player-style
minimum. Those rules remain the legacy baseline. The retail source projections
retain explicit zero, use the native constructor/table stages where the field is
absent, and evaluate the reviewed NPC scalar channels with their native widths
and caps. An unknown source/context remains unsupported.

| Area | Retail rule preserved | Evidence retained outside Git |
|---|---|---|
| Source selection | Exact selected record/build/region; ordered attacks and skills; absent versus explicit zero | Existing extractor manifest and source receipts |
| HP | Separate parser upper cap, initial HP copy and postparse lower bounds | `phase2/ordinary-seed-evidence`; source-step instruction anchors in `RetailNpcOrdinaryCoreSeedProjection` |
| Normal attack inputs | Native-width min/max endpoint copies; raw physical/magical attack fields are excluded from this damage basis | Same seed evidence and frozen 25-record vectors |
| Primary/defensive stats | Retain NPC zero copies and the reviewed integer modifier/cap order rather than silently applying player defaults | Existing ordinary scalar contracts and 25-record scalar vectors |

The pinned MainServer identity is
`da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1`.
The ordinary seed evidence covers 10,289 byte-checked instructions in 30 cached
functions. Individual steps retain their function/address provenance in code.
These static calculations are source-backed reconstructions, not observations
of every runtime actor or special state.

The source reader/projections alone do not activate these differences. The
existing broader runtime adapter, package selection and combat integration must
be reviewed separately. Synthetic unit inputs are not retail data. The full
source fixtures and their hash-checked integration tests remain external.

## Ordinary normal-attack arithmetic

The normal base uses the selected damage sample and authored attack-profile
percentage. The effect percentage adds to that profile percentage; its flat
channel is applied separately. For sample 100, profile 150%, effect +20%, the
base is 170; adding flat 20 makes 190. Applying Java's generic stat multiplier
to an already scaled 150 would give the wrong result. Actual proxy/owner
conditions are evaluated when reading NPC modifier functions.

Native `ChangeAttackParamater` (`0x14035a280`) and `CalcNormalAttackDamage`
(`0x140369e10`) establish the modifier channels and float operation order. The
non-inlined scalar operations retain the recovered binary32 rounding points.
Ordinary arithmetic uses the normal-damage module directly, without depending
on the native player-memory reconstruction module for those operations.

For dual attacks, the native tail (`0x14035a090..0x14035a10e`) halves each source
endpoint before later per-hand modifiers. Odd fractions are discarded: 335..503
becomes 167..251 in each hand. This does not authorize a generic multi-hit split.

The pure stages also cover the reviewed level, physical/elemental defense,
critical factor and PvE-ratio arithmetic. They require explicit NPC-facing
inputs and reject a player-versus-player scope. They do not choose live targets,
draw RNG, apply effects, mutate HP or activate a cohort. The runtime integration
and its broader input admission remain separate pending work.

## Profile conversion boundary

Statistics-only conversion requires the original 4.8 template. Its HP and
reviewed NPC attribute/defense bases come from the selected retail calculation;
MP, legacy skill attack operands and speeds remain compatibility inputs. Omitting
the original template must not silently substitute zero MP/skill attack or source
movement. The full pilot's separate conversion remains available with its
previously reviewed complete profile.

The base-profile reader is shared with the full catalog parser, but its offline
preview is not activation approval. Tests compare every selected membership with
the earlier pinned full-parser export, including source/host identity declarations,
all exported stats, raw damage endpoints and ordered attack entries. Synthetic
mapping cases distinguish zero primaries and all six elemental channels. No
current NPC, AI hook, player state or final HP loss is inferred from these tests.

## Selected NPC getter integration

The statistics package now has one immutable publication/lookup kernel, separate
from optional skill and player-state readers. A build lacking the complete combat
admission consumer rejects configured publication; reading valid profiles cannot
silently enable only HP and defenses. The test-only install seam intentionally
allows getter verification without activation.

For selected ownerless NPCs, construction uses the converted template, selected
level, source primary zero values and reviewed scalar modifier/cap order. Native
critical-rate resistance uses its 9999 cap, independently of critical-damage
reduction. Java's existing final AI hook remains in place. An AI alteration of
exact HP is visible in diagnostics and is an encounter-specific exception, not
evidence that the unchanged retail base was applied.

Native HP remains an integer through `Stat2.GetCurrent`: for example, 86,954,218
must not round down to the binary32 value 86,954,216. The exact result expires on
every subsequent stat mutation, preserving the existing AI/effect hook semantics.
Untagged stats, including player stats, retain Java's float-to-integer behavior.

Late creator/summon ownership retires an ordinary statistics selection. An
unpublished NPC reconstructs fresh host HP/MP; an already published one uses the
existing current-stat update path. Clearing the creator does not readmit the
ordinary profile. These are explicit compatibility exclusions for unreviewed
owned/scaled construction, not a substitute calculation for those units.

Offline getter gates construct real NPCs without spawning, applying passive
skills or running AI. They verify base consumption separately from spawn-time
effects and encounter behavior. The ordinary normal/publication integration below
has a separate gate; the getter test alone does not establish attack parity.

## Ordinary normal consumer and package publication

The existing attack controller and physical/magical result entry points select
the retail normal adapter for matched ownerless NPC-to-player attacks. An ordered
source attack supplies the element and modifier. Multiple entries use the native
minimum-reach weighted pool; this changes combat-entry choice, not movement or
pathfinding. The calculation uses source min/max endpoints, late dual splitting,
current NPC modifiers, native active-defense/critical arithmetic and the selected
map/NPC PvE channels. Raw source physical/magical attack fields remain excluded.

Each hand enters the existing host protection owner once. The normal controller
then delivers its result through the existing HP-loss path. Pilot scheduling,
special skills, player-memory reconstruction and navigation are separate partials;
the ordinary consumer compiles and runs its tests without them. Historical
`SkyTemple` type names remain to preserve existing callers, not to limit membership.

Configured startup requires both the manifest and its separate SHA256. Before
publication it verifies selected sources, loaded 4.8 template identities, supported
map PvE inputs and reviewed owner-family exclusions. Optional full-pilot validation
checks source agreement through its existing interop. No source-only publication
or second active catalog is introduced. Both variables absent retains legacy
behavior. This code change neither selects a machine's runtime package nor deploys it.

Native calculations are source-backed; current 4.8 player item/effect getters,
observers and packet/HP ownership are explicit compatibility mappings. They are
not a reconstruction of every 5.8 player state. The ordinary adapter covers
NPC-to-player normal attacks. NPC-to-NPC and player-to-NPC attack algorithms retain
their host paths unless an existing separately reviewed full adapter applies;
selected NPC defensive getters still supply the corrected defensive values.

Profiles with special exclusive equipment responses remain visible exceptions
when the separately reviewed response consumer is absent. Unsupported normal
shapes also emit a specific compatibility warning. They retain host normal attack
operands, never a made-up retail damage value. Owned/scaled construction and the
bounded complex-skill list remain separate exceptions. These exceptions and the
controlled normal coverage are recorded per map/NPC outside Git.

Per-hit calculation traces are debug-only, and JSON operands are serialized only
when that logger enables debug. Missing-data warnings remain visible. The managed
retail RNG retains the recovered engine and inclusive mapping; a cryptographic
host seed is an infrastructure choice, not a claim to reproduce a captured retail
global seed or interleaving.

## Ordinary StatUp numeric programs

The existing `BufEffect` registration/removal owner now selects source numeric
programs for matched statistics-only NPC-to-NPC effects. Cast selection, success,
targeting, duration, secondary effects and scheduling retain their existing owners.
Player-origin or player-target buffs and unrelated player/PvP programs are outside
this selection. Unknown descriptors, nonzero R9's separate writer, conditional host
core modifiers and missing exact bindings remain explicit compatibility warnings.

The source reader is shared with optional damage parsing without depending on its
damage-family implementation. Direct bindings retain native case-insensitive ASCII
name lookup. Constant triggered children require a source lineage and matching host
Provoker links/stack; they are not added to NPC skill lists. The two Ishalgen
triggered-buff gaps have source-bound child programs; their existing coefficients
are verified unchanged rather than counted as new numeric defects.

Native `GetBonusAttrTo` index99 (`PMDefend`) expands to PDef plus all six elemental
defenses, not magical hit resistance. The same six-element expansion applies to
`ElementalDefendAll`. Complete `SkillEffectStatUp::ApplyEffect` proof supplies four
ordered descriptors, signed16 skill level, wrapped32 level*delta+value, separate
flat/percent channels and the existing integer scalar caps. R5/R8/R10/R11/R12/R23/R24
are not numeric operands in that body; textual R10 such as `Fire` no longer disables
an otherwise supported program. No regeneration/speed/status getter is imported.

If a unique selected source numeric level differs from an exact existing host NPC
list binding, only the numeric descriptor uses that level. For NPC212863/skill16458,
source29 produces evasion+360%, while the existing effect remains host level1.
Neither its skill entry nor other effect/timing consumers are rewritten.

On late target ownership, the source overlay restores only its original host
numeric functions. Other owners, effect registration and existing expiry survive.
Offline gates apply/remove the 293 frozen source-program differences and check
3,516 defense values plus attack modifiers, HP restoration and source-level context.
The fixture supplies success and the spawned flag; it runs no spawn AI or clock.

One historical comparison used absent XML `lv`=1; Java/C# actually default the int
to0. The corrected external comparison changes one row's host-level metadata and
preserves all293 source numeric vectors. Tests use loaded host levels, not that old
default. Source reports and the erratum remain outside Git.

## Actual world-boss HP overrides

Java Sematariux and world Padmarashka explicitly replace HP with20,880,000 as a
"Tweak for12p". The selected 5.8 profiles for216520 and216580 instead calculate
86,954,218. Their C# AI hooks now retain that selected integer HP; the unselected
Java compatibility path still uses its existing value. Legacy skill attack2200
remains a separately identified compatibility operand. No cast, phase, timing,
damage-skill or movement behavior is changed by this HP correction.

The offline actual-AI gate first reproduced both20,880,000 overrides despite the
selected86,954,218 profile. It then checks those same AI classes, exact current/max
HP and an additional2000-HP modifier/removal. It also checks unselected behavior
and the three Altgard one-damage AI base defenses, which already agree with their
selected zero evasion/resistance values. Neutral-AI getter coverage alone did not
prove these actual-AI results.

## Independent offline stat comparison

The [committed CLI](mob-stat-comparison.md) uses the production base/normal/buff
readers without publishing a catalog or loading optional primary-skill/player-state
packages. Source identity is separate from support for skill damage: an unassessed
primary skill is explicitly null, not silently supported or labeled a defect.
This separation does not alter runtime admission or the existing skill path.

The full selected export preserves all11,517 earlier profile/normal rows and buff
source records. Required evidence hash failures remain errors. Reports and receipts
are external/private; generated source records are not committed. The coverage CSV
now keeps its unassessed-skill column even when the first membership has no profile.

## Extraction version and private evidence

The committed [source/index/package workflow](mob-stat-extraction.md) is read-only.
The selected historical generation used an earlier extractor revision. Its scalar
projection and raw-record selection remain unchanged; the later extractor adds
the already-reviewed native ASCII case-insensitive skill-name join. The external
130-binding reindex receipt records that metadata correction. Fresh generations
therefore have their own source/dataset identities; do not rewrite the historical
receipt or change a runtime trust anchor merely to accept a new generation.

The original90-row identity review remains a pinned external test input. Its
224,987 bytes were preserved with the original SHA256 before removing the opaque
compressed source fixture. Public checks visibly skip private cases when the
fixture is absent; changed bytes fail. Package assembly rejects duplicate rows
and overlapping memberships, rebases relative assets, preserves known context
pins and rehashes all inputs before atomic publication. It performs no activation.

## NPC AI stat and normal-damage ownership

The ordinary stat normal-attack fast path skipped the existing one-damage
mechanic. Statistics-only attacks now invoke that mechanic once per hand after
the retail numeric stages and before protection. This is an explicit 4.8 content
compatibility rule, not verified 5.8 script parity. Full behavior packages and
player/PvP paths are unchanged. Consumed owner output is recorded in debug traces;
no hook is replayed for diagnostics. A changed result emits a bounded warning.

Do not reinstate Java's generic numeric damage overrides. Pashid/dragon profiles
already author 130%/120% attacks, and Hyperion authors 150%/450%. Applying Java's
distance boost again double-counts those modifiers. The 22 Shugo instance members
also retain calculated retail normal ranges, not the legacy attack-stat replacement.
Complex phase and custom-map behavior remains separately bounded and unverified.

A no-op `setBaseRate(1)` also expired the native NPC integer HP channel, allowing
large HP values to round through binary32. An unchanged base-rate operand now
retains that channel. Real rate changes still expire it and preserve existing
configuration/scenario behavior. Untagged stats keep their existing float path.
Offline actual-AI checks cover unity siege settings and retain the earlier world-boss
HP/legacy skill-attack gates. AI stat deviations now report before/after values
for defensive getters, including existing one-damage zero-defense rules.

The compiled template-AI metadata audit resolves inherited hooks without executing
AI: all 11,208 non-global selected memberships resolve, across 735 named classes.
It finds 389 stat-hook memberships, including 53 inherited cases omitted by the
direct-only scan. The declared contexts are 10,819 without a stat override, 281
configuration-scaled, 87 one-damage, 7 special HP-rate, 12 custom PvP-map and the 2
corrected world bosses. Among one-damage cases, 27 memberships have nonzero source
evasion/resistance replaced by the existing script rule. These remain explicit
compatibility exceptions. 111 attacking memberships declare a damage-owner override;
36 are fortress guards whose player-target normal callback is an identity.

This is template binding and ordinary getter/calculation evidence, not proof of
active spawn overrides, phases or every custom script. Ordinary matched NPC-to-NPC
normal damage is covered by the receiver integration below. The exact per-NPC
boundaries and source pins remain in the external reconciled coverage table.

## Selected rift-protector and thunder-shield HP

Java permanently multiplies MAXHP by 0.1 for `rift_protector` and
`sematariux_thunder_shield`. For the seven matched fresh/unowned memberships,
that replaces the selected 5.8 constructed HP with another ten-percent value.
These AI hooks now retain the complete selected profile HP. Unselected or owned
actors retain their existing Java rate; no phase, damage or lifecycle code changes.
The actual-AI offline gate checks all seven max/current HP values and retirement
back to the captured unselected result. No new per-NPC HP literal is introduced.

## Ordinary NPC normal-attack receivers

Statistics-only, matched ownerless NPC pairs now use the existing retail normal
calculator. Physical armor uses the receiver's stat ratio; elemental resistance
uses the attacker's ratio and the explicit NPC -1300 floor. Active defense retains
the native NPC level adjustment and dodge/block/parry order. NPC block requires a
nonzero current block word, without a player shield. Forced block/parry precede
random eligibility. Both NPC block and parry multiply post-defense damage by0.9,
then apply the NPC attacker's critical multiplier. Offhand shares these outcomes
and retains its separate sample, current defense/reduction reads and protection.

Main0x14036c490 and the common block/parry branches0x14036cc09/0x14036ce39
establish these rules. The receiver slot129 helper0x1400bc8a0 returns the block
word. Main0x14036a5b0, NPC classifier0x140b221f0 and the constructor's zero owner
reference establish that ordinary NPC pairs skip both PvE and PvP scaling. The
adapter therefore supplies identity scaling and does not read legacy NPC PvE
programs. Existing player-facing behavior and optional pilot lifecycle gates retain
their original domain.

Ordinary stance0 eligibility is a documented static-default reconstruction:
Creature::Init0x14034f050 clears stance, the readonly parry entry0 permits parry,
and the dodge entry0 is loader-zero-filled with no recovered initializer/store.
This does not certify native runtime stance overrides. The managed pose/abnormal
state and effect-observer mappings remain explicit host compatibility assumptions.
The saved PE checks, constructor/table inventory and classifier ledger are under
external `mob-stats-focus-20261028/npc-receiver-01`. They pin Main SHA256
`da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1`.

Receiver scripts with an unknown `ModifyDamage` owner and source damage overrides
outside the verified numeric/one-damage owners retain a visible compatibility
reason and the legacy calculation. This includes the fortress protector's separate
NPC-only fivefold boost, unverified phase boosts and custom PvP-map scripts.
Existing one-damage source/receiver mechanics
remain explicit compatibility exceptions before protection; arbitrary Java damage
boosts are not replayed. Unmatched, owned/scaled and full-behavior NPC receiver
packages are not admitted through this statistics-only branch. Offline cohort
checks use neutral AI and ordinary states; they do not certify active scripts,
player weapon calculations or gameplay.

## Player normal attacks against selected NPCs

Statistics-only NPC targets now consume the same verified level, armor-ratio,
elemental-floor and chance helpers through `RetailNpcPlayerNormal`. Player
weapon/stat damage, movement input, weapon critical coefficients and additional-hit
programs remain the existing4.8 source calculations. Neither PvP nor an unmatched
NPC enters this adapter. Optional pilot/skill code stays outside this commit; its
working normal wrapper forwards to this implementation instead of duplicating it.

NPC dodge/block/parry eligibility and level adjustment share the ordinary NPC
receiver helper. Both block and parry retain0.9 of post-defense damage before
critical. Critical chance consumes the NPC's resistance and stat ratio. Critical
damage retains the current player weapon coefficient and applies the NPC's
reduction: the native User main subtracts it, while physical User offhand adds it.
That unusual offhand sign is retained from Main0x14036b0e7..0x14036b23e; the NPC
attacker's1.5/1.0 coefficients must not replace player weapon calculations.
Source weapon/class tables and native Player override memory are compatibility
inputs, not a new claim of complete5.8 player-source reconstruction.

The fixed `physical_damage_trim`/`magical_damage_trim` fields belong to the damaged
NPC. Main0x14036b6bf..0x14036b80a consumes target virtual slots0x5d8/0x5e0. Complete
NPC getters0x1400bc290/0x1400bc2a0 read template+0x3f4/+0x3f8; User getters return0.
Nonzero trims replace positive reached damage late, after ordinary damage/PvE,
before protection. Every selected nonzero value is1. The shared normal trim helper
applies per record in both NPC-to-NPC and Player-to-NPC directions, retaining the
existing host AI/additional-record owners. It does not revive misses, cap outgoing
NPC damage, or guarantee1HP loss after protection. One-damage compatibility hooks
also skip terminal misses. Existing debug operands distinguish AI, trim and
protection results without re-reading callbacks.

The external `player-receiver-01` evidence rechecks all1,092 instructions/4,819 bytes
of the complete normal consumer and the four complete target getter leaves against
the pinned supplied Main. Ordinary stance0 and managed effect/pose mappings retain
the earlier explicit compatibility assumptions. Unknown incoming script owners,
owned/scaled actors and player critical inputs outside the ordinary word domain
remain visible compatibility boundaries. Skills, transformations, rare Player
overrides and active script states are not certified by the normal-stat fixtures.

## Ordinary runtime publication boundary

`AION_RETAIL_STATS_MANIFEST` publishes the committed profile, normal-combat and
numeric-buff consumers. It does not invoke optional skill, periodic/heal, player
equipment, curse, timing or shield readers merely because their fields occur in
a combined discovery manifest or their partials exist in the local checkout.
Read-only/offline experimental parsers remain separate. Required ordinary assets,
source/host identities and combat context still validate before atomic publication;
failure leaves the previous state intact and reload remains prohibited.

The full-working regression first reproduced publication entering an invalid
optional player-shield context. The ordinary-only publication gate now ignores
that candidate context while retaining all selected profiles, normal PvE inputs
and524 numeric-buff programs. Local optional collection defaults are empty so
their existing callers can decline absent experiments safely; those uncommitted
modules are not imported into the ordinary runtime commit.

Conditional global memberships additionally have an unowned projection gate in
one supported fixture world:309 getter projections,40 authored normal profiles
and269 absent normals. This tests the explicit map0 lookup fallback; it does not
invent a spawn location or certify ownership/scaling and active script behavior.
The full selected11517-row source/normal/buff export remains unchanged after the
runtime isolation and consumer corrections. Per-membership exceptions and the
original-goal audit remain in the external discovery handoff.

The separate catch-up commit made previously local experiments part of the tracked
build. Follow-up integration cleanup retains their behavior while clarifying
guarded nullable inputs and using dedicated threads for monitor-contention tests.
The existing `SystemClock.RetailTick32` monotonic-clock seam is explicitly reviewed
in the clock allowlist; it preserves deterministic overrides and does not change
the legacy wall-clock seam. The original warning baseline is retained.
