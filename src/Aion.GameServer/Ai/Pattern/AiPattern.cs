using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine.Effects;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>A condition guarding one branch. Some of them mutate — see <see cref="When.FirstTime"/>.</summary>
public delegate bool PatternCondition(PatternAi ai);

/// <summary>One action in a branch's action list.</summary>
public delegate void PatternAction(PatternAi ai);

/// <summary>
/// One branch of a retail event handler: a priority, a guard, and what to do when the guard passes.
/// </summary>
/// <remarks>
/// <paramref name="Comment"/> carries the pattern's own comment where it has one. Several bosses ship
/// branches with no comment at all, so it is not a reliable label — it is here to make a table
/// readable against the digest, not to identify anything.
/// </remarks>
public sealed record PatternBranch(int Priority, string Comment, PatternCondition[] Conditions, PatternAction[] Actions);

/// <summary>
/// One NPC's translated AI pattern: the branches for each event we run, in evaluation order.
/// </summary>
/// <remarks>
/// Retail evaluates an event's branches highest priority first and stops at the first whose conditions
/// all pass, so order <em>is</em> the behaviour — a boss that runs its low-health chain instead of its
/// opening chain does so because those branches outrank it, not because anything disabled the others.
/// <see cref="Of"/> sorts on construction so a table can be written in whatever order reads best.
/// </remarks>
public sealed class AiPattern
{
    public static readonly PatternBranch[] None = Array.Empty<PatternBranch>();

    /// <summary>
    /// <c>on_wake_up</c> — runs once when the NPC enters the world, before anyone has touched it.
    /// </summary>
    /// <remarks>
    /// Encounters use this to put their furniture out: the spheres a boss makes players run between,
    /// the controllers that drive an add wave, the condition variables an instance reads later. It is
    /// not a combat event and does not wait for one.
    /// </remarks>
    public PatternBranch[] OnWakeUp { get; init; } = None;

    public PatternBranch[] OnEnterAttack { get; init; } = None;

    /// <summary>
    /// <c>on_attacked</c> — every hit, not just the one that starts the fight.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="OnEnterAttack"/>, which fires once per fight. Retail uses this for
    /// reactions that can happen at any moment — rounding on whoever just hit you, calling for help —
    /// and gates the once-only ones with a flag var of their own rather than by the event.
    /// </remarks>
    public PatternBranch[] OnAttacked { get; init; } = None;
    /// <summary>
    /// <c>on_spelled</c> — a skill landed on this NPC.
    /// </summary>
    /// <remarks>
    /// Retail's most-used handler after <c>on_attacked</c>: 1,170 patterns carry it. Almost every use
    /// pairs it with the same body as <c>on_attacked</c>, because a caster who never lands a melee blow
    /// would otherwise miss the reaction entirely — Vallakhan's illusions and the village killers both
    /// wanted it before the event existed. The caster is <see cref="PatternAi.LastCaster"/> for the
    /// duration of the branch.
    /// </remarks>
    public PatternBranch[] OnSpelled { get; init; } = None;

    public PatternBranch[] OnBattleTimer { get; init; } = None;
    public PatternBranch[] OnLeaveAttack { get; init; } = None;

    /// <summary>
    /// <c>on_talked_by_user</c> — a player opened a dialogue with this npc.
    /// </summary>
    /// <remarks>
    /// <b>Retail uses this for gates rather than for conversation.</b> The Raksha shortcut is the clearest
    /// case: talking to the trigger teleports you only when a world flag is set, and the flag is set by
    /// clearing the room. That mechanic is a talk branch guarded on a flag, and this port could express
    /// the flag for four passes without being able to express the talk.
    /// <para>
    /// The talking player is available to guards and actions as <see cref="PatternAi.Talker"/>.
    /// </para>
    /// </remarks>
    public PatternBranch[] OnTalk { get; init; } = None;

    /// <summary>
    /// <c>on_arrived_at_waypoint</c> — this npc reached a point on its walk path.
    /// </summary>
    /// <remarks>
    /// <b>Retail uses arrivals to advance state, not just to walk.</b> The silikor's roaming akaimum
    /// clears a per-npc flag here, and that clearing is what lets its <i>second</i> nearby-marker branch
    /// run and set the world flag the silikor consumes to dismiss it. Without an arrival hook the chain
    /// stops after one step, which is what this port did.
    /// </remarks>
    public PatternBranch[] OnArrivedAtWaypoint { get; init; } = None;
    public PatternBranch[] OnEnterIdle { get; init; } = None;

    /// <summary>
    /// <c>on_enter_return_sp</c> — the NPC has given up and started walking back to its spawn point.
    /// </summary>
    /// <remarks>
    /// Retail names this pair the way it names <c>on_enter_attack_state</c> / <c>on_leave_attack_state</c>,
    /// which is what settles what "leave return sp" means: it is leaving the returning <i>state</i>, not
    /// leaving in order to return. 38 patterns use the enter side and 103 the leave side.
    /// <para>
    /// The port already had both transitions, from the Java: <c>ReturningEventHandler.OnNotAtHome</c>
    /// sets <c>AIState.RETURNING</c> and <c>OnBackHome</c> sets <c>AIState.IDLE</c>. So these are the
    /// two edges of that state and needed no new machinery, only a call site.
    /// </para>
    /// </remarks>
    public PatternBranch[] OnEnterReturning { get; init; } = None;

    /// <summary>
    /// <c>on_leave_return_sp</c> — the NPC has finished returning and is idle at home again.
    /// </summary>
    /// <remarks>
    /// <b>98 of its 103 patterns spawn or cast</b>, the highest proportion of any handler this port had
    /// left unread — an npc that gets home and immediately puts something on the ground.
    /// </remarks>
    public PatternBranch[] OnLeaveReturning { get; init; } = None;
    public PatternBranch[] OnDie { get; init; } = None;

    /// <summary>
    /// <c>on_despawn</c> — the NPC is being removed, whether it was killed, timed out or cleared.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="OnDie"/>: a hazard with a <c>live_time</c> never dies, it expires, and
    /// 361 handlers across the 5.8 files hang work off that moment. Xasta's trap is the case that
    /// forced this — it tells him to make another one <em>as it goes</em>, so the chain that keeps his
    /// second form dropping traps runs entirely through this event.
    /// <para>
    /// Evaluated before the pattern resets, so a branch here still sees its timers, flags and spawn
    /// groups.
    /// </para>
    /// </remarks>
    public PatternBranch[] OnDespawn { get; init; } = None;

    /// <summary>
    /// <c>on_message</c> — how retail wires two NPCs of one encounter together.
    /// </summary>
    /// <remarks>
    /// Message numbers are chosen per encounter and have no global registry, so a table must guard
    /// every branch with <see cref="When.Message"/>. A boss and its adds have to be translated
    /// together: a broadcast nothing listens for, or a listener nothing broadcasts to, is silence.
    /// </remarks>
    public PatternBranch[] OnMessage { get; init; } = None;

    /// <summary>
    /// <c>on_stop_to_flee</c> — the NPC has finished running and is turning back round.
    /// </summary>
    /// <remarks>
    /// Retail hangs real work off this: across the 5.8 files its handlers carry 71 broadcasts, 69
    /// shouts and 66 casts. A boss that runs is not simply out of the fight for three seconds — it
    /// comes back shouting for help, or onto whoever is weakest. See <see cref="Do.Flee"/> and
    /// docs/retail-ai-fidelity.md.
    /// </remarks>
    public PatternBranch[] OnStopFleeing { get; init; } = None;

    /// <summary>
    /// <c>on_idle_timer</c> — the one timer that is not a battle timer.
    /// </summary>
    /// <remarks>
    /// There is a single idle slot rather than thirty, any event can set it, and unlike a battle
    /// timer it runs whether or not the NPC is fighting. Encounters use it for the things that
    /// happen around a fight: a controller removing itself once it has done its job, an orb
    /// calling out on a heartbeat, a boss counting down.
    /// </remarks>
    public PatternBranch[] OnIdleTimer { get; init; } = None;

    /// <summary>
    /// <c>on_see_npc</c> — another NPC came into view.
    /// </summary>
    /// <remarks>
    /// Almost every use is guarded by <see cref="When.SeenRace"/>: retail's village killers watch for
    /// a garrison chief and go for it rather than for whatever player is in front of them. The seen
    /// creature is <see cref="PatternAi.SeenCreature"/> for the duration of the branch.
    /// </remarks>
    public PatternBranch[] OnSeeNpc { get; init; } = None;

    /// <summary>
    /// <c>on_see_user</c> — a <em>player</em> came into view.
    /// </summary>
    /// <remarks>
    /// Retail keeps this separate from <see cref="OnSeeNpc"/> and the split matters: a trap that fires
    /// on seeing a player must not fire on seeing the guard standing next to it. The seen player is
    /// <see cref="PatternAi.SeenCreature"/> for the duration of the branch, as it is there.
    /// </remarks>
    public PatternBranch[] OnSeeUser { get; init; } = None;

    /// <summary>Retail's <c>on_see_user_move</c>: a player moved while inside this NPC's sight.</summary>
    /// <remarks>
    /// <b>Not the same event as <see cref="OnSeeUser"/> and not a substitute for it.</b> Seeing fires
    /// once, when the known list admits the player; moving fires again on every movement notification
    /// while they are there. 254 patterns carry it, and <b>14 of them have no <c>on_see_user</c> at
    /// all</b> -- for those, this is the only way anything happens when a raid walks up.
    /// <para>
    /// Retail guards most of these rungs itself: of 365 rungs, 111 test <c>is_npc_state</c> and 85 are
    /// behind a test-and-set <c>set_flag_var</c>. That is retail's own answer to an event that repeats,
    /// and it is carried across rather than second-guessed with a throttle invented here.
    /// </para>
    /// </remarks>
    public PatternBranch[] OnSeeUserMove { get; init; } = None;

    /// <summary>
    /// <c>on_see_friend_killed_by_user</c> — one of its own went down in front of it, to a player.
    /// </summary>
    /// <remarks>
    /// Retail has 129 patterns with this handler and aionemu has no event for it at all; see
    /// <see cref="FriendDeathNotice"/> for the event this server raises instead. What hangs off it is
    /// almost always <c>despawn_self</c>: retail uses it to make a group of adds leave together when
    /// the raid kills one of them in front of the rest, which is the counter-play to more than one
    /// escalating add ladder.
    /// </remarks>
    public PatternBranch[] OnFriendKilled { get; init; } = None;

    /// <summary>
    /// Retail's <c>on_see_friend_attacked</c> — 397 patterns in the 5.8 files, the largest handler
    /// this port had no event for. See <see cref="FriendCombatNotice"/>.
    /// </summary>
    public PatternBranch[] OnFriendAttacked { get; init; } = None;

