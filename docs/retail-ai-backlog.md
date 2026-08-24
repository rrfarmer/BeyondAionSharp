# Retail AI — what is left

Standing list of open work on the retail (5.8) NPC AI port. Re-measured 2026-08-23.

`docs/retail-ai-fidelity.md` is the running log — why each decision was made, in order. It is 40,000
lines and is not a to-do list. **This file is the to-do list.** Keep it short; move detail to the log.

## First, a correction to the numbers

Earlier backlogs in the log carried figures like *"`control_door` (691); `enable_area` (575);
`random_move` (187)"*. **Those are usage counts across the whole retail dump, not work items.** Most of
those uses are in patterns no npc in this port runs, or in patterns already taken for other reasons.

Measured against what is actually blocked today, the same items are:

| carried in the log as | actually refused now |
|---|---|
| `control_door` 691 | **9** |
| `random_move` 187 | **29** |
| `switch_target_by_class_indicator` 53 | **9** |
| abnormal-state groups 108 | **9** (`is_in_abnormal_state`) |
| `enable_area` 575 | **1** |

Priorities set from the old numbers were wrong by an order of magnitude. What follows is from the
extractors' own tallies.

## Where coverage stands

| table | patterns | npcs |
|---|---|---|
| battle cycles | 3,956 | 30,187 |
| wake / idle | 1,572 | 3,949 |
| death spawns | 678 | 1,927 |
| guard answers | 4,242 answers | 3,696 |

## A. Best value: one vocabulary item, dozens of handlers

These are **partial** losses. The npc's rotation runs; one arming handler was dropped, so the fight is
subtly quieter than retail's. 711 handlers are dropped this way in the battle table alone.

| item | dropped | what it means in play |
|---|---|---|
| `is_npc_state NPC_STATE_WAKE_UP` | **69** (23 each on `on_attacked`, `on_see_npc`, `on_see_user`) | "only while still waking" — the port answers six npc states and not this one |
| `activate_skillarea` | **33** (25 battle + 8 wake) | turns a skill area on; ground effects and hazard zones |
| `goto_alias` | **14** | move to a named point rather than a route step |
| `is_race` (wake table) | **10** | see the note below — the count understates it badly |

**`is_race` is worth more than its row says.** The refusal count is 10; the two subjects the port cannot
answer are `OBJI_SELF` and `OBJI_FRIEND`, and measured by who runs them, `OBJI_FRIEND` is **28 patterns,
1,485 npcs, 217 of them spawned here**. `When.FriendRace` does not exist while `FriendHpBelow` and
`FriendInAbnormalState` do, and `ai.Friend` is already a `Creature` — so it is a two-line addition in an
established family. `OBJI_SELF` stays refused for the documented reason: an npc's own race is a
constant, so the branch is decided at build time, and the table shares one branch list between npcs of
different races.

**The two readers are aligned now.** The wake and idle tables used to import a guard reader that knew
2 condition kinds and an action reader that knew 10, against the battle reader's 20 and 22, purely
because of which module they import from. Both delegate now, minus the kinds that cannot travel —
anything carrying a skill index (retail resolves it against the owning npc's own ordered list, and this
pipeline has no such pass) or a spawn group (these tables have no group column).

**Yield was small and is worth stating honestly: +20 npcs, of which two handlers were new to the
runtime.** The value is that there is one vocabulary rather than two, so the next condition added
reaches every table.

**Since then, three things the engine could already do and nothing could ask it to.** `SANCTUARY` and
`DEFORM` were missing from the extractor's abnormal-state allow-list while the port's enum named both
— `SANCTUARY` is the most-used abnormal state in the whole dump — and the subjectless
`is_in_abnormal_state` form was unread. `When.CountBelow`, `When.CountAbove` and `When.Decrement` had
no loader token, so retail's compare-and-set counters refused their whole patterns. Net: **+18 battle
patterns, +21 npcs, and 17 fewer dropped arming handlers**. Worth checking the engine before assuming
a refusal means missing runtime.

## B. Whole patterns refused — the small tail

Each of these blocks the entire pattern, so the npc runs nothing from it.

`random_move` 29 · `switch_target_by_class_indicator` 9 · `control_door` 9 · `nothing arms the first
timer` 9 · `spawn_on_target` told to attack with no hate points 8 · `switch_target` at
`OBJI_CUR_TARGET` 7 · `is_in_abnormal_state` of `STUN_LIKE_GROUP` 4 · `despawn_by_nameid` 4 · a tail of
ones and twos.

`control_door` still needs one in-game observation to settle which `method` value opens versus closes;
everything else is ordinary work.

**Deliberately not taken, so nobody re-derives them:** the `*_GROUP` abnormal indicators
(`STUN_LIKE_GROUP`, `CANNOT_ACT_GROUP`, `MENTAL_GROUP`, `PHYSICAL_GROUP`) and `INVISIBLE`, because the
port has no name that means the same thing and "nearest" is a guess; `decrease_intvar` in its
pass-only-at-the-bound form, which is a different condition; and `sub_intvar`, whose **six uses in the
dump all set that flag**, so building it would be dead code that reads as coverage.

