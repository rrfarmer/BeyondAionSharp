using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Templates.Npcskill;

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
    public PatternBranch[] OnBattleTimer { get; init; } = None;
    public PatternBranch[] OnLeaveAttack { get; init; } = None;
    public PatternBranch[] OnEnterIdle { get; init; } = None;
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

    /// <summary><c>is_hp_lower_than</c> — true on every evaluation below the threshold, not just the first.</summary>
    public static PatternCondition HpBelow(int percent) => ai => ai.HpPercent < percent;

    /// <summary><c>is_hp_in_boundary</c> — inclusive at both ends, which is how retail's regimes tile.</summary>
    public static PatternCondition HpBetween(int low, int high) => ai => ai.HpPercent >= low && ai.HpPercent <= high;

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

    /// <summary><c>test_probability</c>.</summary>
    public static PatternCondition Chance(int percent) => ai => ai.RollPercent(percent);

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

    public static PatternCondition Message(int messageType) => ai => ai.CurrentMessage == messageType;

    /// <summary>
    /// Which npc sent the message being handled — our stand-in for retail's <c>is_race</c> where two
    /// senders share a message number. See <see cref="PatternAi.MessageSender"/>.
    /// </summary>
    public static PatternCondition SenderIs(int npcId) => ai => ai.MessageSender?.GetNpcId() == npcId;

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

    /// <summary><c>spawn</c> at <c>SPAWN_LOCATION_ABSOLUTE</c>, one per listed spot.</summary>
    public static PatternAction SpawnAt(int npcId, int spawnId, int liveSeconds, params SpawnSpot[] spots)
        => ai => ai.SpawnAt(npcId, spawnId, liveSeconds, spots);

    /// <summary><c>spawn</c> at <c>SPAWN_LOCATION_MY_POINT</c>, scattered within <paramref name="range"/>.</summary>
    public static PatternAction SpawnNear(int npcId, int spawnId, int count = 1, float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnNear(npcId, spawnId, count, range, liveSeconds);

    /// <summary><c>spawn_on_target</c> — placed at whoever the caster is facing.</summary>
    /// <param name="attackHate">
    /// Retail's <c>hatepoints_to_add</c> where the spawn carries <c>attack_target_after_spawn</c>;
    /// leave at 0 and the add arrives passive, as most of them do.
    /// </param>
    public static PatternAction SpawnOnTarget(int npcId, int spawnId, int count = 1, float range = 0f,
        int liveSeconds = 0, int attackHate = 0)
        => ai => ai.SpawnOnTarget(npcId, spawnId, count, range, liveSeconds, attackHate);

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
    public static PatternAction SpawnOnAttacker(AggroTarget which, int npcId, int spawnId,
        float range = 0f, int liveSeconds = 0, int attackHate = 0)
        => ai => ai.SpawnOnAttacker(which, npcId, spawnId, range, liveSeconds, attackHate);

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

    /// <summary><c>despawn</c> of everything spawned under one spawn id.</summary>
    public static PatternAction Despawn(int spawnId) => ai => ai.DespawnGroup(spawnId);

    /// <summary><c>set_idle_timer</c> — arm the single idle slot, replacing whatever was in it.</summary>
    public static PatternAction SetIdleTimer(int delayMillis) => ai => ai.SetIdleTimer(delayMillis);

    /// <summary>
    /// <c>despawn_by_nameid</c> — clear up to <paramref name="maxCount"/> NPCs of one kind within
    /// <paramref name="radius"/> metres. The kind is retail's client devname, resolved to an npc id
    /// at porting time.
    /// </summary>
    public static PatternAction DespawnKind(int npcId, float radius, int maxCount)
        => ai => ai.DespawnKind(npcId, radius, maxCount);

    /// <summary><c>increase_intvar</c> — one on the counter, held inside the bounds.</summary>
    public static PatternAction Increment(int counter, int low, int high)
        => ai => ai.IncrementCounter(counter, low, high);

    /// <summary><c>despawn_self</c>.</summary>
    public static PatternAction DespawnSelf() => ai => ai.DespawnSelf();

    /// <summary><c>say_to_all</c> / <c>broadcast_message</c>, by our own message id.</summary>
    public static PatternAction Say(int messageId, int delayMillis = 0) => ai => ai.Say(messageId, delayMillis);

    /// <summary><c>broadcast_message</c> — tells nearby NPCs of this encounter something happened.</summary>
    public static PatternAction Broadcast(int messageType, float range, bool aboutTarget = false)
        => ai => ai.Broadcast(messageType, range, aboutTarget);

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

    /// <summary><c>add_hate_point</c> at the object a message carried, then attack it.</summary>
    /// <summary><c>add_hate_point target=OBJI_MESSAGE_SENDER</c> — hate whoever spoke.</summary>
    public static PatternAction HateMessageSender(int hate)
        => ai => ai.HateMessageSender(hate);

    /// <summary><c>switch_target target=OBJI_MESSAGE_PARAM</c> — turn, without taking hate.</summary>
    public static PatternAction TargetMessageParam()
        => ai => ai.TargetMessageParam();

    public static PatternAction HateMessageTarget(int hate) => ai => ai.HateMessageTarget(hate);

    /// <summary>Anything with no pattern op behind it — an encounter-specific hook the table needs.</summary>
    public static PatternAction Custom(Action<PatternAi> body) => ai => body(ai);
}

/// <summary>One <c>SPAWN_LOCATION_ABSOLUTE</c> placement, as the pattern carries it.</summary>
public readonly record struct SpawnSpot(float X, float Y, float Z, sbyte Heading = 0);