    /// <summary>Retail's <c>on_friend_spelled</c> — 344 patterns, and nearly always the same body.</summary>
    public PatternBranch[] OnFriendSpelled { get; init; } = None;

    /// <summary>Builds a branch, sorting its conditions and actions as written.</summary>
    public static PatternBranch Branch(int priority, string comment, PatternCondition[] conditions, params PatternAction[] actions)
        => new PatternBranch(priority, comment, conditions, actions);

    /// <summary>Orders a set of branches the way retail evaluates them.</summary>
    public static PatternBranch[] Of(params PatternBranch[] branches)
        => branches.OrderByDescending(b => b.Priority).ToArray();
}

/// <summary>Branch guards, named after the pattern ops they translate.</summary>
public static class When
{
    /// <summary><c>is_battle_timer_indicator</c> — the branch belongs to timer slot <paramref name="index"/>.</summary>
    public static PatternCondition Timer(int index) => ai => ai.FiredTimer == index;

    /// <summary>
    /// <c>is_waypoint_index</c> — the NPC has just reached step <paramref name="index"/> of its route.
    /// </summary>
    /// <remarks>
    /// Retail counts its waypoints from one, and this port's route steps from zero, so the two differ by
    /// one and the conversion belongs here rather than in every table that uses it. <b>1,072 branches
    /// across the 5.8 dump are guarded on this</b> and none of them could be expressed before: a patrol
    /// that shouts at the third corner and turns at the fifth is entirely made of these.
    /// </remarks>
    public static PatternCondition AtWaypoint(int index) => ai => ai.WaypointIndex == index - 1;

    /// <summary><c>is_last_waypoint</c> — the NPC has reached the end of its route.</summary>
    /// <remarks>
    /// The 98 branches guarded on this are mostly one action: the NPC removes itself. A patrol whose
    /// route has run out and which cannot say so <b>stands at the end of it, or loops back</b>, and the
    /// difference is a permanent extra NPC in the room.
    /// </remarks>
    public static readonly PatternCondition AtLastWaypoint = ai => ai.AtRouteEnd;

    /// <summary><c>is_skill_count_left</c> — the NPC still has this skill available to cast.</summary>
    /// <remarks>
    /// 832 uses, and in every one the branch it guards goes on to cast that same skill. So the
    /// question is "can I use it now", and this port answers it with the skill's own cooldown:
    /// <c>NpcSkillEntry.HasCooldown()</c> compares the template cooldown against when it was last
    /// used. An entry the NPC does not have is not available.
    /// <para>
    /// <b>Retail's word is "count", not "cooldown", and the difference is worth stating.</b> If retail
    /// means a per-fight budget rather than a recharge, this is a close reading and not an exact one —
    /// a boss would get its skill back where retail had spent it for good. Nothing in this port models
    /// a budget, so cooldown is the only truthful answer available, and it is the conservative one:
    /// both readings agree that a skill just used is unavailable, and they differ only in how long.
    /// </para>
    /// <para>
    /// The guard is not redundant against the cast path's own readiness check. Branch lists are
    /// first-match-wins, so a branch taken on an unavailable skill silently swallows the branches below
    /// it — the cast fails quietly and the NPC does nothing, where retail would have run the next rung.
    /// </para>
    /// </remarks>
    public static PatternCondition SkillReady(int skillId) => ai => ai.SkillAvailable(skillId);

    /// <summary><c>is_event_skill_id</c> — the skill that just landed on me is this one.</summary>
    /// <remarks>
    /// Only meaningful inside <c>on_spelled</c>, which is the only handler retail uses it in.
    /// Outside one <see cref="PatternAi.SpelledSkillId"/> is 0 and this is false, so a branch cannot
    /// leak into another handler.
    /// <para>
    /// <b>Retail names the skill, not the id</b> — <c>DGRA_SatkBig_TA</c> — and the extractor resolves
    /// that through <c>skill_base.xml</c>'s own <c>&lt;id&gt;</c>. 61 of the 65 names it uses resolve
    /// to a skill this port has a template for; the four that do not are 5.8-only and are refused
    /// rather than pointed at a skill that would never arrive.
    /// </para>
    /// </remarks>
    public static PatternCondition EventSkill(int skillId) => ai => ai.SpelledSkillId == skillId;

    /// <summary>
    /// <c>is_event_skill_category</c> — the skill this event carries is of that kind, rather than that
    /// exact skill.
    /// </summary>
    /// <remarks>
    /// Almost every use is on <c>on_friend_spelled</c>: a support npc watching for its friend to be
    /// debuffed or healed, and answering the kind rather than the id. Retail asks four —
    /// <c>PHYSICAL_DEBUFF</c>, <c>MENTAL_DEBUFF</c>, <c>HEAL</c>, <c>CHAIN_SKILL</c>.
    /// <para>
    /// The category comes from retail's own <c>skill_base.xml</c> and cannot be derived from this
    /// port's skill data; see <see cref="Dataholders.SkillCategoryData"/>.
    /// </para>
    /// </remarks>
    public static PatternCondition EventSkillCategory(SkillCategory category)
        => ai => category != SkillCategory.NONE
            && DataManager.SKILL_CATEGORY_DATA.Of(ai.SpelledSkillId) == category;

    /// <summary><c>is_npc_state</c> — what the NPC is doing right now.</summary>
    /// <remarks>
    /// 2,834 uses in the 5.8 dump, every one of them asking about <c>NPCI_SELF</c>, which is why there
    /// is no subject parameter here. Retail leans on this to keep a branch from firing out of context:
    /// a friend-attacked rung that should only answer while the NPC is patrolling, a shout that should
    /// only happen mid-fight.
    /// <para>
    /// <b>Two of retail's eight states are deliberately not here.</b> <c>NPC_STATE_WAKE_UP</c> (336
    /// uses) is a state this port does not have — waking is a moment in <c>HandleSpawned</c>, not a
    /// condition an NPC sits in, so there is nothing truthful to test. <c>NPC_STATE_FLEE</c> (21) has
    /// <c>AIState.FEAR</c> nearby, but fear here is the abnormal effect, and equating "running away at
    /// low health" with "feared by a skill" would be a guess dressed as a mapping. Both are refused by
    /// the extractor rather than approximated.
    /// </para>
    /// </remarks>
    /// <summary><c>NPC_STATE_IDLE</c> — standing about, as retail means it.</summary>
    /// <remarks>
    /// <b>Not the same predicate as <see cref="Idle"/>, which is why both exist.</b> <c>Idle</c> is
    /// <c>!InCombat</c>, written for the hand-written classes that only ever ask "fighting or not";
    /// an NPC walking its route satisfies it. Retail's <c>NPC_STATE_IDLE</c> does not — patrolling is
    /// <c>NPC_STATE_GOTO_WAYPOINT</c>, a different state — so a table using the loose one would fire
    /// 16 branches at patrolling NPCs that retail keeps for NPCs standing still.
    /// <para>
    /// <c>Idle</c> is left as it is rather than tightened: four hand-written classes lean on it, and
    /// narrowing a condition underneath them would change encounters this commit is not about.
    /// </para>
    /// </remarks>
    public static readonly PatternCondition Idling = ai => ai.IsInState(AIState.IDLE);

    /// <summary><c>NPC_STATE_GOTO_WAYPOINT</c> — walking its own route.</summary>
    public static readonly PatternCondition WalkingItsRoute =
        ai => ai.IsInState(AIState.WALKING) && ai.IsInSubState(AISubState.WALK_PATH);

    /// <summary><c>NPC_STATE_RANDOM_MOVE</c> — wandering rather than following a route.</summary>
    public static readonly PatternCondition WanderingAtRandom =
        ai => ai.IsInState(AIState.WALKING) && ai.IsInSubState(AISubState.WALK_RANDOM);

    /// <summary><c>NPC_STATE_GOTO_POINT</c> — sent to one place, off its route.</summary>
    public static readonly PatternCondition MovingToAPoint = ai => ai.IsInState(AIState.FORCED_WALKING);

    /// <summary><c>NPC_STATE_USE_SKILL</c> — mid-cast.</summary>
    public static readonly PatternCondition Casting = ai => ai.IsInSubState(AISubState.CAST);

    /// <summary><c>is_hp_lower_than</c> — true on every evaluation below the threshold, not just the first.</summary>
    public static PatternCondition HpBelow(int percent) => ai => ai.HpPercent < percent;

    /// <summary><c>is_hp_in_boundary</c> — inclusive at both ends, which is how retail's regimes tile.</summary>
    public static PatternCondition HpBetween(int low, int high) => ai => ai.HpPercent >= low && ai.HpPercent <= high;

    /// <summary><c>is_hp_lower_than</c> about somebody other than this NPC.</summary>
    /// <remarks>
    /// <c>is_hp_lower_than</c> is 6,386 uses and 6,048 of them ask about <c>OBJI_SELF</c>, which
    /// <see cref="HpBelow"/> has always answered. The other 338 ask about a creature the event names,
    /// and the extractor refused them — correctly, since emitting <c>HpBelow</c> would have read this
    /// NPC's health instead of theirs.
    /// <para>
    /// <b><c>OBJI_FRIEND</c> is 314 of the 338</b>, and it lives entirely in <c>on_see_friend_attacked</c>
    /// (165) and <c>on_friend_spelled</c> (149) — a healer deciding whether the friend is hurt enough to
    /// be worth helping. <see cref="PatternAi.Friend"/> is set for exactly the span of those two
    /// handlers, so the question is answerable precisely where retail asks it and nowhere else — and
    /// <see cref="FriendHpBelow"/> was already written for a hand-written class, which is the sixth
    /// time in this stretch of work that the runtime turned out to know a word the parser refused.
    /// </para>
    /// <para>
    /// A role that is not set answers <b>false</b>, which is the opposite of the choice
    /// <see cref="SkillReady"/> makes and for a different reason. There, an absent entry meant "this
    /// port has no cooldown data", so blocking the branch would have destroyed a working mechanic.
    /// Here an absent role means the event genuinely has no such creature, and "somebody who is not
    /// there is below 30% health" is not a true statement about anything.
    /// </para>
    /// <para>
    /// <c>OBJI_PARTY_MEMBER</c> (2 uses) is refused: this port has no party-member role on an NPC
    /// pattern, and the nearest creature to hand would be a guess.
    /// </para>
    /// </remarks>
    /// <summary><c>is_hp_lower_than who=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetHpBelow(int percent)
        => ai => ai.CurrentTarget is Creature who && who.GetLifeStats().GetHpPercentage() < percent;

    /// <summary><c>is_hp_lower_than who=OBJI_SEEN</c>.</summary>
    public static PatternCondition SeenHpBelow(int percent)
        => ai => ai.SeenCreature is Creature who && who.GetLifeStats().GetHpPercentage() < percent;

