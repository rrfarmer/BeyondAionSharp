"""Retail's combat rotations: the adds a boss puts on the ground *during* the fight.

WHY THIS EXISTS
---------------
`IdleCycles` covers `on_idle_timer` -- what an NPC does while nothing is happening. **The fights live
somewhere else.** Retail bosses are not HP ladders: on entering combat a boss calls `add_battle_timer`
with an indicator and a delay, and when that timer fires the branches guarded by
`is_battle_timer_indicator` run and *re-arm the next link themselves*. A fight is a chain of timers.

The scale of it, across the whole dump:

| element | uses |
|---|---|
| `btimer_indicator` | 47,603 |
| `use_skill` inside `on_battle_timer` | 24,250 |
| `add_battle_timer` | 23,151 |
| **patterns with an `on_battle_timer`** | **2,600+** |

This started as spawn-only -- rotations that place adds -- because `use_skill` could not be said and a
skill rotation with its skills removed is nothing at all. With `SKILLI_INDEX` resolved
(`extract_npc_skill_lists.py`) that restriction was lifted, and the table grew from 16 patterns to
545: most boss mechanics are a cast on a timer, not a spawn.

`PatternAi` has had the engine all along -- thirty battle-timer slots, combat-gated, cancelled on death,
with `When.Timer` and `Do.ArmTimer`. What was missing was the data. This extracts it.

WHAT IS LEFT OUT, AND WHY IT IS MOST OF IT
------------------------------------------
A pattern is taken only if **every** branch of both handlers is sayable in full. Dropping one
unsayable action from an otherwise-portable branch is the shortcut that would make a boss spawn its
adds and never cast, which is worse than not running it at all. Counted rather than emitted:

* **`use_skill` -- 209 patterns.** By far the largest, and the one to fix next. Retail names skills by
  index into the NPC's own list (`SKILLI_INDEX_1`), and this port has no resolver for that.
* `control_door` (10), `increase_intvar` (5), and a long tail of one-offs -- `is_user_flying`,
  `is_skill_count_left`, `despawn_by_nameid`, `switch_target_by_attacker_indicator`.
* **82 rotations that nothing here arms.** They spawn from `on_battle_timer` but have no
  `on_enter_attack_state`, because retail also arms battle timers from `on_message` (334 uses),
  `on_attacked` (115) and `on_spelled` (110). Those handlers are a separate porting job.

`set_idle_timer`, `attack_most_hating` and `spawn_on_target` all have `Do.` helpers and were added
here on the theory that three more one-offs would pay. **They bought nothing** -- every pattern using
them was refused for a second reason as well -- so they were taken back out rather than left as
emitter paths no row exercises. A vocabulary gap is only worth closing when it is the last one.

Two conditions need care rather than a helper:

* **`is_hp_in_boundary` is exclusive at both ends**, and `When.HpBetween` is inclusive, so the bounds
  are emitted as `low+1 .. high-1`. Percentages are integers, so that is exact rather than a rounding.
* **`is_hp_lower_than` asks about a named creature, not always this one.** 6,048 of its 6,386 uses are
  `OBJI_SELF` and were always taken; the other 338 ask about somebody the event names and were refused,
  because emitting `HpBelow` for them would silently have read the wrong creature's health. They are
  read now, each against the role `PatternAi` already tracks -- `OBJI_FRIEND` is 314 of the 338 and
  lives entirely in the two friend handlers, where `Friend` is set. `OBJI_PARTY_MEMBER` (2) is still
  refused: this port has no party-member role on an npc pattern.

Unlike the idle table these rows carry **real spawn group ids**: `despawn` names a `SPAWN_ID_n`, so a
rotation that cleans up after itself needs the group it spawned into, not `Untracked`.

CLI:
    python extract_battle_cycles.py <patterns_dir> <binding_tsv> <out.tsv> [--repo ..]
"""
from __future__ import annotations

import argparse
import collections
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import summarize_pattern as S  # noqa: E402
import audit_missing_adds as A  # noqa: E402
from client_npc_names import npc_names  # noqa: E402
from extract_idle_cycles import COUNTERS, slot as flag_slot, string_ids  # noqa: E402

#: Retail's skill targets, and the two this port can say. `OBJI_SELF` and `OBJI_CUR_TARGET` are 88% of
#: all uses; the rest -- the event target, the talker, a friend, the message sender -- name a creature
#: this table has no way to point at, and are refused rather than approximated with the most-hated one.
#: `OBJI_FRIEND` is here because this port's `FRIEND` means the same thing -- a known, living npc the
#: caster is not hostile to -- so it needs no inference. The rest of retail's targets name a creature
#: by its role in the event (the attacker, the caster, the message parameter, the one that started the
#: fight). Those are **not** refused for lack of a name: `PatternAi` tracks every one of them. They are
#: refused because the skill queue resolves its target from the aggro list when it drains, so a cast
#: aimed at a particular creature would mean adding a case to `NpcSkillTargetAttribute` and to
#: `SkillAttackManager` -- a Java-parity enum and the engine around it, not data. See the doc entry.
TARGETS = {"OBJI_SELF": "ME", "OBJI_CUR_TARGET": "MOST_HATED", "OBJI_FRIEND": "FRIEND"}

#: Retail's role targets: the creature involved in the event, rather than a place in the hate list.
#: The queue now carries the creature itself (`AimedSkillEntry`), captured when the branch runs, so
#: these say what retail says instead of re-deriving somebody at drain time.
#:
#: `OBJI_TALKER` is absent -- talking is not a combat event and no rotation here has a talker.
ROLES = {
    "OBJI_EVENT_TARGET": "EventTarget",
    "OBJI_ATTACKER": "Attacker",
    "OBJI_CASTER": "Caster",
    "OBJI_MESSAGE_PARAM": "MessageParam",
    "OBJI_MESSAGE_SENDER": "MessageSender",
}

#: Retail's attacker indicators: a creature picked by its place in the hate list, or by how hurt it is.
#: `AggroTarget` names all six -- `LOWEST_HP` and `MOST_HP` were added to it for these very patterns --
#: so target switching is an exact mapping with nothing inferred.
AGGRO = {
    "ATTACKERI_RANDOM_ONE": "RANDOM",
    "ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET": "RANDOM_EXCEPT_CURRENT_TARGET",
    "ATTACKERI_SECOND_HATING": "SECOND_MOST_HATED",
    "ATTACKERI_THIRD_HATING": "THIRD_MOST_HATED",
    "ATTACKERI_HAS_LOWEST_HP": "LOWEST_HP",
    "ATTACKERI_HAS_MOST_HP": "MOST_HP",
}

