# Retail AI — what is left

Standing list of open work on the retail (5.8) NPC AI port. Re-measured 2026-08-23.

`docs/retail-ai-fidelity.md` is the running log — why each decision was made, in order. It is 40,000
lines and is not a to-do list. **This file is the to-do list.** Keep it short; move detail to the log.

## Before anything else: the dump is 5.8 and this port is 4.8

**Most of what is "missing" is a version difference, and version differences are not work.** Retail's
5.8 files name npcs, skills, routes and whole mechanics that 4.8 does not have. The right response is
to record the boundary, never to add a 4.8 template or spawn so that a 5.8 pattern will fit.

The extractors already hold that line, and it is worth knowing how much of the backlog it accounts
for:

| | |
|---|---|
| patterns refused for *no npc here free to run it* | **6,899** — the version gap plus hand-written classes |
| npcs the battle table drives | 30,197, and **every one has a 4.8 template** |
| skill categories retail names / kept here | 2,052 / **1,977** (75 name a skill 4.8 lacks) |
| walker routes retail names and 4.8 has not | 16 |

So the tables carry nothing 4.8 cannot run. When a refusal turns out to be "5.8 has this and 4.8 does
not", it belongs in section E and stops being a to-do.

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
| battle cycles | 3,968 | 30,227 |
| wake / idle | 1,572 | 3,949 |
| death spawns | 678 | 1,927 |
| guard answers | 4,242 answers | 3,696 |

## A. Dropped handlers — and this section is nearly empty now

These are **partial** losses. The npc's rotation runs; one arming handler was dropped, so the fight is
subtly quieter than retail's.

**Worth saying plainly: what is left here is not work.** Every tractable item in this section has been
taken. The three below are a group taxonomy 4.8 has no exact name for, a suppression rung that needs an
invented wake duration, and a constant. The two that *looked* like the biggest — `activate_skillarea`
and `goto_alias` — turned out to be 4.8-versus-5.8 boundaries and moved to section E.

If you are looking for the next real thing, it is in section B or D, not here.

**Read the tally by npcs, not by patterns.** `extract_battle_cycles.py` now prints both and ranks by
npcs, because the pattern count has mis-set priorities here twice: `control_door` was carried as 691
and is 9, and `is_race` about a friend was carried as 10 and was worth 217 spawned npcs. Six patterns
is nothing; 266 npcs is not, and those were the same row.

Ranked by npcs affected, which is what the extractor now prints:

| item | npcs | what it means in play |
|---|---:|---|
| `is_obj_in_abnormal_state PHYSICAL_GROUP` on `on_friend_spelled` | 163 | **not work** — retail's group taxonomy has no exact name here, and picking a "nearest" is inventing it |
| `is_npc_state NPC_STATE_WAKE_UP` on `on_attacked` / `on_see_npc` / `on_see_user` | 53 each | "only while still waking"; every use is a `do_nothing` suppression rung, so taking it needs a wake duration this port would have to invent |
| `is_race` about `OBJI_SELF` | 10 patterns | **not work** — see section E |

**`is_race` about a friend is done, and it is the clearest lesson in this file about reading the
tally.** The refusal count said 10; the honest number was **217 spawned npcs**, because the condition
was costing *handlers inside patterns that were taken anyway* rather than blocking patterns whole.
Building `When.FriendRace` moved patterns and npcs **not at all** and moved actions **319,483 ->
326,569**. Read the tally as "patterns blocked", never as "how much is missing".

The remaining 10 are `OBJI_SELF` and are **not work**: an npc's own race is a constant, so the branch
is decided at build time, and one branch list is shared between npcs of different races. Moved to
section E.

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

`switch_target_by_class_indicator` 9 · `control_door` 9 · `nothing arms the first timer` 9 ·
`spawn_on_target` told to attack with no hate points 8 · `is_in_abnormal_state` of `STUN_LIKE_GROUP` 4 ·
`despawn_by_nameid` 4 · a tail of ones and twos.

**`random_move` is out of this list and into section E**, measured. 4.8 has random walking, but retail
gives a duration with no range while 4.8 takes the range from the *spawn row*. Of the 135 npcs spawned
here that would run it, **112 have `random_walk="0"` on every spawn row** — this port's data says they
do not wander — and all **23** that do have a range use `random_move` inside a combat handler, where
`StartRandomWalking` would take them out of the fighting state. Neither half composes.

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

- **`percent_to_add` on `switch_target`** — every one of the 1,321 uses carries one, and it is
  deliberately unmodelled: the element does not say what the percentage is *of*, and a guess puts a
  silent wrong number into a hate list. Answering it needs an observation, not a decision.
- **`HateEventTarget` is the only member of its family that does not also set the target.** Harmless
  today; worth knowing before someone routes a new subject through it.

- **Kaidan's low-health rung** — needs the skill index list resolved for thirteen shaman npcs first;
  they may not agree the way the wave attackers' index 0 did (22 of 22).