    /// <summary><c>is_hp_lower_than who=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterHpBelow(int percent)
        => ai => ai.LastCaster is Creature who && who.GetLifeStats().GetHpPercentage() < percent;

    /// <summary><c>is_hp_lower_than who=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerHpBelow(int percent)
        => ai => ai.LastAttacker is Creature who && who.GetLifeStats().GetHpPercentage() < percent;

    /// <summary><c>is_hp_lower_than who=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternCondition MessageSenderHpBelow(int percent)
        => ai => ai.MessageSender is Creature who && who.GetLifeStats().GetHpPercentage() < percent;

    /// <summary>
    /// <c>set_flag_var</c> in a branch's conditions: passes only the first time, and consumes the flag.
    /// </summary>
    /// <remarks>
    /// This is what makes a threshold branch a step rather than a regime. It sits in the condition list
    /// because it is a test-and-set, and it must be evaluated in the position the pattern wrote it —
    /// a guard ahead of it that fails has to leave the flag alone.
    /// </remarks>
    public static PatternCondition FirstTime(int flag) => ai => ai.TestAndSetFlag(flag);

    /// <summary><c>unset_flag_var</c> in conditions: the mirror of <see cref="FirstTime"/>.</summary>
    public static PatternCondition Consuming(int flag) => ai => ai.TestAndUnsetFlag(flag);

    /// <summary>
    /// <c>set_world_flag_var</c>: <see cref="FirstTime"/> shared by every npc in the map instance.
    /// </summary>
    /// <remarks>
    /// <b>A separate flag space from the per-npc one</b>, matching retail — several patterns use the
    /// same <c>FLAGVARI_</c> name in both scopes within one handler, and they are different variables.
    /// See <see cref="WorldFlags"/> for why the scope is the instance rather than the server.
    /// </remarks>
    public static PatternCondition FirstTimeInWorld(int flag)
        => ai => WorldFlags.TestAndSet(ai.GetOwner().GetWorldMapInstance(), flag);

    /// <summary><c>unset_world_flag_var</c>: the mirror, and the half that lets one npc arm another.</summary>
    public static PatternCondition ConsumingWorld(int flag)
        => ai => WorldFlags.TestAndUnset(ai.GetOwner().GetWorldMapInstance(), flag);

    /// <summary><c>is_world_flag_var</c>: reads the shared flag without touching it.</summary>
    public static PatternCondition WorldFlagSet(int flag)
        => ai => WorldFlags.IsSet(ai.GetOwner().GetWorldMapInstance(), flag);


    /// <summary><c>test_probability</c>.</summary>
    public static PatternCondition Chance(int percent) => ai => ai.RollPercent(percent);

    /// <summary>
    /// <c>increase_intvar</c> — bumps one of retail's four counters and asks where it landed. Evaluating
    /// this <b>increments</b>; see <see cref="PatternAi.IncreaseIntVar"/> for what the bound flag means
    /// and why that reading is written down.
    /// </summary>
    public static PatternCondition Counting(int slot, int lower, int upper, bool onlyAtBound = true)
        => ai => ai.IncreaseIntVar(slot, lower, upper, onlyAtBound);

    /// <summary><c>add_intvar</c> — <see cref="Counting"/> by a step retail names rather than by one.</summary>
    /// <remarks>
    /// 153 uses, carrying the identical bound fields and differing only in <c>var_to_add</c>. The step
    /// is not always one: 12 uses add 550, which is retail saying "this counts for a lot" rather than
    /// stepping through a range, and collapsing it to an increment would fire those rungs on the wrong
    /// pass.
    /// </remarks>
    public static PatternCondition CountingBy(int slot, int step, int lower, int upper,
        bool onlyAtBound = true)
        => ai => ai.AddToIntVar(slot, step, lower, upper, onlyAtBound);

    /// <summary>
    /// <c>set_intvar_if_less_than</c>: passes when counter <paramref name="counter"/> is below
    /// <paramref name="comparand"/>, and on passing sets it to <paramref name="setTo"/>.
    /// </summary>
    /// <remarks>
    /// Retail's counters are how a boss knows how many of its summons are still standing. The pattern
    /// sets the counter to the number it is about to spawn, each summon decrements it as it dies, and
    /// this condition asks "are they all gone?" — passing sets the counter to the size of the wave the
    /// branch is about to place, so the test and the bookkeeping are one step.
    /// <para>
    /// Like <see cref="FirstTime"/> this mutates when it passes, so it has to be evaluated in written
    /// order: a health band or timer guard ahead of it that fails must leave the counter alone.
    /// </para>
    /// </remarks>
    public static PatternCondition CountBelow(int counter, int comparand, int setTo)
        => ai => ai.TestAndSetCounterIfBelow(counter, comparand, setTo);

    /// <summary>
    /// <c>set_intvar_if_larger_than</c>: the mirror of <see cref="CountBelow"/>, and the branch that
    /// answers "some are still alive".
    /// </summary>
    public static PatternCondition CountAbove(int counter, int comparand, int setTo)
        => ai => ai.TestAndSetCounterIfAbove(counter, comparand, setTo);

    /// <summary>
    /// Reads a counter without changing it: passes when it equals <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// This is how <c>increase_intvar</c> with <c>be_true_only_when_hit_the_bound=TRUE</c> is
    /// expressed. Retail writes that element as a <em>condition</em>, so the same event's five branches
    /// would each increment the counter as they were tried — which cannot be what a five-wave ladder
    /// spaced two apart means, and no reading of it produces the five evenly spaced waves the
    /// designer's own comments describe. Split instead: the branches test with this, and the counter is
    /// advanced by <see cref="Do.Increment"/> as an action inside whichever branch runs. See
    /// docs/retail-ai-fidelity.md.
    /// </remarks>
    public static PatternCondition CountEquals(int counter, int value)
        => ai => ai.CounterEquals(counter, value);

    /// <summary>
    /// <c>decrease_intvar</c> with <c>be_true_only_when_hit_the_bound=FALSE</c>: takes one off the
    /// counter, holds it inside <paramref name="low"/>..<paramref name="high"/>, and always passes.
    /// </summary>
    /// <remarks>
    /// The clamp is the point. A summon that dies after the boss has already re-armed the wave must not
    /// drive the count negative, or the "are they all gone" test above would stop passing.
    /// <para>
    /// The <c>TRUE</c> variant — pass only on reaching the bound — is the more common one in the retail
    /// files and is deliberately not implemented; no ported pattern uses it, and it would ship untested.
    /// The same goes for <c>increase_intvar</c>, <c>add_intvar</c> and <c>sub_intvar</c>.
    /// </remarks>
    public static PatternCondition Decrement(int counter, int low, int high)
        => ai => ai.DecrementCounter(counter, low, high);

