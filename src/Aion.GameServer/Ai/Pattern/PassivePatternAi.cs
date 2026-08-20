using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// A translated retail pattern on an npc that does not start fights.
/// </summary>
/// <remarks>
/// <see cref="PatternAi"/> extends <c>AggressiveNpcAI</c>, which is right for a boss and wrong for the
/// wave controllers, flag markers and scenery that make up much of the pattern data: retail has them on
/// <c>general</c>, and <c>general</c> does not attack anybody.
/// <para>
/// <b>This is not a new behaviour, it is the absence of one.</b> `AggressiveNpcAI` adds exactly three
/// overrides over `GeneralNpcAI` -- seeing a creature, being aggroed by one, and answering a guard's
/// call for support -- and this puts all three back the way `GeneralNpcAI` has them. Everything else
/// about the pattern runtime is untouched.
/// </para>
/// <para>
/// It exists because a table bound 67 npcs that retail keeps passive to a class that descends from
/// `AggressiveNpcAI`, which made scenery attack players on sight while every pin stayed green -- the
/// variable was still written, the wave was still placed. Nothing about the mechanic being ported shows
/// the mistake; only the base class does.
/// </para>
/// </remarks>
public abstract class PassivePatternAi : PatternAi
{
	protected PassivePatternAi(Npc owner)
		: base(owner)
	{
	}

	/// <summary>Passive: seeing somebody is not a reason to do anything.</summary>
	protected override void HandleCreatureSee(Creature creature)
	{
	}

	/// <summary>Passive: being aggroed does not start a fight.</summary>
	protected override void HandleCreatureAggro(Creature creature)
	{
	}

	/// <summary>Passive: does not answer a guard's call for support.</summary>
	protected override bool HandleCreatureNeedsSupportByGuard(Creature creature) => false;
}
