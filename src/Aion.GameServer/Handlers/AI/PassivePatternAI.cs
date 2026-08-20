using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// A retail pattern on an npc that does not fight: flags, wave controllers and scenery.
/// </summary>
/// <remarks>
/// 425 retail patterns across 462 npcs, all of them on <c>general</c>. <see cref="WakeVariables"/> took
/// the ones whose whole behaviour was an unguarded list of spawn-variable writes; these carry a guard,
/// a timer, a message or a spawn as well, which needs the pattern runtime.
/// <para>
/// <b>What made this table possible was not vocabulary but a base class.</b> Every other pattern table
/// feeds a class descending from <c>AggressiveNpcAI</c>, and binding a passive npc to one of those
/// makes it attack players on sight -- which happened here to 67 wave controllers and went unnoticed
/// for a dozen entries, because the waves still arrived and every pin stayed green.
/// <see cref="PassivePatternAi"/> puts the three overrides back the way <c>GeneralNpcAI</c> has them.
/// </para>
/// </remarks>
[AIName("passive_pattern")]
public class PassivePatternAI : PassivePatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public PassivePatternAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id =>
		new AiPattern
		{
			OnWakeUp = AiPattern.Of(PassivePatterns.OnWakeUpFor(id)),
			OnIdleTimer = AiPattern.Of(PassivePatterns.OnIdleTimerFor(id)),
		});
}
