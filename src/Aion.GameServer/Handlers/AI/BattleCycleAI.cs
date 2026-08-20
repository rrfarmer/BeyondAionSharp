using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A combat rotation: arms a timer when the fight starts, and puts adds on the ground when it fires.
/// </summary>
/// <remarks>
/// 10 retail patterns across 15 npcs, none of which ran here. <see cref="IdleCycleAI"/> covers what an
/// npc does while nothing is happening; this covers what a boss does <b>during</b> the fight, which is
/// where retail keeps most of its mechanics.
/// <para>
/// <b>The engine was already here.</b> <see cref="PatternAi"/> has had thirty battle-timer slots since
/// it was written -- armed by indicator, gated on being in combat, cancelled on death. What was missing
/// was the data: 810 retail patterns spawn from <c>on_battle_timer</c> and this port ran none of them.
/// </para>
/// <para>
/// <b>Ten of 810 is the honest yield</b>, and the reasons are counted rather than estimated. 510 name
/// npcs that are not free -- already modelled by a hand-ported encounter, or not bound here at all.
/// <b>164 use a skill</b>, which retail names by index into the npc's own list and this port cannot yet
/// resolve; that single gap is the whole remainder and the thing to fix next. 82 more spawn from a
/// timer that nothing in this table arms, because retail also arms battle timers from
/// <c>on_message</c>, <c>on_attacked</c> and <c>on_spelled</c>.
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

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnEnterAttack = AiPattern.Of(BattleCycles.ArmingRungsFor(id)),
			OnBattleTimer = AiPattern.Of(BattleCycles.CycleRungsFor(id)),
		});
}