#: The same indicators as a *skill* target. `NpcSkillTargetAttribute` used to be the narrower enum,
#: which meant a boss could **switch** to whoever was closest to dying but not **cast** at them; it now
#: carries the health-ranked pair too, resolved by delegating to `AggroTarget`, so the six map alike.
SKILL_AGGRO = dict(AGGRO)

#: Where a battle timer can be armed. Retail does not only start a fight's chain from entering combat:
#: of the 390 rotations with no `on_enter_attack_state` arming, 59 are started by a message from another
#: npc, 21 by being attacked, 18 by being spelled and 10 on waking. Reading only the first handler left
#: all of those inert -- the rotation was ported and nothing ever pulled the trigger.
#:
#: `on_battle_timer` is deliberately absent: 188 rotations re-arm only from inside themselves, which is
#: a chain with no first link *in the pattern*, and guessing an entry point for those would be invention.
#: `on_see_user_move` is deliberately absent: this port raises no "a player moved nearby" event, and
#: `AiPattern` keeps seeing an npc and seeing a user apart on purpose, so folding it into either would
#: be a guess. 10 rows are given up for it.
ARMING = ["on_enter_attack_state", "on_message", "on_attacked", "on_spelled", "on_wake_up",
          "on_see_npc", "on_see_user"]

#: Handlers that are not about arming anything -- they are what the encounter does when it ends.
#:
#: `on_die` is where 77 of the 196 encounters still missing an add place it, second only to the battle
#: timer itself, and this table read none of it. `on_leave_attack_state` is another 39. They are
#: best-effort for the same reason the optional arming handlers are: an unsayable branch costs that
#: handler, not the rotation.
ENDINGS = ["on_die", "on_leave_attack_state"]

#: Retail handlers whose engine slot in `PatternAi` was wired and never fed by any table.
#:
#: `Evaluate(Pattern.OnEnterIdle)`, `OnTalk`, `OnFriendAttacked`, `OnArrivedAtWaypoint`, `OnDespawn`,
#: `OnFriendSpelled`, `OnStopFleeing` and `OnFriendKilled` have all been called by the runtime since
#: `PatternAi` was written. Every one of them read an empty array, because the tables only ever filled
#: the slots the table in question was named after. This is the same asymmetry `ENDINGS` was added to
#: fix, two handlers at a time; these are the remaining eight.
#:
#: They are **best-effort** like the rest of `ARMING`, not `CORE`: an unsayable branch here loses this
#: way in and keeps the rotation. That asymmetry is deliberate and is argued at `CORE` below -- a
#: dropped rung in the rotation itself silently promotes the next one, because branch lists are
#: first-match-wins, but a handler that never fires is merely a mechanic we do not have yet.
SIGNALS = ["on_enter_idle_state", "on_talked_by_user", "on_see_friend_attacked",
           "on_arrived_at_waypoint", "on_despawn", "on_friend_spelled", "on_stop_to_flee",
           "on_see_friend_killed_by_user", "on_enter_return_sp", "on_leave_return_sp"]


#: `on_battle_timer` and `on_enter_attack_state` are the rotation, and an unsayable branch in either
#: refuses the whole pattern -- dropping a rung there silently promotes the next one, because branch
#: lists are first-match-wins.
#:
#: **The other arming handlers are best-effort, and that is a deliberate asymmetry.** They are extra
#: ways in, not the rotation itself, so an unsayable branch drops *that handler* and keeps the
#: rotation. Refusing the whole pattern instead cost nine rotations the first time these were added --
#: strictly worse than the status quo, where the handler was not read at all and the npc simply lacked
#: that trigger. What is given up is counted, never silent.
CORE = {"on_enter_attack_state"}

#: Retail's npc states, and the six this port can answer truthfully. See `When.Fighting` for why
#: `NPC_STATE_WAKE_UP` and `NPC_STATE_FLEE` are absent.
NPC_STATES = {
    "NPC_STATE_ATTACK": "fight",
    "NPC_STATE_IDLE": "idle",
    "NPC_STATE_GOTO_WAYPOINT": "route",
    "NPC_STATE_RANDOM_MOVE": "wander",
    "NPC_STATE_GOTO_POINT": "point",
    "NPC_STATE_USE_SKILL": "casting",
}

#: Retail's `is_enemy` subjects, and the condition each becomes.
ENEMY_ROLES = {
    "OBJI_MESSAGE_PARAM": "MessageParamIsEnemy",
    "OBJI_CASTER": "CasterIsEnemy",
    "OBJI_SEEN": "Enemy",
    "OBJI_CUR_TARGET": "TargetIsEnemy",
    "OBJI_ATTACKER": "AttackerIsEnemy",
    "OBJI_MESSAGE_SENDER": "MessageSenderIsEnemy",
    "OBJI_EVENT_TARGET": "EventTargetIsEnemy",
}

#: Retail's `is_user` subjects, and the condition each becomes.
USER_ROLES = {
    "OBJI_TALKER": "TalkerIsPlayer",
    "OBJI_KILLER": "KilledByPlayer",
    "OBJI_ATTACKER": "AttackedByPlayer",
    "OBJI_CASTER": "SpelledByPlayer",
    "OBJI_SEEN": "SeenIsPlayer",
    "OBJI_CUR_TARGET": "TargetIsPlayer",
    "OBJI_EVENT_TARGET": "EventTargetIsPlayer",
}

#: The same for `is_npc`.
NPC_ROLES = {
    "OBJI_KILLER": "KilledByNpc",
    "OBJI_CUR_TARGET": "TargetIsNpc",
    "OBJI_ATTACKER": "AttackerIsNpc",
    "OBJI_CASTER": "CasterIsNpc",
    "OBJI_EVENT_TARGET": "EventTargetIsNpc",
    "OBJI_SEEN": "SeenIsNpc",
}

#: Retail's `flee_from` subjects, and the action each becomes. `OBJI_SELF` is absent on purpose;
#: see PatternAi's flee members.
FLEE_ROLES = {
    "OBJI_CUR_TARGET": "flee_Flee",
    "OBJI_SEEN": "flee_FleeFromSeen",
    "OBJI_MESSAGE_PARAM": "flee_FleeFromMessageParam",
    "OBJI_ATTACKER": "flee_FleeFromAttacker",
    "OBJI_FRIENDS_ATTACKER": "flee_FleeFromFriendsAttacker",
    "OBJI_CASTER": "flee_FleeFromCaster",
    "OBJI_KILLER": "flee_FleeFromKiller",
    "OBJI_EVENT_TARGET": "flee_FleeFromEventTarget",
    "OBJI_MESSAGE_SENDER": "flee_FleeFromMessageSender",
    "OBJI_TALKER": "flee_FleeFromTalker",
}