    /// <summary>
    /// <c>is_distance_longer_than(OBJI_MESSAGE_PARAM, n)</c> — the object the message carried is
    /// further away than <paramref name="metres"/>.
    /// </summary>
    /// <remarks>
    /// Retail uses this to make a call selective: Kistenian shouts to seventy-five metres every three
    /// seconds, and only the spirits that have drifted more than twenty metres from him answer it, so
    /// the call pulls stragglers back rather than churning the whole pack.
    /// </remarks>
    public static PatternCondition MessageParamFartherThan(int metres) => ai =>
        ai.MessageParam is Aion.GameServer.Model.GameObjects.VisibleObject param
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), param, metres);

    /// <summary><c>is_npc_state(NPCI_SELF, NPC_STATE_ATTACK)</c> — already in a fight.</summary>
    /// <summary>
    /// <c>is_user</c> on a death handler — true when a player did the damage that mattered.
    /// </summary>
    /// <remarks>
    /// Retail guards its death rewards with this so that only a kill by players pays out. Tiamat
    /// Stronghold's siege weapons are the clearest case: each leaves a usable cannon behind, and without
    /// the guard a reset or a cleanup would litter the field with artillery nobody earned.
    /// <para>
    /// Backed by <see cref="PatternAi.Killer"/>, which is whoever did the most <em>player</em> damage. A
    /// death with no player damage recorded is therefore not a user kill, which is the intended reading
    /// and also the reason this cannot be exercised through <c>BossAiHarness.Kill</c> — that records no
    /// damage on purpose, because the reward path it would otherwise run needs a database.
    /// </para>
    /// </remarks>
    public static PatternCondition KilledByPlayer => ai => ai.Killer != null;

    /// <summary><c>on_killed_by_npc</c>: something killed it and that something was an npc.</summary>
    public static PatternCondition KilledByNpc => ai => ai.NpcKiller != null;

    /// <summary><c>is_user_flying user=USERI_EVENT_TARGET</c>: whoever opened the fight is airborne.</summary>
    public static PatternCondition EventTargetFlying => ai => ai.IsAirborne(ai.EventTarget);

    /// <summary><c>is_user_flying user=USERI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerFlying => ai => ai.IsAirborne(ai.LastAttacker);

    /// <summary><c>is_user_flying user=USERI_CASTER</c>.</summary>
    public static PatternCondition CasterFlying => ai => ai.IsAirborne(ai.LastCaster);

    /// <summary><c>is_user_flying user=USERI_SEEN</c>.</summary>
    public static PatternCondition SeenFlying => ai => ai.IsAirborne(ai.SeenCreature as Creature);

    public static PatternCondition Fighting => ai => ai.InCombat;

    /// <summary><c>is_npc_state(NPCI_SELF, NPC_STATE_IDLE)</c> — standing about.</summary>
    public static PatternCondition Idle => ai => !ai.InCombat;

    /// <summary><c>is_message</c> — this branch belongs to one designer-assigned message number.</summary>
    /// <summary>
    /// <c>is_distance_shorter_than who=OBJI_CUR_TARGET</c> — the NPC it is fighting is inside
    /// <paramref name="metres"/>.
    /// </summary>
    /// <remarks>
    /// Retail uses this to make a branch melee-only. The krall trappers' escape rung is the case that
    /// needed it: they lay their heavy trap and run <em>only</em> when something is standing on top of
    /// them at six metres, so a ranged group never sees it. See docs/retail-ai-fidelity.md.
    /// </remarks>
    public static PatternCondition TargetWithin(int metres) => ai =>
        ai.CurrentTarget is Aion.GameServer.Model.GameObjects.Creature target
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), target, metres);

    /// <summary><c>is_distance_shorter_than</c> about somebody other than the current target.</summary>
    /// <remarks>
    /// The mirror of the <c>is_distance_longer_than</c> family, and the half that was left behind:
    /// <see cref="TargetWithin"/> covered 194 of the element's 257 uses and the other 63 named the
    /// attacker, the killer, the message parameter, the caster or the message sender.
    /// <para>
    /// Same null rule as the "beyond" family, and it matters in the opposite direction. An absent role
    /// answers <b>false</b> — nobody is not close by — so a melee-only rung does not fire at an empty
    /// room. <c>OBJI_SELF</c> (3 uses) is refused: the distance from an NPC to itself is zero, so the
    /// branch is always true and is decided at build time rather than at run time.
    /// </para>
    /// </remarks>
    /// <summary><c>is_distance_shorter_than who=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerWithin(int metres) => ai =>
        ai.LastAttacker is Aion.GameServer.Model.GameObjects.Creature who
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_shorter_than who=OBJI_KILLER</c>.</summary>
    public static PatternCondition KillerWithin(int metres) => ai =>
        ai.Killer is Aion.GameServer.Model.GameObjects.Creature who
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_shorter_than who=OBJI_MESSAGE_PARAM</c>.</summary>
    public static PatternCondition MessageParamWithin(int metres) => ai =>
        ai.MessageParam as Creature is Aion.GameServer.Model.GameObjects.Creature who
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_shorter_than who=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterWithin(int metres) => ai =>
        ai.LastCaster is Aion.GameServer.Model.GameObjects.Creature who
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_shorter_than who=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternCondition MessageSenderWithin(int metres) => ai =>
        ai.MessageSender is Aion.GameServer.Model.GameObjects.Creature who
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than</c> — the named creature is further away than this.</summary>
    /// <remarks>
    /// 592 uses, and the mirror of <see cref="TargetWithin"/>: retail uses one to make a branch
    /// melee-only and the other to make it a ranged answer — a caster that steps back and casts only
    /// when the tank has drifted out of reach.
    /// <para>
    /// <b>These are not <c>!TargetWithin(n)</c>, and writing them that way would have been wrong.</b>
    /// <c>TargetWithin</c> answers false when there is no target at all, so its negation answers
    /// <i>true</i> — "nobody is further than ten metres" would fire the branch at an empty room, and
    /// on an <c>on_enter_attack_state</c> rung that is exactly the moment the target may not be set
    /// yet. Each of these requires the creature to exist first.
    /// </para>
    /// <para>
    /// <c>OBJI_SELF</c> (1 use) is refused by the extractor: the distance from an NPC to itself is
    /// zero, so the branch can never fire and emitting it would be emitting a rung that is dead by
    /// construction.
    /// </para>
    /// </remarks>
    /// <summary><c>is_distance_longer_than who=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetBeyond(int metres) => ai =>
        ai.CurrentTarget is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than who=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetBeyond(int metres) => ai =>
        ai.EventTarget is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than who=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerBeyond(int metres) => ai =>
        ai.LastAttacker is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than who=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterBeyond(int metres) => ai =>
        ai.LastCaster is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than who=OBJI_MESSAGE_PARAM</c>.</summary>
    public static PatternCondition MessageParamBeyond(int metres) => ai =>
        ai.MessageParam as Creature is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary><c>is_distance_longer_than who=OBJI_SEEN</c>.</summary>
    public static PatternCondition SeenBeyond(int metres) => ai =>
        ai.SeenCreature is Aion.GameServer.Model.GameObjects.Creature who
        && !Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), who, metres);

    /// <summary>
    /// <c>is_race from=OBJI_SEEN</c> — the NPC that just came into view is one of these races.
    /// </summary>
    /// <remarks>
    /// <b>This guard was read as unusable for months.</b> `is_race` appeared in summaries with no
    /// argument, so a comment in <see cref="PatternAi"/> recorded it as carrying nothing readable and
    /// the akaimum was discriminated by npc id instead. Every one of the <b>2,879</b> `is_race`
    /// conditions in the 5.8 files carries a `race_type`; the summariser was dropping it. See
    /// docs/retail-ai-fidelity.md.
    /// </remarks>
    /// <summary>
    /// <c>is_enemy who=OBJI_SEEN</c> — the creature that just came into view is hostile to this NPC.
    /// </summary>
    public static PatternCondition Enemy
        => ai => ai.SeenCreature is Creature seen && seen.IsEnemy(ai.GetOwner());

    /// <summary><c>is_enemy who=OBJI_CUR_TARGET</c> — this NPC's current target is hostile to it.</summary>
    public static PatternCondition TargetIsEnemy
        => ai => ai.CurrentTarget is Creature target && target.IsEnemy(ai.GetOwner());

    /// <summary><c>is_enemy who=OBJI_CASTER</c> — whoever just cast on this NPC is hostile to it.</summary>
    public static PatternCondition CasterIsEnemy
        => ai => ai.LastCaster is Creature caster && caster.IsEnemy(ai.GetOwner());

    /// <summary>
    /// <c>is_hp_in_boundary who=OBJI_CUR_TARGET</c> — the player this NPC is fighting is inside this
    /// health band.
    /// </summary>
    /// <remarks>
    /// The bastion drudges are the clearest use: they run only from an attacker still above forty
    /// percent. <b>A drudge that has nearly killed its attacker stays and finishes the job</b>, which
    /// is a judgement about the fight rather than about itself, and the only guard in this log that
    /// reads the player's health rather than the npc's.
    /// </remarks>
    public static PatternCondition TargetHpBetween(int low, int high)
        => ai => ai.CurrentTarget is Creature target
            && target.GetLifeStats().GetHpPercentage() >= low
            && target.GetLifeStats().GetHpPercentage() <= high;

    /// <summary><c>is_hp_in_boundary who=OBJI_FRIEND</c> — the friend is inside this band.</summary>
    /// <remarks>
    /// The boundary family exists for the same reason <see cref="TargetHpBetween"/> does: retail asks
    /// about a band rather than a threshold when the answer should stop being true again. Retail names
    /// four subjects besides itself — friend, attacker, caster and current target — and this port could
    /// answer only the last.
    /// <para>
    /// An absent subject answers false, as everywhere else in this family: "somebody who is not there
    /// is between 40 and 60 percent" is not true about anything.
    /// </para>
    /// </remarks>
    public static PatternCondition FriendHpBetween(int low, int high)
        => ai => ai.Friend is Creature friend
            && friend.GetLifeStats().GetHpPercentage() >= low
            && friend.GetLifeStats().GetHpPercentage() <= high;

    /// <summary><c>is_hp_in_boundary who=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerHpBetween(int low, int high)
        => ai => ai.LastAttacker is Creature attacker
            && attacker.GetLifeStats().GetHpPercentage() >= low
            && attacker.GetLifeStats().GetHpPercentage() <= high;

    /// <summary><c>is_hp_in_boundary who=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterHpBetween(int low, int high)
        => ai => ai.LastCaster is Creature caster
            && caster.GetLifeStats().GetHpPercentage() >= low
            && caster.GetLifeStats().GetHpPercentage() <= high;

    /// <summary><c>is_hp_lower_than who=OBJI_FRIEND</c> — the friend taking the hit is below this.</summary>
    public static PatternCondition FriendHpBelow(int percent)
        => ai => ai.Friend is Creature friend
            && friend.GetLifeStats().GetHpPercentage() < percent;

    /// <summary>
    /// <c>is_enemy who=OBJI_CASTER</c> inside a friend-spelled branch — the caster hitting a friend is
    /// hostile to this NPC.
    /// </summary>
    public static PatternCondition FriendsAttackerIsEnemy
        => ai => ai.FriendsAttacker is Creature attacker && attacker.IsEnemy(ai.GetOwner());

    /// <summary>
    /// <c>is_enemy who=OBJI_MESSAGE_PARAM</c> — whoever a message named is hostile to this NPC.
    /// </summary>
    /// <remarks>
    /// The fortress guards' whole answer hangs on this. Their call goes out on a shared number that
    /// both factions' guards use, so an Elyos guard hears an Asmodian guard's cry and this guard is
    /// what stops it answering — it does not check who spoke, it checks whether the player named is
    /// its enemy.
    /// </remarks>
    public static PatternCondition MessageParamIsEnemy
        => ai => ai.MessageParam is Creature named && named.IsEnemy(ai.GetOwner());

    /// <summary><c>is_enemy</c> for the other six roles retail asks about.</summary>
    /// <remarks>
    /// 1,156 uses in the 5.8 dump, and <b>the extractor refused every one of them</b> — including the
    /// 1,121 whose condition was already sitting here. <see cref="MessageParamIsEnemy"/>,
    /// <see cref="Enemy"/>, <see cref="TargetIsEnemy"/> and <see cref="CasterIsEnemy"/> were all
    /// written for hand-written classes and none had ever been emitted into a generated table. Only
    /// the attacker, the message sender and the event target genuinely needed adding, and they are the
    /// 35 rarest uses. This is the same miss as <c>is_waypoint_index</c>: a refusal message naming an
    /// element the parser does not know, read as an element the runtime cannot answer.
    /// <para>
    /// All six read a role <see cref="PatternAi"/> already tracks and all six use the same hostility
    /// test as the two that came before — <c>Creature.IsEnemy</c> — so nothing here decides what
    /// "enemy" means. A null role is not hostile, which matters because these fire from handlers where
    /// the role may legitimately be empty: an <c>on_spelled</c> branch asking about the caster runs
    /// again later when the caster has been forgotten.
    /// </para>
    /// </remarks>
    /// <summary><c>is_enemy who=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerIsEnemy
        => ai => ai.LastAttacker is Creature role && role.IsEnemy(ai.GetOwner());

    /// <summary><c>is_enemy who=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternCondition MessageSenderIsEnemy
        => ai => ai.MessageSender is Creature role && role.IsEnemy(ai.GetOwner());

    /// <summary><c>is_enemy who=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetIsEnemy
        => ai => ai.EventTarget is Creature role && role.IsEnemy(ai.GetOwner());

    public static PatternCondition SeenRace(params Race[] races)
        => ai => ai.SeenCreature is Creature seen && races.Contains(seen.GetRace());

    /// <summary><c>is_race</c> for the roles that had no condition yet.</summary>
    /// <remarks>
    /// 2,855 uses, the largest single condition this port had never read. Retail leans on it to make a
    /// branch answer only one faction: a fortress guard that shouts at Elyos and ignores Asmodians is
    /// one rung with a race check on it, not two npcs.
    /// <para>
    /// <see cref="SeenRace"/>, <see cref="TargetRace"/>, <see cref="CasterRace"/> and
    /// <see cref="AttackerRace"/> were already here for hand-written classes. The killer (896 uses) and
    /// the talker (464) are the two biggest subjects and had neither.
    /// </para>
    /// <para>
    /// <b>Retail's <c>race_type</c> is matched to <see cref="Race"/> by exact name, uppercased, and
    /// nothing else.</b> <c>pc_light</c> and <c>pc_dark</c> are the two aliases spelled out, because
    /// they are the most-used values and mean <c>ELYOS</c> and <c>ASMODIANS</c>; every other value has
    /// to name a member of the enum or the branch is refused. That rules out guessing that, say,
    /// <c>lizardman</c> and <c>ratman</c> are interchangeable, which is the sort of thing a fuzzy match
    /// would decide silently.
    /// </para>
    /// </remarks>
    /// <summary><c>is_race from=OBJI_KILLER</c>.</summary>
    public static PatternCondition KillerRace(params Race[] races)
        => ai => ai.Killer is Creature who && races.Contains(who.GetRace());

    /// <summary><c>is_race from=OBJI_TALKER</c>.</summary>
    public static PatternCondition TalkerRace(params Race[] races)
        => ai => ai.Talker is Creature who && races.Contains(who.GetRace());

    /// <summary><c>is_race from=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetRace(params Race[] races)
        => ai => ai.EventTarget is Creature who && races.Contains(who.GetRace());

    /// <summary><c>is_race from=OBJI_MESSAGE_PARAM</c>.</summary>
    public static PatternCondition MessageParamRace(params Race[] races)
        => ai => ai.MessageParam as Creature is Creature who && races.Contains(who.GetRace());

    /// <summary><c>is_race from=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternCondition MessageSenderRace(params Race[] races)
        => ai => ai.MessageSender is Creature who && races.Contains(who.GetRace());

    /// <summary>
    /// <c>is_user_class user=USERI_ATTACKER</c> — the player who just hit this NPC is one of these
    /// classes.
    /// </summary>
    /// <remarks>
    /// Retail's threat assistance for tanks. The Catacombs bosses give a <c>CLASSI_KNIGHT</c> attacker
    /// thousands of extra hate points on every blow, which is how a templar holds a boss without the
    /// player noticing anything happened.
    /// <para>
    /// <c>CLASSI_KNIGHT</c> is <see cref="PlayerClass.TEMPLAR"/> — not a guess, the enum already
    /// carries the client's own naming in its comments (<c>TEMPLAR, // knight</c>, beside
    /// <c>GLADIATOR, // fighter</c> and <c>SORCERER, // wizard</c>).
    /// </para>
    /// <para>
    /// Only a player has a class, so an NPC attacker fails this guard rather than matching a default.
    /// </para>
    /// </remarks>
    public static PatternCondition AttackerClass(params PlayerClass[] classes)
        => ai => ai.LastAttacker is Player hitter && classes.Contains(hitter.GetPlayerClass());

    /// <summary><c>is_user_class</c> for the other subjects retail names.</summary>
    /// <remarks>
    /// 185 uses across five subjects, of which <see cref="AttackerClass"/> covered one. Retail asks it
    /// of the creature seen (61), the attacker (54), the caster (44), the event target (22) and the
    /// talker (4).
    /// <para>
    /// <b>Retail's class *groups* are the interesting half, and three of them are derivable rather
    /// than guessed.</b> <c>PlayerClassExtensions</c> carries <c>StartingClass</c>, so
    /// <c>CLASSI_MAGE_GROUP</c> is exactly the classes whose starting class is <see cref="PlayerClass.MAGE"/>
    /// -- mage, sorcerer, spirit master -- and the same for the warrior, scout and cleric branches.
    /// That is reading the class tree this port already holds, not inventing one.
    /// </para>
    /// <para>
    /// <b><c>CLASSI_CASTER_GROUP</c> (30) and <c>CLASSI_MELEE_GROUP</c> (29) are refused.</b> Nothing
    /// here says whether a cleric is a caster or a ranger is melee, and both readings are defensible
    /// -- which is exactly the problem. A guard that admits one class too many fires for a player it
    /// should ignore, and a boss that answers a chanter as if it were a sorcerer looks like a boss
    /// working. <c>CLASSI_NONE</c> (6) is refused too: it names no class at all.
    /// </para>
    /// <para>
    /// Only a player has a class, so an NPC in the role fails these rather than matching a default.
    /// </para>
    /// </remarks>
    /// <summary><c>is_user_class user=USERI_SEEN</c>.</summary>
    public static PatternCondition SeenClass(params PlayerClass[] classes)
        => ai => ai.SeenCreature is Player who && classes.Contains(who.GetPlayerClass());

    /// <summary><c>is_user_class user=USERI_CASTER</c>.</summary>
    public static PatternCondition CasterClass(params PlayerClass[] classes)
        => ai => ai.LastCaster is Player who && classes.Contains(who.GetPlayerClass());

    /// <summary><c>is_user_class user=USERI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetClass(params PlayerClass[] classes)
        => ai => ai.EventTarget is Player who && classes.Contains(who.GetPlayerClass());

    /// <summary><c>is_user_class user=USERI_TALKER</c>.</summary>
    public static PatternCondition TalkerClass(params PlayerClass[] classes)
        => ai => ai.Talker is Player who && classes.Contains(who.GetPlayerClass());

    /// <summary><c>is_obj_in_abnormal_state</c> — the named creature is under this effect.</summary>
    /// <remarks>
    /// 157 uses, and <b>only 25 of them name a state this port has</b>. The rest are retail's *group*
    /// indicators — <c>ABNSTATEI_PHYSICAL_GROUP</c> (63), <c>ABNSTATEI_MENTAL_GROUP</c> (37),
    /// <c>ABNSTATEI_CANNOT_ACT_GROUP</c> (8) — plus <c>SANCTUARY</c> (16), <c>INVISIBLE</c> (4) and
    /// <c>DEFORM</c> (4), which <see cref="AbnormalState"/> does not carry at all.
    /// <para>
    /// <b>The groups are refused rather than approximated, and that is not a small call.</b> Deciding
    /// that "physical" means stun, stumble and stagger but not root, or that "mental" covers fear and
    /// sleep and charm, is inventing retail's taxonomy from the names — and a boss whose branch fires
    /// for one effect too many looks exactly like a boss working. See the doc entry: the same shape of
    /// gap blocks <c>switch_target_by_class_indicator</c>, where 47 of 53 uses name a class *group*.
    /// </para>
    /// <para>
    /// A role that is not set answers false. Nobody is not asleep.
    /// </para>
    /// </remarks>
    public static PatternCondition InAbnormalState(AbnormalState state)
        => ai => ai.GetOwner().GetEffectController().IsAbnormalSet(state);

    /// <summary><c>is_obj_in_abnormal_state obj=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetInAbnormalState(AbnormalState state)
        => ai => ai.CurrentTarget is Creature who && who.GetEffectController().IsAbnormalSet(state);

    /// <summary><c>is_obj_in_abnormal_state obj=OBJI_SEEN</c>.</summary>
    public static PatternCondition SeenInAbnormalState(AbnormalState state)
        => ai => ai.SeenCreature is Creature who && who.GetEffectController().IsAbnormalSet(state);

    /// <summary><c>is_obj_in_abnormal_state obj=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerInAbnormalState(AbnormalState state)
        => ai => ai.LastAttacker is Creature who && who.GetEffectController().IsAbnormalSet(state);

    /// <summary><c>is_obj_in_abnormal_state obj=OBJI_FRIEND</c>.</summary>
    public static PatternCondition FriendInAbnormalState(AbnormalState state)
        => ai => ai.Friend is Creature who && who.GetEffectController().IsAbnormalSet(state);

    /// <summary><c>is_race from=OBJI_FRIEND</c> — the friend this event is about is of that race.</summary>
    /// <remarks>
    /// The last of the friend family to be built, and the one that was worth most: 28 retail patterns
    /// ask it, <b>1,485 npcs run them and 217 of those are spawned here</b>. The refusal tally showed
    /// ten, which counts patterns blocked rather than npcs affected — a reminder that the two numbers
    /// are not interchangeable.
    /// <para>
    /// An absent friend answers false, for the same reason <see cref="FriendHpBelow"/> does: "somebody
    /// who is not there is Elyos" is not true about anything.
    /// </para>
    /// </remarks>
    public static PatternCondition FriendRace(params Race[] races)
        => ai => ai.Friend is Creature who && races.Contains(who.GetRace());

    /// <summary>
    /// <c>is_user</c> on <c>on_attacked</c> — the blow that just landed came from a player.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="KilledByPlayer"/>, which is the same retail word on a death handler and
    /// asks a different question: who did the most damage over the whole fight, rather than who swung
    /// last.
    /// <para>
    /// Retail leans on this to keep NPC-on-NPC scuffles from setting off crowd behaviour. Drakenspire Depths'
    /// wave attackers call for help at two health bands, and this guard is what stops the raid's own
    /// forward guards triggering that call while they trade blows with the wave.
    /// </para>
    /// </remarks>
    public static PatternCondition AttackedByPlayer => ai => ai.LastAttacker is Player;

    /// <summary><c>is_user</c> on <c>on_spelled</c> — the spell came from a player, not an NPC.</summary>
    public static PatternCondition SpelledByPlayer => ai => ai.LastCaster is Player;

    /// <summary><c>is_user</c> / <c>is_npc</c> for the roles that had no condition yet.</summary>
    /// <remarks>
    /// 1,553 uses between the two elements, and much of the vocabulary was already here:
    /// <see cref="AttackedByPlayer"/>, <see cref="SpelledByPlayer"/>, <see cref="KilledByPlayer"/> and
    /// <see cref="KilledByNpc"/> cover the attacker, the caster and the killer. What was missing was
    /// the talker, the creature seen, the current target and the event target.
    /// <para>
    /// <b>Every one of these is null outside the handler that sets it</b>, and that is what makes them
    /// worth having rather than tautologies. <see cref="PatternAi.Talker"/> is assigned in
    /// <c>HandleDialogStart</c> and cleared in a <c>finally</c>, so <see cref="TalkerIsPlayer"/> is not
    /// "somebody is talking to npcs somewhere" but "this branch is running because a player opened
    /// dialogue with <i>this</i> npc, right now". A branch carrying it in another handler correctly
    /// never fires.
    /// </para>
    /// <para>
    /// <c>OBJI_SELF</c> (13 uses of <c>is_npc</c>) and <c>OBJI_FRIEND</c> (22 across both) are refused
    /// by the extractor. The first is definitionally true and emitting <c>When.Always</c> for it would
    /// be reasoning rather than porting; the second names a role this port does not resolve to a
    /// creature.
    /// </para>
    /// </remarks>
    public static PatternCondition TalkerIsPlayer => ai => ai.Talker != null;

    /// <summary><c>is_user obj_indicator=OBJI_SEEN</c>.</summary>
    public static PatternCondition SeenIsPlayer => ai => ai.SeenCreature is Player;

    /// <summary><c>is_user obj_indicator=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetIsPlayer => ai => ai.CurrentTarget is Player;

    /// <summary><c>is_user obj_indicator=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetIsPlayer => ai => ai.EventTarget is Player;

    /// <summary><c>is_npc obj_indicator=OBJI_SEEN</c>.</summary>
    public static PatternCondition SeenIsNpc => ai => ai.SeenCreature is Npc;

    /// <summary><c>is_npc obj_indicator=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetIsNpc => ai => ai.CurrentTarget is Npc;

    /// <summary><c>is_npc obj_indicator=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerIsNpc => ai => ai.LastAttacker is Npc;

    /// <summary><c>is_npc obj_indicator=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterIsNpc => ai => ai.LastCaster is Npc;

    /// <summary><c>is_npc obj_indicator=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternCondition EventTargetIsNpc => ai => ai.EventTarget is Npc;

    /// <summary>
    /// <c>is_tribe target=OBJI_MESSAGE_SENDER tribe_name=...</c> — who is talking, by role rather than
    /// by NPC id.
    /// </summary>
    /// <remarks>
    /// <b>1,205 conditions in the 5.8 files carry a <c>tribe_name</c></b>, and every one of them was
    /// invisible here until now: the summariser's field list did not include the tag, so <c>is_tribe</c>
    /// printed as a bare argumentless guard — exactly the way <c>is_race</c> did before <c>race_type</c>
    /// was added to that list.
    /// <para>
    /// It matters because retail addresses a whole role at once rather than an NPC id. Drakenspire Depths' wave
    /// healer and its wave assassin both broadcast 22755; only the healer's call makes the tanks peel
    /// off, and <c>tribe_name=IDSeal_Wave_Healer</c> is the entire difference between the two.
    /// </para>
    /// </remarks>
    public static PatternCondition SenderTribe(params TribeClass[] tribes)
        => ai => ai.MessageSender is Npc sender && tribes.Contains(sender.GetTribe());

    /// <summary>
    /// <c>is_my_curent_target who=OBJI_MESSAGE_PARAM</c> (retail's own spelling) — the player named in
    /// the message is the one this NPC is already fighting.
    /// </summary>
    /// <remarks>
    /// The point of a call-out: a message naming a player is only interesting to whoever is on that
    /// player. Everyone else in range hears it and does nothing, which is why the guard sits on the
    /// branch rather than on the broadcast.
    /// </remarks>
    public static PatternCondition MessageParamIsMyTarget
        => ai => ai.MessageParam is Creature named && ReferenceEquals(named, ai.CurrentTarget);

    /// <summary><c>is_race from=OBJI_CUR_TARGET</c>.</summary>
    public static PatternCondition TargetRace(params Race[] races)
        => ai => ai.CurrentTarget is Creature target && races.Contains(target.GetRace());

    /// <summary><c>is_race from=OBJI_CASTER</c>.</summary>
    public static PatternCondition CasterRace(params Race[] races)
        => ai => ai.LastCaster is Creature caster && races.Contains(caster.GetRace());

    /// <summary><c>is_race from=OBJI_ATTACKER</c>.</summary>
    public static PatternCondition AttackerRace(params Race[] races)
        => ai => ai.LastAttacker is Creature hitter && races.Contains(hitter.GetRace());

    public static PatternCondition Message(int messageType) => ai => ai.CurrentMessage == messageType;

    /// <summary>
    /// Which npc sent the message being handled — our stand-in for retail's <c>is_race</c> where two
    /// senders share a message number. See <see cref="PatternAi.MessageSender"/>.
    /// </summary>
    public static PatternCondition SenderIs(int npcId) => ai => ai.MessageSender?.GetNpcId() == npcId;

    /// <summary>
    /// <c>is_distance_shorter_than who=OBJI_MESSAGE_PARAM</c> — the npc a message names is this close.
    /// </summary>
    /// <remarks>
    /// <b>Retail uses this to split one message into a near answer and a far one</b>, and the near answer
    /// is usually the quieter of the two. The silikor akaimum is the clearest case: a guard that falls
    /// within ten metres of it gets walked to and <i>not</i> stood back up, while a distant one is
    /// re-placed — so where a guard dies changes whether killing it accomplishes anything.
    /// </remarks>
    public static PatternCondition SenderWithin(int metres) => ai =>
        ai.MessageSender is Aion.GameServer.Model.GameObjects.Creature sender
        && Aion.GameServer.Utils.PositionUtil.IsInRange(ai.GetOwner(), sender, metres);

    /// <summary>No guard at all, for branches that run whenever their event fires.</summary>
    public static PatternCondition[] Always => Array.Empty<PatternCondition>();
}

