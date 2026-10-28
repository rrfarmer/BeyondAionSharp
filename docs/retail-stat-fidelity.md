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
