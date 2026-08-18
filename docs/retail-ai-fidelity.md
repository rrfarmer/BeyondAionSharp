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

### Not spawned by anything — **wrong, and corrected below**

> This section claimed `BIDSeal_Twin_P_Sum_Crater` (855623) had "no branch in the 5.8 files naming
> it". It does: the magma glutten spawns it when it answers `22710`. See *The crater the twins log said
> nobody spawned* further down for the chain and for how the mistake was made.

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
| 22718 / 22719 | instance sequencing between the protector and its spawn markers | nothing on our side listens |
| 22710 | **wrong above**: not sequencing. The lava protector's shield branch; every magma glutten in fifty metres casts, drops a **crater**, and despawns | the anchor — our port has no shield branch to hang it on |

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

## Deputy Hanuman and Missing Indratu — the adds are the same four, re-forged twice

`NDrakan_KhB` binds to **212306 deputy hanuman** and **280751 missing indratu**, two LEGENDARY
captains in Heiron. Both were on plain `aggressive` with no class at all, and between them they were
the second of the three spawn-carrying named bosses left at the top of `audit_missing_ai.py`. Their
three summons — 280752, 280753, 280754, `BLF3_NM_DrakanDF3Slave1/2/3_48_Ae` — appear in no spawn file
and were called by nothing.

### The mechanic the spawn list cannot show

The three summons **share one display name**, "faithful subordinate", and differ only in id. That is
not an accident of the data: they are one add in three forms, and the fight is the escalation between
them.

| band | what he does |
|---|---|
| 91–100 | the six-second clock, and casts |
| 71–90 | two subordinates, ten metres out, thirty minutes |
| 51–70 | two more, into the same group |
| 31–50 | broadcast `5001` — **every subordinate sheds itself for the second form** — and five seconds later two already-changed ones arrive |
| below 30 | broadcast `5002` — **they all change again** — and the clock stops |

So a raid that kills the adds each time meets four weak ones over the fight; a raid that ignores them
meets six of the strongest. The count only ever goes 2 → 4 → 6, but what those six *are* is decided
by how long the earlier ones were left alive. Reading the spawn table alone gives three unrelated
waves of two and misses the whole thing.

`NDrakan_ChSlave4` carries the first change and `NDrakan_Chslave5` the second, and they are not
symmetrical: the first form arms a **two-second battle timer** and changes when it fires, the second
changes in the same beat as the message. That gap is the difference between the change reading as a
stagger down the pack and reading as a blink.

### Two peels, and the pick changes

Each band arms its own alarm. When it rings he tells the group to re-pick (`6001`) and turns on the
**third-most-hated** player — behind the tank and the off-tank. The 71–90 and 31–50 alarms alternate
between two timer slots so they keep ringing for as long as the band lasts; the 51–70 one rings once
and hands over to a cast loop, so that band peels exactly once.

Below thirty the pick changes: timer 9 re-arms itself every twenty-eight seconds and takes the
**lowest health fraction** in the room instead. That is what makes the last third the dangerous part
of the fight, and it is the second use of `AggroTarget.LOWEST_HP` since it was added.

The two forms answer `6001` differently too — the first takes a random attacker, the second takes
whoever is closest to dying — so the pack itself gets harder to peel off a healer as it escalates.

### The stop is only visible if you heal him

The below-thirty rung is the only one that does not re-arm timer 0. Below thirty that looks free:
every band rung is out of range anyway, so a mutation that re-arms the clock there changes nothing a
pin sitting at twenty percent can see. It costs a whole wave the moment a healer brings him back up
into 31–50 — the rung would fire, and with the stop in place it cannot. That case is now pinned
directly, and it is the same lesson Bollvig's sweep taught one entry ago in a new disguise: **a pin
that never moves the boss between bands cannot see a rung that only matters between bands.**

### Not translated

Eight skill indices and the four branches that are nothing but casts — the 91–100 timer-1 loop, the
51–70 timer-4 loop and the cast halves of the peel rungs. The `6001` sent below thirty, whose only
audience by then is 280754, whose whole pattern (`NDrakan_ChSlave6`) is a single `use_skill`.
`on_see_friend_killed_by_user` on all three forms, an event our runtime does not raise — retail uses
it to make a subordinate leave when it watches another die to a player, which is why a pack thins out
rather than fighting to the last. And `despawn_at_attack_state=TRUE` on all three spawns, left to
`live_time` for the reason already recorded against the Abyssal Reliquary flying worm: retail
declares no despawn handler in this pattern, so writing one would be our behaviour and not theirs.

**The four `say_to_all` lines are refused for a reason worth stating.** `STR_CHAT_CoDragon_AIPattern_4`,
`_33`, `_57` and `_59` have no row in `npc_shouts.xml` for any of these five npcs, and `Do.Say` takes
our own numeric string id. There is nothing to say them with, and inventing an id would put arbitrary
text in a boss's mouth. This is the general shape of the remaining shout gap and not specific to
Hanuman.

### A note on the other faction's mirror

`5001`, `5002` and `6001` are also answered by `NDrakan_ChSlave1/2/3` — 280685, 280686, 280687, the
`BDF3_` Asmodian-side summons of a different captain. Their handlers are **not** the same: ChSlave1's
`5001` turns it into 280686 outright, and their `6001` is the `add_hate_point` + `attack` summon-order
shape rather than a re-pick. Porting those onto Hanuman's adds would have given the Elyos captain the
Asmodian one's fight. The binding table is what separated them, and it is the third time this session
that a message number shared across two encounters needed the `--binding` check before it could be
read at all.

### Verification

Full suite **1,714 passing** and 1 skipped; fifteen new pins; thirteen mutations, all caught after the
lifetime pin below was added. Missing-AI 723 → **722**; adds 426 across 322 → **424 across 321**;
`audit_pattern_guards.py` unchanged at two triaged findings and `audit_ai_messages.py` at seven
unpaired.

The one mutation that survived the first sweep was **the 31–50 wave keeping the first form's thirty
minutes instead of its own twenty**. Every other pin reads a count within a minute of the wave
landing, and at that range twelve hundred seconds and eighteen hundred are indistinguishable. Pinned
by outliving it, which is the only way a `live_time` is observable at all.

## The Silikor of Memory — a Java class with the add mechanic inside out, and a guard loop nobody had

Theobomos Lab's last boss (**214668**) was one of the few on this list that already had a class:
`ai/instance/theobomosLab/SilikorofMemoryAI`, @author Ritsu. Its retail pattern is `ND2_WhG`, and the
two disagree about almost everything the adds do.

### Health phases against a clock

| | aionemu | retail `ND2_WhG` |
|---|---|---|
| when | 50%, 25%, 10% | fifteen seconds in, then every **thirty seconds**, forever |
| how many | **two** — a fragment *and* an essence | **one** |
| which | both, always | a **coin flip** between them |
| where | within two metres | five |
| how long | until the fight ends | **three minutes** |

Six adds in the first three minutes, coming and going, against two at half health that never leave.
Neither the count nor the cadence nor the lifetime survives the comparison, and all five differences
are pinned. This is the sanctioned exception to Java-is-spec, and it is the clearest example of it so
far: the Java author had no access to the pattern and built something plausible.

He also **points**. Every fifteen seconds `6622` goes out fifty metres carrying whoever he is fighting,
and both silikor guards drop what they are doing and go for that player. Those guards were on plain
`aggressive`, which is why the order had nowhere to land.

### The guard loop, which our server did not have at all

`ND2_WhG1` and `ND2_WhG2` are the two guards; `ND2_WhG3` is the sealed akaimum (280973) that walks the
hall above them; `ND2_WhG4` is a marker. Together they make a loop:

1. a guard dies and leaves a **marker** where it fell, for twelve seconds;
2. the marker **shouts `6620` a hundred metres**;
3. the akaimum **stands a new guard of the same kind back on the same post**;
4. the new guard **shouts on arrival** (`6655` melee, `6656` caster) and the only listener is another
   guard of its own kind, which leaves — so re-placement can never stack.

Clearing that hall means killing the akaimum, and nothing in our server said so. On top of it the
caster guard **drops a summon on a random attacker** at a rate that rises as it weakens — one in four
every fifteen seconds while healthy, one in two every ten through the middle, three in four every ten
below thirty — which is the reason to kill the caster first.

Both guards also peel to the **second-most-hated** player below thirty percent, on a rung that re-arms
itself; the pin for that needed a third peel with the hate order turned over twice, because the rung
that *opens* the peel and the rung that *repeats* it are different branches and the obvious pin only
reaches the first.

### Two findings in our own data

**The guards were standing on each other's posts.** `ND2_WhG3` places the caster (280972) at x≈407 and
the melee (280971) at x≈377; our spawn file had 280971 at 407.07 and 280972 at 377.07 — the right two
spots with the ids exchanged. Swapped, so the akaimum's re-placement puts a killed guard back where it
actually stood.

**A hundred-metre shout from a fresh spawn does not carry a hundred metres.** `NpcMessageBus` falls
back to scanning the sender's own map region when its known list is still empty, which is every
`on_wake_up` broadcast — and that fallback is one region wide. Measured here: a marker left on the
melee guard's post is a region away from the akaimum and reached nothing, while the caster's marker,
the same thirty-eight metres off but inside the region, arrived. The marker now waits one idle tick
before shouting, which is a second out of the twelve it has. **Rule: the region-scan fallback is a
courtesy for short broadcasts, not a substitute for a known list — anything shouting past about thirty
metres from a fresh spawn has to wait a tick first.**

### A new condition: `When.SenderIs`

Retail tells the two markers apart with `is_race`, and in the dump that element **carries no argument
at all** — so as written its two branches have identical guards and, first-match-wins, the second can
never fire. The mechanic is unambiguous even where the discriminator is not, so the akaimum reads the
marker's npc id instead. `PatternAi` now records the message sender and `When.SenderIs(npcId)` tests
it. It is our vocabulary, not retail's, and it exists because retail's own is unreadable here.

### Not translated

Eight skill indices and the branches carrying nothing else. The boss's timers 0, 1 and 14, which
between them hold two casts and a flag var whose only reader is the branch below. His `on_spelled`
branch — retail broadcasts `6621` when a *spell* lands on him and that clears the akaimum and both
guards; we have no `on_spelled` event, and putting it on `on_attacked` would make a melee pull clear
them too, which retail deliberately does not do. The akaimum's waypoint work (`goto_waypoint`, the
arrival branch, the return to waypoint 14 when a guard dies within ten metres) — no vocabulary, and it
already carries a walker route. The two guards and the roamer the boss and the akaimum place on
waking, which our spawn file already stands there. And `6621` itself, whose senders are those two
unported events and which would delete statically-spawned NPCs with nothing to bring them back.

### Verification

Full suite **1,725 passing** and 1 skipped; eleven new pins; seventeen mutations, all caught after
three repairs. Missing-AI 722 → **720**; the other audits unchanged apart from `acts` 35 → 36, which is
this encounter's own wiring.

**The three repairs are the same failure three times: a pin that passes without the code.** The order
pin stood two aggressive guards next to the raid, so they found it themselves; the peel pin only
reached the rung that opens the peel; and the caster's drop was counted at the end of a five-minute
run when it lives thirty seconds — the fifth time that one has been made, and `BossAiHarness.Watch`
exists because of the first four.

## Dragon Lord's Refuge — the drakan that explodes twice over, and the fifth grade

Three npcs left in an instance earlier sessions had otherwise finished: Tahabata's drakan (**281259**)
on plain `aggressive`, Calindi's (**281268**) with only its message branch translated, and **Chramati
Firetail** (215284), the only one of the five grades with no class at all.

### One relay, two drakan

`Dragon_G1SlaveDrakan` and `Dragon_G2SlaveDrakan` have **identical timer halves** — branch for branch,
delay for delay — so they are now one shared builder. Above half health a drakan holds whoever is
holding it. Below half, a once-only rung turns it onto a **random attacker** and opens a four-stage
relay: timer 2 hands to 3, 3 to 4, 4 to 5, and the far end turns it again and hands back to 2. The
peel therefore comes once immediately and then about every fifty-three seconds.

**The relay's middle rungs are casts and they are kept anyway.** Each exists only to arm the next;
drop one and the far end never fires, so the peel happens once and never again. That is the
"helper rung" case, and the mutation sweep confirms it — removing a middle rung is caught.

### The exploder asymmetry, ported as written

A drakan leaves an **exploder** behind whichever way it goes, for ten seconds — and the two branches
name **different npcs**. `on_despawn` leaves 281260, the G1 drakan's own; `on_killed_by_user` leaves
**281269, which belongs to Calindi's drakan**. Both are called "exploder", both lv50 ELITE, so nothing
in play distinguishes them; it reads like a copy-paste in NCSoft's data. Ported as written and pinned
as written, so a later reader does not tidy it into consistency. This is the same call the project
makes about Java quirks, applied to retail's own.

Calindi's drakan had the message branch but not the explosion, which matters more there than it looks:
her clear-call is what removes the standing pair, so **calling a fresh pair detonates the old one**
rather than quietly deleting it.

### Two claims in our own comments that were wrong

`CalindiDrakanAI` recorded its combat chain as *"every branch on them is a cast"* — two of the eight
carry `switch_target_by_attacker_indicator` and the rest carry the arms that pace them. And it recorded
`Dragon_G2SlaveDrakanSu` as *"binds to nothing in our 4.8 client"* — it resolves to 281269, which has a
template. Both were the same mistake: **a gap asserted rather than looked up**. Corrected in place, and
the correction is why the explosion and the relay exist now.

### Chramati Firetail

`Dragon_G5` is two timer slots and, apart from casts, one thing: **ten seconds after something engages
it, it turns on whoever is closest to dying**, and then every thirty-five seconds. Retail alternates a
fifteen-second slot and a twenty-second one, which is why the gap is neither number — a translation
that collapsed the two into one would get the cadence wrong in a way no single reading catches.

### Not translated

Four skill indices on the drakan relay — the cast on engaging, the self-buff each peel opens with, and
the two attack skills the relay carries — and three on Chramati, plus its `say_to_all_str` on engaging,
which has no `npc_shouts.xml` row.

### Verification

Full suite **1,735 passing** and 1 skipped; ten new pins; twelve mutations, all caught after two
repairs. Adds 424 across 321 → **421 across 319**. Missing-AI unchanged at 720: neither of these npcs
was ever on that list, which is worth knowing about it — **it ranks patterns by timer count and these
two are small**, so an instance can be finished on that audit and still be missing mechanics. The adds
audit is what found them.

### The two repairs, and a rule for pinning a random choice

Both survivors were rungs hidden behind a longer-running one. The opening peel survived a mutation
because the four-hundred-second window that measures the relay also contains it; it needed its own
fifteen-second window. Chramati's ten-second delay survived because the pin only looked after it had
elapsed; it needed a reading at nine seconds.

The opening-peel pin also runs **six separate fights**. Retail's pick is a random attacker, so a single
short window has a one-in-five chance of landing back on the tank and reading as nothing happening.
**Rule: pinning a random choice takes either a long window or repeated trials, and a long window
measures whatever else is running in it — so for a rung that fires once, repeat the fight.**

## Theobomos Lab's four elemental lords — one shape, and the one that breaks it

Torch Spirit Iprita (**214663**), Wistful Syripne (**214664**), Soul Spirit Nomura (**214665**) and
Water Spirit Undine (**214666**) were all on plain `aggressive` with no class, and between them they
account for **eight** of the adds our server never spawned — the largest unblocked cluster left on
`audit_missing_adds.py`.

### The ladder is a step per band, not a wave

| | |
|---|---|
| on engaging | one **lesser** elemental, five metres out, five minutes |
| below 75 | one **greater**, once |
| below 30 | one of **each**, once |
| on going home | every one of them despawns |

Four standing at the end of a fight walked down through the bands, and never more however long any
band lasts — both rungs carry flag vars. A raid that pushes straight from full health to twenty gets
**three**, not four: the 31–75 rung is out of range and simply never fires. That asymmetry is the
ladder's own and is pinned.

The despawn here is retail's own `on_leave_attack_state`, not our reading of `despawn_at_attack_state`
— worth stating because the Drakan camp summons two entries ago went the other way, and the difference
is that retail declares the handler here and does not there.

### Iprita is the exception, in exactly two places

Three of the four come off the tank onto the **second-most-hated** player at each band crossing, and
below thirty keep turning every fifteen seconds. Iprita turns **once**, at the thirty crossing; her
75-crossing and her deep rung are casts. Her deep rung is still carried, because it re-arms its slot at
fifteen seconds where the fallback gives twenty — a cadence difference, not a no-op.

Two deliberate differences inside an otherwise identical family is precisely what a shared builder
loses. The builder here takes a `peels` flag and the pins are `[Theory]`-driven over all four, with
Iprita held out of the peeling set and given her own two pins instead.

### The audit could not see the fix, and that is now fixed too

`ElementalLordAI` keeps the four bosses' summons in a lookup table

```csharp
Dictionary<int, (int Lesser, int Greater, bool Peels)> Lords = new()
{
    [214663] = (280986, 280987, false), ...
};
```

and passes them into a builder as `Build(e.Value.Lesser, e.Value.Greater, ...)`. The builder does spawn
its own parameters, so `audit_missing_adds.py`'s helper rule fired — but the call site passes a tuple
field, not a constant, so nothing resolved and all eight adds still read as never spawned **after they
were being placed**.

`audit_missing_adds.py` now reads tuple-valued tables the same way it already read records: declared in
the same file, components taken positionally, ids only out of components typed `int`. The count moved
421 → **413** and the encounters 319 → **315**, which is exactly the eight adds and four encounters
this change added and nothing else — the precision check that matters for a widening.

That is the **fourth** indirection this audit has had to learn (a returner method, a local, a record, a
tuple table), and the pattern in all four is the same: **an id that never sits next to a spawn call is
invisible to it.** Worth stating as a habit rather than a fix — when a class reaches its ids through a
table, check the audit still sees them before trusting the number.

### Not translated

Between four and six skill indices per lord and the branches that carry nothing else, including
Iprita's timer 2 (a thirty-second cast loop). The eight summons' own patterns — `ND2_FeJSum` and its
three siblings — are a single cast on a ten- or fifteen-second timer with nothing else in them, so
those npcs stay on `aggressive`; recorded so the gap is not re-opened by someone reading the pattern
list rather than the patterns.

### Verification

Full suite **1,757 passing** and 1 skipped; twenty-two new pins across four bosses; twelve mutations,
all caught after three repairs. Missing-AI 720 → **716**; adds 421/319 → **413/315**.

**All three repairs were pins that read at the wrong moment.** A band widened to full health went
unseen because the first reading was at two seconds and the ladder's first tick is at five. The deep
peel's re-arm went unseen because one firing satisfied the pin. And nothing followed an elemental as
far as its five minutes. The first two are new; the third is the lifetime mistake this suite has now
made six times, and `BossAiHarness.Watch` only helps when you are counting rather than following.

## The krall trappers — twenty-five world NPCs that were supposed to be laying traps

`NKrall_ReA`, `NKrall_ReB`, `NKrall_ReC` and `Nkrall_RhA` bind to **twenty-five npcs our world
spawns** — the kaidan and kishar scouts, loudmouths, lancers, chuckers and Chieftain Kurka across
Beluslan and Morheim. Every one of them was on plain `aggressive`. They lay traps; the traps
(280449–280452) already had an AI; **nothing had ever placed one**.

A trap goes down at the krall's feet when the fight starts and another every twenty seconds. The
heavy trappers add one more rung: below thirty-five percent, **once**, a *powerful* trap — guarded on
`is_distance_shorter_than OBJI_CUR_TARGET distance=6`, so a group killing them at range never sees it
at all. That guard needed a new condition, `When.TargetWithin`, and it is the first branch in this
work that is melee-only.

### `live_time` on a trap is a ceiling, not a duration

The four patterns give their traps wildly different lifetimes — none at all, sixty seconds, fifty
minutes — and it reads like a mechanic. It is not. `NTrapAI` fires the trap's one skill on waking and
removes it when that lands, **measured at about five seconds**, so a trap is a one-shot area effect
wherever it comes from and the lifetime only ever mattered for a trap nobody triggered. Two pins were
written against the difference before it was measured, and both were wrong. Carried as written and
recorded, so the numbers are not mistaken for a mechanic again.

### Two things retail guards twice, and one pin that had to be deleted

The escape rung carries a flag var **and** declines to re-arm timer 0. Either alone limits it to one
firing, because the only other branch on that slot is a bare re-arm — so **removing either guard
changes nothing we can measure**. A pin was written for the missing re-arm, it passed for the wrong
reason, and it was deleted rather than contorted; the flag var is left as a deliberate mutation
survivor. Both guards are carried. The redundancy is presumably retail's: there the krall runs after
laying it, and the dead clock is what stops it turning round to try again.

**Rule: when two guards enforce the same limit, at most one of them is pinnable — say which, and stop.**
A pin that cannot fail is worse than no pin, because it reads as coverage.

### The largest piece of vocabulary we are still missing

Every one of these patterns ends its escape or its trap loop with `flee_from`, and answers
`on_stop_to_flee` when it stops running. We have neither. Counted across the 5.8 dump:

| action | uses | patterns |
|---|---|---|
| `goto_waypoint` | 1,112 | — |
| **`flee_from`** | **353** | **226** |
| `random_move` | 187 | — |
| `on_stop_to_flee` | 138 | — |

After waypoints it is the single largest gap, and unlike waypoints it is not obviously out of reach:
`AIState.FEAR` already exists and `EffectController.IsUnderFear` already drives player movement. What
is missing is an NPC flee that the AI can start deliberately rather than as a debuff, and a
`stop_to_flee` event when it ends. **The blocker for translating it is testing, not writing:** the boss
harness has no geodata and no visibility, so nothing about where an NPC runs to can be pinned there.
Recorded with its size so the next attempt starts from what it buys.

### Also not translated

Two skill indices per pattern; the `say_to_all` lines, which have no `npc_shouts.xml` row; the `1001`
broadcast on stopping fleeing, unreachable for the same reason as the flee; and the **`6199`** listener
on the scouts and Kurka — a trap telling the krall who tripped it, whose only retail sender is pattern
`D2_Trap`, which binds to no npc our world places.

**`D2_Trap` is very probably these four traps' own pattern**, and it is worth writing the evidence down
rather than losing it: its shape is `on_wake_up` → broadcast `6199`, cast, `despawn_self`, which is
`NTrapAI`'s shape plus the broadcast; the traps are already on `ntrap` by somebody's earlier judgement;
and the only listeners for `6199` anywhere in the dump are exactly the krall patterns above. Three
strands agreeing is usually enough — but the binding table does not resolve it, and wiring a mechanic
on an inference is how the `_Source`/`_Change_Failed` mistake happened earlier in this work. Left as a
lead.

### Verification

Full suite **1,766 passing** and 1 skipped; nine new pins; twelve mutations, eleven caught and one
deliberate survivor. Adds 413 across 315 → **397 across 307**; missing-AI unchanged at 716, which is
the same story as the Dragon Lord's Refuge entry — these patterns are small and that audit ranks by
timer count.

Two of the three repairs were the lifetime mistake again, in a new disguise: a trap that fires and
vanishes cannot be counted on the ground at all, so `AnUnpulledKrallLaysNothing` passed for a mutation
that made an idle krall lay one. That is the **seventh** time in this suite. The third was a threshold
pin that only read at full health, which a `below` guard cannot fail — brackets now come from above as
well.

## The drakan magisters, and a barrier that is a hazard engine

Seven bosses on plain `aggressive` across two instances — four drakan magisters in Tiamat's Stronghold
(219370, 219375, 219386, 219399) and three terath magisters in the Dreadgion (Captain Anusa 233371,
the thaumaturge 233354, the worldwarper 233358) — plus the **great magical barrier** (282984) they
drop, which had a pattern of its own that nobody had read.

### Tiamat's Stronghold: two hands, and only two

The four Stronghold patterns are identical branch for branch. Ten seconds into the fight a slot starts
ticking; the first time it finds the mage below eighty percent it puts a **mystical tyrhund** (282989)
at its own feet for a minute, and the first time below thirty it does it again. Both rungs carry a flag
var and the slot keeps ticking either way, which is what makes the second hand land promptly rather
than at whatever moment the crossing happens to be noticed.

**A near miss worth recording.** Three of the four first read as having *no* flag vars — which would
have meant a hand every seven seconds from eighty percent down, a completely different fight, and a
"deliberate difference in an otherwise identical family" entry in this log. It was a scratch `grep`
filter dropping the `set_flag_var` lines. The rule was already written down here — *a scratch regex is
fine for finding candidates and not for stating facts* — and it earned its keep: the false finding was
one filter away from being published as retail's.

### The Dreadgion: a barrier on somebody every fifteen seconds

Every fifteen seconds a magister drops a **great magical barrier** on a *random attacker* — eight
seconds of life, `attack_target_after_spawn` with a single hate point. Below thirty a **hand** lands on
the tank, once: that rung does not re-arm its slot, so the six-second clock carrying it is over as soon
as it fires. Unlike the krall escape one entry ago this one *is* observable, because there is no flag
var doing the same job — remove the missing re-arm and hands rain every six seconds. Both cases are now
in the log, which is the point: the same construction is load-bearing in one pattern and redundant in
another, and only reading the rest of the branch tells you which.

**Captain Anusa clears up on waking; the other two on dying.** That is the only difference between the
three patterns, and it means a second pull of Anusa starts clean rather than the first kill tidying
after itself. Ported as written.

### The barrier is a pulse, not a debuff

`IDYun_Temp_65` is the barrier's own pattern and it had never been looked at. One second after
something engages it, and every two seconds after that, it leaves an **invisible** copy of itself
(282985) where it stands for two seconds — so the ground under it is re-hazarded continuously for the
eight seconds it lives, and `on_despawn` clears the copies rather than letting them outlast it. That is
how retail builds a standing area effect out of npcs instead of an aura, and it is worth knowing
because the same shape will turn up again.

### Two things carried but not observable

`attack_target_after_spawn` on the barrier changes nothing we can read: a barrier is an aggressive npc
landing on top of a player, so it engages that player whether or not a point of hate was seeded first.
Left as a deliberate mutation survivor. What *is* pinned is the **random attacker**, measured by which
player each of ten barriers landed nearest to — position, because nothing else separates it from the
tank.

`IDDreadgion_03_DrakanWi_Vil_60_Ae` (233350, terath magician) is recorded as **not a gap**: its whole
pattern is casts and re-arms with no spawn in it at all.

### A third instrument for the same recurring mistake

`BossAiHarness.Watch` exists because pins that count summons at the end of a phase measure the lifetime
rather than the rung. These pins have several phases each, and hit the mirror of that: a summon that
survives into the next window is **counted again**, so "nothing more arrived" reads as one arrival and
a correct translation fails. `BossAiHarness.WatchNew` ignores whatever was already standing when the
window opened.

**Rule: a single-window pin counts what is standing at the wrong moment; a multi-window pin counts the
same thing twice. Use `Watch` for one window and `WatchNew` for more than one.** Between them these two
mistakes have now cost nine repairs across this suite.

### Verification

Full suite **1,776 passing** and 1 skipped; ten new pins; fifteen mutations, fourteen caught and one
deliberate survivor. Missing-AI 716 → **709**; adds 397/307 → **390/303**.

### Not translated

Three skill indices on each family — the Stronghold mages' area attack and heal-reduction loops, and
the Dreadgion ones' stumble attack, ranged attack and area fire — and the cast the barrier opens with.
Retail's `set_idle_timer delay=0` in the barrier's despawn branch, which our runtime does on reset
anyway.

## The Abyss undead — killing one is a coin flip, and it lands on you

Eight retail patterns (`AD2_UnDeadFi_Da`, `_Fi_Li`, `_Pr_Da`, `_Pr_Li`, `_Ra_Da`, `_Ra_Li`, `_Wi_Da`,
`_Wi_Li`) bind to **twenty-one spawned npcs** — the eternal and immortal warriors, fencers, guards,
mages and healers of the lower Abyss. All twenty-one were on plain `aggressive`. All eight patterns are
identical in the part that can be translated, and it is one branch:

> **Half the time, killing one leaves a *fear* (290137) standing on the player who brought it down, for
> six minutes.**

Not near the corpse and not on the tank — on the killer, forty metres away if that is where the killer
was standing. It is what makes clearing a field of them a decision rather than a chore, and it is the
kind of mechanic that is invisible in a spawn table because the add belongs to no encounter.

### New vocabulary: `OBJI_KILLER`

`spawn_on_target target_obj=OBJI_KILLER` had never come up in this work, and the pattern runtime had no
way to say who had killed anything. `PatternAi.Killer` now reads `AggroList.GetMostPlayerDamage()` —
**most damage, not most hate** — because that is the lookup the rest of the server already treats as
the killer; it is the same one loot ownership uses. Reading it as most-hated would have handed the fear
to the tank in every group, which is the opposite of the mechanic, and that mutation is one of the six
the sweep catches.

`BossAiHarness.Wound` came with it. `Rehate` only moves the hate list, and a pin for a death branch that
spawns on its killer finds nobody unless damage is on the list — the branch would silently do nothing
and the pin would read as coverage.

### Pinning a coin flip, again

Twenty separate kills per pin, counting how many left a fear, and asserting the count is neither near
zero nor near twenty. The same shape as the drakan's opening peel two entries ago and for the same
reason: **a probability branch cannot be pinned by one trial, and a long window measures whatever else
is running in it.** Repeated fights are the instrument for a rung that fires once.

### Not translated

Two skill indices: the cast on engaging, and a self-cast on being hit or spelled below thirty-five
percent — a fifty-percent, once-a-fight reaction whose entire content is that cast. The `say_to_all` on
the death branch, which has no `npc_shouts.xml` row.

And the branch's **`is_race` guard**, which appears in the dump with no argument at all — the same
unreadable element recorded against the sealed akaimum. There the mechanic was inferable from the
sender's npc id; here nothing recovers it, because the Elyos-side and Asmodian-side patterns spawn the
*same* npc, so whatever it distinguishes is not the summon. Dropped rather than guessed at, which means
our version fires for any killer. **That is a widening, and it is deliberate: a guard whose argument is
not in the data cannot be honoured, and inventing one would be our mechanic rather than retail's.**

Retail also declares no spawn group (`SPAWN_ID_NONE`), so nothing ever clears these as a set — the six
minutes are the only thing that removes them. Carried as written.

### Verification

Full suite **1,781 passing** and 1 skipped; five new pins; six mutations, all caught. Adds 390 across
303 → **382 across 295**; missing-AI unchanged at 709, for the usual reason — these patterns have no
battle timers at all, and that audit ranks by timer count.

## The Balaur officers who put a trap under you

`Dread_XDrakanReA`, `XDrakan_ReB_50` and `Dread_SurkanaNm06` bind to **eight spawned npcs** across the
Dredgion and Dark Poeta — Baranath Triaris, Auditor Nirshaka, Sentinel Garkusa, Prison Guard Mahnena
and the four Anuhart officers. All eight were on plain `aggressive`. The three patterns differ only in
their cast loops; the translatable part is identical in all three.

| | |
|---|---|
| ten seconds in | it turns on the **second-most-hated** player — once, because the timer carrying it is armed on entering combat and never re-armed |
| crossing 70% | a **dragon's trap** (281161) goes down **on whoever it is fighting**, five metres out, and it peels again |

So a fight has exactly two peels and one trap, and a raid pushed through seventy inside ten seconds
still gets both. The trap landing on the *player* rather than at the officer's feet is the part that
makes it a mechanic: a tank that has just been peeled off is standing on it.

### Carried but not observable, from the other direction

The band rung also re-arms the six-second clock. Once its flag var is consumed there is nothing else on
that slot but the bare fallback, so a dead clock and a live one behave identically. This is the krall
escape's situation reached the other way round: **there two guards enforced one limit; here one guard
makes the other pointless.** Deliberate mutation survivor, and now the second entry of this kind — the
pattern is worth naming. **Rule: before pinning a "the clock stops here" claim, look at what else is on
that timer slot. If the answer is "only the re-arm", the claim has no consequence to pin.**

### Three events we do not raise

Between them these carry the rest of the patterns: `on_see_friend_attacked` and `on_friend_spelled`,
each turning the officer onto whoever touched its neighbour, once a fight; and
`on_enter_abnormal_state`, which broadcasts `3403` or `6836` ten metres when the officer is
crowd-controlled. That last one is a genuine mechanic — a stunned Balaur calling its friends — and it
is doubly unreachable: no event to raise it, and no listener for the message in our data either.

### Also not translated

Seven skill indices and the branches that carry nothing else — the 76–100 and 36–70 cast loops on
timer 2, the below-35 chain across timers 3, 4 and 5, and Garkusa's extra 90–100 loop on timer 4. The
`say_to_all` lines, which have no `npc_shouts.xml` row.

## The adds audit learns its fifth indirection

`audit_missing_adds.py` walks a spawn call's whole argument list for **literals**, but harvested
**names** with `SPAWN_CALL + r"\s*(\w+)"` — the first token only. `Do.SpawnOnAttacker(which, npcId, …)`
puts the aggro indicator there, so the id was never seen, and the Dreadgion magisters' great magical
barrier read as never spawned **while three bosses were dropping one every fifteen seconds**.

Same paren walk, collecting identifiers instead of digits. It is safe to be greedy: a name only
contributes if it resolves to a `const int` holding a five- or six-digit value. The diff is exactly six
rows and all six are correct crediting — 281025 (the silikor caster's summon), 282984 ×3, 281424
("shatter"), 282444 (Xasta's trap) — nothing else moved.

That is the fifth: a returner method, a local, a record, a tuple table, and now an argument position.
**The habit stands: when a class reaches its ids by any route other than a literal first argument,
re-run the audit and check it still sees them.**

### Verification

Full suite **1,789 passing** and 1 skipped; eight new pins; ten mutations, nine caught and one
deliberate survivor (plus one mutation replaced after it turned out to be a no-op — arming a battle
timer on `on_wake_up` does nothing at all, because battle timers only fire in combat). Adds 382 across
295 → **373 across 286**; missing-AI 709 → **707**.

## `flee_from` — the largest missing piece of vocabulary, now translated

Recorded two entries ago as the biggest gap after waypoints and **blocked on testing**: 353 uses across
226 patterns, 138 `on_stop_to_flee` handlers, and a boss harness with no geodata and no movement to
observe it with. That reasoning was half right and the half that was wrong is the interesting part.

**Retail specifies a duration, not a distance.** The element carries only `<from>` (invariably
`OBJI_CUR_TARGET`), `<seconds>` — one to five — and `<push_state>`. How far an NPC gets is its own run
speed times the time. So there is nothing about *where it ends up* to translate, and the two things
the pattern does say — **which way it aims** and **when it stops** — are both readable without moving
anything. `PatternAi.Flee` computes the point directly away from the current target at
`GetMovementSpeedFloat() × seconds`, hands it to the move controller, and schedules the stop.

**Rule: "we cannot test it" is a claim about what the mechanic specifies, not about the harness. Read
the element's arguments before concluding it needs movement — half the actions that look like movement
specify a timer and a direction.**

### `on_stop_to_flee` was the point of it

An NPC that runs is not simply out of the fight for three seconds. Across the 5.8 files these handlers
carry **71 broadcasts, 69 shouts and 66 casts** — a boss comes back shouting for help, or onto whoever
is weakest. `AiPattern.OnStopFleeing` is the event; the krall are the first users of it, and their
`1001` shout came out of the message audit as **acting** rather than `no audience`, so it lands on
something.

### What the krall do now

| | |
|---|---|
| heavy trappers | lay the powerful trap, back away **five seconds**, turn round and shout `1001` fifteen metres naming their quarry |
| scouts | back off **two seconds** after every trap, and come back onto whoever is closest to dying |
| Chieftain Kurka | the same, **three seconds** |

That is the half of those patterns the previous entry recorded as unreachable, and it is a large part
of what makes a krall camp feel different from a pack of monsters: they lay, they back off, and they
come back for the weakest.

### Not translated

`push_state`, which restores an AI state ours never leaves — our NPC keeps its hate list and its timers
throughout, and the move controller is simply told to stop when the clock runs out. And the `<from>`
argument is not honoured as a general case: every one of the 353 uses names `OBJI_CUR_TARGET`, so
`Flee` runs from the current target and nothing else. If a pattern ever names something different that
is a widening to make then, not a guess to make now.

## The adds audit learns its sixth indirection

`RagingKraterrAI` holds its three faithful servants as constants and builds its whole fight with
`ElementalSummonerPattern.For(FirstWave, SecondWave, ThirdWave)` — a builder declared in
*another file*. The same-file helper rule cannot see it, so all three read as never spawned **while the
class had been placing twelve of them a fight**. `audit_pattern_guards.py` and
`audit_retail_messages.py` were both taught to follow shared builders; this one never was.

Now indexed across every handler file and qualified by type (`Type.Method`), so a common method name
like `For` cannot credit an unrelated class. The diff is exactly five adds across two encounters — the
Kraterr trio and two of Frostmane Lestin's waves, which had additionally been carrying a "we spawn
280489 for this role" flag that was simply wrong. Nothing else moved.

Six indirections now: a returner method, a local, a record, a tuple table, an argument position, and a
cross-file builder. **Every one was found by noticing a number that should have moved and did not.**
That is worth stating as the practice: after any change that adds spawns, read the audit's delta and
check it equals what you added — not that it merely went down.

### Verification

Full suite **1,792 passing** and 1 skipped; three new pins for the flee; seventeen mutations on the
krall, sixteen caught and one deliberate survivor. Adds 373 across 286 → **368 across 284**;
missing-AI unchanged at 707.

## A new audit: what is worth doing next

The audits so far answer "what is missing". `tools/client-extract/audit_translatable.py` answers a
different question — **how much of an unported pattern could we actually write** — and it had been
eyeballed rather than measured. The adds audit ranks by adds, and that is a poor proxy twice over: a
boss can carry a rich mechanic with no spawn in it at all (the Balaur officers' peel, the elemental
lords' band ladder), while a pattern with three spawns can be nothing but waypoint furniture.

It counts each pattern's actions against our vocabulary and names the blockers separately, because they
are blocked for different reasons: **skill** (`SKILLI_INDEX` against a per-npc list the dump does not
carry), **shout** (a client string id with no `npc_shouts.xml` row), **path** (`goto_waypoint`,
`random_move`), and **script** (instance-progression verbs that belong to a handler). Only patterns
whose owners our world actually spawns *and* which still sit on a stock AI are listed.

Current state: **956 unported patterns with at least six translatable actions, and 4,899 npcs behind
them.** The first thing it found was the pattern this entry is about — the only one in the dump with a
completely clean sheet.

## The Unstable Triroan — a clock that speeds up, and a controller that chooses

`ND2_FhXSum2` scored **twenty-eight translatable actions and nothing blocked at all**, on npc 280983,
which our spawn file already stands in the Triroan's room. It is the room's summoner.

### The boss says how many; the controller says which

The Triroan broadcasts one of three numbers a hundred metres — `6610` for one elemental, `6611` for
two, `6612` for three — and the controller picks *which*, from fire, water, earth and air, and puts
them down where it stands for thirty seconds. Every combination is a branch: four for the single, all
six unordered pairs, all four triples.

**The chains look uniform and are not.** Retail evaluates `test_probability` *before* `is_message` and
takes the first branch that passes, so 25/25/25/fallback is 25%, 19%, 14% and **42%** — not four
quarters. The six-way pair chain runs 17% five times and leaves **39%** on the last pair. Written in
retail's order with the guards in retail's order, so the weighting falls out of the structure rather
than being asserted. **Rule: a chain of probability branches is a decay, not a distribution — the
fallback is always the most likely outcome.**

### What this replaces

The Java class (`ai/instance/theobomosLab/UnstableTriroanAI`, @author Ritsu) had eleven fixed health
phases — 99, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5 — each spawning a hard-coded list of elementals
itself. Retail has **one summon slot whose count and clock both track the band**: one elemental in
61–80 and 41–60, two in 21–40, three below 20, and the interval tightening as it goes. Sanctioned
exception, and the second Theobomos Lab class to need it.

**The reading was confirmed by a coordinate.** Our spawn file stands the controller at
602.17 / 488.805; the Java class hard-coded its own spawn point as 601.966 / 488.853. The same spot —
the Java author had found where the elementals appear without knowing what put them there.

### Two corrections the sweep forced

**The interval is not the one written on the summon branch.** Retail arms that slot two ways: the
branch re-arms it at its band's figure, and the band timer pokes it three seconds after each of its own
twenty-second ticks. The poke always lands first, so in the upper bands the cadence is about *twenty*
seconds and the branch's own re-arm never fires. The first draft of this class said "every thirty
seconds" in its remarks and its pin agreed with it; the mutation that removed the re-arm survived, and
that is what exposed it. **Rule: when two timers can arm the same slot, the cadence is the shorter one
— read both before writing the interval down.**

**And a raid that skips straight to the last third waits for it.** The chain that arms the summon slot
starts at the 61–80 step, so dropped in at fifteen percent the king takes about thirty-three seconds to
make his first call. Pinned as the real numbers rather than the ones the branch delays suggest.

### A correction to a shipped class

The Java class set each elemental's walker to `"3101100002"`–`"3101100005"` and started it walking.
**Those route ids are not in our data.** Theobomos Lab has eighteen walker templates and every one is a
SHA-style id; numeric ids exist elsewhere in `npc_walker` but not these. So the walk never began, the
elementals never arrived, and `TriroansSummonAI`'s helper-skill mechanic — which fires on arriving at a
numbered step — has never run in this port. Routing the spawns through the controller loses nothing
that worked, and the dead mechanic is now recorded rather than assumed working.

### The next big piece of vocabulary, measured

`pathname` on a spawn — walk this summon in along a route — appears **1,726 times across 282 patterns**,
larger than `flee_from` was. The mechanism is easy: `GetSpawn().SetWalkerId(id)` then
`WalkManager.StartWalking`, which two shipped classes already do. **The blocker is the mapping**, and it
is the same shape as `SKILLI_INDEX`: retail names routes like
`BIDLF2A_SummonBabyElemental_50_n_Path_00`, our data names them by hash, and nothing in either connects
them. Adding a `walkerId` parameter with no resolvable mapping would be vocabulary with no user, so it
is recorded and not built.

### Verification

Full suite **1,803 passing** and 1 skipped; eleven new pins; thirteen mutations, twelve caught and one
deliberate survivor. Adds and missing-AI unchanged — the elementals were already being spawned, by the
class this replaces, which is exactly why neither audit could see that the mechanic behind them was
wrong.

## The two Heiron watchers — one summons, one commands

`audit_translatable.py`'s next two picks, and they are a useful pair because they fail in opposite
ways. Both were on plain `aggressive`.

### Bulwark Jeshuchi (212282) — a wave that grows

`ND2_KeD`, twenty-six translatable actions against ten casts, the best ratio left with a spawn in it.
Three **disciples of Jeshuchi** (280758) on the first clock tick, **four** on crossing seventy,
**five** on crossing thirty-five, ten metres out and thirty minutes each. Each step also turns him off
the tank — the first two onto the **third-most-hated** player, the last one onto whoever is **closest
to dying**, which is the escalation that matters more than the extra disciple. He clears them on both
exits, because retail declares the despawn on `on_leave_attack_state` *and* on `on_killed_by_user`.

Both of his broadcasts are **not** sent. `6191` and `6192` reach only the disciple's own pattern, whose
handlers are a cast and a two-second timer leading to a cast — nothing we can express, so sending them
would be noise. Recorded as cast-only rather than left looking like a gap.

### Watcher Zapiel (212283) — a commander whose reinforcements we cannot place

`ND2_KeE` is the mirror. Every band step — at eighty-one, at eighty, at fifty-five — he broadcasts
`6190` fifty metres carrying whoever he is fighting, and every **disciple of Zapiel** (280760) in range
drops what it is doing and goes for that player, while he turns onto the third-most-hated himself.
Below thirty he stops stepping and starts repeating: `6189`, the same order, roughly every thirty-two
seconds for the rest of the fight — and that rung does not re-arm the ladder, so there are no more band
steps however long the fight lasts.

**His spawns are not translated, and they are the clearest case yet for the walker gap.** All four band
steps place disciples with `SPAWN_LOCATION_WAY_POINT_START` and a `pathname` — `E3_Cheru3_1` through
`_4` — meaning "at the start of that route, then walk it". We have neither the location kind nor the
route mapping. What saves the encounter is that our spawn file already stands disciples around him, so
the orders land on real cherubim; what is missing is the reinforcement.

**Rule, and it is the useful half of this entry: a blocked spawn does not block the mechanic.** Zapiel's
value to a raid is not that four more cherubim arrive, it is that the ones already there stop hitting
the tank. That half needed no vocabulary we did not have.

### Retail's two orders differ by one action and ours cannot tell them apart

`6190` is `add_hate_point` + `attack_most_hating` + `switch_target`; `6189` is `add_hate_point` +
`switch_target` with no attack. `Do.HateMessageTarget` does hate-then-attack, so the second comes out
very slightly stronger than retail wrote it. An aggressive cherubim that has just switched target was
going to attack anyway, which is why this is accepted as a widening rather than worked around — and
recorded so it is not mistaken for a translation error later.

### The pin that would not fail, and why

Removing the order from one of Zapiel's three band steps survived the sweep twice over. The first
version stood the disciple beside the raid, where an aggressive cherubim finds a player without being
told. Moving it forty metres out did not fix it either: the disciple still joined, because it sees its
friend attacked. What works is a **decoy** — the disciple gets its own player next to it, takes that one
unprompted, and only the order moves it to Zapiel's quarry.

That is the third time in this suite an aggressive add has made an order pin pass on its own (the
Dreadgion barrier and the silikor guards were the others). **Rule: to pin "X was told to attack Y",
give X something else it would have attacked. Distance is not enough — an add that can see the fight
will join it.**

### Also fixed: a pin measuring nothing

`TheLastStepTakesTheWeakestRatherThanTheThird` healed the whole raid every tick, so at the moment the
below-thirty-five rung fired everybody was at full health and "closest to dying" was whatever the aggro
list happened to return. The wounded player is now left wounded.

### Not translated

Ten skill indices on Jeshuchi and fifteen on Zapiel; Zapiel's `6191` per-band cast loops, cast-only at
the listener like Jeshuchi's; and the waypoint-start spawns above.

### Verification

Full suite **1,814 passing** and 1 skipped; eleven new pins; thirteen mutations, all caught after four
repairs. Missing-AI 707 → **705**; translatable 956 → **953**. Adds unchanged: Jeshuchi's disciples were
already on the spawn list through their static placement, and Zapiel's reinforcements are the blocked
half.

## The cheapest work in the project, and nothing was looking for it

Retail ships an encounter as **several npc ids** bound to one pattern — a normal-mode boss and a
hard-mode one, an Elyos copy and an Asmodian one, three difficulty variants of one room. Translate one
of them and the others keep whatever their template said, which is almost always `aggressive`. Nothing
in this project was looking for that, and it turns out to be the cheapest fix available: the class
already exists, already matches the pattern, and already has pins.

**Macunbello is the case that prompted it.** `MacunbelloAI` has been a complete translation of
`IDCT_Boss_LichKing` for some time, and **three live HERO copies of the same boss** — 216734, 216735,
216737 — were still fighting as plain monsters beside it.

`tools/client-extract/audit_orphan_siblings.py` reports these, and it is deliberately conservative,
because a false positive here means a *wrong* fight rather than a missing one:

- **narrow patterns only.** More than eight binders and it is a generic behaviour shared by unrelated
  monsters, not one encounter. `D2_FnA` alone would otherwise report 994 "orphans" with nothing to do
  with each other.
- **one bespoke class only.** If the siblings already run two different classes the pattern is being
  specialised on purpose, and nothing here can say which one is right.
- **stock means stock.** The generic set includes the semi-generic helpers — `servant`, `summoner`,
  `noaction`, `simple_abyssguard` — because sharing one of those with a sibling says nothing at all.

It found **32 npcs across 23 patterns**. Twenty-two of them were repointed this pass.

### What was checked before repointing

Every hit still needs reading: a class may key on its own npc id, and the sibling may be exactly the
variant that check excludes. Two of the candidate classes do use `GetNpcId()`, and both turned out to
be safe for different reasons — `MacunbelloAI` uses it only to pick the hard-mode band table, and the
three siblings bind to the *normal* pattern; `MonolithicAmbusherAI` uses it to identify a **different**
npc it pulls, not itself. The other eight classes never look at their own id.

Repointed: three Macunbellos; six Danuar frost summons and their four `85xxxx` hard-mode twins; three
drakan priests; and one each of the pazuzu worm, the Vasharti assassin, the Nidalber balaur, the
monolithic ambusher, and two reian prisoners.

Pinned by **spawning** each one and asserting the AI object's type rather than by comparing the
template string — it is the registration path that matters, and a mistyped `ai=` name resolves to
nothing rather than failing loudly.

### The ten left, and why each needs a decision rather than a sweep

| npc | pattern | class it would take | why it is being left |
|---|---|---|---|
| 297189 ahserion | `Gab1_Sub_Boss` | `ahserion` | a second Ahserion, and `AhserionRaid` drives the first through a service; repointing without reading that service could start a raid twice |
| 236297 captain xasta | `IDYun_Nmd3_FallOff` | `captain_xasta` | the pattern name says *fall off* — likely the scripted retreat copy rather than the fight |
| 216883 quartermaster nupakun | `Dread02_SurkanaNm06` | `takahan` | a different named npc on a shared pattern; the class carries Takahan's own drop behaviour |
| 296495 hora akacha, 296491 lady pasiphae | `Gw*Guard_FlA` | `gateway_guard` | both LEGENDARY named bosses sharing a pattern with the guards; the class is the guards' |
| 282140 padmarashka | `DF4_DramataSumD` | `rock_slide` | Padmarashka herself on a rockfall summon's pattern — almost certainly a data quirk worth reading first |
| 799363 raeyena, 209472 baltasar hill field gun, 700169 klaw spawner, 230995 captain rata | — | — | one-off scripted npcs whose classes are instance-driven |

**Rule: this audit finds candidates, not conclusions. A shared pattern is evidence that two npcs do
the same thing; a shared *name* or a shared *role* is what confirms it.** The twenty-two taken this
pass all had both.

### Verification

Full suite **1,836 passing** and 1 skipped; twenty-two new pins; no mutation sweep, because nothing
was written — this is a data change against classes that already carry their own sweeps. Missing-AI
705 → **700**; orphan siblings 32 → **10**.

## The anuhart casters — four pets, and an order that never stops

`XDrakan_EeB_F_50` binds to the four Dark Poeta casters — spiritlord (215249), invoker (215258),
conjurer (215267) and transporter (215276) — all on plain `aggressive`, and picked by
`audit_translatable.py` for the best owner count against blocked actions left on the list.

Each of them fights **with a pet**. On engaging it puts a faithful subordinate (281171) at its own feet
and broadcasts `3406` naming whoever it is fighting; nine seconds in it does it again; crossing seventy
it does it again and takes the second-most-hated itself; and below thirty-five it settles into a loop
that re-points the pet roughly every twenty-seven seconds for the rest of the fight. Retail even
shrinks the shout radius as the fight goes on — fifteen metres, then thirteen, then ten.

**The order is the mechanic, not the pet.** One extra monster is a detail; a pet that is moved onto the
healer every time the caster changes its mind is why these four are dangerous in a group.

### One rule, two encounters that want opposite things from it

The caster spawns the pet and broadcasts **in the same branch**, and `PatternAi` deliberately excludes
whatever the running branch spawned from that branch's own broadcast. That exclusion was *measured*,
for RM-56c, which lays traps and immediately tells traps to leave — without it the boss deleted the
arrangement it had just laid.

Here the same rule means the pet does not hear the order it arrives with, and stands idle until the
nine-second one. **The two encounters want opposite things and nothing in the runtime distinguishes
them.** Our measured behaviour is kept rather than widened on a guess, and the evidence on both sides
is worth writing down, because it is the kind of thing that gets "fixed" later without it:

- *for the pet hearing it:* `XD_EPet` has two `3406` branches split on whether the pet is already
  fighting, and the idle branch only makes sense if orders can arrive before it has a target;
- *against:* retail's spawn action is `PLANNED` — queued — so the pet may genuinely not exist yet when
  the next action of the same branch runs, which is exactly what the RM-56c note concluded.

Nothing in the dump settles it. Recorded as an open question with a named test either way.

### Three pins rewritten, and what each was claiming falsely

**"He re-points the pet at whoever he turns on."** He does not: the rung broadcasts *before* it
switches, so the pet gets the player he was holding and he moves on. The first version asserted they
ended up together.

**"…and they end up on different people."** Also false, and the second thing this pin asserted. The
switch on that rung is `ATTACKERI_RANDOM_ONE`, which can land back on the same player. What the branch
order actually guarantees is only that the pet gets the *pre-switch* victim, and that is what is
pinned now.

**"The loop brings the pet back to his victim."** Failed two runs in five with a raid of three, because
the caster's own target drifts back to the most-hated between orders, so the next order can name the
very player the pin had just moved the pet to. Narrowed to one player in the fight and a decoy a
hundred metres away.

**Rule: when a branch both broadcasts and switches, read the order of the actions before naming who
ends up where — and if the switch is random, do not assert a difference it is not obliged to produce.**

### Carried but not observable

The shrinking shout radius. The pet stands at its master's feet and our harness has no movement, so
every radius reaches it; it would matter in the live game to a pet that had chased somebody out past
ten metres. Deliberate mutation survivor.

### Not translated

Eleven skill indices and the two cast-only timer loops. The `3407` broadcast, whose only listener
answers with a self-cast — its rung is kept anyway, because the timer it arms is what paces the order
loop. And `on_enter_abnormal_state`, which broadcasts `3403` when the caster is crowd-controlled: an
event our runtime does not raise, and **the third pattern in this log to want it**. That message has
**twenty-seven** listener patterns across the dump, which makes it the most-wanted single event we are
missing after the friend-attacked pair.

### Verification

Full suite **1,846 passing** and 1 skipped; ten new pins, checked five times over for flakiness after
two of them turned out to be racy; twelve mutations, eleven caught and one deliberate survivor. Adds
368/284 → **367/283**; translatable 953 → **941**.

## `on_enter_abnormal_state` — measured, and not worth building. A correction.

Three entries in this log call this "the most-wanted single event we are missing", on the strength of a
listener count. That framing was wrong, and this entry withdraws it.

### What it would cost

Almost nothing, which is why it looked attractive. `EffectController.SetAbnormal` already notifies
observers and is the obvious place to raise it; `AbnormalState` already carries compound values that
map onto retail's groups without inventing anything:

| retail group | uses | ours |
|---|---|---|
| `ABNSTATEI_CANNOT_ACT_GROUP` | 74 | `CANT_ATTACK_STATE` |
| *(no guard — any state)* | 36 | — |
| `ABNSTATEI_PHYSICAL_GROUP` | 30 | **nothing** |
| `ABNSTATEI_STUN_LIKE_GROUP` | 6 | `ANY_STUN` |
| `ABNSTATEI_SLEEP`, `_SANCTUARY`, `_ROOT`, `_BLEED`, `_POISON` | 23 | direct |
| `ABNSTATEI_MENTAL_GROUP` | 80 (+18 paired) | **nothing** |

Two of the groups have no counterpart in our data — the same shape as `SKILLI_INDEX` and the walker
route ids — but the rest would cover roughly 130 of the 272 handlers.

### What it would buy, which is the part nobody had measured

**272 handlers across 272 patterns, and 245 of them are a single `broadcast_message`.** So the whole
event is "I have been crowd-controlled — tell the room". The question that matters is therefore not how
many patterns *have* the handler, it is what the listeners *do*. Nineteen distinct message numbers are
broadcast this way, and the five that carry it are:

| message | senders | what its listeners do |
|---|---|---|
| `3403` | 64 | `add_hate_point` at the message's object, then attack |
| `23003` | 59 | cast, and nothing else |
| `10001` | 56 | spawn, hate, attack, cast — genuinely mixed |
| `6836` | 36 | cast and arm a timer |
| `1022` | 9 | cast, and nothing else |

**And `3403` — the largest — is a no-op in our engine.** Its senders broadcast with
`param_obj=OBJI_SELF`, so the object the listeners hate is *the stunned npc itself*. Adding hate toward
a same-tribe ally does nothing here, for the reason recorded much earlier in this log: our aggro list
only offers a valid **enemy** as most-hated. Whatever retail means by it — most likely making the room
converge on the stunned one's position — is not something the action says.

Add up what is left: two of the five biggest messages are cast-only, the biggest is inert for us, and
`10001`'s listener actions are aggregated across every sender of a very generic number rather than the
abnormal-state ones. **The event is cheap to raise and would light up almost nothing we can express.**

### The three bosses that wanted it

`XDrakan_ReB_50` and `XDrakan_EeB_F_50` send `3403` — inert. `Dread_XDrakanReA` and `Dread_SurkanaNm06`
send `6836` — cast-only, already recorded as such. So all four of the families ported in the last three
entries would gain nothing from it, which is the opposite of what those entries implied.

### The rule this is really about

`flee_from` was recorded as blocked and turned out to be cheap once its *arguments* were read. This is
the mirror: an event recorded as valuable on a count of handlers, which turns out to be nearly empty
once its *listeners* are read. **Rule: the size of a gap is the size of what it unlocks, not the number
of places it appears. Count the receiving end before promising anything.**

Left unbuilt, deliberately, with the measurements above so the decision can be revisited if a listener
set that does something translatable turns up — `10001` is the one to look at, and it needs separating
by sender first.

## The drakan high priests — three summon relays that stack

`XDrakan_HighPriest` binds to Elder Malekor (236449) and Head Priest Nashuma (236494), both on plain
`aggressive`.

**He does not have a summon ladder, he has three of them, and none of them ever stops.** One relay
starts with the fight; crossing fifty opens a second; dropping below twenty-five opens a third. The
band rungs are once-only, but each relay is a *pair* of timers that re-arm one another for the rest of
the fight, and the relay branches carry no health guard at all.

| | |
|---|---|
| from twenty seconds | **two** lesser summons every forty |
| crossing fifty | one greater, then **three** lesser every thirty *on top* |
| below twenty-five | one greater again, and a third relay of **three** every thirty |

Measured in the harness: **eight** lesser summons in three minutes with one relay running,
**twenty-eight** with two, **forty-four** with three. Each lives thirty seconds, which is what keeps
that from being unbounded — the pressure is the arrival rate, not the count.

### Each relay is two timers, and collapsing one would double its rate

Retail writes them as a hand-off: the first slot arms the second, the second arms the first and spawns.
The interval is therefore the **sum** of the two delays. This is the Unstable Triroan's lesson from the
other direction — there two timers could arm one slot and the *shorter* won; here two timers chain and
the delays *add*. **Rule: before writing an interval down, work out whether the timers race or queue.**

### The pins that were too loose to see a whole relay disappear

Three of the mutations survived the first sweep, all for the same reason: the relay pins asserted
`together > alone`. That is satisfied by noise. Removing the second relay outright still left the count
higher than the previous window's, because the windows differ in when the base relay's first payment
lands. Counted exactly — 24–32 with two relays, 40–50 with three — all three are caught, and so is a
relay paying two instead of three, which no comparison would have noticed.

**Rule: "more than before" is not a pin. If a mechanic adds a known quantity, assert the quantity.**

A fourth survivor was a bad mutation rather than a missing pin: moving `OnEnterAttack` to `OnWakeUp`
does nothing, because battle timers only fire in combat. That is the second time this session I have
written that same no-op — worth remembering that arming a timer outside a fight is not a change.

### Not translated

Sixteen skill indices and the branches that carry nothing else, including timer 20's twenty-second cast
loop and the four `unset_flag_var` rungs, which each let one relay tick do a different cast the first
time after its band opens. The `6311` broadcast on timer 29 and the `on_message` handler that arms it:
nothing in the dump sends whatever message that handler waits for, so the chain is unreachable from
both ends.

### Verification

Full suite **1,854 passing** and 1 skipped; eight new pins, run three times over; thirteen mutations,
all caught after the count-based repairs.

## Medeus the Vile — Ulan's fight with a target switch on every step

`ND2_WhC` binds to Medeus the Vile (211265), a HERO on plain `aggressive`, and he turns out to be the
fourth member of a family already half-translated: **the same summon pair as `UlanAI`, with different
ids.** Both patterns place `ND2_Sum_WhB1` and then hand over to `ND2_Sum_WhB2`; Ulan's are the ghost
wizards (280806/280807), Medeus's the lich summons (280809/280810), with the same three-at-a-time and
the same ten- and forty-minute lifetimes. Two bosses of one design with a different cast — which is
also why the adds audit had been flagging Medeus's pair with "we spawn 280806 for this role".

| | |
|---|---|
| 61–80 | three lich summons, ten minutes |
| 36–60 | those three **removed**, three of the other kind take their place, forty minutes — and a peel opens onto the third-most-hated every forty seconds |
| below 35 | no summon at all; the clock is spent entirely on peeling, every twenty seconds |

**Where Ulan's deep rung stops the clock, Medeus's opens a loop.** Same rung, opposite consequence: Ulan
below thirty-five summons nothing and never ticks again, Medeus summons nothing either but keeps
ticking and spends every tick coming off the tank. That is why the two are written out separately
rather than shared — the shape is identical and the meaning is not.

### A pin that was wrong rather than weak

The first version of the opening pin wounded a bystander and expected Medeus to take them, because
`on_enter_attack_state` carries `ATTACKERI_HAS_LOWEST_HP`. It failed, and the reason is the mechanic
rather than the harness: **an attacker indicator picks from the hate list, and at the instant a fight
starts that list holds only whoever pulled.** The wounded bystander is not an attacker yet, so the
switch resolves to the puller and changes nothing.

So the pin now asserts what the action actually returns, and the mutation that removes the switch
entirely is left as a **deliberate survivor** — on a fresh pull the two are indistinguishable. It would
matter on a re-engage, where a hate list already exists.

**Rule: an attacker indicator on an <em>enter-attack</em> handler is nearly always a no-op. Retail
writes them anyway; do not pin them as though they choose anything.**

### Not translated

Sixteen skill indices. Both broadcasts — `6184` at 61–80 and `6186` below thirty-five — because their
only listeners are these very summons' own patterns, whose handlers are a single cast each. **And with
the broadcasts dropped, the timer 2 and 3 chain that exists only to pace `6184` goes with them**, while
the timer 4 and 5 chain is kept, because that one paces a peel. That is the helper-rung rule cutting
both ways in one pattern, which is the clearest illustration of it so far: a chain earns its place by
what survives at the end of it.

### Verification

Full suite **1,862 passing** and 1 skipped; eight new pins run three times over; eleven mutations, ten
caught and one deliberate survivor. One mutation had to be rewritten after it failed to compile —
removing a spawn from the middle of an action list leaves a dangling comma, and a mutation that does
not build is not a survivor.

## The Akairun of Medeus — a fight made almost entirely of target switches

`ND2_AhB` binds to the Akairun of Medeus (212008), a LEGENDARY on plain `aggressive`, and he is the odd
one out in this family. The others call waves; **his fight is about who he is hitting.**

Every band opens a target-switch loop *of its own*, and none of them ever closes:

| | |
|---|---|
| crossing 85 | takes whoever is **closest to dying**, every twenty-five seconds |
| crossing 65 | a second loop, taking the **second-most-hated**, on its own twenty-five |
| crossing 45 | a third, back to the weakest |
| below 25 | the ladder stops and a **faster** pair of timers hand off every twenty-seven, taking the weakest |

Each loop is guarded only on being *above* the band that opened it, so they accumulate: a raid that has
walked him down is being peeled from three independent clocks at once. That is the whole encounter, and
it is invisible in a spawn table because it has almost nothing to do with adds.

### One protector where retail places four

At 46–65 retail puts out four **splendid protectors** (280816) for twenty minutes. Three of them use
`SPAWN_LOCATION_WAY_POINT_START` with a `pathname` — "at the start of that route, then walk it" — and
we have neither the location kind nor the route mapping. The fourth is at his own point and is carried.

**One instead of four is a real divergence and is recorded as one rather than smoothed over.** The
alternative was placing none; a wave at quarter strength is closer to the fight than a wave that does
not exist, and the pin says so in its name.

### Two guards enforcing one limit, for the third time

The wave's band is `46–65` *and* the deep rung declines to re-arm the heartbeat. Below twenty-five the
deep rung wins first-match and takes the clock with it, so a wave band widened downward would never get
a tick to fire on — and with the band correct, adding the re-arm changes nothing either. Each guard
hides the other.

This is the third instance in this log, after the krall escape and the Balaur officers, and it now has
a reliable answer: **pin the guard at a health where the other one is not in play.** Thirty-five percent
is where the ladder is still running and the wave band is still closed, and that catches the band; the
missing re-arm stays a deliberate survivor.

### Not translated

Twenty skill indices; the three waypoint-start protectors; and timer 1, armed on entering combat and
read by no branch at all — retail arms it and nothing uses it, left as written rather than tidied away.

### Verification

Full suite **1,870 passing** and 1 skipped; eight new pins; twelve mutations, eleven caught and one
deliberate survivor. Adds 363/281 → **362/281**; translatable 940 → **938**.

## Brigade General Anuhart — Dark Poeta's last boss, on plain `aggressive`

`XDrakan_LastBoss` binds to Brigade General Anuhart (214904). The other five grades of his instance
were translated some time ago; the boss at the end of it was still a plain monster.

| | |
|---|---|
| on engaging | takes a **random** attacker, which is what he does at every step |
| crossing seventy | four **faithful subordinates** (281249) on four fixed marks, an order to take whoever he is fighting, another random turn |
| from then on | a relay re-issues that order about every twenty-seven seconds |
| below thirty | four **flame centres** (281246) at his feet, a random turn, and the ladder stops |
| and then | every thirty-four seconds: four more flame centres, **two more subordinates**, and the order again |

**The four opening subordinates are placed absolutely**, which is what makes them portable at all —
retail names four coordinates around his platform rather than a walker route, so unlike the Akairun's
protectors an entry ago these go exactly where they belong. The pin checks one of them stands on the
mark furthest from him, which a spawn-at-his-feet would not.

### Two of retail's own branches are unreachable

The enrage relay exists **twice**: once unguarded at priorities 10 and 11, once guarded on 31–100 at
priorities 8 and 9. First-match-wins means the unguarded pair always wins, so the guarded copies never
run — and the practical consequence is that **the enrage relay is not bounded by health at all**, only
by the rung that starts it. Recorded rather than ported, so nobody restores them as a missing band.

That is the third pattern in this log with dead branches in retail's own data, after the Unstable
Triroan's `p16` and the sealed akaimum's second `is_race` rung. **Rule: when two branches on one timer
differ only by a guard, check which one outranks the other before believing the guarded one runs.**

### The same-branch broadcast, for the second time

The step spawns the four and broadcasts in the same branch, so — under the rule measured for RM-56c —
they do not hear it, and they stand idle until the relay's first order thirty seconds later. This is
the **second** encounter to want the opposite of that rule (the anuhart casters' pet was the first),
and our measured behaviour is kept in both. Both pins say so in their names rather than quietly
advancing past it.

### Not translated

Nineteen skill indices; the 71–100 timer 1 and 2 chain, which is a cast loop and nothing else; and the
subordinates' `on_leave_attack_state` self-despawn, which would only race the general's own clean-up of
the same group.

The subordinates' pattern is written as a **four-way split on npc state** — idle it adds hate and
attacks, fighting it switches target and casts, once for each of the two messages — and collapses to
one action for each message. That is the second pattern to be written that way; the anuhart casters'
pet was the first, and it collapses for the same reason: our runtime has no vocabulary for testing an
NPC's own state inside a branch, and the outcome does not depend on it.

### Verification

Full suite **1,880 passing** and 1 skipped; ten new pins run three times over; fourteen mutations, all
caught. Adds 362/281 → **361/280**; missing-AI 699 → **698**; translatable 938 → **937**.

## The next-worth-doing ranking was counting its own scaffolding

`audit_translatable.py` ranks unported patterns by how much of them we could actually write. It had
one class of action in the "translatable" set that should never have been there: `add_battle_timer`.

The Belsagos trio — `IDLDF4_Re_01_EasyBoss`, `IDLDF4_Re_01_PhyBoss` and `IDLDF4_Re_01_HardBoss` —
scored 27, 33 and 34 and sat at the top of the list for it. Reading them, each is a **cast chain**:
seven or eight health rungs whose every action is a `use_skill` and the timer arm that brings the next
one round. The only translatable content in all three together is one `1001` on entering combat, two
`1002` broadcasts on death, and a `set_condition_spawn_variable Boss_Die` that belongs to the instance
handler rather than to an AI pattern. Porting any of them changes nothing a player would see.

A timer arm is *translatable* — we have `Do.ArmTimer` — but it is never the point. It is how a pattern
gets from one action to the next, so counting it rewards patterns for being long rather than for doing
anything. The audit now counts two columns: `do` for actions with a visible effect (spawn, despawn,
broadcast, switch target, add hate, flee) and `arm` for the scaffolding, and ranks on `do` alone.

**Rule: an audit that ranks work by "how much of this could we write" must not count the plumbing.**
Only the actions a player could see are the reason to do the work; a high score made of timer arms is a
cast loop wearing a suit. The same trap is available to the adds audit and to any future one that
counts elements rather than effects.

The re-ranked head of the list is unrecognisable from the old one: Ahserion's sub-boss pattern at 43,
the cowardly tutu at 36, kaliga the unjust at 19, and the naga pair — high mage brashuna and commander
gitimuka — at 17 each. 461 unported patterns still carry four or more payload actions, with 1,564 npcs
behind them.

## Heiron's two naga field bosses, and a dismissal hidden inside a cast

`Naga_WrF2` binds to High Mage Brashuna (212310) and `Naga_WrF3` to Commander Gitimuka (212307). Both
were HEROes on plain `aggressive`. They are the same fight written twice — six health bands, each
opened once by a ladder timer and then held by a relay of its own — so they share one builder.

| | |
|---|---|
| 91–100 | nothing but a slower tick: the ladder re-arms at ten seconds instead of six |
| 76–90 | off the tank and onto **whoever is closest to dying**, then again every fifteen or twenty seconds |
| 61–75 | the same rule on its own relay |
| 41–60 | **three faithful subordinates land on the player he is fighting**, an order sends them after that player, and a relay adds one more every thirty seconds and re-issues the order |
| 21–40 | he **dismisses the subordinates**, takes a random attacker, and starts a forty-five-second peel that runs to the end |
| below 20 | the ladder stops and he goes for the **third**-most-hated, over and over |

### The dismissal is a `despawn_self` wearing a cast's clothes

Retail's `6185` branch on `Naga_Sum_WrF2` is an `add_battle_timer` and a `use_skill`, which is exactly
the shape this log has been dropping as *cast-only* for weeks. It is not cast-only. The timer it arms
has one handler — `despawn_self` — so the branch is how the boss clears his own wave on the way past
forty. The cast is the animation; the despawn is the mechanic.

**Rule: a timer arm is only scaffolding if you have looked at what the timer does.** The cast-only
rule asks whether a branch changes anything; a branch that arms a timer changes something exactly when
that timer's own branch does. This is the first time in this log that reading one branch in isolation
would have deleted a whole mechanic.

The other half of that reading is the reverse, and it also applies here: retail's timer **1** is armed
by the 91–100 rung and re-armed by its own relay every twenty seconds, and its handler really is one
cast and nothing else. No other branch uses that slot, so both the arm and the relay are dropped. The
91–100 rung itself is kept — its ladder re-arm is four seconds longer than the fallback's, and that is
a real difference in cadence.

### The wave lands on the player, which changes what the same-branch rule costs

`spawn_on_target` puts the three within seven metres of whoever he is fighting, and the relay's extra
one within three. This is the **third** encounter to want the opposite of the same-branch broadcast
exclusion measured for RM-56c, after the anuhart casters' pet and Anuhart's own subordinates: the three
do not hear the order issued in the branch that spawned them, and wait thirty seconds for the relay.

Here it costs almost nothing, and for a reason worth writing down: **they land on a player, so they
aggro unaided.** Anuhart's four land on marks away from the raid and genuinely stand idle. Same rule,
same divergence, opposite practical weight — which is the argument for keeping the measured behaviour
rather than special-casing either encounter.

### The pin that passed for the wrong reason, twice, at two different distances

Pinning "the boss told the subordinate whom to hit" needs a witness that could not have found the
target itself. Three geometries were tried before one was decisive:

- **Witness thirty metres from the boss**, quarry five metres from the boss. Three order mutations
  survived: the witness reaches the quarry on its own.
- **Witness forty metres from the boss.** Better, but `the step order names nobody` still survived —
  the witness was still finding the quarry by itself in the twelve seconds the pin allows.
- **Witness forty-five metres from the boss** — decisive but *flaky*, failing about one run in three
  with no target at all. Broadcast delivery walks the sender's known list rather than a clean radius,
  so a listener at the edge of `range_as_meter` is not reliably a listener.

What worked was moving **the quarry**, not the witness: the boss holds the quarry forty metres out and
the witness stands thirty metres behind him, seventy from any player. Inside his order, outside
everything else. Nineteen mutations, all caught.

**Rule: to pin "X was told about Y", put Y out of X's reach — moving X away is not the same thing.**
Every earlier version of this pin moved the witness, and every one of them left the quarry sitting
next to the fight where the witness could reach it. The decoy rule from the anuhart entry said distance
is not enough; this says which distance.

### Not translated

Thirty-three skill indices and five shouts across the pair. Retail's timer 1 cast loop, as above. The
two bosses differ in three delays and one npc id and in nothing else, plus one attacker indicator on a
cast that is blocked anyway.

### Verification

Full suite **1,893 passing** and 1 skipped; thirteen new pins run five times over; nineteen mutations,
all caught. Adds 361/280 → **359/278**; missing-AI 698 → **696**; translatable 461/1,564 →
**459/1,562** — each delta exactly the two patterns translated.

## A fourth blocker: payload on a timer nothing can arm

The third blocked bucket — spawns whose only trigger is a waypoint arrival — was found by tracking
which handler each spawn action sits under. That is not enough, and Kaliga the Unjust is the proof.

`Cromede_Named_Angry` (217006) ranked **third on the entire worth-doing list** at nineteen payload
actions. Every one of the branches that made it rank sits under `on_battle_timer`, an ordinary handler
no audit had reason to distrust:

```
on_enter_attack_state    goto_waypoint 2
on_arrived_at_waypoint   index 2 -> goto_waypoint 4
on_arrived_at_waypoint   index 4 -> add_battle_timer 0, add_battle_timer 1
on_battle_timer          timer 0, below 80 -> spawn two statues
on_battle_timer          timer 0, below 50 -> spawn two more
on_battle_timer          timer 1, below 50 -> a hazard on his target
```

Timers 0 and 1 are armed **nowhere but the waypoint arrival**, at the end of a two-hop scripted walk
he takes on entering combat. We have no waypoint-arrival event, and `KromedesTrialInstance` gives him a
single static spot. The whole ladder is dead, and no branch of it looks it.

`audit_timer_reach.py` finds this shape. It runs a reachability fixpoint over timer indices: every
handler except `on_arrived_at_waypoint` can run, so the timers its branches arm are reachable; an
`on_battle_timer` branch runs only if the timer it is guarded on is reachable, and then the timers *it*
arms become reachable too; repeat until nothing changes. Payload in a branch guarded on a timer outside
that set is dead. An unguarded battle-timer branch answers whichever timer fired, so it is dead only
when no timer is reachable at all.

Fourteen patterns carried dead payload — thirty-one actions across forty-seven npcs. `audit_translatable.py`
now subtracts it, and Kaliga falls from third to fifth.

**Rule: a branch is only as reachable as the timer that carries it.** Every audit before this one
asked "can this action run", one action at a time. The question that matters is "can this action ever
be reached", and the answer lives in a different branch — sometimes two hops away, in a handler that
looks perfectly ordinary until you ask who arms it.

## Kromede's Trial — the dismissal, and the chain that has to land in one piece

What is built is one branch: **when the Angry Judge falls, three markers go out across the manor and
call his servants away.** Retail places them absolutely, and the three coordinates land within three
metres of our own spawn points for Hamam the Torturer (216982), Lady Angerr (217000) and Justicetaker
Wyr (217002) — which is what identifies them as one-per-servant rather than scenery.

| marker at | our spawn for | apart |
|---|---|---|
| 749.80, 628.18, 198.37 | 216982 hamam the torturer | 3.1 m |
| 512.55, 574.35, 217.60 | 217000 lady angerr | 2.9 m |
| 568.19, 833.13, 226.33 | 217002 justicetaker wyr | 2.6 m |

The marker (282115, `Cromede_Kkt_Noshow`) is two actions: broadcast `6406` within fifty metres, and
`despawn_self`. It is invisible and it exists for one reason — **retail addresses a specific NPC by
putting a speaker next to it**, because a pattern has no way to name one. That idiom is worth
recognising; it is the same trick as the anuhart casters' pet order, done with geography instead of a
parameter.

### The rest of the trial is specified here and deliberately not built

The chain the log should carry, because it is fully resolved and only the landing is left:

1. Each servant, **at thirty percent or on death**, drops its own marker at the judge's dais
   (662.28, 774.4, 216.85) and — at thirty percent — **removes itself instead of dying**.
2. That marker (282112 `Cromede_Torture_Spawn`, 282113 `Cromede_Wife_Spawn`, 282114
   `Cromede_Assijudge_Spawn`) seats a **wounded copy** of that servant beside the judge — 217004 at
   (663.07, 769.54), 217001 at (663.07, 779.08), 217003 at (661.07, 774.43) — then broadcasts `6403`
   and `6404` and goes.
3. `6404` turns the Angry Judge into **Shadow Judge Kaliga (217005)** at his own spot and removes him.
4. `6403` removes the two relics (`Cromede_Relic1`, `Cromede_Relic2`).

`KromedesTrialInstance` already produces the **end state** of that chain — scared judge plus the same
three wounded servants, at coordinates within two metres of retail's — from a single `IsDead` check on
all three servants at treasury entry. So aionemu reimplemented the outcome and dropped the mechanism,
and the two cannot be mixed:

- land the wounded-servant spawns alone and a player who clears all three before entering the treasury
  gets **six** wounded servants, three from the markers and three from the handler;
- land the thirty-percent vanish alone and the servants stop dying, so the handler's `IsDead` gate
  never opens and the Shadow Judge never appears at all;
- land the `6404` conversion alone and the judge turns scared with no wounded servants beside him.

**Rule: when an instance handler has reimplemented a pattern's outcome, the pattern lands whole or not
at all.** Half of a reimplemented chain is not half-right, it is a duplicate or a dead end. This is the
first entry to hold work back on that ground rather than on a missing vocabulary.

Lady Angerr is the fourth piece: she is on our `summoner` AI with a tuned `spawn_helpers.xml` ladder
for her six bats, and her retail pattern `Cromede_Wife` carries that same wave, so she wants the whole
hand-over at once — pattern class in, helper rows out — not a fourth branch bolted on.

### Not translated

Kaliga's health ladder (dead, above); his `on_leave_attack_state`, which spawns two relic carriers
whose entire patterns are one blocked cast and `despawn_self`; his `on_message 6513` from the manor
door; eight skill indices and five shouts. The servants' casts, their `random_move` loops, and Hamam's
`on_stop_to_random_move` probability split.

### Verification

Full suite **1,897 passing** and 1 skipped; four new pins; eight mutations, all caught. Missing-AI 696
→ **693**; translatable 456/1,556 → **453/1,553**; dead-timer payload 14 patterns / 31 actions →
**13 / 26** — every delta exactly the three patterns translated.

## Ophidan Bridge links, and sixteen npcs were doing it alone

`NpcAIPatterns_IDLDF5_Under_01_JSM.xml` is Ophidan Bridge (300590000). Fifteen of its patterns — the
three hard-mode velkurs and the twelve `BIDF5_U01_Runaway_*` grades — carry the same branch pair, which
retail's own comment calls `애드 수신`, "add receive":

| | |
|---|---|
| on engaging | broadcast `10500` at **thirty metres**, naming whoever you are fighting |
| on hearing `10500` | **ten thousand** hate on the player named, and attack |

Sixteen live npcs, every one a HERO, every one on plain `aggressive`. They now share one class.

**It chains, and the chain is the mechanic.** Answering the call is an entry into combat, and entering
combat is what makes an NPC call in turn, so one careless pull walks from group to group across the
bridge. It terminates rather than running away: an NPC already fighting does not re-enter combat, so it
does not call twice.

**Ten thousand is not decoration.** It is far above anything a player accumulates in a pull, so the
called NPC goes to the named target and stays. This is retail saying "hand-off", not "nudge", in the
only vocabulary a pattern has for it — and it is pinned by a mutation that drops the value to one.

**Normal mode does not link.** Spirited Velkur (235768, `BIDF5_U01_Boss_Wi_Nor`) has neither half of
the pair: the same fight with one mechanic taken out. He keeps the stock AI, and a mutation that gives
him the class is caught.

### The decoy pin that measured its own contamination

The pin for "the call outweighs whoever is standing nearer" was written the obvious way: a decoy player
beside the listener, hated before the pull, and the listener should still take the caller's target.
It failed, and the reason is the mechanic itself. Hating the decoy **put the listener into combat**,
which fired the listener's own call, which named the decoy, which the caller heard — so the caller took
the decoy too, and named it in its own call. The setup had quietly inverted the thing being measured.

Rewritten as an ordering: pull first, then bring the second player in afterwards and let it land a
thousand hate against the call's ten thousand.

**Rule: in a web where every listener is also a sender, a decoy is a message.** Every earlier decoy in
this log was inert — a player standing somewhere, a summon parked out of reach. Against a linked pull
there is no such thing as an inert participant, and the setup has to be ordered in time rather than
laid out in space.

### `despawn_by_nameid` — a new blocked verb, and a large one

All three velkurs, on entering combat, place four triggers at fixed points across the bridge
(674.2/471.7, 604.3/555.5, 528.8/437.2, 468.6/516.8). Each trigger's whole pattern is **nine
`despawn_by_nameid` calls** — clear every NPC of nine named kinds from the map. That is a room reset on
pull, and we have no vocabulary for it: the verb addresses NPCs by client devname across the whole
instance, not by a spawn group.

It is not a one-off. `despawn_by_nameid` appears **849 times across 171 patterns** in the 5.8 dump,
which puts it in the same class as `pathname` as a missing verb rather than a missing mechanic. Unlike
`pathname` it has no data blocker at all — the devname-to-id table is the one `audit_missing_adds.py`
already builds. What it needs is a runtime op that walks the map's npc list and removes matches, and a
decision about whether "the whole instance" or "the caller's known list" is the right scope. **The
biggest single unblocked verb left in the dump.**

### Not translated, with the reason for each

- **The six-timer round-robin** (BT0→BT1→…→BT5→BT0, nine to eleven seconds a step) is a cast chain.
  Its only non-cast content is three broadcasts, and each fails the cast-only test in its own way:
  `10200`'s listeners answer with a cast; `10600` and `10700` have one listener that answers with
  spawns — `BIDF5_U01_Runaway_Pr`, which binds to **235763 runaway hirakiki leader, an npc our server
  never spawns**. So the loop is scaffolding for us today and becomes worth building the moment that
  leader is placed. Recorded rather than dropped, because the trigger for revisiting it is concrete.
- **235763 and 235767** (runaway hirakiki leader, escapee asachin leader) are HERO templates with full
  retail patterns that nothing in our data spawns, while their own rank-and-file are all live. Their
  live sibling 235759 is spawned by `OphidanBridgeInstance` through a `235759 + Rnd.Get(0,2) * 4`
  rotation, so the other two are reachable by the same handler and simply never chosen.
- `set_condition_spawn_variable under_01_out` on every death — instance progression, not AI.
- `10900` (a fugitive died) and `10100` (a request for the finisher) — both answered only with casts.
- Fifteen skill indices and one shout.

### Verification

Full suite **1,902 passing** and 1 skipped; five new pins run three times over; seven mutations, all
caught. Missing-AI 693 → **686**; translatable 453/1,553 → **438/1,537** — the npc delta is exactly the
sixteen, and the pattern delta exactly the fifteen.

## `despawn_by_nameid`, and a correction to the entry before this one

The runtime can now say `despawn_by_nameid`: `Do.DespawnKind(npcId, radius, maxCount)`, backed by
`PatternAi.DespawnKind`. Retail's element carries exactly three arguments and all three are bounded —
across the 5.8 dump the radius runs 2 to 100 metres (640 of 849 at fifty) and the count 1 to 100 (556
of 849 at ten) — so this is a local sweep and never the map-wide wipe the name suggests.

**The owner is not excluded, and that is measured rather than assumed.** Of 849 uses, **none** names
the devname of the NPC running it, so the "clear yourself" case retail never wrote is not guarded
against here either. It shares `NpcMessageBus.Nearby` with the broadcast path — same question, same
known-list-then-region fallback, and now the fallback's one caveat is inherited too (see below).

### The correction: 849 uses, and one encounter behind them

The previous entry called this "the biggest single unblocked verb left in the dump" on the strength of
849 uses across 171 patterns. That was a count of the *sending* end, which is the exact mistake this
log already has a rule against. Counted at the receiving end:

| | patterns | sweeps | |
|---|---|---|---|
| no binding row at all | 132 | 641 | no client npc names the pattern; unreachable |
| bound, but we spawn none of them | 27 | 183 | |
| live and already ported | 12 | 25 | **the real work** |
| live and unported | **0** | **0** | |

So the verb unblocks **zero new encounters** and **twenty-five sweeps inside twelve encounters we had
already translated without them** — which is worth having, and is a twentieth of what the raw count
implied.

**Rule: a verb's worth is the payload behind it, not its frequency.** The "count the receiving end"
rule was written for broadcasts and applies unchanged to vocabulary. A verb used 849 times in patterns
nothing runs is worth exactly as much as a message with no listener.

It was still worth building — 25 sweeps we were silently dropping, and three genuinely different uses
of it inside one file, below — but the ranking claim is withdrawn.

### Three uses of one verb, all in Ophidan Bridge

Eight of those twenty-five are now built, and they are not the same mechanic wearing three hats:

1. **The bridge sweep.** A boss engaging drops four triggers at four fixed points; each trigger's whole
   pattern is nine sweeps — up to ten of each fugitive grade within fifty metres. Engaging the boss
   empties the approach the raid picked through on the way in.
2. **The mode switch.** Each hard-mode velkur clears **Spirited Velkur**, the normal-mode boss, the
   moment it appears. That is retail saying "the two modes are the same fight and only one is running",
   in AI rather than in an instance handler — and `OphidanBridgeInstance` says the same thing in its
   own comment ("instance starts always in hardmode").
3. **Bookkeeping.** A fugitive reaching its *second* grade clears the invisible check marker (856062)
   at its post. Not a fight mechanic at all, and the reason the audit should never have scored the verb
   as payload-by-frequency.

The class became a three-flag builder because none of its mechanics is universal: the normal boss
sweeps without calling, the fugitives call without sweeping, the second-grade fugitives clear a marker
the first and third do not, and only the three hard-mode velkurs do everything.

### The wake-up bound is the region, not the radius

`on_wake_up` runs before the NPC has a known list, so `Nearby` falls back to scanning the sender's map
region — a limit already recorded for wake-up broadcasts, and now shared by wake-up clears. A pin that
puts the target seventy metres away therefore bounds the clear from above without saying *which* bound
stopped it. The decisive test is from the other side: dropping the range to five metres leaves a target
at ten standing, and that mutation is caught. The seventy-metre pin says so in its own remarks rather
than implying more than it measures.

### The other seventeen sweeps, specified

Everything left is inside an already-ported encounter, so each is a small edit to an existing class
rather than a new one:

| pattern | npcs | sweeps | targets |
|---|---|---|---|
| `Legion_01_Boss_03` | 855776 vision of kaliga | 4 | 856129, 856130, 856131, 856132 |
| `BIDF5_U01_Middle_Boss_Fire` | 235772 hakara, 235773 zubala | 3 | 231185 ×3 |
| `IDVritra_Base_Boss1` | 230858 brigade general sheba | 1 | 284436 |
| the six `Runaway_*_P2_*_P3` | six fugitive grades | 6 | 856062 — **built** |
| `BIDF5_U01_Boss_Wi`, `_Monster_01` | three velkurs | 2 | 235768 — **built** |
| `BIDF5_U01_Middle_Boss_Ice` | 857437 | 9 | the nine grades — **built** |

### Verification

Full suite **1,908 passing** and 1 skipped; fifteen pins on this encounter run three times over;
**twenty-two mutations, all caught**. Missing-AI 686 → **685**; translatable 438/1,537 → **437/1,536**
with `despawn_by_nameid` now counted as vocabulary — the delta is exactly the normal-mode boss, and
counting the verb moved no pattern into or out of the list, which is the same finding as the table
above arriving by a second route.

## Ophidan Bridge, part two: the escape, and a web that was bigger than measured

### The web is twenty, and the probe that said sixteen was filtering out its own answer

The linked-pull entry two commits ago counted sixteen npcs. The probe that found them required the
owner to be on a **generic** AI — the same filter every "what is unported" audit uses — and the four
middle bosses were already on `middle_boss_fire`, so they were dropped before they were counted. They
carry both halves of the pair, and at **fifty metres** rather than thirty.

**Rule: to measure a web, drop the unported filter.** "What is left to do" and "what is this mechanic"
are different questions, and the filter that answers the first quietly corrupts the second. The number
to report was never how many were unported; it was how many are in the web.

### A million hate points

A middle boss answers the call with `point_to_add` of **1,000,000**, a hundred times the fugitives'
ten thousand. Nothing takes a middle boss off the player it was sent after — pinned by a mutation
dropping it to one, and by a pin where a second player hits the boss afterwards and does not move it.

### Killing a middle boss is what makes the fugitives run

The death branch broadcasts `10000` at fifty metres, and every fugitive grade answers it with a system
message, a shout, a cast, a **`teleport_target`** and `despawn_self`. So the instance's premise —
these are runaways, and clearing a stronghold scatters the ones around it — is written in the AI, not
in the instance handler.

**We have the last of those five actions and none of the other four**, so our fugitives vanish where
retail throws them clear first. That is half a mechanic and it is recorded as half: the outcome is
right and the flight is missing.

`teleport_target` measured at the receiving end before any claim was made for it: **208 uses across 92
patterns, 174 of them in patterns with no live owner, 34 in patterns we have already ported, and none
at all in a live unported pattern.** The same shape as `despawn_by_nameid` one entry ago. A verb worth
building for polish, never for reach.

The same branch clears the **beritran support combatants** (231185) around the post, and walking away
from the fight clears them without the signal going out — two sweeps that differ by one broadcast, and
both pinned that way round.

### What is left of this instance, specified

- **`10800`** — the death also broadcasts this, and the check-marker controller `BIDF5_U01_Ctrl_07`
  (856062) answers it by placing two despawn markers at the **other two strongholds**, with the
  designer's own comment saying so: 다른 거점 도망자 디스폰, "despawn the fugitives at the other
  strongholds". Killing one middle boss thins the whole bridge. Blocked on nothing except
  `BIDF5_U01_T_Runaway_Despawn_NPC`, whose devname resolves to no npc id in our client table.
- **856398**, the support relay the death branch leaves at the boss's feet: `BIDF5_U01_Monster_09` is
  an idle timer that re-sends `10000`, `10800` and `11100` **every six seconds** and sweeps. It is how
  retail catches fugitives that arrive after the boss falls. Unspawned in our data and buildable the
  moment the spawn is added.
- **`11100`** — only listener is `BIDF5_U01_Ctrl_10`, which binds to no npc we spawn. Not sent, by the
  no-audience rule.
- **The Dark variants** (235776–235778) are HERO templates with a pattern identical to the Fire one
  plus a `DespawnAll` trigger at 672.9/473/599.3. Nothing spawns them.
- **`set_condition_spawn_variable mboss_die` / `ra_run_ok`** — instance progression, not AI.

### Brigade General Sheba: left alone, deliberately

`IDVritra_Base_Boss1` carries one sweep — 284436 within fifty metres, at most ten, on
`on_leave_attack_state`. `BrigadeGeneralShebaAI` is a Java-parity class whose `RemoveAdds()` already
deletes **both** 284435 and 284436 across the **whole instance**, on death as well as on reset. Retail's
sweep is a strict subset of that, so porting it would be a regression: adds outside fifty metres, or
past the tenth, would be left standing.

**Rule: a retail action that is narrower than what we already do is not an upgrade.** The sanctioned
exception says retail AI outranks aionemu where they disagree about behaviour; it does not say a
bounded verb should replace an unbounded cleanup that is doing the same job better. Recorded so the
sweep is not "restored" later as a missing one.

`Legion_01_Boss_03` (855776 vision of kaliga, four sweeps on death) is still to do and is the last of
the twenty-five that is neither built nor argued away.

### Verification

Full suite **1,915 passing** and 1 skipped; seven new pins, thirty-seven across the two classes run
three times over; **ten mutations, all caught**. The three audits are unchanged, which is the expected
result for a commit that only extends classes we had already written.

## Payload that talks to nobody: 411 npcs leave the worth-doing list

`audit_timer_reach.py` asks whether a branch can ever run. `audit_message_reach.py` asks the next
question — whether its effect can reach anybody — and finds two shapes the ranking had been paying
full price for:

- **a broadcast nobody answers.** `broadcast_message` is payload, and it is worth nothing if no
  pattern we can spawn receives that number and does something visible with it. The cast-only rule in
  this log is exactly this rule applied one message at a time; this is it applied to all of them.
- **a receive nobody sends.** An `on_message` branch full of hate points and target switches is worth
  nothing if no pattern we can spawn ever broadcasts that number.

"Alive" means the pattern has at least one owner our spawn data actually places, because a listener
that exists only in the dump is the same dead end as a listener that answers with a cast.

Across the unported patterns: **377 of them talk to nobody somewhere — 510 broadcasts nobody answers
and 84 actions nobody triggers.** Subtracting that from the ranking moves it more than any earlier
correction has:

| | patterns | npcs |
|---|---|---|
| before | 437 | 1,536 |
| after | **347** | **1,125** |

Ninety patterns and **411 npcs** were on the list on the strength of messages that reach an empty room.
The whole DirectPortal guard family, the anuhart camp guards, the fortress guard captains and the
tayga pack drop out or fall a long way down.

**Rule: an audit that counts a message must know who is at the other end of it.** This is the third
correction of the same kind in this log — the scaffolding one, the dead-timer one, and now this — and
they share a shape worth naming: *every one was the audit counting what the data says instead of what
the server would do.* The data is the same either way; the question is always "and then what happens".

**One level only, deliberately.** A broadcast counts as answered if some live pattern replies with a
payload action, even where that reply is itself a broadcast into a dead end. A fixpoint would be more
correct and would need the cycle care `audit_timer_reach.py` takes; the one-level answer already stops
the ranking paying for an empty room, and the gap is recorded rather than hidden.

## Two encounters closed out rather than built

### The taygas: fourteen payload actions, six of them real

`D2_FnM` (four taygas) ranked joint-third at fourteen. Measured message by message, **eight of the
fourteen are dead**: it answers `2302` and `2304`, which nothing alive sends; and it broadcasts `1019`,
`2305` and `2306`, whose only listeners either have no live owner or answer with a cast.

What is left is real and small: on death it broadcasts `2307`, which `Lycan_HeB` — four live lycans —
answers with `flee_from`, so killing a tayga scatters the lycans beside it. Plus a hate-and-switch on
sensing a friend killed, and the `2301` order. Six actions, four npcs, and worth doing on its own terms
rather than as a top-of-the-list item.

### `D2_FnA` is empty, and 3,315 npcs bind to it

The single most-bound pattern in the 5.8 dump is `<event_handlers></event_handlers>` and nothing else.
995 of its owners are live and on generic AI. `audit_missing_ai.py` already excludes it — checked
rather than assumed — so no headline number was ever inflated by it, but it is worth writing down that
"this npc has a retail pattern" and "this npc has retail behaviour" are three thousand npcs apart.

### Vision of Kaliga: the last of the twenty-five sweeps, and it cannot be built

`Legion_01_Boss_03` carries four `despawn_by_nameid` on death, clearing the four summons its own health
ladder places. Three facts close it:

1. **855776 spawns in Stonespear Reach** (301500000), not in a legion dungeon — the pattern comes from
   retail's `NpcAIPatterns_F5_Legion_JSM.xml` and the npc id is reused.
2. It runs `StonespearAggressiveNpcAI`, a Java-parity class whose whole job is to send it at the
   guardian stone, deny it loot and delete it on death. That is what Stonespear Reach needs.
3. Nothing in our data spawns its four summons — no `spawn_helpers.xml` row, and the ladder that would
   place them is the one we are not running.

So the sweeps would clear a room that is already empty, and running the ladder that fills it would mean
replacing an instance's working AI with a boss fight from a different instance.

That closes the twenty-five sweeps `despawn_by_nameid` unblocked: **nineteen built**, one collapsed
into another (retail writes the middle boss's death branch twice behind one flag), and five argued away
in the log — Sheba's, because ours is broader, and these four.

### Verification

Full suite **1,915 passing** and 1 skipped, unchanged — this commit adds an audit and changes no server
code. Missing-AI 685 and adds 359/278 unchanged; dead-timer payload 13 patterns / 26 actions unchanged;
translatable 437/1,536 → **347/1,125**.

## Ophidan Bridge's reinforcement posts, and the first guard we could not port as written

Four invisible NPCs (284708–284711), one at each corner of the bridge, and between them the reason the
instance is a race: **a pair of beritran arrives every sixty seconds, five times, and then the post is
spent.** Each post has its own two marks and its own two kinds — the third sends a wind beritran
alongside an ordinary one, the fourth sends two shadows — which is what makes it a table rather than
one pattern. When a post goes, everything it called goes with it.

### `increase_intvar` as a condition, and why it is an action here

Retail guards each wave with `increase_intvar be_true_only_when_hit_the_bound="TRUE"` over bounds 0–2,
2–4, 4–6, 6–8 and 8–10. That element is written as a **condition**, and our evaluator — like retail's,
by every other piece of evidence in this log — tries branches in priority order until one passes. So
all five would advance the counter as the event walked past them, and the first tick would land
somewhere in the middle of the ladder. Every reading of it that was tried produces something other
than five evenly spaced waves, and the designer's own comments say precisely what it should be: 1차
through 5차 스폰, each 60s 후.

Split, therefore: the branches test with a new read-only `When.CountEquals`, and the counter is
advanced by a new `Do.Increment` action inside whichever branch runs, including the do-nothing tick
between waves. The counter is seeded to one on waking, because retail's guard advances as a side
effect of being *evaluated* and an action-driven counter is otherwise a step behind. Waves then land
at 60, 120, 180, 240 and 300 seconds.

**Rule: where a retail guard cannot mean what it says, port the outcome and say so in the class.** This
is the first entry in this log to write that about a *condition* rather than an action; the earlier
divergences were all about what we could not express, and this one is about what retail's own element
cannot have meant. The bounds in the code are retail's own numbers so the two can be compared, and the
comment carries the whole argument.

`Do.Increment` also fills in half of what the counter section of `AiPattern` had explicitly deferred:
"the `TRUE` variant is deliberately not implemented; no ported pattern uses it, and it would ship
untested." One does now, and it ships with ten mutations behind it.

### They arrive, and they do not march

Every one of retail's sixteen spawns carries a `pathname` — twenty-four distinct routes across the four
posts, `NPCPathSupport_Path01` through `_Path24` — so in retail each pair walks its own line in from
the corner. Ours appear at the post and hold it. Half the mechanic, and the half that matters for
pacing is the half we have.

### The audit was calling that "nothing blocked"

These four ranked joint-tenth at eleven payload actions with an empty blocked column, because
`audit_translatable.py` keys its blocked set on **action tags** and `pathname` is an *attribute* of one.
A spawn that names a walker route places the npc perfectly well and then leaves it standing where
retail marches it in — a third of what these posts do, invisible to the ranking.

Now counted. Totals do not move, because a route is not payload and the spawn still happens, but the
blocked column tells the truth: Ahserion picks up `path:13`, the cowardly tutu goes from `path:31` to
`path:62`, and `BIDF5_U01_Ctrl_01` — which read as clean apart from three script verbs — turns out to
be `path:9` as well.

**Rule: a blocker can be an attribute.** Every blocked bucket in this audit so far has been a verb,
and the habit of looking only at verbs hid the single largest missing piece in the project on the four
patterns where it mattered most.

### Verification

Full suite **1,921 passing** and 1 skipped; six new pins run three times over; **ten mutations, all
caught**. Translatable 347/1,125 → **343/1,121**, exactly the four patterns and their four npcs;
missing-AI 685 and adds 359/278 unchanged, as expected for four invisible spawners.

## Two of the four DF5 named field bosses, and the escalation written as loop geometry

`NpcAIPatterns_DF5_Named_SSH.xml` holds four field bosses, all HEROes on plain `aggressive`. Two of
them are cast chains and two are mechanics.

### Tidalsail Spirit (219929) lays eight mines and sets them off together

| | |
|---|---|
| six seconds in | the summoning motion |
| then four times, six seconds apart | **two mines, each on its own randomly chosen attacker** |
| six seconds after the last pair | **they all go off at once** |
| eleven seconds later | the cycle starts again |

**Every mine picks its player independently.** Retail writes the spawn twice rather than asking for
two, and each carries its own `ATTACKERI_RANDOM_ONE`, so a raid ends up with mines scattered across it
instead of eight under one person — the difference between a mechanic and an execution. Pinned by
spread rather than by count, and by a mutation that replaces the pair with a single two-count spawn on
one target.

**The detonation is a `despawn_self` behind a cast**, for the third time in this log — after the naga
summons' dismissal and Kaliga's markers. The mine's whole pattern is one branch: on hearing `1001`,
cast and go. We have the going.

**Retail's own clean-up here does nothing.** Both death branches and the leash branch despawn
`SPAWN_ID_1`; every mine is laid with `SPAWN_ID_NONE`, so that group is always empty. Kept as written —
the forty-second lifetime is what actually clears them, and porting a despawn that clears an empty
group is porting the quirk rather than correcting it.

### Infernomane Vortile (219930) escalates by changing the shape of his loop

| | |
|---|---|
| above fifty | five steps of ten seconds; on the third and fifth he turns to a random attacker and drops **two** blazes on them |
| below fifty | **four** steps; the two drops become **three** blazes each |

**There is no enrage branch.** The rung below fifty is a second copy of the same loop with one step
taken out and one blaze added, so the cycle shortens from fifty seconds to forty while each drop grows
by half — the rate nearly doubles and retail never writes a word about it. That is worth naming,
because a reader looking for the enrage in this pattern will not find one: **the escalation is in the
geometry of the loop, not in a branch.**

### Not translated

- **`DF5_ItemNamed_6_Fi_01_SSH`** (219926 rootrage rotron) is a cast chain whose only payload is one
  `switch_target` at the end of its upper loop, and **`_As_01`** (219927 chromascale dreadclaw) has no
  payload at all. Both stay on `aggressive`, recorded so the family is not re-opened as three-quarters
  missing.
- The blazes (282390) walk and lay a trail of standing fire every two seconds in retail — an encounter
  of its own in another file, left on the stock AI, so the trail is missing rather than the drop.
- Eight and seven skill indices; the self-buff on waking; and `on_enter_return_sp`, an event our
  runtime does not raise, which both bosses use to re-buff on leashing.
- The four `B…` twins (855914, 855915, 855918, 855919) share these patterns and are spawned by nothing
  in our data.

### Verification

Full suite **1,931 passing** and 1 skipped; ten new pins run five times over; **fourteen mutations, all
caught**. Missing-AI 685 → **683**; translatable 343/1,121 → **341/1,119**; adds 359/278 → **358/277**,
the one being the mine — the blazes were already spawned by another encounter, which is why the delta
is one rather than two.

## Nochsana's two naga wizards, and the first branch split on npc state we could port

`MiNaga_WeA` binds to the Nochsana Protector (256690) and `MiNaga_WeB` to the Nochsana Teleporter
(256691). Both were ELITEs on plain `aggressive`, and between them they hold the training camp's one
piece of teamwork.

| | |
|---|---|
| on engaging | each calls — the Protector to twenty-five metres, the Teleporter to twenty — naming whoever pulled |
| on hearing the call | go for that player |
| the Teleporter only | a **nochsana reservist** lands on his quarry as he engages, and a second thirty seconds later while he is above seventy |

### The Teleporter answers the same call two different ways

Retail splits his `10004` handler on whether he is already fighting: if he is, he **only turns** to the
player named; if he is not, he takes hate and starts. That distinction survives here, and it is the
**first time in this log a retail branch split on npc state has been ported rather than collapsed** —
the anuhart casters' pet and Anuhart's subordinates were both flattened for want of exactly this guard,
which `When.Fighting` has since supplied.

The turn without hate needed a new action. `switch_target target=OBJI_MESSAGE_PARAM` is not
`add_hate_point` plus a turn; it is the turn on its own, and `Do.TargetMessageParam` says it. Its
weakness — the turn lasts only until the aggro list is consulted again — is retail's own, in the
action rather than in the porting.

### The Protector answers one call a fight

His branch carries a test-and-set flag, so the second call he hears does nothing. That is what stops
two wizards bouncing each other between players for the length of a fight, and it is pinned by handing
him two calls naming two players and asserting he holds the first.

### 70 no-op target switches, and the ranking was counting all of them

Reading these two turned up `switch_target target=OBJI_CUR_TARGET` — turn to the object you are already
on. The Protector has three and the Teleporter two, and across the dump there are **70 of them in 57
patterns**. Every one was scoring as a target switch in the worth-doing ranking.

Now subtracted, along with the dead timers and the unheard messages. The list moves 341/1,119 →
**337/1,111**, of which two patterns and two npcs are these wizards and the rest is the no-op.

**Rule: an action that names the thing it is already pointed at is not an action.** Third variety of
"payload that is not payload" this log has had to name — after scaffolding timers and messages nobody
hears — and the same lesson each time: *the audit was reading the data rather than asking what the
server would do.*

### Not translated

`param_obj=OBJI_EVENT_TARGET` on both calls, which we send as the current target — the same player at
the moment of engaging, and there is no other moment these calls are made. Five shouts, fourteen skill
indices, and the Teleporter's `on_killed_by_npc` branch, which duplicates his death clean-up for a
death our runtime does not distinguish.

### Verification

Full suite **1,938 passing** and 1 skipped; seven new pins run three times over; **twelve mutations,
all caught**. Missing-AI 683 → **681**; adds 358/277 → **357/276**, the one being the reservist;
translatable 341/1,119 → **337/1,111**.

## Chief Gunner Kurmata: a laser designator built out of two NPCs and a message

`IDVritra_Base_Drakan_Gi_Nmd` binds to Chief Gunner Kurmata (230851) of the Sauro Supply Base, a HERO
on plain `aggressive`. The whole fight is a targeting mechanic in three parts: **he paints a player,
the paint calls, and the cannon fires at the paint.**

| | |
|---|---|
| on engaging | a mark on a **random** attacker, and a call that puts the flame cannon on whoever he is fighting |
| above sixty | a four-step loop of about thirty-nine seconds; one step marks **whoever he is fighting**, another turns him onto somebody else |
| below sixty, once | a shorter loop that marks **two players at a time**, twice round, with ten times the hate on each mark |

**The marks do their work by hating.** Each is spawned with `attack_target_after_spawn` and a hundred
thousand hate points — a million below sixty — so a mark is not scenery: it lands on a player and stays
there. That is why the fight reads as a gunnery drill rather than a boss with adds.

**Below sixty he marks two, not everyone.** `spawn_on_multi_target` carries `total_set_to_spawn=2` over
a forty-metre reach, which is easy to misread — the element's name says multi and only the count says
two. A mutation that raises it to eight is caught.

### `OBJI_MESSAGE_SENDER` is not `OBJI_MESSAGE_PARAM`

The cannon's second branch hates **the thing that spoke** rather than the thing the message named. That
is the whole designator: the mark announces itself, and the cannon turns on the mark standing on a
player rather than on the player. It called for a new action, `Do.HateMessageSender`.

The outcome would have matched either way here — the mark broadcasts with `param_obj=OBJI_SELF`, so
sender and parameter are the same object — and **the mechanism would not have**. Ported as retail wrote
it, with a mutation that swaps the two, which the pins catch. **Rule: where two retail objects happen
to coincide, port the one the data names.** The coincidence is a property of this pattern, not of the
verb, and the next pattern that uses `OBJI_MESSAGE_SENDER` will not have it.

### Three pins that measured the wrong thing first

- **"a second mark twenty-two seconds in"** was written as a head count. Marks live twenty seconds, so
  the first is already gone when the second lands and the count reads one either way. Rewritten as
  arrivals.
- **"the gunner's call sends the cannon at his quarry"** put the cannon six metres away, where the
  mark's own call — which comes second and carries the same ten thousand — took the cannon off the
  player again. It now stands forty metres out: inside the gunner's fifty-metre call, outside the
  mark's. **The chain has to be taken apart by geometry to pin its first step.**
- **"ten thousand hate keeps the cannon on the mark"** ran the whole chain, in which the gunner's call
  gives the *player* ten thousand too, so a thousand more on top puts the player ahead fairly.
  Narrowed to the mark's call alone.

All three are the same mistake in different clothes: **a pin on one step of a chain has to exclude the
rest of the chain**, and in a message web the way to exclude it is range.

### Not translated

Eleven skill indices — every 탄환발사 and 산탄 in the branch comments is one — and five shouts. The
mark's own `on_spelled` branch, guarded on `is_event_skill_id`, which leaves a puff of smoke and removes
the mark: that is the **player's answer to the mechanic**, and it needs a skill id we cannot resolve, so
today a raid cannot shoot a mark off. Its counter-driven battle timer ends the same way. Our marks are
cleared only by their twenty-second lifetime and by the gunner's own despawns.

### Verification

Full suite **1,947 passing** and 1 skipped; nine new pins run three times over; **sixteen mutations, all
caught**. Missing-AI 681 → **680**; adds 357/276 → **356/275**, the one being the mark; translatable
337/1,111 → **336/1,110**.

## Darkblade Ovanuka, and a second handler our runtime can never fire

`IDVritra_Base_Drakan_As_IU_Nmd` binds to Darkblade Ovanuka (233256) of the Sauro Supply Base, a HERO
on plain `aggressive` with a three-phase timer chain. What survives translation is the turning and one
order:

| | |
|---|---|
| above eighty | a thirty-second loop, one step of which takes a **random** attacker |
| crossing eighty | **he names the player he is fighting and his bladesmen take them** |
| below thirty-five | a shorter loop that turns him twice more, and stops |

### `on_stop_to_random_move` joins `on_arrived_at_waypoint`

Two phases of this fight are reached through `random_move` and the `on_stop_to_random_move` event it
raises. Timers 5, 6, 7 and 10 are armed **there and nowhere else**, so in our runtime — which has
neither the action nor the event — those branches are dead.

`audit_timer_reach.py` now carries both handlers in its unreachable set. The two are the same shape:
**an NPC that never walks a route never arrives at a waypoint, and an NPC that never wanders never
stops wandering.** The audit's numbers move from 13 patterns / 26 actions to 14 / 28, and Ovanuka's own
score drops from fourteen payload to twelve.

**His second call goes with those phases.** `22271` — the soft one, which the bladesmen answer one time
in three with a turn rather than a charge — is broadcast only from timer 10. Neither half is built: not
the call, and not his own thirty-percent answer to it. Both come back the day `random_move` does.

**And phase three ends early, faithfully.** The branch on timer 9 is a two-way toggle on one flag: the
first turn arms timer 11 and takes a random attacker, the second wanders. So our last phase turns twice
and stops, exactly where retail's walked away.

### The alarm that is one broadcast from being real

The bladesmen also answer `22251`, and so do the **sheban legion ambushers** (233277): both take
whoever the boss is fighting. Retail broadcasts it from `IDVritra_Base_Boss1` and `Boss2` — **Brigade
General Sheba (230858) and Guard Captain Ahuradim (230857)** — as they engage. Neither sends it here,
because both run Java-parity classes rather than patterns, so this is an addition to those classes
rather than a translation. It is the largest unbuilt thing left in this instance and it is one
broadcast wide.

### A pin that measured drift, and a decoy that measured nothing

The last-phase pin took three attempts, and both failures are worth keeping:

1. **Sampling from the eighty-percent crossing** counts that crossing's own turn and the drift back to
   the most-hated afterwards. Distinct targets appeared with or without the last phase, and both
   mutations that delete it survived. The fix is to let the crossing settle and take the target he is
   left on as the baseline.
2. **A decoy with one hate point** measured nothing at all. A random turn onto it is real, and the next
   think puts him back on the most-hated before a one-second sample can see it. **The turn happened and
   the observation could not.**

**Rule: when a pin watches a target, it is watching two mechanisms — the branch that switches and the
hate list that pulls back.** Every earlier target pin in this log got away with ignoring the second
because the switch was to a *stable* choice, most-hated or lowest-HP. A random switch against a stable
hate list is visible only if you know what it was before, and only for as long as the AI leaves it
alone.

### Not translated

Twenty-two skill indices, four shouts, three `random_move`s, and `set_condition_spawn_variable
ITEMNAMED_SUM` — the phase-three subordinate wave, which retail hands to the instance rather than to
the pattern.

### Verification

Full suite **1,952 passing** and 1 skipped; five new pins run five times over; **ten mutations, all
caught**. Missing-AI 680 → **678**; translatable 336/1,110 → **334/1,108**; dead-timer payload
13 patterns / 26 actions → **14 / 28**, the extra being Ovanuka's four stranded timers.

## The Sauro Supply Base alarm, and a broadcast added to a Java-parity class

The entry before this one called `22251` "one broadcast from being real". It is now real.

| | |
|---|---|
| Brigade General Sheba (230858) and Guard Captain Ahuradim (230857) | raise the alarm at fifty metres as they engage, naming the player they are fighting |
| sheban bladesmen (233286) | answer with **three thousand** hate on that player |
| sheban legion ambushers (233277) | answer with **one thousand** |

**The weights are the only thing separating the two guard kinds** in retail's data — same message, same
two actions, a third of the commitment. A raid that peels a bladesman off the named player needs three
times what an ambusher takes, and the pin measures exactly that: two thousand hate from somebody else
moves the ambusher and not the bladesman.

### `CombatAlarm`: what a Java-parity class is missing

`PatternAi` gets `on_enter_attack_state` for nothing, because it latches the transition itself and
evaluates a whole handler there. A Java-parity class has neither: it sees `HandleAttack` on every
swing, so a broadcast written there would go out several times a second.

`CombatAlarm` is the smallest thing that closes that gap — one field, a `Raise` on attack and a
`Rearm` on the two handlers that end a fight. Both bosses keep every line of their Java behaviour
beside it.

**Rule: an addition to a Java-parity class is allowed, and it has to be shaped so the Java is still
legible.** The golden rule says the Java tree is the spec; the sanctioned exception says retail AI
behaviour outranks aionemu's approximation of it. Between them sits this case — a class that is a
faithful port of something aionemu simply never had — and the answer is to add the missing mechanic in
a form that reads as an addition rather than a rewrite. Three lines each, both pointing at the log.

The latch is pinned from both sides: a guard arriving after the pull hears nothing, and a guard
arriving after a reset hears the second pull.

### Verification

Full suite **1,961 passing** and 1 skipped; five new pins run three times over; **ten mutations, all
caught**. Missing-AI 678 → **677**; translatable and adds unchanged, the ambusher's pattern being one
branch below the ranking's threshold.

**One flake seen and not caused here.** `GuardReinforcementAiTests.AWaveLivesForItsOwnPatternsLifetime`
failed once in three full-suite runs and passed five times in isolation and twice more in the full
suite afterwards. It has nothing to do with this change — different instance, different classes — and
it is recorded here rather than left in a scrollback: **a test that fails one run in three under load
is a defect in the pin, not noise**, and it wants a look.

## The flake, and ten runs to prove there is not another

The entry before this recorded a test that failed once in three full-suite runs and passed everywhere
else: `GuardReinforcementAiTests.AWaveLivesForItsOwnPatternsLifetime`. It was not load, and it was not
the change beside it. It was arithmetic.

The garrison patrol calls its reinforcements on a **fifty-percent roll, once per twenty-second
heartbeat**. The pin waited two minutes for the first wave before asserting one had arrived — six coin
flips, so it fails about **one run in sixty**. Rare enough to look like noise; frequent enough to be
seen. The wait is now ten minutes: thirty flips, which puts the setup's failure below one in a billion,
and it costs nothing because the loop stops the moment a wave lands.

**Rule: a pin's setup must not be able to fail.** Everything after the setup is the measurement, and a
measurement that sometimes never starts teaches the reader to discount the pin — which is worse than
not having it, because the run where it fails for a real reason looks the same. Where the setup waits
on a probability, budget enough attempts that failure is not a number anyone will ever see.

This is the fourth kind of "the pin measured something other than the mechanic" in this log, after the
lifetime counts, the decoy that aggroed on its own, and the target watch that saw drift. The first
three were about *what* was measured. This one is about whether the measurement happens at all.

### And a sweep, because one flake is evidence of a habit

Ten consecutive full-suite runs after the fix: **no failures at all**. That is the whole suite, not the
one test — the point was to find out whether the same arithmetic was hiding in the other bounded waits,
of which there are nine across the pins, several of them behind probabilities. None of them surfaced in
ten runs, which is not proof but is the strongest statement available without reading every one against
its pattern's cadence.

Recorded so the next flake is measured before it is explained: **ten runs, then look at the numbers,
then change the test.**

## Priest Zitan, and a broadcast we decided not to send

`IDTP_Fanatic_Boss_EL_ve40` binds to Priest Zitan (216512), who was on plain `aggressive`. His fight is
one thing done three times — **seven illusions of melancholy, and where they land is the mechanic.**

| | |
|---|---|
| on engaging | **three** at his own feet |
| the first blow under fifty | **two more, on the player he is fighting** |
| the first blow under twenty-five | **two more**, the same way |

**The opening wave guards him and the later two chase.** Retail changes the placement rather than the
count — `SPAWN_LOCATION_MY_POINT` for the three that come with him, `spawn_on_target` for the four that
come after. A class that put all seven in one place would pass a head count and lose the fight, so the
pins read positions rather than totals and a mutation that moves either wave is caught.

**Both crossings are written twice and fire once.** Retail carries identical branches under
`on_attacked` and `on_spelled`, both behind the same flag var, so whichever kind of blow lands first
pays and the other finds the flag gone. Our runtime raises the first of those two events, and the flag
makes the pair equivalent to it — this is the cheapest of the "retail wrote it twice" cases in this log,
because the duplication is already a no-op in retail.

### The broadcast with a listener that cannot act

Each crossing also broadcasts `6915` at fifteen metres naming his target. Its only listener is the
illusions themselves, and their branch is a bare `attack_most_hating` with **no `add_hate_point`**.

That cannot redirect anything. An illusion with an empty hate list has nobody to attack most; one
already fighting is already doing it. The message is a kick into combat for NPCs that are `aggressive`
and do not need kicking — in either engine. Not sent.

**Rule: `attack_most_hating` without `add_hate_point` is not an order, it is a nudge into combat.** The
message-reach audit counts it as payload because it is in the PAYLOAD set, and for a summon that starts
passive it genuinely is one. For an aggressive summon it is nothing at all, and the difference is the
spawn's own template rather than anything in the message. Worth knowing before the next `6915`-shaped
broadcast is built on the strength of a listener that only has that one action.

### Not translated

Three skill indices on three cast timers that carry nothing else; seven shouts; the death message; and
`set_condition_spawn_variable FanaticElNBoss`, the instance's own bookkeeping.

### Verification

Full suite **1,967 passing** and 1 skipped; six new pins run three times over; **ten mutations, all
caught** — one of which had to be rewritten because deleting the last branch of a handler leaves a
dangling comma and a mutation that does not build is not a survivor. Missing-AI 677 → **676**;
translatable 334/1,108 → **333/1,107**; adds unchanged, the illusions already being spawned elsewhere.

## The abyss guards' call for help — the largest mechanic in the dump by npc count

Retail message `23000`. **Three hundred and ninety live guards**, of whom fifty cry out as they are
pulled and three hundred and eighty-five answer, across fifty-two pattern variants — the `[DL]Guard_*`,
`DirectPortal_*` and `*_Artifact_Killer` families among them.

| | |
|---|---|
| on being pulled | broadcast at the guard's own range — twenty, twenty-five or fifty metres — naming the player that pulled it |
| on hearing it, already fighting | **turn** to that player, and nothing else |
| on hearing it, standing about | one hate point on that player, and go |

**The answer is uniform to a degree nothing else in this project has been.** Forty-seven patterns carry
the fighting half and forty-seven the idle half; there is no third shape anywhere, and the hate value is
`1` in every one. The send half is not uniform, which is why the range is a table column: ten patterns
at fifty metres, seven at twenty-five, one at twenty.

**Most guards only listen.** Fifty criers against three hundred and eighty-five answerers is a fortress
with a few voices and a great many ears, and it is why pulling one guard in the abyss has never felt
like pulling one monster.

Built the way the reinforcement table was: `extract_guard_calls.py` writes a TSV a human can read
against the patterns, `emit_guard_calls_table.py` transcribes it, and `AbyssGuardCallAI` builds one
pattern per guard from it. **368 templates repointed**; twenty-two were left alone because they already
had a bespoke class — the ahserion guards among them — and those keep their own behaviour and lose the
call, which is recorded rather than silently accepted.

### The pins were passing for the wrong reason, and the reason was an introduction

Every early version of these pins called `MakeMutuallyKnown(listener, player)` in its setup. That is
enough for an aggressive guard to find the player by itself, so the pins passed whether or not the call
was ever sent. **The same mistake as the decoy that aggroed, in a new place**, and the fix is the same
shape: the player is now kept out of the listener's known list entirely, so hate on somebody it has
never seen is the only way it can arrive. The Sauro alarm pins from two entries ago had the same flaw
and are corrected here too.

**Rule: a listener must not be introduced to the thing it is supposed to be told about.** Visibility is
the mechanism a broadcast exists to bypass, so putting it in the setup removes the mechanic from the
measurement.

### Three things the aggro layer does that a pattern cannot see

Chasing one assertion turned up three behaviours worth writing down, none of which any pattern class
can observe:

1. **`AddHate` is gated on awareness *and* enmity.** `AggroList.IsAware` wants the owner to know the
   creature, not be in sanctuary, and either have it on the list already or be its enemy. A pin written
   with the default Elyos player against an Elyos guard measures a guard declining to attack its own
   side — which is correct, and is retail's `is_enemy` guard arriving from underneath rather than from
   the branch. The pins here use Asmodian players.
2. **A guard given a nudge and nothing else goes home, and the list clears on the way.** One hate point
   is not a reason to fight, so the guard returns and `AggroList` empties itself — which is why retail's
   `1` cannot be pinned as a number. It is arguably the intent expressed exactly: a nudge to join, not
   a claim on the player.
3. **A target set by a branch is sticky.** Nothing re-evaluates it until the AI has a reason to, so
   `GetTarget` answers "who was it last told about" and never "who would it fight".

### A fix written, measured, and thrown away

Believing (1) was the blocker, a `Notice` helper was added to `PatternAi` so a listener would be made
aware of whoever a message named before taking hate on them. It was then measured: **the whole suite
passes without it**, and the direct experiment that motivated it turned out to be showing (2) rather
than (1). It is removed.

**Rule: a fix you cannot demonstrate is a fix you do not keep.** The finding is worth more than the code
was, and the finding is written down here instead.

### Not translated

The rest of these fifty-two patterns, which is a great deal: every guard's cast ladder, the
`goto_waypoint` that walks it back to its post, and three further `23000` broadcasts that sit on battle
timers inside cast chains rather than on the pull. Retail's `is_enemy` guard on both halves is not
written into the branches, because the aggro layer enforces it — see (1) — and it would matter only the
day a guard broadcasts about another NPC.

**Message `30002`** is the same pair again but about the *sender* rather than its target, so one guard
sets another on the thing attacking it. Fifty-three patterns send it and four answer, covering eight of
our npcs. Left for its own pass, with the count recorded so it is not mistaken for this one's size.

### Verification

Full suite **1,973 passing** and 1 skipped; six new pins; eight mutations, **six caught** — and the two
survivors are recorded rather than papered over. One is a claim (that a listener-only guard never cries)
for which no mutation both changes behaviour and builds: the guard's own send range is zero, so forcing
the branch on still broadcasts to nobody. The other tested the `Notice` helper that is no longer there.
Missing-AI 676 → **665**; translatable 333/1,107 → **306/992**, the largest single move that list has
made.

## Archmagus Sayahum, and an escalation written into who he looks at

`IDVritra_Base_Drakan_Wi_IU_Nmd` binds to Archmagus Sayahum (233257), the third of the Sauro Supply
Base's named drakan and the only one of the three whose whole fight is about **who he is looking at**.
No summons, no marks, no messages: three phases of a cast ring with a turn at set points in each.

| | |
|---|---|
| above eighty | a four-step ring of about thirty-two seconds, and he turns on **every other lap** |
| crossing eighty | a turn onto somebody **other than** his current target, and a new four-step ring |
| below forty-five | a five-step ring, and the turn is now on **every** lap |

**The escalation is in how often he turns, not in what he casts.** Retail writes the alternation as one
flag toggled between two branches on a single timer: the lap that finds the flag set turns and clears
it, the lap that finds it clear sets it and does not. Below forty-five that pair is gone and a single
unconditional branch replaces it, so the turn rate doubles without a word about enraging — the same
trick as the Infernomane Vortile's shrinking loop, applied to the target instead of the count. That is
now twice in this log that a boss's "enrage" is a change of loop geometry, and it is worth expecting
rather than discovering.

**Both crossings move him and the in-ring turns may not.** Retail uses
`ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET` at the two phase changes and plain `RANDOM_ONE` inside the
rings — the difference between "he is off you now" and "he might turn". Pinned with **two players**, so
"anybody but the one he is on" has exactly one answer and the assertion is an equality rather than a
probability, and read over eight fights because the mutation it exists to catch leaves him in place only
half the time.

**Rule: to pin a choice that excludes something, shrink the field until the exclusion has one answer.**
With four players, swapping the exception for a plain random still moves him three times in four, and
the mutation survives. With two, the correct behaviour is deterministic and the wrong one is a coin
flip — eight of which is proof.

**The ladder stops below forty-five.** That opener does not re-arm the heartbeat, so nothing looks at
his health again; a boss healed back above eighty stays in the last phase, which is retail's way of
saying the last phase is the last one.

### One deliberate survivor

Adding an `ArmTimer(0)` back to the phase-three opener changes nothing and is recorded rather than
chased: every branch on timer 0 is behind a flag the crossings have already consumed, so the heartbeat
would fire into the fallback for ever. The claim it appears to test — that the ladder stops — is
actually held by the branch that never re-arms *timer 1*, and the pin that reads it heals him to full
and watches him keep turning.

### Not translated

Nineteen skill indices and four shouts. Every branch carries one or two casts that are the visible half
of this fight; what is ported is its shape.

### Verification

Full suite **1,978 passing** and 1 skipped; five new pins run three times over; nine mutations, **eight
caught** and the ninth explained above. Missing-AI 665 → **664**; translatable 306/992 → **305/991**.

## Grand Chieftain Saendukal, and relays that look like they stack and do not

`ND2_RnI` binds to Grand Chieftain Saendukal (211040) and his Beluslan twin (280338), both on plain
`aggressive`. The fight is a **peel ladder and nothing else**: four health bands, each opening a relay,
each relay peeling by a different rule.

| | |
|---|---|
| crossing eighty | the **weakest** player, and again every forty seconds |
| crossing sixty-five | the weakest again, on a **second** relay at thirty-five |
| crossing fifty | the **second**-most-hated, on a third at thirty-six |
| below twenty | the **third**-most-hated, on a fourth at thirty-five |

**They look as though they stack and they do not.** Every relay but the last carries its band's own
`is_hp_in_boundary` as well as its timer, so dropping out of a band silences its relay even though the
clock is still going round. The Akairun of Medeus is the boss that genuinely stacks, and **one guard per
relay branch is the whole difference between the two**. The first version of this class said "stack",
and the first version of the pin was written to prove it; the guards said otherwise.

**The last relay is the exception**: no health guard at all, so it runs to the end — and since the rung
that opens it does not re-arm the heartbeat, it is also the only relay left by then.

**Two bands peel the same way and are still two bands.** Eighty and sixty-five both take the weakest, on
separate timers at forty and thirty-five seconds, so crossing sixty-five does not change what he does —
it doubles how often he does it. A class that noticed the repetition and merged them would halve the
pressure of the fight's second half.

### Three survivors, all of them mutations that cannot change anything

- **The turn on the pull.** Retail's `on_enter_attack_state` switches to a random attacker, and at that
  moment the hate list holds only the player who pulled him — one candidate, so the switch picks it.
  The same no-op recorded for Anuhart's enter-attack switch. Ported because retail wrote it, unpinned
  because there is nothing to assert.
- **Banding the last relay.** The pin runs entirely below twenty, so adding the band guard the mutation
  proposes passes anyway. Showing otherwise needs him healed above the band with the relay still
  running, which the harness cannot do — `SetExactPercent` is not a heal the fight knows about.
- **Unbanding the eighty relay.** Once he is out of the band the relay's own branch never runs, so its
  timer is never re-armed and there is nothing left to fire whether the guard is there or not.

**Rule: a mutation that cannot change behaviour is not a gap in the pins.** Three of them here, each for
a different reason, and each worth a line — the alternative is a reader assuming the pins are weak.

### Two harness facts this fight made explicit

- **A target set by a branch is sticky.** The relay below twenty re-selects the same player every turn,
  and with nothing moving him in between every firing after the first is invisible. The pin nudges him
  back onto the tank between firings and counts arrivals; without that it read zero and the relay looked
  dead.
- **`SetExactPercent` upwards is not a heal.** Healing him back over a band boundary stopped his relays,
  which is the harness rather than the mechanic. Recorded so the next pin does not build on it.

### Not translated

Thirty-one skill indices and five shouts — he casts a great deal and none of it can be said. The `1001`
broadcast on engaging is sent; his `on_enter_idle_state` flag housekeeping does nothing observable.

### And a new blocked bucket: `is_skill_count_left`

Reading the mumu farmer patterns alongside this one turned up a guard we cannot express: **"does this
skill still have charges", 832 uses across 431 patterns, with 2,767 live unported npcs behind them.**
It is now reported in the ranking as `charges`.

It is a *caveat* rather than a blocker, and the distinction matters: ignoring the guard makes a branch
fire **more** often than retail rather than never, so the payload behind it is not dead and is not
subtracted. That is the same treatment `pathname` gets, and for the same reason — one is an attribute
and one is a condition, and neither stops the branch.

### Verification

Full suite **1,984 passing** and 1 skipped; six new pins run three times over; nine mutations, six
caught and three explained above. Missing-AI 664 → **663**; translatable 305/991 → **304/990**.

## Vengeful Modor's obscura, and a message written on both ends of a wire nobody held

`Rune_FrostNmd_MezSum_65_Ae` binds to the idean obscura (284379) and its two weakened kinds (284661,
856495), all ELITEs on plain `aggressive`, standing beside a boss they had no way of hearing.

| | |
|---|---|
| on Modor's call | take whoever she is fighting |
| below half, once | two blows in five turn it onto a **random** attacker instead |

**The call had no sender.** Retail's `444` comes from `Rune_FrostNmd_N_65_Ah`, which binds to Vengeful
Modor (234690) — and Modor runs `CursedQueenModorAI`, a Java-parity class rather than a pattern. So the
message existed on both ends and nobody was holding the wire. `CombatAlarm` holds it now, the second
time that helper has closed a gap of exactly this shape after the Sauro Supply Base alarm, and three
lines in a class that is otherwise a faithful Java port.

### The message audit cannot see this, and that is worth knowing

`audit_message_reach.py` counts a message as *sent* when a live pattern contains the broadcast. It has
no way to know that the npc bound to that pattern is running a **different class**, so a listener whose
only sender is a ported-elsewhere boss reads as connected and scores full marks.

**Rule: "a live pattern sends it" is not "the server sends it".** Fixing this properly would mean the
audit knowing what every C# class implements, which is a different kind of tool; recorded instead, with
the reminder that a listener ranked highly may be waiting on a sender that exists only in the dump. Two
encounters have now turned out this way — this one and the Sauro alarm — and both were found by reading
rather than by measuring.

### Two pins that had to be repeated to mean anything

- **Above half it never turns.** Written as one fight of forty blows, which the mutation that removes
  the health guard survives a quarter of the time — the single turn it then makes is a random pick over
  four players and can land back on the tank. Eight fights makes that one in sixty thousand.
- **Below half it turns once.** Written as `Equal(1)`, which fails one run in four for the same reason.
  It is `<= 1` now, which still catches the mutation that removes the flag var — that one turns on
  nearly every blow — and never fails for a reason nobody would want to read about.

**Rule: when the behaviour is one random pick, a pin can assert how many picks happened but not where
they landed.** Both of these were trying to observe a pick by its outcome; counting the picks is the
observation that survives.

### One pin deliberately not written

An earlier version asserted that an obscura arriving after the pull hears nothing. It failed:
introducing a new NPC to a boss that is already fighting is enough for our engine's own
see-a-friend-attacked to bring it in, with no message involved. `CombatAlarm`'s once-a-fight latch is
pinned where it belongs — in the Sauro alarm tests, against guards known to their boss before the pull.

### Not translated

Eleven skill indices; the `goto_waypoint` they walk on waking; retail's `on_spelled` copy of the
below-half branch, which shares its flag with `on_attacked` and is therefore the same one payment; the
marker each drops at Modor's own spot when killed — an invisible NPC (284528) our data already spawns
as Witch Queen Modor, whose sanctuary-release meaning belongs to the instance rather than the pattern;
and message `104`, a fifteen-minute timer whose only action here is an idle timer.

### Verification

Full suite **1,990 passing** and 1 skipped; six new pins run five times over; **seven mutations, all
caught**. Missing-AI 663 → **662**; translatable 304/990 → **303/987**.

## Who would actually say it? — an audit for listeners with no speaker

Two encounters in a row turned out to be listening for a message nobody on this server sends: Vengeful
Modor's obscura waiting on `444`, and the Sauro Supply Base guards waiting on `22251`. Both senders
exist in the dump and both run Java-parity classes here, so `audit_message_reach.py` — which asks
whether any *live pattern* contains the broadcast — scored them as connected. Both were found by
reading.

`audit_message_senders.py` finds the rest. For every message number it asks whether anybody on this
server would actually say it, and separates the three reasons the answer can be no:

| verdict | patterns | what it means |
|---|---|---|
| ported class, not mentioned | 60 | the sender runs a bespoke class of ours whose source never mentions the number |
| sender is never spawned | 40 | the sender's npcs exist as templates and nothing places them |
| no sender at all | 16 | nothing anywhere in the dump broadcasts it |

**116 listener patterns wait on 57 messages, with 191 live npcs behind them.**

The three verdicts want three different fixes, which is the point of separating them: the first is a
`CombatAlarm`-shaped addition to a class we already have, the second is a line of spawn data, and the
third is nothing at all.

**The grep is a proxy and says so.** "Mentioned in a class" is decided by looking for the number in
`Handlers/AI/*.cs`, which misses a class that sends through a constant named elsewhere and counts one
that only has the number in a comment. It is used to *exclude* rows, never to claim one is fine — and
the two buckets the audit is actually for need no proxy at all.

### The largest stranded group is a spawn gap

**Twenty-six klaw gatherers and defenders** answer `2004` with hate and an attack. Its only sender is
`ND2_CnD_BR2`, which binds to 255126, 255127 and 255131 — three klaw templates, level 33 and 34, that
our spawn data never places. The message is a relay: something tells the brood, and the brood tells the
gatherers. So the largest single stranded listener group in the dump is waiting on three NPCs that were
never put in the world, and the fix is spawn data rather than AI.

**Rule: a listener with nobody to listen to is a finding about the sender.** Every earlier audit in this
project asked what an NPC does; this one asks who it is waiting for, and the answer has pointed at
spawn data, at Java-parity classes and at genuinely dead numbers in roughly equal measure.

### What this does not do

It does not subtract anything from the worth-doing ranking. A listener with no sender is still
translatable — the branch is correct and would work the moment somebody spoke — and unlike a dead timer
or an unheard broadcast, building it is not wasted effort. It is a **sequencing** signal rather than a
scoring one: these are the patterns to build *after* their senders, not the patterns to skip.

### Verification

Full suite **1,990 passing** and 1 skipped, unchanged — this commit adds an audit and touches no server
code.

## Hyperion's defence force, found by the audit rather than by reading

`audit_message_senders.py` earned itself in one commit. Its largest "ported class, not mentioned"
group was twelve listener patterns — the `IDRuneWP_Main_*` family — waiting on message `21101`, whose
only sender is `IDRuneWp_AncientArm_N_65_Al`: **Hyperion** (231073), who runs a Java-parity class that
never mentioned the number.

He broadcasts it at fifty metres **when he dies and when he leaves the fight**, and twenty-two npcs
answer with `despawn_self` — combatants, assaulters, medics, healers, snipers, marksmen, scouts,
assassins, sorcerers, mages, a turret and a summoned tyrhund. **When Hyperion goes, the whole defence
goes with him.**

Three lines in `HyperionAI` and one branch in a new class. Third gap of exactly this shape after
Modor's obscura and the Sauro guards, and **the first the audit caught before a human did** — which is
what the audit was written for one commit earlier.

### Repointing a template can silently unspawn an NPC in someone else's pins

Giving those twenty-two npcs a class broke five pins in `VritraCallerAiTests`, which had nothing to do
with this change: the Vritra callers place exactly these troopers, and that harness registers only the
classes it names. A template repointed anywhere in the project stops spawning in any pin whose
`WithAi(...)` does not list the new class — and the failure looks like "the caller called nobody"
rather than "the AI is not registered".

**Rule: repointing a template is a change to every pin that spawns it.** The harness knows only the AI
classes it is handed, so the blast radius of an `ai="…"` edit is the set of test classes that spawn that
npc — which no tool reports and only a full-suite run reveals. It is one line to fix and half an hour to
find, and this is the second time in this project a harness registration has cost that; the first is
already in the log as "the harness registration trap".

### Verification

Full suite **1,995 passing** and 1 skipped; four new pins run three times over; **six mutations, all
caught**. Missing-AI unchanged at **662** — these twenty-two are adds rather than fights, so the
missing-AI audit never counted them — and the stranded-listener audit falls from 116 patterns / 191 npcs
to **104 / 169**.

## Ophidan Bridge's defence posts: two calls, two weights

The second group the sender audit turned up. Four **defence post generators** (230413–230416) — flags
that take one point of damage a hit and never fight back — shout twice while they are being taken, and
eight guards across five patterns answer:

| | |
|---|---|
| as the fight starts | `21212` at **thirty-five** metres: a hundred hate on the player, and go |
| on every blow after | `21215` at **fifty** metres: **turn** towards whoever landed it, and nothing more |

**Two calls with two weights, and the difference is the mechanic.** The first commits the post to
whoever pulled the flag; the second only points. A raid splitting damage between the flag and its
guards is being redirected by the second and held by the first, and one number for both would lose it.

**The flag keeps the Java class it had.** `onedmg_passive` is shared by a hundred and twelve npcs, so
the calls could not go there; `DefencePostFlagAI` extends it and adds nothing but the broadcasts, so the
one-damage rule and the stat suppression beside it are untouched.

**`CombatAlarm` was the wrong shape here and that is worth saying**, because it has been the right one
three times running. It names the owner's *target* as the message parameter, and a flag that never
fights has none — retail names the attacker on both calls. The latch is a bool and the parameter is the
creature that landed the blow.

### A pin that measured the aggro list's reach instead of retail's range

The pin for "the two calls carry different distances" put its far guard forty-five metres from the flag
on the far side — and forty-five metres from the flag is **fifty-four from the player**. A guard that
far from a player cannot take hate on them at all: `AggroList.IsAware` wants the owner to know the
creature, and knowing is a matter of distance. So the guard read zero hate whether the commitment
reached it or not, and the mutation that widens the commitment to fifty metres survived.

Moved to forty-two metres from the flag and **two** from the player, it measures what it says.

**Rule: a pin on a broadcast's range must put its listener where the listener can act.** Range decides
delivery; the aggro list decides whether anything happens. Two mechanisms, and a pin that conflates
them reads the smaller of the two — which is the fourth distinct way this log has now recorded a pin
measuring something other than the mechanic.

This is the same wall the withdrawn `Notice` helper ran into two entries ago, seen from the other side:
there it looked like an awareness bug worth fixing, and here it is plainly the aggro list working as
designed. The fix then was to delete the helper; the fix now is to place the pin properly.

### Not translated

Everything else the five guard patterns do; the flag's five `set_condition_spawn_variable` — the
bridge's own progression, which belongs to an instance handler; and message `21214`, a bridge watcher
that sees a player and points the posts at them, whose npc our data never spawns.

**Two of the ten listeners keep their own class.** The defence post and guard post rearguards (233477,
233487) run `vritra_rearguard` and answer neither call — recorded rather than overwritten, the same
call made for the twenty-two abyss guards that already had classes.

### Verification

Full suite **2,001 passing** and 1 skipped; six new pins run three times over; **nine mutations, all
caught**. Missing-AI 662 → **654**; translatable 303/987 → **299/979**; stranded listeners 104 patterns
/ 169 npcs → **92 / 143**.

## `BroadAtt`: retail's standard cry for an object that cannot fight

The third group from the sender audit, and the first that is a *family* rather than an encounter.
`BroadAtt_LR`, `_MR` and `_SR` are three patterns whose entire content is one call — **somebody is
hitting me** — naming the attacker, on every blow and every spell, at fifty, twenty-five and fifteen
metres respectively. It is how retail makes a barrel, an egg or a spawner shout for the things that can
fight.

The klaw spawner (700169) and the klawspawn (700209) carry the middle one, and what answers is a nest:

| | |
|---|---|
| klaw workers, gatherer, seeker, spriggan fighter | **a hundred** hate on whoever struck it |
| smallhorn kerub, bigfoot kerubar | **one** |

**A hundred is a claim and one is a glance.** The klaws commit to whoever struck the spawner and hold
that player against ordinary threat; the kerubs join and are moved by the next thing that happens.
Retail says all of that with nothing but `point_to_add`, and one value for both would make a field of
kerubs behave like a nest.

**Both senders keep the classes they had**, and the two are different: the spawner is `onedmg_passive`
(a hundred and twelve npcs share it) so `KlawSpawnerAI` extends it, while the klawspawn already has a
Java-parity class of its own and the call is three lines inside it. **The same retail pattern reached
two different C# base classes, and neither could be the shared one.**

### Not built, and the reason is a shared class both times

`BroadAtt_SR`'s arachna egg and the whole `BroadTalk_*` half of the family — the same call raised by
being *talked to* rather than struck — are live only on `quest_use_item`, which **six hundred and ten**
npcs share. Adding a broadcast there would give every quest object in the game a voice.

**Rule: a stock AI name is a shared class, and how shared decides whether a retail call can go in it.**
The number is the whole argument: 112 for `onedmg_passive` meant a subclass, 610 for `quest_use_item`
means not at all until those npcs are separated. Worth checking before reaching for a base class, and
it is one `grep -c` away.

### Verification

Full suite **2,006 passing** and 1 skipped; five new pins; **seven mutations, all caught**. Missing-AI
unchanged at **654** — these are objects and nest-dwellers rather than fights — and stranded listeners
92 patterns / 143 npcs → **89 / 136**.

## Dark Poeta's marabata boosters call the room, and the room answers in two weights

The fourth group from the sender audit, and the first where the **sender was already ported and the
mechanic was missing anyway**. `MarabataControllerAI` is a faithful port of aionemu's booster: it casts
its buff on its marabata, the marabata respawns it every thirty seconds, and killing it strips the
effect. Retail's `ND2_WhHS1`–`_3` agree with all of that and add one thing aionemu never had — the
booster **shouts when it is hit**, at fifty metres, and eight Anuhart patterns answer:

| | |
|---|---|
| a guard standing idle | **three hundred** hate on whoever struck the booster, and go |
| a guard already in a fight | **five hundred**, and switch |

**The larger number is for the guard that is already busy**, which is the whole point of the split.
Three hundred on an empty aggro list is already the top of it; a guard mid-fight has to be *outbid*.
Retail writes the first as `add_hate_point` + `attack_most_hating` and the second as `switch_target`
with `points_to_add`, and `HateMessageTarget` is both — for an idle guard, "most hating" and "the one
just named" are the same creature.

**Sixteen of the eight npcs' sixty-four Dark Poeta spawn spots stand inside the fifty metres**, so this
is not decoration: pulling a booster in the marabata chamber can drag most of a room that until now
stood and watched. Two of the eight (214848 anuhart spotter, 215230 anuhart breeder) have no spot in
reach at all, and retail gives them the pattern anyway — recorded, and left as retail has it.

### The call escalates itself, and the pins say so

Retail wrote the idle/attack split for a guard some *other* fight had already claimed. Because the
answer itself commits the guard, it also applies to the **second blow on the same booster**: three
hundred, then five hundred, then five hundred. That was found by a pin asserting 600 and reading 800,
and the pin now says 300-then-800 because that is what retail's two branches do when they meet.

### Two divergences, both recorded rather than hidden

**Retail names `OBJI_CUR_TARGET` and we name the attacker.** A booster's current target is *itself* —
the Java-parity class calls `TargetSelf` so it can cast its buff, and nothing ever moves it off.
Sending that would have eight guard types put three hundred hate on a golem switch. This is the same
shape as the Ophidan Bridge flag two entries ago, where `CombatAlarm` named a target a flag did not
have; **the third time now that a retail call on a non-combatant names something the non-combatant has
no meaningful value for.** Worth treating as the default suspicion rather than a surprise.

**The eighth listener could not be a `PatternAi`.** 214847, the anuhart guardian, runs `drakanmedic` —
seventy-nine npcs share it, so the answer went into neither the shared class nor a pattern table
without throwing the healing away. `AnuhartMedicAI` is a subclass that implements the single call by
hand, reading `AIState.FIGHT` instead of `PatternAi`'s own latch. **It is the guard you least want to
leave standing**, which is why it was worth the extra class: a call that pulls seven and leaves the
priest would be a quieter fight than retail's.

That makes three sharing outcomes in two commits — 112 npcs meant a subclass, 610 meant not at all,
79 meant a subclass again — and the rule from the klaw entry holds: **the count is the whole argument.**

### Not translated, and worth a look later

The rest of the water-golem chamber is a **complete retail mechanic our data does not have at all**:

* Two **controllers** (700448 `ND2_WhHC1`, 700449 `ND2_WhHC2`), never spawned by us, which cycle the
  three boosters on a sixteen-second battle timer — and **in opposite directions**. A: switch 2, 1, 3.
  B: switch 2, 3, 1. Same opening, opposite rotation.
* Each booster answers its own number (`6810`/`6811`/`6812`), waits six seconds, and broadcasts
  `6813`/`6815`/`6817` at thirty metres. **Nothing in the entire dump listens to those three**, so what
  they drive is on the client side or in a system nobody wired up. Switch 3 re-arms its own timer every
  fifteen seconds and the other two do not — recorded because it looks like a mistake in retail's data,
  not because we understand it.
* `on_killed_by_user` spawns `BIDLF1_WaterGolemN_Summoned` (281095–281097, patterns `ND2_WhHS*B`) — an
  add on booster death that aionemu has no trace of.
* Three `say_to_all` shouts, `STR_CHAT_IDLF1_ND2_WhHS_01`–`_03`, blocked on the same shout work as
  everywhere else.

Building the controllers means placing two npcs we have no retail coordinates for and adopting a
rotation whose visible effect is a client-side signal we cannot see. **Left whole and left out**, the
same call made for the Kromede chain: half of this would be worse than none of it.

Also unported: retail's `on_attacked` sets `FLAGVARI_ALPHA_1` on all three switch patterns and **no
branch anywhere reads it**. A dead flag, noted so the next reader does not go looking for the other
half.

### Verification

Full suite **2,011 passing** and 1 skipped; six new pins; **nine mutations, all caught**. Missing-AI
unchanged at **654** — the guards had classes, they simply had no ears — and stranded listeners 89
patterns / 136 npcs → **82 / 129**.

## The crater the twins log said nobody spawned

Returning to the Seal of Destruction twins for the message rows the sender audit still lists, and the
first thing found was a **wrong fact in this log**. The twins entry says of
`BIDSeal_Twin_P_Sum_Crater` (855623): *"no branch in the 5.8 files names it"*, and files it beside
Hokuruki's gunners as a template that exists for the room. That is false. It is spawned, it heads a
three-step chain, and the chain is one of the better mechanics in the fight:

```
lava protector  --22710 (50 m)-->  magma glutten 855621
                                     casts, spawns a CRATER on its own spot, despawns
crater 855623   on wake          -->  spawns CRATER SKILL 855624 on its spot, arms a 6 s idle timer
crater          every 6 s        -->  broadcast 22711 (50 m)   [erupt]
crater          on the third     -->  broadcast 22711, then despawns the skill npc and itself
crater skill    --22711-->            casts SKILLI_INDEX_0 on itself   [the lava column]
```

**Erupt, erupt, erupt-and-go**, and the counter that says so is retail's `increase_intvar` used as a
*condition*: `lower_bound=3 upper_bound=4 be_true_only_when_hit_the_bound=TRUE` on the despawn branch,
and the same counter unbounded-true on the erupt branch below it.

### How the mistake was made, which is the part worth keeping

The earlier pass searched for the **npc id**. Retail's spawn actions do not carry npc ids — they carry
`npc_nameid=BIDSeal_Twin_P_Sum_Crater`, a devname — and the id only appears in the client binding
table. So a grep for `855623` across the dump correctly returns nothing, and the conclusion "nobody
spawns it" follows from a search that could not have found it.

**Rule: a "nothing spawns this" claim has to be made against devnames, never against npc ids.** The
id-side search is still worth running — it is what catches spawn-data gaps — but it can only ever say
"no *spawn data* places it", which is a different sentence. Both previous uses of this claim in the log
should be read with that in mind. **Both have since been re-checked with
`tools/client-extract/audit_unnamed_templates.py`, and Hokuruki's gunners hold** — see the next
entry.

### What blocks the chain, precisely

Two things, and only one of them is the usual one.

**The anchor.** Retail sends `22710` from the lava protector's `BTIMERI_INDEX_11` branch — the shield
(`보호막`) cast, on a first-time `FLAGVARI_ALPHA_4` guard, so it happens once per fight the first time
the shield goes up. Our `TwinProtectorAI` is an HP-phase ladder inherited from aionemu and has no
shield branch at all; there is no honest place to hang the broadcast. Choosing one of our existing
phase steps would be inventing an anchor, which is the thing this project's own rule forbids.

**The two skill indices**, as everywhere: the glutten's `SKILLI_INDEX_1` and the crater skill's
`SKILLI_INDEX_0` (the lava column, and the whole visible point of the mechanic).

So the chain is **reachable in shape and unresolvable in placement**, and it stays out until the
protector's timer chain is translated rather than approximated. That is a bigger job than a message
wire — it means replacing an aionemu HP ladder with retail's timer graph for a fight that currently
works — and it is recorded here as one job rather than four message rows.

### An open question about `set_idle_timer` that this raised

The crater arms its idle timer **once**, on wake, and no branch re-arms it — yet its counter has to
reach three. Under our `PatternAi.SetIdleTimer`, which is a one-shot `Schedule`, the crater would erupt
once and then stand forever, because the branch that despawns it is the one that never fires.

Either retail's idle timer repeats on its own, or the crater is a leak in retail's own data. Measured
across the dump: **2,369 patterns have an `on_idle_timer` handler, and 1,276 of them re-arm inside it**
— leaving 1,093 that do not. If the timer repeated, those 1,276 would be doubling their own beat, which
argues one-shot; if it did not, the crater never dies, which argues repeating. **The two readings
cannot both be right and the data does not settle it.**

Recorded rather than guessed, because the answer changes 1,093 patterns rather than this one. Anything
we port that leans on a self-repeating idle timer should re-arm explicitly until this is settled — and
nothing currently does.

## Acting on the last entry's rule: `audit_unnamed_templates.py`

The crater correction ended with a rule and a homework item — *a "nothing spawns this" claim has to be
made against devnames, and Hokuruki's gunners are worth re-checking on the same grounds.* Both are now
a tool rather than a note.

**Hokuruki's gunners survive the re-check.** `idsweep_s1_shulack_gu_65_an_01` (235649) and `_02`
(236083): no retail spawn branch anywhere in the dump names either devname. That entry was right, and
was right for a reason the search it used could not have supplied — which is exactly why it needed
checking.

### The claim splits in two, and the old wording conflated them

| | what it means | what to do with it |
|---|---|---|
| **unnamed** | no retail spawn branch names the devname | if we do not place it, it does not exist — scenery, or an encounter nobody wired up |
| **unplaced** | retail branches *do* name it, our spawn data has no spot | a **summon**, not scenery — `audit_missing_adds.py`'s question |

Only the first justifies the sentence "nothing spawns this". The crater was the second and was written
up as the first.

### What the sweep says, including the part that is not actionable

Of the pattern-carrying npcs we ship a template for and never place:

```
32,654 unnamed      NORMAL 15,986   ELITE 10,183   HERO 4,894   LEGENDARY 1,273   JUNK 318
 2,478 unplaced
```

**The big number is not a backlog and should not be read as one.** The client ships an AI pattern for
essentially every npc in the game, and our world uses a fraction of the maps; fifteen thousand unplaced
NORMAL npcs are villages and hillsides we have not populated, not missing mechanics. The ratings split
is in the output precisely so nobody quotes the total. The 4,894 HERO and 1,273 LEGENDARY rows are the
part with encounters behind them, and even those are mostly other regions rather than gaps in the
instances we run.

**The 2,478 is the honest one**, and it is deliberately a larger number than `audit_missing_adds.py`'s
275: that audit asks the narrower question of whether the *caller* is something we can put in the
world, which is the right filter for work and the wrong one for a factual claim about the data.

### Why `--check` is the point of this tool

The sweep exists to keep the two populations separate on the page. The mode that will actually get
used is `--check`, which settles one npc:

```
python audit_unnamed_templates.py <client> <patterns> out/ai_binding.tsv --check 855623
```

and answers all three questions at once — named by retail, placed by us, template on our server. Every
future "nothing spawns this" line in this log should be a paste of that output rather than a grep, and
the two claims already in it have now been run through it.

## Queen Serusia's eggs never hatched

An Idian Depths field named, spawned twice in our world — Light and Dark — on a three-and-a-half-hour
respawn, so this is content players reach. Her egg-laying was already right: 75%, 50% and 25% for one,
two and three eggs, in `ai/spawn_helpers.xml`, against the correct npc. **What was missing was the
other half of the mechanic**, and without it the eggs were scenery the queen tidied away on dying.

```
queen  75% / 50% / 25%   lay 1 / 2 / 3 eggs, and arm a fifteen-second timer with each
queen  fifteen seconds later   broadcast 402000 / 402001 / 402002 at fifty metres
egg    on any of the three     put a larva on its own spot, and go
larva  when the fight ends     go
```

**Fifteen seconds is the mechanic.** An egg that lives out its timer is a larva; an egg killed first is
nothing. That is the whole decision the fight offers a raid, and this server has never offered it.

### Retail's three numbers are decoration, and that is a mechanic too

Three timers, three message numbers — and **one listener that answers all three identically**. So
whichever call comes due first hatches every egg standing, including eggs laid at a later threshold
whose own timer has ten seconds left. A raid that pushes her from 75 to 50 quickly gets all three at
once and a raid that takes its time gets them in clutches.

That is retail's arithmetic rather than an approximation of it, and it is pinned, because writing three
listeners each answering "its own" number would look tidier and be wrong.

### A divergence in shared aionemu code, recorded rather than fixed

**One blow that crosses all three thresholds lays all six eggs at once.** `SummonerAI.CheckPercentage`
walks every threshold in a single pass, so a burst from full to a quarter fires 75, 50 and 25 together.
Retail spreads them across three blows: its three branches are separate priorities in one
`on_attacked`, retail's handlers are **first-match-wins**, and the 75% branch answers the first blow
alone before its `increase_intvar` guard steps aside for the next.

Left as it is. `CheckPercentage` is aionemu's and **fifty-one npcs share `summoner`** — the same count
argument as the klaw spawner's 112 and the anuhart guardian's 79, and here it lands on "not without a
reason bigger than one boss". The difference only shows when a single hit crosses more than one
threshold; a fight that descends normally lays 1, 2 and 3 on separate blows either way. **Both
behaviours are pinned**, so if `CheckPercentage` is ever made first-match-wins the pin that says six
will fail and point here.

### The larva gets its own class, for a reason worth stating

`GhostRun_Sum_As_N_65_Ae` is one branch — leave when the fight is over — and it would have been easy to
leave the larvae on `aggressive` and let the queen tidy them. **She cannot.** `SummonerAI` tracks what
*it* spawned, and a larva was spawned by an egg. Without the class, a hatched larva whose target walks
away stands in the Idian Depths until it decays.

**Rule: when a summon summons, the outer boss's cleanup does not reach the inner one.** Worth checking
wherever a two-step chain is ported — the crater chain in the twins entry is exactly this shape and
would need the same care.

### One mutation survives on purpose

The `IsDead` check inside the scheduled hatch call cannot be pinned: by the time it could matter the
queen's death has already taken the eggs, so no test can tell a guarded call from an unguarded one.
Reported as a survivor rather than papered over with a pin that would pass either way — the check is
there because ours is a scheduled task where retail's is a battle timer that stops with the fight.

### Also repaired

`RetailSummonTests.QueenSerusiaLaysMoreEggsAsSheWeakens` registered only `SummonerAI` and advanced five
seconds per step. Both would now break it — the queen has her own class, and three five-second steps
reach the first clutch's incubation mid-test. It registers the real classes and steps two seconds, so
it still measures the laying while the new file measures the hatching.

### Not translated

Her two combat skills on their alternating fifteen-second loop and the self-buff she casts on waking
and on leaving combat — skill indices, as everywhere.

### Verification

Full suite **2,018 passing** and 1 skipped; seven new pins and one repaired; **nine mutations, eight
caught and one unpinnable by construction**. Stranded listeners 82 patterns / 129 npcs → **78 / 125**.

## Ashunatal Shadowslip's shadows were all the same shadow

Aturam Sky Fortress, and the same shape as Queen Serusia one entry ago: his three waves were already
right in `ai/spawn_helpers.xml` — one decay shadow at 90%, three explosion shadows at 70%, two
disruption shadows at 50% — and everything that made them *different* was missing. All three arrived
and stood there and fought until killed.

Retail gives each of the three its own pattern, and they are three genuinely different things:

| | | |
|---|---|---|
| **explosion** (217379) | `Station_Shadow1` | a **bomb on a twelve-second fuse** — engages, waits, shouts, casts once, gone |
| **decay** (217380) | `Station_Shadow2` | the one that is **not** a bomb: casts on engaging and every twelve seconds after, forever |
| **disruption** (217381) | `Station_Shadow3_1` | **splits** fifteen seconds in — one more of a different npc, or **two on a thirty percent roll** — then stops |

The explosion shadow arms its timer on entering combat and never re-arms it, which is exactly what
makes it a fuse rather than a beat; the disruption shadow does the same, which is what makes its split
a one-shot. **Both of those "never re-arms" are load-bearing** and both are pinned, because a class
that re-armed either would look right for the first fifteen seconds of a fight.

### And then he sweeps the board

**At forty percent** retail despawns his own spawn group *and* broadcasts `7063` at a hundred metres,
and all four shadow patterns — including the children — answer it by leaving. That step did not exist
here at all.

### Retail's belt-and-braces is the point, not redundancy

Why both a group despawn and a broadcast? Because **the disruption shadow's children belong to its
spawn group, not his**. `despawn SPAWN_ID_1` cannot reach them; the broadcast can.

This is retail confirming, in its own data, the rule the Queen Serusia entry reached from the other
side one commit ago — *when a summon summons, the outer boss's cleanup does not reach the inner one*.
There it was a bug we had to avoid; here it is a problem NCSoft's own designers hit and solved, and the
solution was to stop relying on the group and shout instead. **Two independent arrivals at the same
rule in two commits is about as strong as this log gets**, and it is worth applying forward: any ported
two-step chain wants a broadcast rather than a group despawn as its cleanup.

Our class therefore sends only the broadcast. `SummonerAI`'s tracked-spawn cleanup is private, and
duplicating it would clear strictly less than the call already clears.

### Not translated

Every skill on all four patterns — the blast at the end of the fuse, the decay shadow's entire content,
the disruption shadow's cast, and his own self-casts on each wave and at forty percent. **The explosion
shadow's fuse therefore runs out and the shadow simply leaves**, which is the honest half: the timing,
the one-shot and the sweep are real, and the damage is not. Also out: his four shouts, `control_door`
on his death, and messages `7061`/`7062`, whose only listeners are the two `Station_NinjaCTRL` npcs —
instance furniture our data never places.

### Two pins repaired, and one of them for a good reason

`RetailSummonTests.AshunatalSplitsOffADifferentShadowAtEachStep` registered only `SummonerAI` and drove
to 40% for its "counts do not grow" step — which is now the sweep. It stops at 45 instead.

Its explosion-shadow assertion also had to change, and that change *is* the feature: after four steps
of five seconds the three explosion shadows are **gone**, because they engaged and their fuses ran out.
The pin now says so and cites the class. A test that had to be edited because the fight got a mechanic
is the right kind of breakage.

### Verification

Full suite **2,025 passing** and 1 skipped; seven new pins, two repaired; **eleven mutations, all
caught** — after the first sweep turned up two that were not defects (arming one timer slot twice is
the same as arming it once, and a message number shared by sender and listener changes on both sides),
replaced by a mutation that turns the split into a beat and a pin on the number itself. Stranded
listeners 78 patterns / 125 npcs → **75 / 122**.

## The seam behind the last two commits: `audit_mute_adds.py`

Queen Serusia's eggs and Ashunatal's shadows were found one at a time, and they were the same finding
twice: **a boss whose summon table was already correct, whose adds already arrived in the world, and
whose adds did nothing**, because everything that made them a mechanic lived in a retail pattern nobody
had translated. The eggs never hatched; the three shadows were all the same shadow.

Twice is a seam, so it is now a tool. It takes every npc our own `ai/spawn_helpers.xml` and `bombs.xml`
summon, keeps the ones carrying a retail pattern, drops the ones already on a bespoke class, and
reports what is left with a payload count and its handler list.

**Sixty rows. Forty-six of them behind a boss something on this server actually places.**

This is a nastier category than a missing add. A missing add is visibly absent; a **mute** add arrives,
stands in the room, and looks like the mechanic working.

### The liveness filter, and why it is not just the spawn xml

The first row the filter was tried on was Commander Bakarma, and reading `spawns/` alone called him
unreachable — **he is placed by `DraupnirCaveInstance` in C#, not by spawn data.** A boss-liveness test
that only reads the xml would have buried a live Draupnir Cave mechanic at the bottom of the report
and sent the next session to a fight nobody can walk into.

So `placed_ids` reads both, and the C# half is a grep for `Spawn(<id>,`. That is a proxy in both
directions — it over-reports an id that merely appears as a literal, and under-reports one built from a
variable — and the column says **placed** rather than *spawned* for that reason. **Rule: on this
server, "is it in the world" is two questions, and the xml is only one of them.** The devname/id lesson
from the crater entry has a sibling here: the right search depends on which side of the port you are
standing on.

### What is at the top, and what each row costs

| payload | add | boss | what makes it interesting |
|---|---|---|---|
| 28, 28, 25 | laksyaka magus ×2, ambusher | 286933 / 219609 | `friend_spelled` and `see_friend_attacked` — a support pair that reacts to each other |
| 18, 17 | anuhart lookout, fighter | 214843 | `stop_to_flee` — retail's "runs, then comes back shouting" |
| 17, 13, 11, 10 | the four **summoned udas** | 215793 | full stock handler sets; most of the payload is generic |
| 10, 9 | the two Bakarma **legionaries** | 213780 | a transformation ladder, and `see_friend_killed_by_user` |
| 9 | fire spirit | 214163 | `killed_by_npc` / `killed_by_user` |

**The payload count ranks, it does not decide.** The udas rows carry twenty-odd handlers each because
their patterns are built on a stock template, and most of that is the same idle behaviour every drakan
in the game has; the Bakarma legionaries carry ten and every one of them is the encounter. Reading the
pattern is still the job — this only says which ones are worth opening.

### What this does not cover, and should not be read as covering

Only adds **our own summon tables** name. An add a retail pattern summons and our data does not is
`audit_missing_adds.py`'s question, and an npc nothing anywhere summons is
`audit_unnamed_templates.py`'s. Three tools, three different sentences, and the crater correction is
what taught this log to keep them apart.

## Commander Bakarma promotes his legionaries twice, and ours never grew up

The first row worked off `audit_mute_adds.py`, and the tool earned itself immediately: Bakarma is the
boss whose liveness only the C# side knows about — `DraupnirCaveInstance` places him, the spawn xml does
not — so a report built on the xml alone would have skipped him.

His two legionary ranks arrived from a summon table that was already right, and then stood there being
the same legionary all fight. Retail promotes them, on his own health:

| | |
|---|---|
| between **26% and 50%** | `5001` — every legionary within fifty metres becomes a **vanguard** where it stands |
| below **25%** | `5002` — every vanguard starts a **six-second** countdown and becomes a **relic guardian** |

**The ladder is a promotion, not a wave.** Neither call summons anything new: each add replaces itself
on the spot it occupies, so the count does not grow and the fight does. A raid that leaves adds alive
through a band is fighting something else by the end of it — and this server has been letting it.

### The asymmetry is the mechanic

**The first rung is instant and the second takes six seconds.** A class that made both instant would be
simpler, and would throw away the only window in the ladder a raid can act inside: kill a vanguard
inside its countdown and no relic guardian appears. Both halves are pinned, the second one from both
sides.

### Both steps are HP-anchored in retail, which is why they could be built at all

Retail fires them from a battle-timer branch that also casts four skills, and this log has twice now
had to leave a mechanic out for want of an anchor — the twins' crater, whose `22710` hangs off a shield
branch we do not have. Here the guards are `is_hp_in_boundary 26..50` and `is_hp_lower_than 25`, each
with a once-only flag var, which is exactly what `HpPhases` already is. **When* in the band is a timer
we cannot reproduce; *which* band is data.**

### Two things deliberately left out, and both are countable

**Message `6001`**, retail's "everyone onto my target" call. He sends it on a repeating timer whose
period changes with the band — thirty seconds above twenty-five percent, forty below — from branches
that are otherwise all skill indices, and the ladder has gaps at 80–100 and 50–56 that only the timer
chain produces. A plain beat would fire in those gaps. **It is absent from the classes rather than
approximated, deliberately**, so that `audit_message_senders.py` keeps listing it as work: a class that
merely mentions a number counts as a sender to that audit, so building a half-right `6001` would have
hidden it.

**`on_see_friend_killed_by_user`**, which all three ladder patterns carry and which is the raid's answer
to the ladder — kill one in front of the others and the rest leave. **Our AI event set has no
equivalent event at all.** It is not a skill index and not an anchor; it is a missing event, and it is
worth a number: **129 retail patterns carry that handler, with 377 npcs bound to them.** Building it
means adding the event to the AI layer, and the alternative — a death broadcast on an invented message
number, or a hand-picked "sight" radius — would put a made-up constant into the retail number space.
Left out on those grounds.

### A flake caught in passing, and hardened

`ArchmagusSayahumAiTests.TheLadderStopsBelowFortyFive` failed once in one full-suite run and passed in
the two after it and in five runs on its own. Its claim — more than one distinct target over the
window — is **probabilistic**: the switch picks a random attacker, and with four players on a
twelve-second beat a hundred and twenty seconds can land on one player throughout. The window is now six
hundred seconds.

Same fix as the guard-reinforcement flake several entries back, and the same rule: **a pin's setup must
not be able to fail.** Recorded rather than quietly widened, because a pin that fails one run in three
is worse than no pin — it teaches the next session to re-run instead of read.

### Verification

Full suite **2,031 passing** and 1 skipped; six new pins, one hardened; **eleven mutations, all
caught**. Mute adds 60 rows / 46 live → **58 / 44**.

## The event aionemu never had: `on_see_friend_killed_by_user`

The Bakarma commit shipped a promotion ladder without its counter-play, and named the reason: retail's
"one of us went down" event has **no equivalent anywhere in our AI layer**. It was the largest single
structural gap this log has measured that is neither a skill index nor an anchor — **129 patterns in
the 5.8 files carry the handler, with 377 npcs bound to them** — and what hangs off it is nearly always
one action: the survivors leave.

It exists now. `AiEventType.FriendKilled`, raised by `Ai/FriendDeathNotice`, dispatched through
`AbstractAI` as a virtual that does nothing by default, and surfaced to tables as
`AiPattern.OnFriendKilled`.

### Three decisions, and the point is that none of them is a number I chose

Last commit refused to build this because the obvious implementations all needed a made-up constant.
Each one turned out to have an answer already in the data:

| question | answer | why it is not an invention |
|---|---|---|
| who hears it | the dead NPC's **known list** | how every other broadcast on this server finds its audience |
| how far | **each watcher's own `srange`** | retail's event is a *seeing* event, so the range belongs to the eye, not the corpse |
| who is a friend | `TribeRelationService.IsFriend` | retail's word is `friend`, and that is what the word already means here |

**The range one is the interesting answer.** The instinct is a single radius around the body, and that
is exactly the made-up constant that blocked this. Retail's handler is `on_see_...`, which says the
question belongs to the observer — so a bigfoot kerubar with forty metres of sight really does notice a
death a klaw with eight would miss, and no constant is needed because every npc already ships its own.

**And `killed_by_user` is load-bearing.** The notice fires only for a player kill. An add finished off
by its own `live_time`, by another npc, or by a boss sweeping the board is not what the handler is
about — and a ladder that emptied itself on those would be a very different fight. Pinned from both
sides.

### What it unlocks immediately

Commander Bakarma's three rungs — legionary, vanguard and relic guardian — all answer it, so **killing
one in front of the others empties the ladder**. That is the raid's whole counter-play to a fight whose
adds otherwise promote twice and never shrink, and it shipped missing two commits ago.

The remaining 126 patterns are now a table-writing job rather than an engine job.

### An open flake, reported rather than fixed

`AnuhartGuardAiTests.TheGuardianAnswersFromItsOwnClass` failed in **one full-suite run out of seven**
and passed in three isolated runs and in the six full runs either side. Its assertions are
`Assert.Same(raider, guardian.GetTarget())` and a hate of 300, delivered by one message with no random
branch anywhere on the path.

**I could not reproduce it on demand and I have not fixed it.** Recording the measurement rather than
tightening the pin until it stops failing, because the honest reading is "something outside this test
occasionally moves that guardian's target", and the two candidates — a scheduled task surviving from an
earlier test on a shared pool, or an aggro tick the virtual clock does not own — are both worth finding
properly. Contrast the Sayahum flake in the previous entry, which *was* diagnosable: its claim was
openly probabilistic and widening the window was the right fix. This one has no such explanation yet,
and inventing one would be worse than the flake.

### Verification

Full suite **2,034 passing** and 1 skipped (with the intermittent above); three new pins on the event
itself; **six mutations, all caught** — including the two that matter most, "anything's death raises
the notice" and "enemies hear it too".

## The flake that took four attempts, and is still open

The previous entry recorded an intermittent failure and said the candidates were "worth finding
properly". This is that work. **It is not fixed.** Four hypotheses, three real bugs found along the
way, and one decisive experiment that ruled out the explanation all three of them assumed.

### The symptom, measured

An AI pin fails in roughly **one full-suite run in seven** and passes every time it is run alone. It is
not always the same pin: `AnuhartGuardAiTests.TheGuardianAnswersFromItsOwnClass` three times,
`KlawSpawnerAiTests.TheKlawspawnSharesTheCall` twice, `ArchmagusSayahumAiTests` twice — and in two of
those runs `GameServerBootstrapTests` fell over in the same pass, which is the most useful single
observation here: **whatever this is, it is not confined to the AI tests.**

### Four hypotheses

**1. A probabilistic window.** `ArchmagusSayahumAiTests.TheLadderStopsBelowFortyFive` asserts more than
one distinct random target over a window. That claim really is probabilistic and the window really was
too short, so it was widened from 120 to 600 seconds. **A real improvement. Not the cause** — other
pins with no random branch anywhere kept failing.

**2. A race against the guard's own aggro scan.** The Anuhart pins asserted an *absolute* hate of 300
on a guard standing seven metres from an enemy player — which is an aggressive npc next to something it
will find on its own eventually. Those assertions are deltas now, which is a better statement of the
mechanic regardless. **A real improvement. Not the cause** — it failed again.

**3. A class outside the serialising collection.** `QueenSerusiaAiTests` had been added three commits
earlier without `[Collection("GoldenDataManager")]`, and `ChatAuthenticationBridgeTests` had a
**private** collection with `DisableParallelization` — which serialises a class against itself and
against nothing else, so it ran beside every AI test while swapping the global `GameWorld` and
`DataManager`. Both fixed, and `SingletonIsolationTests` now enforces the rule so neither can recur.
**Two real bugs. Still not the cause.**

**4. Parallelism at all.** The decisive test: `[assembly: CollectionBehavior(DisableTestParallelization
= true)]`, so nothing in the assembly can run beside anything else. **Twelve full runs. Two still
failed.** The attribute was reverted rather than kept, because a change that costs five seconds a run
and does not do what its comment claims is worse than no change.

### What that leaves, and it is worth writing down precisely

With parallelism excluded, the remaining explanation is **state surviving one test into the next**, and
the harness's `Dispose` is the place to look:

* It restores `ThreadPoolManager` to `_previousThreadPool ?? new ThreadPoolManager(...)` — a **real**
  pool. Any task a harness scheduled on its `VirtualThreadPool` and never advanced far enough to run is
  abandoned, but anything that captured the manager rather than looking it up each time is not.
* It restores `GameWorld` the same way. NPCs spawned during a test are not individually removed; the
  world object is swapped. An AI still holding a reference to an NPC from the previous world will act
  on it.
* `GeoDataConfig`, `AIConfig` and `InstanceConfig` statics are saved and restored, so an exception
  thrown between construction and `Dispose` leaves them set.

**The most testable of those is the abandoned-task path**, because it predicts exactly the observed
shape: a stale scheduled action firing during a later test and moving a target or deleting an npc that
the later test is asserting about.

### The rule this cost, and it is not the rule I expected

Three of the four hypotheses were reasonable, two of them found genuine bugs, and **none of them was
right**. Each was adopted because it explained the last failure seen. The thing that finally moved the
investigation forward was not another hypothesis but the observation that a *non-AI* test failed in the
same pass — evidence that had been sitting in the output for two commits.

**Rule: when a flake survives a fix, the next move is a decisive experiment, not a better hypothesis.**
Disabling parallelism outright answered in twelve runs what four rounds of reasoning could not.

### Kept from this work

* `SingletonIsolationTests` — every test file that calls `RegisterInstance`/`RestoreInstance` on
  `DataManager`, `GameWorld` or `ThreadPoolManager` must be in the one serialising collection.
  Source-scanning, because the thing to look for is a call and reflection cannot read method bodies. It
  found two offenders on its first run.
* `QueenSerusiaAiTests` and `ChatAuthenticationBridgeTests` in that collection.
* The Anuhart pins as deltas, with the reasoning in the class remarks.

### Still open

The flake. **Roughly one full-suite run in seven, cause unknown, parallelism ruled out.** Anyone
picking this up should start at `BossAiHarness.Dispose` and the abandoned-task path above, and should
treat a green run as meaning nothing — the failure needs seven or more full runs to show itself.

## The flake, found: it was four different bugs wearing one costume

The previous entry left this open after four wrong hypotheses. It is closed. **Twenty consecutive
clean full runs**, against a baseline of roughly one failure in seven.

The reason four hypotheses failed is that there was never one bug. There were **four**, each rare
enough on its own to hide behind the others, and each producing the same outward shape: a different
pin failing, no random branch on its own path, green when run alone.

### 1. A poisoned static initialiser — the big one

`SiegeService` is a singleton built in a **static field initialiser**, and its constructor takes the
live path whenever `SiegeConfig.SIEGE_ENABLED` is true — which is the default. That path reads
`DataManager.SIEGE_LOCATION_DATA` and calls `SiegeDAO`, so whether it succeeds depends on which
`DataManager` happened to be registered the first time anything touched the type.

**A static type initialiser runs once per process, and if it throws the type is poisoned for the rest
of it.** One unlucky ordering therefore breaks every later test that reaches
`NpcAI.Ask(ALLOW_RESPAWN)` — which is most AI tests, and the bootstrap tests too. Measured on one pin:
**7 solo failures in 50 before, 0 in 50 after.**

Fixed with a `[ModuleInitializer]` that turns sieges off, touches the type so it initialises on the
harmless branch, and puts the flag back. **Not a production fix** — a real server touches it after the
data and database are up — but a type that one bad early access can poison permanently is a sharp edge
worth revisiting on its own terms.

### 2. A harness that did not know what the fight could spawn

`AnuhartMedicAI` extends the Java-parity `drakanmedic`, which rolls **three percent on every blow** to
call a servant. The pins never registered `DrakanHealingServantAI` or `EnemyServantAI`, so about one
run in twenty the harness threw *"No AI found for name drakanhealingservant"*. **3 in 50 before, 0 in
50 after.**

**Rule: `WithAi` must list what the fight can spawn, not what the test spawns.**

### 3. and 4. Two under-powered probabilistic pins

`SilikorOfMemoryAiTests.BothKindsOfServantAppear` counted **survivors at the end** of a ten-minute
window. A servant lives three minutes, so it was really sampling the last six coin flips and failing
whenever those came up the same way — `2 × 0.5⁶ ≈ 3%`, measured at **1 solo run in 40**. It now watches
the whole window through a new `BossAiHarness.WatchEach`, which is `Watch` kept apart per npc id.

That helper's own remarks say it exists "because the same mistake has been made four times". **This was
the fifth**, and it happened because `Watch` could not express *how many of each* — a helper that
almost fits gets bypassed.

`UnstableTriroanAiTests.OverManyCallsEveryElementTurnsUp` gave itself forty attempts to see four
elements; one is roughly a one-in-ten call, so `0.9⁴⁰ ≈ 1.5%`. Two hundred now, and the loop still
stops the moment all four appear. `ArchmagusSayahumAiTests.AboveEightyHeTurnsOnEveryOtherLap` had the
same shape at two hundred seconds; six hundred now.

### What actually cracked it, and the rule that follows

Not another hypothesis. **Reading the exception text instead of the assertion.** Every earlier run had
been filtered with `grep -E "Assert|Expected|Actual"`, and the failure had been printing
`TypeInitializationException` the whole time — the bug was reporting its own cause into a pipe that
threw it away.

**Rule: when a test fails intermittently, print the whole failure before theorising about it.** Four
rounds of reasoning, two reverted changes and one abandoned experiment cost more than one unfiltered
run would have.

The second-order lesson is about the shape: *one symptom, four causes* is why each fix "did not work".
Each one really did remove a slice of the failure rate; none removed enough to notice. **A flake that
survives a correct fix is evidence of a second cause, not of a wrong fix** — the opposite of what was
assumed here for three commits running.

### Kept from the two commits of chasing

`SingletonIsolationTests`, and the two classes it caught outside the serialising collection — real
bugs, just not this one. The Anuhart pins as deltas and their geometry, worth having and now correctly
labelled as *not* the fix. The `SiegeService` module initialiser, `WatchEach`, three widened pins, and
two servant classes registered.

Reverted: the assembly-wide `DisableTestParallelization`, which cost five seconds a run and fixed
nothing.

### Verification

**Twenty consecutive full-suite runs, all green** — 2,035 passing, 1 skipped. Per-pin: 0 in 50, 0 in
50, 0 in 60, 0 in 40, 0 in 40.

## Ranking the mute adds by what we can actually build, and three dead ends worth recording

`audit_mute_adds.py` shipped with a caveat in its own entry — *"the payload count ranks, it does not
decide"* — and then the very next session went to the top of its report and found the caveat was the
whole story. That is now fixed in the tool rather than repeated in prose.

### The top three rows were the least actionable in the report

`IDCT_DrakanWi` (the laksyaka magus, twice) and `IDCT_DrakanAs` (the ambusher) sat at **28, 28 and 25
payload** — the top of the list by a wide margin. Reading them: almost every action is
`use_skill SKILLI_INDEX_n`. Every skill index in the dump is still unresolved, so the three highest
rows were the three least buildable.

The audit now counts **buildable payload** — spawns, broadcasts, despawns, timers, hate and target
switches — and excludes `use_skill` and `say_to_all`, both of which are blocked on work of their own.
The laksyaka rows drop from 28 to 14, and the report reads honestly for the first time.

**Rule: a ranking that includes work you cannot do is a ranking of the wrong list.** Worth applying to
the other audits — `audit_translatable.py`'s "at least 4 payload actions" has the same shape and has
never been checked against it.

### Where that leaves the backlog

Forty-four live rows, and the buildable counts run 3 to 14 with most of the mass low. Nothing in the
list is another Queen Serusia — a whole mechanic missing behind a correct summon table — and that is
itself the finding: **the two big wins were the two big wins.** What is left is mostly adds whose
patterns are skill rotations with a timer skeleton around them, and those will not move until skill
indices do.

### Three dead ends, recorded so they are not re-walked

**The fire spirit** (296347, `DrGuard_WhAPet`, summoned by 214163 at 75% and 20%) looked promising:
`on_killed_by_user` and `on_killed_by_npc` both broadcast `10018` at fifty metres, which reads like
"the pet dies and tells its master". It does not. The only listeners for `10018` anywhere in the dump
are `DrGuard_WhA_Reward`, `_Reward_L50` and `DGuard_Kistenian` — **other encounters entirely** — and
the master's own pattern, `DrGuard_PhB_L48`, does not listen for it. The pet's death call has no
recipient in its own fight.

**The four summoned udas** (281501–281504, boss 215793) carry twenty-odd handlers each and rank high on
raw payload for that reason. The handler list is a stock drakan template — `see_npc`, `see_user_move`,
`most_hating_updated` and so on — shared by most drakans in the game; the encounter-specific content is
thin.

**`10011`**, which the fire spirit answers by casting on the message parameter, is one of the low
generic numbers (`5001`, `6001`, `10011`) used by unrelated patterns across the dump — doors, Dramata
guards, Kistenian. Anything built against those numbers has to be bound to npcs that only exist in one
encounter, which the Bakarma commit already had to reason about.

### Not done, and why

No behaviour changed this session. The backlog it was working from turned out to be ranked by the
wrong number, and re-ranking it was worth more than translating the first row of a bad ordering — but
it does mean the honest report is that this was a tooling session.

## The corasks that burst, and the eighteen-row list that found them

The previous entry flagged `audit_translatable.py` as possibly mis-ranked in the same way
`audit_mute_adds.py` had been. **It is not, and the flag was wrong.** That audit already separates
payload from scaffolding, already excludes `use_skill` entirely by counting it as *blocked*, and
already subtracts payload sitting behind timers nothing arms. Its own entry records the Belsagos
lesson that caused all three. Correcting the flag is worth more than repeating it.

What it *was* missing is a way to read it. The report sorts by payload, so the useful rows — the ones
with **nothing in the blocked column at all** — sit scattered down the list. There are **eighteen** of
them out of two hundred and ninety-nine, and one `awk '$3=="-"'` brings them up.

### What the first of them turned out to be

Six live field mobs of Cygnea and Enshar — ebon, black, lurking and burrowing corask, wily and swamp
gnarl — all on stock `aggressive`, each carrying a complete four-branch retail pattern:

**Once, below half health, three clodworms appear on the attacker.** Not on the corask: retail's
`spawn_on_target target_obj=OBJI_ATTACKER`, three metres apart, arriving with a hundred hate and
already swinging. They go when it dies, when it leaves the fight, when it goes idle, and when it
returns to its spawn point — four separate despawn branches, so a swarm never outlives what made it.

**Four patterns rather than one, because the swarm is level-matched**: 284155 at sixty-one, 284157 at
sixty-three, 283903 at sixty-five, and the sulphur gnarl its own 283904. One shared id would have put
a level-61 swarm on a level-65 fight, and the four classes exist only to carry those four numbers. Two
mutations pin exactly that.

**Not translated: nothing.** These four patterns are complete, which is the first time this log has
been able to write that line, and it is the whole argument for the eighteen-row list.

### A number read rather than assumed

The arrival hate reads **101**, not the hundred retail writes. `AttackAfterSpawn` adds one more when
the summon actually starts swinging. Pinned as 101 with that noted, rather than rounded to retail's
figure or loosened to "greater than zero" — either would hide a change in one of the two numbers.

This is the second time in two sessions that reading the actual value beat assuming it. The flake
entry's rule — *print the whole failure before theorising* — generalises: **print the actual number
before asserting one.**

### Two dead ends from the same list

**The mumu farmers and workers** (`Ratman_FnR_LWaSu11`/`12`/`13`, five live npcs at levels 11–15) look
like the best find in the game: below forty-five percent they call for help at twelve metres *and*
summon a lycan warrior. Both blocked. Every branch is gated on `is_skill_count_left SKILLI_INDEX_0`,
which is the largest condition blocker in the dump — 832 uses across 431 patterns and 2,767 npcs — and
without the skill there is no way to know how many uses bound it. Building it ungated would have a
mumu summon on *every* blow below half. Left out.

Worth recording separately: the call itself would be silent anyway. `1007`'s only listener patterns are
`Ratman`, which is bound to **zero** npcs, and `Lycan_KnA`, bound to five that do not stand near mumus.

**The Tiamat beacons** (four patterns, 11 payload each) are blocked purely on `path` — the walker-route
work — and are the strongest argument for doing that job: nothing else stands between them and being
built.

### Verification

Full suite **2,044 passing** and 1 skipped; nine new pins; **eight mutations, all caught**. Translatable
299 patterns / 979 npcs → **292 / 970**, and the no-blocker list 18 → **14**.

## `is_race` was readable all along, and the village killers prove it

Second row off the no-blocker list, and it turned up something bigger than the encounter.

### The guard this log recorded as unusable

A comment in `PatternAi` said `is_race` "is not readable from the pattern dump, where the element
appears with no argument at all", and the sealed akaimum was discriminated by npc id because of it.
**That was wrong.** All **2,879** `is_race` conditions in the 5.8 files carry a `race_type`;
`summarize_pattern.py`'s `KEEP` list simply did not name the field, so every summary printed a bare
`is_race`.

**Third time a dropped value has produced a wrong conclusion**, after `point_to_add` (read out of raw
XML by hand three times before anyone noticed) and the devname/npc-id confusion that made the twins'
crater "spawned by nothing". `KEEP` now carries `race_type`, `from`, `point_to_add`, `points_to_add`
and `percent_to_add`.

**Rule: a summariser that hides a value will eventually be quoted as evidence that the value does not
exist.** The three tools that drop fields should be read as lossy, and a claim about what retail *does
not* say has to be made against the raw XML.

### What the thrashers do

Four stonereach and flamecrest thrashers of Cygnea and Enshar, all on stock `aggressive`. **The moment
one sees a garrison chief it commits to it with five million hate points** — not a weight, a statement:
nothing a player does will peel it off the garrison it came for. A player walking past fails the race
guard and is ignored, which is what keeps this from being "attacks the nearest thing".

**The squads hunt different factions.** The `01` patterns watch `gchief_light` and `gchief_dark`; the
`02` patterns watch `gchief_light` and `gchief_dragon`. One class with one race list would send
flamecrest thrashers after Asmodian chiefs they ignore in retail.

New vocabulary: `AiPattern.OnSeeNpc`, `When.SeenRace`, `When.AttackerRace`, `Do.HateSeen`,
`Do.HateAttacker`, and `PatternAi.SeenCreature` / `LastAttacker`.

### Two halves not shipped, and one reason for both

`AggroList.AddHate` refuses hate on a creature the owner is not an enemy of, and **our tribe table
makes a thrasher and a Balaur garrison friends**. So retail's `02` patterns hunting `gchief_dragon`
translate into a call that lands and a hate that does not — measured as **zero** against five million
for the Elyos and Asmodian garrisons, same guard, same action, same npcs.

It is **pinned as zero rather than forced past the aggro list**. The choice is between retail's pattern
and our tribe table, and routing around either to make a test green would bury the question. The
`on_attacked` and `on_spelled` halves are deferred on the same measurement; `When.AttackerRace` and
`Do.HateAttacker` are built and wired, and what they run into is this gate.

### A harness rule, seen from the other side

The pins spawn real garrison templates, and the first dragon one runs `base_protector` — so the harness
threw *"No AI found for name base_protector"*. That is the rule the flake commit recorded for `WithAi`
turned around: **a test must not spawn an npc whose class the harness was not told about**, and it
applies to the props as much as to the encounter.

### Verification

Full suite **2,048 passing** and 1 skipped; four new pins. The no-blocker list 14 → **10**.

**Not mutation-swept.** The turn ran out on the aggro-list investigation, and a sweep of a class whose
second half is deliberately absent would mostly measure the absence. Worth doing when the tribe
question is settled.

## The tribe table was right and the class was wrong

The previous entry left the village killers with a deferred half and an open question: retail's `02`
patterns hunt `gchief_dragon`, our aggro list refused the hate, and that was written up as a possible
disagreement between retail's data and our tribe table, to be settled by someone choosing a side.

**There was no disagreement.** Reading all six patterns instead of two:

| pattern | hunts |
|---|---|
| `_L` | `gchief_dark`, `gchief_dragon` |
| `_D` | `gchief_dragon`, `gchief_light` |
| `_DR` | `gchief_dark`, `gchief_light` |

**The suffix is the killer's own side, and each hunts exactly the other two.** The three lists are
"everyone but me", and `01` and `02` are two village sets with identical rules. No raiding party ever
hunts its own faction's garrison.

The first version of this class read `01` and `02` as the axis and shipped two classes instead of
three — which handed a **Balaur** raider a **Balaur** garrison to hunt. The aggro list refused, entirely
correctly, because they are friends. **The refusal was the tribe table catching a bug in the class**,
and it was written up as evidence against the tribe table.

**Rule: when our engine refuses to do what a translation asks, suspect the translation first.** The
aggro list, the known list and the tribe relations have now each been suspected of being wrong at some
point in this log; every time, they were right. That is three for three, and it is enough to be a
prior.

The class is three now, keyed on faction, and the nine-case theory pins every cell of the table —
including the three "never its own" zeroes that would have caught the original bug.

### The deferred half, and what it actually is

`on_attacked` is translated and is **not** pinned, and the reason is recorded rather than glossed:
`BossAiHarness.Engage` adds its own thousand hate without raising the AI attack event, and raising
`AiEventType.Attack` by hand adds nothing at all. Measured in order: **0** with the wrong faction, **0**
with the right one, **1000** through `Engage` — which is `Engage`'s figure, not the branch's five
million.

So the branch does not run on that path, and the cause is in the harness or in `HandleAttack` rather
than in the table: `When.AttackerRace` and `Do.HateAttacker` are the same guard and action shape the
sighting half proves working. It is a skipped test carrying that measurement, **not a passing one** —
a pin asserting 1000 would be pinning `Engage`.

`on_spelled` is not translated at all: our engine has no pattern handler for it, so a caster garrison
that never lands a melee blow is not committed to.

### Verification

Full suite **2,055 passing**, 2 skipped; eleven pins, up from four; **six mutations, all caught** —
the sweep the previous entry owed, including one per faction that pins "never its own".

## Locating the `on_attacked` fault, and sizing what it blocks

The village killers shipped with their `on_attacked` half translated and unpinned, and the previous
entry could only say "0, 0, 1000". This turn narrows it to one call, and measures what it costs.

### Three experiments, each ruling out one suspect

| experiment | result | rules out |
|---|---|---|
| remove the race guard, plain strike | still nothing | `When.AttackerRace` |
| replace the action with `Do.DespawnSelf` | **the NPC despawns** | `Evaluate(Pattern.OnAttacked)` — the branch runs |
| hold `LastAttacker` past the branch instead of clearing it in a `finally` | no change | the attacker reference being lost |

**The branch runs and the action does nothing.** What is left is `Do.HateAttacker`'s
`AggroList.AddHate`, called from inside `HandleAttack` — against a creature the *same call* reaches
happily from `HandleCreatureSee`: five million there, nothing here, same pair, same value, same guard
shape.

The likely shape is **re-entrancy**: `base.HandleAttack` runs first and is itself working the aggro
list, so an `AddHate` issued from a branch underneath it is dropped. That is a hypothesis and is
labelled as one — the last time this log adopted a hypothesis without a decisive test it spent three
commits on the wrong four.

### The speculative fix was reverted

Holding `LastAttacker` past the branch is arguably better naming and it changed nothing, so it went
back. **Shipping a behaviour change to every `PatternAi` on a guess is worse than a documented gap**,
and a diff that fixes nothing while claiming to is exactly what the parallelism experiment already
taught this log to revert.

### What it blocks, counted

**Nothing already shipped.** Exactly one class in the tree has an `OnAttacked` branch that adds hate —
`VillageKillerAI`, whose half is the skipped pin — so no existing mechanic is silently dead. That was
worth checking before writing this up as an emergency.

**139 retail patterns, 198 npcs**, put `add_hate_point` or `switch_target` on `on_attacked`. That is
the size of what stays unbuildable until this is settled: every "rounds on whoever just hit it" reaction
in the dump.

### The next step, precisely

Instrument `AggroList.AddHate` for a refusal reason and call it once from each of the two paths on the
same pair. Two calls, one difference. Whoever does it should start from the fact that the branch runs —
that is the expensive half of the search and it is already done.

## `on_attacked` was never broken: the setup had already spent the flag

The previous entry located a fault to one call — `AggroList.AddHate` from inside `HandleAttack` — and
sized what it blocked at 139 patterns and 198 npcs. **There is no fault.** The branch works, and every
reading of zero was measuring a once-a-fight branch whose flag the test setup had already used.

### What the earlier experiments actually proved

All three were correct and all three pointed away from the real answer:

* the race guard was not at fault — **true**;
* the branch runs — **true**;
* the attacker reference was not being lost — **true**.

Each ruled out a suspect, and the suspect that was never on the list was **the baseline**. Bringing two
NPCs into each other's view runs the sighting branch *and*, through the engine's own attack path, the
`on_attacked` one. Every measurement took its "before" reading after that, so a branch guarded by
`FirstTime` had already fired and correctly added nothing the second time.

### The experiment that showed it

Giving the branch a second, visible action — `Do.DespawnSelf` beside `Do.HateAttacker` — made the
raider **vanish during setup**. The pin's own direct `AddHate` afterwards then read zero, because it was
adding hate to a despawned NPC. That single reading, `despawned=True direct=0`, is what turned "the
action does nothing" into "the action already ran".

Measured after: **ten million** on the aggro list straight out of setup — two applications of retail's
five million, one per handler.

### The rule, and it is not the one the last entry expected

**When a once-only branch reads zero, check whether the setup already spent it.** A `FirstTime` flag
makes a branch invisible to any measurement that starts after the first firing, and the symptom is
identical to a broken action. The instinct — instrument the action — is why two commits went past it:
the action was working every time it was asked.

This is the second time in this log that a careful elimination pointed at the wrong thing because the
*measurement* was the fault rather than the code, after the flake that turned out to be four bugs. Both
were found by making the mechanism visible rather than by reasoning further. **Prefer an experiment
that changes what you can see over one that narrows what you suspect.**

### Corrections to the previous entry

* "139 retail patterns and 198 npcs stay unbuildable" — **withdrawn**. Nothing was blocked. That number
  is now simply the size of the `on_attacked` reaction work that is available.
* The re-entrancy hypothesis — **withdrawn**. `AddHate` from inside `HandleAttack` works.
* The skipped pin is a passing one, and the class's "deferred half" note is gone.

### Verification

Full suite **2,056 passing** and 1 skipped — the skip is gone. Twelve pins on this class; **six
mutations, all caught**.

## Retail's threat assistance for tanks, and the recursion that was hiding under it

The `on_attacked` seam the last entry unblocked turned out to hold one coherent mechanic, and building
it surfaced a genuine engine defect.

### The mechanic: a templar's blow counts for thousands more

Five Catacombs bosses carry `is_user_class user=USERI_ATTACKER class=CLASSI_KNIGHT` on `on_attacked`,
with nothing but an `add_hate_point` behind it. **Nothing is cast and nothing is said** — a templar
attacking one of these bosses simply counts for far more on the aggro list than anyone else. It is how
a tank holds a Catacombs boss, and without it the boss is held by whoever does the most damage. That is
a materially different fight and one nobody would think to file as a bug.

**One rule, four weights, and they are not ordered the way the difficulty is:**

| | |
|---|---|
| Taros Lifebane, normal | **35,000** |
| Captain Lakhara, both modes | **22,000** |
| Flarestorm, hard | **5,000** |
| Taros Lifebane, **hard** | **5,000** |

Taros's hard mode helps a templar **seven times less** than his normal one. Both are read out of the
dump, and it is exactly the asymmetry a single per-instance constant would erase — so the weight is per
class and a mutation pins each one.

`CLASSI_KNIGHT` is `PlayerClass.TEMPLAR`, and that is not an inference: the enum already carries the
client's own naming in its comments — `TEMPLAR, // knight`, beside `GLADIATOR, // fighter` and
`SORCERER, // wizard`.

### The defect it surfaced

The first run of these pins died with **`StackOverflowException: Aborted abnormal AI event recursion`**.
Adding hate notifies the controller, the controller raises another attack event, and that runs
`on_attacked` again — so **any branch that adds hate on every blow recurses** until the engine's
cut-off fires.

Retail fires `on_attacked` once per attack, not once per change to the hate list, so ignoring the
nested events is the faithful reading as well as the safe one. `PatternAi.HandleAttack` now holds a
re-entrancy flag, and a mutation that removes it is caught.

**The village killers hid this for two commits.** Their branch is guarded by `FirstTime`, so its flag
stopped the recursion after a single pass and the bug looked like correct behaviour — and the same flag
is what made every measurement of that branch read zero. **One once-only guard produced two different
false readings of the same code.**

### The rule

**A once-only guard is a silencer.** It suppressed a crash here and a measurement there, and in both
cases the code underneath was doing something other than what it appeared to. Worth reaching for the
*unguarded* case first when a branch behaves oddly: the Catacombs rule, which retail deliberately leaves
unflagged, exposed in one run what the village killers hid across three entries.

### What is left on this seam

139 retail patterns put `add_hate_point` or `switch_target` on `on_attacked`; **nine** of them are on
live npcs still running a stock AI, and five are these. The other four — `CKrall_FeA`, `CKrall_ReA`,
`IDRuneWP_A1_VriIU_Wi_SN_65_Ah` and `Britra_Party_Wi_MpDrain_LowNmd` — carry one to three skill indices
each alongside the reaction, so their hate half is buildable and their timers are not.

### Verification

Full suite **2,066 passing** and 1 skipped; ten new pins; **seven mutations, all caught**, including one
that removes the re-entrancy guard.

One mutation first read as a survivor and was not: replacing `PlayerClass.TEMPLAR` hit the XML doc
comment above the code rather than the guard. **A mutation that edits a comment is not a survivor, it
is a bad mutation** — retargeted at the guard expression, it is caught.

## The threat rule is eight bosses, and every two-mode boss helps less on hard

Following `is_user_class` as a seam rather than stopping at the five bosses that carried it on a
skill-free branch. **74 patterns in the dump use the guard; 14 are on live npcs still running a stock
AI.** Three more of those turned out to be the same Catacombs rule:

| | |
|---|---|
| Ahbana the Wicked, normal | **30,000** |
| Ahbana the Wicked, **hard** | **5,000** |
| The Soulcaller | **5,000** |

That makes eight bosses on one rule, and it exposes a pattern the first five only hinted at: **every
boss with two modes helps a templar less on hard.** Taros Lifebane 35,000 → 5,000, Ahbana 30,000 →
5,000, and Captain Lakhara alone keeps 22,000 in both. The theory pin now asserts the *direction* as
well as the figures, so a future boss added with the two backwards fails on the relationship rather
than only on the number.

They are Beshmundir Temple npcs on our server — `BeshmundirInstance` places all three, none is in the
spawn xml — and the retail prefix is `IDCT_`. The classes stay named for the patterns, as everything
else here is.

### Left on the seam, and why

**`IDAbRe_Core_Giant` and `_Golem`** (enos grappler and watcher, plus their unstable variants) carry the
same guard on a **battle timer** rather than on being hit: every four seconds, if the attacker is a
knight, a hundred thousand hate points go onto `OBJI_CUR_TARGET` and the timer re-arms.

That is not translatable as written. Retail's `USERI_ATTACKER` on a *timer* branch means "the most
recent attacker", and our `LastAttacker` is deliberately scoped to the `on_attacked` event and null
everywhere else — a decision the recursion guard in the previous entry depends on. Widening it to a
persistent "last attacker" is a real change to `PatternAi` with its own consequences, and it is not
worth making for two npcs without measuring what else reads it.

**Recorded rather than approximated**, because the obvious approximation — hate on the current target
regardless of class — turns a tank-assist into a flat 100,000 every four seconds on whoever the boss is
already holding, which is not the same mechanic at all.

The remaining nine live rows carry six to twenty-six skill indices each; their class guards sit on
branches whose payload is casting.

### Verification

Full suite **2,070 passing** and 1 skipped; fourteen pins, up from ten; **eight mutations, all caught**.

## The vasharti watch: a pack that drifts rather than snaps

Third row off the no-blocker list, and the most interesting thing about it is a number that looks like
a mistake and is not.

A watch post that engages **broadcasts every three seconds for as long as the fight lasts**, naming its
current target at twenty-five metres, and every watcher in earshot puts **one** hate point on that
player and goes.

**One point is the whole design.** It is nowhere near enough to take a player off whoever they are
already fighting — the klaw nest's *"a hundred is a claim and one is a glance"* — so what this builds
is not a snap-aggro but a **drift**: every three seconds the post edges further onto one target, and a
group that stays too long ends up fighting all of it. A hundred would make the post collapse onto the
first player instantly, which is a cruder fight and a much easier one to write by accident.

### The pin had to change shape, and the reason is the mechanic

The first pin asserted one point per three seconds and read three, then six. That is not a defect: **a
neighbour that takes a point engages, and an engaged watcher starts calling too**, so a post feeds
itself and the rate depends on how many of them are within earshot of each other. Pinning an exact
schedule would have pinned the harness's geometry.

What is pinned instead is the shape — it grows, it keeps growing, and every step is glance-sized (a
mutation that makes the glance a claim is caught by the last of those). The same emergence broke the
"beat stops with the fight" pin, which was sending only the caller home while the neighbour it had
recruited kept calling; that too is correct, and the pin now retires both.

**Rule: when a pin on a pack mechanic reads the wrong number, check whether the pack is the reason
before changing the class.** Two pins here, and both times the surprising number was the feature.

### Faithful where retail is odd

Retail's opening shout carries `param_obj=OBJI_SELF`, so a neighbour answering it tries to put hate on
a friend and the aggro list refuses. The opening call is effectively "I am fighting" with no payload,
and the timer that follows does all the work. **Translated exactly as written rather than tidied into a
second target call** — the difference is one wasted broadcast in retail and would be one extra player
pulled here.

### Not translated

Message `900`, which a dying watcher broadcasts at twenty-five metres. **Nothing in the entire 5.8 dump
listens for it** — this pattern is its own only sender and its own only listener, and `900` appears on
neither side of anything else. Left out rather than given an invented meaning.

### Verification

Full suite **2,075 passing** and 1 skipped; five new pins; **seven mutations, all caught**. Translatable
288 patterns / 966 npcs → **287 / 962**, and the no-blocker list 10 → **9**.

## Adma's zombie traps, and the coin flip that is a reprieve

Fourth row off the no-blocker list. A trap in the Adma Stronghold corridor that has been a harmless
prop since the instance was ported: it goes off when a player walks past, puts suspicious zombies on
them three metres apart, and is gone in the same branch.

### The unlucky roll gives you fewer zombies, not more

Retail writes two branches, and the one carrying `test_probability 50` spawns **two** while the
fall-through spawns **three**. Half the time the coin flip is a **reprieve**, not a punishment.

Reading the priorities the other way round produces the opposite fight, and it is invisible unless both
counts are pinned separately — which four mutations now do, one per direction of getting it wrong.

### `on_see_user` is its own handler slot

Retail keeps `on_see_user` and `on_see_npc` apart and the split is load-bearing: a trap that fired when
the guard beside it wandered into view would be spent before anyone arrived. `AiPattern.OnSeeUser`
exists as its own slot for that reason, and `HandleCreatureSee` routes on what was seen.

New vocabulary alongside it: `When.Enemy` (`is_enemy who=OBJI_SEEN`) and `Do.SpawnOnSeen`
(`spawn_on_target target_obj=OBJI_SEEN`).

### Two guards this npc cannot falsify, reported as survivors

The mutation sweep caught five of seven and **the two survivors are honest ones**:

* **`is_enemy`** — a monster is hostile to every player, so no player exists that fails the guard.
  Removing it changes nothing that can be observed with this trap.
* **the user/npc split** — the natural pin has the trap see *another trap*, which `is_enemy` would
  reject anyway, so a mutation routing NPCs through the user handler also survives. Catching it needs
  an NPC hostile to the trap, and picking one on a guess is how a pin ends up measuring the tribe table
  instead of the split.

**A pin written to kill one of those mutations would have passed for the wrong reason**, which is the
failure mode this log has now recorded four times. The pin that would have been written was deleted and
the reasoning put in its remarks instead.

**Rule: a guard that nothing available can falsify is reported, not covered.** The alternative —
inventing a fixture until the mutation dies — buys a green sweep and loses the information that the
guard is untested.

### Verification

Full suite **2,078 passing** and 1 skipped; three pins; **seven mutations, five caught and two
unfalsifiable with this npc**. The no-blocker list 9 → **8**.

## Beshmundir's decoy liches, and a wire whose two ends were in different places

Fifth row off the no-blocker list, and the first where the two halves had to be found separately: the
**senders** are live and were stock, and the **listener** was a bespoke class that already existed.

Three invisible markers (281696, 281759, 281760) sit in the Beshmundir room, spawned by our data and
running `general` — doing nothing. Retail has each of them broadcast at fifty metres the instant it
wakes and then delete itself, and **every lich in range removes itself in answer**. That is how the room
is left holding a single Macunbello instead of a row of identical ones.

`MacunbelloAI` already implemented `INpcMessageListener` for the soul reapers' curse report, so the
answer is four lines inside it — the klawspawn shape again. **Retail puts the decoy branch at priority
100 DIRECT, ahead of the curse report**, so a lich that is about to vanish does not first stop to devour
somebody; the ported handler checks it first for the same reason.

### The marker exists for exactly one broadcast

Call and self-delete are in the same branch. Retail writes that body **twice** — on `on_wake_up` and on
`on_see_npc` — the second for a marker placed before its liches are, firing on the first one that comes
into view rather than on a player.

### An honest survivor: the two branches are alternatives

The mutation that removes the wake branch **survives**, and it should. Any setup with a lich present
makes the marker see it, so `on_see_npc` fires the identical body and the outcome is the same. Catching
it needs a marker that wakes with a lich in range but not in its known list — which is a state the
world does not produce.

**Retail wrote belt and braces and the belt cannot be tested while the braces are on.** Reported rather
than covered, per the rule the zombie traps set one entry ago: a guard nothing available can falsify is
reported, not papered over with a fixture invented until the mutation dies.

### Not translated

The `display_system_message` beside each call (`STR_MSG_IDCatacombs_NmdLich_weakness1`), blocked on the
same string-id work as every shout.

Also worth recording: `IDCT_DespawnLich` (281697) sends the same `6981` and **our data never places
it**, and the six other lich-king ids (216733–216738) are unspawned. The wire works because the ends we
do have — three markers and one Macunbello — happen to be the live pair.

### Verification

Full suite **2,081 passing** and 1 skipped; three pins; **seven mutations, six caught and one an
alternative branch that cannot be isolated**. The no-blocker list 8 → **5**, three patterns at once.

## Vallakhan's illusions, and an npc two bosses share

Sixth row off the no-blocker list. Vallakhan's illusions were already in `ai/spawn_helpers.xml` and
already arriving; what was missing was everything that made them *illusions*.

**One blow and an illusion is gone.** Its whole pattern is three ways of leaving and one way of
engaging: it pops when attacked, it leaves when the fight ends, and it answers Vallakhan's call by going
for whoever he named. **They are not adds, they are a distraction with a cost** — two land on the
player he is holding and immediately attack, and each takes exactly one blow to remove, so the question
they ask a group is whether the two seconds are worth more than the damage. An illusion with any real
health would be a different fight.

### The collision, and what it says

Repointing 281524 broke six pins in an encounter that has nothing to do with Vallakhan: **Priest Zitan
summons the same npc**. Retail binds 281524 to `IDTP_Fanatic_Elementalearth2` regardless of who called
it, so giving it the retail class is the right answer and Zitan's harness simply had to be told about
it — his six pins pass unchanged, which is the useful part: Zitan's mechanic never depended on his
illusions being inert, it just never knew they were not.

**Rule: a repoint is a change to every encounter that summons the npc, not to the one you are reading.**
The template file has no back-reference, so the only warning is a test suite that already covers the
other fight. Worth a grep of the id across `Handlers/AI` before repointing anything a boss summons.

### A survivor, and a correction to how it was first written up

The mutation removing "pops when attacked" **survives**, and the first version of this entry claimed a
fix for it that did not work.

The pin originally asserted only that the illusion was gone after a blow, which cannot be told from an
illusion that leaves on its own. Adding "untouched, it is still standing ten seconds later" then failed
outright: at two metres the illusion aggroes the raider by itself, fights, and despawns through the
*other* branch. The raider now stands sixty metres off and the blow is delivered as an event, so the
"untouched" half holds — **and the mutation still survives.** Two despawn branches on one npc, and no
window this harness offers separates them.

Reported at **4 of 5** rather than presented as five. **A pin on "X causes Y" needs the case where X
does not happen**, which is the same shape as the once-only baseline mistake from the village killers —
but having the right shape is not the same as having a pin, and the difference was nearly published as
a success.

### Not translated

Retail's `on_spelled` branch, which pops the illusion for a caster the same way, guarded on
`is_hp_lower_than 99` so a spell doing no damage leaves it standing. Our engine has **no `on_spelled`
pattern handler**; a caster who never lands a melee blow does not pop one here. Same gap recorded for
the village killers, and now twice is enough to name it as work: `on_spelled` is a handler slot the
port does not have.

**Vallakhan's own call is built and not pinned.** His summon table fires the spirit at 99% and the
illusions at 75%, and in the harness only the spirit arrives however the descent is staged — that is
`SummonerAI`'s scheduling, not this class. The listener half is pinned against a directly delivered
message instead. Also recorded: **our thresholds are not retail's** — retail summons at 75/40/20 for
two, two and three illusions where our table has 75/30/10 for two each. Changing encounter data is a
different job from translating a pattern, so it is written down rather than quietly corrected.

### Verification

### And a commit made while the suite was red

The verification command chained the commit after a `grep` of the test output rather than after the test
run, so a non-zero suite exit could not stop it. `a5bcc0106` went in with `OneBlowAndTheIllusionIsGone`
failing. Fixed in the commit that follows, and worth stating plainly: **`dotnet test … | grep … && git
commit` commits on the grep's exit code.**

### Verification

Full suite **2,086 passing** and 1 skipped, over three consecutive runs; five pins; **five mutations,
four caught**. One LoginServer test failed once in the run that produced the bad commit and has not
reproduced since — recorded, not chased.

## `on_spelled`: the largest handler gap the port had

The previous entry said two encounters had now wanted this and that twice was enough to name it as
work. Measured, it is much more than two: **1,170 patterns in the 5.8 files carry `on_spelled`, with
5,300 npcs bound to them** — the biggest single handler gap this port has had, and aionemu has no
counterpart for it.

It exists now: `AiEventType.Spelled`, raised from `CreatureController.OnAttack`, dispatched through
`AbstractAI` as a virtual no-op, and surfaced to tables as `AiPattern.OnSpelled` with
`PatternAi.LastCaster` alongside.

### The one decision, and the data made it

**An `Effect` is what distinguishes a skill from a swing.** The damage path already carries one — it is
null for an auto-attack and set for a skill — so the event goes exactly there and needs no new
plumbing, no guess about what counts as "spelled", and no second call site to keep in step.

It is guarded against re-entrancy the same way `on_attacked` is, for the reason the Catacombs bosses
found: a branch that adds hate notifies the controller, and the controller can come straight back
through the handler.

### Two encounters closed with it

**Vallakhan's illusions** now pop for a caster exactly as they do for a melee player, and retail's
`is_hp_lower_than 99` guard is translated with them — a spell that does no damage leaves the illusion
standing, so a buff is not a way to clear the room. Both halves pinned.

**The village killers** get the half deferred twice for want of this event. Retail carries the identical
body on both handlers **and one flag var across them**, so a raiding party commits once however it was
provoked; the pin asserts that a later cast adds nothing rather than a second five million, because
asserting five million would be asserting two flags.

New vocabulary: `When.CasterIsEnemy`, `When.CasterRace`, `Do.HateCaster`.

### Two honest survivors

* **"the event is never raised"** — the pins fire `AiEventType.Spelled` directly, so removing the line
  in `CreatureController` changes nothing they can see. **The call site is unpinned**, exactly as
  `FriendDeathNotice`'s was, and for the same reason: a harness that reaches the AI layer directly does
  not exercise the engine path into it.
* **"the re-entrancy guard is removed"** — nothing in these pins produces a nested spelled event, so the
  guard is carried on the `on_attacked` precedent rather than on evidence of its own.

Both are gaps in coverage rather than defects, and are reported as survivors rather than covered with
pins written to kill them.

### Verification

Full suite **2,090 passing** and 1 skipped; four new pins across two encounters; **five mutations, three
caught and two unpinned call sites**.

## The stoneskin stoffu splits, and the first row worked off the `on_spelled` seam

`on_spelled` unlocked 1,170 patterns; **fifty of them have buildable payload on that handler and sit on
live stock-AI npcs.** The stoffu is the first, and it needed the new event to be worth doing at all —
half its pattern lives there.

**It sheds a piece of itself twice and points it three seconds later.** Once between 65% and 35% and
once below 35%, it drops an angolem fragment at its feet and arms a timer; when the timer runs out it
calls at forty metres naming its current target, and the fragment takes a hundred hate and goes.

**The delay is the mechanic.** A fragment that arrived already fighting would be an add; three seconds
of it standing inert is a window to kill it in, and the call is what closes the window.

**Each band pays out once however the stoffu was provoked** — retail writes the band twice, on
`on_attacked` and on `on_spelled`, with the same flag var across both. The caster half is additionally
guarded on `is_enemy` and the melee half is not; that asymmetry is retail's and is kept.

### A retail quirk kept rather than tidied

The melee branch for the **upper** band arms `BTIMERI_INDEX_1` while every other branch arms `INDEX_0`,
and only `INDEX_0` has a handler. So a stoffu first provoked into the upper band **by a melee blow**
drops its fragment and never calls it — the piece stands there until the fight ends.

Translated as written. A tidied version would quietly make the upper band work and would be a different
fight; this is the third time this log has kept a retail branch that plainly does nothing, after the
dead flag on Dark Poeta's switches and the self-naming shout in the vasharti watch.

### One survivor, and what it says about the two bands

The mutation raising the lower band's floor from 35 to 65 **survives**. Both bands then match inside
the upper one, but the upper branch wins on priority and spends its own flag first, so the second
provocation reaches the lower branch in either version and the totals agree. A pin added specifically
for it — four provocations at 40%, expecting one piece — did not separate them either.

**Two bands that overlap are indistinguishable by counting.** Telling them apart needs the timer slot
each one arms, and the upper band's slot is the one retail leaves unhandled — so the observable
difference is precisely the quirk above. Reported rather than covered.

### And a number read rather than assumed, again

The fragment's arrival hate reads **101** against retail's 100 — one more lands when it actually starts
swinging, exactly as the corask clodworms read. Pinned as read.

### Verification

Full suite **2,096 passing** and 1 skipped; six pins; **seven mutations, six caught**.

## Message numbers are not all encounter-scoped, and three investigations paid to learn it

This log has assumed throughout that a `broadcast_message` number belongs to one encounter. Mostly it
does. The exceptions have cost a wasted investigation each:

* **`1007`** — the mumu farmers' call for help. Its only listener patterns are one bound to **zero**
  npcs and one bound to five that do not stand near mumus.
* **`10018`** — a pet's death cry. Its listeners are two `Reward` patterns and Kistenian, all other
  encounters.
* **`10000`** — the surkana feeder's five HP-banded broadcasts, found this session. Every listener is a
  `BIDF5_U01_Runaway_*` pattern from a different instance.
* **`5001`, `6001`, `10011`** — low numbers used by doors, Dramata guards and unrelated bosses, which
  the Bakarma commit had to reason about before binding classes to them.

Three wasted investigations and one near-miss is enough to make it a check rather than a habit.
`audit_generic_messages.py` counts **how many pattern files each number appears in**: the dump is split
by area and designer, so a number confined to one file is confined to one encounter and a number spread
across a dozen is a shared vocabulary meaning "somebody hit me" or "come here".

**1,329 distinct numbers; 77 span four files or more.** The head of the list is exactly what one would
hope — `1001` in 34 files, `1002` in 21, `1003` in 15, `10` in 13 — small numbers that are plainly a
common language.

### The tool is a prompt, and the false alarm is worth naming

**`6981` spans five files and was built successfully two entries ago.** The Beshmundir decoy-lich call
looked shared and was not: every sender turned out to be the same mechanic, which reading them showed.
The list says *read the senders before binding a class to this*; it does not say the number is unusable,
and a tool that had been trusted as a verdict would have talked this session out of a correct build.

The threshold is three files rather than four because `1007` and `10011` — two of the four cases that
prompted it — span exactly three. That choice buys the two cases and costs more false alarms, which is
the right trade for something that only ever says "go and read".

### What it says about the stranded-listener backlog

Cross-referenced against `audit_message_senders.py`: of the **47** messages that 75 listener patterns
wait on, only **three** are on the shared list — `11103`, `11111`, `21300`. **The stranded backlog is
almost entirely real.** That is the useful negative result: the remaining gaps are genuine missing
senders rather than an artefact of numbers being reused, so the backlog can be worked as written.

### The surkana feeder, recorded rather than built

282291, live, `general`, five HP-banded broadcasts on `on_attacked` and five more on `on_spelled` —
the top skill-free row of the `on_spelled` seam. Left alone: its `10000` has no listener in its own
fight, and building a sender whose call nothing hears adds a row to the stranded backlog instead of
removing one.

### Verification

Tooling and documentation only; no behaviour changed. Full suite **2,096 passing** and 1 skipped,
unchanged.

## The fortress killers: built, unverifiable, reverted

Worked the stranded-sender backlog now that the previous entry established it is almost entirely real.
The best row was `25307` — the fortress killers of Cygnea and Enshar (234106, 234108), the siege
siblings of the village killers, both live and both on stock `aggressive`.

The mechanic reads cleanly and is worth writing down for whoever picks it up:

* **`on_enter_attack_state`** — if the current target is a garrison chief of either faction, **a hundred
  million** hate points on it. Five million said "nothing a player does will peel this off"; a hundred
  million says the same thing about a siege.
* **`on_message 25307`** — the same hundred million on the **message sender**. A guard boss shouting
  "me, here" and pulling its killers onto *itself* rather than onto a player. **That is the opposite
  direction from every call this log has translated**, and it is what makes a fortress fight a convoy
  rather than a mob.

Both were built, and the class is **reverted** rather than shipped.

### Why it could not be verified

Four of six pins read zero. The tribes explain it: **234106 is `LDF5_V_KILLER_D` and race ASMODIANS;
234108 is `LDF5_V_KILLER_L` and race ELYOS** — the fortress killers are faction-sided npcs, and the
garrison npc the pins reached for was `219641 furious dux`, tribe `PROTECTGUARD_LIGHT`, a **Beluslan**
guard rather than a fortress garrison. Whether `AddHate` refused because the tribe table has no hostile
relation for that pair, or because the pair is genuinely the wrong one, cannot be told apart without
the right npc.

**Picking a garrison on a guess is exactly what the zombie-trap entry warned against** — a pin that
measures the tribe table instead of the mechanic. So the class went back rather than shipping four
unverified branches behind two passing ones.

### The next step, precisely

Find the npcs the fortress guard bosses actually protect — the `LDF5_Fortress_*GuardBoss_*` patterns
name them, and none of those six npcs is in our spawn data either — and check
`TRIBE_RELATIONS_DATA.IsHostileRelation` for `LDF5_V_KILLER_D` against that garrison's tribe. If it is
hostile, the class as written is correct and the pins simply had the wrong prop. If it is not, the
question is the same one the village killers raised and answered: **suspect the translation first, then
the table.**

Recorded with the class's full reading above so the next attempt starts from the mechanic rather than
from the pattern file.

### Kept from the turn

`When.TargetRace` and `Do.HateTarget`, which are correct, general, and cost nothing to leave in place —
the fortress killers are not the only pattern that guards on the current target's race.

### Verification

Full suite **2,096 passing** and 1 skipped, unchanged. No behaviour shipped.

## The fortress killers, settled: the translation is right and the gate is Java's

The previous entry left this open with a named next step — find the garrison the guard bosses protect
and check the tribe relation. Both done, and the answer is not the one either candidate predicted.

### The tribe table is right, and it independently confirms shipped work

```
LDF5_V_KILLER_D   aggro  LDF5_V_CHIEF_L   LDF5_V_CHIEF_DR
LDF5_V_KILLER_DR  aggro  LDF5_V_CHIEF_L   LDF5_V_CHIEF_D
LDF5_V_KILLER_L   aggro  LDF5_V_CHIEF_D   LDF5_V_CHIEF_DR
```

**Everyone but its own, exactly as retail's three race lists say.** Cross-checked against the four
village killers shipped two entries ago: 234104 and 234107 are `_KILLER_DR` and run
`village_killer_balaur`, 234105 is `_KILLER_L` and runs `village_killer_elyos`, 234109 is `_KILLER_D`
and runs `village_killer_asmodian`. **Four for four.** The faction fix stands on two independent
sources now.

The garrisons are `LDF5_V_CHIEF_L/D/DR` — 231630, 231631, 231632 — not the `PROTECTGUARD_LIGHT`
Beluslan dux the first pins reached for. That was the wrong prop, as suspected.

### And with the right props it still reads zero, for a reason that ends the investigation

`AggroList.IsAware` gates every `AddHate` on

```
aggroList.contains(creature) || creature.IsEnemy(owner) || IsHostileRelation(owner.tribe, creature.tribe)
```

and these tribes relate by **`<aggro>`**, not `<hostile>`. `IsAggressiveRelation` exists and
`TribeRelationService` uses it — but it is not on this path.

**Our `IsAware` is character-for-character Java's**, including that omission:

> `owner.getKnownList().knows(creature) && … && (aggroList.containsKey(…) || creature.isEnemy(owner)
> || DataManager.TRIBE_RELATIONS_DATA.isHostileRelation(owner.getTribe(), creature.getTribe()))`
> — `AggroList.java:198`

So this is **not a porting gap**. It is aionemu behaving as written, and widening it would be diverging
from Java in exactly the infrastructure the golden rule protects — on a path every npc pair in the game
runs through.

### What that means for the encounter, and it is bigger than the pattern

On this server a fortress killer cannot take hate on a garrison chief **at all**, with or without an AI
class. The pattern is not what is missing; **the whole fortress-raid behaviour is absent one level
lower**, and none of the six guard-boss npcs is placed either. Translating `LDF5_Fortress_Killer` today
buys nothing a player could see.

**Reverted a second time**, and this time the question is closed rather than open: it is a decision
about `IsAware` and the fortress spawn data, not an investigation.

### The rule this cost twice

**A class is not shippable because it is a correct translation.** Both reverts were of correct code —
the first for a wrong prop, the second for a gate that is right to be there. The check that would have
caught both before the class was written is one question: *can these two npcs put hate on each other at
all?* One `IsHostileRelation` lookup, before the table, not after the pins.

### Kept

`When.TargetRace` and `Do.HateTarget` — correct, general, and used by patterns beyond this one.

### Verification

Full suite **2,096 passing** and 1 skipped, unchanged. No behaviour shipped.

## Withdrawing the fortress-killer conclusion: `IsEnemy` reads aggro after all

The previous entry closed the fortress killers as "settled" — the translation right, the gate Java's,
the aggro relation not on `AddHate`'s path. **The last of those is false**, and the entry should not be
relied on.

`AggroList.IsAware` has three terms, and that entry read only the third:

```
aggroList.contains(creature) || creature.IsEnemy(owner) || IsHostileRelation(owner, creature)
```

`Npc.IsEnemyFrom` is:

```csharp
TribeRelationService.IsAggressive(creature, this) || TribeRelationService.IsHostile(creature, this)
```

**The aggro relation is on the path — through the second term.** `LDF5_V_KILLER_D` and
`LDF5_V_CHIEF_L` relate by `<aggro>`, so `IsEnemy` should be true and `IsAware` should pass. The
conclusion that "a fortress killer cannot take hate on a garrison chief at all" is withdrawn.

### What the zero probably was, and why that is not a new conclusion

The likeliest explanation is the one this log has already been caught by **three times**: the pins put
their reading after a setup that had already spent a once-per-fight branch. `OnEnterAttack` fires on
`EnterCombat()`'s latch, and `MakeMutuallyKnown` engages the pair — the same shape as the village
killers' `on_attacked`, the Catacombs recursion, and the illusion's despawn.

**That is a hypothesis and is labelled one.** It is not being written up as settled, because writing up
the last one as settled is what produced this correction.

### The tool that was going to prevent this, and why it was deleted

An `audit_hate_reachable.py` was written this session to answer "can these two npcs put hate on each
other?" from the tribe table. It reported `NLIZARDMAN vs PC` as **refused** — monsters cannot hate
players — which is obviously wrong, and wrong for exactly the reason above: it modelled one of the
three terms.

Deleted rather than shipped. A tool that answers a question confidently and wrongly is worse than no
tool, and this one would have been consulted precisely when someone was least able to check it. The
`audit_generic_messages` entry made the same point about verdicts two entries ago and this is the case
it was warning about.

### The rule, corrected

The previous entry's rule — *ask whether these two npcs can put hate on each other, before writing the
table* — still stands. What is wrong is the method it recommended. **Reading one branch of a
three-branch condition and reporting the result as the condition is how both the false conclusion and
the false tool happened.** The check has to run the real code path, not a paraphrase of part of it.

### State

The fortress killers are **unbuilt and open**, not blocked. The next attempt should:

1. take a baseline *before* `MakeMutuallyKnown`, per the once-per-fight lesson;
2. if hate still does not land, instrument `IsAware` rather than reasoning about it — the flake entry's
   rule about printing the whole failure applies to conditions as much as to exceptions.

### Verification

Full suite **2,096 passing** and 1 skipped, unchanged. Nothing shipped; one tool deleted and one
published conclusion withdrawn.

## Stopping on the fortress killers, after three turns and three reverts

The mechanism works. A probe calling the real code path, in the real harness, with the real npcs:

```
ai=FortressKillerAI  targetAfterKnown=2  chief=2  hate=100000000
```

`IsAware` passes, `IsEnemy` is true, the class binds, the target survives the introduction, and a
hundred million lands. **The previous entry's correction was right and this entry confirms it by
measurement rather than by reading.**

And the pins still fail. `TheGuardBossesCallNamesTheSender` reads **200,000,000** against an expected
hundred million — the branch fires twice, once from the engagement that `MakeMutuallyKnown` causes and
once from the message — while `AKillerHoldingAGarrisonChiefCannotBeMovedOffIt` reads *less* than a
hundred million in the same setup the probe read exactly a hundred million in.

Those two facts do not fit together, and I have not found what separates them.

### Stopping, and why that is the right call

This encounter has now taken three turns and three reverts of code that is, as far as every direct
measurement shows, correct. Each turn produced a confident explanation and each was wrong:

1. the aggro list refuses these tribes — **wrong**, `IsEnemy` reads aggro;
2. the props were Beluslan guards — **true but not sufficient**;
3. the target was unset when the branch fired — **not sufficient either**.

**A third wrong explanation is a signal to stop explaining.** Continuing costs more than the encounter
is worth: two npcs, in an instance whose six guard bosses our data does not place, whose `25307`
senders therefore never speak.

### What is genuinely known, for whoever picks it up

* The mechanic, in full, is in the previous two entries and is not in doubt.
* `FortressKillerAI` binds, and `Do.HateTarget` / `Do.HateMessageSender` both land a hundred million on
  a `LDF5_V_CHIEF_*` npc — probe output above.
* The disagreement is between the probe and the pins, **not** between the class and the engine. Start
  by diffing those two setups line by line rather than by re-reading `IsAware`.
* `on_enter_attack_state` fires on the `EnterCombat` latch, and `MakeMutuallyKnown` trips it. Any pin on
  this class has to account for a branch that has already run before the test's first line of intent.

### The rule

**When three explanations in a row have been wrong, the next thing to write down is not a fourth
explanation.** The flake took four wrong hypotheses before an experiment settled it; that experiment
was cheap and was available from the start. Here the experiment was run — the probe — and it *disagrees
with the tests*, which is a different and harder situation than a bug. Recognising that and stopping is
cheaper than a fourth guess.

### Verification

Full suite **2,096 passing** and 1 skipped, unchanged. Nothing shipped; the class reverted a third time.

## The lich soul call, and a default that was wrong for it

Second row off the `on_spelled` seam, and it reuses last entry's listener idiom exactly: **below half
health, once, a lich puts a faithful servant at its feet and tells it who to go for** — spawn and call
in one branch, at ten metres, naming whoever the lich is holding. Fourteen npcs carry the pattern and
four are live on our server.

**The same shape as the stoneskin stoffu, without the delay.** The stoffu arms a three-second timer and
calls when it runs out, which is a window a group can act in; the lich calls immediately, which is not.
One idiom, two fights, and retail expresses the whole difference by moving one action between two
branches. Both use message `2006`, and a pin now asserts the two constants are the same number so a
change to either moves both visibly.

### A default that was right until it wasn't

`PatternAi.Broadcast` excludes whatever the current branch has already spawned. That exists for RM-56c,
which lays traps and immediately tells traps to leave, and the exclusion's own remarks call it what it
is. **Spawn-then-point is the counter-example**: the lich's servant landed and stood there, which is
what the first run of these pins measured.

`Do.Broadcast(..., includeOwnSpawns: true)` is an **opt-in** rather than a flip. The exclusion is right
for every pattern already relying on it, and a table that needs the other behaviour can say so in one
word. A mutation flipping it back is caught.

**Rule: a heuristic named after the case that motivated it will meet its counter-example.** This one
survived a dozen encounters before the lich, and the fix is a parameter rather than a rewrite because
both behaviours are real.

### Numbers read rather than assumed, twice more

The servant arrives holding **100** — not the corask clodworms' **101**. Those come through
`AttackAfterSpawn` and gain one when they start swinging; a servant is pointed by a message and takes
retail's `points_to_add` exactly. Both are pinned, and the comment on each says why it differs from the
other.

### Not translated

The lich's self-cast, the servant's skill on engaging, and `percent_to_add` — retail's `switch_target`
carries *a hundred points and a hundred percent*, and this port has no equivalent for the percentage.
Recorded rather than approximated: on a fresh servant with an empty aggro list a percentage of nothing
is nothing, so the two agree today and would diverge for a servant that had already been fighting.

### Verification

Full suite **2,102 passing** and 1 skipped; seven pins; **eight mutations, all caught** — after one that
reported "not found" three times running turned out to be a broken anchor rather than a survivor, which
is worth naming: **a mutation that cannot be applied is not evidence of anything.**

## The lobseks, and the "sheds a piece when hurt" idiom seen three times

Third row off the `on_spelled` seam, and the smallest: **below half health, once, a coastal or sea
lobsek drops a strange object beside it, and the object lasts a minute.**

That is the same idiom as the stoneskin stoffu and the lich soul call, and having three of them makes
the shape legible:

| | shed | lifetime | pointed at anyone? |
|---|---|---|---|
| stoneskin stoffu | angolem fragment | 6 minutes | yes, after a 3-second timer |
| lich | faithful servant | 50 minutes | yes, immediately |
| lobsek | strange object | **1 minute** | no |

**A lobsek's object outlives the fight only if the fight is quick.** No call, no hate, no timer — it is
a nuisance with a clock rather than an add, and the sixty seconds is what says so.

**Both provocations, one flag, and the melee branch has no `is_enemy` guard while the caster branch
does.** Three encounters now carry that asymmetry unchanged, which makes it the idiom rather than an
accident of one pattern — worth expecting rather than re-deriving next time.

### A branch retail does not have, and the pin that says so

The stoffu clears its group on leaving the fight; **the lobsek does not**. Retail gives it
`on_killed_by_user` and `on_killed_by_npc` and nothing else, so a lobsek that disengages leaves its
object standing for the rest of its minute. Translated as written and **pinned**, so a later tidy-up has
to argue with retail rather than with nothing.

### Not built, from the same seam

Three neighbours on the `on_spelled` list reduce to nothing once their broadcasts are followed:

* **`Naga_SubEle`** (bloodlock's minions) — its `3313` has **no listener anywhere in the dump**, and its
  two `on_message` branches are skill-only.
* **`Naga_Servant`** — `3306`, likewise no listener.
* **`ND2_Bst_38`** and **`Lizardman_BeastB`** have listeners for `6511` and `3297`, and are worth a look
  when someone next works this seam; they were skipped only because the lobsek was complete.

### Verification

Full suite **2,108 passing** and 1 skipped; six pins; **six mutations, all caught**.

## The marked drakes and the drakies that run from everybody

Fourth row off the `on_spelled` seam, and the first where the mechanic is a **pair**: neither half is
anything on its own.

* The **drake** is an ordinary monster until half health, when it calls once at twelve metres naming
  whoever it is fighting.
* The **drakies** around it run from any player they see, for three seconds, while idle or walking a
  route — and answer that call by committing to the player it names.

**The call is what turns a field of fleeing hatchlings into a fight.** A player who pulls a drake
without noticing the drakies finds that out at half health, and until now the drakies on this server
neither fled nor came.

Eight npcs across three live pairs: longhorn, naduka and blackhorn.

### `Do.Flee` was a no-op for exactly the creature flee exists for

`Flee` reads `CurrentTarget`. A drakie that has never fought does not have one, so the target-based
flee did nothing at all for a skittish npc — the case the action exists to serve. Retail's is
`flee_from from=OBJI_SEEN`, and `Do.FleeFromSeen` is that: run from what came into view, not from what
you are fighting.

**Found by writing the pin, which is the argument for writing it.** The class looked right and read
right; the first run of the flee pin is what showed the action underneath it was inert.

### And the pin itself could not be kept

The flee half is **built and unpinned**. `Flee` computes a destination and hands it to the move
controller, and this harness advances a virtual clock without simulating movement — so the drakie's
position is unchanged however long the clock runs, whether the branch fired or not. A pin asserting it
moved would fail for correct code; one asserting it had not would pass for broken code. **Neither
direction is a test**, so it is a skip carrying the reason.

That is the second unpinnable-by-construction half this log has recorded, after Queen Serusia's
`IsDead` guard, and both were reported rather than covered.

### The fourth sighting of the same asymmetry

Both provocations share one flag, the melee branch has no `is_enemy` guard and the caster branch does.
Four encounters in four entries now — stoffu, lich, lobsek, drake. It is the idiom, and worth expecting
rather than re-deriving.

### Verification

Full suite **2,113 passing** and 2 skipped; five pins and one documented skip; **seven mutations, all
caught**.

## The trained beasts, and the first call that names two different people

Fifth row off the `on_spelled` seam, and the last of the four this seam's survey named. **At a quarter
health a lizardman's trained beast calls its breeder, once, at ten metres** — a trained animal that is
losing shouts for whoever trained it, and the breeder comes with one hate point on the player it names.

Five beasts and two breeders, all live: trained monitors, trained tipolids, and the anuhart sergeant's
mount.

### The two branches name two different people

**The melee branch names `OBJI_ATTACKER` and the spell branch names `OBJI_CASTER`.** For a beast being
focused by a melee player and a caster at once those are two different players, and whichever landed
the blow that took it under a quarter is the one the breeder is sent after. A single "name my target"
would have picked whoever the beast happened to be holding — and that is what every previous call in
this log did, because until now no pattern needed the distinction.

New vocabulary: `Do.BroadcastAboutAttacker` and `Do.BroadcastAboutCaster`, alongside the existing
self/target forms. A mutation swapping the caster form for the target form is caught.

### A guard that reads oddly and is kept

The spell branch's `is_enemy` tests **`OBJI_CUR_TARGET`**, not the caster it is about to name — so a
beast whose current target is friendly does not call, however hostile the caster. That is retail's own
wording, translated as written; `When.TargetIsEnemy` exists for it.

The melee branch has no such guard at all, which is the **fifth encounter in five entries** to carry
that asymmetry: stoffu, lich, lobsek, drake, beast. Melee unguarded, caster guarded, every time.

### One point, again

The breeder answers with a single hate point — the vasharti watch's glance rather than the klaw nest's
claim. Retail follows it with `switch_target OBJI_CUR_TARGET` carrying a hundred, which on a breeder
whose only hate point is the one just added means switching to the player it was named: the same one.
Translated as the single point it amounts to rather than as two actions whose second re-selects the
first's result, and pinned by a test that puts the breeder in a fight of its own first.

### Not translated

The shouts on both calls; the `3201` branch, whose senders are `DrGuard_*_Reward` patterns our data
never places; and the breeders' `3298` branch, a second call with **no sender anywhere in the 5.8
files**.

### Verification

Full suite **2,120 passing** and 2 skipped; seven pins; **seven mutations, all caught**.

## Thirty-seven npcs I left behind

Re-running the `on_spelled` survey to pick the next row turned up `ND2_Callsoulst` still listing live
stock-AI npcs — a pattern I translated two entries ago. **The lich commit repointed four of fourteen
bound npcs and left three live ones doing nothing**, because the ids came from the first page of a
survey's output rather than from the pattern's membership.

Checking the rest of the fortnight's work found the same shape in **seven classes and thirty-seven
npcs**:

| pattern | class | missed |
|---|---|---|
| `ND2_PnC` | faithful servant | **9** — griffos, fungies, fungen |
| `Lizardman_BeastKA` | bakarma breeder | **11** — indratu tamers, petmasters, beastlords |
| `D2_FnG_D1` | angolem fragment | **10** — shardlings, mosbears, frightcorns |
| `ND2_Callsoulst` | lich soul call | 3 |
| `Lizardman_BeastB` | trained beast | 2 |
| `ND2_Bst_38` / `_41` | drake / drakie | 1 each |

All repointed. The suite is unchanged at 2,120 passing, which is the uncomfortable part: **nothing
measured the gap, and nothing would have.** The classes were correct, their pins passed, and each
encounter worked for the npc it was written against while its siblings stood silent.

### The mistake, and the rule

Every one of these came from reading a survey that prints `[(n, name) for n in live[:2]]` and treating
the two ids it showed as the population. The survey was doing its job — it ranks patterns, it does not
enumerate them.

**Rule: repoint by enumerating the pattern's members, not by copying ids out of a survey's output.**
One `grep -P "\tPATTERN\t" out/ai_binding.tsv` gives the whole membership; that is the list to filter
for liveness, and it takes a second longer than trusting the excerpt.

### The audit, and why its unfiltered output is not a backlog

`audit_missed_siblings.py` asks the question directly, and **`--classes` is the mode that matters**:
"did *my* recent work miss anything". Unfiltered it reports ninety-nine patterns, and most are not gaps
at all — `LMerchant` binds five hundred npcs of which one is a named quest-giver, `D2_FnA` is retail's
generic monster pattern with a thousand members. A pattern whose bespoke class was written *for that
pattern* is the actionable case, and only the person who wrote it can say which those are. The tool
says so in its output rather than leaving the number to be quoted.

That is the third audit in this log to need "this number is not a backlog" printed next to it, after
the mute adds and the aggro-relation counts. **A count that looks like work and is not will be treated
as work.**

### Verification

Full suite **2,120 passing** and 2 skipped, unchanged — as expected, and as noted above, that is the
point rather than a reassurance. The audit reports **0** for every class shipped this fortnight.

## The klaw pack, and the number that decides who comes

`ND2_CnD_BR3` came off the `on_spelled` survey as an unexamined row. Reading it turned up a
**broadcast number shared by four patterns**, which is the first family in this log where the mechanic
lives between patterns rather than inside one.

Retail's `2003` is sent by the klaws that fight and heard by the klaws that stand around them. What a
listener does with it is decided by two things and nothing else — which pattern it is on, and whether
it was busy:

| pattern | live | when hurt | when it hears `2003` |
|---|---|---|---|
| `ND2_CnD_BR1` — wardens, patrols, king klawtan, queen taran | **17** | below **half**, once, names its target at 20m | **1 point, busy or not** |
| `ND2_CnD_BR3` — sentinels, king klawtun, nanny nuk | **11** | at a **third**, once, buffs, names, **and flees** | 1 point, **only if idle** |
| `ND2_CnD_RE1` — gatherers, peons, spies, scouters | **26** | never calls; has no `on_attacked` at all | **1000 points if idle**, scatter if busy |

**Fifty-four npcs across Beluslan, Morheim, Brusthonin and Reshanta**, every one of them previously
`aggressive`.

### What the numbers mean when they are put together

**A thousand against one is the whole pack.** The klaws that answer hardest are the peons and the
gatherers — the ones with no attack pattern of their own. Pulling a warden to half health does not
bring the other wardens in any meaningful sense; they take a single point and carry on. It brings the
gatherers, at a dead run, from twenty metres.

**And the sentinel's flight is only sensible because of the call.** It is the one member of the family
that leaves, at a third health, for three seconds when it is hit and four when it is cast at — retail's
own asymmetry, kept. A sentinel breaking off mid-fight would otherwise be a reset; naming the player on
the way out means the camp it runs through picks the fight up, and the player chasing it is the one
they are already hating.

**The two state guards are opposite on purpose.** BR1's answer has no guard at all and BR3's requires
idle, so one cry into a camp already fighting pulls the wardens and leaves the sentinels. RE1 answers
either way but does something different each time — commit if idle, switch to a random one of its own
attackers if not. **A single cry therefore converges the camp's idle klaws and scatters its busy ones
in the same instant.** No one pattern says that; it is what the three do together.

### Not built, and why

- **`ND2_CnD_BR2`** — the fourth member, and **our data places none of its npcs**. Not translated.
- **RE1's `2004` pair.** Retail gives RE1 the same two branches again on a second message number.
  `2004`'s only sender anywhere in the 5.8 files is BR2's relay branch — which is itself gated on
  `is_skill_count_left`, and belongs to a pattern with no live npcs. The branches would be unreachable
  duplicates of the `2003` pair they sit beside. **Recorded rather than written**; if a BR2 klaw is ever
  placed, this is what needs restoring.
- **BR3's "already fighting" answer** is retail's `attack_most_hating`, which for an npc already
  attacking its most hated is a no-op. Deliberately absent rather than untranslated — the distinction
  matters, because the *absence* of that branch is what the idle guard's pin measures.
- **Skill indices**, as ever: the sentinel's self-buff, the callers' opening skill, every listener's
  answering skill, and `on_stop_to_flee` — a skill on whoever the sentinel stops in front of.
- **`points_to_add=100`** on RE1's switch, which `SwitchTarget` does not carry. The established
  translation since the Anuhart casters.

### Verification

Ten pins, and **a seven-mutation sweep in which every mutation was caught and none failed to apply** —
the idle guard, the thousand, the busy-escort branch, both thresholds, the twenty metres, and the
shared flag. The flee is built and unpinnable for the drakies' reason, which the skipped pin states.

Full suite **2,130 passing**, 3 skipped. `audit_missed_siblings.py --classes klaw_call,klaw_sentinel,klaw_escort`
reports **0**.

## A claim in this log was wrong, and it cost forty-one branches

The klaw commit ended with a question worth asking again: which other listeners are waiting on a call
nobody makes? `audit_message_senders.py` answers it, and with the klaws repointed the largest group
left was the **black claw lycans of Morheim and the taygas they keep** — `Lycan_HeA`, `Lycan_HnA`,
`Lycan_HeB` and `D2_FnM`, twelve live npcs including Jahama the Ruthless.

Reading them turned up something more useful than the encounter.

### The correction

`FriendDeathNotice` was shipped with this in its remarks:

> The fallen NPC is not the message parameter — retail's handler takes no object and **its branches
> never name one**.

**That is false.** `OBJI_KILLER` appears in **41 of the 129** `on_see_friend_killed_by_user` handlers
in the 5.8 files, and **15 of the 67** `on_sense_` ones. A third of every branch on the largest event
retail has and aionemu does not, unreachable because of a sentence.

It was not a guess written under pressure — it was written to justify *not* plumbing the killer
through, and the justification made the check feel unnecessary. **A claim that makes work go away is
the one to verify first.** The count above took one query.

The killer now reaches the watcher: `PatternAi.FriendsKiller`, set by `FriendDeathNotice` immediately
before it raises the event and cleared in a `finally` after the branches run, exactly as `LastAttacker`
and `LastCaster` are. `Do.HateFriendsKiller` is the first user.

### The bug the correction immediately caused

`HateFriendsKiller` was written by copying `HateAttacker`, which adds hate **and turns to face**.
Retail's action is a bare `add_hate_point`, and the branches that use it follow with
`switch_target target=OBJI_CUR_TARGET points_to_add=100`. Turning first hands that second action the
killer instead of whoever the npc was already facing, and both payloads land on one player.

The consequence in play: **killing one tayga would have pulled the next one off the person tanking
it**, which is the opposite of what retail wrote. Caught by a pin, not by reading.

### Two pins that passed for broken code

The first version of the friend-killed pins engaged the lycan before the kill — which put a hundred and
one points on the raider through the `2301` call, so the killer was *already* the watcher's most-hated
and the assertions held whether or not `OBJI_KILLER` reached it at all. **Two mutations survived.** The
fix is to leave the watcher out of the fight, so the only thing that can have put hate on the killer is
the branch under test.

Same shape as the once-only guard and the survey excerpt: **a setup that pre-satisfies the assertion is
not a weaker pin, it is not a pin.**

### And a tribe question, left open rather than guessed

The notice reaches a watcher only where `TribeRelationService.IsFriend` says so — and `LYCAN_PET` and
`LYCAN_HUNTER` are related by **`support`**, not by `friend`. **A tayga does not hear its own tamer
fall.** Taygas share a tribe with each other, so the branch is reachable and pinned; whether retail's
"friend" means the wider word is not something this pattern can settle.

Broadening `IsFriend` to `IsFriend || IsSupport` would change every consumer of the event on evidence
nobody has, so it is **not** done here. **What would settle it:** a pattern carrying a friend-killed
handler whose npcs have *no* same-tribe companion placed anywhere near them — there the branch is dead
under the narrow reading and alive under the wide one, and retail would not have written it dead.

### The encounter itself

| pattern | live | what it does |
|---|---|---|
| `Lycan_HeA`, `Lycan_HnA` | 4 | on engaging, names its target at fifteen metres |
| `Lycan_HeB` | 4 | the same, and **runs from whoever killed its tayga** |
| `D2_FnM` | 4 | answers the call with **101**; answers a friend's death with a point on the killer and a hundred on its own target; **names its killer as it dies** |

**The hundred and one is retail's**, not a rounding: `add_hate_point` of one followed by
`switch_target` carrying a hundred, both landing. And the friend-killed branch's two payloads split or
merge depending on whether the tayga was busy — an idle one has no target until the killer's point
gives it one, so both land on the killer; a fighting one pays the killer a point and its tank the
hundred. That asymmetry falls straight out of writing the actions in retail's order.

### Not built

- **`2302` and `2304`** — listened for by `D2_FnM` and both lycan patterns, and **broadcast by nothing
  in either the 2.7 or the 5.8 files**. Dead wire in NCSoft's own data, checked in both dumps.
- **`2303`** — the reverse: broadcast by `Lycan_HeA` and `Lycan_HeB` on a six-second timer below half
  health, and **listened for by nothing**. The reason that timer is absent rather than built.
- **`2305`/`2306`** — a tamer's cleanse when its tayga is crowd-controlled and its heal when one drops
  below half. Both skill indices, the heal also gated on `is_skill_count_left`. **These are the answers
  a tamer is supposed to give**, and what is left without them is the one it gives when there is
  nothing left to save.
- **`on_enter_abnormal_state`** — no handler in this port. It is what sends `2305`.
- **`1019`** — sent by `Lycan_HeB` when it stops fleeing, to the `Lycan` pattern, **none of whose npcs
  our data places**.
- `Lycan_HeA` and `Lycan_HnA` collapse to one class because every difference between them is a skill
  index. Recorded so they can be split when that lifts.

### And a tool fix, from the same root as the last one

`summarize_pattern.py` dropped `seconds`, so **every `flee_from` in every summary printed as a bare
`from=`**. The klaw sentinels flee for three seconds when hit and four when cast at; the tamers for
three. None of that was visible until the raw XML was read by hand — and the klaw entry's "retail's own
asymmetry, kept" was written from a hand read that the tool should have supplied. Third time the KEEP
list has hidden the content of an action, after `race_type` and `point_to_add`.

### Verification

Nine pins, and an **eight-mutation sweep with one survivor**: clearing `FriendsKiller` in the `finally`
cannot be falsified, because every path that raises the event also sets the field. Reported rather than
covered, as the rule says. Full suite **2,139 passing**, 4 skipped.

## The experiment I wrote down, run — and what it actually found

The last entry left the friend-killed tribe question open and wrote down the experiment that would
settle it:

> A pattern carrying a friend-killed handler whose npcs have **no** friend-reachable companion placed
> anywhere near them is dead under the narrow reading and alive under the wide one — and retail would
> not have written it dead.

`audit_friend_reach.py` runs it, over every live npc on a friend-killed pattern, using the spawn
coordinates and each npc's own `srange` — the same eye `FriendDeathNotice` uses.

```
     29  friend-reachable
     28  support-only
    185  alone
```

**It does not settle it.** The support-only group — the deciding cases, where widening `IsFriend` would
bring a dead branch to life — is 28 npcs, about one in nine. Real, but nothing like the landslide the
experiment was designed to detect.

### What it found instead

**Three quarters of these npcs have no companion of any relation inside their own sight.** The median
nearest one is **twenty metres** away against a median `srange` of **eight**; among those out of range
the median is thirty. So the question the experiment was asking — narrow `friend` or wide? — is
answered "neither" for most of the population: the branch is unreachable from static placement either
way.

That reframes the handler rather than the tribe table. **`FriendDeathNotice` is, by its ranging, an
event for grouped and instance-spawned adds** — which is exactly where it already works and where it
was first built, for Commander Bakarma's legionaries. A field mob carrying the handler because its
pattern is shared with instance content is not a gap.

**The srange choice is therefore not disproved, but it is now measured**, which it was not when it was
argued for. The argument was "the range belongs to the eye, not the corpse"; the number that argument
implies is that three in four placed npcs never hear anything, and that number is now written down
where the next person can weigh it.

### The variant hint, and why it is only a hint

Retail has two handlers and this port collapses them into one event: `on_see_friend_killed_by_user`
(129 patterns) and `on_sense_friend_killed_by_user` (67, with 35 carrying both). If *sense* reached
further than sight, sense patterns' npcs should be placed further apart. **They are — 27m against 15m
median — but the share within sight barely moves, 20% against 27%.**

The two pattern sets cover different content, so the placement difference may be entirely about what
kind of npc uses which. **Recorded as a hint, not a result.** What would make it a result: a pattern
carrying *both* handlers whose branches differ in what they assume about range. There are 35 such
patterns and none has been read.

### The decision

**`IsFriend` is not widened.** Twenty-eight npcs is not enough to change an event's audience for every
consumer, and the population those 28 sit in says the question is smaller than it looked. The taygas
stay as they are: a tayga hears another tayga fall and not its own tamer, which may well be wrong and
is at least wrong in a way that is now written down with its numbers.

**What I will not do again:** write down an experiment and count the writing as the work. The
experiment cost one script and half an hour, and it changed the shape of the question — the previous
entry's confident framing of what would settle it was itself a guess.

## Guardian Vingeveu, and a boss whose skills are all blocked and is worth building anyway

`audit_translatable.py` ranks `ND2_KeB` at nine translatable actions with **skill** as its only blocker
— eight of them. On the face of it that is a boss with nothing left. It is not, and the reason is worth
recording: **what the skills are is blocked; when they happen is not**, and when they happen is the
part a raid reads.

### The shape

One heartbeat timer, and a ladder of health guards hanging off it.

| band | opener fires once on | re-arms heartbeat | arms | announces |
|---|---|---|---|---|
| 71–100 | `ALPHA_1` | 7s | its own timer at 25s | **`6193`** — help me |
| 36–70 | `ALPHA_2` | 5s | a handover timer at 15s | **`6194`** — and scatters |
| below 35 | `ALPHA_3` | 5s | its own timer at 15s | **`6194`** — and scatters |
| — | (fallback) | 6s | — | — |

Timer zero runs all fight. Each band's opener is a branch on that *same* timer carrying its own flag,
so entering a band announces it once and never again however long the raid stays there. The bottom
branch — timer zero, no guard — is the six-second heartbeat that keeps the ladder turning.

**What separates the bands is which call they open with, not the pace.** The first band asks for help
and does not move him. The second and third open on `6194`, which carries a scatter — for him and for
every servant inside fifty metres, in the same instant. So the fight changes character twice, at
seventy and at thirty-five, and both times the whole room re-picks its targets.

His servant (`ND2_Ksum1`, vinsev's servant) has nothing but the two answers: **one point** on `6193`,
and **ten points, a self-buff and a scatter** on `6194`. Ten and then throwing it away reads oddly on
the page and obviously in the room — the ten is the boss saying who matters and the scatter is the
servant losing its head anyway.

### Two things kept because retail wrote them

**Health of exactly thirty-five belongs to no band.** The third guard is `is_hp_lower_than 35` and the
second is `is_hp_in_boundary larger_than=36`. At that one integer neither passes and only the heartbeat
runs. Kept: health does not linger on an integer, and closing the hole would mean inventing a number.
Pinned, so nobody closes it later by accident.

**A band change is worth eleven, not ten** — the loud call, and then the quiet one fifteen seconds later
when the timer the opener armed comes round. The first version of the crossing pin read that as an
off-by-one and it is not.

### The pin that had to be reconciled rather than fixed

Two pins disagreed about the same branch: one saw a band open for ten, the other for eleven. Neither
was wrong. In the first the fight *starts* inside the band, so the opener lands on the engage branch's
fifteen-second heartbeat and the timer it arms falls outside the window; in the second the heartbeat has
already been sped up by the band above, so the opener lands earlier and its timer lands inside.

**The instinct was to pick a number and make both pins use it.** That would have been a pin asserting
an arbitrary window. Both are now written with the window they measure and a note saying why they
differ — the difference is the fight's, not the harness's.

### Not built

- **Eight skills**, which is every `use_skill` in the pattern, on the boss and on the servant.
- The **scatter** is built and not pinned as an outcome: with one raider on the list a random pick is
  that raider, and with several the pin asserts a coin flip. What the pins turn on instead is the
  payload riding with it — ten against one. Stated in the skipped pin rather than left to be found.
- `OBJI_EVENT_TARGET` on the engage branch is translated as the current target, which on entering
  combat is the same creature.

### Verification

Eight pins and an **eight-mutation sweep, all caught, none unapplied** — shared band flags, a flagless
opener, the first band opening loudly, the thirty-five hole closed, the reach, the heartbeat's re-arm,
the engage call, and a servant that cannot tell the two calls apart. Full suite **2,148 passing**, 5
skipped.

## Chaoslord Kalabar, and the ceiling on what these dumps can give us

`NKrall_WhA` — Chaoslord Kalabar of Eltnen and Visionmaster Omutata of Morheim. **He builds one thing
at ninety, trades it for a different thing at sixty, and detonates that at thirty-five.** Three bands,
one add each, and the add *is* the band.

| band | opens once on | what happens |
|---|---|---|
| 61–90 | `DELTA_3` | spawns a **wheel of death**, scatters |
| 36–60 | `DELTA_2` | spawns a **stone guard** and **despawns the wheel in the same branch**, scatters |
| below 35 | `DELTA_1` | calls `3008` — and the stone guard answers with `despawn_self` |

**The wheel and the guard are never both up**, because retail writes the spawn and the previous
group's despawn as two actions of one branch. A raid that leaves the wheel alone finds it gone; one
that killed it early has changed nothing. And the guard exists to be spent: its only branch worth
anything is the one that ends it, so a raid that kills the guard and a raid that ignores it reach the
same board at thirty-five.

**Both groups are cleared when he dies and when he leaves the fight**, written as its own two handlers
rather than left to `despawn_at_attack_state` — so the adds cannot be pulled away and kept. That is
unusually explicit for a field boss and it is pinned.

Two gaps kept because they are retail's: **above ninety there is no band at all**, and **health of
exactly thirty-five belongs to no band** — the same off-by-one Guardian Vingeveu carries, one entry
earlier. Twice in two bosses is no longer an oddity; it is how NCSoft writes a three-band ladder.

**One dead action, not built:** `on_enter_idle_state` sets `FLAGVARI_ZETA_5`, which no branch in the
pattern reads.

### The wheel's own pattern does not exist

The spawn action names `BLF2_NM2_RollingWheel_40_An`, a devname — the client resolves it to **280357,
wheel of death**, whose `ai_name` is **`ND2_RnJ`**. That pattern is in **neither the 2.7 nor the 5.8
dump.** Not mis-grepped, not renamed: absent from both.

So the wheel spawns on schedule and behaves as an ordinary monster, and no amount of reading will fix
that. **This is the first gap in this log that is in the source rather than in the port**, and the
obvious question was how often it happens.

### `audit_missing_patterns.py`, and the number nobody had

```
client npcs naming a pattern: 63,244; present in a dump: 49,134 (77.7%)
1,571 pattern-shaped names the client uses that neither dump carries;
3,858 npcs behind them, of which 760 are placed here.
```

**Roughly one client AI reference in five points at behavior no dump we have describes.** That is a
ceiling on what this port can ever reach from these files, and it was not written down anywhere.

**The head of the raw list is not the interesting part**, which is why the tool filters it: `NPC`,
`NoAction`, `Resurrect`, `ReturnToEntrance`, `FOBJ_NormalDrop` are the client's built-in AI types, and
an npc on `NoAction` is not missing a pattern — it has none. Counting those would have turned 760 into
5,021 and made the gap look four times worse than it is. **The fourth audit in this log to need a
"this number is not what it looks like" filter**, after the mute adds, the aggro relations, and the
missed siblings.

What is left — `D2_RnB` at sixteen live npcs, `D2_AnF` at fifteen, `Brownie_FnA`, `Ratman_FnA` — is the
honest list. **Nothing on it is actionable**; the point is to answer "is this really absent, or did I
mis-grep?" in one command, and to stop a future reader concluding that a silent npc is a porting
oversight when it is a missing file.

### Verification

Eight pins and a **nine-mutation sweep, all caught, none unapplied**: the high band's upper bound, the
guard failing to replace the wheel, shared band flags, a flagless opener, the call, a guard that
ignores it, the thirty-five hole, the death handler, and the wrong add id. Full suite **2,156
passing**, 5 skipped.

### And a flake caught in the same run, whose arithmetic was always visible

The full-suite run for this commit failed once on `SilikorOfMemoryAiTests.TheCasterGuardDropsSummonsOnAttackers`,
in a file this commit does not touch. Twenty repeats of that pin alone were clean, which is exactly the
evidence that would have got it filed as unexplained.

**It is a one-in-four roll asserted over twenty windows.** `0.75^20` is about **one run in three
hundred** — invisible in a single run and inevitable across a suite that is run all day. Raised to
sixty windows: `0.75^60`, one in a hundred million.

**The sibling pin two hundred lines above needed no change**, and the same arithmetic says why: a coin
flip over twenty rolls fails at `2 × 0.5^20`, one in five hundred thousand. **Window length is not the
thing to standardise — the per-window odds are**, and a probabilistic pin should carry the exponent it
is betting on. This one now does.

## Masto the Ancient, and pins built on coin flips

`ND2_EhA` ranks eight translatable actions against **thirty-one blocked skills** — the worst ratio of
anything shipped here. It is worth building anyway, for the reason Guardian Vingeveu was: *what* the
skills are is blocked, *when* they happen is not, and here the "when" is not even the point. **What
changes across the bands is how often he throws his target away.**

| band | opener (once) | repeat | scatters? |
|---|---|---|---|
| 81–100 | — none — | skill on 15s | **no** — a tank holds him |
| 61–80 | `ALPHA_2` | 25s | yes, both |
| 41–60 | `ALPHA_3` | 30s | yes, both |
| 21–40 | `ALPHA_4` | 25s | yes, both |
| below 20 | `ALPHA_5` → **second-most-hated** | 30s, **no switch** | opener only |

So: **held at the top, unholdable through the middle, and at the bottom he picks the off-tank once and
stays there.** Five bands and a single mechanic.

**Health of exactly twenty belongs to no band.** Third boss in three entries — after Guardian Vingeveu
at thirty-five and Chaoslord Kalabar at thirty-five. It is not a slip in one pattern; **it is how
NCSoft writes a banded ladder**, and it is kept every time.

**One dead action, not built:** `on_enter_idle_state` sets `FLAGVARI_ZETA_5`, which no branch reads —
the same dead flag in the same slot as Kalabar's.

### The pins were wrong first, in a way worth writing down

**`SwitchTarget(RANDOM)` can land on the player it already had.** So "the target changed" is a
one-in-three coin, not an observation. The first draft asserted it three times and **failed three
different ways on the first run** — which is the good outcome; the same file with two players instead of
three would have failed one run in eight and been called a flake.

Every pin was rebuilt on something that is not a coin:

- **an absence** — the top band has no switching branch, so *any* switch over two minutes is a failure;
- **a determinism** — the bottom band's `SECOND_HATING` pick is the same player every time, so twelve
  identical fights are twelve real observations;
- **a stated exponent** — where a count was unavoidable, the window is long enough that the odds
  against are arithmetic rather than hope.

That last one is the rule from the previous entry's flake, applied before the flake rather than after.

### Three mutations survived, and one of them corrected the class doc

The sweep caught five of eight. The three survivors were each worth more than the five:

**A claim I had written up as the mechanism turned out to be inert.** The bottom band's opener is the
only one that does not re-arm the opener timer, and I described that as what stops the scattering. It is
not — that timer's fallback branch has no switch either, so whether it keeps running or dies, nothing
moves him. **The band's own flag is what ends it.** A mutation restoring the re-arm changes no pin, and
that is correct rather than a gap. The class doc now says so; it used to say the opposite, in bold.

**A shared flag between two middle bands was invisible.** Those bands' openers only scatter at random,
so the target tells you nothing. The failure is visible one step further out: a band that never opens
never arms its own repeat timer either, so the scattering stops. The band-crossing pin now measures
that, and the target only for the bottom band. **One pin, two halves, because the middle and the bottom
fail differently.**

**The opening scatter was unpinned** precisely because it is a coin flip — so it is pinned as one:
twelve fights at a health with no band to claim him, and landing on the tank all twelve times is one in
half a million.

### Also settled, cheaply: the 2.7 dump adds nothing

The wheel of death's missing pattern raised the question of whether the older dump carries anything the
newer one lost. **It carries three patterns 5.8 does not, and no npc our data places runs any of
them.** 2.7 has 3,030 patterns against 5.8's 12,798, and is a strict subset for every practical
purpose. Worth one query to stop anyone asking again.

### Verification

Nine pins. **Eight-mutation sweep, five caught on the first pass**; the three survivors produced a
corrected class doc, a new band-crossing pin and a new opening-scatter pin, and all three then failed
as they should. Full suite **2,165 passing**, 5 skipped.

## The fortress guards, and the number that carries both factions

`audit_message_senders.py` has been pointing at this all along and I had been reading past it: **`23200`
has twenty-two sender patterns and sixteen listeners.** It is the fortress guard call, and this server
had none of it — **137 live npcs**, across every 5.x fortress and both factions, standing next to each
other and never speaking.

| role | patterns | live |
|---|---|---|
| **callers** — ranged patrols and watchguards | `F5_PvP_DGuard_Ra_Ae_Broad`, `F5_PvPLight_DGuard_Ra_An_Broad` | **38** |
| **answerers** — knights and defenders | `F5_PvP_DGuard_Kn_Ae`, `F5_PvPLight_DGuard_Kn_An`, `F5_RvR_DGuard_Kn_Ae` | **99** |

**Pull one guard and every guard within twenty-five metres comes.** Idle, an answerer takes a single
point on the player named and goes; already fighting, it turns on that player and takes a hundred.
Without it a raid picks a fortress apart one guard at a time, which is what has been happening.

### The design worth stealing

**The answerer never checks who spoke.** Retail's guard is `is_enemy who=OBJI_MESSAGE_PARAM` — is the
*player named* my enemy — and that one condition is what lets a single message number serve both
factions. An Elyos guard standing in earshot of an Asmodian call hears it and does nothing, because the
player it names is not its enemy. **Written the obvious way, checking the sender, the family would have
needed a number per faction.**

### A blocker that turned out not to block

Every branch of the caller's `on_enter_attack_state` is split on `is_user_flying`, which is one of this
port's standing structural blockers. **Both halves broadcast the same message at the same range.** The
flying test picks which skill it opens with and nothing else, so the blocker does not touch the call at
all. Worth checking before writing off a pattern for a condition we cannot evaluate: **the guard may not
be guarding the part you want.**

### The mutation that could not fail, and why that was worth an experiment

Deleting `is_enemy` from the idle answer changed no hate at all. Not a weak pin — **this port's
`AggroList.AddHate` already drops hate aimed at a creature that is not an enemy**, so retail's condition
is enforced a second time one layer down. Measured rather than assumed: a probe adding fifty points to a
friendly and a hostile player read back **zero and fifty**.

**But the turn is not protected.** `HateMessageTarget` faces its target whether or not the hate landed,
so a guard with the condition deleted swings round to a friendly player and stands there. That is the
observable difference, and the pin now measures it.

Same shape as the village killers, where the aggro list's refusal was the tribe table catching a bug of
mine. **The rule is holding: when a pin cannot fail, ask what the engine is doing before rewriting the
pin.**

### Not built

- **`percent_to_add=10`** riding with the busy answer's hundred. No way to add a percentage of existing
  hate here — recorded, as for the faithful servants and the drakies.
- **`23201`**, "protect the sender" (retail's own comment, in Korean). Its three listeners disagree about
  whether to cast on the sender or on what the message named, and its only sender in the 5.8 files is
  `F5_PvPLight_DGuard_Fi_An`. A skill index either way.
- Every battle timer on both roles, and the opening skills the flying test chooses between.
- **`F5_AbyssTower_DGuard`** (6 live) also broadcasts `23200`, at ten metres rather than twenty-five, and
  its branch is guarded on `is_race from=OBJI_SELF race_type=pc_dark` — which for the Elyos "sacred image
  of marchutan" reads as a branch that can never fire. **Left alone rather than guessed at**; it wants
  its own reading.
- The `23204`–`23209` elemental-guard conversation, whose npcs our data does not place.

### Verification

Five pins and a **six-mutation sweep, all caught** once the enemy guard was pinned on the turn rather
than the hate. Full suite **2,170 passing**, 5 skipped.

## The audit that should have come five commits ago

The klaw pack, the black claw lycans, Guardian Vingeveu, Chaoslord Kalabar and the fortress guard call
were all found the same way: reading a survey, following a hunch, reading a pattern. **They are all the
same shape** — a retail message number whose senders *and* listeners are both still on stock AI, so the
call is never made and would not be heard if it were.

`audit_silent_conversations.py` ranks them. There are **135**.

```
     msg  call  ans   who
   10001   884  146   mist, jecasti, ricaldo
   41000    79  135   belani lookout, aspamon lookout, atasin lookout
    9001   208    2   vaelath, citadel fencer, lab warrior
   41100    60  120   belani slayer
    3302   118   42   navigator nevikah, assistant malakun, kind saraswati
    3201   132   13   bakarma lookout, bakarma scaleguard
   23100    33   47   stonereach garrison watchguard, upright sentinel
    ... and 128 more
```

**Five commits of hand-picking, and the thing that finds them is forty lines.** The audits I already had
asked adjacent questions — listeners waiting on a sender nobody has, adds that are never spawned — and
none of them asked about the conversations absent at *both* ends, which is where nearly all of the
remaining work turns out to be.

**The count is npcs, not importance**, and the tool says so in its own output: `10001` at a thousand
npcs is retail's generic chatter, while a named boss and his two adds sit near the bottom and are the
better hour's work. Fifth audit in this log to need that caveat printed next to its number.

### And the first thing it found was the other half of the last commit

**`23100` is `23200` with a different number.** Byte for byte the same mechanic — twenty-five metres,
the event target named, one point idle and a hundred while fighting, the same `is_enemy` on the player
named — for the Light-side guard family instead of the Dark. **Seventy-four more npcs**: 27 callers and
47 answerers.

I shipped the Dark side yesterday without noticing the Light side existed. The audit put it third from
the top of a list I could have had first.

### Two classes, not one, and the reason is not obvious

The tempting version is one answerer listening for both numbers. **That would be wrong, and the
`is_enemy` guard would not save it:** the two families stand in the same fortresses and both have
Elyos-side and Asmodian-side members, so a Light guard and a Dark guard on the same side have *the same
enemies*. A merged class would have them answering each other's calls, which is precisely what retail's
second number exists to prevent. Pinned: a Dark caller pulling a player the Light guard also counts as
an enemy leaves that guard standing.

### Not built

- **`23101`**, the Light-side "protect the sender", whose listeners include the Kamar generals and whose
  only sender is `F5_PvPLight_LGuard_Fi_An`. A skill index, as its Dark twin is.
- The same **`percent_to_add=10`** on the busy answer.
- **`F5_AbyssTower_LGuard`** (6 live, "sacred image of kaisinel") — the Light twin of the abyss tower
  guard left alone last commit, and left alone for the same reason: its branch is guarded on
  `is_race from=OBJI_SELF`, which appears never to pass for its own npcs. **Both twins now wait on one
  reading of that guard**, which is a better place to leave it than one.

### Verification

Eight pins on the family now, and a **three-mutation sweep on the new half, all caught**: the two
families sharing a number, the Light answerer listening on the Dark one, and a Light caller that never
calls. Full suite **2,173 passing**, 5 skipped.

## Panesterra's bases, 275 npcs, off the top of the new list

`audit_silent_conversations.py` was written last commit and the second and fourth rows on it were
`41000` and `41100` — Panesterra. **Fifteen retail patterns, two bases, 275 live npcs**, every one of
them `aggressive` and silent.

Each base runs the **same two-tier conversation on its own pair of numbers**:

| | ordinary guard's call | captain's call |
|---|---|---|
| Vritra side | `41000` | `41001` |
| the other | `41100` | `41101` |
| **answered with** | **10 hate** | **100 hate** |

**That ten-to-one is the whole tiering of a base.** Pulling a guard is a nuisance; pulling the captain
brings the room. And the captains have no answer branch of their own — **pulling the captain pulls the
base, pulling the base does not pull the captain.**

**What differs pattern by pattern is only how far a call carries** — thirteen metres for most and
twenty-five for the lookouts and patrols, which is what a lookout is posted for. Everything else — the
payloads, the actions, the absent state guards — is identical across all ten guard patterns, so the
family collapses into nine classes without losing a thing.

**And a warcaptain is heard by the rival bases.** Twelve cutthroats across Aspamon, Atasin and Disilgot
listen for `41101` and nothing else, which is Panesterra's design in one line: four factions in one
map, each with a standing interest in the others' captains.

### Two things retail wrote once

**`Gab1_LGuard_05` is the only pattern of the ten that checks whether the player named is its enemy.**
Nine others answer whoever is named. Kept — the sixth encounter in this log to carry that kind of
one-off asymmetry, and the sixth to keep it.

**`Gab1_Gaurd_Ra_An_Broad` is the one pattern in the family where `is_user_flying` changes something
real:** thirteen metres if the puller is airborne, twenty-five from the ground. This port cannot
evaluate the condition, so it takes **the ground branch — retail's own lower-priority fallback**, which
is the overwhelmingly common case. **A flying puller should get the shorter call and does not.**
Recorded rather than averaged.

That is the second `is_user_flying` reading in two commits and they came out opposite ways: on the
fortress guards both halves broadcast identically and the blocker was irrelevant, here it is the only
difference between the halves. **The condition is not a reason to skip a pattern and not a reason to
ignore it — it has to be read each time.**

### A pin that failed for the right reason

The slayers' `is_enemy` pin first used an Elyos raider against Elyos-race guards, and failed. Correctly:
**in Panesterra a player's race makes them nobody's friend.** The guards' tribes *are* the four base
factions, and both races are enemies of all of them until a player is assigned one — so there is no
friendly player here without `SetPanesterraFaction`. Belani's `GAB1_01_POINT_01` is `BELUS`, and with
that set the guard bites.

Worth keeping because it is a fact about this map that will trip the next person the same way: the
faction layer, not the race, is what the guards read.

### Not built

- **The warcaptain's death.** `on_killed_by_user` fans out six broadcasts behind `is_tribe` guards on
  the killer — `10101`, `20101`, `30101`, `40101`, `10103`, `4440444`, one per faction. **That is the
  base-capture announcement, not an AI mechanic**, and it belongs with the siege code. Listed here so
  nobody translates it into an aggro action.
- **`percent_to_add`** on every captain answer — eleven across the family and **ten on the slayers**,
  which is the sort of difference that exists only because a person typed it. Neither is translatable.
- The skills on every answer, and the battle timers on every guard.
- **`41200`/`41201`**, the third base's pair. The patterns exist; our data places none of their npcs.

### Verification

Nine pins and a **seven-mutation sweep, all caught**: the two payloads equalised, the lookout's range
shortened, the two bases sharing a number, the bosskillers listening to the wrong captain, the slayer's
`is_enemy`, a captain that never calls, and a soldier that never answers. Full suite **2,182 passing**,
5 skipped.

## The two largest handlers in the dump, and the first thing that uses them

`on_see_friend_attacked` appears in **397** patterns of the 5.8 files and `on_friend_spelled` in
**344**. The friend-*killed* handler this port already had is in 129. **These two are the biggest events
retail has and aionemu does not**, and nearly everything a camp does when one of its members is jumped
hangs off them.

`FriendCombatNotice` raises both. **Its audience is decided exactly as `FriendDeathNotice` decides
its** — the victim's own known list, each observer's own `srange` because the range belongs to the eye,
and `TribeRelationService.IsFriend` for what "friend" means. **Sharing those three decisions matters
more than the decisions do:** two notices with different audiences would be a bug nobody could see, and
there is now a pin on each rule in each notice.

### The one thing it does that the death notice does not

**It fires on every blow**, so it carries a re-entrancy guard. A watcher's answer is nearly always to
take hate on the attacker, that hate raises an attack event of its own, and without the guard a camp of
mutually-watching npcs would notify itself until the engine's recursion cut-off fired.

**That guard is reported rather than covered.** Removing it fails nothing, because these pins call
`Raise` directly and only the live damage path can recurse. Retail hides the same problem in its data —
its branches are nearly always flagged to fire once — which is not a reason to leave the engine
unguarded.

**Cost:** the damage path already walks the victim's known list on every hit to raise
`CreatureNeedsSupport`. This walks the same list beside it.

### And 126 npcs to use it

`3201` came third on the silent-conversations list: **forty-two patterns broadcast it and exactly one
listens** — `Lizardman_BeastA`, thirteen pet drakes across the Abyss camps. A hundred and thirty callers,
one answerer.

The callers sort into three shapes and nothing else:

| shape | patterns | live | what it does |
|---|---|---|---|
| `*_ABRwd*` reward officers | 33 | **78** | pulled → call at **30m** |
| plain `*_Reward` named officers | 12 | **5** | pulled → call at **50m** |
| bakarma lookouts and fangsnares | 3 | **30** | **a friend below three-quarters** → call at 13m |

**The lookouts are the interesting third.** They do not call when *they* are pulled — they call when
they see somebody else pulled, which is the whole of `on_see_friend_attacked` and the reason the event
had to exist first. The spell branch checks the caster is an enemy and the melee branch checks nothing:
**seventh encounter in this log to carry that asymmetry, seventh to keep it.**

The drakes answer with a point and then a hundred, so what lands is **101** — retail's usual order, the
same shape the tamed taygas use.

### Two pins that measured nothing, and what fixed them

**The sight-range pin put the victim eighty metres away.** That is outside the known list entirely, so
the notice never reached the range check and the pin passed with the check deleted. Twelve metres —
inside the known list, outside a lookout's eight-metre eyes — is the only gap that measures anything.

**The tribe pin did not exist.** Added: a lookout watching a *pet drake* take a beating says nothing,
because `NLIZARDMAN` and `NLIZARDPET` are related by `support` and not `friend`. That is the same
distinction the taygas exposed on the death notice, and this pin is now what keeps the two notices
answering it the same way.

### Not built

- **`3302`**, which ranked fifth on the silent-conversations list at 157 npcs and **is worth nothing**:
  the naga casters broadcast it naming *themselves*, and `Naga_KeA`'s answer is a single
  `use_skill` on the caller. Every action on both ends is a skill index. **Recorded because the audit
  will keep offering it** — a big number with nothing behind it.
- The ten remaining `3201` patterns that carry the number outside the handlers read here; one live npc
  between them.
- The lookouts' shouts, the drakes' own `3298` call at a quarter health, and every skill.

### Verification

Eight pins and a **nine-mutation sweep with one survivor** — the re-entrancy guard, for the reason
above. Full suite **2,190 passing**, 5 skipped, with the new per-hit event live in the damage path.

## The biggest handler left, measured and not built — and Panesterra's castles

With the friend-attacked events in, the obvious question was which handler blocks the most now. **Every
handler above `on_enter_abnormal_state` in the dump is already supported**; that one is not, and it is
in **272 patterns with 1,168 live stock-AI npcs behind it** — larger than the friend-attacked pair.

Reading it, the shape is almost uniform: **245 of its ~270 branches are a single `broadcast_message`**
guarded by `is_abnormal_state`. It is the "I have been crowd-controlled — somebody help" handler, and
the guards of every fortress carry it.

**It is not built, and the reason is the guard rather than the event.** The states the branches name
are:

| retail state | live npcs | maps to |
|---|---|---|
| `ABNSTATEI_MENTAL_GROUP` | **919** | *nothing* |
| `ABNSTATEI_CANNOT_ACT_GROUP` | **206** | *nothing* |
| `ABNSTATEI_PHYSICAL_GROUP` | 3 | *nothing* |
| `ROOT`, `POISON`, `BLEED`, `SANCTUARY`, `SLEEP`, `STUN_LIKE_GROUP` | 38 total | exactly |

**Those three group names are defined nowhere we can read.** They are not in the Java tree, and a scan
of every `.pak` in the client for `MENTAL_GROUP` returns nothing. Our `AbnormalState` enum has
composites of its own — `CANT_ATTACK_STATE`, `ANY_STUN` — but no member of it is retail's
`MENTAL_GROUP`, and picking a plausible union of bits would be **inventing a number in the one place a
guess is invisible**: nothing would fail, the guards would fire on roughly the right crowd control, and
nobody would ever find out it was wrong.

**1,128 of the 1,166 live npcs sit behind those three names.** Building the event for the other 38
would be scaffolding with almost no user, which this log has criticised before. So:

- **What is needed:** a definition of `ABNSTATEI_MENTAL_GROUP`, `ABNSTATEI_CANNOT_ACT_GROUP` and
  `ABNSTATEI_PHYSICAL_GROUP` as sets of concrete states. A retail client build with an unstripped
  string table, an NCSoft tools dump, or a private-server writeup that lists them would all do.
- **What is already known:** the event itself is a two-line hook in `EffectController.SetAbnormal`,
  which already has the before-and-after bitmask needed to tell *entering* a state from refreshing it.
  The work is the table, not the plumbing.

**This is the first time in this log that measuring a handler has argued against building it**, and the
measurement took one query against the numbers that were already to hand.

### And what was buildable: Panesterra's castles

`40000` and `40100` came off the silent-conversations list, and they are the base guards' mechanic one
tier up — the siegemake and siegebreak companies, **93 live npcs**.

**A castle answers harder than a base and is fussier about who it answers for.** Its guards take a
hundred where a base's take ten, and **every one of the fifteen answering patterns carries `is_enemy` on
the player named**, where among the ten base patterns exactly one did. Both read like a later pass over
the same design, which the numbering suggests too.

**The third `is_user_flying` reading in three commits, and the third to come out differently.** The
fortress guards' two halves broadcast identically, so the condition did not matter. Panesterra's base
patrols broadcast at different ranges, so it changed a number. Here it decides whether the branch
broadcasts *at all*: `Gab1_CastGaurd_Hide_PM_Ae_02` announces a puller who is in the air and says
nothing about one on the ground — so under the ground reading **six ambushers never call**, which is
retail's behaviour for every ground pull and wrong for an air pull. Recorded; they are bound as
answerers only.

### Verification

Fourteen pins on the Panesterra family now, and a **five-mutation sweep on the castle half, all
caught**: the payload dropped to a base's ten, the `is_enemy` deleted, the two companies sharing a
number, a caller that never calls, and the reach shortened. Full suite **2,195 passing**, 5 skipped.

## Chasing the abnormal-state groups, and a firm negative

Last entry left `on_enter_abnormal_state` measured and unbuilt: 272 patterns, **1,168 live npcs**, of
which **1,128 sit behind three names nothing readable defines** — `ABNSTATEI_MENTAL_GROUP`,
`ABNSTATEI_CANNOT_ACT_GROUP` and `ABNSTATEI_PHYSICAL_GROUP`. This entry is the attempt to define them.

**It did not work, and knowing exactly why is worth more than another guess would have been.**

### The lead, which was a good one

Aion has a real player-facing taxonomy behind these names. The game's own wiki describes **mental
conditions** as *"abnormal conditions such as sleep, fear or paralyze"*, removed by Cure Mind or
Cleanse, against **physical conditions** removed by Dispel — *"with the exception of Stun"*, which needs
Remove Shock. **Three player-facing families, three retail group names.**

And the split is already in our data: `skill_templates.xml` carries `dispel_category` on 2,800 skills —
**315 `DEBUFF_MENTAL`, 2,011 `DEBUFF_PHYSICAL`, 453 `STUN`**. That is the game categorising its own
debuffs, which is exactly the kind of source this log prefers to a judgment call.

### Why it fails

`dispel_category` is a property of the **skill**, not of the state it inflicts, and the same state is
inflicted by skills of different categories. Restricting to skills carrying exactly one state-bearing
effect — which removes every trace of multi-effect contamination, and still leaves 156, 863 and 407
skills respectively — the three categories **still overlap**:

```
   DEBUFF_MENTAL    ^ DEBUFF_PHYSICAL  CURSE, DEFORM, PARALYZE, SILENCE, SNARE
   DEBUFF_MENTAL    ^ STUN             PARALYZE
   DEBUFF_PHYSICAL  ^ STUN             OPENAERIAL, PARALYZE, STUN
```

**`PARALYZE` is in all three.** So no partition of `AbnormalState` bits can be read out of this table,
and a set built from the dominant members would be **a guess dressed as a derivation** — the worst kind
available here, because it would look sourced. `derive_abnormal_groups.py` exists to show that, not to
produce an answer; its closing line says so.

### What the attempt did establish

- **`STUN` is 90% `STUN`**, and its members are exactly `SPIN | STUN | STUMBLE | STAGGER` plus
  `OPENAERIAL`. Our `AbnormalState.ANY_STUN` is `SPIN | STUN | STUMBLE | STAGGER`. **That corroborates
  `ABNSTATEI_STUN_LIKE_GROUP` → `ANY_STUN` from a direction independent of the name**, which is the one
  group mapping that was already safe and is now evidenced.
- **The dominant mental states are `PARALYZE`, `SLEEP`, `FEAR`, `CONFUSE` and `DEFORM`** — 87% of the
  unambiguous mental skills, and a superset of the wiki's three examples. Suggestive. Not a definition,
  and the overlap above is why.
- **`ABNSTATEI_CANNOT_ACT_GROUP` has no counterpart in the dispel data at all.** It is a functional
  family rather than a curable one, so this whole line of attack could never have reached it. Our
  `CANT_ATTACK_STATE` composite is the obvious candidate and remains a judgment nobody has evidence for.

### What is still needed

A source that names members rather than skills: **a client build whose string table still carries the
group names** (the current one does not — all 3,332 `.pak` files were scanned for `MENTAL_GROUP`), an
NCSoft tools dump, or a server writeup that lists them. The event itself remains a two-line hook in
`EffectController.SetAbnormal`, which already has the before-and-after bitmask needed to tell entering a
state from refreshing it. **The work is still the table, not the plumbing.**

### The rule

**A source that categorises the wrong noun is not a source.** `dispel_category` categorises skills and
the guard needs states categorised; the two agree often enough to look usable and disagree exactly where
it matters. This is the second time in this log that a promising table has been the wrong shape rather
than the wrong content — the first was the tribe table's `support` against `friend` — and both times the
tell was the same: **check whether the thing being counted is the thing being asked about, before
believing the counts.**

No code or data change. Full suite unchanged at **2,195 passing**, 5 skipped.

## The top of the ladder: Panesterra's artifacts

`42001` and `42101` came off the silent-conversations list, and with them **the whole of Panesterra is
one ladder**:

| rung | call | answered with | reach | repeats? |
|---|---|---|---|---|
| base guard | `41000` / `41100` | **10** | 13m, 25m for lookouts | no |
| base captain | `41001` / `41101` | **100** | 13m | no |
| castle company | `40000` / `40100` | **100** | 25m | no |
| **artifact protector** | `42001` / `42101` | **1000** | **13m** | **every 7 seconds** |

**108 more npcs** — 72 protectors and 36 guards — and the three rungs are three sets of numbers that
nobody hears across. A base cutthroat's call leaves an artifact guard standing.

### The two things that make the top rung the top rung

**It repeats.** Every other call in Panesterra is a one-off: pulled, announce, done. The artifact
protector announces when it is pulled and again every seven seconds for as long as the fight lasts, so
its guards are re-committed continuously. **An artifact pull cannot be waited out the way a base pull
can**, and that — rather than the payload — is what actually decides whether a raid can peel anything
off an artifact.

**And it shouts quietly.** Thirteen metres is the shortest reach on the map, against the lookouts'
twenty-five. **The relationship between reach and payload is inverted all the way up the ladder:** a
base lookout shouts across the camp and is barely heeded at ten points; an artifact protector shouts to
whoever is standing on it and is obeyed absolutely at a thousand. That reads as deliberate — the ladder
is about who is *near* the thing worth guarding.

The artifact guards also check nothing at all: **no `is_enemy`, no state guard**, where the castles
check who was named and one base pattern in ten does. The strongest answer in Panesterra is also the
least discriminating.

### Thirty-six patterns that differ in nothing

`Gab1_DrArtiGuard_Boss_01_01` through `_04_08`, plus the `Gab1_DArtiGuard_Boss_*` set — **thirty-six
retail patterns for the Vritra side and eight for the other, one npc apiece, identical in every
action.** Retail gives each artifact and each slot its own pattern name, which is bookkeeping rather
than behaviour, and they collapse into two classes.

### Not built

- **`42000` / `42100` / `42200`** — the artifact guards' *own* call, as distinct from their
  protector's. They broadcast it at **one metre**, and one of them at twenty-five. **A one-metre
  broadcast is a strange enough number to want its own reading before it is translated**; recorded
  rather than guessed at, and it is the only part of the Panesterra family now left.
- **`4440444`** and **`909090`**, both broadcast by the artifact protectors at fifty and thirty metres.
  The first is Panesterra's base-capture announcement, already recorded against the warcaptains as
  belonging with the siege code rather than the AI layer; the second has no listener anywhere in the
  5.8 files.
- `percent_to_add=10` on every artifact answer, as everywhere else in this family.
- The skills on the guards' answers.

### And what is queued behind it

**`23005`** is the next one of this shape: the Dispute-PvP guards of the 5.x maps, `2` callers and
**39** answerers, with the fortress guards' exact one-idle/hundred-busy split at fifteen metres.
Captain Wigthor's call is on a battle timer guarded by an HP boundary, which is a shape none of the
other guard families use. Read but not built this turn.

### Verification

Twenty pins on the Panesterra family now, and a **six-mutation sweep on the artifact half, all
caught**: the payload dropped to a castle's, the repeat deleted, the reach widened, the two sides
sharing a number, the guards listening on the base number, and the repeat slowed. Full suite **2,200
passing**, 5 skipped.

### The near-miss: a name that was already taken

The first run of the full suite failed on the bootstrap test with
`Duplicate AIs with name artifact_protector`. **There is already an `ArtifactProtectorAI` in this
tree** — a Java-parity siege class, with **1,013 npcs bound to it**, whose `HandleDied` calls
`StopSiege`: it tallies the aggro list into the siege counter, marks the boss killed and ends the
siege.

**My repoint had bound forty-seven Panesterra protectors to that name.** They were on stock AI before,
so nothing was lost — but had the class name not also collided, the suite would have gone green and a
hundred and eight Panesterra npcs would each have been carrying **a siege-ending action on death**.
Renamed to `panesterra_artifact_*`.

**What caught it was the engine's own duplicate-name check, not anything I did.** The fortress-guard
commit checked for an existing `fortress_guard` AI name before writing one; this commit did not make
the same check, and got away with it only because the collision happened to be exact.

**Rule: check the AI name against the existing registry before writing a class, every time — the
repoint is what makes it dangerous, not the class.** A new class with a colliding name fails loudly. A
new class with a *free* name, repointing npcs onto a name that already means something else, fails
silently and in the siege layer.

### An open question this raised

`Gab1_*ArtiGuard_Boss_*` npcs are Panesterra's artifact protectors, and Panesterra is the Ahserion
siege. **Should they be carrying the Java-parity siege class rather than a retail pattern?** Our data
had them on stock AI, so this commit does not change what they were; but an artifact protector whose
death does not end a siege is worth someone checking against the Java tree. **Recorded, not decided** —
it is a Java-parity question rather than a retail-AI one, and this log is the wrong place to settle it.

## Teaching the backlog to stop lying, and the tursin loudmouths

The last two entries each spent their reading on a row that turned out to be worth nothing —
`23005` at 41 npcs, whose live answerers answer Captain Wigthor with a skill index. That is the third
time, after `3302` at 157 and `10001` at 1,030. **The audit was ranking by npc count and saying nothing
about whether the answer could be built**, so it kept sending me at the biggest unreachable thing on
the list.

`audit_silent_conversations.py` now classifies every row by what its **live** answerers actually do:

```
     msg  call  ans  answer     who
   10001   884  146  skill-only ranx channeler, ranx arcanist
    9001   208    2  hate       lepharist protector, sentinel
    3302   118   42  skill-only baranath priest, baranath fleshmender
   60001     4   61  self-named tejhi coralblade, black fin surveyor
    1002    28   29  hate       mamaki worker, mamaki peon
   23005     2   39  skill-only captain wigthor, defense corps shaderanger
```

**49 of 116 are reachable.** The other sixty-seven were the top of the list.

### Two ways a row lies, and both are now named

**`skill-only`** is the obvious one: every answering branch is a `use_skill`, so the row is unbuildable
however many npcs sit on it.

**`self-named`** is the one that took a second pass. `60001` — sixty-one tejhi answering four callers —
*is* a hate action: `add_hate_point` of a thousand. But every caller broadcasts with
`param_obj=OBJI_SELF`, so the object being hated is **the caller**, a friend, and `AggroList.AddHate`
drops hate aimed at a non-enemy. Retail's own comment on the branch reads *"join combat when broadcast
60001 is sent"*, so retail evidently treats hating a friend as a way to enter a fight. **This port reads
it as nothing at all.** A hate action is not the same as a reachable one.

**And the classifier had to be told to look only at the live answerers.** Its first version marked
`23005` buildable on the strength of `LDF5_*_DisputePvP_Support_Ele_Ee`, a pattern with the right shape
and **no npc anywhere in our spawn data** — reproducing, inside the fix, exactly the over-promise the
fix was for.

### `1002`: the loudmouths

The top reachable row, and the low-level ancestor of every guard call in this log — **36 npcs in
Altgard**.

| pattern | live | what it does |
|---|---|---|
| `Krall_KnA`, `Krall_KnC` — tursin big boss, loudmouth | 3 | **below forty health, once**, names its target at 15m |
| `NKrall_KeA` — kaidan bigmouth | 7 | **fifteen seconds into any fight, once**, at 20m |
| `NBrownie_FnC` — mamaki worker | 17 | answers with **100**, and calls when it stops fleeing |
| `Brownie_FnQ`, `Brownie_FnR` — dukaki miner, digger | 9 | answers with **101** |

**The bigmouth calls on a clock and not on its health**, which is the only caller in this log that
does. One killed inside fifteen seconds never calls; one that survives always does, at full health or
at one percent. And it calls once — the timer carrying the call is never re-armed, while a second timer
runs forever carrying a number nothing listens to.

**The workers answer with a hundred and the miners with a hundred and one**, because retail gives the
miners an `add_hate_point` before the switch and the workers only the switch. One point of difference
between two npcs standing in the same camp, and it is retail's.

### A pin that failed twice for the same reason

The bigmouth's clock pin asserted the miner had **zero** hate before the call. It had one. Not from the
pattern: **a fight running near a friendly npc puts a point on the attacker through the engine's own
support aggro** — and not at engage either, but on the first attack tick, which is why taking the
baseline immediately after `Engage` failed the same way.

**What separates "called" from "did not" is the size of the step, not the total.** The pin now measures
the jump: under 101 before, exactly +101 after. Worth recording because every pin in this file counts
hate, and any of them could have been written against a baseline that was never zero.

### Not built

- **`1398`**, the kaidan bigmouth's other call, on the timer that does repeat. Nothing our data places
  listens to it.
- `percent_to_add` on both answers — **including a `percent_to_add=0`** the miners carry, which does
  nothing under any reading and is left out rather than translated to a no-op.
- The shouts and the skills throughout.

### Verification

Seven pins and a **seven-mutation sweep, all caught**: the miners' extra point, the workers answering
like miners, the health threshold, the shared flag, the tursin's reach, the bigmouth's call repeating,
and the bigmouth never calling. Full suite **2,207 passing**, 5 skipped.

## The camps that never stop calling

Two rows off the reachable list, both the same shape and both new in one respect: **`2001`, the
kerubiel bandits and their fighters, and `2005`, the kerubian hunters and their garks. Sixty-six
npcs in Verteron and Eltnen.**

| pattern | live | what it does |
|---|---|---|
| `ND2_AnE` — kerubiel bandit | 9 | below half health, **on every blow**, names its target at 15m |
| `ND2_AnL` — kerubiel fighter | 20 | answers with **101** |
| `ND2_AnJ` — kerubian hunter | 12 | below half health, **on every blow**, at 20m |
| `ND2_AnJ_BR` — gark | 25 | answers with **200** |

### The first callers in this log that do not call once

**Retail puts no flag var on either branch of either caller.** Everything before this — the klaws, the
lycans, the tursin loudmouths, the fortress guards — announces once and then goes quiet. A kerubiel
bandit under half health keeps naming the same player for as long as the fight lasts.

**That is the mechanic, not an oversight.** A camp with a once-only caller answers a call; a camp with
these answers *continuously*, so every fighter that wanders into earshot is pulled in as it arrives
rather than only those standing there when the threshold was crossed. Pinned directly: three blows
below half health put three payloads on the raider.

**And the garks hit twice as hard as the fighters** — retail gives a gark an `add_hate_point` of a
hundred where a kerubiel fighter gets one, so 200 against 101. Two camps, the same call shape, the pets
committed twice as far as the soldiers. Twenty-five garks to twelve hunters makes that arithmetic
matter.

### The third time the tribe table has decided a pin

Every hunter-side assertion read **zero** on the first run. Not the branch: `ND2_AnJ_BR`'s membership is
mixed — fourteen `TAURIC`, seven `MONSTER`, three aggressive and **one `GENERAL_DARK`** — and the
`GENERAL_DARK` one sorts first, so it was what the pin picked to stand for the pattern. An
Asmodian-side npc is not the enemy of an Elyos raider by that tribe, and `AggroList.AddHate` drops hate
aimed at a non-enemy.

After the fortress guards and the Panesterra slayers, that is three. **Stated as a habit rather than an
anecdote: when a pin over a broadcast reads zero, check the answerer's tribe before checking the
branch.** The npc chosen to stand for a pattern has to be one whose tribe can hate a player, and the
first id in a pattern's membership is not chosen for that.

**It is also a fact about the pattern**, not only about the pin: one npc in twenty-five on `ND2_AnJ_BR`
will never answer anything, because its tribe cannot hold the hate. Retail presumably has other rules;
here it is simply inert, and worth knowing before somebody reports it as a bug.

### Not built

- The skills on all four patterns — two per branch on the bandits, one on the hunters, one on each
  answer.
- `percent_to_add=0` on both answers, which does nothing under any reading.

### Verification

Eight pins and a **six-mutation sweep, all caught**: a flag added to the bandit's call, the two payloads
swapped in both directions, the camps sharing a number, the hunter's reach shortened, and the
half-health guard dropped. Full suite **2,215 passing**, 5 skipped.

## Two mutations that hid behind each other

`22001` — the Tiamat Remnant insurgents, **15 npcs**. Four scouts and eleven infantry, and the smallest
family in this log by some way, but it produced the sharpest lesson.

**Twelve seconds into a fight the scout names its target, and the infantry commit three hundred** —
the largest answer to a field call anywhere in this log. Twelve is not a number retail writes down: it
is a chain of timers, five seconds then seven, and the seven-second one carries the call. After it
fires the chain hands over to a pair that swap every fifteen seconds forever, carrying skills.

**Each side does its thing exactly once** — the scout's call timer is never re-armed, and retail flags
the infantry's answer.

### The lesson

The sweep found **two survivors, and each was hidden by the other's mechanism.**

- Re-arming the scout's call timer changed nothing, **because the infantry's flag refused the second
  call.**
- Deleting the infantry's flag changed nothing, **because the scout never made a second call.**

One pin covered "each side does it once" and it could not fail, because the two halves of that sentence
protect each other. **Two guards enforcing the same observable are indistinguishable from one**, and a
sweep is the only thing that shows it — reading the code, both look pinned.

The fix is two pins that break the symmetry:

- the infantry's flag, measured against **two scouts**, so a second call exists to refuse;
- the scout's single call, measured against **an infantryman that arrives after the first**, so a
  listener with an unspent flag exists to receive one.

**Neither observation is available in the setup the other one needs**, which is why one pin could never
have done it.

### And the support-aggro baseline, again

The late-arriving infantryman was asserted at zero hate and had one — **the same support-aggro point
the tursin bigmouth's pin ran into**, from a fight running beside a friendly npc. Second time in two
entries. The pin now asserts under three hundred rather than zero, which is what the claim actually is:
a call is worth three hundred, and one point is not a call.

### Not built

- Four skills on the scout and the shout that goes with the call.
- The rotation timers are built and carry nothing, for the reason Masto's spare timer was: **a pattern
  missing a timer is a different pattern.**
- **The infantry's answer has no blocked action at all** — a `switch_target` and its payload, both
  translated. The first answer in this log that is complete.

### What the reachable list looks like now

`audit_silent_conversations.py` marks **49 of 116** rows buildable. Two more were read this turn and
rejected before any code was written:

- **`7004`** — the brutal lycan camp, 24 callers and 5 answerers. **Self-named**: every caller
  broadcasts `OBJI_SELF`, so the sentries' hundred points land on the caller, a friend, and are dropped.
  The classifier added last entry caught it, which is the first time that check has paid for itself.
- **`9001` and `1016`** — the Lepharist camps, **208 and 113 callers against two answerers each**.
  Genuinely buildable and genuinely lopsided: binding three hundred npcs to a caller class so that four
  protectors respond. Worth doing, worth doing knowingly, and worth doing after the balanced rows.
- **The shulack mercenaries** (`21251`, `21253`, `21271`) are the best-shaped thing left: three numbers,
  about thirty npcs, one instance. **Ten patterns whose send/answer combinations all differ**, so it is
  ten small classes rather than four — the reason it was not taken this turn rather than a blocker.

### Verification

Six pins and a **six-mutation sweep, all caught** after the symmetry was broken. Full suite **2,221
passing**, 5 skipped.

## The shulack relay: built, reverted, and written down

The shulack mercenaries of the Danuar Sanctuary were the best-shaped thing left on the reachable list —
**three numbers, eleven patterns, twenty-six npcs**, one instance. They were translated in full, and
then **reverted unshipped**, because one pin would not go green and I did not understand why.

Recording it because the reading is worth keeping and the blocker is worth naming.

### What the family is

**The first relay in this log.** Every other call here goes out once and is answered by whatever stood
in the circle. A shulack watcher that hears `21253` takes its hundred points, **arms a one-second
timer, and re-broadcasts the alarm itself** — so the alarm walks outward through the camp a second at
a time. Retail flags that branch, so each watcher relays once, which is what stops the camp ringing
forever.

| rank | number | payload |
|---|---|---|
| officers (Sachirunerk, the two bodyguards) | `21251` | **1000** |
| the alarm, which relays | `21253` | 100 |
| rank and file (the cannon chief's) | `21271` | 100 |

**The slaves answer the alarm too** — the dukaki peons and seized miners, tribe `IDF5U2_SHULACK_SLAVE`,
the very thing the mercenaries are guarding, take a hundred points on whoever the alarm names.

### The typo

`IDF5_U2_ShulackM_Fi_party_65_Ae` is the watcher with one digit changed. Where the watcher relays
`21253`, the assaulter relays **`21153`** — and `21153`'s only listener anywhere in the 5.8 files is
`IDRuneWP_A3_Protection_65_n`, **a rune-weapon pattern from a different instance entirely**.

So half the assaulter's relay goes nowhere: two npcs in the same camp, one passing the alarm on and one
not, because somebody typed a 1 for a 2. **That is retail's behaviour and would have been kept**, not
corrected — a typo is a quirk with a cause rather than a different kind of thing.

### Why it was reverted

The second hop could not be demonstrated. A caller's alarm reaches a watcher forty metres away
(1000 → 1100 hate, confirmed); the relay branch **does** run a second later — proved by giving it a
second, visible action, which landed; and the slave forty metres beyond the watcher takes **nothing**.

What was ruled out:

- **Not range.** Widening the relay to five hundred metres changed nothing.
- **Not the timer.** The branch executes; its other action lands.
- **Not broadcasting from a timer callback in general.** Guardian Vingeveu broadcasts from
  `OnBattleTimer` and its pins measure the answer.
- **Not the recipient's ignorance of the caller**, nor of the player: both were made mutually known.

**What is left is the difference between this setup and Vingeveu's**, which is that the answering npc is
seventy-seven metres from the player it is being told to hate rather than twenty. That is a guess, and
guessing is how the last several entries' worst mistakes started.

### The rule

**A correct translation whose central mechanic cannot be observed is not shippable.** The flee actions
in this log are built and unpinned, and that is fine, because *why* they cannot be pinned is understood
and written down. Here it is not. Shipping twenty-six npcs whose relay might silently do nothing is the
same failure as the thirty-seven missed siblings — correct-looking code, passing pins, and a mechanic
nobody would notice was dead.

### What the next person should do

1. Reproduce with two npcs and no distance: caller, watcher and listener within ten metres of each
   other and of the player. If the second hop lands, the blocker is distance and the question becomes
   which layer imposes it.
2. If it still does not land, instrument `NpcMessageBus.Nearby` inside a timer callback and compare its
   audience against the same call made synchronously.
3. The reading above is complete — patterns, ranges, payloads, the flag, and the typo — so only the
   engine question is open.

## Ruling the engine out of the shulack relay

The previous entry reverted the shulack mercenaries with the second hop of their relay undemonstrated,
and wrote down three steps. This is step one, and it produced a permanent test rather than an answer to
the encounter.

### What the probes established

`MessageRelayTests` builds the relay shape out of three throwaway pattern classes — a caller that
broadcasts, a relay that hears it and re-broadcasts from a one-second battle timer, and a listener that
answers the second number — and runs it across every axis the failure could have turned on:

| tried | result |
|---|---|
| listener 6m from the relay | **relays** |
| 20m, 30m, 40m, 45m | **relays** |
| relay range widened to 500m | (the original failure was unaffected) |
| Altgard and Danuar Sanctuary | **relays on both** |
| npc ids from the tursin camp and from the shulack camp | **relays with both** |

**So the engine relays messages correctly**, and every hypothesis in the previous entry's list is dead:
not range, not the timer, not broadcasting from a timer callback, not the map, not the npc identity,
and not the tribe.

### Which means the fault was mine

**A relay that fails in one class, when the primitive works everywhere else, is that class's problem.**
The last entry hedged between "harness limitation" and "engine bug" and it was neither.

Running the real classes in the probe's own geometry narrowed it further, and turned up something the
original test never showed: **in that run the watcher did not take the alarm's hundred points at all**
— it ended on the single point support aggro gives, where the original failing test had it at 1100.
The same two classes, the same message, two different outcomes depending on the surrounding setup. That
is where the next session starts, and it is a much smaller question than the one this began with.

### Why this is a test and not a note

The probe is kept as `MessageRelayTests` rather than deleted. **Without a pin on the primitive there is
no way to tell an engine limitation from a mistake in a translation** — which is exactly the position
the previous entry was stuck in, and it cost a shipped encounter. The next relay pattern will want the
same reassurance, and anybody reading the shulack entry will want to know the difference between "this
does not work" and "this did not work for me".

**The rule: when an encounter fails on a primitive this port has never pinned, pin the primitive
first.** It is cheaper than debugging the encounter and it survives the encounter.

### Still open

- **The shulack family remains unshipped** — eleven patterns, twenty-six npcs, the full reading in the
  previous entry. The engine is exonerated; the translation needs re-testing against the geometry above.
- The `21153` typo finding stands and is worth keeping whatever happens to the rest.

### Verification

Five pins, all green. Full suite **2,226 passing**, 5 skipped.

## The shulack relay, shipped — and the bug was in the test all along

Two entries ago the shulack mercenaries were reverted with their relay undemonstrated. The last entry
pinned the relay primitive and ruled the engine out. This entry finishes it: **eleven patterns,
twenty-six npcs, shipped**, and the cause of the original failure found.

### The cause

**A probe used `Spawn` instead of `SpawnWithAi`.** `Spawn` reads the AI name off the npc template —
and by the time that probe ran, the template repoint had been rolled back with the rest of the revert.
So the "watcher" under test was a **stock aggressive npc that had never heard of the pattern**, and it
sat there not relaying, exactly as a broken class would.

**Two entries of investigation, and the class was right the whole time.** The engine was exonerated
correctly, the reading was correct, the ranges and payloads were correct — and the thing under test was
not the thing I thought I was testing.

**The rule: when a pin's subject is chosen by data rather than by name, the data is part of the pin.**
Every npc in the new tests is spawned with its class named explicitly, which cannot go wrong that way.
Cheap, and it would have saved two entries.

### What shipped

| rank | number | payload |
|---|---|---|
| officers — Sachirunerk and the two bodyguards | `21251` | **1000** |
| the alarm, which **relays** | `21253` | 100 |
| rank and file — the cannon chief's | `21271` | 100 |

**The first relay in this log.** A watcher that hears the alarm takes its hundred, arms a one-second
timer, and re-broadcasts — so the alarm walks outward through the camp a second at a time and reaches
npcs the caller has never seen. Pinned directly: a slave outside the caller's known list is pulled in
three seconds later.

**And the slaves answer the alarm** — the dukaki peons and seized miners the mercenaries are guarding
take a hundred points on whoever the alarm names.

### The typo, kept

`IDF5_U2_ShulackM_Fi_party_65_Ae` is the watcher with one digit changed: it relays **`21153`** where the
watcher relays `21253`, and `21153`'s only listener anywhere in the 5.8 files is
`IDRuneWP_A3_Protection_65_n` — a rune-weapon pattern from a different instance. **Half the assaulter's
relay goes nowhere.** Two npcs in the same camp, one passing the alarm on and one not, because somebody
typed a 1 for a 2. Kept exactly as written and pinned as such: the assaulter takes its hundred like the
watcher, and nothing beyond it hears a thing.

### One guard reported rather than covered

**Deleting the relay's `FirstTime` fails nothing.** A second caller engaging beside a watcher already
in combat does not produce a second relay even unflagged, in every arrangement tried — so the guard has
nothing to refuse and no pin can falsify it. The pin holds the observable claim (one alarm, one relay)
and the flag stays because retail wrote it. Same treatment as the friend-killed `finally` and Masto's
inert timer re-arm.

### Verification

Seven pins and a **six-mutation sweep, five caught**: the officers' payload, the relay deleted, the
typo corrected, the cannon chief's reach, and the slaves listening on the wrong number. Full suite
**2,233 passing**, 5 skipped.

## The ice claw camp: two grades of the same animal

`7006` and `7007` off the reachable list — **the brutal ice claw and mist mane camp of Beluslan, 22
npcs.** It is the black claw lycans of Morheim, built early in this log, tuned up: the same hunters and
tamers keeping the same taygas, with three differences worth the reading.

| pattern | live | what it does |
|---|---|---|
| `nlycan_HeA` — ice claw hunter | 9 | calls `7006` on engaging; calls `7007` **below half, once** |
| `NLycan_HeB` — brutal ice claw tamer | 4 | calls `7006` on engaging; again **below a third, once** |
| `NLycan_Pet_A` — ruthless tayga | 4 | answers **both** calls with **500** |
| `NLycan_Pet_B` — ruthless tayga | 5 | answers **only the first**, with **100** |

### Two grades of the same creature

**The two taygas carry the same name on the nameplate and answer five times apart.** One commits five
hundred to either call; the other commits a hundred to the opening call and does not hear the second at
all. Nothing a player can see distinguishes them, so which tayga a pull happens to bring decides whether
the handler is reinforced or merely accompanied.

**Five hundred is the largest answer to a field call outside Panesterra** — five times what the black
claw taygas of Morheim give to the same shape of call.

**And the thresholds differ by five points between the two callers**: the hunter's second call is below
a half, the tamer's below a third. Pinned as its own fight, because a tamer at forty-five percent must
be shown *not* calling.

### The loop that is not built

A ruthless tayga below half health broadcasts `7008` at ten metres **naming itself**, and its handler
answers with a skill on it — a heal. So the camp's conversation runs both ways in retail and only one
way here: **the handler's calls are translatable and the tayga's cry for help is not**, being a skill
aimed by a self-named message whose payload would land on a friend and be dropped. The
silent-conversations audit flags exactly this shape. The constant is kept so the hunter's listener still
names the right number.

**Also unbuilt: the tamer's timer-zero fallback.** Retail gives it a low-priority branch that re-arms
the timer when the health guard fails; this port has only the guarded branch, so a tamer whose timer
comes round above a third loses it. **The pin had to be written around that** — health set before the
timer fires rather than after — and saying so in the pin is what stops the next person reading the
workaround as a preference.

### Verification

Nine pins and a **seven-mutation sweep, all caught** once the tamer's threshold got its own fight: the
two grades equalised, the lesser grade listening on the wrong number, the hunter's second call deleted
and then made to repeat, the tamer's threshold, the reach, and the taygas ignoring a friend's killer.
Full suite **2,242 passing**, 5 skipped.

### And a third instance of one small thing

Three pins in this file needed `InRange` rather than `Equal`, because **a fight running beside a
friendly npc adds a support-aggro point of its own on the first attack tick**. That is the third entry
in a row to trip on it — the tursin bigmouth, the Tiamat insurgents, and now here. It is written into
each pin's comment rather than abstracted, because the number that matters is different every time:
what a call is worth is five hundred, or three hundred, or one hundred and one, and one point is never
any of them.

## The ratman camps: the farmer is not the fight

`1007` and `8001` off the reachable list — **the mumu, dundun, munmun and nunu camps of Altgard and
Beluslan, 30 npcs**, and one arrangement repeated at two levels: **a worker that is attacked names its
attacker, and what answers is a lycan.**

| pattern | live | what it does |
|---|---|---|
| `Ratman_FnR`, `Ratman_FnR_LWaSu11`–`13` — dundun and mumu farmers | 10 | call `1007` at 12m, **on every blow** |
| `Lycan_KnA` — gray mane stalker | 3 | answers with **101** |
| `NRatman_FnA`, `NRatman_RnA` — munmun warriors and sentinels | 7 | call `8001` at 15m **when pulled** |
| `NRatman_FnC` — nunu farmer | 4 | calls below a third, **and again for a friend's killer** |
| `NRatman_RnC` — munmun patrol | 1 | calls when pulled **and** below a third |
| `NLycan_KeA` — kuriuta and ice claw guards | 5 | answers with **200** |

**Beluslan answers twice as hard as Altgard** — two hundred against a hundred and one, for the same
arrangement one zone north. And the two ends of the camp announce differently: **the warriors announce
a fight and the farmers complain about one.**

### The nunu names the killer

`NRatman_FnC`'s friend-killed branch broadcasts with `param_obj=OBJI_KILLER` — so a nunu watching a
neighbour die calls the lycans down on **whoever did it**, not on whatever it was fighting itself.
That needed `Do.BroadcastAboutFriendsKiller`, which is new: the friend-killed handler already carried
the killer for *hating*, and this is the first branch that wants to *name* them.

**Two flags, one per call**, so a nunu beaten low that then sees a neighbour fall calls twice.

### The mutation that needed one npc to do both things

A shared flag between those two calls **survived every pin in the file**, because the pins used two
different nunu — one for the hurt call and one for the friend's death. Neither can show a flag being
spent by the other. It took one nunu doing both in one fight.

**Same symmetry as the Tiamat insurgents' pair of "once" claims**, two entries ago, and the same fix:
when two guards protect the same observable, the pin has to put both on the same npc.

### The tribe rule, for the fourth time

The kuriuta that heads `NLycan_KeA`'s membership is `GENERAL_DARK` — an Asmodian-side npc whose aggro
list refuses hate aimed at an Elyos raider — so every Beluslan assertion read zero until the pin picked
a `LYCAN`-tribe member instead. **Fourth time**, after the kerubiel garks, the fortress guards and the
Panesterra slayers. The rule is written into the constant itself now rather than the remarks.

### Not built

- **`OBJI_FLEE_FROM`.** `NRatman_FnA` has a second call, sent when it stops fleeing, naming **the thing
  it ran away from**. This port does not retain that object. Every other blocked param in this log is a
  skill index or a self-name; **this is the first that has no equivalent at all**, and it is worth its
  own line because the fix is an engine field rather than a table.
- **`1017`**, the farmers' second call: the same event broadcast again *naming themselves*, whose only
  live listeners belong to an unrelated Lepharist conversation that happens to use the same number.
  Self-named and cross-wired. The Lepharist half of `1017` — eleven defenders calling eight bastion
  drudges for a single point — is genuinely buildable and is the next thing on this shelf.
- The skills throughout.

### Verification

Nine pins and a **seven-mutation sweep, all caught** once the shared flag got its own fight. Full suite
**2,251 passing**, 5 skipped.

## The Lepharist bastion: a whisper and a shout

`1017` and `1018` — **the lepharist defenders and their bastion drudges, 19 npcs**, and the sharpest
contrast in this log between two calls from the same npc.

| call | reach | payload | when |
|---|---|---|---|
| `1017` — **the whisper** | **5m** | **1** | on being pulled |
| `1018` — **the shout** | 15m | **100** | on a combat timer, in a health band |

**Three times the reach and a hundred times the payload**, decided by which branch fires. Five metres is
the shortest call anywhere in this log: a defender being pulled tells whoever is standing on top of it
and nobody else, and buys a single point of hate for it.

### The shout is not built, and the answer to it is

`1018`'s sender rides a battle-timer branch guarded by **`is_skill_count_left`** — it fires only while a
particular skill still has charges, and this port has no notion of a skill's remaining uses.

**Building it without the guard would make a defender shout in a health band where retail may have
fallen silent**, which is inventing behaviour rather than translating it. So the sender stays unbuilt —
but **the drudges' answer is built and pinned**, by sending the message directly. The day the charge
guard becomes expressible, the half that answers is already known to work.

That is a better shape than the usual "recorded as blocked": **half a conversation is worth building
when the half you can build is the half that will still be right.**

### The guard that judges the fight rather than the npc

A drudge below thirty percent flees — **but only from an attacker still above forty.** A drudge that has
nearly killed the player stays and finishes the job.

Every other health guard in this log reads the npc's own health. This one reads the player's, and it
needed a new condition (`When.TargetHpBetween`) to say at all.

**It is built and unpinned, for two reasons rather than the usual one.** The flee needs the move
controller, as every flee here does; and the guard needs a player at a chosen health, which the harness
has no helper for — `SetExactPercent` takes an NPC. **The missing piece is a way to hurt a test
player**, and that is worth more than this one pin: it is the first time a guard has been unpinnable
because of what the harness cannot do to a *player*.

### And the drudges are what fetch the protectors

Both the defenders and the drudges broadcast `1016` when they stop fleeing — the number the two
lepharist protectors listen for, and the only thing in our data that answers it. **A drudge that runs
and turns is what brings them.** That closes the small end of the `9001`/`1016` pair without binding
the three hundred callers those numbers otherwise want.

### Verification

Three pins and one skipped, and a **five-mutation sweep, all caught**: the whisper's reach, both
payloads swapped, a defender that never whispers, and drudges listening on the wrong number. Full suite
**2,254 passing**, 6 skipped.

## Four pins that said "impossible" and were wrong

Every flee in this log has been shipped with the same note, in four files:

> Flee computes a destination and hands it to the move controller, and this harness advances a virtual
> clock without simulating movement — so a pin asserting it had moved would fail for correct code and
> one asserting it had not would pass for broken code.

**All of that is true, and none of it is the point.** `PatternAi.FleeingTo` records the destination the
flee computed, and it has been `public` since the action was written. **The movement is unobservable;
the decision to flee, and its direction, never were.**

Four skipped pins are now four real ones, and each measures something the note said could not be
measured:

- **The klaw sentinel** runs from the player it is fighting at a third health, *away* from it.
- **The drakie** runs from **what it saw** rather than what it was fighting — and it has no target when
  it runs, so a target-based flee would have done nothing at all. **That is the distinction the whole
  `Do.FleeFromSeen` action exists for, and it went unpinned for the entire session.**
- **The black claw tamer** runs from **whoever killed its tayga**, not from its own attacker.
- **The bastion drudge** runs from a healthy attacker below thirty percent.

A three-mutation sweep confirms it: replacing `FleeFromSeen` with `Flee`, or `FleeFromMessageParam` with
`Flee`, now fails. **Those two actions were added specifically to fix a bug, and until today nothing
would have caught the bug coming back.**

### The rule

**"The harness cannot observe X" is a claim about the harness, and claims about the harness need
checking too.** This one was written once, honestly, from a real failed attempt at asserting position —
and then copied into three more files as though it had been established. It had been established about
*position*. Nobody looked for another observable.

That is the same shape as the friend-killed handler's "its branches never name one", which was also
written to justify not doing something and was also false. **A claim that closes a door is worth one
more minute than a claim that opens one**, and this log has now produced two of them.

### What is still genuinely out of reach

**The drudge's negative case.** Retail flees only from an attacker still above forty percent — a drudge
that has nearly killed the player stays. Showing that needs a player at a chosen health, and the
harness's `SetExactPercent` takes an NPC. **A way to hurt a test player** is the one real gap here, and
it now blocks exactly one assertion rather than four pins.

### Verification

Four skipped pins replaced by four real ones, and a **three-mutation sweep, all caught**. Full suite
**2,258 passing**, 2 skipped — down from 6, and the two that remain are the scatter-with-one-attacker
coin flip and Preceptor's trio, neither of which is a flee.

## One signature, and the last two skips

The previous entry ended with one named gap — **a way to hurt a test player** — and one skip it had not
looked at. Both are closed.

### `SetExactPercent` takes a `Creature`

That is the whole change. It took an `Npc`, and **the negative half of every guard that reads the
player's health was therefore unpinnable**. The bastion drudges are the only such guard in this log so
far: they flee below thirty percent, *but only from an attacker still above forty*, so a drudge that has
nearly killed the player stays and finishes the job.

Both halves are now pinned, and both mutations bite — deleting the guard, and inverting its band. **One
parameter type was the difference between a condition nobody could test and a condition with complete
coverage.** The stand-in player is invulnerable to *damage*, which is what stops a fight ending a test;
setting its health directly was never damage and was never blocked.

### Vingeveu's scatter, skipped for a reason that had already been solved

> a random target switch with one attacker is not an observation

True — of the setup it was written for. With three players it is a one-in-half-a-million coincidence to
hold the same one twelve fights running. **That is the stated-exponent technique Masto's opening scatter
already used, in a file written earlier in this same log**, and it was not applied here.

**And the pin failed twice before it worked, on something worth keeping:** the scatter picks from the
aggro list's **attackers**, not from everything on the hate list. Adding hate to a bystander does not
put it in the pool — so the first version, which gave two players hate and engaged only the third,
watched the boss hold its tank every single time and looked exactly like a scatter that did not work.
**Each of the three has to have actually attacked.**

### Where the skips stand

Two remain, from six at the start of the previous entry:

- **The Preceptor's trio** — an NRE inside `Effect.ApplyEffect` applying skill 8217 to the stand-in
  player. A specific, reproduced, one-layer-deeper blocker with its own note; not a claim that needs
  re-testing.
- …and that is all. The other is now zero: **every "cannot be pinned" in the AI suite has been either
  proved or replaced.**

### The rule, now twice earned

**A technique that solved a problem once is not automatically applied the next time the problem
appears.** Masto's file knew how to pin a coin flip; Vingeveu's file, written days earlier in the same
run, said it could not be done. Nothing connected them but a person remembering — and the log is now
the thing that connects them.

### Verification

Full suite **2,259 passing**, 1 skipped.

## The citadel overseers, and a flag that does not hold

Two rows this time. One shipped, one stopped at the door, and one that was never a row at all.

### Shipped: the Lepharist citadel

`9003` — **eight citadel overseers and five labourers, 13 npcs.** An overseer calls its labourers when
it is pulled, at twenty metres, and they commit a hundred. When it stops running away it calls again on
two numbers at once: `9003` at fifteen metres for the labourers, and `9001` at ten — **the same pair of
lepharist protectors the bastion drudges fetch**, and the only live listeners that number has.

### Stopped: the Esoterrace alarm, and why

`10000` looked like the find of the day. **One npc — the surkana feeder — with five alarm bands, and
nineteen esoterrace drakan listening.** Retail writes the bands in descending priority with the tightest
guard first and gives each its own flag, so the feeder announces once when first touched and again the
first time it passes eighty, sixty, forty and twenty percent: **the facility arrives in five instalments
and the laddering is entirely branch order plus five flags.** No counter, no timer.

It is translated, and it was **taken back out**, because the flags do not hold.

A probe: feeder at exactly a hundred percent, attacked six times without its health changing.

```
pct0=100  1=10  2=20  3=30  4=40  5=50  6=60
```

**The unguarded band fires on every blow.** `When.FirstTime` is `TestAndSetFlag`, which returns false
once set; `ResetPattern` clears the flags but is only called from back-home, died and despawned, none of
which happens between two attack events. So a branch whose *only* guard is a flag re-fires, and the
five-instalment ladder collapses into "ten points per hit, for ever" — an alarm that never stops
ringing.

**Every once-only branch in this log has a health or timer guard in front of its flag**, and every one
of them is pinned and passing. That is either a coincidence across a dozen encounters or the shape of
the bug, and finding out which is the next step:

1. Pin `FirstTime` on its own, in a throwaway pattern with no other guard — the primitive first, which
   is the rule the shulack relay earned.
2. If it holds there, the difference is in this pattern; if it does not, a dozen shipped encounters
   have a guard that only looks like it works because something else fires first.

**The second possibility is why this was not shipped with a note.** An alarm that rings for ever is
visible; a once-only branch that quietly becomes an every-time branch is not.

### Never a row: `6512`

The baby cellatu, six callers and ten answerers, marked reachable by the audit. **Its only caller branch
broadcasts on `on_stop_to_flee` naming `OBJI_FLEE_FROM`** — the param recorded two entries ago as the
first with no equivalent in this port at all. The answer is buildable and nothing can ever send it.

**The classifier checks what the answer does and never asks whether the caller can fire.** That is its
third blind spot, after skill-only answers and self-named ones, and the fix is the same shape: ask the
same question of the sending branch. Recorded rather than built, because the next person deserves to
know the count is still soft.

### Verification

Two pins, and the citadel half of the work is green. Full suite **2,261 passing**, 1 skipped.

## The flag is sound, and a dozen encounters are now positively verified

The previous entry stopped the Esoterrace alarm with two possibilities open: either its five once-only
bands fail for a reason peculiar to that pattern, or **`When.FirstTime` has never worked and a dozen
shipped encounters only look correct because something else fires first.**

**It is the first.** `FirstTimeFlagTests` pins the primitive two ways:

- a branch whose **only** guard is a flag fires once and never again;
- a branch with a **health guard in front of** the flag — the shape every shipped encounter uses —
  does the same.

And a probe on the surkana feeder itself, with an action that needs no prior target, reads **7, 7, 7**
across three blows. **The flag holds, on that npc, in that harness.**

So the dozen once-only branches in this log — the klaw sentinels, the drake calls, the tursin
loudmouths, Vingeveu's bands, Kalabar's, Masto's, the insurgents', the nunu's two, the ice claw
hunters' — are verified rather than assumed. That was worth an entry on its own.

### What is still unexplained

The Esoterrace ladder's own failure. With the feeder pinned at a hundred percent and its five bands in
place, the drakan's hate climbed ten per blow; with one bare-flag branch on the same npc, it does not.
**The difference is the five branches, and nothing in the primitive explains it.** Left open, narrowly:

1. Does `Evaluate` stop at the first matching branch, or run every branch whose guards pass? A
   five-band ladder and a one-branch pattern differ in exactly that.
2. If it runs them all, the ladder is not the only pattern in this log with several branches on one
   handler, and the others should be re-read.

**That is a much better question than the one this started with**, and it is the last thing between the
Esoterrace alarm and shipping: one npc, five bands, nineteen listeners, and a mechanic where beating a
facility object brings the facility in instalments.

### The rule, earned twice now

**Pin the primitive before diagnosing the encounter.** The shulack relay earned it; this entry spent it
and got a dozen encounters' worth of assurance for one small test file. **A primitive that a dozen
things depend on is worth pinning the first time you doubt it, not the second.**

### Verification

Two new pins. Full suite **2,263 passing**, 1 skipped.

## The backlog was ranking encounters that do not exist

`1001` sat near the top of the reachable list at **34 live callers and 21 live answerers**, with a
`hate` answer and Grand Chieftain Saendukal's name on it. It looked like the best hour's work on the
board. It is not an encounter at all.

Its callers are krall camps, dukaki runners, two unrelated slimes, an arena controller and a Reian
warrior. Its answerers are arena togs, lava floors and a surkana feeder. **They share a number and
nothing else.** The only pair that could actually talk is the arena's invisible controller and its four
red sand togs — one caller, and the row's other thirty-three are noise.

**`audit_silent_conversations.py` now groups both ends by encounter family** — the leading token of the
retail pattern name, which is the instance or the mob family — and ranks each number by *the largest
pair that share one*, not by the total. Numbers whose two ends never share a family are quarantined in
their own list as **cross-wired rather than silent**.

The re-ranked board:

- **`1001` collapses from 34/21 to 1/16** and leaves the top.
- **13 numbers move out of the buildable list entirely**, including `10` and `1002`.
- The reachable count falls from 34 to **31**, which is the honest figure.

### And it found the Esoterrace bug

The first version of the family rule kept **two** tokens for `ID`-prefixed instance patterns, on the
theory that an arena stage is its own encounter. That split the Esoterrace alarm down the middle —
`IDF4Re_FOBJ` calling and `IDF4Re_Drana` answering, reported as cross-wired — when they are one surkana
feeder and the lab that hears it. **The second token is a role inside the instance, not a separate
encounter.** One token, always.

Which is also the answer to two commits' worth of confusion about that alarm: **the pairing was never
verified.** The ladder was built from a caller and a set of listeners that the audit had put on one row
by adding unrelated families together, and no amount of pinning the flag primitive was going to explain
a mechanic assembled from two halves that were never checked to belong together. `When.FirstTime` was
sound the whole time, and so was `Evaluate` — it returns after the first matching branch, confirmed by
reading it. **The fault was in the row, not the runtime.**

`10000` now reads as what it is: **one surkana feeder, twenty esoterrace drakan, a `hate` answer**, and
a family that matches at both ends. That is the next thing to build, and this time the pairing is
checked.

### The rule

**A backlog row is a claim, and rows built by counting are the weakest claims on the board.** Two
entries were spent debugging an encounter whose existence the tool had asserted and nothing had tested.
The audit's job is to rank work; ranking work that cannot be done is worse than ranking none, because
it is indistinguishable from the real rows until an hour is gone.

Fifth blind spot found in this tool, and the first that was inventing rows rather than missing them.

### Verification

Full suite **2,263 passing**, 1 skipped — no server code changed. The audit's own output is the result:
85 numbers, 31 reachable, 13 quarantined.

## A flag on an NPC that never fights does not hold — measured

The Esoterrace alarm is **still not shipped**, and this entry is why. It is the third attempt, and the
first one that knows what is wrong.

### What retail actually writes

`IDF4Re_FOBJ_1`, the surkana feeder, read properly this time:

| priority | guard | flag | action |
|---|---|---|---|
| 10 | `is_hp_lower_than 20` | `ALPHA_1` | `broadcast_message 10000 range=30 param_obj=OBJI_ATTACKER` |
| 9 | `is_hp_lower_than 40` | `ALPHA_2` | same |
| 8 | `is_hp_lower_than 60` | `ALPHA_3` | same |
| 7 | `is_hp_lower_than 80` | `ALPHA_4` | same |
| 6 | **none** | `ALPHA_5` | same |

Twenty esoterrace drakan answer, across sixteen patterns, with one uniform branch:
`add_hate_point target=OBJI_MESSAGE_PARAM point_to_add=10` then `attack_most_hating`.

**The earlier attempt had the thresholds wrong** — it read 10/20/30/40/50/60 where retail writes
20/40/60/80/none — because it was built from a row the audit had assembled out of two unrelated
families. The previous entry fixed the audit; this one fixes the reading.

**And the lowest band has no health guard at all**, so the very first blow raises the lab and the four
thresholds below it raise it again as the object falls. That only works because evaluation is
priority-ordered and stops at the first match, which was confirmed by reading `Evaluate`.

### Why it still cannot ship

The pins failed, and the probe that chased them found something bigger than the encounter:

```
broadcast only ..................  10 20 30 40   state=IDLE
broadcast + one hate action .....  10 10 10 10   state=FIGHT
```

**Same branch. Same flag. Same broadcast.** The only difference is one extra action that puts hate on
the feeder and drags it into combat. **A `set_flag_var` on an NPC that never enters combat does not
hold.**

That is not a property of this encounter. It is a property of **every pure broadcaster in the retail
data** — a pattern whose answer to being hit is to shout and nothing else takes no hate, never reaches
`FIGHT`, and loses its flags between blows. Retail uses that shape for field objects and alarms
specifically, which is exactly the class of mechanic this log has been working through.

**The obvious fix is wrong.** `ResetPattern` runs from `HandleBackHome`, so the first guess was that a
hate-less NPC is sent home after every blow and cleared on the way. Guarding the reset on `inCombat`
changed nothing — `EnterCombat()` latches `inCombat` on the attack itself, so the guard is already true
by the time back-home runs. **The change was reverted rather than kept as a plausible-looking no-op.**

So the question is narrower and still open: **what clears — or fails to set — a flag on an NPC in
`IDLE`?** `TestAndSetFlag` returning true four times running is indistinguishable from the flag being
cleared four times, and the next probe has to tell those apart before anything is changed.

### Two smaller things the probe found

- **The surkana feeder has fifty maximum HP.** `BossAiHarness.SetExactPercent` cannot express 75% of it
  and throws its own assertion — `Expected: 75, Actual: 100`. Several of the failures in the third
  attempt were that, not the ladder. **A harness helper that fails is not a measurement, and its
  failure looks exactly like the encounter's.**
- **`on_die` is not translatable:** `set_condition_spawn_variable condition_type=2` drives the
  instance's spawn progression and has no equivalent here, and the system message with it needs
  `display_system_message`. The feeder's `on_message` answer to `1001` — a `despawn_self` — has a
  caller in a different instance entirely, which is one of the thirteen cross-wired numbers.

### Verification

Nothing shipped. The encounter, its pins and the engine change were all reverted; the measurement is
the deliverable. Full suite **2,263 passing**, 1 skipped.

## Pure broadcasters could not keep a flag, and now they can

The previous entry left one question: **what clears — or fails to set — a flag on an NPC in `IDLE`?**
It is answered, the engine is fixed, and the Esoterrace alarm ships.

### Telling the two apart

`TestAndSetFlag` returning true four times running is indistinguishable from the flag being cleared four
times, because **every way into the flags was a test-and-something** — a probe that asked whether a flag
was set changed the answer by asking. `PatternAi.IsFlagSet` now reads one without touching it. Same
rationale as `FleeingTo`: the state was always there and only the reading of it was missing.

The probe then read, on the surkana feeder with one flag-guarded broadcast branch:

```
start=False  after1=True  after2=True  after3=True     hate 10 -> 20 -> 30
```

**The flag was set the whole time and the listener still answered three times.** That looked impossible
until the listener's payload was moved from ten to seven and the hate came back `7 14 21`, tracking the
payload exactly. So the message really was sent three times — and the flag reads *set* between blows
**because the branch had just set it again**. The clear happens inside the next event, before the
branch is evaluated. That is why this took three attempts.

### The cause, and the fix that does not work

`ResetPattern` runs from `HandleBackHome`. A pure broadcaster answers a blow with nothing but a
`broadcast_message`, so it takes no hate, never reaches `FIGHT`, and is sent straight home — clearing
its flags — after every single blow.

**Guarding the reset on `inCombat` does nothing**, which the previous entry recorded and reverted:
`EnterCombat()` latches on the attack event itself, so the guard is already true. The question is not
whether the NPC was attacked but **whether it ever hated anything**, and an empty aggro list answers it.
`HandleBackHome` now resets only when the owner's aggro list has an entry. Measured after: `7 7 7`.

**This is not one encounter's bug.** Retail uses the pure-broadcaster shape for field objects and alarms
as a class, and every one of them was losing its flags. Any once-only branch on an NPC that answers with
a shout and nothing else was firing every time.

### The encounter

`10000` off the reachable list — **the surkana feeder and the twenty esoterrace drakan that hear it**,
across sixteen answering patterns plus the senior researcher and the lab supervisor.

| priority | guard | flag | action |
|---|---|---|---|
| 10 | below 20% | 1 | `broadcast 10000 range=30 param_obj=OBJI_ATTACKER` |
| 9 | below 40% | 2 | same |
| 8 | below 60% | 3 | same |
| 7 | below 80% | 4 | same |
| 6 | **none** | 5 | same |

The answer is uniform across all sixteen: **ten points on whoever the feeder named, then
`attack_most_hating`.** Ten is nothing on its own; twenty of them at once is the mechanic. The spell
ladder shares the same five flags, so a caster cannot spend a band the melee already spent.

**The lowest band has no health guard**, so touching the feeder at all raises the lab and the four
thresholds raise it again as the object falls. That works only because evaluation is priority-ordered
and stops at the first match.

### Not built

- **`on_die`:** `set_condition_spawn_variable condition_type=2` drives the instance's spawn progression
  and has no equivalent here; the `display_system_message` beside it needs `STR_MSG_IDF4Re_Drana_08`.
- **The feeder's `on_message` answer to `1001`**, a `despawn_self` whose live callers are all in other
  instances — one of the thirteen cross-wired numbers the audit now quarantines.

### The mutation that cannot be caught

Seven mutations, **six caught**. The seventh — promoting the bare band above the four thresholds — is
**not observable through this encounter and no pin was invented for it**. All five bands carry the same
payload and the same message, so any ordering still produces five calls over a full descent; the
priority only becomes visible if the bands differ, and here they do not. **Recorded rather than
papered over.**

### Verification

Eight pins, a seven-mutation sweep with the one exception above stated, and 21 npcs repointed. Full
suite **2,022 passing**, 1 skipped.

## Correction: there was no pure-broadcaster bug

**The previous entry is wrong, and the engine change it shipped has been reverted.**

It claimed that a `set_flag_var` on an NPC that never enters combat cannot hold, that this broke every
pure broadcaster in the retail data as a class, and that `HandleBackHome` had to stop resetting when the
owner's aggro list is empty. The measurements behind it were real. **The conclusion drawn from them was
not.**

### What the measurements actually showed

The probe drove the feeder with bare `OnCreatureEvent(AiEventType.Attack, …)`. **That event carries no
damage and no hate.** A real blow goes through `AddDamage`, which puts the attacker on the NPC's aggro
list and takes it into `FIGHT` — which is precisely what `BossAiHarness.Engage` reproduces, and why
`FirstTimeFlagTests` passed on the same NPC all along.

So the comparison that looked like the smoking gun —

```
broadcast only ..................  7 14 21   state=IDLE
broadcast + one hate action .....  7  7  7   state=FIGHT
```

— was **two different harness paths, not two different production behaviours.** The second action was
standing in for the damage the first path never delivered. Driven the way the server drives it, the
feeder's ladder was correct before any engine change: eight pins, green, with `PatternAi` untouched.

### Why the fix was also unsafe

Guarding the reset on a non-empty aggro list would have **stopped patterns resetting after real
fights**, because `AttackEventHandler.OnFinishAttack` calls `LoseAggro`, which empties the list *before*
the NPC goes home. The guard would have been true exactly when it should have been false. It was caught
by a pin written to check the fix did not break the ordinary case — `ButARealFightStillResetsOnTheWayHome`
— which failed on its first run.

**Both halves of that are worth keeping.** A pin on the case a fix is *not* about is what caught this;
without it the revert would have been a second wrong turn instead of the first right one.

### What survives

- **`PatternAi.IsFlagSet`.** Reading a flag used to mean test-and-setting it, so a probe changed the
  answer by asking. That was a genuine gap and the reader stays.
- **The encounter.** `10000` ships unchanged — the surkana feeder's five bands and the twenty esoterrace
  drakan that answer them — now pinned through `Engage` rather than through bare events.
- **The harness lesson**, which is the real finding: **an `Attack` event is not a blow.** It skips
  damage, aggro and the combat-state transition, and for any NPC whose own pattern adds no hate that
  difference decides whether its flags survive to the next event. Every pin in this log that drives a
  no-hate pattern with bare events is measuring the harness. The Esoterrace pins now say so in place.

### The rule

**Three commits went into an engine bug that was a test artifact.** The tell was there from the first
entry and misread twice: `FirstTimeFlagTests` passed on this very NPC, and the difference between the
passing probe and the failing one was never the flag — it was `HateAttacker`, sitting in plain sight in
the passing probe and read as incidental.

**When a new probe contradicts a passing pin, the probe is the suspect.** The pin has already survived a
suite run; the probe was written five minutes ago to prove a hypothesis, which is the worst provenance a
measurement can have.

### Verification

Engine reverted to `d680dddf5`'s `PatternAi` apart from `IsFlagSet`. Full suite **2,022 passing**,
1 skipped.

## The kaidan casters: one cry asking for two different things

`1005` off the reachable list — **the kaidan shamans, chieftains and soothsayers of Beshmundir and the
smackstoppers they call, 49 npcs.**

A hurt kaidan caster shouts **twice in the same breath**: `1004` naming *itself* and `1005` naming
*whoever it is fighting*. One asks for a heal, the other asks for a kill, and the camp splits its answer
between them. **Only the second half lands here** — the `1004` answer is a heal cast on the caller and
needs a skill index — but the `1004` call is still sent, so a listener costs nothing the day skills
arrive.

| pattern | live | band | what it does |
|---|---|---|---|
| `NKrall_WeA` — kaidan shaman | 13 | **41–75** | calls once, on a 9s clock that then runs at 6 |
| `NKrall_WeB` — kaidan chieftain | 14 | **36–75** | the widest band in the camp |
| `NKrall_WeC` — crack kaidan soothsayer | 8 | **46–75** | the narrowest, **and the cry stops its own clock** |
| `NKrall_KeC` — kaidan smackstopper | 14 | — | switches to the named target with **100** behind it, once |

### The call is a band, not a threshold

Retail guards it with `is_hp_in_boundary`, not `is_hp_lower_than`. **A caster burned straight past the
bottom of its band never calls at all** — a burst that takes a shaman from full to a fifth in one go
silences it, where a slower fight brings the smackstoppers. Three different bands across three patterns
standing side by side in the same camp means a raid at forty percent has the chieftains shouting and the
soothsayers already quiet.

### The soothsayer's cry kills its own clock

`NKrall_WeC`'s call branch is the only one on `BTIMERI_INDEX_0` that **does not re-arm it**, and branches
are first-match-wins. So the tick that carries the cry is the last tick that timer ever gets: the
soothsayer calls once and then runs on its other clocks only. Reproduced exactly, and pinned through the
switch timer so a dead timer zero cannot be mistaken for a dead pattern.

### `HateMessageTarget` conflates two retail ops — and it is not only this encounter

The `1005` answer is `switch_target target=OBJI_MESSAGE_PARAM points_to_add=100`. Writing that as
`Do.HateMessageTarget(100)` followed by `Do.TargetMessageParam()` **looks like two steps and is one**:
`PatternAi.HateMessageTarget` already calls `SetTarget`. The second action was removed as a no-op.

**But that cuts the other way.** Retail has both `add_hate_point target=OBJI_MESSAGE_PARAM` — which adds
hate and *leaves the target alone* — and `switch_target`, which moves it. **This port has only the
switching form.** Every branch in this log that translated a plain `add_hate_point` on a message
parameter is therefore switching a target retail would have left where it was:

- the klaw gatherers and spies (`Do.HateMessageTarget` twice, modelling point-then-switch);
- the gray mane stalkers and the kuriuta;
- the esoterrace drakan, where the `attack_most_hating` that follows happens to correct it;
- and any future answerer written the same way.

**None of those is wrong about the hate; all of them may be wrong about the facing.** The fix is a
non-switching variant and a pass over the answering branches to sort which retail op each one is. It is
recorded here rather than done now because sorting them means re-reading sixteen patterns, and doing
half of it would leave the log less trustworthy than doing none.

### Not built

- **`NKrall_PeA`, the kaidan healers — 9 live npcs, and the largest single group on this number.** Their
  `1004`/`1005` call sits on a branch guarded by `is_skill_count_left`, which this port cannot read.
  Building it without that guard would make them call on **every** tick below 35 percent instead of
  while a particular skill has charges left. Left out rather than approximated.
- The `1004` answer, a heal — a skill index. The flag slot is reserved so the two answers stay
  independent when it lands.
- `1399`, broadcast by every caster on entering the fight: no live listener anywhere on this server.
- `percent_to_add=10` on the smackstopper's switch and on the soothsayer's, which this port has no
  equivalent for. Recorded on every switch in this log.
- Every `use_skill`, and the `say_to_all` lines.
- `is_user_class` on the chieftain's four `on_attacked`/`on_spelled` bands.

### Verification

Fourteen pins and an **eight-mutation sweep, all caught** — but two of them only after the pins were
strengthened. "Callers lose their flag" passed at first because **the answerer's flag hides it**: a
smackstopper that has answered will not answer again whatever the caller does, so one listener cannot
tell a caller that cries once from one that cries every six seconds. It took a listener arriving *after*
the first cry. **Third time in this log that two guards protected one observable**, after the Tiamat
insurgents and the nunu farmers.

The support-aggro drift bit for the fourth time — every assertion here is a band rather than an equality
for that reason. Full suite **2,036 passing**, 1 skipped.

## Two thirds of the calls in the game are answered wrongly

The previous entry noticed that `Do.HateMessageTarget` sets a target, and wondered what that costs
elsewhere. **It costs more than expected, and the size of it is now measured.**

Retail answers a `broadcast_message` two ways:

| op | what it does | branches in 5.8 |
|---|---|---|
| `add_hate_point target=OBJI_MESSAGE_PARAM` | note the call, **keep fighting whoever you were** | **700** |
| `switch_target target=OBJI_MESSAGE_PARAM` | drop it and go | 349 |
| both, in one branch | — | 54 |

**This port has only the switching form.** Two out of every three answers in the game turn an NPC that
retail leaves facing where it was.

`PatternAi.AddHateToMessageTarget` and `Do.HateMessageParam` are the missing half, and they ship here.
**Nothing else does** — see below.

### Which of our classes are wrong

`tools/client-extract/audit_message_answers.py` reads the retail pattern names each AI class documents,
looks up what those patterns' `on_message` branches actually do, and reports a verdict per class. Of
**83 uses across 41 classes**:

- **14 classes: `add`** — every pattern they name only adds hate. These are wrong today.
  `ShulackMercenaryAI` (10 uses), `KlawPackAI` (3), `GuardianVingeveuAI`, `OphidanBridgeCallAI` (2 each),
  and ten with one apiece.
- **19 classes: `mixed`** — the class covers patterns that disagree, so each branch needs reading.
- **4 classes: `unknown`** — no retail pattern name in the file to check against.
- **4 classes: `switch`** — correct as written, including the kaidan smackstoppers from the last entry.

### Why the fix is not in this commit

**It was applied, and reverted.** Changing the 14 unambiguous classes turned four pins red, and the four
failures were not the mechanical kind:

1. **Three of them were asserting the bug.** `Assert.Same(player, npc.GetTarget())` was pinning *our*
   forced target as if it were the mechanic. Those pins pass today because the answer turns the NPC, and
   the turn is the part retail does not do.

2. **`NagaSummonerAI` and `MiddleBossFireAI` turned out not to add hate at all.** With the switch gone
   the answer does nothing whatsoever: `AggroList.IsAware` refuses hate aimed at a creature the owner is
   not hostile to, and the faithful subordinates are tribe **`NNAGA`**, which is not hostile to a player
   race. **The forced target was the only thing making those encounters look alive.** Fifth time the
   tribe check has decided a result in this log, and the first time it has hidden behind another bug.

3. **`OphidanBridgeCallAI`'s chain does not chain.** Its pin claims a call hops from a listener to a
   listener beyond the caller's reach, and that worked only because a forced target puts an NPC in
   combat at once, which fires its own entry branch and its own call. With `add_hate_point` the middle
   listener takes the hate and never acts on it — at eight seconds of ticks or at none. **The missing
   step is "has hate" → "is fighting"**, which this port does not take by itself here.

Each of those needs its encounter re-derived, not its assertion patched. Fixing 14 classes properly
means re-reading their patterns, re-picking npcs whose tribe can actually take hate, and deciding what a
chained call should do without a forced target. **Doing a third of that and leaving the rest would make
this log less trustworthy than doing none**, which is the same call the pure-broadcaster revert made two
entries ago.

### What is left to do, precisely

1. Add the non-switching action to the **14 `add` classes**, and re-derive each pin that asserted a
   target rather than hate.
2. Resolve the **19 `mixed`** classes branch by branch with the audit's per-pattern verdicts.
3. Name a retail pattern in the **4 `unknown`** files so they can be checked at all.
4. **Fix the `NNAGA` answerers** — an encounter whose answer is dropped by `IsAware` is not shipped, it
   only looks shipped.
5. **Build the hate-to-combat step**, without which every chained call in the retail data stops at its
   first listener.

### Verification

Engine addition and audit tool only. Full suite **2,036 passing**, 1 skipped, with the 14-class change
reverted.

## Fourteen answers corrected, and the step they rest on pinned

The previous entry measured the damage and reverted the fix, because four pins went red and none of the
failures was the mechanical kind. **The blocker was one unverified claim**, and pinning it turned three
of the four failures into things with names.

### Hate alone brings an NPC into the fight

`AggroList.AddHate` ends in `CreatureController.OnAddHate`, which raises an `Attack` event on the owner.
So an NPC given hate by somebody else's call is meant to join the fight without ever being targeted —
which is what every chained call in the retail data depends on, since it is *entering combat* that fires
the branch that calls in turn.

**That had never been pinned.** `CallChainTests` pins it now, on throwaway patterns using the faithful
`Do.HateMessageParam`: a caller shouts, a relay answers with hate and nothing else, the relay's own
entry branch shouts again, and a third NPC out of the caller's reach hears it. **The chain carries.**
A second pin shows the relay keeps facing whoever it was already fighting while all this happens.

**So the engine step is not missing** — the previous entry's fifth open item was wrong, and is withdrawn.

### The fix, this time kept

`Do.HateMessageParam` applied to the **14 classes the audit calls unambiguous**. Three pins had to be
restated, and each restatement is a finding rather than a patch:

1. **`OphidanBridgeCallAI` — the chain stops at the first listener.** The middle listener takes the call
   and joins the fight; its onward cry does not reach the far one. Whether that is the reach, a guard on
   its entry branch, or the moment its current target is set **has not been established**. The pin
   asserts zero so it goes red when it is, rather than claiming a chain that no longer happens.

2. **`NagaSummonerAI` and `MiddleBossFireAI` — the answer does nothing at all.** `IsAware` refuses hate
   aimed at a creature the owner is not hostile to, and the faithful subordinates are tribe **`NNAGA`**,
   which is not hostile to a player race. No hate, so no entry into combat, so nothing. **The forced
   target was the only thing that ever made these two look alive**, and it bypassed the aggro list
   entirely. Sixth time the tribe check has decided a result here, and the first time it was hiding
   under another bug.

Both pins now assert zero and null with the cause written beside them, so they turn red the day the
tribe is sorted out.

### Still to do

- **The 19 `mixed` classes**, branch by branch, using the audit's per-pattern verdicts.
- **The 4 `unknown` classes** — name a retail pattern in each file so it can be checked at all.
- **The `NNAGA` answerers.** An encounter whose answer `IsAware` drops is not shipped, it only looks
  shipped. This needs the tribe relations checked against retail rather than an npc swapped in a pin.
- **The Ophidan chain's second hop**, now narrowed to three candidates.

### The rule

**A wide change blocked by one unverified claim is a pin away from being safe.** The previous entry
reverted 14 correct edits because three pins disagreed, and all three disagreements came from the same
unpinned property. Pinning it cost one small test file and turned "revert and document" into "ship and
document what is left".

That is not an argument against the revert — the revert was right *at the time*, with the property
unknown. It is an argument for asking, when a change is blocked, **which single claim is doing the
blocking.**

### Verification

Two new engine pins, 14 classes corrected, three encounter pins restated with their causes. Full suite
**2,038 passing**, 1 skipped.

## Correction: the naga answers are not blocked by their tribe

**The previous entry blamed `NNAGA`, and that was wrong.** It claimed `AggroList.IsAware` refuses hate
aimed at a creature the owner is not hostile to, that the faithful subordinates are a tribe not hostile
to a player race, and that two shipped encounters therefore do nothing at all. The symptom was real. The
cause was invented, and it is corrected here.

### What the data actually says

`NNAGA` in `tribe_relations.xml` reads `<aggro>PC GUARD PC_DARK GUARD_DARK</aggro>`, and **our file
matches the Java reference byte for byte**. `AggroList.IsAware` and `Player.isEnemyFrom(Npc)` are both
faithful ports — checked line by line against the Java. There was no divergence to find.

Measured on the actual pair:

```
knows=True  enemy=True  tribe=NNAGA  type=AGGRESSIVE  hateAfterAdd=5
```

**The naga takes hate perfectly well.** Every step of the chain the previous entry accused was working.

### What is actually happening

The same probe inside the encounter's own arrangement:

| witness distance from the named player | result |
|---|---|
| **70 metres** (the pin's layout) | `hate=0  target=null` |
| **2 metres** | `hate=11  target=set` |

**An NPC handed hate for somebody it cannot reach gives up and clears its aggro list.** The order lands,
the witness joins, it finds nothing within reach, and `LoseAggro` empties the list before anything can
observe it. Nothing to do with tribes.

And on a live server the witness would **run at the player** — retail's fifty-metre order exists
precisely to pull distant subordinates in. **This harness does not move NPCs**, so the witness can never
close the gap and always gives up. So this is a third harness artifact, after the pure-broadcaster
events and the forced-target chain.

The pins now assert zero with that cause written beside them, and they go red the day movement is
simulated — which is the correct trigger, unlike the tribe check, which would never have fired.

### Withdrawn

- **"Fix the `NNAGA` answerers"** as an open item. There is nothing wrong with them.
- **"An encounter whose answer `IsAware` drops is not shipped, it only looks shipped."** True as a
  sentence, false about these two.

### What replaces it

**The harness cannot observe any call whose listener has to travel.** That is a much larger caveat than
one tribe, and it touches every pin in this log where a listener stands outside its own aggro range of
the named player — which is the *normal* shape for the long-range orders in the retail data, since a
fifty-metre broadcast exists to reach NPCs that are not already in the fight.

Recorded rather than fixed, and it wants a decision rather than a patch: either the harness gains enough
of the move controller to close a gap, or these pins are written with the listener already in reach and
say so. **The second is cheaper and weaker**; the first is the only one that would have caught this
without a probe.

### The rule, for the third time this session

**A mechanism that explains the symptom is not the same as the mechanism causing it.** The tribe check
was a real thing that really does drop hate — it had bitten five times in this log — and it fit the
evidence. It was also not what was happening. Five prior sightings made it the first thing reached for
and the last thing checked.

The check that settled it took one probe and four lines: ask the pair directly whether hate lands.
**That probe should have been written before the explanation was committed, not after.**

### Verification

Two test files corrected, one wrong claim withdrawn from this log. Full suite **2,038 passing**,
1 skipped.

## The mixed answers, resolved by message number

Nineteen classes were parked as **`mixed`** — the audit could not say whether their answers should
switch a target or only note the call. Most of that was the audit's fault, and the rest is now fixed.

### Two wrong ways to ask the question

**First version keyed on the pattern.** It called almost everything mixed, because a single retail
pattern routinely answers several numbers in different ways: `Gab1_Gaurd_An` obeys one call and merely
notes another. The union over a pattern says nothing about the branch we wrote.

**Second version fell back to every pattern on the number** when a file's own patterns had nothing to
say. That reported almost everything mixed again, for the opposite reason: a message number is reused
across unrelated encounters, so the union over the game is noise. **When a file names no pattern that
answers its number, the honest verdict is `absent`** — the file has not documented where its branch came
from — and that is what it reports now.

Keyed on `(pattern, message number)` and scoped to the patterns each class documents, the board reads
clean.

### `add/switch` on one number is not ambiguity

It is **retail's two-action idiom**, and reading one branch settled it:

```
? is_message message_type=1007
> add_hate_point  target=OBJI_MESSAGE_PARAM point_to_add=1
> use_skill       target=OBJI_MESSAGE_PARAM skill=SKILLI_INDEX_0
> switch_target   target=OBJI_MESSAGE_PARAM points_to_add=100
```

**A point first, then the switch with the hundred behind it.** This log has been calling that pair
"Glance then Commit" since the klaw pack, and translating both halves as `Do.HateMessageTarget` — so the
first one switched too.

**That one is cosmetic**: the second action switches anyway, so the end state was already right. Seven
such pairs are now written as `Do.HateMessageParam` then `Do.HateMessageTarget`, and the suite did not
move, which is the expected result and the reason it was safe to do mechanically.

### Three that were not cosmetic

| class | number | retail | was |
|---|---|---|---|
| `LepharistBastionAI` | `1017` whisper | `add_hate_point` | switching |
| `PanesterraGuardAI` | `41100` guard call | `add_hate_point` | switching |
| `DarkbladeOvanukaAI` | `22251` base alarm | `add_hate_point` | switching (3 branches) |

**A captain is obeyed and a peer is only noted.** Panesterra answers `41101` with `switch_target` and
`41100` with `add_hate_point`, so a guard already busy keeps its own quarry when a guard calls and drops
it when its captain does. The whole point of two numbers, and this port had them doing the same thing.

The same shape in Lepharist: **the whisper is noted and the shout is obeyed.**

**Nothing in the suite caught any of this** — the change is invisible while the answerer is idle, and
every pin in those files had it idle. `ACaptainIsObeyedAndAGuardIsOnlyNoted` gives the answerer a fight
of its own first, and it fails if the guard answer switches again.

### What is left

- **Twelve classes still `mixed`**, all of them the two-action idiom on a single `Do.HateMessageTarget`
  where the branch needs reading to say whether our one call is standing for the point or the switch:
  `FortressGuardCallAI`, `KerubielCampAI`, `RatmanCampAI`, `TursinLoudmouthAI`, `BlackClawLycanAI`,
  `BrigadeGeneralAnuhartAI`, `NochsanaNagaWizardAI`, `PetDrakeCallAI`, `AnuhartCasterAI`,
  `DrakeMarkAI`, `LichSoulCallAI`, `StoneskinStoffuAI`, `TrainedBeastAI`, `VanukaLizardAI`.
  **These are low-risk** — a single call standing for the pair lands in the same place either way — but
  they are not verified.
- **Six classes `absent`**: `AnuhartGuardAI`, `HeironWatcherAI`, `SilikorGuardAI`, `AbyssGuardCallAI`,
  `DefencePostFlagAI`, `IllusionOfMelancholyAI`. Each needs the retail pattern its answer came from
  named in the file before anything can be checked. **That is a documentation gap, not a code one**, and
  it is the cheapest item on this list.
- The Ophidan chain's second hop, unchanged from the previous entry.

### Verification

Seven cosmetic pairs, five behavioural branches, one new pin with its mutation checked. Full suite
**2,039 passing**, 1 skipped.

## The six undocumented answers, and two inventions found under them

The `absent` list was described last entry as "a documentation gap, not a code one, and the cheapest
item on the list." **It was neither.** Naming the retail pattern behind each answer turned up two
branches this port made up.

### What each of the six answers

| class | number | retail answerers | what they do |
|---|---|---|---|
| `DefencePostFlagAI` | `21212` | `IDF5_U1_War_Vri_Def01_Re_Fi_65_Ae` + 6 siblings | **`add_hate_point`, all seven** |
| `AnuhartGuardAI` | `6821` | 8 `Lizardman_*_IDLF1` | the two-action idiom |
| `HeironWatcherAI`, `BrigadeGeneralAnuhartAI`, `AnuhartCasterAI` | `3406` | `XD_EPet` | the two-action idiom |
| `BrigadeGeneralAnuhartAI`, `HeironWatcherAI` | `6833` | `LastBoss_Su` | the two-action idiom |
| `SilikorGuardAI` | `6655`, `6656` | `ND2_WhG1`, `ND2_WhG2` | **`use_skill` and nothing else** |
| `IllusionOfMelancholyAI` | `6915` | `IDTP_Fanatic_Elementalearth2` | **`attack_most_hating` and nothing else** |

`21212` is unambiguous and now uses `Do.HateMessageParam`: **all seven answerers note the post and
finish what they are doing.** A guard already engaged does not drop its fight because a flag went up.

### The illusion was never told who to attack

`6915`'s only listener answers with a bare `attack_most_hating`. Our branch was
`Do.HateMessageTarget(0)` — a zero-point hate on the message parameter — with a remark that reasoned:

> "attack_most_hating on a freshly-placed illusion with an empty aggro list means the one it was just
> named, and a zero-point entry is how our aggro list says that."

**It does not.** An empty aggro list has no most-hated, and the translation manufactured a target retail
never gives. The pin agreed with the code, down to its name —
`AnIllusionToldToGoGoesForTheOneItWasToldAbout` — so the invention was pinned as if it were the
mechanic. The branch is now `Do.SwitchTarget(MOST_HATED)`, and the pin says what actually happens: **the
call names nobody, and an illusion with nothing to hate has nowhere to go.**

**Not pinned:** that it goes for its own most hated rather than the one named. The illusion despawns
itself on any blow or damaging spell — that is its entire mechanic — so it cannot be put in a fight, and
a bare `AddHate` on an idle one is refused. **The claim is real, the action is retail's, and there is no
arrangement in this harness that shows it.** Recorded rather than approximated with a pin that would
pass for a different reason.

### And the silikor guards answer with hate retail never gives

`6655` and `6656` have four and two listeners respectively, and **every one of them answers with a
single `use_skill`.** No hate action anywhere on the number. Our point is standing in for a skill this
port cannot cast — it is the only way the order has any effect here, so it stays, but it is now labelled
in the source as **ours rather than retail's**, and it should *become* the skill rather than survive
beside it.

**That distinction matters more than it looks.** Everything else in this log marked "not translated" is
absent. This is the first case found of the opposite: a branch that does something retail does not,
which no audit of missing pieces would ever surface.

### Still to do

- **Fourteen classes on the two-action idiom**, where a single `Do.HateMessageTarget` needs reading to
  say whether it stands for the point or the switch. Low risk — a single call lands in the same place
  either way — and now the retail pattern behind each is named, so it is a reading job rather than a
  hunt.
- **The silikor skill**, and every other answer that is really a skill.
- **The illusion's most-hated claim**, which needs a harness that can hold an illusion in a fight.
- The Ophidan chain's second hop.

### The rule

**An audit of what is missing cannot find what is invented.** Every tool in `tools/client-extract` asks
some version of "what does retail have that we do not". Nothing asks the reverse, and the reverse is
where a pin agreeing with its own bug hides. The illusion's remark reasoned carefully from a false
premise for as long as nobody read the pattern it claimed to translate.

### Verification

Three branches corrected, two pins restated, one claim withdrawn as unstageable. Full suite
**2,039 passing**, 1 skipped.

## The audit that asks the other question

Every tool in `tools/client-extract` asks some version of **"what does retail have that we do not"** —
missing adds, missing patterns, dead shouts, silent conversations, unreachable skills. The previous entry
found two bugs that no version of that question can reach, both by accident, and wrote down why:
**an invented behaviour arrives with a pin that agrees with it.**

`audit_invented_actions.py` asks the reverse. For every `on_message` branch this port wrote, it compares
the *kinds* of action in our branch against the kinds in the retail branches answering the same number,
scoped to the patterns the class documents.

### What it found, and what it taught about itself

**First run: five "invented".** Four were the tool's fault. Our `Do.SwitchTarget` translates retail's
`switch_target_by_attacker_indicator` — pick a new target by indicator — and the mapping had filed that
retail op with `switch_target`, which names an object instead. Two ops with similar names and different
jobs, and every class using one looked like it had invented the other.

**Corrected: one.** Which is the right kind of number for this question — the tool exists to find rare
things, and a tool that reports five when one is real trains you to ignore it.

### The one that was real

`ShebanMysticalTyrhundAI` answers Teselik's self-destruct order at `22261`. Retail:

```
> spawn      SPAWN_ID_NONE  BIDVritra_Base_Suicide_Mon  at my point
> use_skill  target=OBJI_SELF  skill=SKILLI_INDEX_2
```

A spawn, then **a suicide skill — which kills the hand**, and a killed hand runs its `on_die`, whose one
branch tells the boss `HandDied`. Our `npc_skills` does not carry that skill, so the branch was written
as `Do.DespawnSelf()`.

**A despawn is not a death.** `HandleDespawned` does not evaluate `OnDie`, so a hand that blew itself up
never reported in, and the boss's live-hand count only ever came down for hands the players killed. The
encounter had two ways to lose a hand and only counted one of them.

The branch now sends `HandDied` itself before despawning, labelled in the source as **a substitute for
the skill rather than a translation of an action retail writes on that branch** — because that is what it
is, and the next reader deserves to know which of the two they are looking at. Pinned through a throwaway
listener that despawns on hearing the notice; the boss's own answer is a counter decrement that clamps at
zero and shows nothing until its hands have been counted up first. **The pin fails when the notice is
removed.**

### The `dropped` column, for contrast

82 answers drop a kind retail has. Almost all are `skill`, already recorded throughout this log, and
`attack` — retail's `attack_most_hating` following a hate action, where our hate action already sets the
target. **Neither is news, and that is the point**: the tool's value is entirely in the short column.

### Still to do

- **Extend it past `on_message`.** Message numbers give a reliable key between our branches and retail's;
  `on_attacked`, `on_battle_timer` and the rest do not, so 15 branches were skipped here and every
  non-message handler in the port is unchecked. **That is the large majority of what we have written.**
- **The 14 two-action-idiom classes**, unchanged: a reading job now that their patterns are named.
- **The silikor skill**, and the other answers that are really skills.
- **The illusion's most-hated claim**, which needs a harness that can hold an illusion in a fight.
- The Ophidan chain's second hop.

### The rule

**A tool that finds nothing is not the same as a tool that is not needed.** This one found a single bug
across 99 branches, and it is a bug that had a passing pin, a plausible remark, and no chance of turning
up in any other audit here. The first version's four false positives were worth fixing precisely so the
one true positive is legible.

### Verification

One branch corrected, one pin with its mutation checked, one new audit. Full suite **2,040 passing**,
1 skipped.