/// <summary>Branch actions, named after the pattern ops they translate.</summary>
public static class Do
{
    /// <summary><c>add_battle_timer</c>.</summary>
    public static PatternAction ArmTimer(int index, int delayMillis) => ai => ai.ArmTimer(index, delayMillis);

    /// <summary><c>use_skill</c> at the caster.</summary>
    public static PatternAction SkillOnSelf(int skillId) => ai => ai.CastSkill(skillId, NpcSkillTargetAttribute.ME);

    /// <summary><c>use_skill</c> at <c>OBJI_CUR_TARGET</c>.</summary>
    /// <remarks>
    /// Resolved as most-hated, which is the closest our skill queue offers. The two differ only
    /// between a <c>switch_target</c> and the next hate update, since a queued skill picks its target
    /// when it fires rather than when it is queued.
    /// </remarks>
    public static PatternAction SkillOnTarget(int skillId) => ai => ai.CastSkill(skillId, NpcSkillTargetAttribute.MOST_HATED);

    /// <summary><c>use_skill_by_attacker_indicator</c>.</summary>
    public static PatternAction SkillOn(NpcSkillTargetAttribute target, int skillId) => ai => ai.CastSkill(skillId, target);

    /// <summary><c>switch_target_by_attacker_indicator</c>.</summary>
    public static PatternAction SwitchTarget(AggroTarget which) => ai => ai.SwitchTarget(which);