#: Retail's `is_hp_lower_than` subjects other than itself, and the condition each becomes.
#: `OBJI_PARTY_MEMBER` is absent -- this port has no party-member role on an npc pattern.
HP_ROLES = {
    "OBJI_FRIEND": "FriendHpBelow",
    "OBJI_CUR_TARGET": "TargetHpBelow",
    "OBJI_SEEN": "SeenHpBelow",
    "OBJI_CASTER": "CasterHpBelow",
    "OBJI_ATTACKER": "AttackerHpBelow",
    "OBJI_MESSAGE_SENDER": "MessageSenderHpBelow",
}

#: Retail's `is_distance_longer_than` subjects. `OBJI_SELF` is absent -- see the condition.
DISTANCE_ROLES = {
    "OBJI_CUR_TARGET": "TargetBeyond",
    "OBJI_EVENT_TARGET": "EventTargetBeyond",
    "OBJI_ATTACKER": "AttackerBeyond",
    "OBJI_CASTER": "CasterBeyond",
    "OBJI_MESSAGE_PARAM": "MessageParamBeyond",
    "OBJI_SEEN": "SeenBeyond",
}

#: Retail's `order_in_attacker_list`, and this port's enum.
MULTI_ORDER = {
    "ORDERI_RANDOM": "Random",
    "ORDERI_DESCENDING": "Descending",
    "ORDERI_ASCENDING": "Ascending",
}

#: Retail's `is_race` subjects, and the condition each becomes. `OBJI_SELF` is absent: an npc
#: asking its own race is asking about a constant, and the branch is decided at build time
#: rather than at run time -- emitting it would be emitting a rung that is always or never
#: taken, which is a claim about the data this table has no business making.
RACE_ROLES = {
    "OBJI_SEEN": "SeenRace",
    "OBJI_CUR_TARGET": "TargetRace",
    "OBJI_CASTER": "CasterRace",
    "OBJI_ATTACKER": "AttackerRace",
    "OBJI_KILLER": "KillerRace",
    "OBJI_TALKER": "TalkerRace",
    "OBJI_EVENT_TARGET": "EventTargetRace",
    "OBJI_MESSAGE_PARAM": "MessageParamRace",
    "OBJI_MESSAGE_SENDER": "MessageSenderRace",
}

#: The only two retail race names that are not this port's enum name lowercased.
RACE_ALIASES = {"pc_light": "ELYOS", "pc_dark": "ASMODIANS"}

#: Every member of this port's `Race`, so a retail value that names none is refused.
PORT_RACES = {
    "ELYOS",
    "ASMODIANS",
    "LYCAN",
    "CONSTRUCT",
    "CARRIER",
    "DRAKAN",
    "LIZARDMAN",
    "TELEPORTER",
    "NAGA",
    "BROWNIE",
    "KRALL",
    "SHULACK",
    "BARRIER",
    "PC_LIGHT_CASTLE_DOOR",
    "PC_DARK_CASTLE_DOOR",
    "DRAGON_CASTLE_DOOR",
    "GCHIEF_LIGHT",
    "GCHIEF_DARK",
    "DRAGON",
    "OUTSIDER",
    "RATMAN",
    "DEMIHUMANOID",
    "UNDEAD",
    "BEAST",
    "MAGICALMONSTER",
    "ELEMENTAL",
    "LIVINGWATER",
    "NONE",
    "PC_ALL",
    "DEFORM",
    "NEUT",
    "GHENCHMAN_LIGHT",
    "GHENCHMAN_DARK",
    "EVENT_TOWER_DARK",
    "EVENT_TOWER_LIGHT",
    "GOBLIN",
    "TRICODARK",
    "NPC",
    "LIGHT",
    "DARK",
    "WORLD_EVENT_DEFTOWER",
    "ORC",
    "DRAGONET",
    "SIEGEDRAKAN",
    "GCHIEF_DRAGON",
    "WORLD_EVENT_BONFIRE",
    "DOOR_KILLER",
}

BRANCH_RE = re.compile(r"<pattern>(.*?)</pattern>", re.S)

#: Every class an npc may already be on and still acquire generated pattern rows.
#:
#: **This used to be per table, and that is what made the tables mutually exclusive.** Nothing here
#: excluded another table's npcs on purpose; the sets simply disagreed, and binding order did the rest
#: -- once the battle table moved an npc from `aggressive` to `battle_cycle`, no other extractor could
#: see it any more. The measured cost was 533 npcs holding a retail rotation their owning table could
#: not read, among much else.
#:
#: The set is now the same everywhere, so a pattern's handlers are read by every table that can read
#: them, and `GeneratedPattern` composes what each npc ends up with. The class an npc is bound to still
#: decides only one thing -- whether it fights -- which is why the generated class names are listed
#: here too: rebinding must be idempotent, or a second run would drop everything the first run took.
#:
#: `wake_variable` and `wake_variable_aggressive` are here, which they were not when the accepted set
#: was first unified: they were held back because those classes descended from `GeneralNpcAI` and
#: `AggressiveNpcAI` rather than `PatternAi`, so an npc bound there would carry rows that never ran.
#: They are `PassivePatternAi` and `PatternAi` now, keeping the aggression each was written to protect,
#: so the exclusion has no reason left. Their spawn-time variable write is an override and survives
#: unchanged -- **the tables do not subsume it**, and nothing here claims they do.
GENERIC = {"aggressive", "general", "battle_cycle", "death_spawn", "idle_cycle",
           "idle_cycle_passive", "aggressive_pattern", "passive_pattern",
           "wake_variable", "wake_variable_aggressive"}

#: Retail's thirty battle-timer slots, named by index.
TIMER_RE = re.compile(r"BTIMERI_INDEX_(\d+)")


def timer_slot(token: str) -> int | None:
    found = TIMER_RE.search(token)
    return int(found.group(1)) if found else None


class Unsayable(Exception):
    """The one element that stopped a rotation being ported, by name."""


