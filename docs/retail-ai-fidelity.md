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

## Sweeps

`tools/client-extract` carries three audits that turn a hand-found bug into a
server-wide worklist. Each produces candidates for review, not auto-fixes.

| Audit | Finds | Current count |
|---|---|---|
| `audit_missing_adds.py` | Encounter adds that exist only as templates, nothing ever spawns them | 812 across 518 encounters (768 implementable, 44 waypoint-blocked) |
| `audit_dead_shouts.py` | NPCs left mute because their lines sit on a twin we never spawn | 0 remaining (was 197) |
| `audit_hp_phases.py` | Hand-written `HpPhases` thresholds that disagree with the retail pattern | 24 AI classes; 7 corrected, 2 judged correct as-is, ~8 are sequence-at-one-threshold, 7 regime-guarded |

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

### The sequence-at-one-threshold shape

The largest remaining group is not mis-numbered at all. Retail crosses **one**
threshold and then runs a flag-latched sequence off a repeating battle timer;
aionemu reproduced the sequence by inventing a *ladder* of thresholds. The
Flamelord is the clearest case: retail spawns its delivery NPCs in four steps
all at 25%, and we spawn the same four NPCs at 40/30/20/10. Engineer Lahulahu
(8 steps at 25), Empowered Agent (6 at 20), Unstable Triroan (7 at 20) and
Enraged Modor (3 at 50) are the same shape.

Matching these means driving a timed sequence from a single threshold rather
than editing numbers, and `HpPhases` cannot express it. That is a small
sub-system — an ordered, delayed step runner — and the work belongs with the
regime-guarded reimplementations rather than with the renumbering.

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