- **The other 65 `on_see_user_move` patterns** — 105 npcs gained the handler; one encounter is pinned.
- **The guards' two broadcasts** (22696 on waking, 22658 on the second clock) — no listener found in
  this tree, so a pin would assert into silence. Check the listener side before assuming they are idle.
- **18 `--implemented` audit candidates** — `python tools/client-extract/audit_stale_claims.py
  --implemented`. Most are accurate past-tense history; the yield is in repeated claims.

## D-bis. The skill-index rule, and what it costs

**An npc whose skill list cannot answer a pattern's indices is dropped from the pattern**, and if none
is left the pattern is refused. 203 npcs and 36 patterns sit behind that today.

The rule is too broad in one way, and it has now bitten once. The indices it counts include those in
**best-effort handlers** — the ones the extractor is otherwise happy to drop. So teaching the extractor
a new condition can *cost* a pattern: `Krall_WnH` was taken while its `on_spelled` was dropped whole,
and reading that handler raised the index bar past what its one npc could answer. (That npc is not
spawned here, so nothing in play changed.)

The narrower rule is to drop the *handler* rather than the *npc* when the unanswerable index appears
only in a best-effort handler. Worth doing, and worth measuring first: 203 npcs are behind the current
rule and some of them will be there for indices the rotation genuinely needs.

## E. Boundaries, not backlog

Not work items — recorded so nobody re-derives them.

**The first three are 4.8-versus-5.8**, and they are the shape to watch for: retail's 5.8 patterns ask
for a mechanism 4.8 has no source for, and the fix would be to *invent* the mechanism rather than port
it.

- **`activate_skillarea`** (121 npcs) — checked both sides: `SkillAreaNpcAI` is an empty stub here
  **and in the 4.8 Java**, so the C# is a faithful port and there is no area registry on either side to
  turn anything on. 4.8's skill areas are npcs a skill summons (`SummonSkillAreaEffect`), which is a
  different mechanism, not a smaller version of this one. Building a registry to satisfy 5.8 patterns
  would be inventing a subsystem 4.8 does not have.
- **`goto_alias` and `teleport_target_alias`** (14 and 1) — retail moves an npc, or a player, to a
  *named point*. 4.8's world and spawn data have no alias concept at all; the only `alias` in this tree
  is on item templates and means something else. Taking these needs an alias source extracted from the
  client first, which is a data project rather than a vocabulary item.
- **The Abyss turret switches** (172 npcs spawned here) — see section C-bis; they need the alias
  mechanism above plus a player transform tribe this port has no source for.
- **`random_move`** (135 npcs spawned here) — retail gives a duration and no range; 4.8 takes the range
  from the spawn row, and 112 of the 135 have `random_walk="0"` on every row. The 23 that do have a
  range use it only in combat handlers, where `StartRandomWalking` would take the npc out of the fight.
  See the log entry *`random_move`, and why it is not a port*.

The rest are the tables' own limits:

- **10,948 patterns refused for having no npc here free to run them** (6,899 battle + 4,049 wake).
  Either this port lacks the npc, or it is bound to a hand-written class.
- **876 patterns with no rotation and nothing sayable outside waking.**
- **220 npcs dropped from a pattern their skill list cannot answer** (197 + 23).
- **`is_race` about `OBJI_SELF`** (10 patterns) — an npc's own race is a constant, so the branch is
  always or never taken, and the table shares one branch list between npcs of different races. Deciding
  it at extract time would mean giving each race its own copy of every shared list.
- **16 walker routes** named by retail spawns and absent from every world file and the client pak.
- **75 skill categories** retail names for skills 4.8 has no template for. Dropped by the extractor,
  which prints the count.
- **The runner encounter** (`BIDF5_R2_Runner`, 9 npcs) — no spawn rows and no routes in this port, so
  it cannot be driven here at all.

## How to work on this

```bash
python tools/client-extract/check_loader_names.py   # cheapest check: needs no dump, no tables
python tools/client-extract/regen_check.py          # run the whole pipeline, verify it round-trips
dotnet test AionServer.slnx                         # ~55s
python scripts/parity/check_fidelity.py             # structural gate
pwsh scripts/ci/check-warning-baseline.ps1          # warning gate
```

Extractor refusal tallies are printed by each `extract_*.py` run and are the authority on what is
blocked. **Read the npc column, not the pattern column** — `extract_battle_cycles.py` prints both and
ranks by npcs, because the pattern count has mis-set priorities here twice. Re-measure before setting
a priority; this file did, and the old one was wrong.

`check_loader_names.py` guards a bug this port has hit three times: a role name the extractor can emit
and `PatternTableLoader` has no case for. It is free until a pattern using it goes live, and then it
refuses the whole table rather than one branch. Run it after touching any `*_ROLES` map.
