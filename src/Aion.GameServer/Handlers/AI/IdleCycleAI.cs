using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A wave controller: wakes, waits, and then runs a guarded set of rungs on every tick of its cycle.
/// </summary>
/// <remarks>
/// 81 retail patterns across 83 npcs, none of which ran here — every one was on a class that does
/// nothing with a timer. <see cref="IdleSpawns"/> covers the flat case, one unguarded rung that spawns
/// and re-arms; these carry two to ten branches guarded by retail's flag idiom or a probability roll.
/// <para>
/// <b>They are not where the conditional spawn engine's fuel turned out to live.</b> The expectation
/// was that porting these would connect the spawn gates to their writers; of the 984 actions here
/// exactly <b>one</b> is a <c>set_condition_spawn_variable</c>. The writers sit in the 31 patterns
/// refused below, behind <c>increase_intvar</c> and the string-id actions. What this does port is the
/// adds themselves: 747 spawns, in waves that never happened.
/// </para>
/// <para>
/// <b>Left to their owners:</b> 22 patterns name an npc some hand-ported encounter already models --
/// Kalindi's dispel worm among them, whose own pattern removes it after two seconds where
/// <see cref="CalindiFlamelordAI"/> gives it ten. Which is faithful is a real question and not one to
/// answer by accident, so those npcs keep the class they have.
/// </para>
/// <para>
/// <b>Not translated:</b> 31 patterns carry a branch this port cannot say — <c>increase_intvar</c>, a
/// counter with a bounds test, is the largest — and 16 more have no wake-up delay to start the cycle.
/// A pattern is refused whole rather than in part: branch lists are first-match-wins, so dropping one
/// rung silently promotes the next.
/// </para>
/// </remarks>
[AIName("idle_cycle")]
public class IdleCycleAI : PatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public IdleCycleAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnWakeUp = AiPattern.Of(IdleCycles.WakeRungFor(id)),
			OnIdleTimer = AiPattern.Of(IdleCycles.CycleRungsFor(id)),
		});
}

/// <summary>
/// The same cycle, on an npc retail keeps passive.
/// </summary>
/// <remarks>
/// <b>67 of the 83 npcs this table drives were <c>general</c> before it bound them</b>, and
/// <see cref="IdleCycleAI"/> descends from <c>AggressiveNpcAI</c> -- so binding them turned wave
/// controllers and scenery into things that attack on sight. Every pin stayed green throughout: the
/// waves still arrived, the flags were still written. The mistake is only visible in the base class,
/// which is why the split exists here and in the wake tables.
/// </remarks>
[AIName("idle_cycle_passive")]
public class IdleCyclePassiveAI : PassivePatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public IdleCyclePassiveAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnWakeUp = AiPattern.Of(IdleCycles.WakeRungFor(id)),
			OnIdleTimer = AiPattern.Of(IdleCycles.CycleRungsFor(id)),
		});

}