    /// <summary><c>switch_target target=OBJI_ATTACKER</c>.</summary>
    public static PatternAction TargetAttacker() => static ai => ai.TargetAttacker();

    /// <summary><c>switch_target target=OBJI_SEEN</c>.</summary>
    public static PatternAction TargetSeen() => static ai => ai.TargetSeen();

    /// <summary><c>switch_target target=OBJI_CASTER</c>.</summary>
    public static PatternAction TargetCaster() => static ai => ai.TargetCaster();

    /// <summary><c>switch_target target=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternAction TargetMessageSender() => static ai => ai.TargetMessageSender();

    /// <summary><c>switch_target target=OBJI_KILLER</c>.</summary>
    public static PatternAction TargetKiller() => static ai => ai.TargetKiller();

    /// <summary><c>use_skill target=OBJI_EVENT_TARGET</c>: at whoever started the fight.</summary>
    public static PatternAction SkillOnEventTarget(int skillId)
        => ai => ai.CastSkillAt(ai.EventTarget, skillId);

    /// <summary><c>use_skill target=OBJI_ATTACKER</c>: at whoever just hit us.</summary>
    public static PatternAction SkillOnAttacker(int skillId)
        => ai => ai.CastSkillAt(ai.LastAttacker, skillId);

    /// <summary><c>use_skill target=OBJI_CASTER</c>: at whoever just spelled us.</summary>
    public static PatternAction SkillOnCaster(int skillId)
        => ai => ai.CastSkillAt(ai.LastCaster, skillId);

    /// <summary><c>use_skill target=OBJI_MESSAGE_PARAM</c>: at the creature a message was about.</summary>
    public static PatternAction SkillOnMessageParam(int skillId)
        => ai => ai.CastSkillAt(ai.MessageParam as Creature, skillId);

    /// <summary><c>use_skill target=OBJI_SEEN</c> — cast at whoever this NPC has just noticed.</summary>
    /// <remarks>
    /// <see cref="SkillOnSeenNow"/> was here first, for the hazard case; this is the ordinary queued
    /// one. Together they are 104 retail casts across <c>on_see_user</c>, <c>on_see_user_move</c> and
    /// <c>on_see_npc</c> — the greeting, the warning shot and the trap.
    /// </remarks>
    public static PatternAction SkillOnSeen(int skillId)
        => ai => ai.CastSkillAt(ai.SeenCreature, skillId);

    /// <summary><c>use_skill target=OBJI_FLEE_FROM</c> — the parting shot.</summary>
    /// <remarks>
    /// Retail names this only on <c>on_stop_to_flee</c>, and only to cast: an NPC that has run its
    /// distance turns and answers whoever chased it. 12 patterns and <b>113 npcs</b>.
    /// <para>
    /// See <see cref="PatternAi.FledFrom"/> for why the creature survives the stop that triggers this.
    /// </para>
    /// </remarks>
    public static PatternAction SkillOnFledFrom(int skillId)
        => ai => ai.CastSkillAt(ai.FledFrom, skillId);

    /// <summary><c>use_skill target=OBJI_KILLER</c> on <c>on_see_friend_killed_by_user</c>.</summary>
    /// <remarks>
    /// <b>The friend's killer, not this NPC's own.</b> The port keeps the two apart --
    /// <c>ai.Killer</c> is whoever did most damage to <em>me</em> -- and retail spells both
    /// <c>OBJI_KILLER</c>, leaving the handler to say which. Reading this one as the other would aim
    /// the revenge cast at whoever last hit the avenger, usually nobody, so the mechanic would simply
    /// not happen. Same trap as <c>flee_from OBJI_ATTACKER</c>, which already has this remapping.
    /// </remarks>
    public static PatternAction SkillOnFriendsKiller(int skillId)
        => ai => ai.CastSkillAt(ai.FriendsKiller, skillId);

    /// <summary><c>use_skill target=OBJI_MESSAGE_SENDER</c>: at the npc that called.</summary>
    public static PatternAction SkillOnMessageSender(int skillId)
        => ai => ai.CastSkillAt(ai.MessageSender, skillId);

    /// <summary><c>spawn</c> at <c>SPAWN_LOCATION_ABSOLUTE</c>, one per listed spot.</summary>
    public static PatternAction SpawnAt(int npcId, int spawnId, int liveSeconds, params SpawnSpot[] spots)
        => ai => ai.SpawnAt(npcId, spawnId, liveSeconds, spots);

