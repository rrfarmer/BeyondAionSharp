using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The drakan healing servant, which healed at half retail's rate.
/// </summary>
/// <remarks>
/// Retail's <c>IDYun_Temp_69</c> is two rungs: entering attack state arms a timer at 1000, and the rung
/// it fires re-arms at 3000 and casts. This port re-armed at 6000 — a servant a group is meant to have
/// to kill quickly was never the pressure it should have been.
/// <para>
/// <b>Table pins.</b> The servant heals its creator and does nothing at all without one; the harness
/// spawns npcs without a creator, so there is no fight in which the cadence can be observed. What is
/// fixed here is the period and the opening.
/// </para>
/// </remarks>
public sealed class DrakanHealingServantTests
{
	/// <summary>
	/// <b>It heals every three seconds, not every six.</b>
	/// </summary>
	[Fact]
	public void ItHealsEveryThreeSecondsNotSix()
	{
		Assert.Equal(3000L, DrakanHealingServantAI.HealRepeatMillis);
	}

	/// <summary>
	/// <b>And retail's opening is one second.</b>
	/// </summary>
	/// <remarks>
	/// Kept as retail's number even though this port reaches it later: the servant waits two seconds
	/// after spawning to acquire its creator, so the first heal lands about three seconds in rather than
	/// one after being pulled. That gap is this port's plumbing, recorded in the class rather than tuned
	/// away, because shortening the acquisition risks a servant that finds no creator and never heals.
	/// </remarks>
	[Fact]
	public void AndRetailsOpeningIsOneSecond()
	{
		Assert.Equal(1000L, DrakanHealingServantAI.HealOpeningMillis);
	}

	/// <summary>
	/// <b>And the two are not the same number.</b>
	/// </summary>
	/// <remarks>
	/// Retail's opening and repeat differ, which is the shape a single fixed-rate constant loses. This
	/// is the pin that fails if someone collapses them.
	/// </remarks>
	[Fact]
	public void AndTheTwoAreNotTheSameNumber()
	{
		Assert.NotEqual(DrakanHealingServantAI.HealOpeningMillis, DrakanHealingServantAI.HealRepeatMillis);
	}
}