def read_guards(block: str) -> list[str]:
    """The branch's conditions as tokens; raises Unsayable if one cannot be said."""
    out: list[str] = []
    for element in re.finditer(r"<(\w+)>(.*?)</\1>", block, re.S):
        kind, body = element.group(1), element.group(2)
        if kind == "is_battle_timer_indicator":
            slot = timer_slot(body)
            if slot is None:
                raise Unsayable("is_battle_timer_indicator with no slot")
            out.append(f"timer:{slot}")
        elif kind == "is_hp_lower_than":
            who = re.search(r"<who>(\w+)</who>", body)
            percent = re.search(r"<percent>(\d+)</percent>", body)
            if not who or not percent:
                raise Unsayable("is_hp_lower_than with no subject or percent")
            if who.group(1) == "OBJI_SELF":
                out.append(f"hp_below:{percent.group(1)}")
            elif who.group(1) in HP_ROLES:
                # Somebody else's health, which is a different question and a different creature.
                # See `When.FriendHpBelow`: an absent role answers false, because "somebody who is not
                # there is below 30%" is not true about anything.
                out.append(f"hp_of:{HP_ROLES[who.group(1)]}:{percent.group(1)}")
            else:
                raise Unsayable(f"is_hp_lower_than about {who.group(1)}")
        elif kind == "is_hp_in_boundary":
            who = re.search(r"<who>(\w+)</who>", body)
            low = re.search(r"<larger_than>(\d+)</larger_than>", body)
            high = re.search(r"<less_than>(\d+)</less_than>", body)
            if not (low and high) or not who or who.group(1) != "OBJI_SELF":
                raise Unsayable("is_hp_in_boundary about somebody else")
            # Exclusive at both ends in retail; When.HpBetween is inclusive.
            out.append(f"hp_between:{int(low.group(1)) + 1}:{int(high.group(1)) - 1}")
        elif kind in ("set_flag_var", "unset_flag_var",
                      "set_world_flag_var", "unset_world_flag_var", "is_world_flag_var"):
            indicator = re.search(r"<flagvar_indicator>([^<]+)</flagvar_indicator>", body)
            slot = flag_slot(indicator.group(1)) if indicator else None
            if slot is None:
                raise Unsayable(f"{kind} in a flag family this port does not number")
            out.append(f"{kind}:{slot}")
        elif kind == "is_message":
            number = re.search(r"<message_type>(\d+)</message_type>", body)
            if not number:
                raise Unsayable("is_message with no message type")
            out.append(f"message:{number.group(1)}")
        elif kind == "is_user_flying":
            who = re.search(r"<user>(\w+)</user>", body)
            roles = {"USERI_EVENT_TARGET": "EventTarget", "USERI_ATTACKER": "Attacker",
                     "USERI_CASTER": "Caster", "USERI_SEEN": "Seen"}
            if not who or who.group(1) not in roles:
                raise Unsayable("is_user_flying about a creature this port cannot name")
            out.append("flying:" + roles[who.group(1)])
        elif kind == "test_probability":
            percent = re.search(r"<percent>(\d+)</percent>", body)
            if not percent:
                raise Unsayable("test_probability with no percent")
            out.append(f"chance:{percent.group(1)}")
        elif kind == "is_skill_count_left":
            # Retail names the skill by its place in this npc's own ordered list, exactly as `use_skill`
            # does, so the index is carried here and resolved per npc alongside the casts.
            index = re.search(r"SKILLI_INDEX_(\d+)", body)
            if not index:
                raise Unsayable("is_skill_count_left without an index")
            out.append("skillready:" + index.group(1))
        elif kind in ("is_user", "is_npc"):
            # Is the creature in this role a player, or an npc? `OBJI_SELF` and `OBJI_FRIEND` are
            # refused: the first is definitionally true, which would be reasoning rather than porting,
            # and the second names a role this port does not resolve to a creature.
            table = USER_ROLES if kind == "is_user" else NPC_ROLES
            who = re.search(r"<obj_indicator>(\w+)</obj_indicator>", body)
            if not who or who.group(1) not in table:
                raise Unsayable(f"{kind} about {who.group(1) if who else '?'}")
            out.append("who:" + table[who.group(1)])
        elif kind == "is_enemy":
            # Is whoever is in this role hostile to me? Every role retail asks about is one `PatternAi`
            # already tracks, so all 1,156 uses are sayable and none needs a new notion of hostility --
            # `Creature.IsEnemy` answers all of them, as it already did for the fortress guards.
            who = re.search(r"<who>(\w+)</who>", body)
            if not who or who.group(1) not in ENEMY_ROLES:
                raise Unsayable(f"is_enemy about {who.group(1) if who else '?'}")
            out.append("enemy:" + ENEMY_ROLES[who.group(1)])
        elif kind == "is_race":
            # Retail names the race with `race_type`, matched to this port's `Race` by exact name
            # apart from the two pc aliases. Anything that does not name a member is refused rather
            # than approximated -- see `When.KillerRace`.
            who = re.search(r"<from>(\w+)</from>", body)
            race = re.search(r"<race_type>(\w+)</race_type>", body)
            if not who or not race:
                raise Unsayable("is_race with no subject or race")
            if who.group(1) not in RACE_ROLES:
                raise Unsayable(f"is_race about {who.group(1)}")
            name = RACE_ALIASES.get(race.group(1), race.group(1).upper())
            if name not in PORT_RACES:
                raise Unsayable(f"is_race of a race this port does not name: {race.group(1)}")
            out.append(f"race:{RACE_ROLES[who.group(1)]}:{name}")
        elif kind == "is_distance_longer_than":
            who = re.search(r"<who>(\w+)</who>", body)
            metres = re.search(r"<distance>([-\d.]+)</distance>", body)
            if not who or not metres:
                raise Unsayable("is_distance_longer_than with no subject or distance")
            if who.group(1) not in DISTANCE_ROLES:
                # `OBJI_SELF` lands here: the distance from an npc to itself is zero, so the branch is
                # dead by construction and emitting it would be emitting a rung that cannot fire.
                raise Unsayable(f"is_distance_longer_than about {who.group(1)}")
            out.append(f"beyond:{DISTANCE_ROLES[who.group(1)]}:{int(float(metres.group(1)))}")
        elif kind == "is_npc_state":
            # What the npc is doing right now. Every one of the 2,834 uses asks about NPCI_SELF, but
            # the subject is checked rather than assumed -- a pattern asking about somebody else would
            # otherwise be silently answered about this npc.
            #
            # `NPC_STATE_WAKE_UP` (336) and `NPC_STATE_FLEE` (21) are refused. Waking is a moment in
            # `HandleSpawned` here, not a state an npc sits in, and this port's `AIState.FEAR` is the
            # abnormal effect rather than retail's low-health flight. Approximating either would put a
            # branch on the ground that fires at the wrong time, which is worse than not having it.
            who = re.search(r"<who>(\w+)</who>", body)
            state = re.search(r"<state>(\w+)</state>", body)
            if not who or who.group(1) != "NPCI_SELF":
                raise Unsayable("is_npc_state about somebody other than itself")
            if not state or state.group(1) not in NPC_STATES:
                raise Unsayable(f"is_npc_state {state.group(1) if state else '?'}")
            out.append("state:" + NPC_STATES[state.group(1)])
        elif kind == "is_waypoint_index":
            # Which point of its own route the npc is standing on. The engine has had this since
            # `When.AtWaypoint` was written for the hand-written classes -- `PatternAi.WaypointIndex`
            # reads `GetCurrentStep().GetStepIndex()` -- and no extractor ever emitted it, so 143
            # `on_arrived_at_waypoint` handlers were dropped on a condition the runtime could answer.
            #
            # Retail counts from one and this port's `RouteStep` from zero. The conversion lives in
            # `When.AtWaypoint`, so retail's own number is carried here unchanged rather than shifted
            # in two places.
            index = re.search(r"<index>(\d+)</index>", body)
            if not index:
                raise Unsayable("is_waypoint_index with no index")
            out.append(f"waypoint:{index.group(1)}")
        elif kind == "is_last_waypoint":
            out.append("last_waypoint:")
        elif kind == "increase_intvar":
            # A condition that increments as it tests, like the flag idiom. All 1,409 uses in the dump
            # are conditions and none is an action; see `When.Counting`.
            indicator = re.search(r"<intvar_indicator>([^<]+)</intvar_indicator>", body)
            low = re.search(r"<lower_bound>(-?\d+)</lower_bound>", body)
            high = re.search(r"<upper_bound>(-?\d+)</upper_bound>", body)
            at_bound = re.search(r"<be_true_only_when_hit_the_bound>(\w+)</", body)
            if not (indicator and low and high) or indicator.group(1).strip() not in COUNTERS:
                raise Unsayable("increase_intvar on a counter this port does not number")
            out.append("count:%d:%s:%s:%s" % (
                COUNTERS.index(indicator.group(1).strip()), low.group(1), high.group(1),
                "1" if at_bound and at_bound.group(1).upper() == "TRUE" else "0"))
        else:
            raise Unsayable(f"condition {kind}")
    return out


