using Aion.GameServer.Handlers.AI;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Tahabata's fire storm statue and fire tornado, which both pulsed at twice retail's rate.
/// </summary>
/// <remarks>
/// Two retail npcs share this class — <c>IDTiamat_Thor_SumStatue_PhyAtk</c> (283045) and
/// <c>IDTiamat_Tahabata_Tornado</c> (283102) — and it gave both the same one-second timer starting the
/// instant they spawned. Retail opens the statue at three seconds and the tornado at two, and repeats
/// both at two.
/// <para>
/// The statue's twenty-second life against retail's hundred and eighty came from
/// <c>audit_lifetime_conflicts.py</c>; the cadences came from reading the two patterns it pointed at.
/// </para>
/// <para>
/// <b>The cadence half of this is pinned as a table, not through a fight, and that is weaker.</b> Both
/// npcs cast through <c>AIActions.UseSkill</c>, which leaves nothing the harness can observe under the
/// virtual clock — the standing limitation that blocks cast-cadence pinning across this whole family of
/// classes. The first attempt here asserted <c>DrainQueuedSkills</c> and <b>passed on the empty
/// list</b>: that queue is for a different cast path and was never going to fill. The table pin fixes
/// the four numbers in place; it would not notice if the schedule stopped using them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FireStormAiTests
{
	private const int TiamatStronghold = 300510000;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(FireStormAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>The statue opens at three seconds, the tornado at two, and neither on landing.</b>
	/// </summary>
	/// <remarks>
	/// An opening delay is what gives a player standing in the wrong place a chance to move. Both were
	/// zero.
	/// </remarks>
	[Theory]
	[InlineData(FireStormAI.Statue, 3000L)]
	[InlineData(FireStormAI.Tornado, 2000L)]
	public void EachOpensOnItsOwnDelay(int npcId, long openingMillis)
	{
		Assert.Equal(openingMillis, FireStormAI.OpeningMillisFor(npcId));
	}

	/// <summary>
	/// <b>And both then pulse every two seconds, not every one.</b>
	/// </summary>
	/// <remarks>
	/// Doubling a persistent floor hazard's rate is the difference between a fight a group can stand in
	/// and one it cannot.
	/// </remarks>
	[Theory]
	[InlineData(FireStormAI.Statue)]
	[InlineData(FireStormAI.Tornado)]
	public void AndBothThenPulseEveryTwoSeconds(int npcId)
	{
		Assert.Equal(2000L, FireStormAI.RepeatMillisFor(npcId));
	}

	/// <summary>
	/// <b>The statue follows its target for three minutes.</b> It was deleted after twenty seconds.
	/// </summary>
	/// <remarks>
	/// Retail's <c>live_time=180</c> is on the boss's <c>ConcentratedFire</c> rung, which puts the statue
	/// on whoever sent the message. Twenty seconds made a three-minute pressure mechanic into a blip.
	/// This one is behavioural: the npc is actually gone.
	/// </remarks>
	[Fact]
	public void TheStatueStandsThreeMinutes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FireStormAI.Statue, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromSeconds(175));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == FireStormAI.Statue));

		harness.Clock.Advance(TimeSpan.FromSeconds(10));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == FireStormAI.Statue));
	}

	/// <summary>
	/// <b>The tornado has no lifetime at all.</b> Retail spawns it with no <c>live_time</c>.
	/// </summary>
	/// <remarks>
	/// It arrives at the bottom of the fight and is meant to stay for the rest of it. Worth pinning
	/// alongside the statue, because the obvious wrong fix for the statue's twenty seconds would have
	/// been to raise the shared number and give the tornado a lifetime it should not have.
	/// </remarks>
	[Fact]
	public void TheTornadoStandsForTheRestOfTheFight()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(FireStormAI.Tornado, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromMinutes(5));

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == FireStormAI.Tornado));
	}
}
