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

---

## Log

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
