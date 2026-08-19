using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Berserk Anoha's reward commander, which went to the wrong faction.
/// </summary>
/// <remarks>
/// Retail's <c>on_killed_by_user</c> splits on <c>is_race from=OBJI_KILLER</c>: a <c>pc_dark</c> killer
/// gets one commander, a <c>pc_light</c> killer the other. This class picked from the fortress race
/// read at spawn time, so a raid that took him from the holding faction was handed the holding
/// faction's commander.
/// <para>
/// Pinned as a mapping rather than through a fight. Anoha is a world-siege boss whose death path
/// reaches <c>SiegeService</c> and the quest engine, neither of which the AI harness stands up. That is
/// weaker than a behavioural pin and worth saying: it fixes which commander belongs to which race, and
/// it would not notice if <c>CheckForFactionReward</c> stopped calling it.
/// </para>
/// </remarks>
public sealed class BerserkAnohaAiTests
{
	/// <summary>
	/// <b>The killer's race decides which commander appears.</b>
	/// </summary>
	/// <remarks>
	/// 804594 is the <c>ASMODIANS</c> npc and 804595 the <c>ELYOS</c> one, by their own templates.
	/// </remarks>
	[Theory]
	[InlineData(Race.ELYOS, BerserkAnohaAI.ElyosCommander)]
	[InlineData(Race.ASMODIANS, BerserkAnohaAI.AsmodianCommander)]
	public void TheKillersRaceDecidesWhichCommanderAppears(Race killer, int commander)
	{
		Assert.Equal(commander, BerserkAnohaAI.CommanderFor(killer));
	}

	/// <summary>
	/// <b>And the two are never the same npc.</b>
	/// </summary>
	/// <remarks>
	/// Without this the mapping's <i>shape</i> is unpinned: a change that returned one commander for
	/// both races would still satisfy one of the two cases above.
	/// </remarks>
	[Fact]
	public void AndTheTwoAreNeverTheSameNpc()
	{
		Assert.NotEqual(BerserkAnohaAI.CommanderFor(Race.ELYOS), BerserkAnohaAI.CommanderFor(Race.ASMODIANS));
	}

	/// <summary>
	/// <b>The commander stands thirty minutes.</b> Retail's <c>live_time</c> is 1800; it was given 3600.
	/// </summary>
	[Fact]
	public void TheCommanderStandsThirtyMinutes()
	{
		Assert.Equal(1800, BerserkAnohaAI.CommanderLifeSeconds);
	}
}
