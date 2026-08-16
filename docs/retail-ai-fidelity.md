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
