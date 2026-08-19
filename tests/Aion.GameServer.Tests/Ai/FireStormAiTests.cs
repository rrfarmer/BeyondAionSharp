using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

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
/// <b>The cadence was pinned as a table when this was written, and the two openings are behavioural
/// now.</b> The reason given then — "casts through <c>AIActions.UseSkill</c> leave nothing the harness
/// can observe under the virtual clock" — stopped being true when the harness took over the combat
/// model's clock: <c>GetLastSkillTime</c> moves with it, so a cast is visible. The repeat is still a
/// table pin. The first attempt at all of this asserted <c>DrainQueuedSkills</c> and <b>passed on the
/// empty list</b>: that queue belongs to a different cast path and was never going to fill.
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

	/// <summary>
	/// <b>Behavioural now: the statue's first cast really does land at three seconds.</b>
	/// </summary>
	/// <remarks>
	/// This file said, when it was written, that the cadence could only be pinned as a table because
	/// "casts through <c>AIActions.UseSkill</c> leave nothing the harness can observe under the virtual
	/// clock". That is no longer true: the harness drives the combat model's clock, so
	/// <c>GetLastSkillTime</c> moves with it and a cast is visible.
	/// <para>
	/// Left alongside the table pins rather than replacing them — the table still fixes the tornado's
	/// two seconds and the shared two-second repeat, and this is the first of those numbers to be held
	/// by something the npc actually did.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheStatuesFirstCastReallyLandsAtThreeSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc statue = harness.Spawn(FireStormAI.Statue, 679.88f, 1068.88f, 497.88f);
		Assert.Equal(0L, statue.GetGameStats().GetLastSkillTime());

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2500));
		Assert.Equal(0L, statue.GetGameStats().GetLastSkillTime());

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.NotEqual(0L, statue.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And the tornado's at two.</b>
	/// </summary>
	[Fact]
	public void AndTheTornadosAtTwo()
	{
		using BossAiHarness harness = NewHarness();
		Npc tornado = harness.Spawn(FireStormAI.Tornado, 679.88f, 1068.88f, 497.88f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(1500));
		Assert.Equal(0L, tornado.GetGameStats().GetLastSkillTime());

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.NotEqual(0L, tornado.GetGameStats().GetLastSkillTime());
	}
}