    /// <summary>
    /// <c>spawn</c> at <c>SPAWN_LOCATION_WAY_POINT_START</c> — at the head of a named route, walking it.
    /// </summary>
    public static PatternAction SpawnOnPath(int npcId, int spawnId, string pathName,
        float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnOnPath(npcId, spawnId, pathName, range, liveSeconds);

    /// <summary>
    /// <c>spawn</c> at <c>SPAWN_LOCATION_MY_POINT</c> with retail's <c>dir</c> in degrees, for a spawn
    /// that has to face a particular way rather than inherit the spawner's heading.
    /// </summary>
    public static PatternAction SpawnFacing(int npcId, int spawnId, int degrees, int liveSeconds = 0)
        => ai => ai.SpawnFacing(npcId, spawnId, degrees, liveSeconds);

    /// <summary><c>spawn</c> at <c>SPAWN_LOCATION_MY_POINT</c>, scattered within <paramref name="range"/>.</summary>
    /// <summary>As <see cref="SpawnNear"/>, for adds retail marks <c>despawn_at_attack_state</c>.</summary>
    public static PatternAction SpawnNearForTheFight(int npcId, int spawnId, int count = 1,
        float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnNear(npcId, spawnId, count, range, liveSeconds, untilFightEnds: true);

    /// <summary>As <see cref="SpawnAt"/>, for adds retail marks <c>despawn_at_attack_state</c>.</summary>
    public static PatternAction SpawnAtForTheFight(int npcId, int spawnId, int liveSeconds,
        params SpawnSpot[] spots)
        => ai => ai.SpawnAt(npcId, spawnId, liveSeconds, true, spots);

    /// <summary>As <see cref="SpawnOffset"/>, for adds retail marks <c>despawn_at_attack_state</c>.</summary>
    public static PatternAction SpawnOffsetForTheFight(int npcId, int spawnId, float dx, float dy,
        int liveSeconds, float dz = 0f)
        => ai => ai.SpawnOffset(npcId, spawnId, dx, dy, liveSeconds, dz, untilFightEnds: true);

    public static PatternAction SpawnNear(int npcId, int spawnId, int count = 1, float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnNear(npcId, spawnId, count, range, liveSeconds);

    /// <summary><c>spawn_on_target</c> — placed at whoever the caster is facing.</summary>
    /// <param name="attackHate">
    /// Retail's <c>hatepoints_to_add</c> where the spawn carries <c>attack_target_after_spawn</c>;
    /// leave at 0 and the add arrives passive, as most of them do.
    /// </param>
    public static PatternAction SpawnOnTarget(int npcId, int spawnId, int count = 1, float range = 0f,
        int liveSeconds = 0, int attackHate = 0, float validDistance = 0f)
        => ai => ai.SpawnOnTarget(npcId, spawnId, count, range, liveSeconds, attackHate, validDistance);

    /// <summary>
    /// <c>spawn_on_target target_obj=OBJI_SELF</c> with <c>attack_target_after_spawn</c> — a summon that
    /// appears at the caster's feet and attacks the caster, starting the caster's own fight.
    /// </summary>
    public static PatternAction SpawnAsMyEnemy(int npcId, int spawnId, int liveSeconds, int hate)
        => ai => ai.SpawnAsMyEnemy(npcId, spawnId, liveSeconds, hate);

    /// <summary><c>spawn_on_multi_target</c> — one add on every valid target in range.</summary>
    /// <summary><c>SPAWN_LOCATION_RELATIVE</c> — a fixed offset from where the NPC stands.</summary>
    public static PatternAction SpawnOffset(int npcId, int spawnId, float dx, float dy,
        int liveSeconds = 0, float dz = 0f)
        => ai => ai.SpawnOffset(npcId, spawnId, dx, dy, liveSeconds, dz);

    /// <summary>
    /// <c>spawn_on_multi_target</c>. <paramref name="maxTargets"/> and <paramref name="order"/> are
    /// both required — see the runtime for why neither has a default.
    /// </summary>
    public static PatternAction SpawnOnEachTarget(int npcId, int spawnId, float validDistance,
        int maxTargets, MultiTargetOrder order, float range = 0f, int liveSeconds = 0, int attackHate = 0)
        => ai => ai.SpawnOnEachTarget(npcId, spawnId, validDistance, range, liveSeconds, maxTargets, order,
            attackHate);

    /// <summary><c>spawn_on_target_by_attacker_indicator</c> — on one attacker rather than the tank.</summary>
    /// <param name="range">Retail's <c>spawn_range</c> — the scatter around the target, often zero.</param>
    /// <param name="validDistance">
    /// Retail's <c>valid_distance</c> — how far the attacker may be from the caster and still get one.
    /// <b>Not the same number as <paramref name="range"/></b>; see the runtime for the fight that
    /// proved it.
    /// </param>
    public static PatternAction SpawnOnAttacker(AggroTarget which, int npcId, int spawnId,
        float range = 0f, int liveSeconds = 0, int attackHate = 0, float validDistance = 0f)
        => ai => ai.SpawnOnAttacker(which, npcId, spawnId, range, liveSeconds, attackHate, validDistance);

    /// <summary><c>spawn_on_target target_obj=OBJI_KILLER</c> — on whoever brought this NPC down.</summary>
    public static PatternAction SpawnOnKiller(int npcId, int spawnId, int count = 1, float range = 0f,
        int liveSeconds = 0)
        => ai => ai.SpawnOnKiller(npcId, spawnId, count, range, liveSeconds);

    /// <summary>
    /// <c>flee_from</c> — run away from whoever this NPC is fighting for <paramref name="seconds"/>.
    /// </summary>
    /// <remarks>
    /// Retail's element carries only <c>from</c>, <c>seconds</c> and <c>push_state</c>; how far the
    /// NPC gets is its run speed times the time, which is what this computes. When the clock runs out
    /// it stops and <see cref="AiPattern.OnStopFleeing"/> runs.
    /// </remarks>
    public static PatternAction Flee(int seconds) => ai => ai.Flee(seconds);

    /// <summary><c>flee_from from=OBJI_SEEN</c> — run from what just came into view.</summary>
    public static PatternAction FleeFromSeen(int seconds) => ai => ai.FleeFromSeen(seconds);

    /// <summary><c>flee_from from=OBJI_MESSAGE_PARAM</c>.</summary>
    public static PatternAction FleeFromMessageParam(int seconds)
        => ai => ai.FleeFromMessageParam(seconds);

    /// <summary><c>flee_from from=OBJI_ATTACKER</c>.</summary>
    public static PatternAction FleeFromAttacker(int seconds) => ai => ai.FleeFromAttacker(seconds);

    /// <summary>The same, on <c>on_see_friend_attacked</c>, where the attacker is the friend's.</summary>
    public static PatternAction FleeFromFriendsAttacker(int seconds)
        => ai => ai.FleeFromFriendsAttacker(seconds);

    /// <summary><c>flee_from from=OBJI_CASTER</c>.</summary>
    public static PatternAction FleeFromCaster(int seconds) => ai => ai.FleeFromCaster(seconds);

    /// <summary><c>flee_from from=OBJI_KILLER</c>.</summary>
    public static PatternAction FleeFromKiller(int seconds) => ai => ai.FleeFromKiller(seconds);

    /// <summary><c>flee_from from=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternAction FleeFromEventTarget(int seconds) => ai => ai.FleeFromEventTarget(seconds);

    /// <summary><c>flee_from from=OBJI_MESSAGE_SENDER</c>.</summary>
    public static PatternAction FleeFromMessageSender(int seconds) => ai => ai.FleeFromMessageSender(seconds);

    /// <summary><c>flee_from from=OBJI_TALKER</c>.</summary>
    public static PatternAction FleeFromTalker(int seconds) => ai => ai.FleeFromTalker(seconds);

    /// <summary><c>despawn</c> of everything spawned under one spawn id.</summary>
    /// <summary><c>goto_waypoint</c> — start down the route this NPC's spawn names.</summary>
    /// <remarks>
    /// 1,112 patterns open with it. A path-walking NPC usually starts on its own once its AI reaches
    /// THINK, so this is not always load-bearing — but retail states it, and a pattern that says "walk"
    /// should say it here rather than depend on the state machine reaching the same conclusion.
    /// </remarks>
    /// <summary>Retail's <c>do_nothing</c>: a branch that matches, does nothing, and stops.</summary>
    /// <remarks>
    /// <b>Not the same as leaving the branch out.</b> Branch lists are first-match-wins -- the runtime
    /// runs the first branch whose guards hold and returns -- so a matching <c>do_nothing</c> is how
    /// retail says "in this case, and not the cases below". Dropping it promotes whatever came next,
    /// which is the opposite of what the pattern asks for. 3,445 uses across the dump.
    /// </remarks>
    public static PatternAction Nothing() => static _ => { };

    /// <summary><c>goto_waypoint</c>: walk the npc's own route from the given step.</summary>
    public static PatternAction GotoWaypoint(int step) => ai => ai.GotoWaypoint(step);

    /// <summary><c>goto_waypoint move_type=MOVETYPE_RUN</c>.</summary>
    public static PatternAction GotoWaypointRunning(int step) => ai => ai.GotoWaypointRunning(step);

    /// <summary><c>goto_next_waypoint move_type=MOVETYPE_RUN</c>.</summary>
    public static PatternAction ContinueRouteRunning() => static ai => ai.ContinueRouteRunning();

    /// <summary><c>goto_next_waypoint</c> — carry on to the next point of the route.</summary>
    /// <remarks>
    /// <b>Does nothing for an npc already walking, and that nothing is still the point.</b> This port
    /// advances the route by itself: arriving fires <c>MoveEventHandler.OnMoveArrived</c> ->
    /// <c>TargetEventHandler.OnTargetReached</c> -> <c>WalkManager.TargetReached</c> ->
    /// <c>ChooseNextRouteStep</c>, all of it ported from the Java. <see cref="PatternAi"/> evaluates
    /// <c>OnArrivedAtWaypoint</c> <i>before</i> the base handler, so a rung that advanced the route
    /// itself would advance it a second time and the patrol would visit every other point.
    /// <para>
    /// <b>For an npc standing still it is an instruction, and this used to ignore that.</b> See
    /// <see cref="PatternAi.ContinueRoute"/>: nine runners whose entire race is this element never left
    /// the start line.
    /// <para>
    /// <b>Which leaves the question of why read it at all, and the first answer was wrong.</b> The
    /// obvious argument is the <c>do_nothing</c> one — branch lists are first-match-wins, so a
    /// "keep going" branch blocks the ones below it. That is not what the data says. All 45 branches
    /// whose only action is this are the <i>last</i> branch of their handler, and 39 of those are the
    /// only branch; they block nothing whatsoever.
    /// </para>
    /// <para>
    /// The real gain is that a branch is all-or-nothing. 142 branches carry this element <i>alongside</i>
    /// real actions — 106 casts, 62 spawns, 44 shouts, 19 despawns — and refusing the element dropped
    /// every one of those branches whole, taking the mechanics with it. Reading it as a no-op is what
    /// lets the rest of the branch through. It is named rather than folded into <see cref="Nothing"/>
    /// so the table still records which retail element was there.
    /// </para>
    /// </remarks>
    public static PatternAction ContinueRoute() => static ai => ai.ContinueRoute();

    public static PatternAction StartWalking() => ai => ai.StartWalking();

    /// <summary><c>attack_most_hating</c> — end the march and engage.</summary>
    /// <remarks>
    /// <b>98 branches in the 5.8 dump pair this with <c>is_last_waypoint</c></b>, which is a wave that
    /// walks in and then fights. Without it the walker loops the NPC back to its first point forever.
    /// </remarks>
    public static PatternAction AttackMostHating() => ai => ai.AttackMostHating();

    public static PatternAction Despawn(int spawnId) => ai => ai.DespawnGroup(spawnId);

    /// <summary><c>set_idle_timer</c> — arm the single idle slot, replacing whatever was in it.</summary>
    public static PatternAction SetIdleTimer(int delayMillis) => ai => ai.SetIdleTimer(delayMillis);

    /// <summary>
    /// <c>set_condition_spawn_variable</c> — moves a counter the world's spawn gates read. A
    /// <paramref name="modify"/> of zero assigns <paramref name="set"/>; anything else adds it.
    /// </summary>
    public static PatternAction SetSpawnVariable(string name, int set = 0, int modify = 0)
        => ai => ai.SetSpawnVariable(name, set, modify);

    /// <summary>
    /// <c>despawn_by_nameid</c> — clear up to <paramref name="maxCount"/> NPCs of one kind within
    /// <paramref name="radius"/> metres. The kind is retail's client devname, resolved to an npc id
    /// at porting time.
    /// </summary>
    public static PatternAction DespawnKind(int npcId, float radius, int maxCount)
        => ai => ai.DespawnKind(npcId, radius, maxCount);

    /// <summary>One on the counter, held inside the bounds.</summary>
    /// <remarks>
    /// <b>This is not where retail puts <c>increase_intvar</c>.</b> All 1,409 uses in the dump are
    /// <em>conditions</em> — the element increments and tests in one step, which is
    /// <see cref="When.Counting"/>. This action is the shape two hand-ported classes reached for
    /// (<c>ArenaSaamAI</c> and <c>OphidanReinforcementAI</c>) when the condition form did not exist, and
    /// it stays because they are pinned against it. New ports should use the condition.
    /// </remarks>
    public static PatternAction Increment(int counter, int low, int high)
        => ai => ai.IncrementCounter(counter, low, high);

    /// <summary><c>despawn_self</c>.</summary>
    public static PatternAction DespawnSelf() => ai => ai.DespawnSelf();

    /// <summary>
    /// <c>teleport_target_alias</c> — send the player who is talking to this npc somewhere.
    /// </summary>
    /// <remarks>
    /// <b>Retail names a destination alias; this takes coordinates.</b> The alias table is client data
    /// this port has not extracted, so every use has to resolve its own destination by hand until it is.
    /// The capability is separable from the data, and it is the capability that was missing: the Raksha
    /// shortcut has been recorded as blocked for four passes on a talk handler and a teleport, and the
    /// flag it is gated on has been expressible since world flags were built.
    /// <para>
    /// Does nothing outside an <c>OnTalk</c> branch, because nothing else sets
    /// <see cref="PatternAi.Talker"/>.
    /// </para>
    /// </remarks>
    public static PatternAction TeleportTalker(int worldId, float x, float y, float z, byte heading)
        => ai =>
        {
            if (ai.Talker is Aion.GameServer.Model.GameObjects.Players.Player talker)
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(talker, worldId, x, y, z, heading);
        };

    /// <summary><c>say_to_all</c> / <c>broadcast_message</c>, by our own message id.</summary>
    public static PatternAction Say(int messageId, int delayMillis = 0) => ai => ai.Say(messageId, delayMillis);

    /// <summary>
    /// <c>display_system_message</c> — a line to everyone on the map instance. See
    /// <see cref="PatternAi.SystemMessage"/> for why this is not <see cref="Say"/>.
    /// </summary>
    public static PatternAction SystemMessage(int messageId, int delayMillis = 0)
        => ai => ai.SystemMessage(messageId, delayMillis);

    /// <summary><c>broadcast_message</c> — tells nearby NPCs of this encounter something happened.</summary>
    public static PatternAction Broadcast(int messageType, float range, bool aboutTarget = false,
        bool includeOwnSpawns = false)
        => ai => ai.Broadcast(messageType, range, aboutTarget, includeOwnSpawns);

    /// <summary><c>broadcast_message param_obj=OBJI_ATTACKER</c>.</summary>
    public static PatternAction BroadcastAboutAttacker(int messageType, float range)
        => ai => ai.BroadcastAboutAttacker(messageType, range);

    /// <summary><c>broadcast_message param_obj=OBJI_CASTER</c>.</summary>
    public static PatternAction BroadcastAboutCaster(int messageType, float range)
        => ai => ai.BroadcastAboutCaster(messageType, range);

    /// <summary><c>broadcast_message param_obj=OBJI_SELF</c>.</summary>
    public static PatternAction BroadcastAboutSelf(int messageType, float range)
        => ai => ai.BroadcastAboutSelf(messageType, range);

    /// <summary>
    /// <c>use_skill(OBJI_SELF, SKILLI_INDEX_0)</c> where the NPC's list holds exactly one skill.
    /// </summary>
    /// <remarks>Does nothing if it holds any other number — see <see cref="PatternAi.CastOnlySkillOnSelf"/>.</remarks>
    public static PatternAction OnlySkillOnSelf() => ai => ai.CastOnlySkillOnSelf();

    /// <summary>
    /// <c>use_skill(OBJI_SELF, …)</c> for an NPC that never fights, cast outright rather than queued.
    /// </summary>
    /// <remarks>A queued cast on such an NPC never fires — see <see cref="PatternAi.CastSkillNow"/>.</remarks>
    public static PatternAction SkillOnSelfNow(int skillId) => ai => ai.CastSkillNow(skillId);

    /// <summary><c>use_skill target=OBJI_SEEN</c>, cast at once because the branch despawns.</summary>
    public static PatternAction SkillOnSeenNow(int skillId)
        => ai => ai.CastSkillAtNow(ai.SeenCreature, skillId);

    /// <summary><c>use_skill target=OBJI_CUR_TARGET</c>, cast at once because the branch despawns.</summary>
    public static PatternAction SkillOnTargetNow(int skillId)
        => ai => ai.CastSkillAtNow(ai.CurrentTarget, skillId);

    /// <summary><c>add_hate_point</c> at the object a message carried, then attack it.</summary>
    /// <summary><c>add_hate_point target=OBJI_MESSAGE_SENDER</c> — hate whoever spoke.</summary>
    public static PatternAction HateMessageSender(int hate)
        => ai => ai.HateMessageSender(hate);

    /// <summary><c>switch_target target=OBJI_MESSAGE_PARAM</c> — turn, without taking hate.</summary>
    public static PatternAction TargetMessageParam()
        => ai => ai.TargetMessageParam();

    /// <summary><c>switch_target target=OBJI_MESSAGE_PARAM</c> — hate, and turn to face.</summary>
    public static PatternAction HateMessageTarget(int hate) => ai => ai.HateMessageTarget(hate);

    /// <summary>
    /// <c>add_hate_point target=OBJI_MESSAGE_PARAM</c> — hate, and <b>leave the target alone</b>. The
    /// commoner of retail's two answers; see <see cref="PatternAi.AddHateToMessageTarget"/>.
    /// </summary>
    public static PatternAction HateMessageParam(int hate) => ai => ai.AddHateToMessageTarget(hate);

    /// <summary><c>spawn_on_target target_obj=OBJI_SEEN</c>.</summary>
    public static PatternAction SpawnOnSeen(int npcId, int spawnId, int count = 1, float range = 0f,
        int liveSeconds = 0)
        => ai => ai.SpawnOnSeen(npcId, spawnId, count, range, liveSeconds);

    /// <summary><c>switch_target target=OBJI_SEEN</c> with its <c>points_to_add</c>.</summary>
    public static PatternAction HateSeen(int hate) => ai => ai.HateSeen(hate);

    /// <summary><c>add_hate_point target=OBJI_EVENT_TARGET</c>.</summary>
    public static PatternAction HateEventTarget(int hate) => ai => ai.HateEventTarget(hate);

    /// <summary><c>switch_target target=OBJI_ATTACKER</c> with its <c>points_to_add</c>.</summary>
    public static PatternAction HateAttacker(int hate) => ai => ai.HateAttacker(hate);

    /// <summary><c>reset_hatepoints</c> — drop the whole hate list.</summary>
    /// <summary>
    /// <c>use_skill_by_attacker_indicator restricted_range=TRUE</c> — rank only who is in reach.
    /// </summary>
    public static PatternAction SkillOnRankedInReach(AggroTarget which, int skillId)
        => ai => ai.CastSkillOnRankedInReach(which, skillId);

    public static PatternAction ResetHate() => static ai => ai.ResetHate();

    /// <summary><c>reset_hatepoints is_except_most_hating=TRUE</c> — drop it apart from the tank.</summary>
    public static PatternAction ResetHateExceptTop() => static ai => ai.ResetHateExceptMostHated();

    /// <summary><c>add_hate_point target=OBJI_CUR_TARGET</c>.</summary>
    public static PatternAction HateTarget(int hate) => ai => ai.HateTarget(hate);

    /// <summary><c>switch_target target=OBJI_CASTER</c> with its <c>points_to_add</c>.</summary>
    public static PatternAction HateCaster(int hate) => ai => ai.HateCaster(hate);

    /// <summary><c>broadcast_message param_obj=OBJI_KILLER</c>.</summary>
    public static PatternAction BroadcastAboutKiller(int messageType, float range)
        => ai => ai.BroadcastAboutKiller(messageType, range);

    /// <summary><c>broadcast_message param_obj=OBJI_ATTACKER</c> from a friend-attacked branch.</summary>
    public static PatternAction BroadcastAboutFriendsAttacker(int messageType, float range)
        => ai => ai.BroadcastAboutFriendsAttacker(messageType, range);

    /// <summary><c>add_hate_point</c> on whoever is hitting a friend.</summary>
    public static PatternAction HateFriendsAttacker(int hate) => ai => ai.HateFriendsAttacker(hate);

    /// <summary><c>broadcast_message param_obj=OBJI_KILLER</c> inside a friend-killed branch.</summary>
    public static PatternAction BroadcastAboutFriendsKiller(int messageType, float range)
        => ai => ai.BroadcastAboutFriendsKiller(messageType, range);

    /// <summary><c>add_hate_point target=OBJI_KILLER</c> inside a friend-killed branch.</summary>
    public static PatternAction HateFriendsKiller(int hate) => ai => ai.HateFriendsKiller(hate);

    /// <summary>Anything with no pattern op behind it — an encounter-specific hook the table needs.</summary>
    public static PatternAction Custom(Action<PatternAi> body) => ai => body(ai);
}

/// <summary>One <c>SPAWN_LOCATION_ABSOLUTE</c> placement, as the pattern carries it.</summary>
public readonly record struct SpawnSpot(float X, float Y, float Z, sbyte Heading = 0);
