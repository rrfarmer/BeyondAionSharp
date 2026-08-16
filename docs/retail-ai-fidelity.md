# Retail AI fidelity layer

Changes in this layer **deliberately diverge from the Java reference**. They are
the one sanctioned exception to the golden rule in `CLAUDE.md` ("the Java source
is the spec"), because they are sourced from something more authoritative than
aionemu: NCSoft's own server-side NPC AI scripts.

Do not "fix" these back toward Java during parity work. Anything listed here is
intentional; aionemu's version is an approximation that predates access to the
retail data.

## Sources

| Source | What it gives |
|---|---|
| NpcAIPatterns dump (patch 5.8, 242 files, 12,798 patterns) | The retail behavior script per AI name: event handlers, battle timers, HP thresholds, add spawns, messaging |
| NpcAIPatterns dump (patch 2.7) | Diff baseline — 5.8 renamed or deleted ~5% of 4.8-era patterns |
| Game client, via `tools/client-extract` | `ai_name` per npc_id — the pattern → NPC binding for 49,134 NPCs |

See `tools/client-extract/README.md` for the extraction tooling and the formats.

## Rules

1. **Commit trailers.** Use `Retail-AI-Pattern: <pattern name>` rather than
   `Upstream-Java-SHA`. These are not upstream ports and must not be logged in
   `docs/upstream-port-log.md`.
2. **Prefer data over code.** Where the existing data model can express the
   retail behavior (`npc_skills.xml` probabilities/cooldowns/HP windows,
   `ai/spawn_helpers.xml` HP-triggered summons, `npc_shouts.xml`), change data.
   Reserve new AI handler classes for behavior the data model cannot express.
3. **Cite the evidence.** Every entry below states which pattern it came from
   and how the NPC and its skills were identified, so each change is reviewable
   without re-deriving it.
4. **Mark inferences.** Retail data does not resolve everything — most notably
   `SKILLI_INDEX_N`, which indexes a per-NPC skill list that exists in neither
   the client nor our repos. Where a skill identity is inferred, say so.

## The waypoint-placement problem

A second class of pattern data is unavailable for the same reason as skill
lists: it lived on NCSoft's server. When a `spawn` action uses
`SPAWN_LOCATION_WAY_POINT_START`, the add is placed at the start of a named
designer path (`LDF4_GHObject1_KJS` and the like). Those paths are in neither
our `npc_walker.xml` nor the client — searched the CryEngine mission files of
the Inggison, Levinshor and Levinshor-instance levels, and `*-path.dat` is
navmesh rather than named paths. Without them an add has no position.

This affects little: of 17,892 spawn actions in the dump, 881 (5%) are
waypoint-placed. Everything else carries its own position — at the spawner, at
a target, or as literal coordinates. `audit_missing_adds.py` flags the blocked
ones, and 504 of 518 encounters have none.

**Inggison and Gelkmaros fortress bosses (Enraged Veille 258203 / Enraged
Mastarius 258207) are blocked by this.** Their pattern `LF4_GH_KJS` / `DF4_GH_KJS`
is a 30-minute time attack: below 90% HP the boss spawns eight aether
concentrators at waypoint paths, and messages from those concentrators arm the
timers and self-buffs that drive the rest of the fight. Without the eight
positions the mechanic cannot be reconstructed. Java's `EnragedAgent` already
carries a matching note from its author, who skipped the concentrators
deliberately as needing "about 100 player to be activated". A few of their
helper spawns are positionable, but adding those alone would announce a time
attack that does not happen.

## Shared patterns with absolute coordinates

A second placement that cannot be ported, distinct from the waypoint problem
above and easier to miss because it looks perfectly implementable: a pattern
whose spawn carries `SPAWN_LOCATION_ABSOLUTE` coordinates, bound by NPCs that
live in **more than one map**.

`BGuard_ChiefD_Minor` is the example that found this. Its `on_die` drops six
Balaur at three fixed points, and those points sit right beside a real spawn in
Krotan Barracks — which is exactly why it looked safe. But seven NPCs bind to
that pattern: guard chiefs at level 45 and 65 across Krotan, Dkisas and Lamiren,
plus an Abyss reward guardian. The coordinates can only be right for one of the
three fortresses, so implementing them would put six NPCs in the wrong place in
the other two.

`triage_missing_adds.py` now buckets these separately (6 adds). The check is
whether the pattern's bound NPCs appear in more than one `spawn_map`; when they
do, its absolute coordinates are unusable without per-map values we do not have.

**The general rule this is an instance of:** before porting anything positional,
check how many NPCs share the pattern and where they live. A pattern bound by one
NPC can be taken literally. A pattern bound by seven cannot.

## The skill-index problem

AI patterns reference skills as `SKILLI_INDEX_0..14`, a 0-based index into the
NPC's ordered skill list. That list was server-side data; it is **not** in the
game client (verified by indexing all 525,657 entries across all 3,332
archives). Resolve it per NPC using:

- distinctive skill names and effects (a self-target buff in the pattern must
  map to a buff skill in our list),
- the `skill_no` attribute in `npc_shouts.xml`, which equals index + 1,
- our own `npc_skills.xml` ordering, which sometimes matches the client's and
  sometimes does not — corroborate it, never assume it.

## What is portable at all

`audit_skill_index_reach.py` answers the question that decides whether a
timer-driven boss can be ported: does its pattern address any skill index beyond
the end of our own `npc_skills` list? An index past it cannot be identified —
the client list that would resolve it is the data we do not have — so a rotation
reaching that far can be reproduced in shape but not in content, and writing it
would mean inventing the casts.

Of 127 timer-driven bosses with an AI class, **40 keep every index inside our
list and 85 reach past it**. Run this before starting a boss, not halfway
through.

**Unstable Triroan is the cautionary case.** It looked like a natural next
target — 24 timer branches, an eleven-step invented HP ladder to replace. But
its pattern addresses indices 0 through 8 against a five-skill list, so most of
the rotation is unidentifiable; and the pattern carries **no combat spawns at
all**, while our version spawns fire, water, earth and wind elementals through
its ladder. Porting it faithfully would delete a visible mechanic and be unable
to reproduce what retail runs instead. It stays as it is until a client skill
list turns up.

## Sweeps

`tools/client-extract` carries three audits that turn a hand-found bug into a
server-wide worklist. Each produces candidates for review, not auto-fixes.

| Audit | Finds | Current count |
|---|---|---|
| `audit_missing_adds.py` | Encounter adds that exist only as templates, nothing ever spawns them | 812 across 518 encounters (768 implementable, 44 waypoint-blocked) |
| `audit_dead_shouts.py` | NPCs left mute because their lines sit on a twin we never spawn | 0 remaining (was 197) |
| `audit_hp_phases.py` | Hand-written `HpPhases` thresholds that disagree with the retail pattern | 21 remaining; 7 corrected, 2 judged correct as-is. Of the 21, 12 are timer-driven and 7 regime-guarded, so only ~2 are true renumbers |

Notes for whoever works these lists:

- **Dead shouts** were worked in one pass (see the log below). Two traps the
  audit now encodes: an NPC already covered by a broad catch-all group is not
  mute even when this group's own lines never play, and a group's
  `restrict_world` often names a different map than the live NPC's.
- **HP-phase mismatches split into two very different jobs.** `is_hp_lower_than`
  latched behind a flag is a phase transition, and where retail has a comparable
  list the fix is renumbering — Adjutant Anuhart's `50/25/10` against a retail
  `70/40/22`, Vasharti's `75/50/25/10` against `86/56/26`. But
  `is_hp_in_boundary` is a regime guard that gates timer branches and fires
  repeatedly, and a boss built from those has no phase list to copy at all.
  Modor is the example: retail runs her as two regimes, above and below 75%,
  with flag-latched chains inside each, so our `HpPhases(100, 81, 77, 61, 50)`
  is not five wrong numbers but the wrong shape. Those seven are
  reimplementations on the scale of Macunbello, not edits. The audit separates
  the two.
- **Two false-positive classes the audit now filters out**, both found by
  working the list. Several classes use `HpPhases` as a start-of-fight trigger
  rather than a ladder — `HpPhases(95)` with a handler that ignores its argument
  and just starts a skill loop. Renumbering those to a retail threshold would
  delay the whole fight: Ebonsoul and Rukril would not begin casting until 7%
  HP. And patterns carry latched HP steps with empty `<actions>` as sequence
  markers, so counting them inflates the apparent phase count — Watchman
  Hokuruki has five HP steps of which only two spawn anything, which made a
  structural difference look like a clean renumber.
- Even after filtering, a matching count is not sufficient. Check that each
  step's *actions* line up before renumbering.

## Verifying these changes

`tests/Aion.GameServer.Tests/Ai/` runs a boss headless against a simulated
player on a virtual clock, so a fight that takes minutes in game is asserted in
milliseconds. It exists because the changes in this document are the kind that
`dotnet build` and every other test are blind to: renumbering a threshold or
mis-ordering a battle timer compiles perfectly and breaks the encounter.

`BossAiHarness` loads the **real** static data — `npc_templates`,
`skill_templates`, `npc_skills`, `tribe_relations` — so `Spawn(216245)` is the
actual Macunbello with his real skill list. A wrong npc id, a broken `ai_name`
binding or a skill that does not exist fails the test rather than passing
quietly, which synthetic templates would not catch.

Assertions observe an AI's *decisions* — which skill it queued against which
target attribute, which adds it spawned, which phase it entered — not their
damage. That is precisely the layer the retail patterns specify, so it is the
layer worth pinning; it does mean a test cannot tell you a skill actually
landed.

The one production concession is a test seam: `ThreadPoolManager` is no longer
`sealed` and its two scheduling entry points are `virtual`, so the harness can
substitute a virtual clock. Every other overload funnels through those two.
Production behaviour is unchanged.

Adding a boss costs roughly 30–80 lines and no new infrastructure. Not yet
covered: shout broadcasts, which need a recording connection.

---

## Log

### HP thresholds — Mage Preceptor (217580), Heiramune (233467), Calindi Flamelord (219359)

**Mage Preceptor**, `IDArena_S7_Named_3`: retail has two steps, 60 and 30; we
had three at 75/50/25. The shapes match once the odd one out is folded in —
retail's 60 casts three skills and spawns both elementals, its 30 is four casts
with no spawn, which is our old 50 body plus the 75 cast, and our old 25 body.
Nothing dropped, invented threshold gone.

**Nightmare Lord Heiramune**, `IDAsteria_IU_world_3Stage_Boss`: add spawns at
**55**, not 50. Its other two retail steps only shout, which is shout data's
job, so they are left alone.

**Calindi Flamelord**, `IDTiamat_Kalrindy`: retail runs the hallucinatory event
**four** times, at 80/60/40/25, then a different finisher at **15**. We ran it
three times at an invented 75/50/25 and finished at 12 — so this both renumbers
and restores a missing repeat.

### Not changed, and why

- **King Consierd** (`IDArena_S9_Named_2`) and several others declare a phase
  whose body only starts a rotation task. That is a start-of-fight trigger
  wearing a threshold, and retail keeps such rotations on battle timers rather
  than HP steps. Renumbering it to a retail threshold would delay the boss's
  entire skill loop. Left alone.
- **Queen Alukina** (`IDArena_S8_Named_3`) has three escalating steps against
  retail's one. Folding three phases' casts into a single step is speculative
  in a way the Mage Preceptor merge was not — there the counts corroborated it
  exactly — so it waits for a way to observe the fight.

### Rentus Base — Brigade General Vasharti (217313)

Pattern `IDYun_Nmd6`: **three** steps at 86/56/26, where we had four at an
invented 75/50/25/10. His handler does the same thing at every step, so there is
no per-percent branch to follow the renumbering.

**His Glove Controllers are deliberately still not spawned.** Retail creates one
at each step, and those npc_ids (283002/283004/283006) do exist unspawned — but
they are plain `aggressive` clones of Vasharti himself, carrying his name and
level and no controller AI. Spawning them would put three extra full-strength
bosses in the room instead of retail's controllers, which is harder than retail
rather than closer to it. They wait for their own AI class, which is the deep
part of this encounter: walls, buffers and area attacks driven by their own
patterns.

**Verification.** Pinned and mutation-checked; restoring the old four-step
ladder fails.

### Eight bosses given their retail summons

`triage_missing_adds.py` buckets the missing adds by how retail spawns them,
because that decides what each costs. Of 812:

| | |
|---|---|
| 420 | battle-timer spawns — need a timer-driven AI class |
| 121 | on death or despawn — instance handler |
| 143 | on wake-up, aggro, message or idle timer — AI class |
| 46 | waypoint-placed — blocked, see above |
| 19 | **HP threshold — of which only a plain `<spawn>` at the spawner is data** |

So the adds are not a cheap breadth win: only that last bucket is data at all.
The first count here read 29, because the classifier treated a missing
`<spawn_location_type>` as "at the spawner" — but only a plain `<spawn>` carries
that field, and `spawn_on_target` and friends place the add at whatever object
they are aimed at, which no summon table can express. With that corrected the
bucket is eleven, and **all eleven sit on bosses with bespoke AI classes** that
would have to carry the spawn themselves.

Eight bosses on plain `ai="aggressive"` owners had complete retail data
(threshold, count and scatter). Those eight are now
`ai="summoner"` with a summon table. `SummonerAI` extends `AggressiveNpcAI`, so
aggro behaviour is unchanged; it adds the HP-triggered summons and cleans them
up on reset and death.

| Boss | Retail summon |
|---|---|
| Unstable Drakie | a plant at 70% |
| Debilkarim the Maker | all seven guardians at once below half, ringed at 5/10/15/20m |
| Apostate Alchemist | an earth spirit at 50% |
| Lich Priest | a servant at 50% |
| Severed Gnarl | two objects at 35% |
| Coastal Lobsek | an object at 50% |
| Queen Serusia | one egg at 75%, two at 50%, three at 25% |
| Ashunatal Shadowslip | decay at 90%, three explosion at 70%, two disruption at 50% |

**Verification.** Two pinned in `RetailSummonTests` — the one whose waves grow
and the one that sends a different add per step, which between them cover the
shapes. Mutation-checked: flattening Serusia's counts and giving Ashunatal the
same shadow each time both fail. Confirmed every `ai=` name still resolves and
all 55 summoner NPCs have a summon table, since `SummonerAI` requires one.

### Raksang Ruins — The Flamelord (217451)

Pattern `Raksha_Firemage_Nmd`. The first of the timer-driven group to be ported,
and the boss whose pattern produced the correction below.

We ran an HP ladder at an invented 40/30/20/10 that delivered scalding
executors in growing bursts — one, then two, then three, then four. Retail runs
four battle timers instead:

- **9s** — Blazing Cut on the current target, the fight's steady beat.
- **7s** — carries three one-shot steps at **75/50/25**, each firing a burst on
  the first tick below it.
- **20s** — casts and spawns a **Torment Blaze** (282459). Nothing in either
  server spawned that NPC: it sat in npc_templates with a skill of its own and
  no way into the world.
- **25s** — the delivery rotation, sending the next executor in turn and
  thickening to several at once below 25% HP.

**Skill indices** follow our list order, corroborated by index 0 being the only
entry with a nonzero probability and the beat repeating it. The delivery tick's
second cast is index 4, beyond our four-entry list, so it is not reproduced —
noted rather than guessed.

**Verification.** Six pins in `TheFlamelordAiTests`, mutation-checked: delivering
all four at once instead of rotating, dropping the low-HP thickening, and never
spawning Torment Blaze each fail a test. Full suite 780, 1 skipped.

### Correction: most of what is left is timer-driven, not mis-numbered

An earlier revision of this document claimed the remaining bosses shared a
"sequence at one threshold" shape — that retail crossed one threshold and ran a
latched sequence, while aionemu spread that sequence across a ladder of invented
thresholds. **That was wrong.** It came from an audit that counted every
`is_hp_lower_than` in a pattern without noticing how many of them sat inside
battle-timer branches.

What the patterns actually show is that these fights are *timer-driven*. The
Flamelord is the clearest example: it reads as a threshold mismatch, and is
really four battle timers — a 9s attack beat, a 7s beat carrying three latched
HP steps, a 20s flame spawn, and a 25s delivery rotation that cycles through
spawn sets and thickens below 25% HP. Engineer Lahulahu has 29 battle-timer
branches, Empowered Agent 25, Unstable Triroan 24.

`audit_hp_phases.py` now reports the branch count and flags anything at ten or
above, because renumbering such a boss cannot match it. Of the 14 remaining
threshold mismatches, **12 carry that flag**. Together with the 7 regime-guarded
fights, that means essentially all remaining work is reimplementation on the
scale of Macunbello and Stormwing — writing the timers out — rather than editing
constants. The threshold class itself is close to exhausted.

### HP thresholds — Priest Preceptor (217581) and Gelkmaros Padmarashka (216580)

**Priest Preceptor**, pattern `IDArena_S7_Named_4`: retail steps at **80** and
**30**, where we had 75 and 25. The actions line up — a skill at the first step,
an add wave at the second — so this is a straight renumber.

**Gelkmaros Padmarashka**, pattern `DF4_Dramata`: her rock slides drop at
**10%**, not the 33% we had. The 5% berserk step stays; retail acts there too,
though with rocks rather than a berserk buff, so that step remains ours.

**Verification.** Build clean, full suite 1,002 tests passing.

### HP thresholds — Adjutant Anuhart (219357) and Icaronix the Betrayer (214598)

The first two entries off `audit_hp_phases.py`, both cases where retail has a
phase list of the same shape as ours and only the numbers differ.

**Adjutant Anuhart**, pattern `IDTiamat_Anuhart`. Three one-shot latched steps
at **70/40/22**, each casting the next of his escalating self-buffs — exactly
the structure our class already had, at an invented 50/25/10. Renumbered, and
the `HandleHpPhase` switch with it. The buff identities (20938/20939/20940)
stay as aionemu identified them: our curated skill list holds four entries
while the pattern casts indices 5–7, so the client list is longer and the
indices cannot be checked against our data; three consecutive ids for three
consecutive indices is at least consistent.

**Icaronix the Betrayer**, pattern `ND2_AhC_1`. One latched step at **75%**,
not 50: shout, spawn the successor at his own position, despawn himself —
structurally identical to what we already did. The successor's starting HP was
carried from 50 to 75 to keep the continuity aionemu evidently intended;
flagged in code as ours rather than the spec's, since retail's spawn sets no HP.

**Verification.** Build clean, full suite 1,002 tests passing.

### Server-wide — mute NPCs given their voices back

Retail ships many NPCs twice: one npc_id the world places and a near-identical
one that goes unused. Both carry the same `<ai_name>`, so both run the same
retail pattern, but our `npc_shouts.xml` frequently binds an encounter's lines
to only the twin nothing spawns — leaving the NPC players actually fight silent
for the whole fight. Hamerun the Bleeder was the first found; the audit turned
up 197 more groups in the same state, including Padmarashka, the Steel Rake
officers, Princess Karemiwen and Grand Commander Pashid.

Added 422 `<shout_npcs>` blocks covering 2,244 lines, each inserted into the
group whose `client_ai` it belongs to. The justification is that a shout group
is keyed by pattern name, not by npc_id: the lines belong to the AI pattern, and
the client confirms the live NPC runs it.

Scoping: each block takes the `restrict_world` of the map our spawn data
actually places that NPC in. Sixteen NPCs are summoned or code-spawned and have
no spawn-XML map; those use `restrict_world="0"` (global), which is equivalent
in practice since each exists in exactly one instance.

Two traps the audit now encodes, both found the hard way:

- An NPC covered by a broad catch-all group already speaks, so adding to its
  per-variant group would double every line. Brass-Eye Grogget sits in a
  400-npc_id block and needed nothing. 32 groups are in that state and are now
  reported separately as needing no action.
- Wildcard `client_ai` values such as `Station_Drakan[A-D]` cover several
  per-variant patterns at once, and are the reason those duplicates arise.

**Verification.** Diffed the file line-multiset before and after: zero original
lines lost, groups unchanged at 739, `shout_npcs` 983 → 1,405, shouts
3,794 → 6,038. Checked that no npc_id/world pair we introduced collides with an
existing binding. Audit re-run reports zero mute NPCs. Full suite 1,002 tests
passing.

### Haramel — Hamerun the Bleeder (216922)

Pattern `IDNovice_Hameroon` in `NpcAIPatterns_LDF4_PJW.xml`. Binding confirmed
from client data: both `216922` and `282040` carry
`ai_name = IDNovice_Hameroon`, and `216922` is the one our spawn data places in
Haramel (world 300200000).

**Retail behavior.** On aggro, arm a 10s battle timer and cast skill index 0 on
the target; every 10s thereafter recast it on the current target, deferring to
15s while under Sanctuary. The first drop below 50% HP — whether from a hit or
an enemy spell, latched by a one-shot flag — spawns one
`BIDNovice_HameroonSum_Brownie` and one `BIDNovice_HameroonSum_Ratman` within
5m for 180s, and self-casts skill index 1. Adds despawn on combat reset and on
death. Death spawns the class chest and exit portal and plays cutscene 457.

**Skill indices.** Index 1 = `19210 Hamerun's Hypnosis`, a self-target buff: the
pattern casts it on `OBJI_SELF` exactly when the adds appear, the adds are named
*brainwashed fighter* / *brainwashed mumu fighter*, and the `skill_no="2"`
(= index + 1) shouts corroborate. Index 0 = `17230 Ferocious Strike I`, the only
physical attack in the list, cast on the current target.

**Changes.**

- `ai/spawn_helpers.xml` — the 50% trigger now spawns **both** adds (was only
  `282041`) at distance 5 (was 2) and casts `19210` via `skillId`. `282042` had
  been dead data: a template spawned nowhere.
- `npc_skills/npc_skills.xml` — `17230` at `prob="100" cd="10000"` to approximate
  the 10s battle timer, replacing three skills at a flat `prob="25"`. `19210`
  removed from the random pool because it is now threshold-driven, and `19264`
  Ability Drain removed because the retail script never casts it.
- `npc_shouts/npc_shouts.xml` — added a `shout_npcs` block binding Hamerun's
  five barks to `216922`. They were bound only to `282040`, which never spawns,
  so the boss was silent. **Inferred**: no client-side shout table exists to
  confirm the assignment, but the `skill_no="2"` barks fire on skill index 1,
  which only the live boss casts.

**Not implemented.** The Sanctuary deferral (skip the cast, retry in 15s) and
the adds' 180s lifetime are not expressible in the current data model; both are
minor. The three Haramel named mobs (`216897`, `216907`, `216915`) additionally
have retail HP-threshold self-buffs, and `IDNovice_RatmanA` has a hit-and-run
(skill on target, flee 2s, self-buff on stopping) that needs a data-driven flee
mechanism the engine does not have.

**Verification.** `dotnet build` clean; full suite 1,002 tests passing. Not yet
observed in a running server.

### Kromede's Trial — the encounter cast

Patterns `Cromede_Named_Angry` (217006), `Cromede_Named_Scared` (217005),
`Cromede_Wife` (217000) and `Cromede_Assijudge` (217002) in
`NpcAIPatterns_LF4_minho.xml`. All four bindings confirmed from client
`ai_name`. World 300230000.

**Kaliga the Unjust (217006) — relic re-cast defect.** `19247`/`19248`
(Strength/Mana Relic Effect) sat in his skill list at `prob="25" cd="30000"`.
`KromedesTrialInstance` strips the matching buff when players destroy a relic,
but the generic AI simply re-cast it about 30s later, defeating the mechanic
entirely. Retail never casts them in combat: `Cromede_Named_Angry` absorbs one
at each of waypoints 2 and 4 during his scripted intro walk. Removed both from
the skill list.

**Lady Angerr (217000).** Retail summons all six bats in a single burst the
first time she falls below 70% HP, together with Protective Shield on herself;
`spawn_helpers.xml` had two bats each at 90/60/30%. Consolidated to six at 70%
with `skillId="16409"`. Skill list retuned to match the pattern: Strengthen
Armor (`16405`, self) once as combat opens, Fear Casting (`16704`, target)
every 20s, Protective Shield moved to the threshold. Index mapping corroborated
by shape — the pattern casts indices 0 and 1 on `OBJI_SELF` and index 2 on
`OBJI_CUR_TARGET`, and our list is exactly two self-buffs followed by a
target-side debuff.

**Shadow Judge Kaliga (217005).** Retail spawns five of his bloodwings
(`217111`) the first time he falls below 70% HP; nothing spawned `217111`
before, so the NPC was dead data. Added the summons entry and switched his
`ai=` from `aggressive` to `summoner`, without which `spawn_helpers` never
fires. No skill assigned at the threshold: his four listed skills do not line
up with the pattern's indices (index 0 is cast on the aggro target, but our
list starts with a self-heal), so the ordering is not trustworthy for this NPC.

**Justicetaker Wyr (217002).** Judge's Robes (`19286`) was gated
`max_hp="30" cd="40000"` — held back until he was nearly dead. Retail re-arms
that timer every 15s under `is_hp_in_boundary 1..100`, i.e. at any HP from the
opening of the fight. Now `prob="100" cd="15000"`. Identified by role rather
than index: the pattern casts index 5 on `OBJI_SELF`, our curated list has only
four entries, and `19286` is Wyr's only self-buff.

**Not implemented.** The scripted intro walk and its two relic absorptions (no
walker route exists for 217005/217006); the statue adds at 80/50% and the
votaic column drop below 50%; Wyr's and Angerr's escape-at-30% despawn; the
Shadow Judge's 3s flee at 30%; and Wyr's counterattack window. All need either
code or engine features (data-driven flee, HP-triggered despawn) that do not
exist yet.

**Verification.** Full suite 1,002 tests passing. Confirmed all 47 NPCs with
`ai="summoner"` have a `spawn_helpers` entry, since `SummonerAI` requires one.
Not yet observed in a running server.

### Beshmundir Temple — Macunbello (216245, and 216164 hard)

Patterns `IDCT_Boss_LichKing` and `IDCTH_Boss_LichKing` in
`NpcAIPatterns_TeCa_JM.xml`, plus `IDCT_SumLich` for the adds (281698 / 281775).
Bindings confirmed from client `ai_name`.

Neither server had an AI class for this boss. He cast four skills at a flat 25%
chance with no cadence, no phases, and no adds — the fight's entire structure
was missing. New `MacunbelloAI` and `MacunbelloSoulReaperAI` implement it:

- **10s Shockwave beat** on the current target.
- **Phase beat**, 10s: the first time each HP band is crossed (91/71/51/31/11%
  normal, every 10% hard), pull a random attacker and cast Absorb Energy of
  Darkness. Otherwise self-cast Tide of Darkness. Retail latches each band with
  its own flag variable, so falling past several at once still fires one per
  tick; `TryCrossBand` reproduces that.
- **Add waves**: two soul reapers above 50% HP every 30s, four below every 40s,
  at four fixed positions. Both modes spawn the normal-mode reaper; hard mode
  only uses its own variant for an on-hit proc that is not implemented.
- **The signature combo**: every 12s a reaper yanks a random player, curses
  them, and reports that player to Macunbello, who devours that exact player.
- Shield buff self-cast at spawn, and the Start/Wave/Devour/Die shouts.

**New sub-system — `Ai/NpcMessageBus.cs`.** Retail wires encounters together
with `broadcast_message` / `on_message`: an integer message type, an optional
object parameter, and a radius. It appears 6,820 times across the dump and has
no equivalent in aionemu, so every encounter built on it is missing. An AI
opts in by implementing `INpcMessageListener`. The reaper-to-boss handoff is
its first use.

**New helper — `Ai/NpcSkillCasting.cs`.** Patterns select skills by index and
say nothing about level, so a hand-written rotation must take the level from
the NPC's own `npc_skills` entry; the normal and hard bosses genuinely differ
(lv20 vs lv22). The convention for AI-driven NPCs is to leave their entries at
`prob="0"` so the list still defines levels while the generic random selection
fires nothing on top of the rotation. Applied to all four NPCs here; `19049`
was also added to 216245, which lacked the shield buff its own pattern casts.

**Not implemented.** Door control on aggro and reset — pattern door ids are
instance-local and we have no mapping to our door ids. The eight on-death
corridor markers. The "Macunbello leaves" failure path, which needs the
Lichkey/DespawnLich chain. Hard mode's 5%-per-hit add proc. Separately,
**216164 still never spawns**: tier selection lives in condition-spawn data we
do not have, so changing which variant `BeshmundirInstance` spawns would be
guesswork.

**Verification.** Build clean, full suite 1,002 tests passing. Confirmed all
434 distinct `ai=` names in npc_templates resolve to a registered `[AIName]`
handler, since `AIEngine.ValidateScripts()` hard-fails at boot otherwise.
Not yet observed in a running server.

### Beshmundir Temple — Stormwing (216183, and 216264 normal)

Patterns `IDCTH_Rudra` and `IDCT_Rudra` in `NpcAIPatterns_TeCa_JM.xml`.
`BeshmundirInstance` spawns 216183; 216264 is never spawned.

No AI class existed, so his signature mechanic was entirely absent — he never
summoned a single twister, and all four twister NPCs (281794 root, 281796
sharp, plus the 281795/281797 elite variants) sat in npc_templates spawned by
nothing. New `StormwingAI`:

- **Band timer**, 10s: seven HP bands (95/80/65/50/35/20/5), each firing once,
  calling Threshing Wind down on himself and summoning four twisters —
  alternating between the four diagonals at ±10 and directly on top of him.
- **Escalation timer**, 30s below 50% HP: four waves of the elite variants,
  sharp twice then root. Retail uses the elite ids here even in normal mode.

**Skill indices.** Corroborated by index 3: the pattern casts it on an attacker
only below 50% HP, and our entry for Dragon's Quake (`18616`) carries a
matching `max_hp="45"`. Only Threshing Wind (`18613`) is AI-driven and set to
`prob="0"`; the other four keep their probabilities, standing in for the retail
rotation timers this class does not reproduce.

**Shouts** are left to `npc_shouts.xml`, which already carries this NPC's
START/ATTACK_K/DIED lines and fires them through `NpcShoutsService` — an AI
broadcasting them directly would double them up.

**Not implemented.** Retail's two skill-rotation timers, whose branches are
flag-gated chains, and the exit portal spawned on death and on reset.

**Verification.** Build clean, full suite 1,002 tests passing, all `ai=` names
resolve. Not yet observed in a running server.

### Rentus Base — Captain Xasta, first form (217309)

Pattern `IDYun_Nmd3` in `NpcAIPatterns_IDYun_hue.xml`. His second form (217310)
shares the AI class but runs its own pattern and is untouched.

The class existed, and almost nothing in it was his. It ran a 28s cycle that
stopped him attacking, walked him along walker `B186C8F4…` and two helper
walkers, summoned two Inhibitor Sikars (282604) at fixed coordinates and ended
in a "sanctuary event" that re-acquired his target. The pattern has no walking,
no Sikars and no sanctuary: `on_enter_attack_state` arms two battle timers and
that is the whole fight. Meanwhile both of the NPCs the pattern *does* spawn
sat in npc_templates spawned by nothing.

Rebuilt to the pattern's two timers, both starting at 6s:

- **Beat**, re-arming every 9s (branch `Blaze`): self-cast index 0, then
  `spawn_on_target` three Magic Flames (282390) on the current target within
  4m, each living 15s. The flames are the damage; the cast is what leaves them.
- **Summons**, re-arming every 6s: four one-shot steps at 85/65/45/20, each
  sending one siege artilleryman (282606) within 5m of him.

**One step per tick.** The four wave branches are a single priority chain, each
gated by a `set_flag_var` test-and-set in its *conditions*, so a burst that
takes him from full health to 10% does not summon four at once — the 85 branch
matches on the next tick, the 65 branch six seconds after that, and so on.
`stepsTaken` reproduces that rather than summoning per threshold crossed.

**Skill index.** Index 0 resolves to Dragon Breath (`19657`): the branch is
named `Blaze`, the skill's stack is `IDYUN_RASTA_BLAZE`, and the branch spawns
`IDYun_3Nmd_Blaze`. Its target is `OBJI_SELF`, not the tank. Index 1 is
Interception Soldier Shout (`19968`, stack `IDYUN_RASTA_SANCTUARYSHIELD`) — the
shield the invented sanctuary event applied; no branch casts it, so both
entries go to `prob="0"` and it stays listed but silent.

**A devname that lies.** `IDYun_Rasta_Sum_Invisible` is not invisible: 282606 is
a named level-60 "siege artilleryman" with a real `name_id`. This is why
`audit_skill_index_reach.py` judges controller NPCs by `name_id="350000"`
rather than by devname.

**Kept.** His three broadcast messages and the on-death spawn of 217310 at
fixed coordinates, which already matched the pattern's `SPAWN_LOCATION_MY_POINT`
closely enough to leave alone.

**Not implemented.** `on_wake_up`'s `InvisibleWall2` spawn variable, and the
`do_nothing` guards that suppress reactions while walking a waypoint — we no
longer walk him, so there is nothing to suppress.

**Verification.** Build clean, full suite 1,039 tests passing and 1 skipped, six
new pins in `CaptainXastaAiTests`, each checked against a mutation of the
behaviour it covers (period, target, flame count, flame position, flame
lifetime, step latching, reset cleanup, death cancellation — all eight caught).
Not yet observed in a running server.

### Correction: the portability gate was counting duplicates

`audit_skill_index_reach.py` compared a pattern's highest `SKILLI_INDEX_n`
against the number of `<npc_skill>` *entries* we list for that NPC. Many of our
lists are aionemu chain constructions rather than flat skill lists -- Tahabata
Pyrelord has fifteen entries built from nine distinct skills, with 18225 repeated
four times across four `chain_id` sequences -- so the count was inflated and
bosses looked resolvable when they were not. Counting distinct skills instead
takes the cleanly-portable set from 32 to 27, dropping Tahabata, Dark Poeta's
Calindi, Ahserion and Hyperion among others.

**The gate is necessary, not sufficient**, and it is worth being blunt about
that. Passing it means our list is long enough to hold the indices a pattern
names. It says nothing about our list being in retail's order, and for a
chain-built list there is no reason it would be. Every index written down as a
skill needs its own corroboration -- the branch comment, the skill's stack name,
the `skill_no` in npc_shouts, or what the branch spawns alongside the cast. One
index resolved that way is worth more than a whole rotation assumed from
position.

### A runtime for translated patterns

Hand-porting bosses one at a time was not going to reach the end of this. Of the
805 retail adds our server never spawns, **427 belong to timer-driven bosses** —
NPCs that arm a battle timer on entering combat and let each timer branch arm the
next, so the fight is a chain of timers whose links are chosen by the boss's
current health regime. Captain Xasta is the small version of that shape; Tahabata
Pyrelord runs four regimes across nine timer slots.

`src/Aion.GameServer/Ai/Pattern/` runs the structure once so each boss is a table:

- **`AiPattern.cs`** — the table types and the `When` / `Do` vocabulary, named
  after the pattern ops they translate, so a table reads against the digest from
  `summarize_pattern.py`.
- **`PatternAi.cs`** — thirty battle-timer slots, thirty-two flag vars,
  first-match-wins branch evaluation, and spawn-id groups for `despawn`.

**The runtime decides nothing.** Skill indices, npc ids, coordinates and message
ids are resolved per boss in that boss's table, where the reasoning can be cited
here. A boss whose indices cannot be resolved does not get a table with guesses
in it — see the skill-index problem above.

**Rules worth stating, because they are easy to get subtly wrong:**

- Conditions short-circuit in the order the pattern writes them. A test-and-set
  guard behind a failing one must not consume its flag, or the step it protects
  is lost outright rather than delayed.
- Only a branch can re-arm a timer slot, so a tick matching nothing ends that
  chain. This is why patterns carry a low-priority catch-all re-arm, and why one
  missing from a table quietly stops the fight.
- Arming a slot replaces it. If re-arming stacked, a self-re-arming chain would
  double its own rate every pass.
- Battle timers only run in combat, and a reset clears the flags so a boss that
  resets replays its steps from the top.
- A spawn's `live_time` belongs to the spawned NPC, not the spawner, so it is not
  cancelled when the spawner dies — tying it to the spawner strands every add
  whose group no branch despawns.

**Verification.** Captain Xasta was re-expressed as a table and his six existing
pins — written against a hand-rolled implementation — pass unchanged, which is
the evidence that the runtime reproduces a boss whose behaviour was already
verified. Eleven further tests cover the runtime's own rules, and all twelve
mutations tried against them were caught (branch order, first-match-wins,
condition short-circuit, flag consumption, flag reset, slot replacement, the
in-combat guard, spawn-id despawn, spawn lifetimes).

### Dragon Lord's Refuge — Tiamat's three incarnations (219365, 219366, 219368)

Patterns `IDTiamat_T1_Crack_Key_Named_60_Al`, `..._Gravity_...` and
`..._Crystal_...` in `NpcAIPatterns_Tiamat_hue.xml`. The first boss translated
with the pattern runtime rather than by hand.

All three are the same fight with a different element. Retail arms three timers
on entering combat:

- **3s, re-arming every 9s** — a power attack that leaves one hazard behind.
  Fissurefang drops it on the tank, Graviwing on a random attacker, Petriscale
  on everyone in 50m.
- **15s** — an area attack that drops a hazard on **every** target in 100m,
  re-arming at 25s (30s for Graviwing).
- **20s** — a bind on a random player, but only below 30% health. Above that a
  catch-all re-checks every 3s; once it fires it goes onto its own 30s cadence.

What this replaces was invented: two hazards on two random players within 30m,
every 30s, on a cycle that started when the boss **activated** rather than when
anyone fought it, and kept running between pulls. The hazard ids were right; the
mechanic that placed them was not.

**Skill indices** are corroborated by stack name, and they are a good example of
why position is not enough: our list runs 20105, 20145, 20146, breath, while the
pattern's indices are 0 PowerAtk, 1 AreaAtk, 2 HandBind, 3 breath. Reading index
0 off our list's first entry would have given Bite instead of Smash. The stacks
settle it — `LDF4B_TIAMATAVATAR_POWERATK` is 20145, `..._AREAATK` is 20146,
`..._HANDBIND` is 20105 — and the branch comments name the same three.

**One deliberate divergence.** The `on_die` branch spawns two closing effects and
then despawns the spawn id it just filed them under, which read literally deletes
them a line after creating them. The effects are given their own id so both
halves of the branch do something; retail's action order is otherwise kept.

**Not implemented.** Index 3, the breath: its branch fires on `on_message` 71
from Tiamat, and that message chain is not translated. The skill keeps its
npc_skills probability, so it still appears. The hard-mode twins (236278/236279/
236281 and the 856xxx set) bind to their own `IDTiamat_Hard_*` patterns and keep
the behaviour they had. Fissurefang's hazard is also meant to engage its target
on arrival with a large hate bonus; we leave that to the add's own AI.

**Verification.** Full suite 1,061 passing and 1 skipped, eleven new pins. Six of
seven mutations caught; the survivor — dropping the `despawn` from `on_die` —
survives because the class's existing `HandleDespawned` already deletes those npc
ids by hand, so two mechanisms cover it. Not yet observed in a running server.

### Inggison — Omega (216516)

Pattern `LF4_FieldRaid` in `NpcAIPatterns_LF4_minho.xml`.

His fight is four waves of clones summoned onto whoever he is fighting, and each
wave **replaces** the one before it — the branch that summons the next wave
despawns the previous wave's spawn id in its first action:

| HP | wave | clears |
|---|---|---|
| 85 | 3× clone of power (281945) | — |
| 65 | 3× clone of explosion (281946) | the power wave |
| 45 | 3× clone of healing (281947) | the explosion wave |
| 25 | 1× magical barrier (281949) + 1× physical barrier (281948) | the healing wave |

Ours came from `ai/spawn_helpers.xml` and differed in four ways at once: the
thresholds were 80/60/40/20, nothing was ever cleared so all four waves piled up,
the last wave was three physical barriers instead of one of each, and the clone
of magical barrier was consequently spawned by nothing anywhere in the server.
His `spawn_helpers` entry is removed, since his summons now come from the
pattern.

**Skill rotation not translated, deliberately.** The pattern addresses thirteen
indices against our fourteen skills and its branches carry no comments, so
nothing corroborates the mapping — this is precisely the case the skill-index
rule above exists for. The two casts that accompanied the old summons
(`19189`, `19191`) stay on the summon branches where they already were, and the
rest of his casting keeps its npc_skills probabilities. His health regimes
(86-100 / 66-85 / 46-65 / 26-45 / below 25) are recorded here for whenever the
indices can be resolved.

**Verification.** Full suite 1,066 passing and 1 skipped, five new pins, all
seven mutations caught — both changed thresholds, the wave rotation, the closing
pair, the one-shot latch, the heartbeat re-arm, and the cleanup on death. Not yet
observed in a running server.

### Inggison — Omega's clone of physical barrier (281948)

Pattern `LF4_FieldRaid_SumD`, the other half of the Omega encounter.

Left alone it does not die quietly: at **10% health it detonates**, casting Self
Destruct, leaving a self-destruct effect (281952) and a soul essence (281764)
behind for ten seconds, and removing itself. Killed outright it still leaves the
soul essence. None of that happened here, and neither NPC was spawned by anything
in the server.

**Skill index 1 is anchored hard** and is the only one translated: its branch
fires at 10% immediately before `despawn_self` and alongside the self-destruct
spawn, and our 19196 is named Self Destruct with the stack
`BNFI_AREABOMB10_LFRAID_SUM` — the 10 is the threshold. Indices 0 and 2 have no
such anchor: one fires once each at 70% and 35%, the other every 20s, and our
remaining two skills (Protective Wave, an attack; Enervating Wave, a debuff) fit
either role about equally. Both keep their npc_skills probabilities rather than
being placed on a coin flip.

**Kept, and not retail:** the Magic Ward (18671) it holds on Omega. Retail applies
that shield from somewhere in Omega's own unresolvable rotation; ours has the
clone apply it, and it is what makes killing these clones worth doing. One bug
this port would otherwise have introduced: `despawn_self` deletes without killing,
so `HandleDied` does not run — a clone that detonated would have left its shield
on Omega permanently. The removal now hangs off despawn as well.

**The rally chain is now translated** (see the message-bus section below): Omega
broadcasts 6354 on every phase naming whoever he is fighting, and this clone puts
hate on that player and turns to attack. The clone of magical barrier (281949)
binds to its own sibling pattern and runs `aggressive`, so it hears nothing.

**Inferred, and marked as such.** The pattern's `add_hate_point` carries no
amount. 1000 is a judgement: enough to make the named player the clone's target
on arrival, not so much that nothing can pull it off them afterwards.

**Verification.** Full suite 1,071 passing and 1 skipped, five new pins, all seven
mutations caught after two test fixes. Not yet observed in a running server.

### Empyrean Crucible — Queen Alukina (217590)

Pattern `IDArena_S8_Named_3` in `NpcAIPatterns_IDArena_JM.xml`. Two corrections,
neither of which needs an index resolved.

**Phase steps 75/50/25 → 80/55/25.** Read off the boundaries her once-only
branches guard: the `ALPHA_1` branch is gated on 56-80, `ALPHA_2` on 26-55, and
`ALPHA_3` on below 25. Her per-phase casts are unchanged and stay where they
were.

**She bursts into seven azure blobbles (280713) when a player kills her**, each
lasting thirty seconds. Nothing in our server spawned that NPC. The pattern hangs
it on `on_killed_by_user` rather than `on_die`, so it belongs to being killed and
not to any despawn — it is in `HandleDied` and deliberately not in
`HandleDespawned`.

**Rotation not translated.** Seven indices against our seven skills, no branch
comments, nothing to corroborate the mapping. Her retail regimes, for whenever it
can be: 81-100, 56-80, 26-55, below 25, with a 20s low-health chain on timer 6.

**Verification.** Full suite 1,073 passing and 1 skipped, two new pins, all five
mutations caught — including moving the first step by a single point.

### Idgel Dome — Destroyer Kunax (287249)

Pattern `IDLDF5_Fortress_Re_Vritra_01`. The simplest shape in the corpus and the
clearest win: his entire fight is **one fixed chain of eight skills, ten seconds
apart, looping forever**. Each timer branch arms the next slot and the eighth
arms the first again.

Ours ran the same eight skills off `prob="100"` entries with per-skill cooldowns,
so neither the order nor the spacing was fixed — and the NPC his last step drops
on the tank, kunax's wrath (855009), was spawned by nothing at all. Every
probability is now 0, since the chain drives them.

**The index mapping is positional**, which is only defensible here because three
independent things agree with it:

- step 7 casts Aether Prison, and the pattern spawns an NPC named *kunax's wrath*
  on that same step;
- steps 3 and 4 are the only two cast at `OBJI_SELF`, and they land on Cleaving
  Massacre and Butcher's Sweep — both sweeps, both plausibly centred on the
  caster;
- the list is exactly eight entries against exactly eight indices, with no
  duplicates and no chain construction.

The one loose end is step 0, Ide Scale: a self-buff the pattern casts at the
current target. Our data had it as `is_post_spawn`, which is left alone, so it is
now both applied on spawn and re-applied once per cycle.

**Verification.** Full suite 1,077 passing and 1 skipped, four new pins, all six
mutations caught — step spacing, opening delay, two steps swapped, the self-cast
targets, the wrath spawn, and the loop back to step 0. Not yet observed in a
running server.

## What is left, and what each part costs

Measured against the 5.8 dump as of the Destroyer Kunax commit. Regenerate any of
these numbers with the tools in `tools/client-extract/`.

### Adds: 779 retail adds our server never spawns, across ~500 encounters

| Bucket | Count | What it needs |
|---|---:|---|
| timer-driven | 409 | a table on `PatternAi` for that boss |
| death / despawn | 107 | the encounter's instance handler |
| `on_wake_up` | 58 | spawn-time setup, usually a condition variable |
| `on_enter_attack_state` | 53 | an opener on the boss's table |
| `on_message` | 47 | the NPC message bus, both halves together |
| **blocked: waypoint-placed** | 46 | **nothing — server-side paths we do not have** |
| `on_idle_timer` | 19 | out-of-combat behaviour, no runtime support yet |
| hp threshold at spawner | 9 | `ai/spawn_helpers.xml`, data only |
| hp threshold, fixed position | 8 | an AI class or instance handler |
| **blocked: shared absolute coords** | 6 | **nothing — see the section above** |
| on hit / spell | 6 | an AI class |
| other | ~20 | assorted single cases |

So **52 of the 779 are permanently blocked** and the rest are work.

### Bosses: 27 cleanly portable timer-driven bosses, 6 done

Done: Captain Xasta, Stormwing, Tiamat's three incarnations, Omega, his clone of
physical barrier, Queen Alukina (partial), Destroyer Kunax.

Remaining, roughly cheapest first by pattern size — `top index` vs `our list` is
the resolvability gate, `timers` is the size of the job:

| Class | npc | timers | note |
|---|---|---:|---|
| `VirhanaTheGreatAI` | 216165 | 12 | 3 skills, no spawns |
| `EternalBastionAggressiveNpcAI` | 230744 | 11 | no spawns |
| `EternalBastionAssaulterNpcAI` | 230745 | 17 | no spawns |
| `ShieldNpcAI` | 260207 | 17 | 1 add: ice sheet (295074) |
| `PopuchinAI` | 217373 | 20 | adds already covered |
| `CelestiusAI` | 215488 | 20 | 4 spawns |
| `MonolithicAmbusherAI` | 216215 | 21 | no spawns |
| `EternalBastionDragonAI` | 231131 | 22 | 1 spawn |
| `WarriorPreceptorAI` | 217578 | 23 | no spawns |
| `DorakikiTheBoldAI` | 216169 | 24 | no spawns |
| `DredgionCommanderAI` | 251383 | 33 | no spawns |
| `SilikorofMemoryAI` | 214668 | 35 | 11 spawns |
| `PazuzuAI` | 216951 | 39 | 27 spawns |
| `EngineerLahulahuAI` | 215080 | 54 | 17 spawns |
| `RakshaAI` | 217475 | 73 | 12 spawns |
| `InfernalDynatoumAI` | 234686 | 94 | 13 spawns |
| `BrassEyeGroggetAI` | 215081 | 110 | 10 spawns |
| `KaluvaAI` | 216950 | 37 | **waypoint- and message-driven; low value** |
| `FortressInstanceDukeAI` | 233632 | 11 | **shared absolute coords; blocked** |

**95 further timer-driven bosses reach past our skill list** and cannot have
their rotations translated at all. Their *spawns and thresholds* are still
portable — that is how Omega and Queen Alukina were done — so they are worth
revisiting for structure even though their casts are out of reach.

### Known limits of the tooling

- The spawn sweep does not follow an id returned from a method, so a handler that
  picks its add out of a `List<int>` helper still reports as missing. Tiamat's
  incarnations do exactly that; spot-check before acting.
- `audit_skill_index_reach.py` is a necessary and not sufficient gate — see the
  skill-index section.
- Nothing here has been observed in a running server. Every entry in this log is
  verified by build, test suite and mutation checks only.

### Runtime features not yet built

Needed by the buckets above, in the order they would unblock the most work:

1. **`on_message` / the NPC message bus** — 47 adds. `NpcMessageBus` exists but
   `PatternAi` has no `OnMessage` hook, and both halves of a chain must be
   translated together (Omega broadcasts 6354; his clones respond to it).
2. **`on_idle_timer`** — 19 adds. Out-of-combat timers; the runtime deliberately
   only runs battle timers.
3. **`on_wake_up` and condition spawn variables** — 58 adds, most of which are
   instance-state plumbing rather than combat.
4. **`attack_target_after_spawn` / `hatepoints_to_add`** — several bosses spawn an
   add that should immediately engage with a large hate bonus. Currently left to
   the add's own aggressive AI.

### Beshmundir Temple — Virhana the Great (216165)

Pattern `IDCTH_Boss_StatueDrakan`. Two independent timers, and ours had them
crossed.

- **From 12s**, a self-centred Earthly Retribution, re-arming at 15s normally, 8s
  on a 15% roll and 30s on a 10% roll.
- **At 70s, once**, Blade of Lunacy on the tank *and* on a second player, opening
  a 10s chain that casts it and switches him to someone else each time. The chain
  re-arms itself and never stops.

What it did before: nothing but the opening buff for seventy seconds, then
*Earthly Retribution* on the ten-second chain where Blade of Lunacy belongs,
twelve times, then the seventy-second wait again. The fifteen-second chain did
not exist and neither did the target switching.

**All three indices are corroborated twice.** By role: index 2 is the self-cast
on entering combat and 19121 is Seal of Reflection, a buff; index 1 is cast at
`OBJI_SELF` and 18897 is Earthly Retribution, an attack — so a sweep centred on
him; index 0 is cast at the current target and at a second player, and 18602 is
Blade of Lunacy. And independently by our own previous code, which already used
19121 as the opener and 18897 as the repeated cast. All three probabilities go to
0 now the pattern drives them.

**One quirk kept.** The two probability branches test their roll *before* the
timer indicator, which is the order the pattern writes them, so the table does
too.

**Not implemented.** His three shouts, and the `control_door` on death — the
instance handler owns the door.

**Verification.** Full suite 1,082 passing and 1 skipped, five new pins, all six
mutations caught: the missing sweep chain, the chain interval, the chain failing
to repeat, the paired opener, the two skills swapped, and the 70s delay.

### Fortress field generator — the ice sheet (260207)

Pattern `LGuard_Shield`. **Only one mechanic taken, on purpose.**

Once the generator falls below 35% it drops an ice sheet (295074) on whoever is
attacking it — one time, within two metres, lasting ten minutes. That NPC was
spawned by nothing in our server. Retail hangs the check on a timer armed at 20s
and re-checked every 15s, so the sheet is not instantaneous, and our
implementation keeps that.

**Why the rest is not translated.** This is siege infrastructure rather than an
encounter: `ShieldNpcAI` extends `SiegeNpcAI` and exists to raise and drop the
fortress shield, and the pattern's nine skill indices have nothing to corroborate
them against our nine skills. Restructuring a class that live sieges depend on,
on the strength of an unresolvable rotation, is not a trade worth making. Adding
the one mechanic that needs no index is.

**One design note.** Retail's timer 0 re-arms for the whole fight and it is the
flag var, not the timer stopping, that holds the sheet to one. Cancelling the
check on the drop would have been equivalent in effect but would have made the
flag decorative — and a mutation removing the flag then went undetected. The
check now keeps ticking, as retail's does, and is cancelled only on death,
despawn and reset.

**Verification.** Full suite 1,085 passing and 1 skipped, three new pins, all five
mutations caught after that change: the threshold, the one-shot flag, the
twenty-second delay, the sheet's position, and the cancellation on death.

### The NPC message bus, wired into the pattern runtime

`on_message` is the single largest unbuilt feature in the backlog — **47 missing
adds** depend on it — so it is built now, and proven on the one chain that was
already fully researched.

Retail wires the NPCs of an encounter together with `broadcast_message` and a
matching `on_message` handler: an integer chosen per encounter, an optional
object parameter, and a radius. `NpcMessageBus` already existed; what was missing
was a way for a translated pattern to send or receive on it. `AiPattern` gains an
`OnMessage` branch list, `When.Message`, `Do.Broadcast` and `Do.HateMessageTarget`,
and `PatternAi` implements `INpcMessageListener`.

**One rule differs from battle timers, deliberately.** Messages are handled
whether or not the listener is in combat. Retail uses them to *start* fights as
well as to coordinate them, so a listener that ignored them out of combat could
never be pulled by one.

**Both halves must be translated together.** A broadcast nothing listens for, and
a listener nothing broadcasts to, are both silence — and neither shows up as a
failure. Omega and his clone of physical barrier are the worked example: he
shouts 6354 on each of his four phases naming his current target, and the clone
answers by hating that player, so a wave arrives aimed at the tank instead of
wandering to whoever hits it first.

**Verification.** Full suite 1,086 passing and 1 skipped. Five mutations, all
caught: the broadcast removed, the broadcast naming nobody, the listener removed,
the listener watching the wrong number, and the runtime dropping the message's
parameter.

### Adma Stronghold — the coffins and Lord Lannok (280942/280950/281055-58, 214696)

Patterns `NoAction_CoffinA`..`F` and `Adma_DeathknightNamed`. The first encounter
translated on the message bus, and a good demonstration of why both halves have
to be done together.

**Disturbing a coffin is meant to be a mistake.** The first hit makes the coffin
shout message 6609 naming whoever landed it, within 50m; Lord Lannok hears it and
comes for that player. When he dies he calls the all-clear (6601, 100m), which
sends the coffins' skeletons away and re-arms their alarms. Before this the
coffins were plain aggressive NPCs, nothing listened to them, and Lannok treated
them as scenery.

**Retail's two-branch idiom, kept.** The all-clear is handled by two branches for
the same message: the higher-priority one is guarded by `unset_flag_var` so it
clears the alarm flag on its way past, and the lower one runs once the flag is
already clear. Both despawn the wave, so the despawn happens either way and only
the flag differs. The runtime reproduces this directly.

**Not translated: the skeleton waves.** Each coffin spawns a skeleton — or a
skeleton mage, on a 15% or 30% roll depending on which message arrived — at its
own fixed coordinates, on messages 6602/6603/6604. Those three messages come from
`ND2_FhWSumA/B/C` (281045/281046/281047), invisible controllers that broadcast on
waking. Nothing in our server spawns them, and `on_wake_up` is not implemented in
the runtime, so this is blocked on two things at once. The despawn branches are
written and correct; they currently clear an empty group. **This is the 12 adds
in the on_message bucket that belong to Adma.**

**Also not translated.** Lannok's three escalating shouts on message 6608, whose
sender appears in no pattern of this encounter; and the two NPCs his death spawns
(an invisible skeleton-despawner and a treasure box).

**A known divergence.** Retail's alarm flag survives the coffin leaving combat and
is cleared only by the all-clear. Ours is cleared by the runtime's reset, which
every boss depends on for replaying its steps, so a coffin that de-aggros will
shout again where retail's would stay quiet. Not worth a per-pattern opt-out for
one stationary object; recorded here instead.

**Verification.** Full suite 1,090 passing and 1 skipped, four new pins covering
both NPCs as one mechanic. Five of six mutations caught; the survivor — removing
the one-shot flag from the shout — survives because `PatternAi` already latches
`on_enter_attack_state` to once per fight, so the flag is redundant *for the
shout* while remaining load-bearing for the all-clear.

### `on_wake_up`, and Dragon Lord's Refuge — Wrathclaw (219367)

`on_wake_up` is the second runtime feature from the backlog — 58 missing adds
name it. It runs once when the NPC enters the world, before anyone has touched
it, and is hooked to `HandleSpawned`. Encounters use it to put their furniture
out: spheres players must run between, the controllers that drive an add wave,
the condition variables an instance reads later. It is not a combat event and
does not wait for one.

**Wrathclaw had no AI at all.** His template pointed at plain `aggressive` while
his three siblings shared `TiamatsIncarnationAI`, so the fourth incarnation of
Tiamat simply auto-attacked. His pattern is the same family shape — a 9s power
attack, an area attack, a bind gated at 30% — plus one mechanic the others do not
have:

- On spawning he places a **sphere of wrath** (282979) and a **sphere of peace**
  (282733) at two fixed points, 214/858 and 185/838.
- Every area attack despawns both and puts a fresh pair out. On a 34% roll they
  go back where they were; otherwise **they come back swapped**. Players have to
  find the right sphere, and where it is keeps changing.

Neither sphere was spawned by anything in our server.

**Indices carry over from his siblings** with the same corroboration: identical
branch comments (`PowerAtk`, `AreaAtk`, `HandBind`) against the same three stack
names, so 0 is Smash, 1 is Incarnate Surge and 2 is Bite. Index 3 fires only on
`on_message` 71, which is untranslated, so it needs no resolution — which is just
as well, since his two remaining skills (20165, 20166) have nothing to separate
them.

**Only one NPC binds to this pattern**, so its absolute coordinates are safe by
the rule above.

**Verification.** Full suite 1,093 passing and 1 skipped, three new pins, all five
mutations caught — the wake-up removed from the runtime, the wake-up branch
removed from the table, the swap made a no-op, the despawn dropped so spheres
pile up, and the two spheres exchanged.

Missing adds: 780 → **776**.

### Correction: the sweep now follows computed ids, and the total went up

`TiamatSkillHelperAI` spawns `GetNpcId() + 1` — which is how every "infinite
pain" and "sinking sand" damage twin reaches the world. Nothing at the call site
names those ids, so all three read as never spawned, and this was the fifth
indirect-spawn idiom the audit had missed (after `RndSpawnInRange`, named
constants, id arrays, and locals). The sweep now resolves it, using the AI name
off the class and the npc_ids pointing at that name in npc_templates.

**The count rose from 776 to 786, and that is the fix working.** The missing-add
total is not monotonic: an NPC that becomes spawnable brings its own pattern's
adds into scope for the first time. Three twins dropped off the list and ten
previously-invisible adds appeared behind them. A rise after a sweep fix means
the tool is seeing more of the world.

**What this run cost, and what it bought.** Four of the six wake-up candidates
examined turned out to be already implemented — `TiamatSkillHelperAI` covered
three, and `CelestiusAI`, checked in an earlier pass, covered its own. That rate
is the argument for spot-checking every finding against the code before writing
anything, which is now the documented rule. The genuinely missing ones from that
group are the two Calindi surkana twins (730697, 730698) and the Drakenspire
exploding flame (856459).

### Effect objects are not adds

Chasing the two Calindi surkana twins turned up a third class of false positive.
`IDTiamat_Temp12` spawns `IDTiamat_FOBJ_GroundSurukana_3` — "FOBJ", field object
— and its despawn branch is commented `Despawn_noshownpc`. Two independent
signals from the designers that this is scenery. Our template calls the same NPC
an `ELITE`-rated `MONSTER` named "surkana", so `is_real_combatant` passed it, and
spawning it would have added a live elite combatant to the Calindi fight.

The audits now exclude adds whose **devname** says they are effects: `fobj` or
`noshow` anywhere, or an `_fx` suffix. 16 adds drop out; 786 → **770**.

**Two markers are deliberately excluded**, and both are traps:

- `invisible` — Captain Xasta's summon is `IDYun_Rasta_Sum_Invisible` and is a
  perfectly visible level-60 siege artilleryman. Filtering on it would have
  discarded one of the first real mechanics this audit found.
- `_dmg` — `BLF3_NM_DMGhostPrSum2_49_Ae` matches by accident; it means "DM
  ghost", not damage.

The general lesson, now three idioms deep: the template is not authoritative
about what an NPC *is*. The devname and the branch comment are the designers
talking, and they are usually right where our data is wrong.

### Rentus Base — Vasharti's dancing flames (217313)

Pattern `IDYun_Nmd6`. His reflect alternates between two colours on a 40s timer —
`Ref_Blue` on a 35% roll, `Ref_Red` otherwise — and players have to stand in the
matching flame. **Neither flame was ever spawned.** The mechanic had no board to
play on.

`on_enter_attack_state` lights a dancing red flame (282996) at 167.6/418.22 and a
dancing blue flame (282997) at 208.58/410.71, and `on_leave_attack_state` and
`on_die` put them out. `DancingFlameAI` was already written for them and had
never been given anything to drive; the class's existing `ClearSpawns()` already
runs on all three exit paths, so the cleanup came for free.

Only 217313 is live on this pattern — 855903 binds to it but is spawned nowhere —
so the fixed coordinates are unambiguous under the shared-pattern rule above.

**Still not translated.** His three Glove Controllers at 86/56/26 are placed by
`on_arrived_at_waypoint`, so they are waypoint-blocked; and as noted earlier they
are plain aggressive clones of Vasharti himself in our data, so spawning them
would add three full-strength bosses rather than retail's controllers.

**Verification.** Full suite 1,094 passing and 1 skipped, one new pin, all five
mutations caught — flames never lit, both lit at one point, only one lit, left
burning after death, and lit before the pull rather than on it.

### Azoturan Fortress — Icaronix the Betrayer (214599)

Pattern `NLehpar_BhB`. **He had no AI at all.** `BetrayerIcaronixAI` spawns him
when the Deceiver hits 75% and then nothing drives him, so the second half of the
fight was a plain aggressive monster.

Retail has him call up a different servant as he loses ground, each on its own
spawn id so they accumulate rather than replace one another:

| when | servant |
|---|---|
| on the pull | Kuillus, 280937 |
| below 80 | Mudthorn, 280939 |
| below 50 | Pretor, 280938 |
| below 30 | Rottentree, 280940 |
| on death | a strange creature, 280941, for twelve seconds |

**All five were spawned by nothing anywhere in the server.** By the end of the
fight all four servants should be up at once; leaving the fight or dying clears
them.

**Rotation not translated**: five indices, five skills, no branch comments. The
timers those branches run on (2 through 6) are deliberately not armed either —
arming a timer whose branches do not exist only starts a chain that dies on its
first tick.

**Two test gaps this port exposed**, both found by mutation and worth recording
because they generalise:

- *Counting is not enough when a step despawns before it spawns.* Each summon
  clears its own group first, so a step that repeated every tick would delete and
  replace its servant and still leave exactly one. Only the servant's identity
  distinguishes them.
- *A cleanup test has to fill every group first.* Killing him with one servant up
  cannot notice a missing despawn for a servant that was never summoned, so the
  test now walks the whole ladder before killing him.

**Verification.** Full suite 1,099 passing and 1 skipped, five new pins, all seven
mutations caught after those two fixes. Missing adds 770 → **764**.

### Correction: summon-table owners were counted as spawnable

`ai/spawn_helpers.xml` uses the same attribute name twice: `<ai npcId="...">`
names the NPC that *owns* a summon table, and `<summonGroup npcId="...">` names
the NPC it summons. The sweep read both, so any NPC with a summon table counted
as live whether or not anything places it in the world.

Jurdin the Cursed is the case that exposed it. He looked like the largest
un-ported encounter left — eight missing adds, no AI class — and reading his
pattern was a genuinely rich fight: wave spawners at fixed points on four
one-shot steps, a shadow flung at the raid with 1.5 million hate, ten action
summons across five points. Then it turned out **he is spawned nowhere**: not in
any spawn file, not in any instance handler, not in any code. Only a summon table
and a console-command entry. His whole encounter is content our server does not
have, and porting his AI would have been writing a boss nobody can fight.

Counting only `<summonGroup>` removes him and the other phantom owners: 764 →
**754**, and the encounter count 489 → 486.

**This is the sixth correction to the missing-adds sweep**, and they have all been
real: `RndSpawnInRange`, named constants and id arrays, `GetNpcId() + N`, effect
objects by devname, and now summon-table owners. The number has moved 812 → 754
without a single add being ported by those changes alone. The instrument is worth
more than any one boss, because everything downstream is prioritised from it.

### Occupied Rentus Base — hard-mode Vasharti's illusions (236300)

Pattern `IDYun_Nmd6_Hard`. He shares `BrigadeGeneralVashartiAI` with the normal
version, which is why the dancing flames added in the previous entry already
reach him: the hard pattern lights them at the same two coordinates, in the same
room geometry, and only 236300 binds to it.

What hard mode adds is a pair of **illusions of himself**: a kiss of fire
(856338) and a kiss of ice (856339), conjured 23 seconds into the fight and again
every 75 seconds. Each stands beside the flame of its own colour — the hard-mode
twist on a fight that is already about picking the right one. Neither was spawned
by anything.

Their headings come from the pattern's `dir` in degrees, through the engine's own
`PositionUtil.ConvertAngleToHeading` rather than by hand.

**Shared classes need a guard, and a test for the guard.** The two Vashartis run
one AI class, so the illusion branch is gated on the hard-mode npc id and there is
a pin asserting the normal fight conjures none. Without it the easier version
would have quietly inherited a hard-mode mechanic, and nothing would have failed.

**Still not translated.** His three Glove Controllers (856351/856352/856353) are
placed by `on_arrived_at_waypoint` and are clones of Vasharti himself, exactly as
in normal mode — blocked twice over.

**Verification.** Full suite 1,101 passing and 1 skipped, two new pins, all six
mutations caught. Missing adds 754 → **752**.

### Positionable is not the same as implementable

Infinity Shard's Vritra callers looked like the biggest remaining prize: an
invisible controller (284675) that our spawn data really does place, whose
pattern rolls down ten equal-priority probability branches and summons one of ten
Hyperion defense troops. Ten adds from one small pattern.

Every one of those spawns carries `pathname=NPCPathVriAss_Path01`, and they all
spawn at **the same point**, 150.03/145.5/125.2. The troops exist in order to
march that path into the battle. We do not have it — those paths are server-side,
the same gap documented above — so implementing this would pile ten stationary
soldiers on one tile. That is worse than leaving them out, not closer to retail.

The audit now separates the three cases, because "positionable" was doing too
much work:

| | count |
|---|---:|
| fully self-contained | 661 |
| positionable, but walk a server-side path | 47 |
| blocked on waypoint placement | 44 |

so **91 of 752 are gated on path data we will never have**, not 44.

One incidental confirmation while checking this: the client's devname → npc_id
map has **no duplicates at all** across its two NPC files, so a devname always
identifies exactly one NPC. Where a resolved name looks wrong — these troops
carry Vritra devnames and our templates call them Hyperion defense — the mapping
is right and it is our display names that differ.

Two smaller notes from the same pattern, for whoever ports it if paths ever
arrive: its branches are all priority 2, so evaluation follows document order,
which the runtime preserves because `Of()` sorts stably; and it needs
`set_idle_timer` / `on_idle_timer`, still unbuilt, to remove itself two seconds
after firing.

### Unstable Splinterpath — Yamennes's portals (219555, 219563)

Patterns `IDAbRe_Core_NamedD_02` and `IDAbRe_Core_NamedD_Hard_02`. This one is a
bug fix rather than a missing mechanic, and the interesting part is what was
*already* right.

Retail alternates the portal floors using **one flag var toggled by two
branches**: the higher-priority branch is guarded by `set_flag_var` and opens the
upstairs gates, the lower by `unset_flag_var` and opens the downstairs ones, so
each firing flips which one can match next. `UnstableYamennesAI` had arrived at
the same alternation independently, with a boolean.

Three things were wrong:

- **The cadence.** Retail arms the portal timer at 30s and re-arms at 65s; ours
  waited a flat 60 both times.
- **The gates never expired.** Retail gives each `live_time=70`.
- **A wave only opened if no gate was still standing.** Combined with the missing
  lifetime that is a stall: a group that ignored the portals instead of killing
  them saw the first wave and **never another for the rest of the fight**. Retail
  spawns unconditionally and lets them time out. The 70s life against a 65s cycle
  bounds it at two overlapping sets for five seconds, which is retail's own
  behaviour.

**The gate npc_ids are deliberately unchanged.** Retail's patterns name
283203/283222/283223 upstairs and 283233 downstairs, and the audit reports all
four as missing adds. They are not: they bind to the *same* retail patterns as
the 219567/219579/219580 this class already spawns, and only ours carry the
`unstableyamenessportal` AI that makes a gate do anything. Swapping to the ids
the pattern names would replace working portals with inert scenery.

**A fifth class of false positive, and a rule for it.** Where retail names one id
and our data uses a sibling for the same role, an add can read as missing while
the mechanic is fully implemented. Testing for it: the add's *own* retail pattern
is bound by a small number of NPCs, at least one of which we do spawn, and that
sibling carries the same display name. On this corpus that flags **20 adds**. It
is a flag and not an exclusion — deciding it needed checking which of the two ids
actually had a working AI, which no heuristic can do.

**Not changed.** Retail's gate coordinates differ from ours by roughly ten metres
and about a metre in z. Ours look snapped to the floor and have presumably been
observed working; moving them risks putting a gate inside geometry for a
cosmetic gain. Recorded rather than applied.

**Verification.** Full suite 1,103 passing and 1 skipped, two new pins, all five
mutations caught after one test fix.

### The sibling flag, and a convention the audit depends on

The duplicate-id case from the previous entry is now reported rather than only
described. Each finding that has one carries
`[we spawn NNNNNN for this role -- check before porting]`, and the summary counts
them: **20 of 752**. The test is the one worked out on Yamennes's gates — the
add's own retail pattern is bound by at most eight NPCs, one of those is spawned
by us, and it carries the same display name. A flag and not an exclusion, because
resolving Yamennes meant checking which of the two ids had a working AI behind
it, and no heuristic here can do that.

**A convention fell out of this, the hard way.** The handler sweep finds
code-driven spawns by looking for calls to a method whose name *contains* `Spawn`.
Refactoring Yamennes's gate placement into a helper called `OpenGate` made three
gates the class demonstrably spawns invisible to the audit — the missing-add total
moved by six with no behaviour change at all, in the direction that looks like
progress. Renamed to `SpawnGate`, and the rule is now in
`tools/client-extract/README.md`: **a helper that places an NPC must be named
`SpawnSomething`.**

That is the second time a number has moved for a reason unrelated to the game, and
it is worth being blunt about what that means: this backlog is measured by a
regex over our own source, so it is sensitive to how our source is written. It is
good enough to prioritise from and not good enough to report as a fact without
looking.

## Specification: Tiamat's dying phase (219362, and hard 236277)

Pattern `IDTiamat_Tiamat_Dragon_Dying_Named_60_Al`. The richest un-ported
mechanic found so far, and the reason `rotation_table.py` exists: 45 timer
branches that read as a wall of XML and resolve to one legible idea.

**The phase is a breath that sweeps left, middle or right, and a beacon that
tells you which.** Each step spawns a beacon at 458.5/514.7/417.4 — Beacon1
facing 17° for left, Beacon2 unrotated for middle, Beacon3 facing 105° for right
— living 7 seconds, then casts the matching breath. As she weakens the chain
grows extra steps and speeds up.

| regime | chain |
|---|---|
| 76-100 | M, M, L, R — 18s apart |
| 51-75 | L, M, R, then three thorns 5s apart, then R, M, L |
| 26-50 | L, M, R at 12s, three thorns at 2s, a cyclops crack, then R, M, L, three thorns, another crack |
| 0-25 | as above at 8s, plus a gravity bomb after each crack and a quake closing the loop |

Timers run 0→1→2→…→16→0 within a regime; a regime change simply means the next
tick matches a different branch, which is why every branch is guarded by a health
band rather than a flag.

**What it needs that we do not have yet:**

- ~~`set_idle_timer` / `on_idle_timer`~~ — **built**, see below.
- `on_message` 10010, her countdown expiring, which makes her despawn.
- Nothing else: 17 timer slots against our 30, and the beacons are placed at
  fixed coordinates in a single-owner pattern, so the shared-coordinate rule is
  satisfied.

**What is not blocked but is worth knowing.** The beacons (283155/283156/283157)
carry no display name, so they never appear in the missing-adds count — they are
markers, not adds. The four adds the audit does report for this pattern
(283057 "burrowing attack" and three "tiamat") come from the thorn and crack
steps.

**Skill indices: partly resolved, and the encounter is not unimplemented.**
`TiamatWeakenedDragonAI` is 249 lines and already covers sinking sand, divisive
creations, infinite pain and the gravity crushers. What it does differently from
retail is the breath itself, and the differences are precise:

| | ours | retail |
|---|---|---|
| direction | `20922 + Rnd.NextInt(3) * 2` — **random every cast** | a fixed sequence, different per health regime |
| pacing | `OnEndUseSkill` re-offers immediately, so breaths run back to back | 18s at 76-100, 18/15s at 51-75, 12s at 26-50, 8s at 0-25 |
| beacon | none | one spawned with each breath at 458.5/514.7/417.4, living 7s |

She has **no `npc_skills` entry at all**, so the pattern's indices cannot be read
off a list — but our own three Ultimate Atrocity skills line up with the three
directions, and one of them is certain: **20924 is the middle breath**, because
the NPCs it spawns run along y=514.6 and the beacons are placed at y=514.7, the
same centre line. 20922 spawns around y=543-550 and 20926 around y=480, so they
are the two sides; which of them retail calls left is *not* established, and the
beacon headings (17° and 105°) do not settle it without knowing her facing in
that room.

**That uncertainty does not block the port.** The beacon and the breath are
spawned by the same branch, so as long as each beacon stays paired with the same
breath the fight reads correctly whichever way round the pair is. Pair Beacon1
with 20922, Beacon2 with 20924 and Beacon3 with 20926 — retail's own index order
— and record the assumption.

**Why this was not ported in the same session as the spec.** Doing it properly
means the whole 45-branch chain, because the breath steps are interleaved with
the thorn, cyclops-crack, gravity-bomb and quake steps that share the same timer
chain; taking only the breaths would leave a rotation that skips steps and
mistimes the rest. It is a `PatternAi` table job of the same shape as Destroyer
Kunax, several times longer, and the table is one command away.

`python rotation_table.py <patterns_dir> IDTiamat_Tiamat_Dragon_Dying_Named_60_Al`
prints the whole table.

### The idle timer

The third and last of the runtime features the backlog named, and the one
blocking Tiamat's dying phase. **28 missing adds want it.**

There is a single idle slot rather than thirty battle-timer slots, any event can
set it, and setting it again replaces what was there. The rule that matters is
that **it is not gated on combat**: its whole purpose is the business around a
fight rather than in it — a controller retiring once it has spawned its wave, an
orb calling out on a heartbeat, a boss counting down — and half its uses in the
corpus are on NPCs that never fight at all. A battle timer that came due out of
combat would correctly do nothing; an idle timer that did the same would be
useless.

A zero delay is retail's way of saying "next tick", so it is scheduled rather
than run inline. Running it inline would evaluate an `on_idle_timer` branch from
inside the event that set it, which is not what the pattern says and would
reorder actions that follow the `set_idle_timer` in the same branch.

**Verification.** Four new pins in the runtime's own tests, all four mutations
caught — never firing, gated on combat, stacking instead of replacing, and
leaking past the owner's death. Full suite 1,107 passing and 1 skipped.

**What is left of the runtime gaps**, now that this one is closed: nothing named
in the backlog. `on_wake_up`, the message bus and the idle timer are all built.
The remaining blockers are content, not machinery — server-side waypoint paths
(91 adds between placement and movement), unresolvable skill indices, and
encounters our server does not spawn at all.

### Correction: Captain Xasta's Inhibitor Sikars are retail after all

The Captain Xasta entry above says his 28s cycle "summoned two Inhibitor Sikars"
and that "the Sikars and the walk were reconstructed from observation". Half of
that is wrong, and it only surfaced once the idle timer made
`IDYun_Temp_53` legible.

**The Sikars are real content.** They are spawned by the *siege artilleryman*
(282606) — the NPC Xasta summons at 85/65/45/20 — not by Xasta. Two seconds after
an artilleryman appears it spawns two Sikars, and fifteen seconds later two more,
at 340/588/146 and 368/605/146, each living four minutes.

The chain closes neatly on work already done: the artilleryman's `on_message` 500
makes it **despawn itself**, and 500 is exactly the `broadcast_message` in Xasta's
`on_leave_attack_state` and `on_enter_idle_state` that this port translated as
`Do.Despawn(Adds)`. Its `on_despawn` then takes its Sikars with it. So the
observation behind the original aionemu implementation was right — Sikars do
appear in that fight — and what was invented was the *mechanism*: Xasta spawning
them directly, on a walk cycle, instead of by way of the artillerymen.

**Why it is still not implemented.** The Sikar spawns carry
`pathname=3named_path_01` and `_02`, and they are level-60 combatants whose
purpose is to march in. Our own `custom_npc_walker.xml` does have routes that the
old implementation used (30028000014/15) — but those start at 263/537/**203** and
186/555/**203**, while retail spawns at z **146**. Different levels of the room
entirely, so our routes were built for aionemu's invented spawn points and cannot
stand in for retail's. Spawning them where retail says without a path leaves four
soldiers standing on two tiles, which is the trap documented under *Positionable
is not the same as implementable*.

**What it would take:** an AI on 282606 that spawns the two waves and answers
message 500, which is now straightforward — the message bus and idle timer are
both built — plus walk routes for 3named_path_01/02 that would have to be
authored by hand against the room's geometry.

### Invisible twins, and the safe form of an unsafe test

Building the idle timer brought a small family into view: Tiamat's hazards each
spawn a counterpart a few seconds after appearing. `LDF4b_Tiamat_Rage_Tranq`
spawns `LDF4b_Tiamat_Rage_Tranq_invisible`, which lives two seconds and carries
the damage; the sphere of wrath, the petrification crystal and the fissure do the
same. They surfaced now because the carriers only became spawnable when Wrathclaw
and the incarnations were ported — the non-monotonic effect again, doing its job.

The twins are scenery, and the audit now excludes them. **The test is narrow on
purpose**, because the obvious version of it is one this log already refused:

- `invisible` as a substring would discard Captain Xasta's siege artilleryman.
- `_invisible` as a *suffix* would too — `IDYun_Rasta_Sum_Invisible` ends that way.
- What separates them is that the artilleryman's base name, `IDYun_Rasta_Sum`, is
  not an NPC in the client, while `LDF4b_Tiamat_Rage_Tranq` is.

So the rule is: **the devname is another NPC's devname plus `_invisible`.** 26
devnames in the client satisfy it; the artilleryman is not one of them. 746 adds,
655 fully self-contained.

That is the sixth false-positive class, and the pattern across all six is worth
stating: every one was found by trying to port something and discovering the
finding was wrong, never by inspecting the audit. The corrections have moved the
total 812 → 746 without porting anything.

### Theobomos — Lost Balor (214567)

Pattern `ND2_FhV`. A world boss on a four-hour respawn with **no AI at all**, and
the same shape as Icaronix the Betrayer: statues called up as he loses ground.

| when | statue |
|---|---|
| below 80 | a Kuillus statue, 280956 |
| below 50 | a test statue, 280957 |
| below 30 | **two at once**, 280954 and 280955 |

All four were spawned by nothing anywhere in the server. Nothing clears them
mid-fight, so all four stand together by the end; leaving the fight clears them.

**Rotation not translated**: six skills, five indices addressed, no branch
comments. The timers those branches run on are not armed here either, for the
reason given under Icaronix — arming a timer whose branches do not exist starts a
chain that dies on its first tick.

**One mutation deliberately left uncaught.** Retail files the three steps under
three different spawn ids, and this port keeps them; but since no branch despawns
an individual group mid-fight and the reset clears all three, moving a statue into
another group changes nothing observable. The ids are kept because they are what
the pattern says, not because they are load-bearing here.

**A test gap worth repeating.** Every test drove health *down* before ticking, so
none of them ever ticked at full health — and the catch-all heartbeat was
therefore never exercised. Removing it went undetected until a test ticked four
times at 90% and then dropped to 75, which is the sequence that actually
distinguishes a live chain from a dead one.

**Verification.** Full suite 1,111 passing and 1 skipped, four new pins, five of
six mutations caught. Missing adds 746 → **742**.

## A second kind of gap: bosses with no AI at all

Four of the encounters ported in this work were found the same way — by accident,
while chasing something else. Wrathclaw sat on plain `aggressive` while his three
sibling incarnations shared a class. Icaronix the Betrayer was spawned by his own
first form and then driven by nothing. Lost Balor is a world boss on a four-hour
respawn that auto-attacked. The Adma coffins were scenery.

`audit_missing_adds.py` structurally cannot find these. It reports adds that never
spawn; an NPC with no behaviour at all has no missing *add*, it has a missing
*fight*. So `audit_missing_ai.py` now looks for them directly: an NPC our data
really places, whose template names a generic handler, and whose retail pattern is
substantial.

**779 NPCs qualify.** The top of the list is unambiguous:

| timers | npc | rating | name | pattern |
|---:|---|---|---|---|
| 127 | 297189 | LEGENDARY | ahserion | `Gab1_Sub_Boss` |
| 55 | 219933 | HERO | arcticore aizenka | `DF5_ItemNamed_12_SSH` |
| 55 | 235975 | LEGENDARY | gatekeeper flox | `LF5_ItemNamed_24_KJS` |
| 45 | 215282 | HERO | **vanuka infernus** | `Dragon_G3` |
| 43 | 220019 | LEGENDARY | tatar's blaze | `LDF4b_Golden_Gururu` |
| 41 | 215283 | HERO | **asaratu bloodshade** | `Dragon_G4` |

The two in bold are the finding that justifies the tool. Dark Poeta has four
named bosses on sibling patterns `Dragon_G1` through `Dragon_G4`; **Tahabata and
Calindi have AI classes, Vanuka and Asaratu have `aggressive`.** Exactly
Wrathclaw's shape, in a different instance, and it would not have surfaced from
the adds audit because their adds are counted against patterns nobody drives.

**Two filters do the work**, and both were needed. Without a minimum timer count
the report is every monster in the game; without a cap on how many NPCs share the
pattern it is 4,930 rows, mostly ordinary mobs on generic behaviours. Narrowly
bound plus timer-heavy is what distinguishes a fight somebody forgot to write.

**Read the count with the same caution as the adds total.** 779 is the number of
NPCs whose pattern *has* content, not the number of fights worth writing: some
are adequately served by generic AI, and the usual skill-index and waypoint
limits apply to whatever is left. It is a ranked hypothesis list, and the entries
at the top of it are the ones this session kept finding by luck.

### Dark Poeta — Vanuka Infernus (215282)

Pattern `Dragon_G3`, and the first boss ported from `audit_missing_ai.py` rather
than found by luck. He had **no AI at all** in an instance where half the roster
is implemented.

His fight is a four-step chain that runs at a different speed in each health
band, dropping flame centers at four fixed points as it goes, plus a separate
chain below 30% that summons instead:

| band | chain |
|---|---|
| 81-100 | four steps, 15s apart bar one at 12s, a **pair** of flames on the last |
| 61-80 | the same four faster, a **ring of four** on two of them |
| 31-60 | slower, 22s on the long steps, one ring |
| below 30 | timer 0 hands over to T5-T8, which summons a faithful subordinate once per loop and never returns |

Neither the flame center (281276) nor the subordinate (281275) was spawned by
anything. **Casts not translated**: ten skills, nine indices, no branch comments.
The chain itself is index-free, so the timing and the spawns are faithful.

**A tool bug caught just in time.** `rotation_table.py` kept only the *last* spawn
per branch, so the table showed one flame center where the pattern drops four. The
port was written from that table and would have shipped a quarter of the mechanic.
It surfaced only because counting the distinct coordinates in the raw pattern gave
sixteen spawns across four points, which did not match. The tool now joins every
spawn in a branch and prints absolute coordinates with each, and there is a pin
asserting four flames at four distinct points.

**A test of mine that was wrong for the right reason.** The first version asserted
exactly four flames after dropping him to 70%, and got six — the opening pair
lasts ten seconds and the first ring lands at six, so they overlap. That is retail
behaviour; the test now lets the opener burn out before measuring the ring.

**Verification.** Full suite 1,117 passing and 1 skipped, six new pins, all seven
mutations caught. Missing adds 742 → **740**.

**Still open in this instance.** Asaratu Bloodshade (215283, `Dragon_G4`, 41
timers) is the other half of the finding and has no AI either.

### Dark Poeta — Asaratu Bloodshade (215283)

Pattern `Dragon_G4`, and the other half of the finding: **Dark Poeta's boss roster
is now fully implemented.** Tahabata and Calindi already had classes; Vanuka and
Asaratu had none.

Two chains run at once, both armed six seconds in. Timer 0 is the banded one and
slows as he weakens — 16s at full health, 22s below 80 and again below 50, its
slower steps leaving a flame center at his feet. Timer 9 drives a faster loop that
does nothing until 20%, where it starts summoning a subordinate every 22 seconds.

The flame center (281246) was spawned by nothing. His subordinate (281245) already
reaches the world elsewhere, so only the flame is new content — but the chain that
places it is the fight, and none of it happened.

**One designer quirk reproduced rather than tidied.** Two adjacent branches are
guarded at `80-100` and `81-100`. At exactly 80 the first matches and the second
does not, which is almost certainly a typo in the original; it is kept as written,
since guessing which number was meant is a worse error than reproducing a
one-point overlap.

**Casts not translated**: ten skills, indices to 9, no branch comments.

**Verification.** Full suite 1,122 passing and 1 skipped, five new pins, all seven
mutations caught — every flame step, the summon gate, the timer-9 arm and the
cleanup. Missing adds 740 → **739**; the missing-AI list 779 → **777**.

## The skill-index problem is not universal: 337 patterns describe their own branches

Every boss ported in this work has left its rotation untranslated with the same
reason — "no branch comments to corroborate a mapping". That reason is correct for
the bosses it was given for, and it turns out to be **wrong as a general claim**.

Of 1,191 boss-shaped patterns (narrowly bound, six or more timer branches), **337
carry branch comments that describe what the branch does**, usually in the
designers' Korean. `DF5_ItemNamed_12_SSH` — Arcticore Aizenka and Machine Spirit
Tottal — is the worked example:

| comment | meaning | index |
|---|---|---|
| `일격` | single strike | 1 |
| `자신중심 광역` | self-centred area | 2 |
| `자신중심 도넛 광역` | self-centred *donut* area | 3 |
| `랜덤타겟 도넛 광역` | random-target donut area | 4 |
| `개체소환` | entity summon | 5 |

Against our five skills for 219933, two of those pin exactly:

- **index 4 is random-target**, and 21851 Gelid Impel is the only entry with
  `target="RANDOM"`;
- **index 5 summons**, and 21852 Shiver Wrath is the only entry carrying a
  `<spawn_npc>`.

Both anchors agree on the same alignment, and the remaining three then fall into
place by role: Sever as the single strike, Earth Cleave and Tectonic Shift as the
two self-centred areas.

**The alignment is offset by one.** Retail's index N is our list's position N-1 —
retail has an index 0 these branches never use, and we do not list it. That is
exactly why positional mapping has been refused throughout: the offset is
invisible without an anchor, and guessing it wrong shifts every cast in the
rotation by one.

**The method, for reuse:**

1. Read the branch comments. If they describe behaviour, continue.
2. Look for uniquely-attributed skills in `npc_skills`: the only `target="RANDOM"`,
   the only one with `<spawn_npc>`, the only `BUFF`, the only one gated by `max_hp`.
3. Match those against the comments that describe the same thing. Two independent
   anchors agreeing is enough to fix the alignment, including any offset.
4. Fill the rest by elimination and role, and record which were anchored and which
   inferred.

**Aizenka is not ported here.** His pattern is 55 branches with probability-gated
variants — three different branches answer timer 1 in the 80-100 band alone — and
transcribing that needs its own session. What has changed is that his rotation is
now known to be *resolvable*, which none of the previously deferred rotations
were.

**Worth re-checking with this.** The bosses whose casts this work deliberately
left alone — Omega, Queen Alukina, Icaronix the Betrayer, Lost Balor, Vanuka
Infernus, Asaratu Bloodshade — were each refused on "no comments". That was
verified per boss and remains true for them. But the 337 figure means the refusal
should be a *check*, not an assumption, on everything still to come.

### Checking the claim on every boss it was made about

The previous entry says the "no branch comments" refusal should be a check rather
than an assumption. Applying that to the twelve patterns this work refused on
those grounds: **all twelve claims hold.** Ten carry no branch comments at all
(Vanuka, Asaratu, Lost Balor, Icaronix, Omega, the clone of barrier, Queen
Alukina, Yamennes, Monolithic Ambusher, the field generator).

Two are worth stating precisely, because their comments are not empty:

- **RM-1337** has two, both on spawn branches that need no index.
- **Vasharti** has ten, and they *are* semantic — `Ref_Blue` and `Ref_Red` say
  plainly what the two reflect branches cast. His rotation is still unresolvable,
  but for a different reason than recorded elsewhere: our npc_skills lists only
  **two** skills for him against nine indices, and his real skill set lives in
  `BrigadeGeneralVashartiAI` as hardcoded ids. The blocker there is the length of
  our list, not the absence of comments.

### A ready work queue: 12 bosses that are resolvable, portable and unwritten

Intersecting the three conditions this session established gives a queue that can
be worked without re-deriving any of it:

1. no AI class (from `audit_missing_ai.py`),
2. branch comments that describe behaviour,
3. every `SKILLI_INDEX` within our own distinct-skill count.

| branches | npc | rating | top idx / ours | name | pattern |
|---:|---|---|---:|---|---|
| 8 | 284377 | ELITE | 2 / 3 | danuar reliquary novun | `Rune_FrostNmd_TankSum_65_Ae` |
| 8 | 284378 | ELITE | 1 / 2 | idean lapilima | `Rune_FrostNmd_DealSum_65_Ae` |
| 11 | 235772-5 | HERO | 5 / 6-7 | hakara, zubala, visha, bahapa | `BIDF5_U01_Middle_Boss_Fire` |
| 15 | 230850 | HERO | 6 / 8 | researcher teselik | `IDVritra_Base_Drakan_Wi_Nmd` |
| 18 | 233258 | HERO | 6 / 11 | derakanak the reaver | `IDVritra_Base_Drake_Nmd` |

The four HERO bosses on `BIDF5_U01_Middle_Boss_Fire` are one pattern and one
class, but each has its own skill list, so the index mapping has to be anchored
four times rather than once.

Note what this queue is *not*: it is not the highest-impact list. Ahserion still
has 127 unused timer branches and Tiamat's dying phase is still the richest
mechanic found. Those need their skill lists resolved by other means, or porting
without casts as several bosses here already were. This queue is simply the set
where nothing is blocked and the method is known.

### Danuar Reliquary — the frost summons (284377, 284378)

Patterns `Rune_FrostNmd_TankSum_65_Ae` and `Rune_FrostNmd_DealSum_65_Ae`, first off
the resolvable queue, and **the first bosses in this work whose casts are
translated** rather than left to npc_skills probabilities.

Both ran on plain `aggressive`. Each runs a five-step chain that cycles. The tank
shields itself on waking and again on the chain's second step, and closes each
cycle with its area attack; the dealer has no shield and hits more often. Below
half health either rounds on a random attacker — once, and then it stays angry.

**How the indices were resolved**, as the worked example of the method:

- The tank's `on_wake_up` comment reads *"cast defence buff (skill 2)"* and the
  branch casts index 2. Boost Physical Defense is the **only BUFF** in its list, so
  index 2 is pinned by two independent things at once.
- Index 0 then falls to Strike, which four branches label "single strike", and
  index 1 to Insanity Eruption, labelled "area strike".
- The list is **rotated, not offset**: our data lists the buff first because it
  carries `is_post_spawn`, where retail has it third.

**One comment is stale** — a branch labelled "skill 1" casts index 2. Four other
comments and the wake-up all agree with each other, so the odd one out is treated
as a typo rather than evidence. Worth knowing that these comments are written by
hand and can drift from the branch they sit on: they are a strong signal, not an
oracle.

**A runtime addition.** `on_attacked` now exists, distinct from
`on_enter_attack_state`: it runs on **every hit**, and a branch that should fire
once carries its own flag var. Gating on the event instead would be a different
mechanic — the coffins' alarm and these summons' switch look alike but are not.

**Verification.** Full suite 1,128 passing and 1 skipped, six new pins, all six
mutations caught after three fixes. Two of the three gaps were the same mistake:
asserting an outcome (which skill appeared, which target it holds) where the
mechanism is a flag, so a random-target branch could satisfy the assertion by
accident. Both now assert the flag the branches consume.

### Specification: the four fire bosses of `BIDF5_U01_Middle_Boss_Fire`

Hakara (235772), Zubala (235773), Visha (235774) and Bahapa (235775) share one
pattern and one shape, and each has its own skill list. Eleven branches, three
health bands, no adds — the whole fight is which skill lands when.

The comments name every index:

| index | comment | meaning |
|---|---|---|
| 1 | `질병독기운` | disease-poison aura |
| 2 | `베기` | slash |
| 3 | `특징1` | **trait 1** |
| 4 | `특징2` | **trait 2** |

| band | chain |
|---|---|
| 71-100 | trait 1 (6s) → slash (9s) → slash (9s) |
| 41-70 | trait 2 (6s) → disease aura (11.5s) → slash (9s) |
| 0-40 | trait 1 *or* trait 2 (6s) → slash (13s) → disease aura (11.5s) |

**The alignment is offset by two**, anchored twice:

- index 2 is "slash", and **Swift Edge is the only ATTACK** in every one of the
  four lists — at position 4, so retail index N is our position N+2;
- index 1 is a disease-poison aura, and position 3 is **Boost Deadly Virulency**,
  which is exactly that.

The offset then puts indices 3 and 4 — the "traits" — on each boss's own
**unique** skills, which is what makes the reading convincing: the four share
positions 0-4 and differ only in the tail, and the tail is elemental. Zubala gets
Soaring Flames and Inferno Breath, Visha gets Throw Poison and Diffusive Poison,
Bahapa gets Cold Attack and Cold Air Emission. Four bosses, one chain, four
flavours.

**Ported after all — see the follow-up below.** What follows was written before
Hakara's gap was traced; the mapping it describes proved correct and gained two
further confirmations.

**Hakara is one skill short.** He
carries six entries where the other three carry seven, so under this alignment he
has a trait 1 (Losing Rationality) and **no trait 2** — yet the pattern casts
index 4 in two of its three bands. Either our data for him is incomplete, or the
offset is wrong; and the offset is supported by two anchors that hold for all
four, while the missing entry would be an ordinary data gap of the kind this log
has found repeatedly.

Resolving it means finding what Hakara's seventh skill should be — most likely by
comparing him against his three siblings in the client's own data, since the
symmetry of the other three is strong. Porting three of four and leaving a hole
in the middle of a shared class is worse than porting none, so this waits.

`python rotation_table.py <patterns_dir> BIDF5_U01_Middle_Boss_Fire` reprints the
chain.

### Follow-up: the fire bosses are ported, and Hakara's gap is upstream

The previous entry stopped on Hakara having six skills where his siblings have
seven, unsure whether that was a data gap or a wrong offset. Both questions are
now answered.

**The offset is right, and the mapping is a rotation by two, mod seven.** Two more
confirmations turned up on the way:

- `on_wake_up` self-casts index 5, and under the rotation index 5 is **Midnight
  Robe** — the only BUFF in every list, and the only entry our data marks
  `is_post_spawn`. A self-buff on waking is exactly what that flag means.
- The branch commented "disease-poison aura" casts indices 0 **and** 1 together,
  and those land on Fatal Disease and Boost Deadly Virulency — the two disease
  debuffs, as a pair.

Four independent anchors now agree, and under the rotation every skill in every
list is used. Under any other offset each boss's last skill would be dead.

**Hakara's gap is upstream.** The Java reference carries the same six skills, so
this is an aionemu data gap rather than a porting error, and nothing in the client
can fill it — the client has no per-NPC skill lists at all. His theme is madness
where his siblings are fire, poison and cold, so the missing entry is a second
madness skill, but *which* one is not recoverable from anything we have.

**So the earlier judgement is reversed, deliberately.** That entry said porting
three of four and leaving a hole was worse than porting none. That was the right
call while the hole might have been a mistake in the mapping; it is the wrong call
now that the mapping is confirmed and the hole is a known missing row. All four
are ported. Hakara's trait-2 branches cast nothing — his 41-70 opener, and half
his openers below 40% — and a test pins that absence so nobody fills it with a
guess.

**One edge worth keeping.** The bands are 71-100, 41-70 and below-40, so **HP
exactly 40 matches none of them**. Only the catch-all keeps timer 0 armed through
it; without one the fight would stop dead for any group that parked a boss on
exactly 40%. There is a test for that specific value.

**Verification.** Full suite 1,143 passing and 1 skipped, fifteen new pins across
all four bosses, all seven mutations caught after two test fixes.

---

## Researcher Teselik and his hands — a counter, and a new primitive

**NPCs:** researcher teselik (230850, HERO), sheban mystical tyrhund (284455).
**Patterns:** `IDVritra_Base_Drakan_Wi_Nmd` and `IDVritra_Base_Drakan_Wi_Nmd_Sum`.
**Instance:** Sauro Supply Base (301130000). Both were on plain `aggressive`, and
nothing in our server spawned the hands at all — their only source is a summoning
ritual the boss had no AI to perform.

### The mechanic our runtime could not express

His fight is not an HP ladder. He keeps a **running count of how many hands are
still standing**, and every branch point asks that count a question:

| count | branch |
|---|---|
| all dead | summon a fresh wave — three or two on a coin flip |
| any alive | order them to blow up, which writes the count back to zero |

The count is retail's `INTVARI_FIRST`. Each hand decrements it as it dies, by
broadcasting message 22260 to fifty metres; the boss's `on_message` answers.

This needed a primitive the pattern runtime did not have, so `AiPattern` gains
three conditions and `PatternAi` four counter slots (`INTVARI_FIRST` through
`FOURTH`):

- `When.CountBelow(counter, comparand, setTo)` — `set_intvar_if_less_than`
- `When.CountAbove(counter, comparand, setTo)` — `set_intvar_if_larger_than`
- `When.Decrement(counter, low, high)` — `decrease_intvar`

Like the flag vars these **mutate when they pass**, so they must be evaluated in
written order. The clamp on `Decrement` is load-bearing: a hand can report its
death after the boss has already zeroed the count, and without the clamp that late
report would drive it negative and he would never summon again.

**This is not a Teselik quirk.** Across the 5.8 dump `increase_intvar` appears
1,409 times, `set_intvar_if_less_than` 215, `set_intvar_if_larger_than` 170 and
`decrease_intvar` 176. Counters are one of the most-used primitives in the whole
pattern language, and until now we could express none of it. Only the four
operations above are implemented; `increase_intvar`, `add_intvar`, `sub_intvar`
and the `be_true_only_when_hit_the_bound=TRUE` form of `decrease_intvar` are
deliberately left out rather than shipped untested — the next pattern that needs
one should bring a test with it.

### Skill indices — all seven resolve

Our `npc_skills` list has exactly seven entries and retail addresses exactly seven
indices, but they are **not the same seven**:

| idx | skill | how it was pinned |
|---|---|---|
| 0 | 20700 Midnight Robe | one of two buffs he self-casts on resetting ("종족버프") |
| 1 | 20701 Blessing of Blood | branch comment 피의 축복 — the exact name |
| 2 | 17335 Flame Bolt | 불꽃화살, four branches, matching its 33% |
| 3 | 21288 Fire Burst | 불꽃뿜기, two branches, matching its 23% |
| 4 | 20657 Summoning Ritual | the only skill in the list carrying `spawn_npc` |
| 5 | **20708 Self-destruct Command** | 자폭명령 — the exact name |
| 6 | 21135 Beritra's Favor | the other reset buff |

Index 5 is the find. Our data carries 20708 only as a **commented-out** entry with
the note *"we have no real handling for NPC summon control"* — and the retail
pattern **is** that handling. In its place the live list repeats 21135 twice; that
duplicate is what upstream substituted. The list is left as upstream has it, since
the AI casts by id, and the stale comment has been corrected in place.

### Two retail quirks reproduced rather than tidied

**Phase two can eat itself.** The handover at 65% is guarded by a one-shot flag
that sits *ahead* of the count test. If the hands all happen to be dead at the tick
he crosses 65, the branch that wants them alive spends the flag and *then* fails on
the count — and the summoning variant beneath it can never match again. Phase two
is skipped for that entire fight. It reads like a bug and it probably is one, but
it is what the data does, so it is pinned by a test named for it.

**The flame bolt alternates.** The two branches on timer 2 swap through a flag —
one sets it and switches target, the next consumes it and does not. Not pinned: our
harness has a single player in the aggro list, so a switch to a random attacker has
no observable effect. Noted here instead.

### What is deliberately not translated

- **The four named server paths.** Every hand is anchored on
  `NPCPath_Bboss_Hand_01`–`04`, which we do not have, so they arrive next to him
  instead. Retail's own phase-two branch already places one of three at his feet, so
  this is a small stand-in — but it is ours, not theirs.
- **The hand's own blast.** Retail's hand spawns the burn zone *and* casts its
  skill index 2, a suicide skill our `npc_skills` does not carry (the list has one
  entry, the knockback at index 0). The zone (284687) delivers the damage with
  21206 Burn Zone, so what is missing is the hand's own explosion, not the hazard.
  It despawns itself instead: the boss has already zeroed his count when he gives
  the order, so a hand left standing would put the count and the field permanently
  out of step and the next wave would stack on top of this one.
- **The self-destruct order's plumbing.** Retail casts index 5 and the hands act on
  message 22261, but *nothing in the pattern sends 22261* — retail routes it through
  the skill. Our skill engine has no AI-message effect, so the branch broadcasts it
  alongside the cast. Same observable behaviour, different plumbing.
- **His three shouts** (`STR_CHAT_IDVritra_Base_Nmd3_01/02/03`) have no numeric id
  in our data.
- **The death tail.** Retail's `on_killed_by_user` opens door 210, announces
  `STR_MSG_IDVritra_Base_DoorOpen_04`, and places four bonus hands (284457) on those
  same named paths. Door control and waypoints are not in the pattern runtime; only
  the despawn half is ported. **This is the largest single piece left on this boss.**

### A tooling fix that changed the answer

`rotation_table.py` kept only the **last** `use_skill` per branch — the same bug
class already fixed for spawns. Teselik's phase-two branches cast a buff *and* a
command, and with only the second showing, the skill indices looked one place out
of step with his list and the whole mapping read as a rotation. It is not a
rotation. Casts are now joined like spawns.

**Verification.** Full suite 1,158 passing and 1 skipped; fourteen new pins; all
eleven mutations caught. Two pins were added because mutation testing showed the
originals could not see the difference: *exactly one hand left* (the only count
that tells `>=` from `>`) and *who he breathes fire at* (self while healthy, at the
target below 65 — same skill, two regimes).

---

## Derakanak the Reaver — an eighteen-branch rotation nobody could see

**NPC:** derakanak the reaver (233258, HERO). **Pattern:** `IDVritra_Base_Drake_Nmd`.
**Instance:** Sauro Supply Base (301130000). Was on plain `aggressive`.

He spawns **no adds**, which is why the missing-adds sweep could never surface him:
there was no absent NPC to count. The whole fight was simply not there, and he
auto-attacked with whatever his skill probabilities happened to roll.

Three regimes, each its own timer chain, entered by one-shot branches on the
heartbeat:

| regime | chain | opens with |
|---|---|---|
| 81-100 | T1 → T2 → T3 → T4 → T1, 10s a step | — |
| below 80 | T5 → T6 → T7 → T8 → T9 → T5 | the fear pair |
| below 40 | T10 → T11 → T12 → T13 → T14 | the fear pair again |

The phase-three tail alternates through a flag: one pass through T14 hops back to
T11 with a fireball, the next loops the whole way back to T10 and re-casts the fear
pair.

### Skill indices — the cleanest mapping so far

Seven indices against a twelve-entry list, and the branch comments name **five of
them outright**, one each, with no ambiguity:

| idx | comment | skill |
|---|---|---|
| 0 | 마법구 "magic orb" | 16987 Large Magic Missile |
| 1 | 화염 | 16574 **Flame** |
| 2 | 불꽃뿜기 | 16918 **Flame Spurt** |
| 3 | 강력한 화염 "powerful flame" | 16919 Fireball |
| 4 | 공포발산 | 17888 **Fear Casting** |
| 5 | 축복의 저주 | 16702 **Curse of Blessing** |
| 6 | 공황유발 | 20782 **Fearful Panic** |

The five bold rows are exact name matches. That leaves "magic orb" for Large Magic
Missile and "powerful flame" for Fireball — the only other flame debuff, and the
stronger of the pair. The five unaddressed skills stay on their probabilities,
which is what retail does with them too.

**One comment disagrees with its own branch.** Step 5 is commented 마법구 but casts
index 2, Flame Spurt, where its three sibling 마법구 steps all cast index 0. The
action is what runs, so the action is what is reproduced; the comment reads like a
copy-paste from the step above it.

### Two seams worth knowing

**Phase three kills the heartbeat.** It is the only phase branch that does not
re-arm timer 0 — its own chain is self-sustaining. The consequence: a boss taken
straight past 40% never gets another timer-0 tick, so **phase two is locked out for
the rest of the fight**. Pinned.

**Exactly 80 matches nothing.** The healthy chain wants 81 or better and phase two
wants strictly below 80, so at 80 no step matches at all and only the heartbeat
keeps him going until he loses another point. Same class as the Ophidan Bridge fire
bosses at 40.

**Nothing is left untranslated.** Every branch, every cast and every target of this
pattern is ported.

### A harness bug this found, and an engine quirk it did not

`BossAiHarness.SetHpPercent` floors on the way in and `GetHpPercentage` truncates a
float on the way out, so **asking for 80 lands on 79**. Invisible mid-band, fatal at
a seam: the "exactly 80" test was silently testing 79 and passing for the wrong
reason. These tests now set health through a helper that asserts the AI reads back
the percentage asked for.

Chasing that turned up something that looks like a bug and is not: `GetHpPercentage`
computes `100f * currentHp / maxHp`, and above roughly 167k HP the single-precision
product loses enough that **full health reads 99, not 100**. The Java reference has
the *identical* expression, so per the golden rule it stays — this is a faithful
port of an aionemu quirk, not a porting slip. Worth knowing before writing any
`HpBetween(x, 100)` test: use 90 for "healthy".

**Verification.** Full suite 1,167 passing and 1 skipped; nine new pins; all nine
mutations caught.

---

## Correction — Tiamat's incarnations dropped one hazard per player

A fidelity bug in already-shipped code, found while reading a different boss.

Retail's `spawn_on_multi_target` carries **`total_set_to_spawn`**, a cap on how many
targets it lands on, together with `order_in_attacker_list` — descending, so the cap
keeps the top of the hate list. Our `SpawnOnEachTarget` had no cap at all and
spawned on **every** valid target in range.

The incarnations' area attack is the one that hurts: in a full alliance Fissurefang
dropped one earthquake per player where retail drops **three**. And each incarnation
has its own numbers, where this port used a single generic set for all three:

| incarnation | cap | range | lifetime | was |
|---|---|---|---|---|
| Fissurefang | 3 | 0 | 25s | uncapped, 20s |
| Petriscale | 3 | 1 | 20s | uncapped, range 0 |
| Graviwing | 1 | 6 | 12s | uncapped, range 0, 20s |

Petriscale's *power* attack is also multi-target and capped tighter still, at **two**
— it was uncapped too.

**The fix.** `maxTargets` is now a **required** parameter of `SpawnOnEachTarget`,
not an optional one with a permissive default. Every `spawn_on_multi_target` in the
retail files carries the field, so omitting it is never right, and making it
required means the next boss cannot repeat the mistake by silence. The cap takes the
most-hated first, matching `ORDERI_DESCENDING`.

**Why the existing pins missed it.** All fourteen passed against the buggy code.
They asserted that hazards appear, where they come from and how often — never how
many, how long they last, or how wide they spread. Two of the four mutations written
for this fix survived the first attempt at new tests as well:

- the lifetime pin counted hazards, and the power attack keeps adding its own every
  nine seconds, so the count could never isolate what aged out. It now follows the
  exact objects the area attack placed.
- the power-attack cap had no pin at all; the cap test was measuring the area attack
  either side of the 15s tick and Petriscale's earlier crystals were inside the
  baseline.

**Verification.** Full suite 1,173 passing and 1 skipped; seven new pins; all four
mutations caught after the two test fixes above.

### Screened and deliberately not ported this round

- **Volatile / Furious / Wounded Belsagos** (233898, 234991, 234990 —
  `IDLDF4_Re_01_{Phy,Hard,Easy}Boss`, 41-53 timers). Well commented, but the comments
  name skills only by *index number* ("스킬1", "스킬6"), never by name, so the casts
  stay unresolvable — and these three spawn **nothing**. A structure-only port would
  add timers that fire and do nothing observable. Worth revisiting only if a skill
  list for them ever surfaces. They do carry one genuinely distinctive mechanic worth
  recording: below 29% they branch on whether the current target is a **caster or a
  melee** and cast a different skill for each.
- **Naga_WrF, ND2_WhF, NLehpar_BhC** — 18-24 branches each, **zero** commented
  branches. Same refusal as Icaronix and Lost Balor.

---

## Aurelian Dadar and Tatar's Blaze — three adds nobody could summon

**NPCs:** aurelian dadar (235966, Cygnea) and tatar's blaze (220019, Enshar), both
LEGENDARY world bosses on plain `aggressive`. **Pattern:** `LDF4b_Golden_Gururu`.

All three things they call up were spawned by **nothing** anywhere in the server:

| threshold | add | how many | picked |
|---|---|---|---|
| below 85 | tatar's clone (282743) | 8 | most-hated first |
| below 60 | paralysis eye (282744) | 2 | at random |
| 90 / 70 / 45 / 25 | lava (282746) | 6 | at random |

Each runs on its own timer with a repeat branch beneath it: the threshold branch
rests fifty seconds after firing, the repeat re-checks every six. The four lava
thresholds are one-shots, one flag each.

### `ORDERI_RANDOM` — and a second correction to yesterday's fix

Yesterday's cap fix hardcoded "most-hated first", because every Tiamat spawn uses
`ORDERI_DESCENDING`. This boss shows that was too narrow: **`ORDERI_RANDOM` is the
common case by a wide margin** — 254 uses across the 5.8 files against 65 descending
and 5 ascending. A paralysis eye on two random players is a different fight from one
on the two tanks.

`MultiTargetOrder` is now a **required** parameter alongside `maxTargets`, for the
same reason: the field is always present in the data, so there is no safe default.
Tiamat's four call sites were re-checked and are all genuinely descending.

### A declared event that never fired

`AiPattern.OnLeaveAttack` existed as API and **was evaluated nowhere in the runtime**.
No shipped boss used it, so nothing was broken — but this pattern puts its add
cleanup in `on_leave_attack_state` rather than `on_enter_idle_state`, and the adds
simply never went away. `HandleBackHome` now runs both, leave-attack first, matching
the order retail fires them.

### What is deliberately left out

- **The casts, and two entire chains with them.** Fifteen skill indices are addressed
  and **neither boss has an `npc_skills` entry at all** — not a short list, no list.
  Retail's other two chains do nothing but cast, so arming them would schedule a
  heartbeat forever to do nothing. Both are omitted, as Lost Balor's were, with their
  timings recorded so they can be restored if a skill list ever surfaces:
  - main rotation: T0 → T1 → T2 → T3 → T0 at 8s, 8s, 8s, 12s — indices 14, 13, 11, then 7+8+8
  - debuff cycle: T7 → T8 → T9 → T10 → T11 → T7 at 40s — indices 2, 3, 4, 5, 6
- **The door.** Retail opens door 1 on waking, closes it sixty seconds into the fight
  — shutting the raid in — and re-opens it on death, reset and leaving combat. The
  pattern runtime has no door control. **This is now the second boss to want it**
  (Researcher Teselik opens door 210 on death), and is the clearest next addition to
  the runtime.
- **The three shouts**, which have no numeric id in our data.

**Verification.** Full suite 1,186 passing and 1 skipped; thirteen new pins across
both bosses; all ten mutations caught.

---

## Correction — Teselik's door was never missing, and door control is not the next thing to build

The Teselik entry above lists his death tail — "open door 210, announce
`STR_MSG_IDVritra_Base_DoorOpen_04`, and place four bonus hands" — as needing door
control the runtime does not have. **Two thirds of that was already implemented.**

`SauroSupplyBaseInstance.OnDie` has carried `case 230850:` all along: it sends
`STR_MSG_IDVritra_Base_DoorOpen_04()` and opens the door. Only the four bonus hands
(284457) are genuinely absent, and those are waypoint-blocked rather than
door-blocked.

**The lesson, which is the useful part.** Before writing "not translated" against a
retail action, check whether something outside the AI already does it. Retail packs
doors, system messages and score into the monster's own pattern because that is the
only place it has; our server splits them across instance handlers, which is the
Java-parity arrangement and the correct one. An instance's doors belong to the
instance handler. **A retail action absent from an AI class is not the same as a
missing feature**, and this port claimed one for the other. That check is now part of
the pre-flight, alongside the audit flags and the single-owner check.

**So the recommendation at the end of the Golden Tatar entry — that door control is
the clearest next addition to the pattern runtime — is withdrawn.** For instance
bosses the instance handler already covers it, in the right place. Only world-zone
bosses like the Golden Tatars have nowhere else to put it, and there the semantics
are not yet resolved:

### `control_door` method values are still ambiguous

`<control_door><id>N</id><method>M</method></control_door>`, 691 uses, methods 1
(590), 2 (94) and 0 (7). The two readings conflict:

- **Method 1 = open.** Teselik's pattern uses `method 1` on the door our own
  Java-parity handler *opens*, with the same system message. That is a hard anchor
  against working code.
- **Method 2 = open.** `LDF4b_Golden_Gururu` is internally consistent across four
  uses: its `OpenDoor` branches are method 2 (on waking, on death, on going idle) and
  its `CloseTheDoor` branch — sixty seconds into the fight — is method 1. That is
  also the only reading in which the mechanic makes sense as a trap.

Across the corpus the comments genuinely disagree: `opendoor` appears against method
1 four times and method 2 three times. Retail's `id` is not our `static_id` either
(Teselik's pattern says 210 where our instance data uses 375), so the ids need their
own mapping regardless. **Not implemented, deliberately** — guessing here either
seals a raid in or opens a progression door early, and both are hard to notice. A
second anchor of the Teselik kind, on a door some other handler demonstrably closes,
would settle it.

---

## Gatekeeper Flox — four branches that spawn one add

**NPC:** gatekeeper flox (235975), Cygnea world boss, LEGENDARY, eight-hour respawn,
on plain `aggressive`. **Pattern:** `LF5_ItemNamed_24_KJS`. The watching eye (855728)
it calls up is **HERO**-rated and was spawned by nothing anywhere in the server.

Four phases, each a T0 → T1 → T2 → T3 → T0 loop at its own speed. Two of them open
by putting an eye out: once between 51 and 75, once below 25.

| band | loop | eye |
|---|---|---|
| 76-100 | 15s, 10s, 10s | — |
| 51-75 | 10s, 15s, 10s | yes |
| 26-50 | 15s, 7s, 15s | — |
| 0-25 | 7s, 15s, 5s | yes |

### The trap: one eye, not four

Retail writes **four** branches for each eye, one per cardinal point twenty metres
out — and all four share **one** one-shot flag. Branches are first-match-wins, so the
first of the four whose 25% roll passes spawns its eye and spends the flag; the
fourth carries no probability and catches the case where the other three miss.

The effect is one eye, at one of four places, once per phase. A table written off the
rotation digest — which shows four spawn rows — would put **eight** eyes out over a
fight where retail puts two. This is the same class of misread as Vanuka Infernus,
where the digest hid three of four flame points; here it shows four where there is
one. The digest is a reading aid, not the pattern.

### `SPAWN_LOCATION_RELATIVE`

New primitive: `Do.SpawnOffset`, a fixed offset from where the NPC stands, distinct
from `SpawnNear`'s random scatter. 726 uses across the 5.8 files. Taken as a
world-axis offset; whether retail rotates it by the NPC's heading is not settled, and
for the four-way symmetric placement this was written for the distinction cannot be
observed.

### The cast-only chain is kept here, unlike the Golden Tatars

Twelve skills, indices 0 through 9, and the branch comments are phase labels ("1P",
"2P") rather than skill names — nothing to map them onto. But T1, T2 and T3 are what
bring timer 0 back round, and **timer 0 is where the eyes come from**. Dropping them,
as the Golden Tatars' cast-only chains were dropped, would leave the second eye
unreachable. The rule is not "always keep" or "always drop": keep a chain when
something observable hangs off it.

Retail's timer-0 branch for the 26-50 band *is* dropped, on the same rule — it is a
one-shot that only casts, so with the cast gone it does exactly what the catch-all
already does.

### Not translated

- **Timer 25** — broadcasts message 550020 and casts index 8. The message is
  presumably for the eye, whose own pattern is not ported, and the cast is
  unresolvable, so the timer is not armed.
- **`on_message` 44022** and **`on_see_friend_killed_by_user`**, the latter having no
  counterpart event in our AI at all.
- The **hate reset and target switch** that ride along with several casts.
- His **four shouts**, which have no numeric id in our data.

**Verification.** Full suite 1,196 passing and 1 skipped; ten pins; all eight
mutations caught. Two pins were added because mutation testing showed the originals
were blind to the 26-50 band putting nothing out, and to the catch-all being the only
thing that brings timer 0 round at full health — the case that matters, since nobody
pulls a world boss at 60%.

---

## Machine Spirit Tottal and Arcticore Aizenka — replacing a stand-in with the real thing

**NPCs:** machine spirit tottal (235971, Cygnea) and arcticore aizenka (219933,
Enshar), HERO world bosses with **identical** skill lists, both on plain
`aggressive`. **Pattern:** `DF5_ItemNamed_12_SSH`.

Three regimes, each a five-slot loop, plus a summon chain below 40:

| band | loop |
|---|---|
| 80-100 | 10s, 14s, 10.5s, 10s, 14s |
| 40-80 | 11s, 14s, 14s, 10s, 14s, opening on a random target |
| below 40 | timer 0 becomes the summon and hands to T5 → T6 → T7 |

**Four waves of six frost bombs** (855913), eight seconds apart — two within five
metres, two within ten, two within twenty — while timer 1 restarts the ordinary
rotation on a 36-second fuse, so the waves run *alongside* the rotation rather than
replacing it. The bombs are `useSkillAndDie`: each detonates and removes itself, so
they never accumulate.

### The doubled summon

Our `npc_skills` hung a `spawn_npc` of **three to six** bombs off skill 21852 for both
bosses — aionemu's stand-in for a summon mechanic it had no other way to express.
With the pattern in place that stand-in double-counts, so it is **removed from both
entries** and the pattern now owns the summoning at retail's count, distances and
timings. Same class of change as Teselik's commented-out summon-control skill: a
workaround superseded by the thing it was working around.

### Skill indices — anchored on structure, not names

Index 4 is the only skill in either list with `target="RANDOM"`, and every branch
using it is a random-target branch. Index 5 is the only one carrying `spawn_npc`, is
marked `max_hp="40"`, and is used by exactly the branches guarded below 40. Both land
on the identity mapping, which fixes the rest.

**Three variants collapse to one.** Retail writes the paired area attack three ways —
centred plus donut at 28%, centred twice at 40%, donut twice as fallback — but our
data resolves index 2 *and* index 3 to the same skill, 21850, because aionemu stores
it as a chain rather than as two skills. All three variants therefore have identical
effect and are written once. If the donut is ever separated out, the three come back.

### An equivalent mutant worth recording

Widening the summon branch's guard from `below 40` to `below 80` changes **nothing**:
the 40-80 branch outranks it on timer 0 across that whole band, so the summon can only
ever fire below 40 regardless. The guard is redundant with the way the bands tile.
Confirmed by mutating to `below 20` instead, which fails seven pins. Worth knowing
before someone "tightens" a guard that is already doing nothing.

### Not translated

- **Index 0**, self-cast on waking and on returning to spawn. Our index 0 is `Sever`,
  an attack, and self-casting an attack on waking is far likelier to be a slot
  aionemu filled differently than something retail meant.
- **Timer 15**, which only broadcasts message 60000 to bombs that already detonate on
  their own clock, so it is not armed.
- **Timer 8**, which retail's last summon wave arms and no branch answers.

**Verification.** Full suite 1,213 passing and 1 skipped; seventeen pins across both
bosses; all nine mutations caught, one of them only after replacing an equivalent
mutant with a real one. One test was wrong before it was right: it counted bombs
standing, where the bombs remove themselves — it now counts them by identity.

---

## Correction — Ahserion was never the biggest missing fight

For several runs `audit_missing_ai.py` put **ahserion (297189)** at the top of its
report — 127 timer references, 13 spawns, no AI class — and this log repeatedly named
it as the largest unused fight in the game. It is not a fight at all.

- **297189 is placed in exactly one spawn file: `900190000_Tag_Match_Test_Level.xml`.**
  A developer map. No player can reach it.
- The Ahserion players actually fight is **277224**, which has had `AhserionAI` all
  along — and that class already spawns Ereshkigal's Voice (297186), which is most of
  what the retail pattern's spawns amount to.
- The second Ahserion the pattern summons, 297195, is already reachable too: skill
  21574 carries its `spawn_npc`, and that skill is in our `npc_skills`.

NCSoft ships developer maps — tag-match arenas, time-attack rigs, zone tests — and
their spawn files sit in the same tree as the real ones. The audit's liveness check
counted any placement anywhere, so a phantom led the report. It now skips spawn files
whose name matches `test|_dev|sample`, which drops the total from 767 to **760** and
puts real content back at the top.

**Had it been ported it would have been wasted work**, and worse, it would have looked
like progress. Two audit corrections in as many sessions now — first that an instance
handler may already implement a pattern's doors and messages, now that a spawn file
may not be a real place. Both were failures of the same kind: treating a mechanical
signal as evidence of reachable content without checking what it pointed at.

**Screened while there:** Ahserion's pattern resolves only 3 of its 13 skill indices
by name (충격파 → Shock Wave, 파멸의 이드분열 → Ide Destruction, and 쾌속의 일격 [랜덤]
→ the 21562/21565 random three-chain). The other ten have no match, so even the real
277224 could not have its casts translated from this pattern.

---

## Prectaz — eight tentacles, and a branch that can never run

**NPC:** prectaz (219934), Enshar world boss, LEGENDARY, on plain `aggressive`.
**Pattern:** `DF5_ItemNamed_24_SSH`. Both tentacle types are **HERO**-rated and
neither was spawned by anything anywhere.

Below 35% it puts out eight at once, on a fifty-second life:

| npc | where |
|---|---|
| 855911 | the four cardinals, eighteen metres out |
| 856067 | the four diagonals, ten metres out |

Three bands, each a T0 → T1 → T2 → T3 → T5 → T0 loop: 10/11/14/10/14 above 85,
14/17/14/10/10 between 35 and 85, and 25/10/14/10/14 below 35 where the first step is
the summon. That last works out at about seventy-nine seconds a cycle, so there is a
real gap — the first set expires at fifty-six — with nothing on the field.

### A dead branch

Retail writes the summon **twice**, with identical guards: same timer, same health
test, no probability on either. The two differ only in geometry — the higher-priority
one puts the cardinals at eighteen and the diagonals at ten, the lower one swaps them.
First-match-wins means the second can never run, so only the first arrangement is
translated, and a test pins the distances specifically. The same duplication appears
in the message handlers, where two branches both answer message 55003.

### Not translated

- **The casts.** Eight indices addressed, **five** skills in our `npc_skills`. The
  chains above 35 are kept regardless — as Gatekeeper Flox's were, unlike the Golden
  Tatars' — because they are what brings timer 0 round, and timer 0 below 35 is where
  the tentacles come from.
- **Timer 10**, a three-second heartbeat broadcasting message 100001 to tentacles
  whose own pattern is not ported. A forever-ticking timer with no listener, so it is
  not armed.
- **`on_message` 55001-55003**, where the tentacles call and he answers with a frontal
  attack toward the caller — indices 7 and 3, both unresolvable.
- **Index 0**, the spawn buff, and **timer 4**, which three branches arm and none answers.

**Verification.** Full suite 1,222 passing and 1 skipped; nine pins; all eight
mutations caught — but only after three rounds. The first pass missed the looping step
in both upper bands and the summon's period entirely; the second pass added tests for
them whose arithmetic was wrong, landing the assertion exactly on the tentacles'
expiry. The lesson worth keeping: **a "fought down from full" test only exercises the
band's looping step if the health drop comes after a complete lap** — drop it earlier
and the low chain picks the sequence up mid-flight, and the missing step never shows.

---

## RM-56c — a trap ladder that thickens as it weakens

**NPC:** rm-56c (214802), Azoturan Fortress, ELITE, on plain `aggressive`.
**Pattern:** `NLehpar_BhC`. The complete traps (281281, ELITE) were spawned by nothing
anywhere — their only reference in the whole server was their own `npc_skills` entry.

Each band lays its own arrangement **once**, the first time timer 0 comes round inside
it, and lights the band's own timer:

| band | traps | where | band timer |
|---|---|---|---|
| 61-80 | 1 | underfoot | 25s |
| 41-60 | 2 | two metres either side | 30s, then 25s |
| 21-40 | 3 | four metres out | 25s, then 20s |
| below 20 | 4 | corners of an eight-metre square | 25s, then 20s |

**The re-lay path.** Each band timer splits on a coin flip: half the time it casts,
half the time it lights timer 9 a second out, whose branches lay that band's
arrangement again. The traps live twelve seconds, so they come and go rather than
accumulating.

**Exactly 20 belongs to no band** — the arrangement below wants `< 20` and the one
above wants `21-40`. Only the per-timer heartbeats carry the fight through it. Third
boss with this shape, after the Ophidan Bridge fire bosses at 40 and Derakanak at 80;
it is clearly a habit of the format rather than a slip in any one pattern.

### The casts, and a hint deliberately not acted on

Five indices addressed, and our `npc_skills` carries **exactly five** skills — which is
suggestive, but this pattern has **no branch comments at all**, so nothing anchors the
mapping. The one hint on record: 17910 and 17911 are named `First Rune Carve` and
`Second Rune Carve`, an ordered pair, and indices 0 and 1 are the pair every
trap-laying branch casts together. That constrains their order relative to each other
and nothing else — indices 2, 3 and 4 have no anchor whatever, and an ordered pair
could sit at any offset. Same refusal as Icaronix, Lost Balor and Prectaz. Recorded
here so a future session with better evidence can pick it up rather than re-derive it.

**Also not translated:** the shout, which has no numeric id in our data, and message
6681, broadcast to ten metres alongside every trap-laying branch — the traps run the
generic `trap` AI, which has no listener for it.

**Verification.** Full suite 1,233 passing and 1 skipped; eleven pins; all eight
mutations caught. One test counted live traps where the traps expire on a twelve-second
clock, so "laid once" and "laid four times and expired three" looked identical — it
now counts by identity, the same fix the frost bombs needed.

---

## The Vritra rearguards — two trap types nobody could lay

**NPCs:** guard post rearguard (233487) and defense post rearguard (233477), Engulfed
Ophidan Bridge, both ELITE and both on plain `aggressive`. **Pattern:**
`IDF5_U1_War_Vri_Def01_Ra_SN_65_Ae`. The drakan mine trap (284693) and drakan net trap
(284692) were spawned by nothing anywhere. `EngulfedOphidanBridgeInstance` names both
rearguards — but only to award score, so the pre-flight check came back clear.

Two chains of eight timers, one per side of 50%, structurally identical and each
opening by putting **three mine traps** on the current target: T1 → … → T8 → T1 above,
T9 → … → T16 → T9 below, both at 10, 21, 10, 9, 7, 15, 15 and 9 seconds. Crossing 50
for the first time also drops **two net traps** and switches to a random attacker.
Everything lands within five metres of the target and lives fifteen seconds.

### `num_to_spawn` again

The rotation digest showed one row per spawn *element*, and both spawns carry
`num_to_spawn` — 2 for the nets, 3 for the mines. Read off the digest this would have
been one trap of each. That is the third distinct way this digest has under- or
over-reported a spawn count: Vanuka's four flame points collapsed to one, Gatekeeper
Flox's one eye read as four, and now a multiplier ignored entirely. **Always open the
spawn element.**

### A flag pair that can strand it

The branch laying the net traps tests two one-shots in order — its own latch, then the
never-again flag. While health stays below 50 the latch alone blocks re-entry, so the
second flag looks redundant. It earns its keep only on a *second* descent, and there
the pair misbehaves: the latch passes and is spent, the never-again flag then fails,
and the branch beneath — which exists precisely to re-arm the low chain without laying
traps — finds the latch already gone. The rearguard has no chain at all below 50 from
then on. Reproduced as written, and pinned by a test that heals it back over 50 and
pushes it down again. Same shape as Researcher Teselik's phase two eating its own flag.

**Not translated:** the casts (neither npc has an `npc_skills` entry at all, and the
comments are timer labels like "BT14"), the waypoint return our AI does anyway, and
message 4444444.

### A latent test bug this exposed

Adding these tests broke `TheFlamelordAiTests.DeliversOneExecutorEveryTwentyFiveSecondsInRotation`
— in the full suite only, never alone. The rotation was fine. The test identified "the
executor that just arrived" as the **last** entry of `LiveNpcs()`, which yields world
order and therefore follows globally allocated object ids; "last" only meant "newest"
while that file happened to run early enough. New tests elsewhere shifted the
allocation and it started reading a different executor. It now diffs the live set
instead. Worth remembering: **`LiveNpcs()` ordering is not spawn ordering**, and any
pin that leans on it is waiting to break.

**Verification.** Full suite 1,245 passing and 1 skipped; twelve pins across both
rearguards; all nine mutations caught, the last only after adding the heal-and-drop
test that makes the never-again flag observable.

---

## Princess Karemiwen — a countdown written as a flag ladder

**NPC:** princess karemiwen (214695), Adma Stronghold, ELITE, on plain `aggressive`.
**Pattern:** `ND2_WhF`. Her banshee maid (281052) and vampire maid (281051) were
spawned by nothing anywhere; their only trace in the server was their own
`npc_skills` entries. `AdmaStrongholdInstance` does not name her.

**The maids arrive on a three-minute fuse.** Retail writes one sixty-second timer and
three branches on it, each guarded by its own one-shot flag. The first two only shout
and re-arm; the third shouts, calls both maids, and does **not** re-arm. So the timer
turns at 60, 120 and 180 seconds and only the last does anything, after which the
ladder is finished for the fight. The maids arrive at her feet and stay five minutes.

It is a countdown expressed as a flag ladder rather than as a delay — which is why the
shouts matter to retail: the first two turns are the warning that the third is coming.
Our data has no numeric id for `STR_CHAT_BIDDF2A_NM_Princess_50_Ah`, so the warning is
silent and only the arrival shows.

### Two equivalent mutants, and why they are equivalent

Two mutations survived, and both for the same reason: **the one-shot flag on the third
branch and its absence of a re-arm are redundant with each other.** Either alone ends
the ladder — with the flag spent no branch matches the next turn, and with no re-arm
there is no next turn. So removing either one changes nothing observable.

Rather than record that as a gap, it was checked: removing **both together** does spawn
repeatedly, and `TheLadderIsSpentAndNoMoreArrive` catches it. Six of eight mutations
caught directly, two equivalent with their combination caught. Retail wrote a belt and
braces here, and it is worth knowing that either can be cut without a test noticing.

### The rest of the pattern is omitted, deliberately

Six skills, indices 0 through 5, and **no branch comments at all** — the same refusal as
Icaronix, Prectaz and RM-56c. Her other five timers exist only to cast, and none of them
spawns, moves or says anything, so arming them would schedule work forever to do
nothing. Recorded here against the day a mapping turns up:

- a five-second heartbeat whose bands each light one cast-only timer once — below 30
  lights T2 at 10s, 31-50 lights T4 at 15s, 51-80 lights T5 at 20s
- T9, a twenty-five second alternation between two skills on a coin flip, tightening to
  twenty seconds below 50
- T1, a twenty-second band timer that only runs at 81-100

**Verification.** Full suite 1,251 passing and 1 skipped; six pins.

### Sweep: no other test leans on world order

Following the Flamelord fix, every `LiveNpcs()` use in the suite was checked. Nothing
else indexes into it — the rest use `Single`, counts, `foreach`, or sort explicitly
before comparing. The Flamelord pin was the only one, and it is fixed.

---

## The naga captains — four slaves on whoever is tanking

**NPCs:** naga sorcerer (290126) and captain lahbri (256115), Reshanta, both HERO and
both on plain `aggressive`. **Pattern:** `Naga_WrF`. The naga slave (290127, ELITE) was
spawned by nothing anywhere; its only trace in the server was its own `npc_skills`
entry.

**Four slaves, on the current target**, once on first dropping into 41-60 and then
every ninety seconds for as long as the fight stays in that band. Each lands within ten
metres of whoever the captain was facing. Below 41 the timer stops matching and no more
come.

`num_to_spawn` was 4 — the fourth time this has mattered, after Vanuka, Flox and the
Vritra rearguards. The digest still shows one row per spawn element.

### Why the casts stay untranslated even though the count matches

Ten indices are addressed. Captain Lahbri has ten skills — but the **naga sorcerer has
no `npc_skills` entry at all**, so one of the two NPCs sharing this pattern could not
cast anything even with a perfect mapping. There are no branch comments either. Same
refusal as Icaronix, Prectaz, RM-56c and Karemiwen.

Omitted with it: a one-shot per health band on timer 1, each lighting a cast-only
ping-pong pair — T2/T3 at 76-90 and again at 61-75, T5/T6 at 21-40, T7/T8 below 20 —
plus a broadcast of message 3315 and three shouts. Only the band that summons is kept,
with the timer-1 heartbeat that carries the fight into it.

### One deliberate divergence

Retail clears the slaves only on death and leans on `despawn_at_attack_state` for the
rest — a flag our runtime does not model. Their `live_time` is **fifty minutes**, so a
reset that left them standing would strand four elites in the abyss for the best part
of an hour. They are cleared on leaving the fight as well, and a test pins it.

### The blind spot the mutation pass found

Two mutations survived the first pass — a nine-second repeat instead of ninety, and a
repeat branch that never re-arms — and both for one reason: **the first repeat rides
the timer the *opening* branch armed**, so watching only that far cannot tell the
repeat's own period from anything. The pin now runs to the second repeat. Worth
carrying to any boss whose opener and repeat arm the same slot.

**Verification.** Full suite 1,261 passing and 1 skipped; ten pins across both
captains; all eight mutations caught after that fix.

---

## General Chunapa — burrows under the two most-hated

**NPC:** general chunapa (218183), Cygnea, LEGENDARY, nine-hour respawn, on plain
`aggressive`. **Pattern:** `LDF4a_SandWarm_General`. The shirik burrow (282556, ELITE)
was spawned by nothing anywhere.

His spawn sits in `spawns/Npcs/Custom/Sandstorm_Targets.xml`. That folder is otherwise
this server's own additions — mailboxes, stigma masters, training dummies — so it was
worth checking before spending effort; but he is a real nine-hour world boss placed in
Cygnea, so the content is reachable and the port stands.

**Phase two opens burrows.** Between 51 and 75 he puts one under each of the **two
most-hated** players within seventy-five metres, and repeats every forty-five seconds
while the fight stays in that band. Each lasts sixty-four seconds, so the first pair is
still standing when the second opens. Below 51 the branch stops matching.

### Heartbeats that switch themselves off

Retail runs four three-second timers whose only job is to notice a phase boundary, and
**the phase branch answering each one does not re-arm it** — so the heartbeat goes
quiet the moment it has done its work. That is how an unflagged branch fires once
without a flag: timer 0 ticks every three seconds until health first goes under 75, its
phase branch lights timer 1, and timer 0 stops. A third idiom for once-only behaviour,
alongside the flag ladder (Karemiwen) and the plain one-shot.

It also produces a small timing quirk the tests had to be corrected for: timer 0 and
timer 1 both come due at three seconds, and the phase branch re-arms timer 1 as it
passes, so the **first** pair of burrows lands a tick later than the arming suggests.

### The casts are not translated, and with them three of the four phases

His comments are among the best in the corpus — 가시분출 "thorn burst", 소화액
"digestive fluid", 스턴 "stun", 격노 "rage", 처형 "execution" aimed at whoever has the
least health — and **none of it is usable, because he has no `npc_skills` entry at
all**. Good comments do not help when the other half of the mapping is missing. This is
the Golden Tatar case, not the Derakanak one, and it is worth stating plainly: the
comment quality that made Derakanak and Teselik resolvable is necessary, not
sufficient.

Omitted with the casts: phase one's thorn-burst and digestive-fluid timers, phase
three's pair, phase four's paralysis and execution timers, and the stuns that mark each
transition. **Also not translated:** the door he controls on engaging — method
semantics still unresolved, see the correction entry above — and his five shouts.

**Verification.** Full suite 1,268 passing and 1 skipped; seven pins; all eight
mutations caught.

---

## Re-measuring the backlog, and what it says to do next

The queue this work has been drawing from was built many commits ago. Re-derived from
scratch:

**722 fightable retail adds our server never spawns, across 470 encounters.** Of those:

| bucket | count |
|---|---|
| fully self-contained | 631 |
| positionable, but walk a server-side path | 47 |
| blocked on server-side waypoint paths | 44 |
| (of all the above, carrying a sibling we already spawn) | 20 |

Down from 739 at the last measure and ~805 when this began. The self-contained 631 sit
across **444** encounters — a long tail: the largest single encounter is missing seven,
and most are missing one or two. There is no more low-hanging fruit of the
"boss-with-a-whole-fight-missing" kind; that seam is worked out.

**A ranking mistake worth recording.** The first pass at ranking encounters filtered on
the `BLOCKED` marker alone and put `BIDRuneWP_Main_CallVritra02` on top with ten adds —
every one of which is annotated `[walks a server-side path]`, a different marker for a
different blocker. Filtering on both drops it off the list entirely. The audit prints
the distinction; the ranking has to read it.

**What the corrected ranking says.** The top entries are no longer bosses with no AI at
all. They are **existing AI classes with partial coverage** — Tiamat's dying phase
(7 and 4, still blocked on breath indices), the Yamennes pair (7 and 6), Tahabata
Pyrelord (4, which has had `tahabata_pyrelord` all along and is missing a primal dragon,
a flame center and two summon spots). That is the shape of the remaining work: not
"port the fight", but "finish the fight someone already started".

---

## Yamennes Blindsight and Painflare — two adds an existing class never spawned

**NPCs:** durable yamennes blindsight (219555) and unstable yamennes painflare (219563),
Unstable Splinterpath, both LEGENDARY and both already on `UnstableYamennesAI`.
**Patterns:** `IDAbRe_Core_NamedD_02` and `..._Hard_02`.

The portal cadence in this class was corrected against these same patterns earlier in
this work — and **two of the encounter's adds were missed at the time**, because the
question then was "is the timing right", not "is everything here". Both were spawned by
nothing anywhere:

- **Protector's fury (281819, ELITE)** — retail's `IDCatacombs_Hard_Buff`. One on each
  of the most-hated, a minute into the fight and every twenty seconds after, ten-second
  life. Two for Blindsight, **three** for Painflare.
- **Yamennes sliver (282065, ELITE)** — retail's `IDAbRe_Core_Sum_NamedD_onDie`. Left
  where the top of the hate list stood when it falls, with no lifetime. One for
  Blindsight, **two** for Painflare.

Both additions are purely additive: the gate logic, its coordinates and its alternation
are untouched, which matters because that part works and the ids in it are deliberate
substitutions (see the earlier entry).

Three of the six adds the audit lists against this encounter are those gate
substitutions — 283203, 283222 and 283223, which our data covers with 219567, 219579 and
219580. The audit flags them for spot-check and the spot-check says leave them: only
ours carry the AI that makes a gate do anything.

**Verification.** Full suite 1,273 passing and 1 skipped; five new pins across both
difficulties; all seven mutations caught.

---

## Tahabata Pyrelord — an enrage that started too early and ran half as long

**NPC:** tahabata pyrelord (215280), Dark Poeta, on `tahabata_pyrelord` since long before this
work. **Pattern:** `Dragon_G1`. Two corrections and one addition, all against retail:

- **The enrage ran on a five-minute fuse** where retail arms battle timer 9 at **ten**.
- **It started counting on spawn**, where retail arms it in `on_enter_attack_state` —
  so a group that spent four minutes fighting its way to him arrived with one minute to
  kill him. Both halves of that were wrong, and both made the fight harder than retail.
- **The primal dragon (281265)** he leaves where he falls was spawned by nothing anywhere.

### What is deliberately not reconciled

Retail also places two kinds of short-lived marker at fixed arena points — a flame
center (281261) on four points, and summon spots (281262, 281263) on four more — each
living ten seconds, across most of its timer branches. This class instead spawns
faithful subordinates (281258, 281259) off the casts of Eruption of Power and Powerful
Flame.

**These are not the same things under different ids.** Retail's are the markers a summon
emerges from; ours are the summons. Reconciling them means rebuilding the fight as a
timer table rather than a skill hook — more than a correction, so it is written up here
rather than attempted.

One thing worth noting for whoever does: **the flame center's four points are the same
four Vanuka Infernus uses** — (1177,1241), (1173,1231), (1187,1229), (1190,1238). Dark
Poeta's dragons share the arena and its hazard spots, which is a useful cross-check on
any future translation of either.

### A mutation that took three attempts to catch

Removing the latch that lights the fuse only once survived twice. First because nothing
in the tests actually hit him — `Rehate` adds hate but never calls the attack handler.
Then, once it did, it still survived: **scheduling is not cancelling**, so the extra
tasks each hit books do not delay the original, they pile up alongside it. The symptom
is not a late enrage but *one per swing* from the ten-minute mark. The pin now asserts
exactly one.

**Verification.** Full suite 1,277 passing and 1 skipped; four pins; all five mutations
caught.

### An unrelated flake, recorded

One `Aion.LoginServer.Tests` case failed once during a full-suite run in this session and
did not reproduce in nine subsequent runs (three full-suite, six of that project alone),
so its name was never captured. Nothing in this work touches the login server. Recorded
because a test that fails one time in ten is worth knowing about before it is blamed on
something else.

---

## Tiamat's dying phase — the breath indices, half solved

This has been the standing blocker: a 45-branch spec written long ago, unportable
because the breath skills could not be mapped to `SKILLI_INDEX_1/2/3` and she has **no
`npc_skills` entry**. Two pieces of that fell out this session, and one did not.

### Solved: what the indices are anchored to

The dying pattern spawns a **breath beacon alongside every breath cast**, and the
beacons are named by number:

| branch casts | branch spawns |
|---|---|
| `SKILLI_INDEX_1` | `IDTiamat_Breath_Beacon1` (283155) |
| `SKILLI_INDEX_2` | `IDTiamat_Breath_Beacon2` (283156) |
| `SKILLI_INDEX_3` | `IDTiamat_Breath_Beacon3` (283157) |

The beacon number *is* the skill index, on every branch, without exception. That is a
naming anchor of a kind nothing else in this corpus has offered — the pattern labels its
own indices. **Any future work here starts from that, not from scratch.**

### Solved: the three breaths already exist in our code

`TiamatWeakenedDragonAI` casts **20922, 20924 and 20926** — all three named "Ultimate
Atrocity" — chosen at random in normal mode and, in hard mode, by the *median angle of
the raid around her*: 20924 when they are massed east, 20922 north, 20926 south. Each
already spawns its own hazard line on cast. So the breaths are implemented; only the
mapping to retail's indices is missing.

### Not solved: which beacon is which breath

The beacons will not separate on position. All three sit on y ≈ 514.7, at x 458.5 or
485.5 — the two ends of one line — whereas the three implemented breaths spray at
y ≈ 550 (20922), y ≈ 514.6 (20924) and y ≈ 480 (20926). Only 20924 shares the beacons'
line. Two readings fit and nothing separates them:

- the beacons mark *where a breath starts*, so all three share an origin and the number
  distinguishes direction; or
- the dying phase breathes only along the middle line, and the beacons mark *stages*
  along it.

The dying pattern's arena coordinates also differ from the ones this class already uses,
so placing its absolute spawns is not safe on the strength of a name match alone.

**Deliberately not wired.** Guessing here would put a telegraph in front of the wrong
breath — worse than no telegraph, because players would learn to dodge the wrong way.
What is needed is one observation tying a beacon number to a breath direction: a video,
a client effect binding, or a `Beacon` referenced from another pattern whose geometry is
unambiguous.

**Still missing on this encounter**, and now with a clear reason rather than a shrug:
the three beacons (283155-7), the burrowing-attack markers (283057, six at fixed points
on a cast-free branch — the one piece here that *is* index-free), and on hard mode the
six lv1 markers plus a path-blocked gravity crusher.

---

## The eight gateway guards — a trap ladder per faction

**NPCs:** Trigon, Lord Skyrose, Lord Agios and Lady Eiros in Inggison; Matigium, Sands
Kukinsia, Sibarum Darkwing and Revolver Blackhands in Gelkmaros. All eight LEGENDARY,
all eight on plain `aggressive`. **Patterns:** `GwLGuard_FlA` and `GwDGuard_FlA`, which
are identical bar the faction prefix on the trap names — so one class serves both and
picks the ids per guard.

**All eight trap types were spawned by nothing anywhere.** The largest single block of
missing content left in the audit, and it went out as one class.

| when | trap |
|---|---|
| on engaging | snare |
| below 70 | throw |
| below 50 | explosion |
| below 30 | mine |

Each within two metres of the guard, each lasting a minute, each a one-shot. Below 10 it
calls out once more and lays nothing.

### The empty rungs are load-bearing

Retail interleaves one-shots at 60, 40 and 20 that only cast. They are reproduced as
bare re-arms even though the casts are not translated, because **each occupies the
timer-0 tick it fires on** — drop them and every trap below comes forward by five
seconds. A guard pulled at 5% does not skip to the bottom: every threshold under 70
matches, and because each is a separate one-shot they fire in turn a tick apart, so the
whole ladder goes down over about fifteen seconds.

That last behaviour was the opposite of what the first test asserted, and the test was
wrong rather than the code.

### Two mutations that needed better pins

- **Widening the mine's threshold to 70** survived, because the ladder walks: the mutant
  lays the mine at 65 and then lays the throw one tick later anyway, so a test asserting
  only "the throw appeared" passes. The pin now also asserts that traps from rungs the
  guard has *not* reached are absent.
- **Dropping the empty rungs** survived, because every window was generous enough to
  absorb ten seconds. The pin now checks the throw is *still absent* at thirty seconds,
  which is only true if the empty rungs are spending their ticks.

**Not translated:** ten skill indices with no branch comments naming any of them, the
timer-1 ladder that casts a different skill per health band, timer 2's coin-flip pair on
a fifteen-minute fuse, and the four shouts and four broadcasts that accompany the rungs.

**Verification.** Full suite 1,286 passing and 1 skipped; nine pins across both factions;
all eight mutations caught after the two fixes above.

---

## The three awakened chamber lords — and when shared absolute coordinates are safe

**NPCs:** awakened krotan lord (215136), kysis duke (215179) and miren prince (215222),
all HERO, all on plain `aggressive`. **Pattern:** `BGuard_ChiefD`. The illusion gate
(281226) and both dredgion elite fighters (296338, 296339) were spawned by nothing
anywhere.

- **below 25** — an illusion gate opens at its feet and stands ten minutes
- **on dying** — six drakan arrive by teleporter, two at each of three points, and three
  more through the barrier. Eighteen seconds and twelve respectively: a parting shot,
  not a second fight.

### The single-owner rule, refined

The death spawns are placed absolutely, and this pattern has **three** owners — which by
the standing rule makes absolute coordinates untrustworthy. Here the check passes, for a
reason worth recording rather than assuming: the three chambers are separate maps
(300140000, 300120000, 300130000) that **share one layout**. Each lord stands at
(526.4, 845.3) in its own map, and the coordinate ranges match across all three.

So the rule is not "one owner or nothing". It is: **absolute coordinates are safe when
the owners' maps agree, and our own spawn data can settle that in one query.** Comparing
where each owner is placed, and the coordinate span of each map, is the check.

The pattern's z of 200 is nominal — the chambers' own spawns sit near 190 — so the death
wave is placed at the lord's own height instead.

### A comment of mine that was wrong, caught by a mutation

The two one-shots at 26-50 and 51-75 are kept as bare re-arms, and I first wrote that
they matter the way the gateway guards' empty rungs do — each spending the tick it fires
on. **That is false here**, and removing one survived the mutation pass because of it.

There the empty rungs mattered because a deeper *trap* rung would otherwise match on the
consumed tick. Here the only rung that does anything is guarded below 25, which cannot
overlap 26-50 or 51-75, and both empty branches do exactly what the catch-all beneath
them does. They are genuinely inert. They are still kept so the table reads against the
pattern — but the comment now says so, instead of claiming behaviour they do not have.

The same reasoning does not transfer between bosses just because the shapes look alike;
whether an empty branch is load-bearing depends on what else could match that tick.

**Not translated:** ten skill indices with no branch comments, the world flag set on
engaging and dying that nothing on our side reads, the broadcast on leaving, and the
`on_message` 6682 dismissal that no ported NPC sends.

**Verification.** Full suite 1,296 passing and 1 skipped; eleven pins across all three
lords; seven of eight mutations caught, the eighth confirmed equivalent and the comment
that misdescribed it corrected.

---

## The harness trap that cost three detours

Three times in this work an add spawned nothing and the test read zero: the frost bombs
(`useSkillAndDie`), the Vritra traps (`trap`) and the illusion gate (`groupgate`). The
cause each time was the same and is worth writing down where it will be found.

`BossAiHarness.WithAi` registers only the handlers named. When a boss spawns an add whose
`ai_name` has no handler registered, `AIEngine.NewAI` throws — and
`VisibleObjectSpawner.SpawnNpc` catches **every** exception, logs it to a
`NullLoggerFactory` logger, deletes the NPC and returns it anyway. The spawn produces
nothing and there is no message anywhere.

In production that catch is right: one bad spawn should not take the server down. In a
test it means a missing registration and a broken table look identical.

**It is a papercut, not a correctness hole** — a test asserting "the add appeared" does
fail, every time, which is how all three were found within a step. What it costs is
diagnosis, and only because the failure points at the wrong thing. `WithAi`'s
documentation now says so, with the three cases named, so the next person checks the
registration list before the table.

Not changed: the catch itself. Making `SpawnNpc` rethrow would suit tests and hurt the
running server, and swapping its logger means making a production field settable for a
test's benefit. Neither trade is worth it for a failure that already fails loudly.

---

## Where the backlog stands

Screened and rejected this round, with reasons, so they are not re-derived:

- **`IDSeal_Scene_17_QuestNPC`** (6 "adds", the largest remaining count) — Masionel and
  Parsia. The six are **level-variants of those same two NPCs**, and neither owner is
  placed by any spawn file. A cutscene ladder, not a fight with adds. The audit counts
  variants it cannot tell apart from reinforcements; this is what that looks like.
- **`DF4_GH_KJS`** (enraged mastarius) — all four adds both waypoint-blocked *and*
  sibling-substituted by ids we already spawn. Nothing to do.
- **`IDSeal_Glacier_Spread_Summon_01`/`_02`** — the owners (855607, 855608) are not
  placed by any spawn file either.

**Still open, in the order they are worth attempting:**

1. **Tiamat's dying phase** — the beacon↔index anchor is established (see above); what
   remains is one observation tying a beacon number to a breath direction.
2. **Tahabata Pyrelord's markers** — needs the fight rebuilt as a timer table rather than
   a skill hook, and the flame-center points are shared with Vanuka Infernus.
3. **`Bionic_EhA`** (telepathy controller, Dark Poeta) — three adds against an existing
   class: a bionic clodworm and the control room's entrance and exit. Not yet screened.
4. The long tail: 444 encounters, most missing one or two adds each.

---

## An audit blind spot: ids computed rather than passed

`audit_missing_adds.py` looked for a literal *immediately after* a spawn call's opening
paren. That misses every call that computes the id, and the idiom is common:

```csharp
RndSpawnInRange(Rnd.NextInt(2) == 0 ? 281150 : 281334, 7, 10)
```

Both of `TelepathyControllerAI`'s adds read as never spawned while the class had been
placing one of them every sixty seconds. The scan now walks to the matching paren and
takes every npc-shaped literal in the argument list, stopping at a statement boundary so
a malformed call cannot run on into the rest of the method.

**The correction is large.** 193 more ids are recognised as spawnable, and the backlog
drops from 722 missing adds across 470 encounters to **685 across 449** — 594 of them
fully self-contained, across 423 encounters. Thirty-seven of what this work has been
prioritising against was noise.

### What this did *not* invalidate

None of the ports. Every add was checked individually with a `refs=` query before being
written, which is what the pre-flight exists for — the audit ranks, the per-add check
decides. That division held: the tool was wrong and no wrong code shipped because of it.

### A second blind spot, left in deliberately

The gateway guards this work ported still read as four missing adds each. Their trap ids
live in `new Traps(281472, …)` and are selected with `Lay(t => t.Snare)` — reaching the
spawn through a *record field*, which a regex cannot follow.

Fixing it properly needs a type resolver. Fixing it cheaply means harvesting every
literal from every constructor call, which would swallow skill ids — the same width, the
same classes — and **fail in the dangerous direction**, marking real gaps as covered. A
false positive costs a wasted screening; a false negative hides content forever. Left as
a documented false positive, in the tool's own docstring as well as here.

---

## The illusion gate — content that only became findable after porting its owner

**NPC:** illusion gate (281226). **Pattern:** `BGuard_DrGateChiefD`. Its three guards —
warguard (281227), bowguard (281228) and aetherguard (281229) — were spawned by nothing.

This one is worth recording for *how* it was found. The gate is opened by the awakened
chamber lords below 25%, and until those were ported the gate itself was unreachable, so
the audit never listed it: an encounter whose owner nothing spawns is correctly excluded.
Porting the lords made the gate spawnable, and **the next audit run surfaced a new
encounter with three more missing adds**.

The backlog is not a fixed list being worked down. Finishing one encounter can uncover
the next, and the only way to see that is to re-run the audit after landing work rather
than continuing down a queue derived once.

**The gate is a spawner, not scenery.** Engaged, it pours out five guards and closes:

| when | what |
|---|---|
| five seconds in | a warguard and an aetherguard |
| thirty seconds later | a bowguard and two more aetherguards |
| five seconds after that | the gate closes, leaving them behind |

It also listens for message **10009** — exactly what the chamber lord broadcasts when it
leaves the fight. Reset the lord and its gate shuts on its own. That pairing was only
visible reading both patterns together, and neither half makes sense alone.

### A change in kind, stated plainly

Our data had this npc on **`groupgate`** — the dialog-driven portal AI that 207539 and its
neighbours use to teleport a group. That is a devname match, not a behaviour match:
retail gives this npc attack-state handlers and an ELITE rating.

Moving it onto the pattern runtime therefore also **makes it aggressive**, where before it
stood inert offering a dialog. That is the retail behaviour and the reason the guards
exist at all, but it is a change in kind rather than in detail, so it is called out here
rather than buried in a table.

It also broke a pin on the chamber lords: their test looked for the gate eight seconds
after it opened, and an unaggroed gate now goes home and shuts itself. The pin catches it
as it appears instead. **Nothing translated:** this pattern casts no skills and every
branch is ported.

**Verification.** Full suite 1,301 passing and 1 skipped; five pins; all seven mutations
caught.

---

## Re-measure after the gate, and the Dark Poeta arena finding

Re-running the audit after landing the illusion gate, as the previous entry argued one
should: **682 missing adds across 448 encounters, 591 of them self-contained across 422**.
The gate's three are resolved and nothing new appeared in their place.

The top of the ranking is now entirely **already-analysed or known-blocked**:

| encounter | state |
|---|---|
| Tiamat's dying phase (7 and 4) | blocked on one beacon-direction observation |
| `IDSeal_Scene_17_QuestNPC` (6) | cutscene level-variants, not adds — rejected |
| `IDAbRe_Core_NamedD_*` (5 and 4) | gate ids we deliberately substitute |
| `GwDGuard_FlA` / `GwLGuard_FlA` (4 each) | **false positives from our own record-constructor blind spot** — ported |
| `DF4_GH_KJS` (3) | waypoint-blocked *and* sibling-substituted |

That is worth saying plainly: **the ranked head of this backlog is now noise and known
blockers, not work.** The remaining real content is the long tail — 422 encounters
missing one to three adds each, where the screening cost per encounter is close to the
porting cost.

### The Dark Poeta dragons share one arena's hazard points

Screening Calindi Flamelord (`Dragon_G2`) confirmed something first noticed on Tahabata:
**all of Dark Poeta's dragons place their hazards on the same fixed points.**

- flame rain / flame centre — (1177, 1241), (1173, 1231), (1187, 1229), (1190, 1238)
  — used by Tahabata (`Dragon_G1`), Calindi (`Dragon_G2`) and Vanuka (`Dragon_G3`),
  the last of which is already ported against exactly these coordinates
- summon spots — (1192, 1254), (1169, 1246), (1173, 1217), (1198, 1224)
  — shared by Tahabata and Calindi

This is a free cross-check for any future work on that instance: a translation of one
dragon that lands on different points than another is wrong, and the ported Vanuka gives
a known-good reference for the flame set.

**Calindi is not ported**, for the same reason Tahabata's markers are not: retail places
short-lived markers on timer branches, our classes spawn different long-lived things off
skill hooks, and reconciling means rebuilding the fight as a timer table rather than
patching a spawn in. Calindi additionally has several classes in our tree
(`CalindiFlamelordAI`, `DarkPoetaCalindiFlamelordAI`, `HM_CalindiFlamelordAI`,
`CalindiSummonsAI`, `CalindiSurkanaAI`), so establishing which one 215281 actually runs is
a prerequisite and was not attempted here.

**Still unscreened**, and the honest next candidates: `DGuard_Kistenian` (204753, three
adds, two of them level 1 and so likely control objects rather than fighters) and
`LF4_GH_KJS` (258203, two "yushin" adds plus one waypoint-blocked).

---

## Kistenian — fully mapped, deliberately not ported

**NPC:** kistenian (204753), Beluslan, LEGENDARY. **Pattern:** `DGuard_Kistenian`. Three
ELITE adds, all spawned by nothing anywhere. This entry exists so the next attempt starts
from a mapped encounter rather than a dump.

### What each add is, and what triggers it

| npc | devname | trigger |
|---|---|---|
| 295179 flame of kistenian | `..._FireElemental_Al` | **on entering combat**, at his feet, permanent — and again on message 10018 |
| 295180 fire spirit | `..._Pet_An` | on message **10016**: two on the current target within 50m, three on a 25% roll, six-second life |
| 295181 dredgion elite fighter | `..._Despawn` | **on dying**, at his feet, six-second life |

Despite the name, 295181 is a death effect, not a fighter — the audit reports the display
name and this one is misleading.

### The message loop, mapped

Kistenian broadcasts **10014** to seventy-five metres every three seconds. The replies
come from patterns of their own:

- **10016** and **10015** ← `DGuard_KistenianPet` (the fire spirit's own pattern)
- **10018** ← `DGuard_KistenianDespawn` (the death effect's own pattern)
- **10015** ← additionally every abyss artifact guard pattern, both factions

So two of the three adds are reachable only once their *own* patterns are ported: the pet
and the death effect talk back, and his replies are what put more of them out. Only the
flame on engaging and the effect on dying need nothing else.

### Why it is not ported here

He runs **`AbyssGuardSimpleAI`**, which **859 NPCs share** and which overrides
`CanHandleEvent` and `HandleCreatureSee` — real abyss-guard behaviour, not a placeholder.
`PatternAi` extends `AggressiveNpcAI`, so the pattern runtime cannot be dropped onto him
without losing that. The correct shape is a hand-rolled subclass of `AbyssGuardSimpleAI`
adding the two unconditional spawns, exactly as `TahabataPyrelordAI` was extended — but
whether `CanHandleEvent` filters the attack and death events those hooks need has to be
checked first, and that check was not done here rather than guessed at.

**Two of the three adds are one small subclass away.** The third needs the pet's pattern
as well.

### Also screened

`LF4_GH_KJS` (enraged veille, 258203): its two remaining adds are both named "yushin",
share one name_id, and run `ai="general"` — a non-combat handler. NORMAL and HERO
variants of the same figure, so almost certainly a quest or scene NPC rather than a
reinforcement. Not pursued.

### Kistenian — ported, after the blocker turned out not to be one

The previous entry stopped short of porting him because he runs `AbyssGuardSimpleAI`,
shared by 859 NPCs, whose `CanHandleEvent` override might have filtered the events the
hooks need. **It does not** — it special-cases only `CREATURE_MOVED`, and everything else
falls through to the base. One read settled it.

So `KistenianAI` extends that handler rather than replacing it, the same shape
`TahabataPyrelordAI` uses, and adds the two spawns that need nothing else:

- **on entering combat** — a flame of kistenian beside him, with no lifetime, cleared when
  he leaves the fight, dies or despawns
- **on dying** — the despawn effect, six seconds

The flame is latched to the first swing. Without that, every hit would light another, and
the mutation pass showed five pins failing when the latch is removed — the same class of
bug the Tahabata enrage had, where a handler that fires on every attack schedules work
each time.

**The fire spirits (295180) remain missing**, and the reason is now precise rather than a
shrug: they arrive on message 10016, which `DGuard_KistenianPet` broadcasts — the fire
spirit's own pattern. He calls out with 10014 every three seconds to seventy-five metres
and they answer. Neither the heartbeat nor the reply handler is implemented, because a
broadcast nothing listens for and a listener nothing broadcasts to are both silence.
Porting the pet's pattern unlocks both, and would also unlock message 10018, which places
a second flame from the death effect's own pattern.

**Verification.** Full suite 1,308 passing and 1 skipped; seven pins; all seven mutations
caught.

### Kistenian's companions — the loop closed, with one half unverifiable

`DGuard_KistenianPet` and `DGuard_KistenianDespawn` are now ported, which completes the
mechanic the previous entry left open. Neither companion has an `npc_skills` entry, so
every cast is unresolvable and everything below is index-free.

**The fire spirit (295180)** calls **10016** every twenty to forty seconds — a quarter of
the time twenty, half the rest thirty, otherwise forty — switching to a random attacker as
it does; reports **10015** once each on first crossing 75, 50 and 25 percent; leaves the
despawn effect where it dies; and removes itself on hearing **10017**.

**Kistenian** now answers: **10016** brings out two spirits on his current target, three on
a quarter roll, each lasting six seconds; **10018** lights another flame. He calls **10014**
to seventy-five metres every three seconds, which is what the spirits answer. Flames now
accumulate and are tracked as a set, since each spirit death hands him one — leaving the
fight clears all of them, not just the first.

### One half could not be verified, and is written up rather than glossed

The despawn effect shouts **from `on_wake_up` and removes itself in the same branch**, so
it broadcasts at the instant it enters the world. `NpcMessageBus` walks the *sender's*
known list, and a just-spawned NPC's known list is not populated in the harness — the cry
reaches nobody, and a test asserting the other spirits disperse fails.

**Whether the live server populates a known list before the AI's spawn hook runs was not
established.** If it does not, this pattern is inert on the server too, and the fix belongs
in the bus or the spawn ordering rather than in this class. The test was removed rather
than weakened to pass, and the concern is recorded in the class itself.

### A pin that was right until the port made it wrong

An earlier test asserted the death effect stands for six seconds. That was true only while
the npc had no AI of its own — its `live_time` was doing the work. Giving it its retail
pattern, which despawns on waking, made the pin wrong. **The pin was wrong, not the port**,
and it now asserts the effect removes itself at once.

### An equivalent mutant recorded

Removing the `!engaged` guard from Kistenian's message handler survives: `SendSpirits`
already returns when there is no most-hated target, which cannot happen before he is
engaged. The guard is defensive redundancy rather than behaviour, and is kept for
readability.

**Verification.** Full suite 1,312 passing and 1 skipped; twelve pins; five of seven
mutations caught, one equivalent as above and one covering the unverifiable broadcast.

---

## `on_wake_up` broadcasts reach nobody — traced, not fixed

The previous entry left open whether the despawn effect's cry works on the live server or
only fails in the harness. **It fails everywhere**, and the cause is exact:

```
World.Spawn(obj):
    obj.GetController().OnAfterSpawn();   // NpcController fires AiEventType.Spawned here
    obj.UpdateKnownlist();                // ...and the known list is built here
```

`NpcController.OnAfterSpawn` raises `Spawned`, which `PatternAi.HandleSpawned` answers by
evaluating `OnWakeUp`. So **any `on_wake_up` branch runs before the NPC knows about
anything around it**, and `NpcMessageBus.Broadcast` walks exactly that known list. A
just-spawned NPC broadcasting reaches nobody.

**The Java reference has the identical order** — `onAfterSpawn()` then `updateKnownlist()`
— so this is a faithful port of upstream, not a porting slip. It has never mattered
upstream because aionemu implements no pattern that broadcasts on waking.

### Why it is not fixed here

Three options, none of them safe to take at the end of a session:

1. **Reorder `World.Spawn`** — correct-looking, and diverges from Java on shared world
   code that every spawn in the server passes through. The golden rule allows infrastructure
   divergence, but this is a behavioural change, not an idiom change, and the blast radius
   is everything.
2. **Defer the pattern runtime's wake-up** by a tick, local to `PatternAi` and away from
   shared code. Smaller blast radius, but it changes *when* every `OnWakeUp` branch runs,
   including ones that place furniture other classes then look for.
3. **Fall back in the bus** — if the sender's known list is empty, scan the map region.
   Targeted and timing-neutral, but a region scan is a cost on a hot path and needs
   measuring.

Option 3 looks best on the evidence and is the recommendation, but the measurement was not
done and picking on a hunch is how a hot path gets slower for a one-NPC mechanic.

**Only one ported pattern is affected today**: `KistenianDespawnEffectAI`, whose cry should
disperse the fire spirits and hand Kistenian a flame. Its class doc carries this note too.
Everything else that broadcasts does so from a battle timer or a death, by which point the
known list is long since built. **Any future pattern with a broadcast in `on_wake_up` will
be silently inert until this is settled** — that is the reason to record it here rather
than in a comment on one class.

### Fixed, the way the last entry recommended

Option 3 from the entry above is implemented: `NpcMessageBus` falls back to the sender's
map region when its known list is **empty**. The concern that stopped it — a region scan on
a hot path — does not apply, and the reason is the gate: every broadcast from a battle
timer, a death or another message runs with a populated list and takes the original path
untouched. Only a just-spawned NPC reaches the fallback. No reordering of `World.Spawn`,
so no divergence from Java on code every spawn passes through.

The disperse half of Kistenian's loop is now pinned: killing one fire spirit clears the
rest. That test existed, failed, and was removed last round rather than weakened — it is
back and passing.

**Scope deliberately narrowed by a mutation.** The first version also scanned neighbouring
regions, and mutating that away broke nothing: retail's wake-up broadcasts carry ranges of
fifty metres or less against far larger regions, so the extra breadth was untestable and
would have been code no pin could reach. It was **removed rather than tested around**. The
known limit is now stated in the class: a wake-up broadcast from a sender close to a region
edge will under-deliver.

**Two mutations survive and are equivalent, not gaps.** Making the fallback run
unconditionally passes everything, because a region scan is a superset of the known list —
that is a performance change, not a behavioural one. Removing the range check also passes,
because every NPC in these tests is within range of every other; pinning it needs a
bus-level test with deliberate distance, which is worth writing when the bus next changes.

**Verification.** Full suite 1,313 passing and 1 skipped; the restored disperse pin brings
Kistenian to twelve.

---

## What the bus fix unblocked, and a listener of mine that had nothing to hear

**615 retail patterns broadcast from `on_wake_up`.** Until the fallback landed, every one of
them would have been silently inert if ported. The dominant shape is the "despawn" family —
`BGuard_ChiefDespawn`, `DGuard_KistenianDespawn`, `ABRwd_DespawnBox`, `Bionic_EhADead` and
their kin — NPCs whose entire pattern is *appear, shout, vanish*. That idiom is how retail
signals across an encounter, and it now works.

### A gap in my own work, found by auditing messages rather than adds

Chasing which of those 615 mattered turned up something closer to home. The illusion gate
carries a listener for message **10009**, which I wrote against the chamber lord's
`on_leave_attack_state` — and then **never implemented that broadcast on the lord**.

A listener nothing broadcasts to is silence. That is a rule this log has stated twice, and
the code shipped violating it anyway, in a pair of classes committed one after the other.
The lord now calls the gate down on disengaging, with a pin that fails if the broadcast is
removed.

**The lesson is about the check, not the bug.** Every port so far has been verified by
asking "does this add spawn?" — a question about *adds*. Nothing asked "does every message
this encounter listens for have a sender, and does every message it sends have a listener?"
Those are cheap greps and they catch a class of half-built mechanic that add-counting
cannot see. Worth doing across the ported set.

### Still open

`BGuard_ChiefDespawn` broadcasts 10009 too and is **not in our npc data at all** — no id, no
template — and nothing in the retail dump spawns it either, so it is presumably placed
server-side. Whether the chamber-lord encounter is meant to have a second source for that
message is unresolved.

**Verification.** Full suite 1,314 passing and 1 skipped; the new pin fails when the
broadcast is removed.

---

## A message audit, and what it found

`tools/client-extract/audit_ai_messages.py` cross-references every `broadcast_message` and
`on_message` in our AI classes: which numbers we send with nobody listening, and which we
listen for with nobody sending. It exits non-zero when anything is unpaired, so it can gate
a build.

This check did not exist. Every port in this work was verified by asking *does this add
spawn* — a question about adds — and the illusion gate shipped with a listener for 10009
that its chamber lord never broadcast, in two classes committed one after the other.

**Nine pairs matched.** Omega to its clone, Lord Lannok and the coffin both ways, the
chamber lord to its gate, Teselik and his hands both ways, and Kistenian's three-way loop.

**Two false starts in the tool itself**, worth recording because both would have made it
useless:

- scanning `case` labels anywhere in a file swept up skill ids from `OnEndUseSkill` and
  state numbers from `MercenaryAI` — nineteen phantom "unpaired" messages. The scan is now
  carved to the body of `OnNpcMessage` by brace depth.
- a first pass missed hand-rolled listeners entirely, because they switch rather than using
  `When.Message`, and so reported their senders as talking to nobody — exactly backwards.

**Four remain unpaired, all understood:**

| message | state |
|---|---|
| 21212, 21221 → `VritraRearguardAI` | listeners for NPCs not ported; already documented |
| 6980 ← `MacunbelloSoulReaperAI` | predates this work, not investigated |
| 10015 ← `KistenianPetAI` | retail's listener is a cast-only branch, and the cast does not resolve |

### 10014 closed, and one thing left unpinned

Kistenian's three-second call had no listener at all. Retail's fire spirit answers it with a
target switch, gated on `is_distance_longer_than(OBJI_MESSAGE_PARAM, 20)` — only spirits
that have drifted more than twenty metres reply, so the call pulls stragglers back rather
than churning the pack. That is index-free and is now implemented, with a new
`When.MessageParamFartherThan` condition.

**The distance gate is not pinned.** Observing it needs a spirit with no target that then
acquires one, and an aggressive NPC in this harness targets anything spawned within reach
before the message arrives, so the "before" state cannot be staged. Three attempts went
into the harness rather than the behaviour, and the test was removed rather than left
asserting something weaker than it claimed. The branch's existence is what the audit pairs
10014 against; the twenty-metre figure rests on the pattern dump alone.

**Verification.** Full suite 1,314 passing and 1 skipped.

---

## Measuring the other axis: retail messages we never implemented

`audit_ai_messages.py` checks our classes against each other. It cannot see an encounter
that is wired to itself correctly while missing half of what retail does.
`audit_retail_messages.py` is that second axis: for every class whose doc names a retail
pattern, which of that pattern's message handlers do we never touch?

**Fifty-four, across twenty-two ported classes.** Split by why:

| verdict | count | meaning |
|---|---|---|
| `acts` | 40 | a handler that spawns, moves or arms a timer, or a broadcast something in retail really does listen for |
| `cast-only` | 14 | every action in the retail branch is a `use_skill`, and this work does not translate casts it cannot map |
| `unheard` | **0** | a broadcast nothing anywhere in retail listens for |

### The zero is the finding

The tool was built expecting most omitted broadcasts to be announcements — shouts to the
client with no in-game listener, harmless to drop. **There are none.** Every message our
ported patterns broadcast is listened for by some pattern in the corpus. Retail does not
appear to broadcast decoratively.

That reframes fourteen omissions in this log described as "a broadcast nothing listens
for". They have no listener *in our server*, which is true, and a listener in retail, which
is the part that was assumed rather than checked. The gateway guards' four rung
announcements (6301-6304) are the clearest case: written up as announcements, and something
in retail is waiting for each.

### The tool's own first mistake, recorded

It first classified a broadcast by what its own branch does, so a shout sitting in a branch
that also spawns counted as a real gap — every gateway-guard rung lit up. A broadcast that
sits beside a spawn still only broadcasts. It now classifies sends by whether *anything in
the corpus listens*, which is the question that actually matters.

### What to do with it

Not fix all forty. Triage: for each, find the pattern that listens and decide whether that
NPC is content we have. That is the same shape as the adds backlog and wants the same
discipline — the audit ranks, a per-item check decides.

The largest clusters are `KistenianAI` (5), the two gateway guards (4 each), `LordLannokAI`
and `SuspiciousCoffinAI` (3 each, both predating this work), and `MiddleBossFireAI` (5,
all `cast-only`).

### Triaging the sixteen, and the tool's third and fourth mistakes

Following the flagged findings back to their listeners collapsed the report from 54 to 16
and corrected the tool twice more.

**Mistake three: a listener that exists is not a listener that does anything.** The gateway
guards' rung announcements (6301-6304) *are* heard — by `GwDGuard_PhA` and `GwLGuard_PhA` —
and every one of those branches only casts or does nothing. So the original write-up
calling them announcements was right, and the previous entry's correction of it was wrong.
The tool now classifies a send by whether some listener **acts**, which moved eight
findings to `unheard`.

**Mistake four: prose is not a claim.** Every `<c>` token in a class doc counted as a
pattern that class implements, so `KistenianAI` was answerable for its pet's handlers
because its doc explains where message 10016 comes from. Only the line introducing the
retail pattern counts now. That removed fourteen more.

**Final tally: 16 acts, 8 cast-only, 6 unheard.**

### The lead this produced: Adma's skeleton waves

The largest cluster is `SuspiciousCoffinAI`, and it connects to a gap recorded early in
this work and never closed — *"Adma's 12 skeleton-wave adds need something to spawn the
three invisible controllers"*. The mechanism is now exact:

- `ND2_FhWSumA`, `ND2_FhWSumB` and `ND2_FhWSumC` broadcast **6602**, **6603** and **6604**
- the coffin patterns (`NoAction_CoffinA/B/C`, and `_SP` hard-mode twins) listen for them
  and **spawn skeletons** — a faithful page (280933) on 6602, and a diligent page (280949)
  alongside it on 6603 and 6604
- both pages have **refs=0**: spawned by nothing anywhere

So the coffins are the controllers, the three `FhWSum` NPCs are the trigger, and the wave
size grows with the message number. `SuspiciousCoffinAI` is already ported and listens for
none of the three.

**Not implemented here.** It needs the three trigger NPCs resolved and checked for
reachability, the `_SP` hard-mode split understood, and the coffin's existing behaviour
extended without disturbing what it already does for Lord Lannok. That is a session's work
with the mechanism already mapped, rather than a dump to read from scratch.

**Also traced and blocked:** `BGuard_ChiefD_Minor`, whose 6682 makes the chamber lord
despawn itself, is absent from our npc data and spawned by nothing in retail — the same
shape as `BGuard_ChiefDespawn`. Both are presumably placed server-side.

### Adma's skeleton waves, mapped end to end

The gap recorded early in this work — *"Adma's 12 skeleton-wave adds need something to
spawn the three invisible controllers (281045/6/7)"* — is now a complete causal chain
rather than a dead end. The message audit found the middle of it; the binding table found
the ends.

```
Adma_DeathknightNamed_SP  ─┐
Adma_T_Control_04         ─┼─ spawn ─► 281045 / 281046 / 281047   (the invisible controllers)
Adma_T_Named_05           ─┘            patterns ND2_FhWSumA / B / C
                                                  │
                                     broadcast 6602 / 6603 / 6604
                                                  │
                            NoAction_CoffinA/B/C  ▼  listen, and spawn
                                        faithful page (280933) on 6602
                                        + diligent page (280949) on 6603 and 6604
```

**Every link is confirmed.** The controllers are the `BIDDF2A_DeathKnightSum_SumSkels*`
npcs, which is why the id note recorded years of this work ago pointed at them without a
mechanism. The wave grows with the message number.

### Where it is still blocked, precisely

- **The controllers (281045/6/7) are spawned by nothing** — `refs=0` across spawns, skills
  and handlers.
- **The three patterns that would spawn them are all unported**, and one of them is
  interesting: `Adma_DeathknightNamed_SP` is Lord Lannok's *hard-mode* twin. Our
  `LordLannokAI` claims the plain `Adma_DeathknightNamed`, which does **not** reference the
  controllers at all — so in normal mode this mechanic may not belong to Lannok, and
  `Adma_T_Control_04` or `Adma_T_Named_05` owns it instead. Which of the three drives the
  normal-mode instance is the one thing still unknown.
- **`SuspiciousCoffinAI` listens for none of 6602-6604.** It handles 6601 and 6609, Lord
  Lannok's alarm pair, and nothing else.

**The next step is a lookup, not an investigation:** find which npc_ids bind
`Adma_T_Control_04` and `Adma_T_Named_05`, check whether our data spawns them, and the
owner falls out. If one is reachable, the whole chain is three small branches — the
controllers on the owner, a broadcast each, and three coffin branches.

**What made this findable.** Not the adds audit, which had counted these pages for months
without being able to say why nothing spawned them. The message audit crossed the gap,
because the missing thing was never a spawn — it was a conversation.

### The lookup, and why the chain stops there

The binding table answers it, and the answer is a blocker rather than a task:

| pattern | npc bound in our client |
|---|---|
| `Adma_DeathknightNamed` | 214696 — Lord Lannok |
| `NoAction_CoffinA` / `B` / `C` | 280942, 280950, 281055 — the coffins |
| **`Adma_T_Control_04`** | **none** |
| **`Adma_T_Named_05`** | **none** |
| **`Adma_DeathknightNamed_SP`** | **none** |

**All three patterns that spawn the controllers bind to no NPC at all.** So do the `_SP`
coffin twins. That is not "we never ported them" — it is that nothing in the 4.8 client
data this binding table was built from claims those patterns.

The likeliest reading is that the whole `_SP` family is 5.8-era content, and our client is
4.8: the patterns dump is newer than the client we resolve devnames against. If so the
mechanic cannot be bound at all until a 5.8 client is indexed, and no amount of reading the
patterns will fix it.

**This is a limit of the binding table, and it is worth stating as one.** Every
"unreachable" verdict in this log rests on that table, and the table can only see what the
4.8 client names. Content newer than the client reads as absent rather than as unknown.

### One thing that is reachable, and is missing

Lord Lannok's own pattern does something our port does not: **battle timer 11 broadcasts
6605 or 6607 on a coin flip and re-arms itself**, and `on_message` 6608 drives a three-stage
flag ladder of shouts. `LordLannokAI` implements neither — it carries only the 6601/6609
alarm pair it shares with the coffins.

That was already in the message audit's sixteen. It is now the only part of the Adma
mechanism that is not blocked on client data, and the coffins that would answer it are
already ported and already spawned. **That is the next thing to do here**, and it is small:
one timer, two broadcasts, and whatever the coffins do with them.

### Adma's normal-mode waves are reachable after all — full specification

The blocked chain was the wrong one. Chasing Lord Lannok's own broadcasts instead of the
controllers' found a second route where **every NPC binds and every add is one we can
already place**:

```
Lord Lannok (214696, Adma_DeathknightNamed)
    battle timer 11, re-arming every 45s:
        50%  → broadcast 6605 (range 50)
        else → broadcast 6607 (range 50)

Coffins D, E, F (281056, 281057, 281058 — all bound, all on suspicious_coffin)
    on 6605 → spawn faithful page (280933)
    on 6606 → 50% diligent page (280949), else faithful page
    on 6607 → 50% diligent page,          else faithful page
        each ABSOLUTE, spawn_range 0, live_time 180
```

Coffins A, B and C answer 6602-6604 from the unbound controllers; **D, E and F answer
Lannok directly.** The same two pages, reached by a route that is entirely inside content
we have. `LordLannokAI` broadcasts neither message and `SuspiciousCoffinAI` listens for
neither.

### Why it is specified here rather than implemented

One thing does not fit the shape of our port. Retail writes **six separate coffin patterns**
— `NoAction_CoffinA` through `F` — precisely because each spawns at its *own* absolute
point. Our `SuspiciousCoffinAI` is one class serving all six npc ids, so it needs the six
coordinate sets keyed by npc id, the way `MiddleBossFireAI` keys its traits.

That is the work: pull the absolute spawn point out of each of the six patterns, key them,
add three message branches, and add Lannok's timer 11 — including finding where it is first
armed, which was not captured here. Plus pins and a mutation pass. It is a session, and
starting it on the last of one is how the coordinates end up guessed.

**Everything needed is above.** Nothing further has to be derived: the npc ids are
confirmed bound, the two pages are confirmed spawned by nothing, the probabilities,
lifetimes and the 45-second cadence are read from the patterns.

**What this changes about the earlier verdict.** The previous entry concluded the Adma
mechanism was blocked on a 4.8-versus-5.8 client mismatch. That is true of the *controller*
route and false of the whole encounter — there was a reachable path one query away, and the
verdict was written before looking for it. Blocked on one route is not blocked.

### Adma's skeleton waves — built

The gap recorded early in this work is closed on the reachable side. Lord Lannok now calls
and coffins D, E and F answer.

**Two corrections to the specification written last entry**, both found by reading the
patterns again rather than trusting the note:

- the mage rolls are **15%** on the second call and **30%** on the third, not 50%. The
  earlier figure came from the boss's own coin flip between messages and was carried onto
  the coffins' branches, which are a different roll entirely.
- each call sends **one** add — a mage *or* a page, never both. The dump lists the two
  spawns adjacently and they read as a pair; they are the two halves of a probability
  branch.

**Where the fuse hangs.** Retail lights it from battle timer 0 at 26-50 with a one-shot,
and that rotation is not translated, so nothing would arm it. It hangs off `on_attacked`
instead — a real event rather than an invented cadence, firing on every swing and so
noticing the band as soon as he is in it. The one-shot keeps it a fuse.

**The two triplets are the mechanism, not a detail.** A, B and C answer 6602-6604 from the
unreachable controllers; D, E and F answer Lannok's 6605-6607. Guarding each coffin on its
own triplet is what stops an A coffin answering a call meant for D, and a mutation that
made every coffin answer everything is caught by a pin for exactly that.

**A mutation worth recording.** Removing the timer re-arm from *one* of the two call
branches survived the first pass: the chain keeps running while the other side of the coin
flip keeps winning, so a test asking "did anything arrive" cannot see it. The pin now
counts distinct pages over ten minutes and expects at least eight of a dozen.

**Still missing:** coffins A, B and C stay silent, because nothing spawns the controllers
that call them — patterns bound to no NPC in our 4.8 client. Half the waves, and the half
that is blocked is blocked on client data rather than on work.

**Verification.** Full suite 1,320 passing and 1 skipped; six pins; all seven mutations
caught.

---

## The audits started reporting phantoms against correct code

Re-running the message audits after building the Adma waves produced two findings that were
wrong, both caused by shapes this work introduced. A check that cries wolf about correct
code is worse than no check, so they are fixed and recorded.

**Name collisions.** `CallForMore` is declared in `KistenianPetAI` as 10016 and — added the
same session — in `LordLannokAI` as 6607. The scan kept one flat name→value map, so
whichever file was read last silently won, and the report claimed Kistenian's pet was
talking to nobody while pairing Lannok's call to *Kistenian*. Constants now resolve against
the class a qualified token names, then the current file, then globally.

**Message numbers held in a table.** `SuspiciousCoffinAI` keeps each coffin's three calls in
a record rather than in `When.Message`, because six coffins need six different triplets. A
scan reading only call sites cannot see them, so it reported the coffins as deaf and
Lannok's 6605 as unheard. A file that reads `CurrentMessage` is doing its own matching, so
its bare four-to-five digit literals now count as messages it handles.

With both fixed the picture is honest: **8 unpaired, down from a claimed 5 that included two
phantoms and hid two real ones**, and the retail-side `acts` list drops from 16 to 11.

The eight are all understood: the coffins listen for 6602-6604 and 6606, which only the
unreachable controllers send; the Vritra rearguards' two; `MacunbelloSoulReaperAI`'s 6980,
predating this work; and the Kistenian pet's 10015, whose retail listener is cast-only.

## A flaky test of mine, found by the same run

`AHandKnocksItsTargetAboutOnItsOwnTimer` failed in a full-suite run and passed six times in
isolation. Not interference: the hand casts on a **coin flip**, and forty seconds gave it
about four ticks, so missing every one had a one-in-sixteen chance. It had been quietly
failing at roughly that rate.

The window is now two and a half minutes — a dozen ticks, under one in a thousand. Five
consecutive full-suite runs are clean.

**This is the second flake this work has produced from the same mistake**: asserting that a
probabilistic branch fires at least once, over a window sized for the deterministic case.
Worth checking the others — any pin whose subject is a `test_probability` branch needs its
window sized for the tail, not the mean.

---

## The probabilistic-pin sweep, and the naga slaves

### Sweep: one flake, and it was already fixed

Every AI class with a `test_probability` branch was checked against its pins, asking whether
any assertion rides the outcome of a roll over a window sized for the deterministic case.
**None does.** Most are safe by construction, and it is worth recording why, because the
shape recurs:

- **a fallback with no guard** — Gatekeeper Flox's four eye placements, the coffins' mage
  rolls, the naga captain's call interval. One branch always matches, so *something* always
  happens and only *which* is random.
- **both branches doing the same observable thing** — RM-56c's band timers, whose casts are
  untranslated so both sides merely re-arm; the Danuar summons, where both switch target.
- **pins that assert counts rather than choices** — Wrathclaw keeps one sphere of each
  however the arrangement rolls.

The one real flake, Teselik's hand knockback, was found and fixed by the run that prompted
this sweep. Tiamat's only positional assertion is on the unconditional wake-up placement,
not the 34% swap.

### The naga slaves detonate

`Naga_WrF`'s last unimplemented message, 3315, turned out to be a mechanic rather than an
announcement. Dropping to 21-40 the captain calls it once, and every slave it summoned
**explodes and removes itself**.

**Both of the slave's skill indices resolve, and the roles corroborate each other** — which
is rarer than a bare count match. Two indices are addressed and the npc has exactly two
skills. Index 0 is cast on waking and again on leaving the fight and is `16921 Fire Sparkle`,
the only BUFF; index 1 is cast in the same breath as despawning and is `16991 Explosion`. A
minion that buffs itself on arrival and explodes when dismissed is a mechanic; the reverse
is nonsense. That makes identity the only reading, not merely the default.

### A pin that was right until this landed

`DroppingOutOfTheBandStopsTheReinforcements` asserted the four slaves *survive* below the
band. True while the dismissal was unported, and wrong the moment it landed — persisting was
this port being incomplete rather than the captain being generous. Corrected to expect an
empty field.

That is the fourth time a pin has had to change because a later port made its subject more
complete. It is worth expecting: **a pin written against a partial translation encodes the
partiality**, and finishing the translation is supposed to break it.

**Verification.** Full suite 1,323 passing and 1 skipped; three new pins; all seven
mutations caught after two invalid patches were redone.

### Vanuka's subordinates answer a rally

The last clean item on the retail-message audit's `acts` list. `Dragon_G3`'s summon
branch below 30% does not only call up a faithful subordinate (281275) — it follows the
spawn with `broadcast_message(3411, range_as_meter 50, param_obj OBJI_CUR_TARGET)`, and
`Dragon_G3SlaveSuLizard`, bound to that npc, answers on **two** branches:

| its state | what it does |
|---|---|
| `NPC_STATE_ATTACK` | `use_skill(idx1)`, then `switch_target()` |
| `NPC_STATE_IDLE` | `add_hate_point()`, then `attack_most_hating(idx1)` |

So a lizard already in a fight is shaken onto someone else at random, and one standing
about is pointed at the boss's own quarry. New handler `VanukaLizardAI`, and 281275
repointed from `aggressive`.

**`is_npc_state` is now in the vocabulary.** Retail branches on it 968 times across the
5.8 files, and `PatternAi` already latched exactly this bit for `on_enter_attack_state`;
`When.Fighting` / `When.Idle` expose the latch it was already keeping.

**The casts are not translated.** Three indices are addressed and the npc has exactly
three skills — 16602 Strike, 17459 Powerful Knockdown, 17471 Tendon Destruction — but all
three are `prob="25"`, none carries a distinguishing attribute, and the pattern's comments
name none of them. A bare count match with nothing to corroborate it is not a resolution.

### Three harness limits this pin ran into, all of them Java-faithful

Getting the boss's own broadcast under test took four attempts, and each failure was the
emulator being right rather than the mechanic being wrong:

- **A summoned lizard arrives already fighting**, so it answers on the `switch_target`
  branch. The idle branch is for the ones loitering in the room, not for the ones the boss
  calls up — the pin had to place a lizard rather than summon one.
- **`AggroList.AddHate` drops a creature the NPC is unaware of**, as it does in Java. A
  distant quarry therefore defeats the mechanic instead of isolating it; keeping the hate
  observable would have meant introducing them, and then the lizard's own aggro explains
  the result. `SetTarget` runs regardless of awareness, so the **target** is the observable
  that isolates the call.
- **The harness has no known-list sweep.** On the live server `World.Spawn` files a new NPC
  into its neighbours' known lists a moment after the AI's spawned event — which is why
  `NpcMessageBus.Nearby` falls back to the map region for a *just*-spawned sender. Nothing
  files anything here, so the pin introduces the pair by hand.

The general shape is worth keeping: when a pin cannot see a mechanic, check whether the
thing blocking it is a guard the Java reference also has before working around it.

**Verification.** Full suite 1,327 passing and 1 skipped; three new pins; all five
mutations caught — the two that survived the first round (removing the rally, and sending
it without the target) are exactly what the end-to-end pin was added for.

### Tahabata Pyrelord gets his rotation back

The deferred item from the first Dark Poeta pass, written up then rather than attempted: the class
was aionemu's, with an enrage timer bolted on and **no rotation at all**. Everything he did between
being pulled and dying was whatever his npc_skills probabilities rolled. `Dragon_G1` is four chained
battle-timer slots per health band, a fifth chain the banded ones never return from, and a ten-minute
fuse on slot 9 — all of it now translated.

| band | chain | what it places |
|---|---|---|
| 81-100 | T1→T2→T3→T4→T1, fifteen seconds a step | nothing |
| 61-80 | T0 hands over to T5→T6→T7→T8→T5 | a ring of four flame centers on the two steps bracketing it |
| 31-60 | T0 hands over to T1→T2→T3→T4→T1 | a ring of four cyclops summon spots, twice per loop |
| below 30 | T0 hands over to T5→T6→T7→T8→T5 for good | a ring of four drakan summon spots |

**The guards on the low chain are worth reading twice.** Entry needs below 30, but every step of the
chain itself only tests below **45** — so it cannot be entered early and cannot be left once running.
Writing all five as `HpBelow(30)` would have been the obvious mistake.

### The summon spots are the mechanic, and we had replaced them with a shortcut

The old class spawned the slaves directly, hung off the casts of Eruption of Power and Powerful Flame,
at eight coordinates of aionemu's own choosing. Retail spawns **neither** slave directly. It places
short-lived *summon spots* — 281262 for the cyclops, 281263 for the drakan — on the same four marks,
and each spot is what calls up its slave. That is why both kinds arrive on the same four points, and
why the wave is bounded by the spots rather than by the boss.

Three NPCs were spawned by nothing anywhere before this: the flame center (281261), and both spots.

**Every marker's cast resolves, and by name rather than by counting.** Each has exactly one skill and
each pattern addresses exactly one index, which alone is only a count match — but the skill names
corroborate it outright:

| npc | retail devname | skill | stack name |
|---|---|---|---|
| 281261 flame center | `Dragon_G1N**FrRain**_A` | 18221 **Flame** Shower | `…_**FRRAIN**NR` |
| 281262/281263 spots | `Dragon_G1Slave**Su**…` | 18222 **Summon** | `…_APPEAR_NR` |
| 281258 subordinate | `Dragon_G1Slave` | 18219 Mana Regression | `…_**SELFBLOW**_NR` |
| 281265 primal dragon | `Dragon_G1**Final**_A` | 18224 **Final** Blow | `…_DRAGON**FINAL**_NO` |

A devname and a stack name agreeing on the same word is the strongest corroboration this work has
found. It also retroactively resolves Vanuka Infernus's flame center (281276), which is the same
NPC shape with the same skill.

### The subordinate is a fuse, and it answers a ring call

`Dragon_G1Slave` is not a fighter. Ten seconds after something engages it, it casts Mana Regression on
itself and four seconds later it is gone; left alone it stands there indefinitely. The aionemu class
had the explosion right — it hooked exactly that skill — and removed it the instant the cast ended, so
the four-second gap did not exist.

What it did not have at all is 3415, the last unimplemented message on this pattern: **every time
Tahabata puts a fresh ring of spots out he first calls the previous wave away.** They do not explode,
they simply leave. That is what holds the wave at four however long he stays in the band.

### A runtime limit found by a mutation that survived

One mutation could not be killed: making the ring call detonate the subordinate on its way out rather
than dismissing it quietly. The reason is not the pin — it is that **a queued self-cast does not
survive a despawn in the same branch.** The control is clean: the same cast is plainly visible when
the despawn comes four seconds later on its own timer, and invisible when the two share a branch.

This is why `NTrap_A` is **not** ported here. Fifty-three NPCs bind that pattern, all of them
cast-once-and-vanish markers, and a literal translation — cast index 0, `despawn_self` — would be
inert for exactly this reason. It needs either a cast path that outlives the despawn or a delay
between the two, which is a runtime question rather than a translation one. The skill ids are all
resolved above, so the follow-up is mechanical once that is settled. Until then the flame centers and
the primal dragon keep their current `aggressive` template entry and are removed by the `live_time`
of whoever placed them.

### Also fixed

`KistenianDespawnEffectAI` carried a caveat wondering whether a broadcast from `on_wake_up` reaches
anyone, since the sender's known list is not built until after the spawn hook. That was answered by
the naga work and never written back: `NpcMessageBus.Nearby` falls back to the sender's map region
precisely when its known list is still empty. The comment now says so.

**Verification.** Full suite 1,337 passing and 1 skipped; ten new pins and four existing ones passing
unchanged against the rebuilt table; twelve of thirteen mutations caught, the thirteenth being the
runtime limit above. Missing adds 679 → **676 across 446 encounters**.

### Calindi Flamelord, the same fight and the same two faults

`Dragon_G2` is Tahabata's twin: same arena, same marks, same four-chained-timers-per-band shape, and
the class had both of his faults — **no rotation**, and an enrage armed in `HandleSpawned` where
retail arms it in `on_enter_attack_state`. A group that spent four minutes reaching her arrived with
six on the A-rank clock.

Where she genuinely differs, and it is not renaming:

- her low chain places **two** drakan spots, on the first and third marks; his places four
- she turns on her **second** most hated on one step where he takes a random one
- her 81-100 wrap arms T2 rather than T1, so T1 fires exactly once all fight
- she leaves no primal dragon — retail's `on_killed_by_user` clears her markers and nothing else

**Two calls rather than one.** 3413 rides every ring of worm spots and 3412 every pair of drakan
spots, each clearing only its own kind. That is what stops the worm band's wave and the drakan band's
wave from stacking when the fight crosses between them.

### The guard that reads like a typo

Both dragons enter their last chain at **below 30** and guard every step of it at **below 45**. It
looks like a mistake and writing all five as `HpBelow(30)` looks like the fix — it matches the entry
guard, and it quietly ends the fight's last chain the moment a healer catches her.

A mutation to exactly that survived the first pass of pins on both bosses. The pin that catches it
has to heal her back into the thirties and watch for another placement, and even then the first
version of it passed under mutation because the pair already standing had not expired yet: watching
for "a spot appears" one second after four spots were placed proves nothing. It waits for them to go
first now.

**This is a general shape worth naming.** A pin that watches for an event has to start from a state
where the event has not already happened. Two of the pins written today failed this way — one
counting markers after they had expired, one counting them before they had.

### Also of note

The drakan's own combat chain is untranslated: four indices against three distinct skills, and its
`on_despawn` effect spawn names an NPC that binds to nothing in our 4.8 client. The worm has no
combat pattern at all — one branch, and it is the call.

**Verification.** Full suite 1,346 passing and 1 skipped; ten new pins; all eleven mutations caught,
two of them only after the pins that should have caught them were fixed. Missing adds 676 → **673
across 445 encounters**.

### NTrap_A, and a cast that was never firing

Deferred one entry ago on the grounds that a literal translation would be inert. It is ported now,
and the reason it was inert turned out to be worse than described.

**A queued cast on an NPC that never fights never fires at all.** The skill queue is drained by the
attack loop, and only while the NPC has a target it hates. Verified rather than reasoned: a summon
spot spawned into the harness was still sitting on an unfired 18222 thirty seconds later. That is not
a trap-only problem — it means the summon spots shipped with Tahabata's rebuild were placing their
slaves correctly and casting nothing, and every one of the fifty-three `NTrap_A` NPCs was on plain
`aggressive`, doing neither.

`NpcSkillCasting.UseOnSelfNow` casts through the skill engine directly, as `UseSkillAndDieAI` already
did for the same reason.

### Two mistakes made while fixing it, both worth recording

**Inferring the immediate path instead of asking for it.** The first version had `CastSkill` choose:
out of combat and self-targeted meant cast now, anything else meant queue. It looked tidier and broke
four unrelated fights — bosses buff themselves from `on_wake_up` too, and switching those from queued
to immediate is a real behaviour change with nothing to do with markers. `Do.SkillOnSelfNow` is now an
explicit choice a table makes.

**Despawning in the same branch as the cast.** The first `NTrapAI` did what the pattern literally
says — `use_skill`, `despawn_self` — and removed the NPC while its own skill was still in flight.
Both are PLANNED actions, so retail queues the despawn *behind* the cast; the despawn belongs in
`OnEndUseSkill`. This is not cosmetic in two ways. A marker gone before its skill resolves is a marker
nobody sees, and its ten-second `live_time` — which every boss placing one supplies — would be
meaningless. And a despawned NPC is dropped from the world map outright, so the collapsed version made
the boss's own decision to place it unobservable: three pins across two dragons went quiet, and one
was rewritten to say so before the real cause was found.

**The self-cast reaches players even though it is aimed at the caster.** These skills are all
`target_type="AREA"` with `target_relation="ENEMY"`, so aiming at itself puts the trap at the centre
and everyone hostile within range takes it. That is how a stationary marker with a self-cast becomes a
patch of fire on the floor.

### What is repointed, and what is deliberately not

Five markers, all placed by a boss on top of the people they are meant to hit: the flame centers of
all four Dark Poeta dragons (281246, 281261, 281270, 281276) and Tahabata's primal dragon (281265).

**The other forty-eight are not**, and the dividing line is `on_see_user`. `NTrap_A` carries two
identical branches — one on waking, one on seeing a player — because a trap *laid in advance* has to
wait for someone to walk into it. Ours are all placed mid-fight, so waking is enough. Repointing a
pre-laid trap (`LycanTrap_18`, `BDF2_Monster_trapA_29_An`, the drakan traps) would make it go off the
instant it spawned, in an empty field, which is strictly worse than today. That needs `on_see_user`
support and is the next piece of this.

Of the fifty-three, 36 have exactly one skill and are safe by the guard; 17 have none and would simply
vanish; 8 already have real handlers (`trap`, `useitem`, `general`, `strange_creature`) and should
keep them.

**One mutation survives on purpose.** Relaxing `CastOnlySkillOnSelf`'s exactly-one-skill check changes
nothing, because no `NTrap_A` NPC in our data has more than one skill. It is a refusal-to-guess
safeguard for future repoints — resolving an index by its position in our npc_skills has been proven
wrong more than once — not live behaviour, and it has no test because the data cannot produce one.

### A pin that was right until this landed, again

`LetsTheOpeningFlamesBurnOutAfterTenSeconds` asserted Vanuka's opening pair was still standing at nine
seconds and gone at ten. True while a flame center was inert furniture; wrong once it became a trap.
The ten seconds are the backstop for a trap whose cast never happens, not the length of the effect.
That is the **fifth** time a pin has had to change because a later port made its subject more
complete, and the second time in two entries.

### Still open

Bosses that buff themselves from `on_wake_up` queue that cast and only fire it when combat starts —
so the buff lands a moment into the fight rather than before it. Four fights are pinned on the queued
behaviour. Whether retail expects it up beforehand is a separate question from this change and was
deliberately not answered here.

**Verification.** Full suite 1,346 passing and 1 skipped, stable across eighteen consecutive runs;
five new pins; four of five mutations caught and the fifth explained above.

### The rest of the traps, and a caution that turned out to be half right

Last entry repointed five `NTrap_A` markers and left the other forty-eight on the grounds that
repointing a **pre-laid** trap would make it go off in an empty field. That reasoning was sound and
the scope was too cautious: of the twenty-three that were on plain `aggressive` with exactly one
skill, **twenty-two are spawned on demand and only one is pre-laid.**

Checked rather than assumed, and one check was wrong on the first pass. Grepping the spawn data for
each id turned up matches for two more — both false. `281166` appeared inside a walker route's forty-
character hash (`…81166…`), and `290116` appeared in a quest work order as an *item* id, the npc and
item namespaces having collided. The single genuine static spawn is **280714**, "strange object"
(`BDF3_LehparZombietrap_45_Ah`), sitting in Beluslan on a 295-second respawn. That one is left alone.

**Every one of the twenty-two corroborates as a trap.** All bind `NTrap_A`, all carry exactly one
skill, and every skill is `target_type="AREA"` with a name that reads as a burst — Explosion, Nerve
Freeze, Strong Contrary Wind, Water Wave, Infernal Rune, Aether Explosion. Two were doing real work
already: Queen Alukina's azure blobble and the strange creature Icaronix leaves on dying. The other
twenty are spawned by nothing yet and are simply ready for when their spawners land.

Names worth not trusting: "aetherback titan core" (`BAb1_NM_CyclopesSlave_51_Al`) reads like a minion
and casts Aether Explosion; "bolstering surkana" casts Wave of Surkana. Same class of misread as the
Kistenian despawn effect calling itself a dredgion elite fighter.

**Region activation is not spawning.** The engine raises `AiEventType.Activate` when a player enters a
region, separately from the spawn event, so a field NPC's `on_wake_up` really does fire at server
start with nobody there. That is what makes 280714 genuinely different, and translating `on_see_user`
is what it needs.

### Queen Alukina's death nova was seven adds and is seven bursts

Her `on_killed_by_user` spawns seven blobbles at her point with `live_time=30`, which the aionemu
class matched exactly. The blobble binds `NTrap_A`, so all seven go off where she fell rather than
standing about for half a minute. The thirty seconds are real and still in the table; they are the
backstop for a trap whose cast never happens.

Sixth pin to change because a later port made its subject more complete.

### A flake that was diagnosed wrongly and has now been diagnosed

`AHandKnocksItsTargetAboutOnItsOwnTimer` was recorded in this document as "found and fixed". It was
not. It failed again during this session's full-suite runs.

The earlier fix stretched the window from forty seconds to a hundred and fifty on the theory that four
ticks became a dozen. **A census over twelve runs shows it bought nothing**: one to two casts at
either length, and a zero in both samples. Stretching it to twelve hundred seconds changed nothing
either — same one to two casts.

The cause is not the window. The pin took its hand out of the boss's wave, and Teselik gives the
self-destruct order on his timer-4 and timer-7 branches within the first minute, so that hand is gone
after one or two flips however long anyone waits. A hand nobody is about to detonate flips every seven
or fifteen seconds for the whole window: **six to nine casts across twelve runs, never zero.** The pin
now spawns one on its own.

**The lesson is about the diagnosis, not the test.** "Make the window longer" is what you reach for
when a probabilistic pin flakes, and it is right only if the number of trials actually scales with the
window. Nobody counted. Counting took one scratch test and showed the trial count was flat.

**Verification.** Full suite 1,347 passing and 1 skipped, five consecutive clean runs, and the
repaired pin clean over six runs of its own. Missing adds unchanged at 673 across 445 encounters —
traps are hazards rather than combatants, so they were never in that count.

### The abyss guards: one mechanic, 460 guards, 86 patterns

The largest single cluster in the missing-adds backlog was never eighty separate encounters. The
`DGuard_*` and `LGuard_*` families are **one mechanic replicated per faction and per level bracket**:
a guard in combat arms a twenty-second heartbeat, and each beat lights a one-second timer that calls
up its bracket's attackers and healer, three metres out, for ten minutes. Leaving the fight sends them
away.

| band | what a three-band guard calls |
|---|---|
| 71-100 | two attackers, on a coin flip |
| 36-70 | two attackers and a healer, on a coin flip |
| below 35 | three attackers and two healers, on a coin flip |

344 of the 460 have a single band instead — below 35, always, one kind of summon.

**The structure is in the class and the facts are in a generated table.** Hand-writing 86 AI classes
for one mechanic would be absurd; hand-copying 692 rows of npc ids into a table would be worse and
wrong within a week. `tools/client-extract/extract_guard_reinforcements.py` reads the mechanic out of
the patterns and `emit_guard_table.py` transcribes it, so the TSV is the reviewable claim and the C#
is a transcription of it.

**The shape census is what made this safe.** Before writing anything, the extractor counted every
spawn branch in the family by (timer slot, lifetime, spawn range, placement): **198 of 205 identical**,
six absolute-placement artifact variants and one outlier. That is the evidence that one class can
serve the family — not an impression from reading two patterns.

**The band gaps are kept.** Retail writes `is_hp_lower_than 35` against `is_hp_in_boundary 36..70`, so
a guard at exactly 35% matches nothing and calls nobody. Tidying that into `0..35` would be a change
dressed as a translation; it is pinned as a dead spot instead. Getting the pin to sit *on* 35 needed a
harness helper — `SetHpPercent` truncates going in and the reader truncates coming out, so asking for
35 reads back 34, which is harmless inside a band and fatal on a boundary.

**Repointed 407 of 460.** The other 53 already carry `simple_abyssguard` (49) or `general` (4), which
add aggro rules this class does not have. Folding the reinforcement branches into those is the
follow-up; overwriting them would have traded one mechanic for another.

**Casts not translated:** thirteen indices across the family and no guard carries thirteen skills.
Timer 2, the two friend-rescue handlers and the `on_message` pair on 10001 are cast-only and go with
them.

### The audit could not see a table, and then saw too much of one

The backlog did not move when this landed. `audit_missing_adds.py` sweeps handler code for spawn
*calls*, and a generated table is data in code shape — the ids sit in tuple literals no call-shaped
regex matches. Ninety-five adds stayed in the backlog after the code to spawn them shipped.

The first fix was worse than the bug. Taking every long integer out of any file declaring itself
generated swept up the table's **dictionary keys — the 460 guards themselves** — so guards that
nothing spawns started counting as live encounters and dragged their own adds in. The total went
*up*, from 673 to 683, while looking like a fix for an undercount. Narrowed to `(npc_id, count)`
pairs it picks up 130 summons and no guards.

**Both guards on that sweep earn their keep**, and the near-miss is the argument for them: a
false positive here silently shrinks the backlog, which is the one failure this audit must not have.

### Where the count actually went

673 across 445 encounters → **676 across 441**. Guard-family rows in the timer bucket fell from 95 to
35 — the remainder being the 53 guards left on their existing handlers, plus the separate `BGuard_`
gate family. The total rose by three because resolving an encounter makes what stands behind it
reachable: the 130 summons are now live NPCs with patterns of their own. That is the fourth time the
backlog has grown by being worked on, and it is worth restating that **the backlog is a frontier, not
an inventory.**

**Verification.** Full suite 1,357 passing and 1 skipped; six new pins; five of seven mutations caught.
The two survivors are both inert by construction and were checked rather than assumed: arming a battle
timer outside combat does nothing (the runtime only fires them in a fight), and no NPC carries this AI
without a table row, because the repoint list *was* the table's key set.

### Finishing the guards, and what the first pass quietly dropped

Two follow-ups were named last entry: the 53 guards left on their own handlers, and the six
absolute-placement variants. Doing the first turned up something worse than either.

**The 49 abyss guards now have both.** `simple_abyssguard` is a faithful port of aionemu's class —
npc-on-npc aggro, movement ignored while fighting, refusing another guard's call for help — and C#
gives one base class. Copying those rules into a second class would have forked Java-parity code, so
`AbyssGuardSimpleAI` moved onto the pattern base with an empty table (`PatternAi` derives from
`AggressiveNpcAI` and every pattern hook returns immediately on a zero-length branch list) and
`AbyssGuardReinforcementAI` fills the table in. Every override in the Java-parity class is untouched.

### The extractor was dropping two whole shapes

The guard rows in the backlog fell from 95 to 35 and stopped. The remainder were not the 53 guards —
they were **variants my extractor never emitted**, for two reasons it should have reported and did
not:

- **`spawn_on_target`.** Retail has more than one spawn op, and I only looked for `<spawn>`. Guards
  using `spawn_on_target` drop their wave **on whoever they are fighting** rather than at their own
  feet. Four pattern variants read as guards that call nobody, and the difference is not cosmetic: a
  wave that lands on the raid is a different fight from one that lands on the guard.
- **Branches with no health guard at all.** `band_of` returned `None` and the row was skipped, so
  `DGuard_PsA`'s unconditional calls read as "never calls" rather than "always calls".

Fixed, the family goes from 90 patterns and 692 rows to **158 patterns and 1,388 rows across 870
guards** — nearly double what the first pass claimed to have covered.

**The lesson is the same one the audit taught two entries ago.** A tool that silently drops what it
does not recognise reports a smaller problem than exists, and the report looks like progress. Both
failures here were invisible: the extractor said "90 patterns" and nothing said 68 were missing. The
`unresolved` counter existed and covered only the case I had thought of.

### Where the count went

676 across 441 encounters → **626 across 395**. Guard-family rows in the timer bucket: **95 → 0.**
The whole `[DL]Guard_` family is resolved — 778 guards on `guard_reinforcement`, 82 on
`abyssguard_reinforcement`.

**Still open:** 8 guards on `general` (killios, aimah, kutos, varzeni and their variants) and 2 on
`siege_shieldnpc`. `GeneralNpcAI` descends from `NpcAI` rather than `AggressiveNpcAI`, so the trick
that worked for the abyss guards — move the base, subclass it — does not apply without deciding what
a `general` guard's aggro rules are for. The six absolute-placement artifact variants are also still
out; they place at fixed fortress coordinates rather than relative to anything, so they need the
single-ownership check that multi-owner absolute coordinates always need.

**Verification.** Full suite 1,359 passing and 1 skipped; five new pins; both placement mutations
caught — the second only after a mirror pin was added, because every self-placement pin stood the
guard two metres from its quarry, where the two placements are indistinguishable.

### The spawn ops are provably all four

Two entries running have turned on a tool quietly failing to recognise something, so this one is
worth settling rather than assuming. Counting every element in the 5.8 dumps whose name contains
"spawn":

| op | uses |
|---|---|
| `spawn` | 16,366 |
| `spawn_on_target` | 896 |
| `spawn_on_multi_target` | 324 |
| `spawn_on_target_by_attacker_indicator` | 306 |

That is 17,892, and `num_to_spawn`, `spawn_range` and `despawn_at_attack_state` each appear exactly
17,892 times. Every spawn action carries those three, so the totals matching is proof the op list is
complete rather than merely long. `audit_missing_adds.py` already knew all four — **the backlog
figure has never been wrong for this reason.** It was `extract_guard_reinforcements.py` that knew
one, and it now imports the audit's pattern instead of keeping its own, so the two cannot drift
again.

The two ops the guard table still cannot express are now **reported rather than flattened**. Eight
branches use `spawn_on_multi_target`, which puts one add on every valid target under a cap; calling
that "on the current target" would put the wave in the wrong place, so those rows are skipped and
counted.

### The fortress gates, extracted and not yet wired

`BGuard_*Gate*` is the next cluster — 48 adds in the backlog, 230 owners — and despite the name it is
**not** the guard mechanic. A gate does not call for help as it weakens; it puts a squad out in waves
on a fixed chain and then removes itself:

```
on_enter_attack   -> arm T0 (ten seconds)
T0                -> arm T1 after 30s, spawn the first wave
T1                -> arm T2 after 5s, spawn the second
T2                -> despawn_self
on_leave_attack   -> despawn the squad, despawn_self
on_message 10009  -> despawn_self
```

No health bands and no coin flips. `tools/client-extract/extract_gate_squads.py` reads it out: **62
patterns, 153 rows, 69 gates**, with chains of one to four waves (50 of them two).

**A structural fact about the dumps, found here and worth knowing generally.** A level variant stores
only what *differs* from its base. `BGuard_DGate_L50` carries the timer branches and nothing else —
no opener, no leave handler, no message handler — while `BGuard_DGate` carries all three. Reading a
variant on its own therefore finds a chain nothing ever starts, which is exactly what the first run
reported: 38 variants, 24 patterns instead of 62. The extractor now falls back to the base pattern
and says when it does. **Any future extractor over this dump needs the same fallback.**

**Still to do, and the reason it is not done here.** The table carries each wave's delay but not the
trailing one — retail's last spawning step arms one more timer whose only job is `despawn_self`, and
that delay sits in a branch with no spawn in it, so the walk stops before reading it. Wiring the AI
without it would mean either inventing the number or leaving gates standing after their squad is
out. The schema needs one more field before the class is written; everything else is ready.

**Also still out:** the `BGuard_CDropGateA` family (twelve variants) has no opener in the base either
— those are siege drop-gates, driven by fortress code rather than by being attacked, so the chain
starts somewhere this extraction cannot see.

**Verification.** Full suite 1,359 passing and 1 skipped, unchanged — this entry is tooling and
extraction only. The guard table regenerates byte-identical after the regex was shared, which is the
check that the sharing changed nothing.

### The fortress gates, wired — and they are the other half of the guards

The schema gap named last entry is closed and the family is ported. What it turned out to be is more
interesting than a second table: **the gates are what the abyss guards summon.** A `W`-family guard
(`LGuard_WhA`, `DGuard_WhA`, Kimeia) calls up a *warp gate*; the gate, once something attacks it, puts
a squad out in waves. Two ports that looked separate are one mechanic with two levels, and the guard
half shipped two entries ago.

```
attacked          -> arm the chain (ten seconds on the common variants)
wave 0            -> squad out, arm wave 1
wave 1            -> squad out, arm the closing link
closing link      -> despawn_self
left alone        -> despawn the squad, and the gate with it
```

62 pattern variants, 153 steps, 69 gates; chains of one to four waves, fifty of them two.

**The trailing delay and the loop.** Retail's last spawning step arms one more timer whose only job is
`despawn_self`, and that delay lives in a branch with no spawn in it — the chain walk stopped before
reading it, which is why this was deferred. Reading it turned up a second shape the first pass would
have got wrong: **the fortress-chief gates have no closing link at all.** Their last wave arms the
*first* one again, so they cycle for as long as the fight lasts. A missing despawn delay and a loop
look identical in one column, and treating the chiefs as "no despawn step found" would have left them
standing idle after three waves instead of producing squads indefinitely. The table now carries both
`despawn_after_ms` and `loops_to`, and a mutation that sent looping gates to the closing branch
survived until a pin existed for them.

**The AI name they carried was a display-name misread.** All 68 were on `groupgate` — aionemu's
handler for the portal a *player* summons with Group Gate — because their display name is "warp gate"
or "illusion gate". Checked before repointing: no player skill template and no spawn helper references
any of the 69, and `GroupGateAI`'s dialog path gates on a player creator these have never had. It was
inert. The one on `illusion_gate` is left alone; it has a real handler of its own.

**Not translated:** `on_message` 10009, which dismisses a gate. It belongs to the fortress siege code
rather than to any NPC, so translating it would add a listener with no speaker.

### Where the count went

626 across 395 encounters → **586 across 390**. `BGuard_*` rows in the timer bucket: 48 → 6, the
remainder being the `CDropGateA` siege drop-gates.

**Still open on this family:** the twelve `BGuard_CDropGateA*` variants have no opener in the base
pattern either — they are siege drop-gates started by fortress code rather than by being attacked, so
the chain begins somewhere this extraction cannot see. They need whatever spawns them ported first.

**Verification.** Full suite 1,365 passing and 1 skipped; six new pins; five of six mutations caught.
The survivor is the familiar inert one — moving the opener to `on_wake_up` changes nothing because
battle timers only fire in combat.

### The Runatorium's Vritra callers

The largest cluster left after the guards and gates, and a small one — the tail is now individual
encounters rather than families. Eight invisible controllers stand in Infinity Shard (300800000),
they are in our spawn data, and their templates pointed at `general`, so they did nothing at all.
Each wakes, puts a Vritra trooper on the floor and removes itself two seconds later.

**The cascade is a weighted pick, not ten rolls.** Four of the eight carry ten branches at equal
priority, each with its own `test_probability`, and one unguarded branch beneath them. Retail
evaluates in priority order and stops at the first branch that passes, so **exactly one** trooper
appears every time and the unguarded branch is what guarantees it. Read as ten independent rolls it
would put anywhere from nought to ten troopers on the floor. The other four are a single unguarded
branch spawning three at once, one of them five metres from the other two.

**Two pins, because one is not enough.** "Exactly one trooper" is satisfied just as well by a table
read in the wrong order — fallback first, where retail puts it last — which produces exactly one, and
the *same* one, every time. A mutation doing precisely that survived until a second pin counted
distinct troopers across twenty runs.

**Not translated: the walk.** Retail hands each trooper a `pathname` so it walks a fixed route from
the drop point. We have no server-path following, so troopers arrive on retail's mark and then behave
as their own template says. That is the audit's own "blocked: waypoint-placed" category and it is the
only part of this mechanic left out.

### The audit's table sweep was too narrow, for the third time

The backlog did not move when this landed. The generated-table sweep matches `(npc_id, count)` tuples
and required the tuple to **close** after the count — the Vritra placements carry coordinates as well,
so twenty ids went unseen. Widened to "an id at the head of a tuple whose next element is a small
integer", which picks up both tables and still excludes dictionary keys.

That is the third time this audit's shape assumptions have cost a measurement: it could not see a
generated table at all, then it saw the guards' keys as well as their summons, and now it could not
see a tuple with a third element. **Each was found by the backlog failing to move after work that
should have moved it** — which is the only reason to re-measure after every change rather than
trusting the number.

**Where the count went.** 586 across 390 encounters → **566 across 387**.

**Verification.** Full suite 1,370 passing and 1 skipped; five new pins; four of six mutations caught.
The two survivors are inert by data rather than untested: every `num_to_spawn` in this family is 1, so
ignoring the count changes nothing, and the reversal mutation was a no-op because `AiPattern.Of`
re-sorts by priority regardless of list order — redone as "evaluate the fallback first", it was
caught.

### "Blocked: waypoint-placed" is now proven, not assumed

Forty-six adds have sat in that bucket since the audit was written, on the reasoning that a spawn at
`SPAWN_LOCATION_WAY_POINT_START` needs the named path's first point and we do not have it. Worth
settling rather than repeating, because 46 is the third-largest bucket left.

The patterns name paths in plain text — `GuardianChief_SouthCastle_Path_1`,
`NPCPathVriAss_Path01`. Our own `npc_walker.xml` holds 5,874 routes, but keyed by a 40-hex id with no
name attached anywhere in the file. Two ways that could have been bridged, both checked:

- **The route id is not a hash of the path name.** Tried SHA-1, MD5 and SHA-256 over UTF-8 and
  UTF-16LE, in original, lower and upper case, against the full route-id set. No hit.
- **The client does not carry the paths.** Indexed all 3,332 archives — 525,657 entries — and the
  only path-named files are flight paths (`fly_path.xml`, `client_airline.xml`), an environment prop
  and a dozen sound effects. There is no NPC route data in the client at all.

So NPC walk routes are **server-side data we do not have**, the same class of blocker as the
`SKILLI_INDEX` skill lists. The bucket is genuinely blocked and the only routes forward are a dump of
NCSoft's own path data, or hand-placing each add from observation — which is a per-encounter judgement
call, not a mechanical one.

**What this does not block.** A pattern that places absolutely and merely *hands* the spawn a path to
walk afterwards is fine: the mark is in the pattern, only the walking is lost. The Vritra callers are
exactly that shape and shipped. The 46 are the ones where the path start **is** the placement.

### Shadowshift, and the first `spawn_on_multi_target` boss

The Catacombs boss (216247 and 281546, both on plain `aggressive`), and the first encounter ported
where the adds are **per player rather than per boss**. `spawn_on_multi_target` puts one spectre on
*every* valid target within a hundred metres, each attacking whoever it landed on, and nothing caps
the count — so a larger group gets proportionally more. That is the mechanic, not an oversight, and it
is why this fight scales with the group where a fixed wave at the boss's feet does not.

Two spectre timers run at once: a Sum1 three metres out, first at ten seconds then every twenty-five;
a Sum2 ten metres out, first at seven seconds then **every four**. Neither spectre was spawned by
anything.

**Casts not translated:** four indices against fewer skills than that. Going with them is the whole
`on_attacked`/`on_spelled` surface — a four-rung health ladder of self-casts plus a near/far pair —
and the timer-2 branch, all of it casting. Also out: `control_door` on leaving the fight, since our
door handling lives in the instance and the pattern does not say which door.

### Two pins that were measuring the harness rather than the boss

**Scatter wider than the spacing.** The pin for "each spectre lands on its own player" assigns each
spectre to its nearest player. With the players four metres apart and retail scattering each spectre
ten metres around its target, a spectre routinely lands nearer somebody else — two players duly
claimed three spectres. They stand twenty-five apart now, comfortably outside the scatter.

**A limit worth recording.** Watching the four-second repeat past about thirteen seconds fails, and
not because of this boss: the near spectre's own `servant` AI starts casting into the harness's
stand-in player and `Effect.ApplyEffect` throws a `NullReferenceException`. The stand-in is
deliberately minimal — invulnerable, with no real effect graph — so this is a harness limit rather
than a server bug, but it **bounds what any pin here can watch**. The repeat is measured at seven and
eleven seconds, short of it. Anything in this fight that only becomes visible after the spectres start
casting cannot currently be pinned.

**Where the count went.** 566 across 387 encounters → **562 across 385**.

**Verification.** Full suite 1,377 passing and 1 skipped; seven new pins; all six mutations caught.

### Tiamat's breath rotation: the blocked observation is now made

An earlier pass left the weakened dragon (219362, `IDTiamat_Tiamat_Dragon_Dying_Named_60_Al`) with a
note that it needed "one observation tying a beacon number to a breath direction". That observation
exists now, and it is over-determined — **four independent signals agree**:

| signal | left | middle | right |
|---|---|---|---|
| branch comment | `BreathL_100-76` | `BreathM1_100-76` | `BreathR_100-76` |
| beacon spawned | `Breath_Beacon1`, dir 17 | `Breath_Beacon2`, no dir | `Breath_Beacon3`, dir 105 |
| skill index | `SKILLI_INDEX_1` | `SKILLI_INDEX_2` | `SKILLI_INDEX_3` |
| skill stack name | `IDTIAMAT_TIAMAT_BREATH**L**_CAST` (20922) | `…BREATH**M**…` (20924) | `…BREATH**R**…` (20926) |

Those three stack names are **unique in the whole skill table** — one skill each, no other candidates.
And the existing aionemu class corroborates the geometry independently: its 20922 hazards land at
(445, 550) to one side, 20924's along the y=514.6 centre line, 20926's at (458, 480) to the other.

Note what carries the resolution: the ids come from the **skill templates' own stack names**, not from
counting `npc_skills`. 219362 has no `npc_skills` entry at all, so a positional reading was never
available — which is exactly why this sat blocked. The naming is the evidence, as it was for the Dark
Poeta markers.

### What that unblocks, and what is still wrong

**The breath order is random in our port and a fixed rotation in retail.** `TiamatWeakenedDragonAI`
picks with `20922 + Rnd.NextInt(3) * 2`. Retail runs a scripted sequence per health band:

| band | sequence |
|---|---|
| 100-76 | M, M, L, R — 18s a step |
| 75-51 | L, M, R, then three Thorn steps 5s apart, then R, M, L — 18s on the breaths, 15s into the thorns |
| 50-26 | L, M, R … 12s a step, and the beacons move to `Tiamat_BeaconL8s`-style marks at different coordinates |
| 25-0 | (not yet transcribed) |

A learnable rotation replaced by a coin flip is a real difference in how the fight plays: in retail a
raid can pre-position for the next breath, and here it cannot.

**The telegraph is missing entirely.** Retail spawns a beacon seven seconds ahead of each breath, at
(458.5, 514.7, 417.4) with the heading that picks the cone. Those are the twelve adds this encounter
contributes to the backlog. Without them the breath arrives unannounced.

**Also noted:** retail's own band guards are inconsistent — `BreathL_75-51` tests `51..74` where the
M and R steps beside it test `51..75`. That is retail's, and belongs in the port as-is.

### Why this is written up rather than ported

Rebuilding the dragon as a pattern table is a Tahabata-sized job — four bands, each a chain of four to
nine timer steps, with beacon coordinates and headings per step and a separate thorn sub-chain. Every
fact needed is now established, but starting it with the room left in this pass would mean shipping a
band or two and a table that reads as complete. **The next pass should transcribe all four bands
before writing any of it.** The pieces are: the sequence table above, the beacon↔direction↔skill
mapping proven here, and the existing class's hazard spawns, which are already faithful and should be
kept as the per-breath effect.

### Tiamat's rotation, transcribed

Last entry said the next pass should transcribe all four bands before writing any of the table. Done,
and as data rather than by eye: `tools/client-extract/extract_tiamat_rotation.py` emits
`out/tiamat_rotation.tsv` — **249 rows, 45 steps, 13 distinct NPCs, nothing unresolved.**

| band | steps | what happens |
|---|---|---|
| 76-100 | 4 | M, M, L, R — 18s a step, beacons all from (458.5, 514.7, 417.4) at dir 0/0/17/105 |
| 51-75 | 9 | L, M, R, then Thorn L/M/R five seconds apart, then R, M, L |
| 26-50 | 14 | L, M, R at twelve seconds with the `Beacon*8s` marks, Thorn L/M/R and a Cyclops crack every two, then the same in reverse |
| 0-25 | 17 | the `Beacon*4s` marks at eight seconds, and Gravity Bombs and a Quake join the thorns and cracks |
| any | 1 | `Repeat_0`, the unbanded three-second heartbeat every banded chain hangs off |

Reading that by hand was not an option: one wrong delay or one wrong beacon heading is not something
review catches, and there are several hundred coordinates.

**Every NPC it places resolves**, and most already have handlers of their own — `ultimate_atrocity`,
`calculated_atrocity`, `divisive_creation`, `gravity_crusher`, `tiamat_skill_helper`. The hazards are
built; what is missing is the boss placing them in retail's order instead of rolling a die.

**The lower bands use different breath skills.** The top two bands address indices 1/2/3, which the
last entry resolved to 20922/20924/20926 by their stack names. 26-50 addresses 7/9/11 and 0-25
addresses 6/8/10 — and the beacon names say why: `Beacon*8s` and `Beacon*4s`, an eight-second and a
four-second telegraph. Those are faster-cast breath variants and their ids are **not yet resolved**;
only three skills in the table carry the `IDTIAMAT_TIAMAT_BREATH{L,M,R}_CAST` names.

**What remains, precisely.** Write the table from the TSV — structure, beacons, thorns, cracks, bombs
and quake are all index-free and fully evidenced. Keep the existing class's per-breath hazard spawns.
Translate the top two bands' casts using the resolved 20922/24/26; leave 26-50 and 0-25 casting
nothing until their variants resolve, and say so in the class. The one thing not to do is carry the
`Rnd.NextInt(3)` forward into any band that now has a sequence.

### Tiamat's rotation is now a C# table, and the thorn beneath it is ported

`emit_tiamat_table.py` turns the transcription into `TiamatRotation.cs` — **45 steps, 13 hazards,
249 placements**, flat and in document order because retail's priorities descend down the file and
this pattern has a place where the ordering between bands matters (`BreathL_75-51` guards 51..74
where the M and R steps beside it guard 51..75, so at exactly 75 the L step fails and evaluation falls
through).

### What aionemu's "sinking sand" actually was

Chasing whether the rotation subsumes the old class's schedulers turned up the answer, and it is not
the one that was assumed. `TiamatWeakenedDragonAI.ScheduleSinkingSand` puts hazard 283135 out
**itself**, every two minutes, in a hand-computed arc from -25° to +25° at seven distances.

Retail never has the boss place that hazard at all. The boss places **thorns** at fixed marks, and
each thorn throws its own sand — 283135 appears in exactly one retail pattern, and it is
`IDTiamat_BurrowingWorm_BurrowFX`, the thorn the rotation spawns thirteen at a time. The arc is
aionemu inventing a shape for a mechanic whose real shape is the thorn coordinates in the table.

**The thorn is ported** (`TiamatBurrowingThornAI`, 283057, was on `aggressive` and spawned by
nothing): it appears, waits two seconds, then throws five bursts of sand — three, four, three, four,
four — at widening intervals before removing itself. Retail's one-shot flags are what make it a
sequence rather than a loop.

It is **inert until the boss's rotation is wired**, and that is the honest state to leave it in: the
piece is built and the thing that calls it is not.

### A pin that asserted nothing

A mutation widening the sand's scatter from three metres to forty survived. The pin looked right — it
loops over every grain and checks the distance — but it advanced three seconds first, past the
one-second life of the burst, so **the loop ran over an empty collection**. `foreach` over nothing
passes whatever you assert inside it.

The fix is a length assertion before the loop. The general form is worth keeping in mind alongside
the two earlier timing traps: a pin that measures after a short-lived thing expires does not fail, it
silently stops testing.

### What remains on this boss

Wiring `TiamatRotation` into the dragon, which needs two decisions this pass did not have room to make
carefully:

- **The hard-mode subclass couples to the base.** `HM_TiamatWeakenedDragonAI` extends
  `TiamatWeakenedDragonAI` and overrides `HandleHpPhase` and `CalculateAtrocitySkillId`, so rebuilding
  the base as a pattern class breaks it. Hard mode has its own retail pattern
  (`IDTiamat_Hard_Tiamat_Dragon_Dying`, eight adds in the backlog) which needs the same transcription.
- **The lower two bands' breath skills are still unresolved** — indices 6/8/10 and 7/9/11 against only
  three skills carrying `BREATH{L,M,R}_CAST` names. The structure and every spawn are index-free and
  can land regardless; those bands would simply place their beacons and cast nothing.

**Verification.** Full suite 1,380 passing and 1 skipped; three new pins; all six mutations caught
after the vacuous one was repaired. Missing adds 562 → **561**.

### Tiamat's rotation is wired

Both blockers named last entry turned out to be avoidable. The hard-mode coupling is not a coupling at
all: normal and hard are **different NPCs** (219362 and 236277), so a new pattern class takes 219362
and `TiamatWeakenedDragonAI` stays exactly as it is, still serving as hard mode's base. Nothing had to
be rewritten to be replaced.

`TiamatDyingRotationAI` builds its branches from `TiamatRotation` — 45 steps, priorities descending in
the table's own order, which is retail's evaluation order. **The fixed sequence is the point**: the
class it replaces for this NPC chose with `Rnd.NextInt(3)`, and the healthiest band's
middle-middle-left-right would come up about one run in eighty by chance.

**Half the casts are translated, and the half that is not casts nothing.** Indices 1/2/3 resolve to
20922/20924/20926. The lower two bands address 6/8/10 and 7/9/11 — faster-cast variants, as their
`Beacon*8s` and `Beacon*4s` marks imply — and those ids are unresolved, so those bands place their
beacons and hazards faithfully and cast nothing. Inventing a skill id would be a guess in the one
place this work does not guess.

### Three pins that measured the wrong thing, and what they have in common

Six mutations, three survivors on the first pass, all three my pins rather than the port:

- **Dying with a clear field.** `DyingClearsWhatSheHasPlaced` ran the clock for forty seconds and then
  killed her — by which time beacons (seven seconds) and thorns (which retire themselves) had gone on
  their own. Deleting the despawn changed nothing. It now kills her *while a beacon is standing*, and
  asserts the field is non-empty first.
- **Headings read after rotation.** The beacons are aggressive NPCs and turn toward whoever is near, so
  holding a reference and reading `GetHeading()` at the end measures where the beacon swung to, not
  where it was placed — a beacon placed on 0 read 119. Flattening every heading to zero passed. The
  heading is now read the tick the beacon is found.
- (and, last entry, a `foreach` over an expired collection.)

All three are the same mistake in different clothes: **measuring a transient at a moment when it is no
longer what it was.** Short-lived spawns expire, aggressive NPCs rotate, and a pin that looks later
than the thing it describes does not fail — it quietly stops testing. Worth checking on every pin that
observes something with a lifetime.

### The backlog number did not move, and that is the number's fault

561 before and after. The thirteen NPCs this rotation places were already counted as spawnable when
the **generated table** was committed last entry — `audit_missing_adds.py` sweeps handler code for
ids, and a table full of them satisfies it whether or not anything executes it.

So last entry's commit moved the count by shipping data nothing ran, and this entry's did not move it
by making that data run. **The metric answers "does any code reference this id", not "does the
mechanic happen".** It is still the right measure for finding unported encounters — that is what it
was built for — but it cannot see the difference between a wired mechanic and an inert table, and this
is the first time that gap has been visible. Do not read a flat number as a flat week.

**Verification.** Full suite 1,387 passing and 1 skipped; seven new pins; all six mutations caught
after the three pins above were repaired.

**Still open on this boss:** hard mode (`IDTiamat_Hard_Tiamat_Dragon_Dying`, eight adds) needs the same
transcription — the extractor takes a pattern name, so it is one run and one table away. And the lower
bands' breath ids remain unresolved.

### Hard-mode Tiamat, and two names that lied

Hard mode was "one run and one table away", and it was — the extractor now takes a `--pattern`, and
`IDTiamat_Hard_Tiamat_Dragon_Dying` transcribes to **the same 45 steps, the same delays, the same
coordinates and headings** as normal mode, with an entirely different cast of thirteen hazards
(856xxx against 283xxx). One class, two tables, keyed by boss npc id.

**Two devnames pointed the wrong way, and both would have shipped silently.**

- `BIDTiamat_Breath_Beacon1_Hard` binds a pattern called `IDTiamat_Hard_Breath_**Centarl**_00`, and
  Beacon2's says `_Right_`, Beacon3's `_Left_`. Read as labels those say the hard beacons are shuffled
  relative to normal mode's Beacon1=Left. They are not: the boss spawns Beacon1 on its `BreathL` step
  with **dir 17** in both modes, Beacon2 on `BreathM` with no heading, Beacon3 on `BreathR` with 105.
  The boss that places a beacon is the authority on what that beacon is; the beacon's own pattern name
  is a label, and this one is even misspelled.
- `BIDTiamat_BurrowingWorm_BurrowFX_Hard` reads as "the same thorn with a suffix" and binds
  `IDTiamat_Hard_Earthquake_00` — the same structure throwing a **different** uplift. Pointing 856040
  at the normal thorn class, which the name invites, would have thrown normal-mode sand in the hard
  fight and nothing would have looked wrong. The thorn's uplift is now a table keyed by thorn.

That is twice in one encounter that a name disagreed with the structure. **The structure wins, every
time** — the same lesson the Kistenian "dredgion elite fighter" and the "aetherback titan core"
taught, and worth stating as a rule rather than an anecdote.

### Hard mode's sand is inert, and registering its AI breaks the bootstrap

Two things found while pinning it, neither about Tiamat:

- **856041 has no `npc_skills` entry and sits on `useSkillAndDie`**, which deletes an NPC with an empty
  skill list the instant it spawns. Hard mode's ground hazard therefore does nothing on our server
  today, whatever places it. Normal mode's equivalent (283135) has no skills either but sits on
  `tiamat_skill_helper`, which does not delete itself — so the same data gap is visible in one mode and
  not the other.
- **Registering `UseSkillAndDieAI` in a harness makes `GameServerBootstrapTests` fail.**
  `SiegeService`'s static initializer throws a `NullReferenceException` once that AI has been
  registered under a test DataManager, and the bootstrap tests run later and inherit the poisoned
  static. Two tests, reproducible, and nothing to do with the AI under test. Recorded rather than
  worked around silently: it is a real coupling between the harness and a static service singleton,
  and it will bite the next person who registers that AI.

### Three mutations, three pins that could not see them

All three survived the first pass, for the third entry running, and the cause was the same each time:

- the hard-thorn pin sampled at twelve and sixteen seconds, which fall **between** bursts that live one
  second, so throwing normal-mode sand was invisible — it samples every second now;
- nothing spawned the hard boss at all, so pointing both modes at normal mode's table changed nothing;
- and the unknown-thorn fallback is unreachable from data, as the equivalent guards have been.

**Verification.** Full suite 1,389 passing and 1 skipped; two new pins; both reachable mutations
caught.

**Still open:** the lower two bands' breath skill ids (6/8/10 and 7/9/11) remain unresolved in both
modes, so those bands place beacons and hazards and cast nothing.

### The last open piece on Tiamat: all nine breaths resolve

Two entries ago the lower bands' breath ids were "not resolved" and those bands cast nothing. They
resolve, and the evidence is the strongest this work has assembled — **four independent orderings all
agreeing**:

| | left | middle | right |
|---|---|---|---|
| 12s (bands 76-100, 51-75; indices 1/2/3) | 20922 | 20924 | 20926 |
| 8s (band 26-50; indices 7/9/11) | 21151 | 21155 | 21159 |
| 4s (band 0-25; indices 6/8/10) | 21149 | 21153 | 21157 |

1. Retail ships exactly three breath cast times and the skill table names them
   `BREATH{L,M,R}_CAST`, `…8S_CAST` and `…4S_CAST`.
2. Each one's `duration` is 12000, 8000 and 4000 to match its name.
3. The bands addressing 6-11 place the `Beacon*8s` and `Beacon*4s` marks — the same claim from the
   telegraph's side rather than the skill's.
4. The index numbering closes it: 6/8/10 and 7/9/11 are interleaved L/M/R pairs, and so are the skill
   ids — even index for four seconds, odd for eight, in the same order.

The pin that matters is the **pairing**, not the id: a band that places a `Beacon*8s` while casting a
twelve-second breath telegraphs one thing and does another, which is exactly what a wrong index
mapping produces. All three mutations — swapping the 4s and 8s sets, swapping left and right, and
dropping the lower bands back to casting nothing — are caught.

**Hard mode shares the casts, and that rests on absence rather than a name.** The skill table has
hard-specific *damage* halves for every breath (`IDTIAMAT_HARD_TIAMAT_BREATH*_DMG`) and **no hard cast
half at all**, so there is nothing else it could be casting. That is weaker than normal mode's name
match — it assumes the absence is deliberate rather than a gap in the data — and it is flagged in the
class rather than buried.

### A sampling limit worth knowing for any cast pin

The first version of the eight-second pin asked for both the left and the middle breath and saw only
the left. The queue is drained by the **attack loop** as well as by the test, so sampling once a
second sees some casts and misses others. Any pin that counts casts is sampling, not observing; assert
that the right family appears and the wrong one does not, rather than enumerating.

**Verification.** Full suite 1,391 passing and 1 skipped; two new pins; all three mutations caught.

**Tiamat's dying phase is now complete** — both modes, all four bands, every beacon, hazard and
breath. What remains on this encounter is nothing this data can answer.

### Unstable Yamennes: the deferral was right and its reason was not

Nine adds sat in the backlog under `IDAbRe_Core_NamedD_02` and its hard twin, with a note in
`UnstableYamennesAI` saying the gate ids were "deliberately left alone" because retail names
283203/283222/283223 upstairs and 283233 downstairs while our class spawns 219567/219579/219580, and
only ours carry the AI that makes a gate do anything. Swapping, it said, would replace working portals
with inert scenery.

The conclusion holds. The reason does not go deep enough, and the difference matters for whoever picks
this up.

**The two families are the same gates twice.** `idabre_core_02_Sum_Teleport2` (219567) and
`bidabre_core_02_Sum_Teleport2` (283203) differ by a `b` prefix and **bind the same retail pattern**,
`IDAbRe_Core_Summon4_02`. Both bosses — normal and hard — spawn the b-prefixed set exclusively. So the
219xxx family our class uses is not what either boss places.

**But swapping ids would not make it faithful, because our gate AI is not a translation of that
pattern at all.** Retail's `IDAbRe_Core_Summon4_02` does three things:

| | retail | `UnstableYamenessPortalSummonedAI` |
|---|---|---|
| on waking | one `IDAbRe_Core_Sum_Teleport2_Enemy` on itself, seventy seconds, attacking with 100,000 hate | nothing |
| in combat | a cannon NPC (283200) at a fixed point every twelve seconds | — |
| twelve seconds in | — | 219565 and 219566 at ±3 metres, repeated once at seventy-two seconds |

Neither the timing, the count, the placement nor the npcs match. Ours is an invention that happens to
produce adds near a portal.

**And a faithful gate cannot currently be finished.** `IDAbRe_Core_Sum_Teleport2_Enemy` — the thing
retail's gate summons the moment it appears — **resolves to no npc in our 4.8 client**. The binding
table has no devname for it, so the on-wake summon is unportable the same way the waypoint bucket is.

**What the work actually is**, for whoever takes it: translate `IDAbRe_Core_Summon4_02` as a gate class
(cannon at a fixed point on a twelve-second timer, group cleared on death and despawn), repoint
283203/283222/283223/283233 to it, change the boss to spawn those ids, and leave the on-wake summon
out with a note. That is a rewrite of a working encounter for fidelity, not a bug fix, and it should be
done deliberately rather than folded into a sweep.

**Recorded rather than done** because the encounter works today and the change is a substitution of one
whole mechanic for another — exactly the kind that wants its own pass with pins written before the
swap, not after.

### The noble lapilima splits, and the audit could not see a helper

`IDAbRe_Core_FlyingWorm` is a complete translation with nothing left out: ten seconds after something
engages it, the worm splits off three flash lapilimo at its own feet, and does it again every fifteen
seconds for as long as the fight lasts. Nothing caps them — a fight that drags becomes a swarm, which
is why the worm is meant to be killed rather than tanked. All five owners were on plain `aggressive`
and the three splinters were spawned by nothing.

Worth noting the three summons are **distinct npc ids sharing one display name**, so they read as one
add in the client and three in the data. And retail never despawns them: no `on_die`, no
`on_leave_attack_state`, no despawn anywhere in the pattern — they carry `despawn_at_attack_state` and
the engine retires them when the fight ends.

**The audit could not see them, and the blind spot cost twenty adds.** The class writes

```csharp
private static PatternAction Splinter(int npcId) => Do.SpawnNear(npcId, Split, count: 1, ...);
... Splinter(FlashLapilimo53), Splinter(FlashLapilimo54), ...
```

so the constant never sits next to a spawn call and `spawned_via_constants` — which resolves names
*passed to a spawn* — found nothing. `audit_missing_adds.py` now treats a method whose body spawns one
of its own parameters as a spawn call by name. That follows this indirection without following
arbitrary ones: the body has to contain the spawn, so a `Burn(int skillId) => Do.SkillOnSelf(skillId)`
helper still does not qualify, which is the false positive that matters.

**This one ran the other way from the earlier three.** The generated-table and tuple-shape gaps made
the audit miss *code*, so the backlog looked bigger than it was; so did this one — fixing it dropped
the count from 561 to **541 across 384**, of which only three are this worm. **Seventeen adds were
already being spawned and listed as missing.** Every previous entry's figure was overstated by that
much.

That is the fourth shape assumption this audit has been wrong about, and the pattern is now clear
enough to state: **it can only see spawns written the way its author had seen them written.** Each new
idiom — a generated table, a three-element tuple, a helper taking the id as a parameter — is invisible
until something makes it obvious. The check that catches them is not review; it is re-measuring after
work that should have moved the number, and asking why when it does not.

**Verification.** Full suite 1,395 passing and 1 skipped; four new pins; five of six mutations caught,
the sixth being the familiar inert one (a battle timer armed outside combat never fires).

### Auditing the audit, and the one thing it found

Four shape assumptions in a row made this worth checking deliberately rather than waiting for the next
surprise. The check: for every AI class that spawns anything, take its `const int` values that are
real npc ids, and ask which the audit cannot see. If a class places an add the sweep misses, the
constant is there and the id is not in `spawnable_npc_ids`.

**One class, two constants** — `MacunbelloAI`'s `HardModeNpcId` and `SoulReaperHard`. The first is a
true negative: it is a comparison (`GetNpcId() == HardModeNpcId`), not a spawn, and the audit is right
to ignore it. The second was a genuine gap the class already knew about — the constant was declared,
never used, and carried a note saying hard mode's variant "is not implemented".

That is a good result for the audit. After the helper fix it can see every add every ported class
places, and the one thing it flagged was real.

### Macunbello's hard-mode reaper

Retail's `IDCTH_Boss_LichKing` puts it on `on_attacked` behind `test_probability 5` and a one-shot
flag: a five-percent roll on every hit, and at most one per fight. What settled the port is that it is
**additional** to the timed wave rather than a substitute — the waves spawn the normal reaper (281698)
in both modes, and this hard-only variant (281775) arrives on top of them.

The two ids differ by a single `H` in the devname — `BIDCT_SumLich` against `BIDCTH_SumLich` — which is
the same trap that would have put normal-mode sand in hard-mode Tiamat, and the third time this
encounter family has set it.

**The flag is only spent on a successful roll.** Retail sets it *inside* the branch the probability
guards, so a failed roll leaves the chance open for the next hit. Latching first and rolling second
would give one attempt per fight instead of one success.

### Two pins for one behaviour, because the latch hides the roll

Hitting a boss two hundred times and asserting exactly one reaper pins the latch — and passes just as
well if the roll is a certainty, since the latch caps the count either way. A mutation making it always
fire survived on that pin alone. Twenty separate fights hit once each is what separates them: at five
percent about one summons, at a hundred all twenty do.

The general form: **a cap and a probability cannot be pinned by the same observation.** One bounds the
count, the other bounds the rate, and a test that only counts sees the cap.

**Verification.** Full suite 1,398 passing and 1 skipped, three consecutive clean runs of the affected
class; three new pins; all four mutations caught after the probability pin was added. Backlog
unchanged at 541 — this add was already counted, because the constant was in the file.

### Kalindi's shadow flame

`IDTiamat_Kalrindy` — the Dragon Lord's Refuge Kalindi (219359), not Dark Poeta's Calindi (215281),
which is a different encounter one letter away. Her four surkana rungs at 80/60/40/25 were already
ported and correct; what each of them also carries is a `spawn_on_multi_target` that had no
counterpart here at all.

**One shadow flame on every player, and more each rung** — one within five metres at 80%, two within
seven at 60%, three within nine at 40%, four within ten at 25%, each lasting fifteen seconds and
reaching a hundred metres, which is the whole room. 283132 was spawned by nothing anywhere; the class
placed 283133 on a skill hook instead, which is a different npc that lands once near the boss rather
than once per player. The escalation and the per-player placement are both the mechanic: the room
fills faster the longer the fight runs.

### A test that asked the ladder the wrong question

The first version set the boss to each threshold in turn and asserted that rung's count. It read three
flames a player where the code produced six, and the code was right: `HpPhases` fires **every** rung it
has crossed, so dropping a boss straight to 39% runs the 80, 60 and 40 steps together. The pin walks
the boss down one rung at a time now and lets the flames burn out between measurements.

That is the same family as the earlier timing traps, one level up: not *when* to measure but *how much
has already happened* by the time you do. A threshold ladder is stateful, and a test that jumps to a
threshold skips the states rather than reaching them.

**One existing pin needed a registration**, not a change: making Calindi spawn 283132 broke
`RetailHpThresholdTests` because that npc's template AI is `noaction` and the harness had never needed
it. Worth noting as the standing cost of adding a spawn — every harness that runs the spawner needs the
spawned NPC's AI registered, and the failure is a hard `ArgumentException` rather than a silent miss.

**Still open on this boss:** the second gap, `IDTiamat_BurrowingWorm_BurrowDispel` (283059), which
retail drops on a **random attacker other than the current target** every twenty-two seconds between
16% and 70%. It needs a timer this class does not have — it is an HP-phase handler with skill hooks,
not a timer table — so it is a restructuring rather than an addition, and it is left for a pass that
can do it deliberately.

**Verification.** Full suite 1,402 passing and 1 skipped; four new pins; all four mutations caught.
Backlog 541 → **539 across 384**.

### Kalindi's dispel worm, and why it was not a restructuring after all

Last entry left this as "needs a timer this class does not have — a restructuring rather than an
addition". That was an over-estimate: the class is an `AggressiveNpcAI` and this codebase's
hand-written bosses schedule tasks routinely. It is an addition.

Retail's timer 2 carries two branches, and translating both is what makes it right:

| | |
|---|---|
| inside 16-70% | plant a `BurrowDispel` on a **random attacker other than the current target**, ten seconds, then wait twenty-two |
| outside it | wait three seconds and look again |

So the implementation is a **self-rescheduling** timer rather than a fixed-rate one. A twenty-two
second loop would miss the moment she enters the band; a three-second loop would need a clock to know
when the next worm is due, and reading wall time would make the pins depend on real elapsed time
rather than the harness's own. Retail's own shape avoids both.

`ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET` is the mechanic, not a detail: a dispel on the tank lands
on somebody expecting it. 283059 was spawned by nothing anywhere.

### The transient trap, for the fourth time

`AboveTheBandSheePlantsNone` beat the fight for forty seconds and counted worms at the end. A worm
lives ten seconds and the interval is twenty-two, so at almost any chosen moment the field is empty
whether the band is honoured or not — a mutation that ignored the band entirely passed, because both
worms it planted had already burrowed away before the assertion looked.

That is now the fourth distinct encounter where a pin measured a short-lived thing after it expired,
after the sand scatter, the hard thorn's bursts and Tiamat's beacons. The rule has earned a place
next to the others: **if what you are asserting about has a lifetime shorter than your window, count
across the window rather than at the end of it.** The failure mode is silent — the pin passes, and it
passes for the mutation too.

**Verification.** Full suite 1,406 passing and 1 skipped; four new pins; all four mutations caught
after the cumulative count replaced the end-of-window one. Backlog 539 → **538 across 383**.

**Kalindi is complete** — both her surkana ladder and both `spawn_on_multi_target` mechanics.

### Removing the footgun instead of catching it a fifth time

Four separate mutations have now survived because a pin measured a short-lived thing after it expired:
the sand scatter, the hard thorn's bursts, Tiamat's beacons and Kalindi's dispel worm. Each was fixed
where it was found. Four is enough to stop treating it as a series of mistakes and treat it as a
missing tool.

`BossAiHarness.Watch(seconds, perSecond, params npcIds)` advances a second at a time and returns both
numbers a pin actually wants:

- **`Peak`** — the most alive at any one moment, which is the size of a wave;
- **`Total`** — how many distinct NPCs appeared at all, counted by object id, so a thing that came and
  went still counts once.

Six test classes had hand-rolled the same loop, each slightly differently, and the two that had *not*
rolled it were the two that shipped a silent pass. The hand-rolled ones are now on the helper, and its
remarks say plainly why it exists — a pin that runs the clock and then counts finds an empty field and
reads it as "nothing happened", and that failure is invisible because the pin passes either way.

**Refactoring a passing test is only safe if the mutations still die.** The four that these pins were
written to catch were re-run afterwards and all four still fail, which is the check that the refactor
preserved what the loops were for rather than just their shape.

**One thing the refactor exposed.** Converting the hand-rolled loops meant passing the per-second work
as a lambda, and the obvious translation — `() => Advance(harness, boss, player, 0)` — calls a helper
whose loop runs zero times and therefore does *nothing*. The tests still passed, because the bosses
happened to stay engaged across those windows without the rehate. It was caught by reading the diff
rather than by the suite, and both instances are now the rehate directly. A no-op lambda in a watch
loop is the same class of silent failure the helper exists to remove, one level up.

**Verification.** Full suite 1,406 passing and 1 skipped, unchanged; four previously-escaping mutations
re-checked and still caught.

### The gateway guards' traps were never missing

Six adds in the backlog under `GwLGuard_FlA` and `GwDGuard_FlA` — the guardians' and archon's throw,
explosion and mine traps — are placed by `GatewayGuardAI` and always have been. They read as missing
because the class holds them in a record:

```csharp
private readonly record struct Traps(int Snare, int Throw, int Explosion, int Mine);
private static readonly Traps Elyos = new Traps(281472, 281473, 281474, 281475);
...
Rung(priority, below, flag, t => t.Snare)
```

so no id ever appears as a spawn argument.

**This file recorded that as unfixable, and it was not.** The earlier note said following a record
field "needs a type resolver rather than a regex", and that harvesting every literal out of every
constructor call "risks swallowing skill ids -- which fails in the dangerous direction". The second
half is right and the first does not follow from it. The objection was to a *broad* rule; it was
written down as an objection to the idea.

The narrow rule is safe and is three conditions: the record must be **declared in the same file**,
**every component must be `int`**, and the file must **spawn something**. An all-int record declared
beside a spawn is a table of npc ids. Checked across the whole handler tree before committing: it
contributes exactly the eight trap ids and nothing else.

**Backlog 538 → 522 across 381** — sixteen rows, because the eight traps appear under several guard
variants each.

### Two directions of wrong, and only one of them is dangerous

That makes five shape assumptions this audit has been wrong about, but this one differs in kind. The
first four made it **miss code**, so the backlog was too big — annoying, and self-correcting the moment
someone re-measures. This one was the same, but the *reason* it went unfixed was a fear of the other
direction: marking a real gap as covered.

That fear is correct and should stay. What it should not do is stop a rule that cannot fail that way.
The test for any new sweep is not "could a broad version of this be unsafe" but "what does this exact
version add" — and that is answerable by running it over the tree and reading the list, which takes a
minute.

**Verification.** Full suite 1,406 passing and 1 skipped, unchanged — this entry changes only the
measurement. The eight ids the new rule contributes were enumerated and checked individually.

### Unstable Yamennes' gates, done properly

The deliberate pass this was scoped for. Two entries ago the finding was that the gates our class
opened were not the ones either boss opens, and that swapping ids alone would not help because our
gate AI was not a translation of the pattern those ids bind. Both halves are fixed now.

**The gates are retail's.** `IDAbRe_Core_NamedD_02` and its hard twin both spawn 283203/283222/283223
upstairs and 283233 downstairs — and the downstairs three are the **same** gate on three marks, where
our class used three different ids. The boss now opens those, at retail's coordinates.

**And they do what the pattern says.** `YamennesSpawnGateAI` translates all four gate patterns: an
attacked upper gate feeds a **summoned orkanimum** onto its own fixed mark every twelve seconds, and
the lower gate a **summoned lapilima** at its own feet every nine. Neither npc was spawned by
anything. What it replaces spawned two other npcs at ±3 metres, twelve seconds in and once more at
seventy-two — a different mechanic that happened to produce adds near a portal.

**Not translated, and unportable rather than deferred:** every gate pattern opens by putting an
`IDAbRe_Core_Sum_Teleport2_Enemy` on itself with a hundred thousand hate, and that devname **resolves
to no npc in our 4.8 client**. Same class of blocker as the waypoint bucket — the data names something
the client does not have.

### A units bug the coordinates were hiding

Retail's `dir` is degrees; the spawn helper takes a heading. The coordinates this class carried before
passed their `dir` straight through — and it compiled, because every one of them was small: 0, 3, 35,
59. Retail's own gate directions run to 279, which does not fit a heading at all, and that is what
surfaced it: **the old numbers were being read as headings when they were degrees.** A wrong-units bug
can sit indefinitely behind values that happen to fit both.

**Backlog 522 → 516 across 381.**

**Verification.** Full suite 1,410 passing and 1 skipped; four new pins plus the seven existing ones
repointed at the retail gates and passing unchanged; three of four mutations caught, the fourth the
familiar inert one — a battle timer armed outside combat never fires.

### The drakan guards were one letter away

`DrGuard_RhB` turned up in the tail as a three-add encounter and is the same mechanic as the abyss
guards — enter combat, arm a twenty-second heartbeat, call reinforcements by health band. The
extractor's `^[DL]Guard_` never matched it. Counting properly: **`DrGuard` has 142 patterns with
spawns**, more than `DGuard` or `LGuard` have.

Widened to `(?:D|L|Dr)Guard_`, the family goes from 194 patterns and 1,388 rows over 870 guards to
**1,693 rows over 1,085**, and 214 more guards are repointed. `BGuard` stays out — those are the gates,
a different mechanic with its own extractor — and so do the gateway guards, which have their own class.

### A constant that was right for its subset and wrong for the family

The table did not carry `live_time` or `spawn_range`; the class hardcoded ten minutes and three metres.
That was not careless — **every branch the extractor could then see carried exactly those values**, and
this document quoted the shape census as evidence: 198 of 205 identical.

Both parts of that were narrower than they sounded. The census covered only the ops the extractor then
recognised — it matched `<spawn>` and nothing else — so the branches using `spawn_on_target` were never
in it. Those carry `live_time=100`. **Guards already shipped were affected**, not just the new drakan
ones: the garrison patrol's reinforcements last a hundred seconds and were being given ten minutes.

The table carries both columns now. The lesson is narrower than "don't hardcode": a constant lifted
from a census is only as good as the census's coverage, and a census over a filtered subset will
happily report uniformity that the whole set does not have.

**What is pinned and what is not.** The lifetime now has a pin, and it needs the right observable: a
guard that calls every twenty seconds and never stops accumulates waves, so no single wave's
disappearance is visible and ninety seconds tells a hundred-second lifetime from a ten-minute one not
at all. What separates them is where the population **plateaus** — five minutes of calls leaves most of
them retired at a hundred seconds and all of them standing at six hundred. `spawn_range` is carried
from the table but **not pinned**: telling a one-metre scatter from a three-metre one needs a
statistical test that would be flaky, and a flaky pin is worse than an honest gap.

**Backlog 516 → 503 across 370.**

**Verification.** Full suite 1,411 passing and 1 skipped; one new pin; the lifetime mutation caught, the
range one not — see above.

### The fortress chiefs, and checking the other prefixes on purpose

Missing `DrGuard` for one letter was worth not repeating, so the remaining `*Guard_` prefixes were
counted rather than assumed. `BGuard` has 148 patterns with spawns and the gate extractor covered 62 of
them; the rest are the **fortress chiefs** — `BGuard_ChiefA`, `ChiefB`, `Chief4`, `ChiefF4`, `ChiefF5`,
`ChiefD`, `ChiefS` — running the same band-driven reinforcement mechanic.

What they call up is **warp gates**, which now have a class of their own. So the chain is three deep and
all of it is ported: a chief calls a gate, the gate feeds a squad, the squad fights.

Including them takes the family to **1,856 rows over 1,200 guards** across 204 patterns. 67 more npcs
repointed; the other 48 already carried real handlers — `fortress_protector`, `fortress_instance_duke`,
`artifact_protector`, `gate_squad`, and the three `awakened_chamber_lord` chamber lords that were
hand-ported earlier. The repoint touches only `aggressive` and `simple_abyssguard`, so those were never
at risk, which is the property that makes widening the family cheap.

**Backlog 503 → 498 across 365** — five, because most of what the chiefs summon was already reachable
through the gates.

### A tooling note that cost ten minutes

Repointing by running two regex searches per guard over the whole `npc_templates.xml` is fine at 460
guards and times out at 1,200 — the file is large and the work is quadratic in guards × file size. One
`re.sub` pass with a replacement function does the same job in seconds. Worth remembering the next time
this family grows: the repoint is the slow step, not the extraction.

**Verification.** Full suite 1,411 passing and 1 skipped, unchanged.

### Where the guard families stand

| prefix | what it is | state |
|---|---|---|
| `DGuard_` / `LGuard_` | abyss guards, both factions | ported |
| `DrGuard_` | drakan guards | ported |
| `BGuard_Chief*` | fortress chiefs | ported |
| `BGuard_*Gate*` | the gates chiefs and guards open | ported, own extractor |
| `GwDGuard_` / `GwLGuard_` | gateway guards | own hand-written class |
| `BGuard_RhAPet*` | ranger pets, 20 patterns | **not looked at** |

The pets are the one group in this family nobody has read yet.

### The ranger pets, and the guard families are done

The last unread group, and the tidiest thing in this whole family: **twenty patterns, twenty-seven
pets, one shape.** Every branch spawns at two metres, for ten minutes, on a target within fifty —
nothing had to be judged, and the shape census is a single row.

Attack a pet and it lays a trap on you and disappears. It is not a fighter: it exists to place one
thing and go, and walking away makes it leave rather than follow. The level bracket is the only thing
that differs across the twenty patterns, which is why the class is a pet-to-trap table and nothing
else.

**Two notes on what is not literal.** Retail lays the trap on `OBJI_EVENT_TARGET` — whoever just
attacked — where this uses the current target. For an NPC whose entire life is the moment it is first
hit those are the same creature. And the casts are not translated: two indices against a bare count
match is not a resolution, *and* retail casts them in the same breath as `despawn_self`, so a queued
cast would not survive the despawn anyway. Unfounded and inert, which is an easy call.

**The backlog did not move**, and for a reason worth recording: every trap these pets lay is already
placed by the ranger *guards*, which were ported with the abyss guards. The pets are a second route to
NPCs that were already reachable. A flat number here means the adds were already counted, not that
nothing was fixed — the pets themselves did nothing at all before this.

**The guard families are now complete:**

| prefix | state |
|---|---|
| `DGuard_` / `LGuard_` / `DrGuard_` | ported, 1,856 rows over 1,200 guards |
| `BGuard_Chief*` | ported, same table |
| `BGuard_*Gate*` | ported, own extractor |
| `BGuard_RhAPet*` | ported, this entry |
| `GwDGuard_` / `GwLGuard_` | own hand-written class |

**Verification.** Full suite 1,415 passing and 1 skipped; four new pins; all five mutations caught.

## Hard-mode Tiamat's first form — and a claim I had no business making

`IDTiamat_Hard_Tiamat_Dragon`, npc 236276, the phase before the dying rotation
`TiamatDyingRotationAI` already covers. Ten adds, the largest cluster left, and the
first of them the fight is actually built around.

**What it does.** She wakes inside a transformation flash on its own mark a few metres
west of her, with a blazing inferno spirit and a burrowing arrival at her feet — ten,
six and eight seconds, none of the three spawned by anything before now. Fifteen
seconds into the fight a one-shot arms a four-second fuse, and on it **four drakan
mages take the four corners of the arena**, each on the heading retail gives it. A
ten-second heartbeat runs alongside adding hate. Killing her clears the group, which
takes the mages with her.

### The claim I got wrong

I wrote in the class, and would have committed, that the nineteen `IDTiamat_TiamatRush_*`
drakan of the idle timer "do not appear anywhere in our 4.8 client — the binding table
has zero matches for `TiamatRush`", and filed them with Yamennes' gate enemy as content
the client does not carry.

That was wrong, and the mistake was methodological. **`out/ai_binding.tsv` maps
*owners* — which npc runs which pattern — not devnames to ids.** Grepping it for a
spawn target and finding nothing means nothing at all. The audit resolves spawn targets
through `client_devname_to_id`, which is keyed **lower-case**, and through it all four
of the devnames I called absent resolve:

| devname | npc | what it is |
|---|---|---|
| `IDTiamat_TiamatRush_*` | 236713-236720 | the protectorate elites, eight of them |
| `IDTiamat_Tiamat_ShapeChangeFlash` | 283174 | the transformation flash — **now ported** |
| `IDTiamat_Tiamat_Dust` | 283134 | the cloud she leaves on dying — **now ported** |
| `IDTiamat_Tiamat_cutscene_play3` | 283184 | the deadly-howling cutscene |

Two of the four went straight into the class the moment they resolved. The rush is
still left out, but for the real reason: every one of its spawns carries
`pathname=path_tiamatdrakan_*`, a server-side walk route we do not have, so spawning
them leaves nineteen elites standing in the corners instead of charging the raid. That
is the audit's own "walks a server-side path" bucket — fourteen adds, eight of them
these — and the established call for that bucket is to leave them out. **The difference
matters: "the client does not have it" is closed, "we do not have the route" is owed.**

**The rule going forward:** to ask whether a devname exists, use `client_devname_to_id`
and lower-case the key. The binding table answers a different question.

### The dust cloud cancels itself, and that is kept

Retail's `on_die` spawns the dust under `SPAWN_ID_1` and then, four lines later in the
same branch, despawns that group. The cloud is placed and taken away in one breath. It
is translated literally, with a pin that asserts the cloud does *not* survive her death,
because the alternative is inventing a lingering cloud retail does not have.

### What is not translated, and why

- **The message half.** She listens for five types — 31 arms a twenty-second timer that
  rebroadcasts to the gods, 38/39/40 each cast a `SKILLI_INDEX`, 27 removes her — and
  broadcasts three of her own on entering the fight. Every cast is index-only, and
  **nothing in our tree sends her any of those numbers**: the senders are the instance
  script and her adds' own patterns. A listener with no sender is silence.
- **The hate bump** on the ten-second heartbeat: no vocabulary for it, nothing
  observable on our threat model. The heartbeat itself is translated so the chain ticks.
- **`say_to_all`, system messages, and the condition variables** (`GOD_SPAWN`,
  `TELEPORT_FUTUREIN1..4`, `SURUKANAFALLING`, `TIAMAT_SPAWN`) — instance sequencing.
- Normal mode's first form (219361, `IDTiamat_Tiamat_Dragon_Named_60_Al`, two adds) is
  still on the old `TiamatDragonAI`, and owner **856028** also binds the hard pattern;
  only 236276 was repointed.

### An unkillable mutation, honestly

Dropping `When.FirstTime(FLAGVARI_BETA_1)` from the portal step **survives every pin**,
and no pin can be written for it: nothing re-arms timer 1, so the step is a one-shot
whether or not the flag guards it. It is carried because retail carries it — if the
message half is ever translated and something re-arms that slot, the guard is already
right. Eleven of the twelve mutations tried were caught; this is the twelfth.

### The harness trap, for the fourth and fifth time

The four mages are `aggressive_no_loot` and produced **nothing** with only
`AggressiveNpcAI` registered — the fourth detour this trap has cost. The flash and the
dust then produced a worse one: they spawn from `on_wake_up`, which runs inside the
owner's own `BringIntoWorld`, so the unregistered-handler throw unwound into the
*boss's* spawn path and the same catch **deleted the boss**. The symptom was a wake-up
that half-happened, which reads like a branch-ordering bug rather than a missing
registration. `BossAiHarness.WithAi` now says so.

**Where the count went.** 498 across 365 encounters → **494 across 364**. Four, not ten:
the spirit, the arrival, the flash and the dust are all reachable elsewhere already, and
the eight rush drakan stay in the walk-path bucket. The four mages are the four.

**Verification.** Full suite 1,425 passing and 1 skipped; seven new pins; eleven of
twelve mutations caught, the twelfth recorded above.

## The same mistake, one file over — and an op we never had

Having just found that reading `ai_binding.tsv` for a devname proves nothing, the
obvious next question was where else I had done it. The answer was one file:
`YamennesSpawnGateAI` said its gates' on-wake summon "resolves to no npc in our 4.8
client". **`IDAbRe_Core_Sum_Teleport2_Enemy` is npc 282016**, a spawn gate, and it is in
our templates.

That one was worse than a missing add, because of what the summon is for:

```
spawn_on_target target_obj=OBJI_SELF npc_nameid=IDAbRe_Core_Sum_Teleport2_Enemy
    live_time=70 valid_distance=50 attack_target_after_spawn=TRUE hatepoints_to_add=100000
```

The gate summons something **that attacks the gate**. That is how a gate gets into
combat — and `on_enter_attack_state` is where its feed timer is armed. Without it the
gate stands inert unless a player attacks it, which no player has a reason to do. The
class's own pin said so out loud: `AnUnattackedGateFeedsNothing` asserted the gate feeds
nothing, and passed. It was pinning the bug.

### `attack_target_after_spawn` — a vocabulary gap, not a one-off

The op is not rare. Across the 5.8 dump, **384 spawns set it TRUE**, over 189 patterns,
and our pattern runtime had no word for it at all:

| where the add is placed | rows | what our runtime does today |
|---|---|---|
| `OBJI_SELF` — at the spawner, attacking the spawner | 53 | **now translated**: `Do.SpawnAsMyEnemy` |
| `OBJI_CUR_TARGET` — on the tank, attacking the tank | 99 | arrives passive |
| `spawn_on_multi_target` — one per player, each attacking | 133 | arrives passive |
| `spawn_on_target_by_attacker_indicator` | 39 | arrives passive |
| `OBJI_ATTACKER` / `OBJI_EVENT_TARGET` / others | 60 | arrives passive |

`hatepoints_to_add` runs from 1 to 99,999,999; the common values are 100 and 1, with the
huge ones (100,000 for these gates, a million elsewhere) meant to outrank anything a raid
can build so the summon never peels.

**What is owed.** Twenty-one patterns our source already names carry TRUE rows, and every
one of them currently spawns a passive add where retail spawns a fighting one:

```
DF4_Dramata (20)          LDF4b_Golden_Gururu (6)   LF4_FieldRaid (5)
DGuard_RsA/WsA/WsB (7)    DrGuard_RsA/RsB (5)       LGuard_RsA/WsA/WsB (7)
IDAbRe_Core_NamedD{,_02,_Hard,_Hard_02} (4)         IDCT_Boss_Shadow (2)
IDTiamat_T1_Crack_Key_Named_60_Al (1)               IDAbRe_Core_Summon4* (4, done)
```

The guard families are the bulk of it and they are table-driven, so the fix is a column
in `extract_guard_reinforcements.py` rather than twelve edits — a next pass, not this one.

### Two things the primitive had to learn

**Both sides get provoked, not just the summon.** Retail's engine makes the summon
attack and the victim's entering combat follows from being hit. Here the summon can be a
passive `general` NPC that never swings — 282016 is exactly that — so waiting for a hit
leaves the pair standing next to each other forever.

**And it has to happen a tick later.** Every use of this op is on `on_wake_up`, which
runs from inside the owner's own `BringIntoWorld`; a state flip made there is overwritten
by the rest of the spawn path and the NPC ends up IDLE. Deferring by a zero-delay
schedule is the same answer `SetIdleTimer` already gives to a zero delay. Both of these
are mutations that were caught rather than guesses: provoking one side survives nothing,
and running inline survives nothing.

**Verification.** Full suite 1,426 passing and 1 skipped. Two new pins on the gates, one
of them replacing the pin that had been asserting the bug; four mutations, all caught.

## The guards' traps arrive fighting — thirty-four rows, one column

The list left owed by the last entry, worked from the top. The `[DL]Guard_*` family is
table-driven, so carrying `attack_target_after_spawn` for it is a column in
`extract_guard_reinforcements.py` rather than twelve hand edits: **34 of the 1,856 rows**,
across six patterns, all of them `spawn_on_target` — the rangers' traps and the wizards'
frost pillars.

They land on the player the guard is fighting and, in retail, **engage that player**.
Ours landed inert: a trap you could stand next to and walk away from. The hate retail
gives them is 1 or 100 — enough to start the fight, nowhere near enough to hold a raid's
attention, which is why it reads as a trap rather than as a second guard.

The runtime side is `SpawnOnTarget(..., attackHate:)`, sharing the `Provoke` the gates
introduced. Two details the shared path needed:

- **The victim's half only runs for an NPC victim.** A player has no AI to put into a
  fight, and being attacked is handled everywhere else. For the `OBJI_SELF` form that half
  *is* the point — the spawner's own `on_enter_attack_state` is what the summon exists to
  trigger — so it is kept, conditionally.
- **Hate does not land on a player who is unaware of the attacker**, the same rule that
  moved Vanuka's rally pins onto `SetTarget` months of entries ago. So the pin observes
  the trap's state and its target, which is what the mechanic is, rather than a hate
  number that our aggro rules drop on the floor.

### A flaky pin, found by walking past it

`AWaveLivesForItsOwnPatternsLifetime` failed on this run: *"a hundred-second wave should
have retired most of 18 arrivals, 10 standing"*. Nothing to do with this change — the
patrol calls at **fifty percent**, so that pin was comparing two numbers that both depend
on coin flips, and the ratio it wanted was inside the variance. It has been passing on
luck.

It now remembers **which** NPCs landed, by object id, and asks whether those are still
there at 99 seconds and gone at 102. Deterministic, and it pins the hundred seconds to the
second instead of to a plateau. Five consecutive runs clean.

**What is still owed on this op.** 331 of the 384 rows remain, in forms the table cannot
carry: `spawn_on_multi_target` (133) and `spawn_on_target_by_attacker_indicator` (39) are
skipped by the guard extractor for the reason they were always skipped — the cap, the
ordering and the attacker indicator are fields the row does not have — and the remaining
per-boss patterns (`DF4_Dramata` 20, `LDF4b_Golden_Gururu` 6, `LF4_FieldRaid` 5,
`IDAbRe_Core_NamedD*` 4, `IDCT_Boss_Shadow` 2, `IDTiamat_T1_Crack_Key` 1) are hand-written
classes that each need the flag threaded through their own tables.

**Verification.** Full suite 1,427 passing and 1 skipped; one new pin and one repaired;
five mutations, all caught.

## The rest of `attack_target_after_spawn` — and Shadowshift was spawning four times too many

Finishing the list. Four of the six remaining patterns are hand-written classes whose
spawns are one edit each; the fifth and sixth turn out not to be flag work at all.

| pattern | class | what it needed |
|---|---|---|
| `LF4_FieldRaid` | `OmegaAI` | all five clone waves, hate 100 |
| `LDF4b_Golden_Gururu` | `GoldenTatarAI` | avatar, eye and magma, hate 1 |
| `IDCT_Boss_Shadow` | `ShadowshiftAI` | both spectres, hate 1 — **and the caps were wrong** |
| `IDTiamat_T1_Crack_Key…` | `TiamatsIncarnationAI` | Fissurefang's power attack only, hate 10,000,000 |
| `DF4_Dramata` | `GelkmarosPadmarashkaAI` | its rocks are not translated at all — a porting job |
| `IDAbRe_Core_NamedD*` | `UnstableYamennesAI` | same: `IDCatacombs_Hard_Buff` is not translated |

### Shadowshift was dropping a spectre on everybody

Reading the pattern for the flag meant reading it properly, and the class was wrong about
something bigger. It said *"Retail sets no `total_set_to_spawn`, so every valid target
gets one"* and used a cap of 64. Retail sets it on both:

| spectre | cap | order | scatter |
|---|---|---|---|
| `Sum1`, near | **2** | random | 3m |
| `Sum2`, far | **1** | most-hated | 10m |

Against a full group that is the difference between three spectres a cycle and a dozen —
and the far one re-arms **every four seconds**. This is the exact failure the
`SpawnOnEachTarget` doc comment warns about, written after Fissurefang did the same
thing, and it had been sitting one class over the whole time. Two pins asserted the bug
(*"one on every player, not one on the boss"*) and passed; both now assert retail's caps
and the far spectre's most-hated ordering.

### Only Fissurefang's power attack is hostile

Worth stating because it is the kind of thing that invites a sweeping edit: within the
incarnations, **the flag appears exactly once**. Fissurefang's power-attack earthquake
carries it with ten million hate; its own area-attack twin is written `atk=FALSE`, and
Graviwing and Petriscale carry it nowhere at all. Both facts are pinned — one that the
quake arrives fighting, one that the whirlpool does not.

### A pin that passed for the wrong reason, caught by mutation

The first version of the three new pins asserted the add's **state and target**. All
three mutations survived. The reason is worth keeping: these adds are `aggressive` and
retail drops them *on top of* a player, well inside their own search range, so they engage
by themselves in the same tick. State and target are identical whether or not the flag is
honoured.

What separates them is the **hate**. Natural aggression is worth one point; the flag adds
retail's `hatepoints_to_add` on top. So two points is the fingerprint for the hate-1 cases
and a hundred and one for Omega — and those pins kill every mutation, including turning
Omega's hundred into a one.

That also settles what the flag buys us in practice. For an add whose own AI is aggressive
and which lands on its target, it is nearly a no-op today; it matters for hazards that are
not aggressive (Fissurefang's quake, the gates' summon), and it is carried on the rest
because it is what the pattern says and because the adds' own AI is not guaranteed to stay
aggressive.

**Still owed:** `DF4_Dramata`'s rocks and Yamennes' `IDCatacombs_Hard_Buff` are not
translated at all — both are `spawn_on_multi_target` with caps and orders in hand, so they
are ordinary porting jobs rather than flag work. And the 133 multi-target and 39
attacker-indicator rows in the guard table stay skipped for the reason they always were:
the row cannot carry a cap, an order and an indicator.

**Verification.** Full suite 1,432 passing and 1 skipped; five new pins and two repointed
off the bug they were pinning; eight mutations, all caught.

## Padmarashka's forty-rock ring was an invention

The last item on the owed list, and the biggest single divergence found in a while. Her
class dropped **forty rocks in a ring around a fixed point**, once, at 10% health, with no
lifetime. Retail's `DF4_Dramata` does none of those things.

| what retail does | what we did |
|---|---|
| rocks land **on players**, `spawn_on_multi_target` | in a ring around a hardcoded centre |
| capped at 3, 4 or 5 depending on the source | forty, always |
| twelve-second lifetime | none — they stayed for the fight |
| **five** sources on their own timers | one, at one threshold |
| each engages whoever it landed on | inert |
| two rock npcs — 281936 and **282140** | only 281936 |

The five sources, which is the shape of the fight rather than a detail:

- **opening step** — three B rocks, once, on the third heartbeat tick
- **every 90s from then** — three more (timer 17)
- **every 90s from the first tick** — four B rocks (timers 6 and 7, handing off at 45 each)
- **below 10%** — *fifteen* heavy rocks at once, three draws of five, and four more every
  90s afterwards (timers 2 and 3)
- **below 5%** — fifteen more, once

### The heartbeat is a ladder, and the cast-only step is load-bearing

Timer 0 re-arms every five seconds and its branches are one-shot steps guarded by flag
vars, so the fight walks down them rather than looping: tick one opens the long-cycle
chains, **tick two is a step that does nothing but cast**, tick three is the opening
rockfall. Translating that second step looks like translating nothing — and dropping it
is a caught mutation, because the rocks then arrive five seconds early. A step that
consumes a heartbeat is doing work even when its actions are all untranslatable.

The class is now a `PatternAi`. Everything Java does is untouched: the protective slumber,
the four shield NPCs that break it, the stat overrides, the berserk at 5%. `HpPhases`
drops from `(10, 5)` to `(5)` because the 10% rockfall belongs to the pattern now.

**A pin that was describing the invention** — `GelkmarosPadmarashkaDropsHerRocksAtTenPercent`,
which walked HP down and asserted rocks appeared at exactly 10 — is retired. It was not
wrong about the old code; it was pinning a mechanic that does not exist. Twelve pins in
`PadmarashkaRockfallTests` replace it, on when each chain fires and how many it drops.

### Two mutations that survived, and what they were hiding

**"Leaving the fight leaves the rocks standing"** survives and cannot be killed: the
Java-parity `HandleBackHome` already deletes both rock ids by hand, so the pattern's
`on_leave_attack_state` branch is redundant with it. It is kept because retail has it and
because `on_die` — which nothing else covers — is the same line. That half is pinned.

**"The timer-2 chain fires once and stops"** survived the first time round because the pin
only watched the chain's first firing. Extended to its ninety-second repeat, it dies. The
first version was measuring that something happened rather than that it keeps happening,
which is the same shape of mistake as the transient-window family.

### Yamennes' furies were translated; only the flag was missing

Filed as "not translated at all" last entry, which was wrong — `SpawnFuries` had the cap,
the ordering and all three timings right. What it lacked was the two million hate, which
matters: a fury lives ten seconds and is meant to be dealt with by whoever it picked
rather than peeled onto a tank. `AttackAfterSpawn` moved out of `PatternAi` into its own
file so a Java-parity class with hand-written timers can use the same op.

**Where the count went.** 494 across 364 encounters → **487 across 361**. The seven are
Padmarashka's B rock and the six adds that came with reading her pattern properly.

**Still owed on this boss.** The rest of `DF4_Dramata` is not translated: fourteen skill
indices across timers 1, 5, 9, 10, 11, 12, 15, 16, 20, 25, 26, 27, 28 and 29, the
waypoint branches that lay eggs on patrol, the abnormal-state handlers, and the
`on_message` pair. The rocks are the part that is index-free.

**Verification.** Full suite 1,444 passing and 1 skipped; thirteen new pins and one
retired; thirteen mutations, all caught after the two above were addressed.

## The gateway garrisons' priests and mages

With the owed list closed, back to the ranked audit — and the top of it is now flat, a long
tail of threes and twos rather than a cluster. The largest coherent group left was the
gateway guards' rank and file: `GwLGuard_PhA`, `GwLGuard_WhA`, `GwDGuard_PhA` and
`GwDGuard_WhA`, **twelve guards, all on plain `aggressive`**, with every trap they lay
spawned by nothing. `GatewayGuardAI` already covered the named `Gw*Guard_FlA` fighters;
these are the ordinary garrison.

**Two roles, and the difference is where the trap goes.** A priest lays at its own feet —
ground it is defending. A mage lays on whoever it is fighting, so the trap follows the
player. Same three-rung shape either way: one on engaging, one below 50, one below 30,
with the deeper rung outranking the shallower so a guard burned down fast skips the middle.

**Two quirks kept literal**, and both are pinned because a family constant is exactly the
kind of thing that gets tidied later:

- the **Elyos priest's** opening net trap lives **fifty** seconds where every other trap in
  the family lives sixty;
- the **Asmodian priest** lays its opening trap within **one** metre rather than two.

**Timer 1 is deliberately not reproduced.** It is a health-banded cast ladder carrying
nothing else. `GatewayGuardAI` keeps its cast-only rungs as bare re-arms because those sit
on timer 0 and each one spends a tick of the trap ladder — dropping them moves every trap
below forward five seconds. This timer cannot do that: it is a different slot, so an empty
version of it would be a branch that does nothing at all. The distinction is worth stating
because the two classes look inconsistent side by side and are not.

### The audit's record rule was one table too tight

The backlog did not move on the first measurement, and the reason was the audit rather than
the port. It follows npc ids held in a record's fields — added earlier for exactly this
family — but only when **every** component is an `int`. This class holds its ids in
`Kit(bool OnTarget, int Opening, float OpeningRange, int OpeningLife, …)`, and one `bool`
in front of them hid all ten.

Now read **per component, by position**: only the `int` slots are harvested. That costs no
precision, since a `bool` or a `float` was never going to hold an npc id, and it turns a
rule that happened to fit one class's shape into one that fits the shape of the problem.
The same third-time-lucky lesson as the spawn ops: a rule written against the example in
front of you fits that example.

**Where the count went.** 487 across 361 encounters → **475 across 357**. Twelve traps, and
they are the whole of it.

**Verification.** Full suite 1,454 passing and 1 skipped; ten new pins; ten mutations, all
caught.

## A third blocked bucket: spawns nothing can trigger

Vasharti was next on the list — three glove controllers on `IDYun_Nmd6` and three more on
the hard pattern, all reading as fully implementable. They are not, and the reason is one
the audit could not see.

All six sit under **`on_arrived_at_waypoint`**. Their placement is ordinary
(`SPAWN_LOCATION_MY_POINT`, at the boss's feet), so nothing about the spawn looks blocked —
but the event only ever fires for an NPC walking a named route, and our spawn data gives
Vasharti a **single static spot**:

```xml
<spawn npc_id="217313">
    <spot x="188.17" y="414.06" z="260.7549" h="86" />
</spawn>
```

He never arrives anywhere, so the branch never runs. Porting it would have produced a
mechanic with nothing to trigger it — the same dead end as the waypoint-*placed* bucket,
reached from the other direction.

The audit now separates them. It tracks which event handler each spawn action sits under,
and an add whose **every** spawn hangs off a waypoint arrival is reported as
`[BLOCKED: only a waypoint arrival spawns it]`. If any one of its spawns hangs off anything
else, it stays actionable.

```
  fully self-contained                       : 407
  positionable, but walk a server-side path  : 14
  positionable, but only a waypoint fires it : 10
  blocked on server-side waypoint paths      : 44
```

**Ten adds**, and they are a tidy set: Vasharti's six glove controllers, and four Seal of
Destruction escort NPCs whose bombers and guards spawn a replacement as they patrol.

**This does not lower the backlog** — the total is still 475 across 357 — and it should not.
Nothing was fixed; what changed is that ten rows moved out of the column that says "somebody
could port this today". A measurement that calls blocked work actionable wastes the next
session, which is the same argument the walk-path bucket was split out on.

**Correction, immediately.** The first draft of this entry called the walk routes "a data
import job against the client's own path files" and "the single largest remaining lever".
That is wrong, and it is wrong against a finding in this same document: the client carries
**no NPC route data at all** — 3,332 archives and 525,657 entries indexed, and the only
path-named files are flight paths. The routes are server-side data nobody has. Sixty-eight
adds are blocked on it and there is no import to run.

**And Vasharti's six were already blocked twice over.** `BrigadeGeneralVashartiAI` records
the other half: 283002/283004/283006 are plain aggressive clones of Vasharti himself with
no controller AI, so spawning them would put three extra full-strength bosses in the room
rather than retail's controllers. The waypoint finding is a second, independent reason —
useful because it is one the audit can see and act on, where the first lives only in a
class comment.

## Two audit false positives, and a divergence left deliberately alone

Next on the ranked list were `F4_Rotation_Normal_Monster` and `F4_Rotation_Party_Monster`
— eight NPCs sharing two patterns, three adds each. Neither is missing. Both are the same
false positive, and it is a shape the audit had not met:

```csharp
npcId = 856175 + Rnd.Get(0, 3);
...
Spawn(npcId, ...)
```

`ConquestOfferingAggressiveAI` has been placing one of the four shugos on every rotation
kill since it was ported. No id appears inside a spawn call's parentheses, so three of the
four read as never spawned. The audit now resolves an id assigned to a local that a spawn
call later passes, and resolves the `+ Rnd.Get(0, n)` form to the whole span, capped at
eight.

**Restricted to the first argument, on the second attempt.** Taking identifiers from
anywhere in the argument list harvested `EnemyHate = 100000` out of
`Do.SpawnAsMyEnemy(TeleportEnemy, Fed, EnemyLife, EnemyHate)` — a hate value read as an npc
id. Harmless there, since no npc has that id, and not harmless in general: a delay or a
hate value the width of an npc id would silently suppress a real finding. Every spawn helper
in this codebase takes the id first, so the first argument is the whole of it. Nine ids
stopped being harvested and the backlog did not move, which is what that change should look
like.

**Where the count went.** 475 across 357 encounters → **469 across 355**. Six rows, all of
them the same three shugos under two patterns.

### The rotation kill's odds do diverge, and are left as they are

Reading the pattern to confirm the false positive turned up a real difference, recorded
here rather than acted on:

| | retail `F4_Rotation_*_Monster` | ours (`ConquestOfferingAggressiveAI`) |
|---|---|---|
| shugo | ~31.4% — four branches at 9% each, first match wins | 24.75% |
| which shugo | **uneven**: 9.0 / 8.2 / 7.5 / 6.8 | uniform, one in four |
| portal | — | 30.25%, `833018`/`833021` by world |
| broadcaster | **always**, `856502` | never |
| nothing | never | 45% |

**Not changed, for two reasons.** Retail's always-spawned `BF4_Rotation_Time_Reset_BR_NPC`
is an invisible control NPC whose whole pattern is "broadcast message 13929 to fifty metres,
three times, then despawn" — and nothing in our tree listens for 13929, so porting it adds
an NPC that does nothing. And the secret portal is the other way round: it is real,
player-facing, has its own working AI, and retail's pattern does not spawn it. Deleting it
to match would remove content that works, on the authority of a 5.8 pattern for content our
4.8 target may carry differently.

**What it would take.** The shugo odds alone are portable without touching the portal — that
is a five-line change. What stopped it is pinning: the difference between 31.4% and 24.75%,
and between uneven and uniform, is only observable statistically, and this work has already
retired one flaky probabilistic pin. Making it verifiable means an injectable RNG at the
`Rnd` static, which is a change to shared infrastructure rather than to this encounter.
Worth doing when something else needs it too.

## Captain Adhati — a boss wearing a shared behaviour

`Dread_DrakanBoss`, npc 214823, on the Dreadgion. He was on **`xdrakanpriest`**, a
Java-parity behaviour shared with **ninety-four other NPCs**: a three-percent chance per hit
of calling up one to three of npc 282988. Not a weaker version of his fight — somebody
else's fight entirely, and none of his own three servants was spawned by anything anywhere.

**What he does is a five-rung escalation.** Two attackers the moment he is engaged, onto
fixed marks on the deck; then a heartbeat carries one-shot steps that each call a
differently-composed wave and **round on somebody else**:

| rung | wave | lifetime | then |
|---|---|---|---|
| on engaging | 2 attackers, on fixed marks | 25s | — |
| below 80 | 4 attackers | 30s | second-most-hated |
| below 65 | 1 attacker + a **buffer** | **22s** | third-most-hated |
| below 45 | 3 attackers + a **healer** | 30s | second-most-hated |
| below 35 | nothing but a cast | — | — |
| below 20 | 4 attackers + healer **and** buffer | 30s | a random attacker |

The rungs are one-shots with the deepest outranking the rest, so a boss burned down fast
skips to the wave it deserves rather than walking every one down.

**Two things that look like nothing and are not.** The empty rung at 35 re-arms the
heartbeat at **ten** seconds where the idle fallback re-arms at **seven** — running it
changes when the next rung can fire, and both are caught mutations. And the difference
between the two cadences is a single second at the first opportunity, which is why the pin
measures it after five idle ticks, where it has grown to five.

**A lifetime pin that had to be tightened.** "The 65 wave keeps thirty seconds" survived the
first pass: the pin let the wave land somewhere inside an eleven-second window, and measured
loosely a thirty-second wave and a twenty-two-second one both read as gone. Landing it on an
exact second — the heartbeat's cadence is known, so this is arithmetic rather than luck —
kills it. The transient-window family again, in its timing form rather than its
did-it-happen form.

**`SpawnOffset` gained a height offset**, because every one of his waves carries `z=3` or
`z=4`. It also gained an honest note: his offsets are the **first asymmetric ones** any
ported pattern uses — (8, 8), (3, -2) — and whether retail rotates a relative offset by the
NPC's heading is still unsettled. He stands on a fixed mark facing one way, so the two
readings cannot be told apart from the pattern alone. Settling it needs observation of the
live encounter.

**Not translated.** Four skill indices, and with them timer 1 (a cast on a twenty-second
cycle carrying nothing else) and timers 2 and 3, which are a chain of `broadcast_message` at
6835 and 6837 that nothing in our tree listens for — his servants run plain `servant` AI,
which is not a message listener. Also out: the `goto_waypoint` he opens with, since we have
no route for him.

**Where the count went.** 469 across 355 encounters → **466 across 354**.

**Verification.** Full suite 1,466 passing and 1 skipped; twelve new pins; thirteen
mutations, all caught after the two above were addressed.

## Frostmane Lestin, and the add that was only reachable by mistake

`ND2_ElementalSu2`, npc 212875. He was on `summoner`, the generic table-driven Java AI, and
his whole fight was five lines of data that got three separate things wrong:

```xml
<percentage percent="80"><summonGroup npcId="280481" minCount="4" distance="10"/></percentage>
<percentage percent="60"><summonGroup npcId="280481" minCount="4" distance="10"/></percentage>
<percentage percent="40"><summonGroup npcId="280481" minCount="4" distance="10"/></percentage>
```

| | retail | ours |
|---|---|---|
| what he calls | **three different elementals**, 280489 → 280490 → 280491 | the same NPC three times |
| which NPC | those three | **280481**, a fourth of the same name, a level lower |
| thresholds | 66-90, 41-65, 21-40 — so below 90, 66 and 41 | 80, 60, 40 |
| what happens to the last wave | **each wave despawns the one before it** | all twelve accumulate |
| who he turns on | whoever is **closest to dying**, from the second rung | nobody, he stays on the tank |

### The vocabulary gap that last row is

`ATTACKERI_HAS_LOWEST_HP` had no equivalent in our `AggroTarget`, and it is not rare. Across
the 5.8 files the attacker indicators run:

```
ATTACKERI_RANDOM_ONE                3,492
ATTACKERI_SECOND_HATING               725
ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT   399
ATTACKERI_HAS_LOWEST_HP               356
ATTACKERI_THIRD_HATING                281
ATTACKERI_HAS_MOST_HP                  58
```

Picking on whoever is closest to dying is the **fourth most common** thing a retail boss does
with a target and there was no way to say it. `LOWEST_HP` and `MOST_HP` are now in the enum,
ranked by health **fraction** rather than absolute HP — absolute would make a boss reaching for
the most nearly dead pick whichever class has the smallest pool, every time, however healthy
they were. A mutation swapping the two ranking directions is caught.

### Correcting him made the backlog go up by one, correctly

280481 was in nobody's retail pattern for Lestin — it was only *reachable* because his summon
table called it by mistake. Removing the now-dead table left it unspawned, and the audit
immediately reported it under **`ND2_NeutEgg2`**: the klaw egg (280482), which was on plain
`aggressive` and therefore sat there as a fightable egg that never hatched. Retail has it put a
faithful subordinate on its own mark for ten minutes and remove itself. Ported, so the
subordinate is back in the world through the door it was meant to come through.

That egg's `on_see_user` branch is **dead in retail too**: it repeats the wake branch's actions
behind the same test-and-set flag, so waking consumes the flag and the copy can never pass.

### An ordering mutation that is genuinely inert

"The shallow rung outranks the deep one" survives every pin here and cannot be killed. Lestin's
guards are `is_hp_in_boundary` bands that **tile without overlapping** — 66-90, 41-65, 21-40 —
so at most one can ever match and evaluation order changes nothing. That is the opposite of
Captain Adhati one entry above, whose rungs are `is_hp_lower_than` and therefore all true at
once below the deepest threshold, where the ordering is the whole mechanic. Both patterns write
their branches deepest-first; only one of them needs it.

**Where the count went.** 466 across 354 encounters → **463 across 353**. Three elementals out,
one subordinate in and back out again with its egg.

**Verification.** Full suite 1,477 passing and 1 skipped; eleven new pins; twelve mutations
caught, one build-broken, one recorded above as inert.

## The duke's gate was pouring out the wrong guards

`IDAB_Reward_Item_NoShowNPC_09`, npc 284978 — and this one was not a missing mechanic but a
**live wrong one**.

The fortress duke opens an illusion gate, and one abyss chief's last reinforcement band calls
the same gate. It already carried `ai_name="illusion_gate"`, so it already ran
<see cref="IllusionGateAI"/> — a class written for the *other* illusion gate, 281226, with one
hardcoded guard set. The duke's gate opened and the **awakened chamber lord's** warguard,
bowguard and aetherguard came through it. Its own three (284979, 284980, 284981) were in
nobody's reach, which is how the audit found it.

The two patterns are the same mechanic with the same clock — five seconds to a warguard and an
aetherguard, thirty more to a bowguard and two aetherguards, five more and the gate closes —
and a different set of ids, so the class is now a two-row table.

**The trap is worth naming: a shared `ai_name` is not a shared guard list.** From inside the
class everything looked right; nothing but reading the second pattern showed that the ids
differ. Every AI keyed by name rather than by npc id has this exposure, and the audit only
catches it when the unreachable ids happen to be somebody's adds.

**Where the count went.** 463 across 353 encounters → **460 across 352**.

### Researched and deliberately not ported: the Ophidan Bridge runaway

`BIDF5_U01_Ctrl_01` (856054) was next on the list and is left alone on purpose, with the
reasoning here so nobody re-derives it.

It is an invisible controller that on waking rolls one of three HERO fugitives — escapee
asachin, runaway hirakiki, fugitive mazikin — at its own mark, puts two check NPCs on fixed
coordinates, and removes itself. That much is a twenty-line class.

**What stops it is the chain around it.** Our spawn file has the controller and *fifteen* other
runaway spawns **commented out** in `300590000_Ophidan_Bridge.xml` — the fugitives at four
checkpoints in three class flavours. Reading the rest of the chain says why that is not just a
matter of uncommenting:

- the check NPCs (856062) run `BIDF5_U01_Ctrl_07`, which listens for **message 10800** and
  spawns despawn-NPCs at two marks. Nothing in our tree sends 10800;
- each fugitive's own pattern sets `mboss_spawn` to one of four values and `ra_as_spawn` — 
  **condition spawn variables**, which are instance-script state we do not model at all;
- the fugitive moves between the four checkpoints, which is the walk-route blocker again.

Porting the controller alone would put a HERO boss and two inert invisible NPCs into a live
instance, and would move three rows out of the backlog while nothing worked — the same
"the number moved before anything ran" failure recorded against the generated guard table.
**What it needs first is condition-variable support and the message bus reaching instance
scripts**, not another AI class.

**Verification.** Full suite 1,480 passing and 1 skipped; three new pins; four mutations, three
caught and one inert (a fallback branch no live gate id can reach).

## A new audit: AI classes that reach the *wrong* ids

The illusion gate was found by accident. A class shared by two NPCs hardcoded one guard set,
so the second gate poured out the first one's guards — and the only visible symptom was three
ids nobody could reach. The missing-adds audit answers "does anything reach this id"; it is
blind by construction to "does this reach the *right* id", because from inside such a class
everything is consistent. It is just consistent with somebody else's pattern.

`tools/client-extract/audit_shared_ai_names.py` looks for the family on purpose. For every
`ai_name` our code implements and 2..12 NPCs carry, it compares the spawn sets of the retail
patterns those NPCs bind. Where the sets disagree, the class has to be choosing between them
somehow — and if it does not, one NPC is wrong.

```
ai_names our code implements : 510
  of those, shared by 2..12 npcs : 205
shared names whose patterns disagree about what to spawn: 46
```

Of the 46, **eleven** name some of the ids in the class and are missing others — the shape the
gate had. They are, with what the class is missing:

| ai_name | missing |
|---|---|
| `agrint` | 219170-3, one per season, on all eight agrints |
| `brigade_general_vasharti` | 856351-3 (the hard mode's glove controllers, blocked separately) |
| `captain_xasta` | 282444 |
| `dancing_flame` | 282999 |
| `eternal_bastion_dragon` | 284075 |
| `fortress_instance_duke` | 296338, 296339 |
| `gravity_tornado` | 856047 |
| `orissan_summon_helper` | 855702, 855703, 856306, 856309 |
| `tiamats_incarnation_spawn` | ten `_invisible` damage twins |
| `twin_protector` | 855626 |

A difference is a candidate, not a verdict: `tiamats_incarnation_spawn`'s ten are the
`_invisible` twins the missing-adds audit deliberately filters as scenery, and Vasharti's three
are the glove controllers already recorded as blocked twice over. The rest are worth reading
one at a time.

### Vasharti's dancing flames, the first one read

All **four** NPCs — the two flames and the two skill launchers — share `dancing_flame`, and the
class treated them as one thing: every one of them cast the buff directly, on a ten-then-nine
second timer, if a player stood within ten metres.

Retail splits the job. **A flame is a spawner**: every three seconds it puts a launcher of its
own colour on its own mark, and that launcher lives **two seconds**. **A launcher is a caster**:
its entire pattern is one self-cast as it appears. So the buff lands three times as often as
ours did, and it lands whether or not anyone is standing there — the ten-metre check was ours.

**282999 was reachable by nobody**, and the reason is the exact shape this audit was written
for. The class picked its skill with `GetNpcId() == 282998 ? 20536 : 20535` — red launcher
against *everything else* — so the blue launcher was the anonymous half of a ternary and nothing
ever created it.

**The one inference, stated plainly.** The launcher casts `SKILLI_INDEX_0` and neither launcher
carries an `npc_skills` row, so the index does not resolve from our data. What does resolve is
the pair of ids the Java class already carried, and the colour mapping its ternary already
implied. Structure from retail, skill ids from Java, and the blue launcher named rather than
left as "else".

**The backlog does not move**, and that is right: the launchers are `name_id`-bearing FX rather
than fightable adds, so they were never in it. What moved is that one of them exists.

**Verification.** Full suite 1,486 passing and 1 skipped; six new pins; five mutations, four
caught and one that will not compile.

### Two more read off the shared-name list

**Tiamat's gravity tornado** was wrong in two ways at once, and both are the same root cause.

Its class chose with `GetNpcId() == 283142 ? 20966 : 21901` — and **283142 is the crusher**, which
never carries this AI. The test could not be true, so every tornado took the else branch and
*both modes cast the hard-mode skill*. And it never spawned the crusher at all, which both
patterns do on waking; the hard one (856047) was reachable by nobody.

Which skill is which is corroborated rather than guessed: both are named "Gravitational
Confusion" and are told apart by stack name — 20966 is `IDTIAMAT_TIAMAT_GRAVITY_SKILL`, 21901 is
`IDTIAMAT_HARD_TIAMAT_GRAVITY_SKILL`. That matches the two patterns exactly, so the ternary's
intent is certain even though what it tested could never work.

**The cast cadence stays ours, deliberately.** Retail casts once on waking and then only on
`on_message` 204. Nothing in our tree sends 204, so translating that literally would leave the
tornado casting once and falling silent. Recorded rather than repaired: repairing it means an
instance script we do not have.

**A pin that had to change shape.** `AIActions.UseSkill` goes through `NpcController.UseSkill`,
which fires immediately instead of queueing, so `DrainQueuedSkills` sees nothing and the cast is
not observable in the harness. The *choice* is: `GravitySkillFor` and `CrusherFor` are `internal`
and pinned directly. Pinning the decision rather than the effect is the right move when the
effect leaves no trace the harness can read.

**Unstable Yamennes' hard mode was missing a whole branch.** Painflare's pattern has a battle
timer the durable boss's does not: two minutes into the fight, and every three thereafter, three
**summoned ametgolems** (283229) take fixed marks on the lower floor for three minutes each. The
class is shared by both bosses and had no such branch, so 283229 was in nobody's reach. Its wave
lifetime is exactly its interval, so the waves hand over with no gap — which means a head-count
never reads zero and the pin has to watch object ids instead.

**Where the count went.** 460 across 352 encounters → **459 across 351**. One, because the
tornado's crusher and the flames' launcher are FX rather than fightable adds and were never
counted; the ametgolem is.

**What is left on the shared-name list.** Nine, and they are not all bugs:

| ai_name | what to check |
|---|---|
| `agrint` | 219170-3, one per season, on all eight — likely a second summon per agrint |
| `brigade_general_vasharti` | the hard mode's glove controllers, blocked twice over already |
| `captain_xasta` | 282444, on the fall-off variant |
| `eternal_bastion_dragon` | 284075, on two of the four dragons |
| `fortress_instance_duke` | 296338, 296339 |
| `orissan_summon_helper` | 855702, 855703, 856306, 856309 |
| `tiamats_incarnation_spawn` | ten `_invisible` damage twins — scenery the other audit filters |
| `twin_protector` | 855626 |
| `yamenessportal` | 282016, which is the gate summon `YamennesSpawnGateAI` now spawns |

**Verification.** Full suite 1,493 passing and 1 skipped; nine new pins; ten mutations, all
caught.

### The agrints' underlings were on the wrong trigger

Eight seasonal agrints, eight patterns, and all eight agree — so this is one mechanic eight
times over rather than a per-season judgement.

The class called five underlings **once, when the agrint fell past half health**, and they
stayed for the rest of the fight. Retail calls five **thirty seconds into the fight and every
two hundred seconds after**, five metres out, each living **twenty seconds**. A recurring squall
rather than a single permanent wave — and an agrint killed inside three minutes never sees the
second one, which our version could not express at all.

The ids were right. `GetNpcId() + 320` for the Elyos four and `+ 308` for the Asmodian four both
land on the shared underlings 219170-3, and reading the patterns confirms both factions call the
same ones. This entry is here because the **trigger** was wrong, not the target — which is a
failure the missing-adds audit is structurally unable to see, and the shared-name audit only
surfaced by accident: its triage greps for literal ids and a computed one reads as absent.

**Recorded, deliberately not changed: the death drop.** Every pattern spawns **48** chests at 24
metres for ten minutes; this class spawns **6** at one to six metres with no lifetime. That is an
eightfold difference in what an agrint pays out, and it is reward economy rather than AI
behaviour — the same call already recorded for the Conquest rotation's shugo odds. The numbers
are in the class so the decision is a one-line change whenever somebody wants to make it.

One detail worth keeping because it says what kind of data this is: the **Asmodian winter**
pattern scatters its chests at 23 metres where the other seven use 24. Eight hand-written
patterns, not one template stamped eight times.

**An interval pin that needed a better window.** "The wave repeats every two hundred seconds"
first passed against a hundred-second mutation: the pin looked just before and just after 230,
and both cadences have a wave there. Checking at 140 — where a hundred-second interval has a
wave standing and a two-hundred-second one does not — is what tells them apart.

**Verification.** Full suite 1,499 passing and 1 skipped; six new pins; six mutations, all
caught.

### A sender with no listener: the fortress lords' despawn helpers

`fortress_instance_duke` is next on the shared-name list and is left alone, with the reasoning
recorded so it is not re-derived.

The three fortress lords each have two patterns — `BGuard_ChiefD_Minor` for the weakened form
and `BGuard_ChiefD_Tune405` for the enraged one — and on dying they place **six** copies of
296339 at three teleporter marks, plus 296338 at a barrier in the enraged case, for eighteen
seconds. Their display name is "dredgion elite fighter", which is why they read as fightable
adds; their **devnames say what they actually are**: `BAb1_DrakanDespawn_ByTeleporter` and
`BAb1_DrakanDespawn_ByBarrier`.

They are not fighters. Each one's whole pattern is a single line:

```
on_wake_up: broadcast_message message_type=10007 range_as_meter=100
```

The lord dies, a helper appears at each teleporter, and everything listening within a hundred
metres goes home. **Eleven patterns listen for 10006 and twelve for 10007** — all of them
`DrGuard_*_WarpH2` and `_WarpH3`, the drakan guards that warp into a fortress.

**And we have neither half.** Twelve NPCs bind those warp patterns and our spawn data places
none of them; nothing in our code listens for 10006 or 10007. Porting the helpers would put six
HERO-rated NPCs into every fortress-lord death for eighteen seconds to broadcast at nobody.

**This is a category, not a one-off.** The Yamennes gate listener was a *listener with no
sender*; this is the mirror. The shared-name audit will keep producing both, because a pattern
half is missing exactly when the other half was never ported either. The test to apply before
porting a broadcast: does anything on our side hear it, and does anything on our side send it?
If neither, the work to do first is the encounter that owns the other half — here, the warp
guards.

**What is left on the shared-name list**, with `dancing_flame`, `gravity_tornado`, `agrint` and
`unstableyamennes` now done and `fortress_instance_duke` recorded above:

| ai_name | what to check |
|---|---|
| `brigade_general_vasharti` | the hard mode's glove controllers — blocked twice over already |
| `captain_xasta` | 282444, on the fall-off variant |
| `eternal_bastion_dragon` | 284075, on two of the four dragons |
| `orissan_summon_helper` | 855702, 855703, 856306, 856309 |
| `tiamats_incarnation_spawn` | ten `_invisible` damage twins — scenery the other audit filters |
| `twin_protector` | 855626 |
| `yamenessportal` | 282016, the gate summon `YamennesSpawnGateAI` already spawns |

## Captain Xasta's second form, and the trap that is its clock

`captain_xasta` on the shared-name list, and the class said so itself: *"217310 binds to its own
pattern, and translating that is separate work from the first form."* That work turned out to be
one of the tidier chains in the whole dump, and it closes a loop rather than adding a branch.

His second form's entire fight, in `IDYun_Nmd3_FallOff`:

1. ten seconds in, **one** trap lands on a random attacker and engages that player with **ten
   million hate**, living thirteen seconds;
2. the trap broadcasts **200** to a hundred metres **as it despawns**;
3. that re-arms his timer at five seconds, and the next trap goes out.

**Nothing in his own branch re-arms the timer.** The cadence is not a constant anywhere — it is
thirteen seconds of trap plus five of waiting — and it only continues because the trap tells him
it is gone. Cut the broadcast and he drops one trap and never another, which is a caught mutation.

The trap (282444) was on the generic `trap` AI, which made it a trap. Its job is not to be a
trap; it is a clock.

### Two runtime pieces this needed

**`on_despawn` is now a real handler.** 361 of them across the 5.8 files, and until now the
excuse for not having one was that the pattern reset covers it — which is wrong in a way that
only shows when a branch touches state: `ResetPattern` *forgets* a spawn group, it does not clear
it. It is evaluated **before** the reset, so a branch still sees its timers, flags and groups.

The ordering was inert on its own — Xasta's trap only broadcasts — so
<see cref="YamennesSpawnGateAI"/> was moved onto it at the same time. Retail gives those gates the
same despawn line on `on_die` *and* `on_despawn`, and only the first was translated; a gate that
was removed rather than killed left its orkanimums standing for the rest of their seventy
seconds. Now both halves are there, and the evaluation order is a caught mutation instead of an
assertion.

**`spawn_on_target_by_attacker_indicator` learned `attack_target_after_spawn`** — the fourth and
last of retail's four placements to get it. The op is complete across the vocabulary now.

**Not translated:** the eight-cast "Trap Combo" Xasta runs on message 100, which the trap sends on
engaging. That is a real pairing and both halves exist — but every one of the eight is index-only,
so sending 100 would reach a listener with nothing to do. Recorded rather than wired, the same
sender-with-no-useful-listener shape as the fortress lords' despawn helpers, and it becomes worth
sending the day those indices resolve. His thirty-second self-cast is kept: it is ours, it is the
only thing making his second form do damage on a schedule, and nothing in the pattern contradicts
it.

**Verification.** Full suite 1,504 passing and 1 skipped; five new pins; eight mutations, all
caught.

## The twin protectors' hellfire fields, and a fourth way to hide an npc id

`twin_protector` on the shared-name list: four protectors on one class, two sides' patterns
naming different NPCs, and **neither side's hellfire field was ever placed**.

Every one of the four patterns opens the same way — a field on a fixed mark, cleared when the
protector falls:

| side | field | mark |
|---|---|---|
| lava (236225, 236227) | **cinderhorn ravager** 855626 | 530.5 / 212 |
| heatvent (236226, 236228) | **cinderspeak immolator** 855712 | 531.4 / 151 |

Both are HERO-rated NPCs rather than scenery, and the class already had the "Raging Hellfire"
cast that names the mechanic — without the thing the mechanic is about. The side is chosen by the
same parity the adds already used (the heatvent pair are even ids), which reading the four
patterns confirms rather than infers.

**And it cleared its spawns on despawning and on going home, but not on dying** — where retail
clears both groups. A killed protector left its adds and its field standing until they decayed.

### A fourth way an npc id can hide from the audit

The backlog did not move on the first measurement, for the fourth distinct reason in this work.
The others were: an id in a generated table, an id computed as `self + 1`, an id assigned to a
local. This one is an id **returned by a helper**:

```csharp
internal static int FieldFor(int protectorId) => protectorId % 2 == 0 ? Heatvent : Lava;
...
Spawn(FieldFor(GetNpcId()), x, y, z, 0)
```

Neither a literal in the call nor a local assigned one — the *result* of a method. The audit now
follows a helper that returns `int` and whose name sits where a spawn call's npc id goes, taking
ids named in its body directly or through this file's `const int` names.

It also picks up `GravityTornadoAI`'s crusher, written the same way and missed for the same
reason — which is why last entry's claim that the tornado's crusher "was never counted" was only
half right: it is a fightable add, and it was invisible rather than excluded.

**Known over-reach, accepted:** a helper that *tests* an id to choose between two others gives up
the tested id as well, so `CrusherFor` yields the tornado alongside its crushers. Those are owners
rather than adds and their instances spawn them anyway; separating the two would mean parsing the
expression rather than reading it.

**Where the count went.** 459 across 351 encounters → **453 across 345**. Six: the two fields, the
tornado's two crushers, and two more the same sweep uncovered.

**Verification.** Full suite 1,509 passing and 1 skipped; five new pins; six mutations, all caught.

## The same invention, on both generations of Yamennes gate

`yamenessportal` — the last actionable name on the shared list, and it turned out to be the
other half of a job already done.

There are **two generations** of Abyssal Reliquary spawn gate. The four Unstable Yamennes opens
(283203, 283222, 283223, 283233) were ported earlier. The other three — **282014, 282015,
282131** — were on `YamenessPortalSummonedAI`, a second class carrying the *same invention* the
first four had: two npcs at ±3 metres, twelve seconds in and once more at seventy-two.

Their patterns are the ported ones without the `_02` suffix. Identical clock, identical marks,
one difference: the pair they feed out is **281903 and 281904** where the newer gates feed 283200
and 283201. So they are three rows in the same table, and that class is gone.

Worth naming: **the invention was on two classes at once, and the first pass only found one of
them.** The shared-name audit is what connected them — not because the two classes disagreed, but
because the three older gates' patterns named NPCs their class never reached.

### Two more audit shapes, both found by the count moving the wrong way

Retiring the old class **raised** the backlog, twice over, and both were the audit rather than
the port:

- **A record row naming its id through a constant.** `new Feed(OldOrkanimum, …)` — the sweep that
  follows record tables read only literals, so retiring the class that had spawned 281903 and
  281904 *literally* put both straight back in. It now resolves this file's `const int` names at
  int positions, the same thing the id-returner sweep already did.
- The id-returner sweep from the previous entry, which is what made 855626 and 855712 visible.

Four ways an npc id can hide from a text sweep have now been met and closed: a generated table, a
computed `self + n`, a local assignment, a helper's return value — and now a constant inside a
record row. The through-line is the same each time: **the audit measures what the code says, and
code says the same thing five different ways.**

**Where the count went.** 453 across 345 encounters → **449 across 341**.

**What is left on the shared-name list.** Two, both already recorded and neither actionable:
`brigade_general_vasharti` (the hard mode's glove controllers — blocked on the waypoint trigger
*and* on the controllers having no controller AI) and `tiamats_incarnation_spawn` (ten
`_invisible` damage twins, which the missing-adds audit filters as scenery on purpose).

`orissan_summon_helper` is the one genuinely open case, and it is written up below.

### Researched, not ported: the Orissan crystals

The two icing crystals (855607, 855608) are **message-driven** in retail and spawn-driven here.
Each answers four messages with four different products:

| message | what the crystal puts out |
|---|---|
| 22729 broken Lv3 | 855699 |
| 22730 broken Lv2 | 855700 |
| 22735 spread Lv3 | 855702 (crystal 1) / 856309 (crystal 2) |
| 22736 spread Lv2 | 855703 (crystal 1) / 856306 (crystal 2) |
| 22737 Orissan dies | clears its group and itself |

Ours casts once on spawning and puts out 855699 or 855700 by crystal id — so the two *spread*
products, four NPCs, are unreachable.

**Both halves exist here**, unlike the fortress lords' despawn helpers: the senders are the
Orissan bosses' own patterns (`IDSeal_HalfWake_Lv2/3`, `IDSeal_FullWake_Lv2/3`), which bind
236230/236231/236233/236234 — all four on our `orissan` AI, all four placed by
`DrakenspireDepthsInstance`. What is missing is the **plumbing**: `OrissanAI` is a hand-written
Java-parity fight that never broadcasts, and the helper never listens.

So this is not an AI-class-sized job; it is translating four boss patterns and replacing a
working implementation of a complex fight. Recorded with the message numbers and the products so
whoever takes it does not have to re-derive them.

**Verification.** Full suite 1,512 passing and 1 skipped; three new pins; five mutations, all
caught.

## Ragnarok — a world boss that auto-attacked

With the shared-name list closed, back to `audit_missing_ai.py`, which reports **737 NPCs that
have a retail fight and no AI class at all**. Ranked by *spawn* actions rather than by timers —
timers are usually casts we cannot translate, spawns are the part we can — the top of that list
is Ragnarok: a **LEGENDARY** field raid boss in Gelkmaros, on a twenty-hour respawn, on plain
`aggressive`. He auto-attacked and did nothing else, and both NPCs his fight is made of were
reachable by nobody.

His Elyos counterpart `LF4_FieldRaid` has been ported for a while as `OmegaAI`. This is the other
side of the same content, and it had been sitting in a different audit the whole time.

**A five-rung ladder on a five-second heartbeat**, one-shot at each threshold, deepest first:

| rung | what arrives |
|---|---|
| below 85 | five parasites on the tank, one on each of up to twenty-five others |
| below 65 | the same, into its own spawn group |
| below 45 | the same, **and** a slime on up to five |
| below 35 | a slime on up to five |
| below 30 | a slime on up to five, again |
| below 25 | five parasites on the tank at **fifty** hate, one on each of up to twenty-five |

Everything lives five minutes and arrives already fighting whoever it landed on. The fifty at the
deepest rung is the one asymmetry in the whole pattern — every other spawn carries a hundred — and
it is kept.

**Two rungs that look like a copy-paste error are not one.** Below 35 and below 30 do exactly the
same thing into the same spawn group behind two different flag vars. That is retail giving the
slime step twice on the way down, and translating it as one step would halve it.

### A pin that could not see its own mutation

"The two slime rungs collapse into one" survived at first, and the reason is worth keeping. The
pin dropped him straight to 34% — but with the below-35 rung deleted, the **below-45** rung
matches there instead and it brings slime too, so the count was right for the wrong reason.

Walking the ladder one rung at a time — 44, then 34, then 29, checking the slime count grows by
four each time — is what isolates them. This is the third time in this work that a threshold pin
has had to walk rather than jump, and the reason is always the same: a `HpBelow` guard is true for
everything below it, so the rung you skipped past is still available to cover for the one you
deleted.

**Not translated.** Fourteen skill indices — the opening cast, three or four on most rungs, and
the whole of timer 1, which is eight health-banded branches that cast, re-arm and carry nothing
else. Timer 2 is armed at 145 seconds and **has no branch in the pattern at all**, which is
retail's own loose end rather than ours.

**Where the count went.** 449 across 341 encounters → **447 across 340**, and the missing-AI count
738 → 737.

**Verification.** Full suite 1,520 passing and 1 skipped; eight new pins; nine mutations, eight
caught and one that will not compile.

## Kingspin — the first ladder that is regimes rather than steps

Second off the missing-AI list ranked by spawns. An ELITE boss of Lower Udas Temple on plain
`aggressive`, no AI class, and the one NPC his fight is made of — the **web** (281391) —
reachable by nobody.

He opens by throwing a web on each of up to three players and **four more behind himself**, at
fixed offsets two metres up: (-15, 0), (-15, -5), (-5, -15), (0, -15). Those four last six seconds
where everything he throws on a player lasts eight or thirty. They are the only thing in the
pattern placed relative to the boss.

**Then a health ladder with no flag vars anywhere on it.** Every threshold pattern translated
before this one guards its branches with `set_flag_var`, which makes them steps that fire once.
These do not: while he is below the threshold the branch fires **every eight seconds**, for as
long as the fight lasts. The distinction is the whole shape of the fight, and it is written in the
pattern by the absence of a line.

| rung | what happens |
|---|---|
| below 86 | casts only |
| below 71 | a web on each of the **four most-hated** |
| below 51 | a web on each of the **five least-hated** |
| below 36 | casts only |

**The ordering flips, and that is the mechanic.** At 71 he webs the top of his hate list — the
tanks. At 51 he webs the bottom of it, which is the healers and the ranged. Retail says
`ORDERI_DESCENDING` and then `ORDERI_ASCENDING`; getting it backwards would invert who the fight
is hard on, and both are caught mutations.

A second timer throws four more on random targets every eighteen seconds from twelve, regardless
of health.

### Two pins that had to be measured rather than reasoned

**The two timers overlap**, so a count at an arbitrary second means nothing. Tracing the fight
second by second gave the windows where it does — every web after the opening lasts eight seconds,
so 20-29 and 38-47 are empty of the second timer's contribution — and the pins use those.

**And the top rung has to be measured where it matches.** "Above eighty-six the ladder throws
nothing" passed against a mutation that made the top rung throw webs, because *above* eighty-six
no rung matches at all and the mistake was invisible. Measuring at **eighty** — inside the top
rung's band, outside the next one's — is what makes it a test of that rung.

**Where the count went.** 447 across 340 encounters → **446 across 339**; missing-AI 737 → 736.

**Still owed on this instance.** Three more Lower Udas Temple bosses are in the same state, each
with one unreachable add: Bergrisar (`IDTP_Keeper1`, whose five blood wheels are all walk-path
blocked, so only its on-death controllers are portable), Anvilface (`IDTP_NepEx1`, add 281424) and
the Nepilim boss (`IDTP_NepBoss1`, add 281421).

**Verification.** Full suite 1,527 passing and 1 skipped; seven new pins; eight mutations, seven
caught and one that will not compile.

## Two more of Lower Udas Temple, and a summon table that was already right

**Anvilface** (`IDTP_NepEx1`) was on plain `aggressive` with the one NPC his fight is made of —
*shatter* (281424) — reachable by nobody. Two one-shot calls, at fifty percent and again at
thirty, and **both go to the third-most-hated**. Not the tank, not a random player: third, both
times, which in a party is the second damage dealer or a healer who has been working. They hang
off `on_attacked`, so they land on the blow that crosses the threshold rather than on the next
tick after it.

**Debilkarim the Maker** (`IDTP_NepBoss1`) is the more interesting half, because **his summon
table was already correct**. Somebody had matched the seven `protection of aion` and their four
rings — two at five metres, two at ten, two at fifteen, one at twenty — to the pattern exactly.

What a percentage table cannot express is the rest of his fight: below nineteen percent, **one hit
in ten**, three *pyre souls* on whoever he is fighting. A `<percentage>` row has no way to say
"sometimes", so that NPC was reachable by nothing. Moving him onto the pattern runtime keeps the
ring and adds the roll; the now-dead table row is removed.

**Two pins that passed for the wrong reason**, both familiar shapes:

- *"The shatter arrives already fighting"* asserted its target. The shatter is `aggressive` and
  lands on top of the player, so it engages by itself within the tick — the target reads the same
  whether or not the flag is honoured. The **hate** is the fingerprint: natural aggression is one
  point, retail's `hatepoints_to_add` of one goes on top, so two.
- *"The pyre souls come in threes"* checked `souls % 3 == 0` over four hundred hits — which is
  just as true when every hit calls them. An upper bound turns it into a test of the roll: one in
  ten of four hundred is about a hundred and twenty souls, and every hit calling would be twelve
  hundred.

**A note on the probabilistic branch.** The ten percent cannot be pinned deterministically — the
limitation recorded against the Conquest rotation's shugo odds still stands — but it can be
bounded from both sides, and that is enough to catch both the "always" and the "never" mutations.
Worth remembering the next time a `test_probability` shows up: bounding beats sampling.

**Still not translated on either boss:** the invisible controllers they drop on dying. Each is one
line — broadcast **6956** to fifty metres and remove itself — and the four patterns that listen for
it (`IDTP_Keeper2`, `IDTP_NepBoss2`, `IDTP_NepBoss3`, `IDTP_NepEx2`) are all untranslated. A sender
with no listener, so it waits for those four.

**Where the count went.** 446 across 339 encounters → **445 across 338**; missing-AI 736 → 735.

**Verification.** Full suite 1,535 passing and 1 skipped; eight new pins; nine mutations, eight
caught and one that will not compile.

## Closing the temple's clear-up: a sender and its four listeners, together

Last entry recorded the invisible controllers Lower Udas Temple's bosses drop as a **sender with
no listener** and left them out. This closes it, because the listeners turned out to be four
patterns whose only translatable branch is the one that answers.

The chain is three pieces and each is one line:

1. a boss dies and drops **five** controllers (281418) — one at its feet, four scattered to
   twenty-five metres;
2. each controller **broadcasts 6956 to fifty metres and removes itself**;
3. every add the boss called answers 6956 with **`despawn_self`**.

Ten NPCs across four patterns do step three — the punishment chakras, the protection of aion, the
pyre souls and the shatters — and *that branch is identical in all four*. What is not identical is
everything else they do, and none of it is translatable: the chakras walk a route we do not have,
the nuclei and pyre souls answer 6955 with a cast, the shatters run a fourteen-second cast loop,
and a pyre soul has a one-in-two chance of casting and vanishing when hit. Sharing one class for
the despawn is not a claim that the four patterns are the same, only that this branch of them is.

**Why five controllers for a fifty-metre broadcast.** Because the room is bigger than fifty
metres. One lands on the boss and four scatter to twenty-five, so the union covers what a single
broadcast could not — which is also why the pin's raid had to be moved closer together: at the old
spacing the furthest add sat sixty metres from the boss and whether it heard anything depended on
where the scattered four happened to land.

### A mutation that is inert because both halves are ours

Changing the message number from 6956 to 6955 survives every pin: sender and listener share the
same constant, so the value cancels out. It is not a gap in the pins — it is that the number only
matters for talking to NPCs *outside* this pair, and there are none translated yet. It will start
mattering the moment Bergrisar is: **he broadcasts 6955 on entering attack**, and a temple wired
to the wrong number would have him clear the room as he pulls.

**Where the counts went.** Neither audit moves: the controllers are `name_id`-blank scenery the
missing-adds audit filters, and the ten adds were already reachable through their bosses. What
moved is that killing a boss now takes its adds with it, which is the mechanic.

**Still owed in this instance.** Bergrisar (`IDTP_Keeper1`) is the last of the four bosses without
a class. His five blood wheels are all walk-path blocked, so what he has left to give is the same
on-die clear-up — worth doing when somebody is next in the file, and now that the chain exists it
is four lines rather than a research problem.

**Verification.** Full suite 1,538 passing and 1 skipped; three new pins; six mutations, four
caught, one that will not compile, and the inert one written up above.

## Bergrisar, and hard-mode Shadowshift

**Bergrisar** is the last of Lower Udas Temple's four bosses, and the one with the least to give.
Almost all of his pattern is blocked: a punishment chakra on entering the fight and five more at
80, 60, 40, 20 and 10 percent, each onto its own absolute mark by the gate — and every one of the
six carries a `pathname`, `Path_IDTemple_Low_AI01_1` through `_6`. A chakra is a thing that rolls
at you; left standing on its mark it is a different encounter rather than a partial one, which is
the call this work has made for the walk-path bucket since it was measured.

So what he gets is his `on_die`: the five clear controllers the previous entry wired up. That is
worth having on its own — **he is the boss whose chakras the clear-up exists to remove.**

Also not translated, and worth stating apart from the blocked half: he broadcasts **6955** on
entering the fight, which is the number the temple's nuclei and pyre souls answer with a cast. His
half of that pairing is here and theirs is not, so it stays out until those indices resolve — and
it is exactly the collision the previous entry predicted, which is why the clear-up uses 6956 and
not 6955.

### Hard-mode Shadowshift: the same fight with every number moved

216166 was on plain `aggressive` with no class, and its pattern is `IDCT_Boss_Shadow` re-tuned
rather than rewritten:

| | normal | hard |
|---|---|---|
| near pair, re-arm | 25s | **20s** |
| near pair, order | random | **the two most-hated** |
| near pair, range | 3m | **2m** |
| far spectre, re-arm | 4s | **10s** |
| far spectre, range | 10m | **6m** |

So the near pair is faster and *aimed*, and the far one is much slower but lands closer. Reading
the two side by side is the only way to see that the **ordering** changes as well as the clock:
normal mode scatters its near pair at random, hard mode puts them on whoever is holding it. One
class, two rows of a tuning record.

**A pin that had to be a table read.** Hard mode's timings are only observable in the harness for
about eleven seconds: the near spectre is *black essence*, which starts casting into the stand-in
player shortly after it lands and takes the effect engine down with it — and deleting the spectre
does not cancel the cast it has already scheduled. So the tuning table is `internal` and pinned
directly, the same answer the gravity tornado needed.

**The residual gap, stated.** Pinning the decision leaves the wiring between table and pattern
half-covered: the *ordering* is exercised by a real observation, the two re-arm delays are not, and
a mutation that reads the tuning and then ignores it survives. It cannot be closed without a
harness that can run this fight past eleven seconds.

**Where the counts went.** Missing-AI 735 → **733**; the adds backlog is unchanged, since both
bosses' adds were already reachable through their normal-mode twins.

**Verification.** Full suite 1,542 passing and 1 skipped; five new pins; seven mutations, six
caught and the one above recorded.

## Flarestorm — the first ladder that runs the other way

A HERO Catacombs boss on plain `aggressive` with no AI class. His ladder is four one-shot rungs on
`on_attacked`, and the waves grow as he is worn down: **three** calamities at eighty percent, four
at sixty, five at forty, six at twenty, each on the most-hated of that many players.

**And his priorities descend with depth.** Every threshold pattern translated before this writes
its branches deepest-first, so a boss burned down quickly skips to the rung it deserves.
Flarestorm's `p4` guards 80, `p3` guards 60, `p2` guards 40, `p1` guards 20 — so the *shallowest
unconsumed* rung is always the one that fires.

The consequence is worth spelling out, because it inverts what deepest-first buys the raid
everywhere else. A group that drops him from full health to ten percent in one burst **does not
get the twenty-percent wave**. It gets the eighty-percent one on the next hit, the sixty on the
one after, and so on: he works *up* the ladder a hit at a time, so every wave lands however fast he
dies. Three, then four, then five, then six, on four consecutive blows.

### An ordering pin that took three attempts

"The wave goes to the least-hated" survived twice before it died, and both reasons are worth
keeping because they are about how a pin is *set up* rather than what it asserts:

1. **Everyone at equal hate.** The harness's `Rehate` gives every player the same number, so
   `ORDERI_DESCENDING` and `ORDERI_ASCENDING` pick the same set and the ordering is unobservable.
2. **Half the raid outside `valid_distance`.** Fixing the first by spreading six players fifteen
   metres apart put the back three more than fifty metres from the boss — outside the op's own
   range filter — so both orderings again picked the same front three.

Four players twelve metres apart, at descending hate, all inside fifty metres: the sets differ and
the mutation dies. **A multi-target ordering can only be pinned when the eligible set is larger
than the cap** — which means checking the raid's spread against `valid_distance`, not just against
the spawn scatter.

**Not translated.** Three skill indices across three timers, and the `on_attacked` branch above the
ladder that reads `is_user_class` and adds a hate point — we have vocabulary for neither half of
that: not the class test, and not a bare hate bump.

**Where the count went.** Missing-AI 733 → **732**; the adds backlog is unchanged, because the
calamity is already placed by Beshmundir Temple's spawn file. This is a boss that had no fight
rather than an add nobody could reach.

**Verification.** Full suite 1,548 passing and 1 skipped; six new pins; five mutations, all caught.

## Three ND2 named bosses, and a summon branch with no health guard

Exedil, Ulan and RM-13b — three named bosses with no AI class at all, from the same `ND2_*`
family as Frostmane Lestin. All three share a shape that had not appeared before: **summon
branches carrying no health guard**, ordered by priority and a flag var each, so they fire as a
*sequence* rather than as a ladder. The first heartbeat runs the highest-priority unconsumed
branch, the second runs the next, and each fires once.

| boss | what it calls |
|---|---|
| **Exedil** | two `PrSum2` ghosts at seven metres, then two `PrSum1` at six, both for twenty minutes — and below 25%, two more `PrSum2` at six metres with **no lifetime at all** |
| **Ulan** | three ghosts at ten metres for **forty** minutes, then three others at ten for **ten** |
| **RM-13b** | two pretorians at five metres, and below 30% three more, both lasting a minute |

**Exedil's deep rung stops his clock, and it is retail's own doing.** It is the only one of his
three branches that does not re-arm timer 0. So a boss taken below twenty-five percent before his
first heartbeat calls two permanent ghosts and then *never summons again* — the two twenty-minute
pairs are skipped entirely. Reproduced rather than tidied, and pinned: the branch that stops the
clock is as much part of the fight as the ones that keep it running.

**Ulan's asymmetry is the kind a port flattens without noticing:** his two steps are identical
except that one pair stays forty minutes and the other ten. A single `GhostLife` constant would
have read perfectly well and been wrong.

### A harness limitation, worked around rather than papered over

Exedil's ghosts are `servant` NPCs that cast at whoever is in reach, and a cast into the harness's
stand-in player takes the effect engine down — the same limitation that bounds the Shadowshift
pins to eleven seconds. Here it was avoidable: the pin stands the player sixty metres back, out of
the ghosts' reach, and the summoning is then observable for as long as it needs. Worth recording as
a technique, because it is cheaper than the alternatives when what is being pinned is *what the
boss spawns* rather than what the adds do.

**Where the counts went.** Missing-AI 732 → **729**; adds backlog 445 across 338 → **443 across
337**.

**Not translated** on any of the three: nineteen skill indices between them, on timers 1 through 6,
none of which carries a spawn.

**Verification.** Full suite 1,554 passing and 1 skipped; six new pins; nine mutations, all caught.

## The last four spawn-bearing bosses with no AI at all

After the ND2 three, the missing-AI list has **four** entries left that carry a spawn and no
handler — everything else on it is cast-only, which is a different problem. All four are done here.

Three of them are one line: **something is left behind when a player kills them.**

| boss | leaves | for |
|---|---|---|
| Menotios (251001, LEGENDARY) | an aetherback titan core | 20s |
| RM-78c (212211) | a strange creature | 120s |
| RA-45c (213764) | a strange object | 120s |

The fourth, **Takahan** (216884), is a trap loop: the first explosive trap lands on his quarry at
**twenty-five** seconds and then every **six**. Slow, then relentless — and a single interval would
have been wrong in both directions.

**On `on_killed_by_user`, not `on_die`.** Retail distinguishes the two and all three drops use the
player-kill form, so nothing is left when one of these dies to something else. Our runtime raises
one death event, which is as close as we get; the difference only shows for an NPC killed by
another NPC, and none of the three is anywhere that happens.

### The `live_time` a boss gives an add is a ceiling, not a duration

A pin tried to check Menotios' twenty seconds by survival and failed at eighteen — because **every
one of these three adds ends itself sooner than its boss allows**:

- the titan core and Takahan's trap are `ntrap`, whose pattern is "cast once, then `despawn_self`",
  so twenty seconds is a ceiling the trap never reaches. That is retail's own design, not a
  divergence: the trap's own pattern outlives nothing.
- the strange creature deletes itself after **six and a half seconds** against retail's **hundred
  and twenty**. That one *is* a divergence, and it belongs to `StrangeCreatureAI` — a Java-parity
  class with its own hardcoded clock — rather than to the boss that drops it. Recorded here so it
  is findable; not changed, because it is somebody else's encounter.

The general shape is worth keeping: when a boss's `live_time` cannot be observed, check whether the
add removes itself first before assuming the port is wrong.

**Where the counts went.** Missing-AI 729 → **725**; adds backlog 443 across 337 → **431 across
325**. The adds figure moved by twelve rather than four because these four bosses' patterns name
adds that other bosses' patterns also name — reaching one reached several.

**What is left on the missing-AI list**, and it is now a single category: **725 NPCs whose patterns
are cast-only**. Every one of them is blocked on the same thing — `SKILLI_INDEX` resolution — and
no amount of spawn-side work will move them. That is the next real lever on this audit, and it is a
research problem rather than a porting one.

**Verification.** Full suite 1,562 passing and 1 skipped; eight new pins; seven mutations, all
caught.

## Dark Poeta's barricades called their guards to the wrong barricade

The missing-AI and missing-adds audits are both down to blocked or cast-only remainders, so this
one came from a different tool: `audit_hp_phases.py`, which compares a hand-written `HpPhases`
ladder against the thresholds its retail pattern actually states. Eighteen classes disagree. Two of
them are not timer-driven rotations, which means retail's numbers are directly copyable rather than
being one strand of a larger clock — and the first of those is `BalaurBarricadeAI`.

The binding table gives all three of Dark Poeta's barricades a pattern of their own:

| barricade | retail pattern |
|---|---|
| 700517 `IDLF1_Barricade_Dragon` | `ND2_H50_3` |
| 700556 `IDLF1_Barricade_DragonB` | `ND2_H50_4` |
| 700558 `IDLF1_Barricade_DragonC` | `ND2_KnQ` |

Three things were wrong, and only the third is the one the audit was looking for.

### The two barricades' postings are transposed

Every one of the six guard positions is written out as `SPAWN_LOCATION_ABSOLUTE` in the pattern, so
there is nothing to derive. Laid side by side:

| | aionemu spawns at | retail posts at |
|---|---|---|
| 700517 | (282.29, 1003.04), (289.50, 1000.16) | **(315, 982), (308, 990)** |
| 700556 | (315.84, 982.89), (309.10, 989.51) | **(290.71, 1002.67), (284.28, 1004.98)** |
| 700558 | (199.75, 843.69), (201.98, 853.49) | (202, 856), (201, 843) |

700558's are right to within a metre. The other two hold **each other's** coordinates — aionemu's
700517 positions sit in `ND2_H50_4`'s neighbourhood and its 700556 positions are `ND2_H50_3`'s, to
within the same metre. The two barricades stand about thirty metres apart, so on our server two of
the three called their reinforcements to the far side of the room.

This is what an observed port looks like when the observation was right and the label was not: the
positions were recorded accurately and then filed under the wrong barricade.

### The guards were the wrong templates

Retail summons a dedicated trio, and aionemu used the ones already standing in Dark Poeta:

| role | retail | aionemu |
|---|---|---|
| fighter | 215452 `IDLF1_G_FeB_DrakanFighterSum_50_Ae` | 215262 |
| knight | 215453 `IDLF1_G_KeA_DrakanKnSum_50_Ae` | 215263 |
| wizard | 215451 `IDLF1_G_DrakanWizardSum_50_Ae` | 214883 |

The names on screen match pair for pair — anuhart proconsul, praefectus, magist — which is exactly
why watching the fight picks the wrong one. Only the `name_id` differs, and no player sees that.
Worth generalising: **a summoned add and its world twin are usually separate templates**, and the
name is not evidence.

### It is not a health ladder at all

This is the part the audit flagged: ours reads `HpPhases(50, 10)` and spawns two guards at each.
Retail has one threshold, at **seventy**, and reaches it on a clock:

- `on_enter_attack_state` arms battle timer 0 at **six seconds**;
- the timer branch guarded on HP below 70 (with a flag var, so once) calls **two fighters** — and
  is the one branch that **does not re-arm the timer**;
- the lower-priority branch re-arms at six seconds and does nothing else;
- the death branch leaves the **knight and the wizard**.

So the poll exists only to watch for the crossing, and it stops itself once it has done its job.
Two consequences an `HpPhases` port cannot produce, both now pinned:

- crossing seventy does **not** summon on the crossing hit — the fighters arrive at the next
  six-second tick, up to six seconds later;
- a barricade killed **inside six seconds** never calls its fighters at all, and leaves only the
  death pair. Four guards in a slow fight, two in a fast one.

All four guards carry `live_time=300`, and unlike the death drops in the previous entry this is
observable: the three summoned templates are plain `aggressive` with no pattern of their own, so
nothing removes them earlier.

### Headings are degrees, and ours are not

Retail writes `dir` in degrees (141, 324, 66, 225…); our positions take the client's 0..120. The
conversion is `PositionUtil.ConvertAngleToHeading`, i.e. degrees / 3. A raw copy overflows `sbyte`
past a full turn and leaves guards facing backwards — pinned, because the two are the same kind of
number and nothing about the data says which.

### Not translated: the broadcast

All three barricades `broadcast_message 3409` to ten metres naming whoever they are fighting, and
retail's `XDrakan` pattern answers it — an idle drakan takes hate and attacks, one already fighting
switches target. Dark Poeta's barricades stand in drakan camps, so **a barricade pulls its
neighbours onto you**. Nothing on our side listens for 3409, so this is a sender with no listener
and shipping it would be a no-op; recorded here instead. Reaching it means porting `XDrakan` for
the camp drakan, which is a much larger job than the barricades.

### One death event where retail has two

700517 spawns its pair on `on_die`; 700556 and 700558 use `on_killed_by_user`, so retail leaves
nothing when something other than a player finishes them. Our runtime raises a single death event
and all three behave as 700517 does. Nothing in Dark Poeta kills a barricade except a player.

### What the rest of the HP-phase audit says

Eighteen classes disagree with their pattern. Thirteen are **timer-driven rotations** — Tahabata
has 36 battle-timer branches, Hyperion 141, Sematariux 172 — where the retail "phase list" is only
the HP guards on a much larger clock, so renumbering our ladder to match would be worse than
leaving it: it would claim a fidelity the class does not have. Those need the whole pattern
translated or nothing.

Five are **regime-guarded**: retail has no threshold list at all, only HP *bands* that gate a
running rotation. `ShugoTombImperialObeliskAI` is the tractable one of the five — retail bands
(30–69) and (0–29) against our (70, 35), and **zero** timer branches — and is the next candidate
from this audit. `CursedQueenModorAI`, `DaliaCharlandsAI`, `EmpyreanLordAI` and `IsbariyaTheResoluteAI`
each carry 14–84 timer branches and belong with the thirteen.

The one remaining non-rotation mismatch is `WatchmanHokuruki` (235634, `IDSweep_Monster_Nmd03`):
ours reads (100, 75, 50, 25, 15), retail spawns bears at **50** and **25** only, two then three,
`spawn_range=8` from its own position, with the ladder's higher-priority rungs at 80/60/30 spending
their turn on a `set_condition_spawn_variable` we cannot express. That last part is why it was not
done here: the ordering consequence is real — the first hit below 80 spends on the condition
variable and the bears come on the following hit — and reproducing the bears without it would be a
different fight, not a closer one.

**Verification.** Full suite 1,578 passing and 1 skipped; sixteen new pins; ten mutations, all
caught — including the transposition put back exactly as aionemu has it.

## Watchman Hokuruki was summoning the room he stands in

The second non-rotation mismatch from `audit_hp_phases.py`, and the larger of the two: our
(100, 75, 50, 25, 15) against retail's two summoning rungs at 50 and 25. Reading the pattern to check
the thresholds turned up something the audit could not see — **the adds are wrong as well.**

### He summons one template, and aionemu had him summoning three

`IDSweep_Monster_Nmd03` names exactly one: `IDSweep_S1_Mosbear_65_An`, the **tamed mosbear**
(235632). aionemu already used it for two of its five phases, and for the other three it called an
intruder marksman (236083) and two intruder snipers (235649) at hand-placed coordinates, one of three
position sets shuffled per fight.

**No retail pattern spawns either gunner.** They are stage one's own room population — a sweep of the
whole 5.8 dump finds them named by nothing. Their real connection to Hokuruki is the opposite of a
summon, and is below.

| | retail | aionemu |
|---|---|---|
| entering combat | 4 mosbears, scattered within 5m of him | 4 mosbears, 3–5m (matches) |
| below 75 | — | marksman + 2 snipers at a fixed spot |
| below 50 | **2 mosbears**, within 8m | marksman + 2 snipers at a fixed spot |
| below 25 | **3 mosbears**, within 8m | marksman + 2 snipers at a fixed spot |
| below 15 | — | 4 mosbears |

There are no coordinates anywhere in the pattern: every wave is a `spawn_range` scatter from his own
position. So the nine hand-placed positions and their per-fight shuffle were an approximation of a
random spawn — a reasonable thing to build from watching, and not what the fight is.

Retail gives no `hatepoints_to_add` to any of the three waves either, so the single hate point
aionemu put on the most-hated is gone with them. The mosbears are aggressive and find their own way
in.

### Rungs we cannot perform still have to cost a swing

Retail's `on_attacked` chain is five rungs, and the two that summon are the **bottom** two:

| priority | guard | action |
|---|---|---|
| 9 | HP < 30 | `set_condition_spawn_variable 2STAGE_ING` |
| 7 | HP < 60 | `set_condition_spawn_variable 2STAGE_ING` |
| 6 | HP < 80 | `set_condition_spawn_variable 2STAGE_ING` |
| 5 | HP < 50 | shout, **2 mosbears** |
| 5 | HP < 25 | shout, **3 mosbears** |

Every rung carries its own flag var, so each fires once. We cannot express
`set_condition_spawn_variable` — it drives the instance's stage progression, not the fight — but the
three rungs are **kept anyway**, because these are first-match-wins chains and a rung that matches
consumes the swing whether or not we can perform what it does.

The consequence is measurable and pinned. Below fifty with nothing spent, retail spends one swing on
the sixty rung, one on the eighty rung, and calls the bears on the **third**. Dropping the three
rungs as unportable would have brought every wave several swings early — a plausible-looking port
that is a different fight.

**Worth generalising: an unportable action is not an unportable branch.** In a first-match-wins
chain the branch's position is behaviour in its own right, and translating it as an empty rung is
more faithful than omitting it.

### Death clears stage one, and that is where the gunners belong

`on_killed_by_user` broadcasts **140505** to a hundred metres. Three retail patterns answer it, and
all three answer identically — `despawn_self`:

| pattern | templates |
|---|---|
| `IDSweep_Monster_02` | 235632, 235682 |
| `IDSweep_S1_Monster` | 235629, 235630, 235631, 235641, 235649, 235652, 235653 |
| `IDSweep_S1_Shulack_Gu_01` | 235633, 236083 |

Eleven templates, including both gunners aionemu had him summoning and the mosbears he really calls.
So the gunners *are* part of his fight — as things that leave when he falls, not things he brings.
Both halves are shipped together, which is what the sender/listener rule asks for: `IDSweepStageAddAI`
extends `IDSweep_Shugos` rather than replacing it, so the instance-progression check on spawn and the
damage variance every Vault NPC shares are untouched and this only adds the listener.

### Not translated

The ten-second cast loop on battle timer 0 (two `SKILLI_INDEX`); the three `say_to_all` lines, which
have no rows in our `npc_shouts.xml` at all; every `set_condition_spawn_variable` — on the ladder, on
entering combat, and on both death branches; `despawn_at_attack_state` on the bear spawns; and the
seven-second cast loops the mosbears and marksmen run.

**One retail branch is unreachable for us.** A second `on_enter_attack_state` rung sits below the
opening wave and sets the same flag var the fifty-percent bears are gated on — so a retail Hokuruki
that resets and is re-engaged loses that wave for good. Our convention is that a boss which resets
replays its steps, which clears the flag that rung would have consumed, so it can never fire.
Recorded rather than modelled: changing the reset convention for one branch would be a worse trade
than losing it.

### A harness trap worth naming: `Rehate` swings

`BossAiHarness.Rehate` adds hate, and **adding hate raises an Attack event**. Harmless for a test
that advances a clock, and quietly fatal for one that counts swings: a `Rehate` plus an explicit
Attack is two swings per call, so the first rung-counting pin here read half the ladder it thought it
did and failed by exactly one rung. Count swings with a bare `OnCreatureEvent(Attack, …)` and let
`Engage`'s hate hold the fight open. Noted on the method.

### And a blind spot in `audit_ai_messages.py`

The audit reported 140505 as a broadcast with no listener while the listener sat in the same file.
It recognised hand-rolled listeners only through `case <token>:` inside `OnNpcMessage`, and a
listener for a *single* message is naturally written as a comparison rather than a switch. Fixed
there rather than by reshaping the class into a one-arm switch: bending code to suit a check leaves
the next single-message listener silently unpaired, which is the exact failure this audit exists to
prevent. It now reads `messageType == X` in either order.

**Verification.** Full suite 1,588 passing and 1 skipped; ten new pins; eleven mutations, all
caught — including aionemu's five-phase ladder put back, and the three stage-counter rungs both
dropped and demoted below the summoning pair. `audit_ai_messages.py` pairs 140505 and is otherwise
unchanged at eight pre-existing unpaired messages.

## Four bosses shipped with their health guards dropped, and a new audit for it

This one is a correction to work in this log rather than to aionemu. Chasing an unrelated finding in
`audit_retail_messages.py` meant re-reading `ND2_PhA` in full, and the pattern did not say what the
class translated from it says. Then neither did two of its siblings, nor a fourth boss done in a
later pass.

### What the mistake is

Retail writes a summoning ladder as battle-timer branches guarded by `is_hp_in_boundary` — a **band**,
not a threshold — with a bottom branch whose only condition is the timer and whose only action is
re-arming it. Read quickly, the bands are easy to miss, and what is left looks like an unguarded
sequence ordered by priority alone. It runs. It summons. It is a different fight:

- **waves arrive at full health** instead of when the raid has pushed the boss into their band;
- **in the reverse order**, because priority descends as the bands get shallower;
- **a band the raid jumps over still fires**, where retail skips it for good;
- and the bottom branch, dropped as "a re-arm that does nothing", turns out to be the only thing
  keeping the clock alive between bands.

The last of those is the one that would have been hardest to find in play: with the bands restored
but no fallback, the first heartbeat matches nothing and the boss never summons at all.

### What was wrong

| class | pattern | what was dropped |
|---|---|---|
| `ExedilAI` | `ND2_PhA` | bands 26–55 and 56–80, the hand-over despawn, the fallback |
| `UlanAI` | `ND2_WhB` | bands 36–60 and 61–80, the hand-over despawn, the fallback, the 81–100 rung's seven-second re-arm |
| `Rm13bAI` | `ND2_AhD` | band 31–75, the fallback |
| `TakahanAI` | `Dread02_SurkanaNm06` | band 36–70 **and the flag var** |

Takahan is the worst of the four. His trap branch carries both a band and a test-and-set, so retail
lays **one** trap, once, and only while he is between 36 and 70 percent. The shipped class laid one
every six seconds for the rest of the fight.

Exedil and Ulan both gained something the earlier reading could not have produced: **a hand-over**.
The middle band despawns `SPAWN_ID_1` before it spawns into `SPAWN_ID_2`, so a raid never faces both
twenty-minute pairs at once — the first is taken away as the second arrives. And Ulan's deepest rung
summons *nothing* while sitting above both summoning rungs and not re-arming the clock, so a raid that
takes him under thirty-five quickly gets **fewer** adds, not more.

Two smaller things fell out of reading the patterns properly:

- Ulan's 81–100 rung is kept although its casts are not, because it re-arms at seven seconds where the
  fallback re-arms at six. Exedil's equivalent rung re-arms at the same six seconds as its fallback
  and is dropped. **A branch earns its place by changing what happens** — the same test that kept
  Hokuruki's stage-counter rungs.
- Takahan's below-35 rung is written **twice** in retail, at priority 10 without a flag var and at 9
  with one. Ten always matches first, so nine can never run: a dead branch in the shipped data.
  Collapsed to one rather than reproduced as a pair.

Retail's bands leave gaps — at exactly 25 for Exedil, 35 for Ulan, 30 for Rm13b — where no rung
matches and only the fallback runs. Preserved rather than closed; widening a band to tidy the seam
would move a threshold.

### The pins were wrong too, and are rewritten rather than relaxed

Five of the six ND2 pins failed against the corrected classes, because they had been written to
agree with the translation instead of with the pattern. That is the worse failure of the two: a
mistranslation with a passing pin reads as verified. They are replaced with pins that state the
bands, the hand-over, the skipping and the fallback, and the fallback pins in particular have to walk
the boss *into* a band from above rather than starting inside one — starting inside a band passes
whether or not the clock survives.

### `audit_pattern_guards.py`

New, and it exists because of this. For every `PatternAi` class bound to a retail pattern it reports:

- a retail branch that **spawns or despawns**, guarded by a band the class has no `When.HpBetween`
  for — cast-only branches are ignored, since this work does not translate casts it cannot map;
- a pattern with a bare-timer fallback branch where the class has none.

Run against the pre-fix classes it names all four bosses and both faults, which is the check that
matters for a tool written after the fact. Guards are scanned **per class, not per file**: the ND2
trio share one file, and a file-wide scan let one boss's `HpBelow` answer for another's missing band —
under-reporting Exedil by one band on the first run.

**Sixteen findings remain**, and they are the next work from this audit:

| class | what is unaccounted for |
|---|---|
| `TiamatDyingRotationAI` | five bands and the fallback |
| `UdasTempleBossesAI` (bergrisar) | bands 11–20, 21–40, 41–60, 61–80 |
| `GuardReinforcementAI` (both names) | four bands across two patterns, and the fallback |
| `GatekeeperFloxAI` | bands 0–25 and 51–75 |
| `GelkmarosPadmarashkaAI` | band 61–90 — already known, part of `DF4_Dramata`'s untranslated half |
| nine others | fallback only |

The fallback-only findings need triage rather than a fix: a class with no battle timers at all cannot
want a fallback, and the check cannot tell that apart from one that dropped it. The band findings are
real work.

**Verification.** Full suite 1,601 passing and 1 skipped; twenty-seven pins across the two files, up
from fourteen; fourteen mutations, all caught — including both shipped bugs put back exactly as they
were. Three mutations survived the first sweep (Exedil's fallback, Ulan's stopping rung, Takahan's
hand-off) and each needed a pin that walks the fight through the rung rather than starting past it.

## Triaging `audit_pattern_guards.py` down from sixteen findings to two

The audit written in the previous entry reported sixteen classes. Working through them one at a time,
**fourteen were false positives** — and every one was a distinct blind spot rather than the same one
fourteen times. A check nobody trusts is worse than no check, so the exclusions are recorded here as
carefully as the findings were.

| what fired wrongly | why | fixed by |
|---|---|---|
| `GuardReinforcementAI`, `TiamatDyingRotationAI` bands | both build guards from generated tables, so the source reads `When.HpBetween(band.Low, band.High)` and no literal exists to match | skip a class whose guards are non-literal |
| `GuardReinforcementAI` again | its whole builder lives in an `internal static class` **above** the first `[AIName]`, so per-class scoping put it in no class at all | read a class as its own body **plus the file preamble**, never a sibling's body |
| `TahabataPyrelordAI`, `AsaratuBloodshadeAI` fallback | both had written the rung, through a local `Step(...)` helper — with a comment about this exact hazard beside it | find the rung by its guard array alone, not by what follows it |
| `PrectazAI` fallback | matched his three-second `broadcast_message` heartbeat: a branch that re-arms a timer **and announces something** | a real fallback does *nothing* but re-arm |
| `PrincessKaremiwenAI` fallback | she translates only her minute-long timer 8; retail's fallback sits on timer 0, a chain she does not run | record the fallback per **timer slot** and only report a slot the class reads |
| `DarkPoetaCalindiFlamelordAI`, `DeathDropBossAI`, `AbyssGuardSimpleAI` and others | knock-on from the above | — |

**The regression check is the one that matters for a tool written after the fact.** Run against the
four pre-fix classes from the previous entry it still names every one, and now reports Exedil's
*both* missing bands where the first version under-reported one. Precision went up and recall did not
move.

### The two that are left, and both are deliberate

**`GelkmarosPadmarashkaAI`, band 61–90.** Already recorded: part of `DF4_Dramata`'s untranslated half,
alongside its remaining timers and waypoint egg-laying.

**`BergrisarAI`, bands 11–20, 21–40, 41–60, 61–80.** This one was re-examined rather than assumed,
because the earlier decision to leave his chakras out was mine and the audit disagreed with it. The
decision stands, and now with evidence rather than a judgement call:

- His six punishment chakras (281417) are placed by `SPAWN_LOCATION_ABSOLUTE` with coordinates in the
  pattern, so unlike the waypoint-placed bucket **we could put them exactly where retail does** — one
  on entering the fight and one per band at 80, 60, 40, 20 and 10 percent, each on its own mark.
- But reading the chakra's own pattern `IDTP_Keeper2` settles it: `on_wake_up` → `goto_waypoint`,
  `on_see_user` → cast and `goto_next_waypoint`, `on_arrived_at_waypoint` → `goto_next_waypoint`,
  and 6956 → despawn. **Its entire behaviour is the walk.** It has no combat branch at all.

So placing them would put six inert twelve-thousand-HP objects around the gate chamber — content that
looks implemented and is not, which is worse than content that is absent. The band findings stay in
the report rather than being suppressed, because the gap is real; what is written down is why it is
not worth closing until walk routes exist.

### A message whose semantics I could not settle, and did not guess

Bergrisar broadcasts **6955** to fifty metres on entering combat, as three other temple patterns do,
with `param_obj=OBJI_SELF`. Three patterns listen:

| listener | on 6955 |
|---|---|
| `IDTP_CyWork` (the cyclops workers) | idle → add hate on the param and attack it; already fighting → switch target to it |
| `IDTP_NepBoss3` (pyre souls) | the same |
| `IDTP_NepBoss2` (od nuclei) | cast `SKILLI_INDEX_0` on the param |

If the param is the boss, the cyclopes and pyre souls **turn on their own master** while the nuclei
cast on him — which reads as a buff. The two halves cannot both be right under one reading, and that
inconsistency is the reason to stop rather than pick. Compare the Dark Poeta barricades' 3409, which
carries `param_obj=OBJI_CUR_TARGET` and is unambiguous: "attack whoever I am fighting".

What would settle it: any pattern that broadcasts with `param_obj=OBJI_SELF` to a listener whose
action is unmistakably hostile or unmistakably friendly. Until then 6955 stays unported, which is the
same verdict as before but now for a stated reason instead of a blocked skill index.

**Verification.** Full suite unchanged at 1,601 passing and 1 skipped — this entry changes no game
behaviour, only the tool and what is known about its output. `audit_pattern_guards.py` 16 findings →
2, both triaged above, with the four pre-fix bugs still caught.

## The twin protectors' lava side was summoning the heatvent side's wave

`audit_retail_messages.py` — the third audit, which asks what a translated class's pattern does with
messages that the class never touches — puts eleven findings on `TwinProtectorAI`, more than on any
other class. Following them meant resolving the whole `IDSeal_Twin_*` NPC web, and the web turned up
something the message audit was not looking for.

### One id, four protectors, two sides

| devname | id | side |
|---|---|---|
| `BIDSeal_Twin_M_Sum_Tornado` | 855625 | heatvent |
| `BIDSeal_Twin_M_Sum_65_Ae` | 855622 | heatvent |
| `bidseal_twin_m_hellfirefield` | 855712 | heatvent |
| `BIDSeal_Twin_P_Sum_65_Ae` | 855621 | lava |
| `BIDSeal_Twin_P_Sum_Crater` | 855623 | lava |
| `bidseal_twin_p_hellfirefield` | 855626 | lava |

Every `spawn_on_multi_target` branch in the two heatvent patterns calls **855625**, and every one in
the two lava patterns calls **855621**. This class had 855625 hardcoded on its hellfire branch for all
four protectors, so **the two lava protectors summoned a heatvent NPC**.

What makes it worth stating rather than just fixing: the class was *already* side-aware. The field is
chosen by parity and so is the phase ladder's wave. Only the hellfire branch was not — which is how a
side-specific id hides in a fight where most of the summons are already side-specific, and the same
shape as the Dark Poeta barricades holding each other's coordinates.

### The waves arrive fighting, and did not

Retail carries `hatepoints_to_add=1000` on every one of those branches, both sides. Ours spawned them
at the target's feet with no hate, so they stood there until a player walked into them. Now routed
through the shared `AttackAfterSpawn` helper, as every other `attack_target_after_spawn` spawn is.

### A pin that agreed with the table and not with the fight

The first version of these pins asserted `WaveFor(protectorId)` and nothing else. Putting the shipped
bug back — hardcoding the tornado at the call site again — **survived the whole mutation sweep**,
because the table was still right and no pin ever watched a lava protector actually summon.

That is the mirror of an earlier lesson in this log. "Pin the decision when the effect leaves no
trace" is right; the corollary is that when the effect *does* leave a trace, pinning only the decision
is not enough. Both are pinned now, and the phase ladder's split — correct all along, and guarded by
nothing, so flattening it also survived — has a pin of its own.

### Not spawned by anything

`BIDSeal_Twin_P_Sum_Crater` (855623) has a template and a pattern and **no branch in the 5.8 files
names it**. The same shape as Watchman Hokuruki's gunners: a template that exists for the room rather
than for a summon. Recorded so it is not read as a gap.

### What is still not translated here, and why

The eleven message findings that started this remain open, and they are a coherent group rather than
eleven separate jobs:

| message | what it does | blocked on |
|---|---|---|
| 22714 / 22715 | the hellfire field casts | `SKILLI_INDEX` on the field |
| 22712 | the heatvent tornado **turns into** 855622 and despawns | reachable — needs the timer chain that sends it |
| 22713 | a heatvent Sum heals the protector and despawns | the heal is an index; the despawn is not |
| 22697 / 22698 | every Sum despawns when the protector leaves the fight | ours clears its own tracked list instead, which covers the same ground by a different route |
| 22704 / 22705 | **time over**: the protector summons three PC guards onto itself that attack it, Elyos or Asmodian by `is_race` | the sender is a `_Source` / `_Change_Failed` NPC neither of which is translated |
| 22710 / 22718 / 22719 | instance sequencing between the protector and its spawn markers | nothing on our side listens |

The one worth naming for later is **22705**. It is a real mechanic and an unusual one — fail the
timer and allied NPC guards arrive and finish the protector for you, with a million hate so nothing
peels them — and both halves of it are portable in principle. What is missing is the sender:
`IDSeal_Twin_M_Source` and `IDSeal_Twin_M_Change_Failed`, the NPCs the protector leaves behind on
dying, which are a separate encounter this work has not touched.

**Verification.** Full suite 1,609 passing and 1 skipped; thirteen pins on this class, up from five;
seven mutations, all caught — including the shipped bug, which survived the first sweep.

## The Empyrean Lords arrived without the gods they call

Following `audit_retail_messages.py`'s next-largest cluster — nine findings on Tiamat hard mode — led
to the four god avatars of the Dragon Lord's Refuge, and to something the message audit was not
asking about: **neither of the two NPCs an avatar places was placed at all.**

Eight npc ids share `empyrean_lord` across two difficulties and four roles, and the eight retail
patterns behind them agree pair for pair. The class already split those four roles for its casts, so
the spawns slot into a shape that was already there:

| role | places | when | for |
|---|---|---|---|
| Kaisinel avatar 1 (219488, 856020) | **kaisinel** (283159) | +7s | 20s |
| Marchutan avatar 1 (219491, 856023) | **marchutan** (283160) | +7s | 20s |
| Kaisinel avatar 2 (219489, 856021) | its teleport (283175) | on arrival | 6s |
| Marchutan avatar 2 (219492, 856024) | its teleport (283176) | on arrival | 6s |

Two details worth stating because a port that read only the spawn action would lose both. The
**seven seconds** are retail's `set_idle_timer` on `on_wake_up`, so the first avatar arrives alone and
the god follows — a beat, not a detail. And 283159 and 283160 are not effects: they are named NPCs on
tribe `IDTIAMAT_SPAWNHEAL`, which is the god itself appearing to mend the raid.

### Not translated: the four corner broadcasters, and why that is not the chakra call again

Each first avatar's `on_die` puts an `IDTiamat_Tiamat_Broadcast_God_OnDie` (283181) on each corner of
the arena — (215, 188), (791, 195), (216, 834), (777, 839) — for ten seconds, and each relays
`broadcast_message 71` fifty metres. It is the same trick Lower Udas Temple uses: a hundred-metre
broadcast covering a room far bigger than that.

Every one of the eight listeners for 71 is a Tiamat key, and every one answers with a bare
`SKILLI_INDEX` cast. So this is the **unheard** category — placing the four would put NPCs in the
world whose only purpose is a message nothing on our side can act on.

Worth separating from the Bergrisar decision in the previous entry, because the two look alike and
are not:

- **Bergrisar's chakras** are blocked because the add's *own* behaviour is a walk we cannot drive. A
  placed chakra is inert.
- **These broadcasters** are blocked because their *audience* cannot act. A placed broadcaster would
  do exactly what retail's does — and land on nobody.
- **These gods and teleports** are neither. An FX or presence NPC's whole job is to exist and be seen
  for a stated number of seconds, and that we can reproduce exactly.

The test for "is this worth placing" is therefore not "does it carry a pathname" or "is it an
effect", but **does the thing it does survive the translation**. For a rolling hazard, no. For a
relay into silence, no. For something whose job is to be there, yes.

### Also not translated

The avatars' whole message web — 20, 23, 32, 37, 38, 39, 200, 201, 202 — and the
`set_condition_spawn_variable` calls threaded through it. The one that matters is **32**: Tiamat
sends it, and a first avatar answers by casting, arming a five-hundred-millisecond timer, and then
leaving inside its own teleport effect. That is the god withdrawing, and it needs the message and the
index together.

Worth recording about those message numbers: 20, 23, 27, 31, 32 and 40 are **reused across unrelated
instances** — Eternity, Infinity Shard, the arenas, the RVR guards all use them — unlike the
encounter-scoped numbers this work has ported (140505, 6956, 22705). Only 37, 38 and 39 are private to
the Tiamat family. Any future port here has to be scoped by range and encounter rather than by number,
which is a caveat the earlier message work never needed.

**Verification.** Full suite 1,626 passing and 1 skipped; seventeen new pins; eight mutations, all
caught — including the shipped state, in which nothing is placed at all.

## Exedil's first-wave ghosts do not die when he reaches the end — they change

Working down `audit_retail_messages.py`'s list by *what the listeners do* rather than by class size
puts most of the forty-four findings out of reach quickly: the listener answers with a bare
`SKILLI_INDEX` cast, and there is nothing to port. Sorting by the ones whose listeners **spawn,
despawn or take hate** leaves a short list, and the clearest entry on it belongs to a boss corrected
two entries ago.

`ND2_PhA`'s deep rung broadcasts **3319** to fifty metres. `ND2_Sum_PhA1` — the first-wave ghost
(280774) — answers it by spawning the second-wave ghost where it stands, with the same twenty-minute
lifetime the boss's own rungs use, and removing itself. One in, one out, on the spot.

**Why it usually looks like nothing.** The 26–55 rung despawns the first pair as it hands over, so by
the time Exedil is under twenty-five there is normally nothing left to hear the broadcast. It matters
exactly when a raid **skipped that band**: the pair that survived because a rung was jumped over gets
*upgraded* rather than removed. Burning him down fast trades two twenty-minute ghosts for two
permanent ones instead of for nothing — which is the opposite of what skipping a band costs
everywhere else in this family, and only visible once the bands themselves were right.

The same branch exists on the naga side: `ND2_Sum_Naga_PhA1` (280769, "power of yatri") becomes
280819. Both listeners are shipped here, because it is one branch.

### What is missing, and it is a boss

**`Naga_PhA` belongs to high priest yatri (212308 and 280768), which is on plain `aggressive` with no
class at all.** It is Exedil's shape with its own numbers — banded rungs on a heartbeat, a deep rung
at 25 that broadcasts 3319, and a first wave placed with `spawn_on_target` rather than scattered. So
the naga listener now works and has nobody to hear from. That asymmetry is deliberate and recorded
rather than hidden: the alternative was to hold back a correct branch until a whole boss was
translated.

### Message 3320, and a limit of our `servant` class

His timer-6 branch broadcasts **3320** every twenty seconds once the deep rung has armed it, and
retail's second-wave ghosts answer by taking hate on whoever he is fighting and turning on them.

That one is **not** ported, and the reason is on our side rather than in the data.
`ServantNpcAI` captures a summon's target when it spawns and drives its cast loop from that captured
reference. A re-aim would move the hate and change `GetTarget()`, and the ghost would go on casting
at whoever it first saw. Sending 3320 would look wired and would not be.

Worth stating as a general limit, because several of the remaining findings are this shape: **an
`add_hate_point` + `attack_most_hating` listener is only portable onto an AI that re-reads its
target.** Of the pending findings, 444 (Danuar frost summons), 104 (the Dramata drakan), 3403
(Takahan's drakan) and 6952 (Kingspin's fanatics) are all that shape and all need the same check
before porting.

### A pin that every other pin agreed with

Making the ghost answer *any* message survived the first mutation sweep — five other mutations were
caught and that one was not, because nothing else in the fight broadcasts to a ghost. Message numbers
are chosen per encounter with no registry, so a listener that answers everything transforms on a
neighbour's broadcast. There is now a pin that sends the wrong number first. **Third time this
session that the surviving mutation was the one nothing else in the test file could see** — after the
twin protectors' call site and Hokuruki's swing counting.

**Verification.** Full suite 1,632 passing and 1 skipped; six new pins on this family, twenty in the
file; six mutations, all caught. `audit_ai_messages.py` pairs 3319 and is otherwise unchanged.

### And the message audit could not read a guard clause

`audit_ai_messages.py` reported 3319 as a broadcast with no listener while `ExedilGhostAI` sat there
listening for it. The listener is written as an early-return guard —
`if (messageType != ExedilAI.TrueForm || IsDead()) return;` — and the scan read only `==`.

Fixing it to accept both senses cleared **two** findings, not one. The other was **6980**: Macunbello
answers his soul reapers through the same guard shape, and the audit had been calling that pairing
broken since the day it was written. Unpaired count 9 → **7**, and both halves of every remaining
finding are genuinely absent.

Second time this audit has been widened after reporting correct code — `case` last time, `!=` now.
Both were the same underlying mistake: **assuming a listener declares itself the way the last one
did.** The listener shapes it now knows are `When.Message`, a `case` label, and a comparison in
either direction inside `OnNpcMessage`.

## High priest yatri, the sender the naga ghosts had been waiting for

The previous entry shipped a listener with no sender on purpose: `ExedilGhostAI`'s naga half answered
`3319` and nothing in the world sent it, because `Naga_PhA` belongs to **high priest yatri** (212308
and 280768) and he was on plain `aggressive` with no class at all. This closes that.

**He is `ExedilAI`'s architecture with none of his numbers**, which makes the pair worth reading side
by side — the same eight-branch skeleton, and every value different:

| | Exedil (`ND2_PhA`) | yatri (`Naga_PhA`) |
|---|---|---|
| opening heartbeat | 10s | 8s |
| 81–100 rung | re-arms at 6s — same as the fallback, so **dropped** | re-arms at **10s**, so **kept** |
| 56–80 | two ghosts **around himself**, 6m | two **on his target**, 5m |
| 26–55 | hand-over, **around himself**, 7m | hand-over, **on his target**, 5m |
| below 25 | two around himself, **no lifetime at all** | two around himself, 8m, **twenty minutes** |
| deep rung ends the chain | yes (arms timer 6) | yes (arms timer 6) |

**His waves land on the raid.** That is the difference that changes how the fight feels: Exedil
scatters ghosts around his own feet and yatri's first two waves are `spawn_on_target`. Only his
deepest comes home to him. Two bosses cut from one template, and the placement is what separates them.

The 81–100 rung is the clearest case yet of the rule this log has been applying: its casts are not
translated and nothing else about it differs from the fallback **except a four-second re-arm**, and
that alone earns it a place in the table. Exedil's equivalent rung re-arms at the same six seconds as
his fallback and is not in his.

### Two harness limits this boss found, both worth knowing before the next `spawn_on_target` port

**A far-away stand-in player does not help when the summons land on it.** Exedil's pins keep the
player sixty metres back so his ghosts' casts never reach it. That trick is useless here: a
`spawn_on_target` wave appears *on* the target whatever the distance, and a `servant` cast into the
harness's stand-in takes the effect engine down. The fix is to stand a plain `aggressive` NPC in as
the thing he is fighting — placement stays observable and the casts never touch a player.

**And `NpcMessageBus` under-delivers to summons the harness placed away from the sender.** It walks
the sender's known list and falls back to a region scan only when that list is *empty* — which it is
not, once the harness has made the quarry known. Production keeps a summon five metres away in the
boss's list through the visibility system; the harness runs none, so a pin about a broadcast to
placed summons has to make them known by hand. Exedil's equivalent pin needs none of this only
because his ghosts land on his own position, which is why the limitation did not surface until now.

There is a real consequence hiding behind the second one, and it is retail's rather than ours: **his
waves land on the raid and his broadcast reaches fifty metres**, so a raid that fights him at range
puts its own waves outside the message that would have upgraded them. The pin that exercises 3319
therefore stands the quarry five metres out, where a real fight is.

### Still not translated

Seven skill indices across timers 1–7 and the rungs that arm them; `valid_distance=50` on both
`spawn_on_target` waves, which retail uses to skip the spawn when the target is further off than
that; and four broadcasts — **3316** and **3318** reach only cast branches, **3301** and **3302**
reach nothing we have, and **3320** is the re-aim recorded last entry, still blocked on `servant`
capturing its target at spawn.

**Verification.** Full suite 1,644 passing and 1 skipped; twelve new pins; eleven mutations, all
caught. Three needed a pin written for them — the fallback, the ten-second re-arm and the chain
stopping — because every other pin sets health straight into a band and never exercises the clock.
`audit_pattern_guards.py` still reports its two triaged findings and no new ones;
`audit_ai_messages.py` is unchanged at seven, with 3319 paired in both directions.

## A third reason a mechanic is unreachable: nobody to say it to

Chasing the next `audit_retail_messages.py` finding — **6682**, on the Abyssal Reliquary chamber
lords — turned up a complete retail chain and a new kind of dead end.

### The chain, end to end

1. The **weakened** lord (`BGuard_ChiefD_Minor`) broadcasts **6682** to ten metres as it wakes. The
   **awakened** lord (`BGuard_ChiefD`) answers with `despawn_self`: the fortress swaps which version
   of the lord is present and the one being replaced bows out. Ten metres, because they stand on the
   same spot.
2. Either lord's death places despawn helpers on four fixed marks — already ported, in the entry on
   the chamber lords' death spawns.
3. Each helper broadcasts on waking, a hundred metres: 296338 → **10006**, 296339 → **10007**.
4. Twenty `DrGuard_*_WarpH2/H3` patterns answer with `despawn_self`. Killing a chamber lord clears
   the drakan garrison.

Links 3 and 4 are the reachable half — the listeners despawn rather than cast. And they cannot be
reached, because **none of the twelve NPCs bound to those listener patterns exists in our world**.
No spawn file, no instance handler, no code. The same is true of the weakened lord that would send
6682 at link 1.

So the mechanic is blocked on **missing world spawn data** — not a skill index, not a walk route,
not the shape of our AI. That is a category this log did not have, and the tool now names it.

### Two verdicts, and the mirror

`audit_retail_messages.py` gains **`no audience`** — a broadcast whose listeners would act, but every
NPC bound to those listener patterns is one our world never spawns — and **`no speaker`**, the mirror,
for a handler worth writing whose every retail sender is unspawned. Both need `--binding`.

Ten of the forty-seven findings move out of `acts`, which materially changes what is worth picking up
next:

| verdict | count | examples |
|---|---|---|
| `acts` | 37 | still the work |
| `no audience` | 5 | Takahan's drakan (3403), Prectaz (100001), three Twin Protector |
| `no speaker` | 5 | the chamber lords (6682), Lord Lannok (6608), **the twin protectors' time-over rescue (22704/22705)** |

### A correction to two entries ago

The Twin Protector write-up named 22705's missing half as `IDSeal_Twin_M_Source`, "a separate
encounter this work has not touched". **Both halves of that are wrong.** `_Source` (855709) is the
*listener* — it is the NPC that spawns the PC guards — and `DrakenspireDepthsInstance` already spawns
it. The sender is `IDSeal_Twin_M_Change_Failed` (855511, 856404), which broadcasts 22705 on waking and
again every three seconds, and which nothing in our world places.

So the rescue is one npc spawn away from working, not an encounter away. That is a much better lead
than the one recorded, and it only appeared because the audit was made to ask the question precisely.

### How the wrong claim got made, and what it means for the rest of this session

Every ad-hoc sender/listener map in this session's notes was built with a proximity regex —
`broadcast_message[\s\S]{0,400}?<message_type>N<` — and **that pattern conflates the two roles**. A
branch that listens for a message and broadcasts a different one puts both tags within four hundred
characters of each other, so `_Source` was read as a sender when it is a listener.

Re-checked every message this session shipped behaviour for, against a proper parse of the branch
tree — 140505, 3319, 3320, 6956, 6682, 3409, 71, 22714, 22715 — and **all of them hold**. The
proximity regex was wrong exactly once, on the one message that was only ever written up rather than
ported. The audits themselves never used it; they parse `<conditions>` and `<actions>` separately,
which is why the tool disagreed with the note and was right.

**Worth generalising: a scratch regex is fine for finding candidates and not for stating facts.**
The ones stated in this log now come from the parsers.

### Still not done here

The chamber lords' 6682, and links 3 and 4 of the garrison clear-up, stay unported. Reaching them
needs the twelve warp guards and the weakened lords placed in the world — a spawn-data job, and one
that should be checked against retail spawn tables rather than invented.

**Verification.** No game behaviour changed; full suite unchanged at 1,644 passing and 1 skipped.
`audit_retail_messages.py` reclassifies ten findings and its sender scan now counts every broadcast,
including those beside a cast — without that the time-over rescue's real sender was invisible.

## Queen Modor called three adds; she was meant to call them onto someone

With the message audit's noise classified, the remaining `acts` findings sort by one question: **can
the listeners act, on NPCs our world actually places?** Message **444** is the best answer in the
list, and it belongs to a boss already on our server.

Every branch of `Rune_FrostNmd_N_65_Ah` that teleports Queen Modor to a pillar and places her three
summons at fixed coordinates follows the spawns with `broadcast_message 444` to fifty metres, naming
**her current target**. All six summon patterns answer it identically: `add_hate_point` on the player
she named, then `attack_most_hating`.

`CursedQueenModorAI.SpawnAdds()` already placed the three at exactly retail's coordinates. It sent
nothing, so they arrived and picked their own quarry.

**A summon that has just appeared holds no hate at all**, which is why one point is enough — it makes
the named player the most-hated by itself. That is the mechanic: she does not summon three adds, she
summons three adds *onto somebody*.

### The pair is reproduced, not collapsed

`add_hate_point` + `attack_most_hating` is not "switch to the named player", and the difference is
observable: a summon that has built real hate on somebody else **stays on them** and the order does
nothing. Collapsing the two into a target switch would be a stronger mechanic than retail ships, so
the listener adds one point and then attacks whoever is most-hated, exactly as written.

This is also the first of the `add_hate_point` findings that turned out to be portable. The limit
recorded two entries ago still stands — `ServantNpcAI` captures its target at spawn, so a re-aim has
nothing to act on — but **these eighteen summons are `aggressive`, not `servant`**, so it does not
apply. Worth stating as the rule: *that* limit is about the listener's AI base, not about the message
shape, and it has to be checked per encounter rather than assumed from the branch.

### Two pins that existed only because a mutation survived

- **The range.** The behavioural pin broadcasts at fifty metres and checks who hears it, which passes
  for any range the pin and the sender happen to share. Widening the order to the whole room survived
  the sweep until a pin asserted the constant against a literal fifty. The constant now lives beside
  the message on the listener rather than with the sender, because the two are one fact about the
  order and a sender that invented its own range would be a different mechanic.
- **The message number.** Answering *any* message survived until a pin sent the wrong one first —
  the fourth time this session, and by now an expected outcome rather than a surprise.

### And a harness detail worth keeping

The "already fighting somebody else" pin first used a second NPC as the thing the summon was busy
with, and failed: the aggro list only offers a valid **enemy** as most-hated, and two NPCs of one
tribe are not enemies. The stand-in read as "no hate at all" and the pin would have passed for the
wrong reason. **A pin about hate ordering needs players on both sides**, or it is not testing
ordering at all.

### Still not translated here

Everything else in those six summon patterns, which is casts. And message **104**, the other number
the audit reports on this family: its listeners are the Dramata drakan rather than these summons, and
its senders are patterns this work has not read.

**Verification.** Full suite 1,652 passing and 1 skipped; eight new pins; four mutations, all caught.
`audit_ai_messages.py` pairs 444 and is otherwise unchanged.

## The same order, a second time — and it is now an op

Frostmane Lestin turned out to be the other half of the shape found on Queen Modor. **All three of
his summoning rungs** — at 66–90, 41–65 and 21–40 — place four elementals and then broadcast
**6505** to fifty metres naming his current target, and the wave that has just arrived takes a hate
point on that player and attacks.

His spawn side was already right (bands, hand-over and fallback verified three entries ago). Only the
order was missing.

### One op, two encounters

Rather than a second copy of the listener, the branch is now `Ai/SummonOrder.cs` — the same shape as
`AttackAfterSpawn`, and for the same reason: the retail branch is identical across unrelated
encounters and only the message number differs. It takes the `Npc` rather than its AI, because the
aggro list and the state flip are reachable from the owner and protected on the AI, so one op serves
any listener base.

`DanuarSummonOrderAI` was rewired onto it in the same commit, which is what makes this a shared op
rather than a copy.

The listener covers all six `ND2_PnF` NPCs, not only Lestin's three waves. The other three are the
fire elemental boss's **faithful servants**, and their master — `ND2_ElementalSu`, raging kraterr
(211715) — runs on `summoner` and broadcasts 6505 from its own rungs in retail. Listing them here is
deliberate: it is one retail pattern, and splitting it would leave the next reader to rediscover
that. **That boss is the obvious next step**, and it is a boss's worth of work rather than a branch's.

### Four pins that only exist because a mutation survived

This encounter was unusually hard to pin honestly, and every difficulty was the same one: *the thing
being asserted had a second cause.*

- **The quarry stands forty-five metres out.** These elementals are aggressive and spawn within
  fifteen metres of Lestin, so a quarry beside him is one they would find by themselves. Removing the
  broadcast entirely survived until the player moved out of their sight and inside his fifty-metre
  order.
- **The listener is a stand-in placed before the fight, not one of the four he summons.**
  `NpcMessageBus` walks the sender's known list, the harness runs no visibility, and the broadcast
  sits in the *same branch* as the spawn — so an elemental he places is invisible to him at the
  instant he calls out to it, and nothing a test does between ticks can change that. A listener that
  was already known hears the same broadcast and pins the same fact. (A `MakeEveryoneKnown` helper was
  written for this and then removed: it cannot help, because the gap is inside one branch.)
- **The stand-in is deliberately not made known to the quarry.** Linking those two lets an aggressive
  elemental find the player on its own, and the pin then passes whether or not the order named
  anybody.
- **The range is pinned against a literal**, as the Danuar one is.

### A mutation that survived for a reason in the mutation

"The order names nobody" survived a sweep, and the cause was the sweep rather than the pins: the
edit replaced the *first* `aboutTarget: true` in the file, which is the 21–40 rung, while the pin
under it drives 66–90. Re-run against all three sites and against the 66–90 site alone, it is caught
both times.

Worth keeping as a rule: **a mutation on a file with several identical call sites must say which one
it is changing**, or a survivor means nothing. Every earlier sweep in this log mutated a unique
string; this is the first file where that stopped being true.

### And the registration trap, in a new form

Repointing six templates to `elemental_wave` broke eight of `FrostmaneLestinAiTests` — the harness
registers the AI classes it needs, and Lestin's waves were `aggressive` when those pins were written.
This is the same trap recorded eight times already, but a distinct form of it: not "I forgot to
register the add's class" but **"repointing a template breaks every existing harness that spawns
it"**. Anything that changes an `ai=` attribute should be followed by a full-suite run before the
class is even finished.

**Verification.** Full suite 1,657 passing and 1 skipped; five new pins plus one harness registration
fixed; seven mutations, all caught.

## Raging kraterr, and the summon table that was an observation

The fire elemental boss was the step recorded at the end of the last entry: `ND2_ElementalSu`,
belonging to **raging kraterr** (211715) and its summoned twin (280332), and the sender
`ElementalWaveAI` shipped without.

**The two patterns are numerically identical.** Same bands, same timer slots, same delays, same
counts, same ranges, same lifetime — Lestin's `ND2_ElementalSu2` and kraterr's `ND2_ElementalSu`
differ only in which three elementals each calls. So the fight is now
`ElementalSummonerPattern.For(first, second, third)` and each boss is three npc ids, the same split
`GuardReinforcementPatterns` uses.

### What the summon table got wrong, itemised

He ran on `summoner`, and that table is what an observed approximation looks like:

| | the table | the pattern |
|---|---|---|
| thresholds | 90 / 70 / 40 | bands 66–90, 41–65, 21–40 |
| which add | **280333 all three times** | a different elemental per wave |
| how many | two to five at random | exactly four |
| how far | ten metres | twelve, then fifteen |
| how long | no lifetime | ten minutes |
| hand-over | none | each wave clears the one before it |
| the order | none | every rung names his target |

The table row is **removed** rather than left inert, so there is one source of truth for what he
summons.

### What moving him off it costs

The table cast **18389** alongside each summon and **18390** at twenty-five percent, and nothing in
the pattern runtime replaces them: this trades two casts for the right waves, lifetimes, hand-over
and order. Stated plainly rather than buried, because it is a real loss in one dimension.

### An index mapping that looked solid and was not

It was tempting to resolve the pattern's `SKILLI_INDEX_0` as 18389 — aionemu casts it beside the
summons, retail's three summoning rungs cast index 0, and both 18389 and 18390 sit at `prob="0"` in
his `npc_skills` row, meaning they are never randomly chosen and exist only to be driven by something
that names them. Three strands, all pointing the same way.

**The skill data refuses it.** 18389 is *Fire Wave*, a `MAGICAL DEBUFF` on a `DEBUFF` slot with a
damage-over-time stack, and retail casts index 0 on `OBJI_SELF`. A debuff is not a self-cast. What
aionemu's table records is a cast **at the target**, which in retail lives on the other timers rather
than on the summoning rungs.

This is the skill-index audit's own rule earning its keep: passing the reach gate is necessary, not
sufficient, and the corroborating source has to be checked rather than assumed. Two strands agreed
and the third — the stack name — was the one that mattered.

Worth keeping for whoever does resolve these: the two bosses' lists are **parallel in shape**. 18389
and 18390 are Fire Wave and Powerful Fire Wave; Lestin's 18394 and 18395 are Small and Powerful
Bloody Wind, with matching `...WEAKTA10_ADDREFLECT20S` / `...STRONGTA20_ADDREFLECT20S` stack names. A
mapping established on one is evidence for the other — and they share a pattern but *not* a skill
list, so resolving one index does not resolve the other's.

### And the guard audit could not see through a shared builder

Splitting the fight into `ElementalSummonerPattern` made `audit_pattern_guards.py` report all three
of kraterr's bands as missing: the class's guards live in a builder declared in **another file**, and
the scan read a class's own body plus its file preamble. It now follows whatever a class delegates
to — `X.Method(` where some file declares `class X` — wherever that is declared. Third widening of
this audit, and the same underlying mistake each time: **assuming the thing being looked for is
written where the last one was.**

Repairing it also cost a detour worth recording: the patch was applied through a shell heredoc, which
turned `\b` into a literal backspace and left two regexes matching nothing. That trap is already in
this log — *write patch scripts with the Write tool, not a heredoc* — and it still caught me. The
symptom is a check that suddenly finds zero of something it used to find.

**Verification.** Full suite 1,664 passing and 1 skipped; seven new pins; seven mutations, all
caught, including the summon table's own shape (one elemental for all three waves) put back.
`audit_pattern_guards.py` back to its two triaged findings.

## Message numbers collide, and the audit was reading a collision as work

Following the next `acts` finding — **6508** on both elemental bosses — turned out to be a false
lead, and the reason generalises.

Their deepest rung, below twenty percent, broadcasts 6507 and then 6508 every twenty-five seconds.
6508 has an acting listener, `ND2_Xipeto3_1`, which takes hate on the named player and attacks. On
that basis the audit called it `acts` and it looked like the deep rung had a translatable effect
after all.

**It does not.** `ND2_Xipeto3_1` belongs to the Dark Poeta xipetos (214870, 281189), which stand on
map 300040000. The elemental bosses stand on Morheim and Eltnen. They could never be within fifty
metres of each other, and the shared number is retail assigning message ids per encounter with no
registry — low numbers collide freely. Within their own encounter, 6507 and 6508 are heard only by
`ND2_PnF`, with casts, which is exactly the `unheard` verdict. The deep rung's translation as "spends
the tick and summons nothing" was already right.

### `diff world`

`audit_retail_messages.py` gains a fifth verdict: **every NPC that would answer this message stands
on maps this class's NPCs never appear on.** It builds an npc → maps index from our own spawn files
(19,415 npcs) and compares the sender's maps against the listeners'.

Only spawn files count. An NPC placed by an instance handler has no map in the data — Watchman
Hokuruki is one — and the check **abstains** rather than guessing, because a wrong "different world"
would hide real work.

Three findings move out of `acts`, all of them this same 6508. The verdict does not fire on anything
this session shipped: 140505, 3319, 444 and 6505 are all same-world, which is the check that mattered
before trusting it.

This is the caveat recorded two entries ago against the Tiamat numbers — 20, 23, 27, 31, 32, 40 are
reused across Eternity, Infinity Shard, the arenas and the RVR guards — now expressed as a check
rather than a note. **Any message-driven port has to be scoped by range and encounter, not by
number**, and the tool can now say which findings fail that test.

### A small confirmation that came free

The map index also settles what the two elemental bosses are to each other: Frostmane Lestin stands
in **Morheim** and raging kraterr in **Eltnen** — the Asmodian and Elyos halves of the same content.
That is why their patterns are numerically identical and their skill lists are parallel in shape but
share nothing: they are one encounter, built twice.

**Verification.** No game behaviour changed; full suite unchanged at 1,664 passing and 1 skipped.
`audit_retail_messages.py` now reports 38 `acts`, 5 `no audience`, 5 `no speaker`, 3 `diff world`,
17 `cast-only` and 14 `unheard`.

## A branch that spawns and then broadcasts means something different here

Two blind spots closed and one port abandoned, and the abandoned one is the useful part.

### The audit was reporting finished work

`audit_retail_messages.py` still had two blind spots its siblings had already fixed, and both made it
list work that was done:

- **comparison listeners** — `ExedilGhostAI` writes `if (messageType != ExedilAI.TrueForm) return;`,
  and the scan knew only `When.Message`, `Do.Broadcast`, `NpcMessageBus.Broadcast` and `case`. Same
  widening `audit_ai_messages.py` needed.
- **delegated builders** — `RagingKraterrAI`'s broadcast lives in `ElementalSummonerPattern`, declared
  in another file. Same widening `audit_pattern_guards.py` needed.

Both are the same underlying mistake, now made three times across three tools: **assuming the thing
being looked for is written where the last one was.** `acts` 38 → 36, and the two rows that left were
`ExedilGhostAI 3319` and `RagingKraterrAI 6505`, both shipped in the last two entries.

### RM-56c's trap dismissal, and why it is not ported

The next finding was **6681** on RM-56c: every trap-laying branch ends by broadcasting it to ten
metres, and the traps answer with `despawn_self`. Laying a new arrangement takes the last one away —
without it a boss walked down through two bands stands in two overlapping sets.

It was written, wired and reverted. **In our engine the broadcast reaches the traps it was just laid
alongside, and removes them the instant they appear.** All eleven of RM-56c's pins failed; changing
only the message number made all eleven pass, which isolates the cause exactly.

The reason is an ordering our runtime has and retail's evidently does not: `NpcMessageBus` walks the
sender's known list, and **our spawn path puts a summon into its spawner's known list before the next
action of the same branch runs**. Measured directly rather than inferred — all four of Frostmane
Lestin's elementals receive the summon order sent from the branch that spawned them.

That measurement also **corrects an explanation given one entry ago.** The Lestin pins use a stand-in
listener placed before the fight, and the reason recorded was that a just-spawned elemental cannot
hear its spawner. That is not true, and the note is fixed. The stand-in is still worth keeping — it
pins the broadcast without depending on an ordering that belongs to our engine rather than to the
pattern — but for that reason, not the one written down.

**This is not a one-boss problem.** 417 branches across 215 patterns broadcast after a spawn in the
same branch. Every one of them will behave differently here than in retail wherever the spawn is also
a listener for that message. Most are harmless — the listener is somebody else — and RM-56c is the
shape where it bites: the spawn *is* the listener.

**What would fix it properly**, and is a runtime change rather than a translation: make a broadcast
skip NPCs spawned by the sender during the current branch, or defer known-list entry until the branch
completes. Both are changes to shared machinery on behalf of one encounter, so neither is made here.
Recorded so that the next person who meets a `spawn`-then-`broadcast` branch knows the shape rather
than rediscovering it through eleven failing pins.

**Verification.** No game behaviour changed; full suite unchanged at 1,664 passing and 1 skipped.
`audit_retail_messages.py` reports 36 `acts`, 5 `no audience`, 5 `no speaker`, 3 `diff world`, 17
`cast-only`, 14 `unheard`.

## Making spawn-then-broadcast mean what retail means, and RM-56c's traps

The previous entry abandoned RM-56c's trap dismissal and recorded why: our spawn path puts a summon
in its spawner's known list before the next action of the same branch runs, so a broadcast written
after a spawn reaches the spawn. Where the thing spawned is a listener for that message, the boss
deletes what it has just placed.

That was left as "a runtime change on behalf of one encounter, so not made". On reflection it is not
one encounter's problem — **417 branches across 215 patterns broadcast after a spawn** — and the
runtime is simply saying something retail does not. So the change is made, and RM-56c is the first
thing it buys.

### The change

`NpcMessageBus.Broadcast` takes an optional exclusion set, and `PatternAi` remembers what the branch
currently running has spawned and passes it. Cleared when the branch finishes, so nothing outside one
branch is affected. Twelve lines across two files.

It is deliberately scoped to *this branch* rather than to some window of time: retail's ordering is a
property of one action list, and a summon placed by an earlier branch should hear the next broadcast —
which is exactly the mechanic RM-56c uses.

### RM-56c's traps, re-landed

Every trap-laying branch ends with `broadcast_message 6681` to ten metres, and the traps answer with
`despawn_self`. Laying a new arrangement takes the last one away, so a boss walked down through two
bands does not stand in two overlapping sets — which the re-lay path, firing on roughly every other
cycle of a band's own timer, would otherwise make common rather than rare.

The traps are `CompleteTrapAI`, extending `TrapNpcAI` so a trap still arms and fires on whoever walks
into it; only the branch that dismisses it is new.

**The pin that matters most is the negative one.** `ATrapSurvivesTheBroadcastItWasLaidWith` asserts
that the arrangement just placed is still on the floor, and it is the only thing in the suite that
exercises the runtime change. Removing the exclusion fails fifteen of RM-56c's sixteen pins;
ignoring it in the bus fails the same fifteen; letting it leak past its branch fails one. All three
are worth having, because the first two say the mechanism is wired and the third says it is scoped.

### What this does not fix

The exclusion is on `PatternAi`'s broadcasts only. A Java-parity class that spawns and then calls
`NpcMessageBus.Broadcast` by hand — `WatchmanHokuruki` and `MacunbelloSoulReaperAI` are the two that
broadcast at all — gets no exclusion, because neither spawns in the same breath and adding a
parameter they do not need would be noise. If a future hand-written class does both, it has to pass
its own set.

And the underlying difference is still ours rather than retail's: our spawn is synchronous and
theirs, by this evidence, is not. Anything else that depends on a summon *not* being visible to its
spawner within one branch will need the same treatment.

**Verification.** Full suite 1,669 passing and 1 skipped; five new pins on RM-56c, sixteen in the
file; six mutations, all caught, including the two that undo the runtime change.

## The time-over rescue: fail the window and your own side finishes the fight

Two entries ago the `no speaker` verdict said the twin protectors' time-over — messages **22704** and
**22705** — was one npc spawn away from working rather than an encounter away. This is that spawn,
and the mechanic it lights up is the most dramatic thing translated in this run.

**Kill one twin and it leaves a font.** The raid then has fifteen seconds to kill the other. Miss it
and a *failure display* appears, announcing the time-over every three seconds; the font answers by
calling **three of your own side's guards down onto itself** — two soldiers at five metres with a
million hate each and their leader at six with a hundred thousand — which then destroy it.

Every piece was already on our server and none of them was connected:

| piece | id | was |
|---|---|---|
| the fonts | 855708, 855709 | `aggressive_no_loot`, spawned by the instance, inert |
| the failure displays | 855510, 855511, 856403, 856404 | `general`, spawned by **nothing** |
| the detachments | 209688/209689, 209753/209754 | `aggressive_no_loot`, spawned by nothing |

And the instance already knew the moment. `DrakenspireDepthsInstance.OnTwinRespawn` runs fifteen
seconds after a twin falls and checks whether its font is still standing — that check *is* the
time-over, and nothing was placed there. The template names settle the pairing without inference:
855510 is `idseal_twin_physical_failuredisplay` and 855511 the magical one, matching the lava and
heatvent fonts.

### The one invented number, stated as such

Retail's display announces until it is dismissed by message **22696**, sent by a quest guard and a
scene NPC neither of which this work has reached. Left alone it would announce forever. It is given
**twenty seconds** instead — long enough for its font to answer once — and that is the only value
here not taken from the data.

### The guards are the raid's race, not the boss's

Retail splits the branch on `is_race` and ships two of everything. The font has no race of its own,
so the detachment is read from the players actually in the instance. A pin covers both sides, because
a fight that always summons Elyos guards would look right in half the runs.

### The flag var is load-bearing

The display repeats every three seconds and the font's branch carries a one-shot flag. Without it a
failed raid drowns in detachments — twelve seconds of announcements is four sets of three guards. The
pin drives the full twelve seconds and asserts three.

**Verification.** Full suite 1,683 passing and 1 skipped; fourteen new pins; eight mutations, all
caught, including the shipped state where no guards are called at all. `audit_retail_messages.py`
`no speaker` 5 → 3.

## Correcting the time-over: the font is a thing that changes, and there are four ways it can

The previous entry landed the twin protectors' rescue and hooked it to
`DrakenspireDepthsInstance.OnTwinRespawn`. **That hook was wrong**, and reading the rest of the font's
pattern is what showed it.

A font is not a timer — it is a **transformer**, and retail ships a separate announcer for each thing
it can become:

| message | announcer | the font becomes |
|---|---|---|
| **22701** | `IDSeal_Twin_*_Spawn` (855710, 855711) | the **Lv3** protector — the one that leaves a font |
| **22707** | `IDSeal_Twin_*_Failed_Spawn` (855713, 855714) | the **Lv2** protector — *fountless*, leaving none |
| **22709** | `IDSeal_Twin_*_Success` | the mind-control quest object (702769) |
| **22704 / 22705** | `IDSeal_Twin_*_Change_Failed` | nothing: your own guards arrive and destroy it |

The fifteen-second window our instance measures is the **22707** moment — the raid failed to kill the
second twin, so the first comes back. It is not the "change failed" moment, which is a font left
standing with no outcome at all, and which our instance never produces. So the hook is removed and
the guards' handler is recorded honestly as a listener without a sender.

**And the hook would not have worked anyway.** `OnTwinRespawn` deletes both fonts in the same method
that spawned the display, and the display's first announcement is scheduled rather than immediate —
so the font was gone before it could be told anything. The pins passed because they drive the message
directly. A mechanic that is wired only in its own tests is exactly what this log's sender/listener
rule exists to catch, and it took reading the pattern rather than running the suite to catch it.

### What replaces it, and it is a better find

`OnTwinRespawn` now does what 22707 does, and two things about it were wrong in Java parity:

- **The fountless one comes back.** Lv2 is 236225 and 236226, and their own names say what they are —
  *fountless* lava and heatvent protector. A fountless protector leaves no font when it dies. Java
  respawned **Lv3**, the font-leaving version, so a raid that missed the window could miss it forever:
  kill, font, fifteen seconds, kill, font. Retail closes the loop after one failure, and the message
  name — `Failed_Spawn` — says which spawn it is.
- **Where it fell, not where it started.** Retail's spawn is `SPAWN_LOCATION_MY_POINT` on the font,
  and the font is left where the protector died. The two fixed marks Java uses are their opening
  positions, which is the same place only if nothing dragged them.

### What is still missing here

The other three announcers. **22701** and **22709** need moments our instance drives its own way —
`OnTwinsComplete` is the success path and could send 22709 to turn the fonts into the quest object
rather than deleting them, which is the obvious next step and a visible one, since retail leaves
something behind and we leave nothing. **22704/22705** needs a font left unresolved, which our
fifteen-second timer prevents by construction.

**Verification.** Full suite unchanged at 1,683 passing and 1 skipped — the correction changes what
the instance spawns, which no pin covered before or after, and that gap is itself worth naming: the
Drakenspire instance handler has no test of its own, so the twin flow is pinned only where it reaches
an AI class.

## The font's fourth outcome, and the first test of an instance handler

Two things were named at the end of the previous entry. Both are done.

### Winning leaves something behind

Retail's success message, **22709**, does not delete the font that is still standing — it turns it
into the **ominous darkness** (702769) where it stands, and only then removes it. That is the fourth
and last of the font's outcomes, completing the table from the previous entry.

Java parity deleted both fonts and left nothing, so a raid that won saw the same empty floor as one
that had never engaged. `OnTwinsComplete` now leaves the object on the mark where the first twin fell.

### The first instance-handler test in the suite, and why it exists

The previous entry's correction turned on a specific miss: a hook was added to
`DrakenspireDepthsInstance`, could not have worked, and **every pin passed anyway** — because the pins
drove the AI class directly and nothing exercised the handler. The twin flow is where this
encounter's decisions actually live.

`DrakenspireTwinFlowTests` constructs the handler against the harness's own map instance and tells it
about deaths by hand. Five pins: the first twin leaves a font; missing the window brings back the
**fountless** protector where it fell; a fountless protector leaves no font; killing both inside the
window leaves the ominous darkness on the font's mark; and a won encounter never brings a protector
back.

Every one of them fails against Java parity's version of the flow — the fountless correction and the
quest object both had no coverage at all until now, including the correction made in the previous
entry.

**A harness detail worth keeping.** `OnDie` is the decision under test and it does not remove the
corpse; production's death path does that separately. Leaving the body in the world made a pin that
counts protectors read the dead one as still standing. The tests kill through a small helper that
does both, so a count means what it looks like.

**And one pin that cannot isolate what it names.** A won encounter never brings a protector back for
two reasons — the fifteen-second task is cancelled *and* the font is gone, and the respawn needs a
font — so removing the cancel alone survives a mutation sweep. It is kept as an outcome pin, because
the outcome is what a raid sees, and the remark says plainly that the cancel is belt-and-braces here.
Anything that later leaves a font standing on a win would make it load-bearing.

### What is still missing on this encounter

**22701**, the announcer that turns a font into the *font-leaving* protector, has no moment in our
flow: our fifteen-second timer is the failure path and always produces the fountless one. Retail
evidently has a success-side respawn that ours does not model, and finding it means reading the rest
of the instance's stage machinery rather than the AI patterns.

And **22704/22705** — the guards' rescue, translated two entries ago — still has no sender, for the
reason recorded there: it answers a font left standing with no outcome at all, which this flow never
produces.

**Verification.** Full suite 1,688 passing and 1 skipped; five new pins, all against an instance
handler for the first time; six mutations, five caught and the sixth documented above.

## Bollvig Blackheart: a wave that changes rather than one that grows

With the twin encounter closed, the missing-AI audit is the better seam again. Its top entries that
*carry spawns* are three LEGENDARY named bosses with no class at all, and the first of them is the
biggest thing left on that list.

**Bollvig Blackheart** (212314 and 280801), Heiron's vampire, ran on plain `aggressive`. His whole
fight was missing:

| band | what happens |
|---|---|
| 81–100 | casts, and the six-second clock |
| 61–80 | two **thirsting bloodwings** (280802), fifteen metres out, forty minutes |
| 41–60 | two more, into the same group |
| 21–40 | the bats **become vampires**, and a **cruel vampire** lands on his quarry every 35s |
| below 20 | the clock stops, and the vampire loop with it |

### The two things worth stating

**His bats are not a wave that grows.** Entering 21–40 he broadcasts `6187`, and every bloodwing still
alive sheds itself for a cruel vampire *where it stands*. Four bats become four vampires in one beat,
and the thirty-five-second loop then adds one more on top. That makes the two earlier waves matter
later rather than only when they land — and it is invisible from the spawn list, which is why the
adds audit never surfaced it.

**The loop is bounded at both ends by different mechanisms**, which took a pin each to separate. Its
timer carries no flag var, so it repeats; its branch is guarded on 21–40, so it stops when he leaves
the band; and the below-twenty rung arms timer 6 rather than timer 0, so the ladder stops too. Push
him through and nothing more arrives. Linger in the band and he keeps paying.

He also clears up after himself: `6630` on waking dismisses the relic (204655) he leaves on dying, so
a second pull does not find the first kill's reward standing.

### Four pins that only exist because a mutation survived

The first sweep left four survivors and every one was the same failure — **a pin that sets health
straight into a band never exercises the clock**:

- unbounding the vampire loop below survived, because the loop is only armed by the band's opening
  rung, which a pin starting under twenty never fires;
- the deep rung re-arming survived, because at fifteen percent nothing matches either way — it needs
  healing him back into a band;
- the fallback survived for the reason it always does;
- and **"waking clears nothing" survived because the pin sent the message itself** rather than
  spawning him. Driving it by spawning works because a boss with an empty known list takes
  `NpcMessageBus`'s region-scan fallback.

That last one is the fifth time this session a pin turned out to be testing its own setup rather than
the code. The pattern is consistent enough to state as a rule: **if a pin constructs the stimulus, it
is pinning the listener; only driving the encounter pins the sender.**

### Not translated

Ten skill indices across timers 1, 2, 3, 4, 6 and 10; the `is_user_flying` guard on timer 10, for
which we have no vocabulary; and broadcasts `6185` and `6188`, whose only listeners answer with a
cast. The 81–100 rung is dropped for the usual reason — its re-arm is the same six seconds the
fallback gives.

**Also recorded, closing an earlier thread.** The twins' `22701` announcer has no counterpart in our
flow and does not need one: it is retail's *arrival staging*, turning fonts into protectors at stage
start, while our twins are static spawns in the instance's own spawn file. Not a mechanic, and the
thread is closed rather than left open.

**Verification.** Full suite 1,699 passing and 1 skipped; eleven new pins; ten mutations, all caught
after the four repairs above. Missing-AI 724 → **723**, and both other audits unchanged at two triaged
findings and seven unpaired messages.
