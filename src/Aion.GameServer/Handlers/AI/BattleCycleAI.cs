using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A combat rotation: arms a timer when the fight starts, and puts adds on the ground when it fires.
/// </summary>
/// <remarks>
/// 1,829 retail patterns across 13,188 npcs, none of which ran here. <see cref="IdleCycleAI"/> covers what an
/// npc does while nothing is happening; this covers what a boss does <b>during</b> the fight, which is
/// where retail keeps most of its mechanics.
/// <para>
/// <b>The engine was already here.</b> <see cref="PatternAi"/> has had thirty battle-timer slots since
/// it was written -- armed by indicator, gated on being in combat, cancelled on death. What was missing
/// was the data: 810 retail patterns spawn from <c>on_battle_timer</c> and this port ran none of them.
/// </para>
/// <para>
/// <b>A rotation nothing arms never runs</b>, so five handlers are read, not one. Entering combat is
/// the common case, but retail also starts a chain when another npc calls (<c>on_message</c>), on being
/// hit, on being spelled, and on waking. Reading only the first left those rotations ported and inert.
/// <para>
/// The refusals are counted rather than estimated: 1,823 name npcs that are not free, 641 cast at a
/// creature this port cannot name, 198 ask whether the player is flying. <b>188 rotations re-arm only
/// from inside themselves</b> -- a chain with no first link in the pattern -- and are left alone,
/// because inventing an entry point for them would be invention rather than porting.
/// </para>
/// </para>
/// <para>
/// A pattern is taken only if every branch of both handlers is sayable in full. Dropping one unsayable
/// action would leave a boss that spawns its adds and never casts, which is worse than one that does
/// nothing -- the mechanic would look ported.
/// </para>
/// </remarks>
[AIName("battle_cycle")]
public class BattleCycleAI : PatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public BattleCycleAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern =>
		ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => GeneratedPattern.For(id));
}