def read_actions(block: str, dev: dict[str, int], known: set[int],
                 strings: dict[str, int], handler: str = "") -> list[tuple] | None:
    """The branch's actions in retail's order, or None if one cannot be said."""
    out: list[tuple] = []
    for element in re.finditer(r"<(\w+)>(.*?)</\1>", block, re.S):
        kind, body = element.group(1), element.group(2)
        if kind == "spawn":
            named = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", body)
            npc_id = dev.get(named.group(1)) if named else None
            if npc_id is None or npc_id not in known:
                raise Unsayable("spawns an npc with no template here")
            where = re.search(r"<spawn_location_type>(\w+)</", body)
            place = ("self" if where and where.group(1).endswith("MY_POINT")
                     else "offset" if where and where.group(1).endswith("RELATIVE")
                     else "absolute")
            spot = [re.search(r"<%s>([-\d.]+)</%s>" % (axis, axis), body) for axis in "xyz"]
            if place == "absolute" and not all(spot):
                return None
            count = re.search(r"<num_to_spawn>(\d+)</", body)
            live = re.search(r"<live_time>(\d+)</", body)
            group = re.search(r"<spawn_id>SPAWN_ID_(\d+)</", body)
            # `despawn_at_attack_state` says the add belongs to the fight and not to the world. 12,614
            # of retail's 16,343 spawns carry it and 7,690 of those are permanent, so dropping it was
            # every one of those staying on the ground once the fight ended.
            transient = re.search(r"<despawn_at_attack_state>(\w+)</", body)
            place = ("for_the_fight_" + place) if transient and transient.group(1).upper() == "TRUE"                 else place
            out.append(("spawn", npc_id, int(count.group(1)) if count else 1,
                        int(live.group(1)) if live else 0, place,
                        float(spot[0].group(1)) if spot[0] else 0.0,
                        float(spot[1].group(1)) if spot[1] else 0.0,
                        float(spot[2].group(1)) if spot[2] else 0.0,
                        int(group.group(1)) if group else 0))
        elif kind == "add_battle_timer":
            slot = timer_slot(body)
            delay = re.search(r"<delay>(\d+)</delay>", body)
            if slot is None:
                return None
            out.append(("arm", slot, int(delay.group(1)) if delay else 0, 0, "",
                        0.0, 0.0, 0.0, 0))
        elif kind == "despawn":
            group = re.search(r"<spawn_id>SPAWN_ID_(\d+)</", body)
            if not group:
                return None
            out.append(("despawn", int(group.group(1)), 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "use_skill":
            # Left unresolved here: the index is into *the npc's own* skill list, and one pattern can
            # be bound to several npcs with different lists. Resolved per npc in main().
            index = re.search(r"SKILLI_INDEX_(\d+)", body)
            who = re.search(r"<target>(\w+)</target>", body)
            if not index:
                raise Unsayable("use_skill without an index")
            if not who or (who.group(1) not in TARGETS and who.group(1) not in ROLES):
                raise Unsayable("use_skill at a target this port cannot name")
            named = who.group(1)
            if named in ROLES:
                out.append(("skill_at", int(index.group(1)), 0, 0, ROLES[named], 0.0, 0.0, 0.0, 0))
            else:
                out.append(("skill", int(index.group(1)), 0, 0, TARGETS[named], 0.0, 0.0, 0.0, 0))
        elif kind == "switch_target_by_attacker_indicator":
            who = re.search(r"<target>(\w+)</target>", body)
            if not who or who.group(1) not in AGGRO:
                raise Unsayable("switch_target_by_attacker_indicator at an indicator this port lacks")
            out.append(("switch_to", 0, 0, 0, AGGRO[who.group(1)], 0.0, 0.0, 0.0, 0))
        elif kind == "use_skill_by_attacker_indicator":
            index = re.search(r"SKILLI_INDEX_(\d+)", body)
            who = re.search(r"<target>(\w+)</target>", body)
            ranged = re.search(r"<restricted_range>(\w+)</restricted_range>", body)
            if not index or not who or who.group(1) not in SKILL_AGGRO:
                raise Unsayable("use_skill_by_attacker_indicator at an indicator this port lacks"
                                if index else "use_skill_by_attacker_indicator without an index")
            # `restricted_range` narrows the candidates to those within reach. It was refused on the
            # grounds that the queue picks its target at drain time and takes no bound -- true of the
            # unaimed path, but `CastSkillAt` resolves a creature now and sends it with the entry, so
            # the bound can be applied where retail applies it. Retail states no distance, so the reach
            # is the skill's own `first_target_range`; see `CastSkillOnRankedInReach`.
            out.append(("skill_in_reach" if ranged and ranged.group(1).upper() == "TRUE" else "skill",
                        int(index.group(1)), 0, 0, SKILL_AGGRO[who.group(1)],
                        0.0, 0.0, 0.0, 0))
        elif kind in ("add_hate_point", "switch_target"):
            # Only the message parameter: these name a creature by role, and the message param is the
            # one this port can point at. `add_hate_point` at a friend or the caster is a different
            # helper and is refused rather than aimed at the wrong creature.
            who = re.search(r"<target>(\w+)</target>", body)
            if not who or who.group(1) != "OBJI_MESSAGE_PARAM":
                raise Unsayable(f"{kind} at a creature this port cannot name")
            hate = re.search(r"<point[s]?_to_add>(-?\d+)</", body)
            out.append(("hate" if kind == "add_hate_point" else "switch",
                        int(hate.group(1)) if hate else 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "attack_most_hating":
            out.append(("attack", 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "spawn_on_multi_target":
            # One add on every valid target, capped, with retail choosing which end of the hate list
            # the cap keeps. `Do.SpawnOnEachTarget` has taken all of this since it was written for a
            # hand-written class; nothing here is new machinery.
            named = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", body)
            npc_id = dev.get(named.group(1)) if named else None
            if npc_id is None or npc_id not in known:
                raise Unsayable("spawns an npc with no template here")
            order = re.search(r"<order_in_attacker_list>(\w+)</", body)
            cap = re.search(r"<total_set_to_spawn>(\d+)</", body)
            if not order or order.group(1) not in MULTI_ORDER:
                raise Unsayable("spawn_on_multi_target with no order")
            if not cap or int(cap.group(1)) <= 0:
                # The cap is what makes the order mean anything, and an uncapped multi-target spawn
                # would place one add per creature on the hate list with no bound. Refused rather than
                # given a number.
                raise Unsayable("spawn_on_multi_target with no cap")
            group = re.search(r"<spawn_id>SPAWN_ID_(\d+)</", body)
            live = re.search(r"<live_time>(\d+)</", body)
            reach = re.search(r"<spawn_range>([-\d.]+)</", body)
            valid = re.search(r"<valid_distance>([-\d.]+)</", body)
            hate = re.search(r"<hatepoints_to_add>(\d+)</", body)
            attacks = re.search(r"<attack_target_after_spawn>(\w+)</", body)
            points = int(hate.group(1)) if hate else 0
            # Same rule the single-target spawn uses: TRUE with no hate points says "attack" and gives
            # nothing to attack with, and inventing a number would invent how hard it pulls.
            if attacks and attacks.group(1).upper() == "TRUE" and points == 0:
                raise Unsayable("spawn_on_multi_target told to attack with no hate points")
            if not (attacks and attacks.group(1).upper() == "TRUE"):
                points = 0
            out.append(("spawn_each", npc_id, int(cap.group(1)), points,
                        MULTI_ORDER[order.group(1)],
                        float(valid.group(1)) if valid else 0.0,
                        float(reach.group(1)) if reach else 0.0,
                        float(live.group(1)) if live else 0.0,
                        int(group.group(1)) if group else 0))
        elif kind == "spawn_on_target":
            named = re.search(r"<npc_nameid>([^<]+)</npc_nameid>", body)
            npc_id = dev.get(named.group(1)) if named else None
            if npc_id is None or npc_id not in known:
                raise Unsayable("spawns an npc with no template here")
            who = re.search(r"<target_obj>(\w+)</target_obj>", body)
            where = who.group(1) if who else ""
            if where not in ("OBJI_CUR_TARGET", "OBJI_SELF"):
                raise Unsayable("spawn_on_target at a creature this port cannot name")
            count = re.search(r"<num_to_spawn>(\d+)</", body)
            live = re.search(r"<live_time>(\d+)</", body)
            reach = re.search(r"<spawn_range>([-\d.]+)</", body)
            valid = re.search(r"<valid_distance>([-\d.]+)</", body)
            group = re.search(r"<spawn_id>SPAWN_ID_(\d+)</", body)
            hate = re.search(r"<hatepoints_to_add>(\d+)</", body)
            attacks = re.search(r"<attack_target_after_spawn>(\w+)</", body)
            points = int(hate.group(1)) if hate else 0
            # `attack_target_after_spawn` and `hatepoints_to_add` are one op here: the hate is what
            # makes the summon fight. TRUE with no hate points says "attack" and gives nothing to
            # attack with, and inventing a number would invent how hard it pulls, so it is refused.
            if attacks and attacks.group(1).upper() == "TRUE" and points == 0:
                raise Unsayable("spawn_on_target told to attack with no hate points")
            if not (attacks and attacks.group(1).upper() == "TRUE"):
                points = 0
            out.append(("spawn_on_target" if where == "OBJI_CUR_TARGET" else "spawn_near",
                        npc_id, int(count.group(1)) if count else 1,
                        int(live.group(1)) if live else 0,
                        "", float(reach.group(1)) if reach else 0.0,
                        float(valid.group(1)) if valid else 0.0, float(points),
                        int(group.group(1)) if group else 0))
        elif kind == "despawn_self":
            out.append(("despawn_self", 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "set_idle_timer":
            delay = re.search(r"<delay>(\d+)</delay>", body)
            out.append(("timer", int(delay.group(1)) if delay else 0, 0, 0, "",
                        0.0, 0.0, 0.0, 0))
        elif kind == "do_nothing":
            # Carried rather than skipped: branch lists are first-match-wins, so a matching do-nothing
            # branch is retail saying "this case, and none of the ones below".
            out.append(("nothing", 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "reset_hatepoints":
            # `volatile_hatepoint_only` asks for retail's split between hate that decays and hate that
            # does not. This port keeps one number per creature, so there is no volatile half to clear
            # on its own; the 4 uses that ask for it are refused rather than turned into a full reset,
            # which would drop hate retail keeps.
            volatile_only = re.search(r"<volatile_hatepoint_only>(\w+)</", body)
            if volatile_only and volatile_only.group(1).upper() == "TRUE":
                raise Unsayable("reset_hatepoints of the volatile hate only")
            keep_top = re.search(r"<is_except_most_hating>(\w+)</", body)
            out.append(("reset_hate_top" if keep_top and keep_top.group(1).upper() == "TRUE"
                        else "reset_hate", 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "flee_from":
            # `push_state` is carried by retail on every one of these and is **not modelled**: this
            # port has a single flee behaviour that runs for the given time and then stops, and there
            # is no state stack for a TRUE to push onto. 232 uses say TRUE and 121 FALSE, and both are
            # read the same way here. Recorded rather than silently flattened.
            who = re.search(r"<from>(\w+)</from>", body)
            seconds = re.search(r"<seconds>(\d+)</seconds>", body)
            if not who:
                raise Unsayable("flee_from with no subject")
            role = who.group(1)
            # **The role means different creatures in different handlers.** `OBJI_ATTACKER` on
            # `on_attacked` is whoever hit *me*; on `on_see_friend_attacked` it is whoever hit my
            # friend, and this port keeps those in separate fields. Reading the second as the first
            # would make a rescuer flee its own last attacker -- often nobody, so the mechanic would
            # simply not happen, which is the quietest kind of wrong.
            if role == "OBJI_ATTACKER" and handler == "on_see_friend_attacked":
                role = "OBJI_FRIENDS_ATTACKER"
            # The same shift for the caster has nowhere to land: this port tracks a friend's attacker
            # and not a friend's caster, so those 3 uses are refused rather than aimed at the wrong
            # creature.
            if role == "OBJI_CASTER" and handler in ("on_friend_spelled", "on_friend_spelling"):
                raise Unsayable("flee_from the caster who spelled a friend")
            if role not in FLEE_ROLES:
                raise Unsayable(f"flee_from {role}")
            if not seconds or int(seconds.group(1)) <= 0:
                raise Unsayable("flee_from with no time to run")
            out.append((FLEE_ROLES[role], int(seconds.group(1)), 0, 0, "",
                        0.0, 0.0, 0.0, 0))
        elif kind == "goto_next_waypoint":
            # 669 uses, all of them carrying nothing but a move type. A run is refused for the same
            # reason `goto_waypoint` refuses one -- this port's route walking has a single speed --
            # which costs 186 of them; the other 483 walk or leave it unspecified.
            how = re.search(r"<move_type>(\w+)</move_type>", body)
            if how and how.group(1) == "MOVETYPE_RUN":
                raise Unsayable("goto_next_waypoint asking for a run")
            out.append(("next_waypoint", 0, 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "goto_waypoint":
            # Retail's waypoint is an index into the npc's own route, not a named path. `move_type`
            # says walk or run and this port's route walking has one speed, so a rung asking for a run
            # is refused rather than quietly walked.
            step = re.search(r"<waypoint>(\d+)</waypoint>", body)
            how = re.search(r"<move_type>(\w+)</move_type>", body)
            if not step:
                raise Unsayable("goto_waypoint with no waypoint")
            if how and how.group(1) == "MOVETYPE_RUN":
                raise Unsayable("goto_waypoint asking for a run")
            out.append(("waypoint", int(step.group(1)), 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind in ("say_to_all", "display_system_message", "send_system_msg"):
            named = re.search(r"<string_id>([^<]+)</string_id>", body)
            message = strings.get(named.group(1).strip()) if named else None
            if message is None:
                return None
            delay = re.search(r"<delay>(\d+)</delay>", body)
            out.append(("say" if kind == "say_to_all" else "sysmsg", message,
                        int(delay.group(1)) if delay else 0, 0, "", 0.0, 0.0, 0.0, 0))
        elif kind == "set_condition_spawn_variable":
            name = re.search(r"<string>([^<]*)</string>", body)
            value = re.search(r"<set>(-?\d+)</set>", body)
            modify = re.search(r"<modify>(-?\d+)</modify>", body)
            if not name or not name.group(1).strip():
                return None
            out.append(("var", int(value.group(1)) if value else 0,
                        int(modify.group(1)) if modify else 0, 0,
                        name.group(1).strip(), 0.0, 0.0, 0.0, 0))
        elif kind == "broadcast_message":
            message = re.search(r"<message_type>(\d+)</message_type>", body)
            reach = re.search(r"<range_as_meter>(\d+)</", body)
            if not message:
                return None
            out.append(("broadcast", int(message.group(1)),
                        int(reach.group(1)) if reach else 0, 0, "", 0.0, 0.0, 0.0, 0))
        else:
            raise Unsayable(f"action {kind}")
    return out


def read_handler(body: str, name: str, dev, known, strings):
    """Every branch of one handler, or None if any part of it cannot be said."""
    block = re.search(r"<%s>(.*?)</%s>" % (name, name), body, re.S)
    if not block:
        return []
    branches = []
    for index, branch in enumerate(BRANCH_RE.finditer(block.group(1))):
        guards: list[str] = []
        found = re.search(r"<conditions>(.*?)</conditions>", branch.group(1), re.S)
        if found:
            guards = read_guards(found.group(1))
        actions: list[tuple] = []
        found = re.search(r"<actions>(.*?)</actions>", branch.group(1), re.S)
        if found:
            actions = read_actions(found.group(1), dev, known, strings, handler=name)
            if actions is None:
                return None
        if not actions:
            continue
        priority = re.search(r"<priority>(\d+)</priority>", branch.group(1))
        branches.append((index, int(priority.group(1)) if priority else 0, guards, actions))
    return branches


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("patterns_dir", type=pathlib.Path)
    ap.add_argument("binding", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--repo", type=pathlib.Path, default=pathlib.Path(__file__).parents[2])
    args = ap.parse_args()

    templates = A.read_text(args.repo / "game-server/data/static_data/npcs/npc_templates.xml")
    ai = {int(m.group(1)): m.group(2)
          for m in re.finditer(r'npc_id="(\d+)"[^>]*?\bai="([\w_]+)"', templates)}
    dev = {k: int(v) for k, v in npc_names(args.patterns_dir).items()}
    strings = string_ids(args.repo)

    # npc -> index -> skill id, but only entries this port can actually cast.
    skills: dict[int, dict[int, int]] = collections.defaultdict(dict)
    for line in (args.repo / "tools/client-extract/out/npc_skill_lists.tsv").read_text(
            encoding="utf-8").splitlines()[1:]:
        fields = line.split("	")
        if fields[5] == "TRUE":
            skills[int(fields[0])][int(fields[1])] = int(fields[3])

    # An npc an encounter class already models must not be rebound to a generated table.
    spoken_for: set[int] = set()
    for source in (args.repo / "src/Aion.GameServer/Handlers/AI").glob("*.cs"):
        for found in re.finditer(r"=\s*(\d{6})\s*;",
                                 source.read_text(encoding="utf-8", errors="replace")):
            spoken_for.add(int(found.group(1)))

    binders: dict[str, list[int]] = collections.defaultdict(list)
    for line in A.read_text(args.binding).splitlines():
        fields = line.split("\t")
        if len(fields) > 3 and fields[0].isdigit():
            binders[fields[3]].append(int(fields[0]))

    rows: list[tuple] = []
    refused: collections.Counter = collections.Counter()
    refused_owners = 0
    dropped: collections.Counter = collections.Counter()
    patterns = 0
    for path in sorted(args.patterns_dir.rglob("NpcAIPatterns*.xml")):
        text = S.read_text(path)
        for match in S.PATTERN_RE.finditer(text):
            body = match.group(1)
            named = S.NAME_RE.search(body)
            if not named:
                continue
            # A rotation is no longer the price of entry. This table was named after `on_battle_timer`
            # and required it, which was right while it was the only thing here -- but the eight
            # `SIGNALS` handlers live overwhelmingly in patterns that have no rotation at all, so
            # requiring one meant reading 4 rows out of 1,332 patterns carrying `on_enter_idle_state`.
            # A pattern is taken if it has a rotation **or** anything sayable in an ending or a signal.
            timer = re.search(r"<on_battle_timer>(.*?)</on_battle_timer>", body, re.S)
            if not timer and not any(re.search(r"<%s>" % handler, body)
                                     for handler in ENDINGS + SIGNALS):
                continue
            owners = [n for n in binders.get(named.group(1), [])
                      if ai.get(n) in GENERIC and n not in spoken_for]
            if not owners:
                refused["no npc here that is free to run it"] += 1
                continue

            arming = {}
            try:
                cycle = (read_handler(body, "on_battle_timer", dev, ai.keys(), strings)
                         if timer else [])
                for handler in ARMING + ENDINGS + SIGNALS:
                    # `CORE` refuses the whole pattern when it cannot be read, and that severity is
                    # about the rotation: dropping a rung there silently promotes the next one, because
                    # branch lists are first-match-wins. With no rotation to corrupt there is nothing
                    # to be severe about, so `on_enter_attack_state` is best-effort like the rest.
                    if handler in CORE and cycle:
                        arming[handler] = read_handler(body, handler, dev, ai.keys(), strings)
                        continue
                    try:
                        arming[handler] = read_handler(body, handler, dev, ai.keys(), strings)
                    except Unsayable as stopper:
                        # Keep the rotation, lose this way in.
                        dropped[f"{handler}: {stopper}"] += 1
            except Unsayable as stopper:
                refused[str(stopper)] += 1
                continue
            # Without a rung that arms a timer nothing starts the chain and the rotation is inert.
            # A handler that merely exists is not enough -- it has to actually arm one.
            # An ending handler can carry an `arm`, but arming a battle timer as you die is not a way
            # into a rotation -- the npc is gone before it fires. They are kept for their spawns and
            # excluded from what counts as armed.
            if cycle:
                # A way into a rotation has to actually arm a timer. Endings and signals ride along:
                # they are worth keeping for their spawns, but arming a battle timer as you die is not
                # a way in, because the npc is gone before it fires.
                armed = {h: rungs for h, rungs in arming.items()
                         if h in ENDINGS or h in SIGNALS
                         or any(action[0] == "arm"
                                for _, _, _, actions in rungs for action in actions)}
                if not any(h not in ENDINGS and h not in SIGNALS for h in armed):
                    refused["nothing arms the first timer"] += 1
                    continue
            else:
                # No rotation, so nothing needs arming and the arming handlers have nothing to arm.
                # Only the standalone handlers mean anything here, and one of them has to say something.
                armed = {h: rungs for h, rungs in arming.items()
                         if (h in ENDINGS or h in SIGNALS) and rungs}
                if not armed:
                    refused["no rotation and nothing sayable in an ending or signal"] += 1
                    continue

            # A skill index is only meaningful against one npc's list, so an owner whose list cannot
            # answer every index the pattern uses is dropped -- not the whole pattern.
            wanted = {action[1] for branches in [cycle, *armed.values()]
                      for _, _, _, actions in branches
                      for action in actions if action[0] in ("skill", "skill_at", "skill_in_reach")}
            # A guard naming a skill index counts too. Without this an owner missing the skill keeps
            # the branch and answers the guard false forever, which reads as a mechanic that never
            # fires rather than as an npc that should not have had the branch.
            wanted |= {int(token.split(":")[1])
                       for branches in [cycle, *armed.values()]
                       for _, _, guards, _ in branches
                       for token in guards if token.startswith("skillready:")}
            if wanted:
                able = [n for n in owners if all(i in skills.get(n, {}) for i in wanted)]
                refused_owners += len(owners) - len(able)
                owners = able
            if not owners:
                refused["no npc here whose skill list answers the indices"] += 1
                continue

            patterns += 1
            for npc in owners:
                for handler, branches in [("cycle", cycle), *armed.items()]:
                    for index, priority, guards, actions in branches:
                        guards = [f"skillready:{skills[npc][int(g.split(':')[1])]}"
                                  if g.startswith("skillready:") else g
                                  for g in guards]
                        for order, action in enumerate(actions):
                            if action[0] in ("skill", "skill_at", "skill_in_reach"):
                                action = (action[0], skills[npc][action[1]]) + action[2:]
                            rows.append((npc, named.group(1), handler, index, priority,
                                         "|".join(guards), order) + action)

    rows.sort(key=lambda r: (r[0], r[2], r[3], r[6]))
    with args.out.open("w", encoding="utf-8", newline="\n") as out:
        out.write("npc\tpattern\thandler\tbranch\tpriority\tguards\torder\t"
                  "kind\ta1\ta2\ta3\tplace\tx\ty\tz\tgroup\n")
        for row in rows:
            out.write("\t".join(str(f) for f in row) + "\n")

    npcs = {r[0] for r in rows}
    print(f"{patterns} battle rotations across {len(npcs)} npcs, {len(rows)} actions -> {args.out}")
    if dropped:
        print(f"    {sum(dropped.values())} optional arming handlers dropped, rotation kept:")
        for reason, count in dropped.most_common(5):
            print(f"        {count:4d}  {reason}")
    if refused_owners:
        print(f"    {refused_owners} npcs dropped from a pattern their skill list cannot answer")
    for reason, count in refused.most_common():
        print(f"    {count:4d} refused: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