## C. Engine-level, and the theme worth a deliberate pass

**Marker NPCs collide with combat machinery built for bosses.** Three encounters hit this and it was
one underlying problem, not three. **All three are now closed.** The theme is kept because the shape
recurs, not because work is outstanding on it.

The shape worth carrying forward: this machinery assumes an NPC that fights, and a marker is an NPC
that merely *can be hit*. Being hit is enough to enter combat, and everything downstream then treats
the marker as a boss that has finished a fight.

1. **Marker clocks** — an npc that never fights had its `on_wake_up` timers cancelled on settling, and
   any survivor refused outside `FIGHT`. **Fixed, and now reviewed.** The change was recorded as broad
   and is not: counted end to end, **11 npcs** have a clock that newly runs, and **2 of them are
   spawned in this port** — 204805 in Beluslan and Kromede's Hierarch Stone (282091). The rest are
   event controllers this port never places: `BLDF4_Dramata_TimerTrigger`, `LF4/DF4_DramataTimer` and
   `DramataGC`, which chain a countdown of system messages and then spawn. Among hand-written classes
   exactly one arms a timer on waking, and it is Kingspin's web, which is pinned. Nothing else to walk
   through.
2. **`NagaSubordinateAI`** — a subordinate that never engaged is now dismissed by its fuse.
   **Correct, and now reviewed.** Both bosses are spawned in Heiron, so the encounter is live; a
   bystander subordinate is ordinary (his wave lands on whoever he is fighting, and that player dies or
   runs); and the boss despawns his whole summon group on both exits, so nothing can outlive the fight
   even if a fuse were lost. Pinned as `IncludingOneThatNeverFoughtAnybody`.
3. **Kingspin's web sweep** — **fixed.** The diagnosis in this file was wrong twice: what puts a web
   in combat is *being hit*, not aggro (`inCombat` is set only in `HandleAttack`), and the fix named
   here — "a web has no `Cycle` rungs" — pointed at a slot the engine does not have. The real bug was
   larger: a web clipped by a stray area skill lost the eight-second fuse that despawns it, so **it
   never left the room**. `ResetPattern` now resets what the fight created and leaves what the npc
   arrived with, recorded per slot at arming time. Retail cancels no timer anywhere. Cost: no pin
   changed, suite green. See the log entry *A fight's clocks end with the fight*.

**Still open from (3):** counters are cleared outright on going home, while timers and flags are now
cleared by attribution. No encounter has been found that needs the counter half, and a counter has six
write paths to the flag's one — so it was left rather than done speculatively. If an encounter turns up
where an npc's out-of-combat counter is wiped by a stray hit, the change is the same three lines.

## C-bis. Clusters, not vocabulary items

Some refusals look like one missing guard and are really a feature nobody has built. Recording them
here so the next pass does not spend a day on the guard and find nothing moves.

- **Abyss turret switches** (`Gab1_TurretSwitch_*`) — **721 npcs run these patterns and 172 are spawned
  here.** A player talks to the switch; the tribe on the *talker* selects which turret to mount them
  on; the branch then casts, sets a spawn condition variable, teleports the player onto the turret by
  alias, and despawns. Needs `teleport_target_alias`, `set_condition_spawn_variable`, and a player
  transform tribe this port has no source for. This is where the 28 `is_tribe` handlers went; they were
  carried in section A as "who may talk to it, by tribe", which was a guess and wrong.

## D. Encounters and hygiene

- **Kaidan's low-health rung** — needs the skill index list resolved for thirteen shaman npcs first;
  they may not agree the way the wave attackers' index 0 did (22 of 22).
- **The other 65 `on_see_user_move` patterns** — 105 npcs gained the handler; one encounter is pinned.
- **The guards' two broadcasts** (22696 on waking, 22658 on the second clock) — no listener found in
  this tree, so a pin would assert into silence. Check the listener side before assuming they are idle.
- **18 `--implemented` audit candidates** — `python tools/client-extract/audit_stale_claims.py
  --implemented`. Most are accurate past-tense history; the yield is in repeated claims.

## E. Boundaries, not backlog

Not work items — recorded so nobody re-derives them:

- **10,948 patterns refused for having no npc here free to run them** (6,899 battle + 4,049 wake).
  Either this port lacks the npc, or it is bound to a hand-written class.
- **876 patterns with no rotation and nothing sayable outside waking.**
- **220 npcs dropped from a pattern their skill list cannot answer** (197 + 23).
- **16 walker routes** named by retail spawns and absent from every world file and the client pak.
- **The runner encounter** (`BIDF5_R2_Runner`, 9 npcs) — no spawn rows and no routes in this port, so
  it cannot be driven here at all.

## How to work on this

```bash
python tools/client-extract/regen_check.py          # run the whole pipeline, verify it round-trips
dotnet test AionServer.slnx                         # ~55s
python scripts/parity/check_fidelity.py             # structural gate
pwsh scripts/ci/check-warning-baseline.ps1          # warning gate
```

Extractor refusal tallies are printed by each `extract_*.py` run and are the authority on what is
blocked. Re-measure before setting a priority — this file did, and the old one was wrong.
