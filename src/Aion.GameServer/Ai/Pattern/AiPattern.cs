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
    public PatternBranch[] OnBattleTimer { get; init; } = None;
    public PatternBranch[] OnLeaveAttack { get; init; } = None;
    public PatternBranch[] OnEnterIdle { get; init; } = None;
    public PatternBranch[] OnDie { get; init; } = None;

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

    /// <summary><c>is_message</c> — this branch belongs to one designer-assigned message number.</summary>
    public static PatternCondition Message(int messageType) => ai => ai.CurrentMessage == messageType;

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
    public static PatternAction SpawnOnTarget(int npcId, int spawnId, int count = 1, float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnOnTarget(npcId, spawnId, count, range, liveSeconds);

    /// <summary><c>spawn_on_multi_target</c> — one add on every valid target in range.</summary>
    public static PatternAction SpawnOnEachTarget(int npcId, int spawnId, float validDistance,
        float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnOnEachTarget(npcId, spawnId, validDistance, range, liveSeconds);

    /// <summary><c>spawn_on_target_by_attacker_indicator</c> — on one attacker rather than the tank.</summary>
    public static PatternAction SpawnOnAttacker(AggroTarget which, int npcId, int spawnId,
        float range = 0f, int liveSeconds = 0)
        => ai => ai.SpawnOnAttacker(which, npcId, spawnId, range, liveSeconds);

    /// <summary><c>despawn</c> of everything spawned under one spawn id.</summary>
    public static PatternAction Despawn(int spawnId) => ai => ai.DespawnGroup(spawnId);

    /// <summary><c>set_idle_timer</c> — arm the single idle slot, replacing whatever was in it.</summary>
    public static PatternAction SetIdleTimer(int delayMillis) => ai => ai.SetIdleTimer(delayMillis);

    /// <summary><c>despawn_self</c>.</summary>
    public static PatternAction DespawnSelf() => ai => ai.DespawnSelf();

    /// <summary><c>say_to_all</c> / <c>broadcast_message</c>, by our own message id.</summary>
    public static PatternAction Say(int messageId, int delayMillis = 0) => ai => ai.Say(messageId, delayMillis);

    /// <summary><c>broadcast_message</c> — tells nearby NPCs of this encounter something happened.</summary>
    public static PatternAction Broadcast(int messageType, float range, bool aboutTarget = false)
        => ai => ai.Broadcast(messageType, range, aboutTarget);

    /// <summary><c>add_hate_point</c> at the object a message carried, then attack it.</summary>
    public static PatternAction HateMessageTarget(int hate) => ai => ai.HateMessageTarget(hate);

    /// <summary>Anything with no pattern op behind it — an encounter-specific hook the table needs.</summary>
    public static PatternAction Custom(Action<PatternAi> body) => ai => body(ai);
}

/// <summary>One <c>SPAWN_LOCATION_ABSOLUTE</c> placement, as the pattern carries it.</summary>
public readonly record struct SpawnSpot(float X, float Y, float Z, sbyte Heading = 0);
