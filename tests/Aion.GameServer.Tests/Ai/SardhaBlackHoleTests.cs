using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Sardha's black hole, which opened a second before it should.
/// </summary>
/// <remarks>
/// Retail's FX npc is the controller: <c>on_wake_up</c> sets an idle timer of 1500, and each firing
/// lays a three-second damage npc and re-arms at 2000. This port opened at 500 — the repeat was right
/// and the first pulse arrived a full second early, which on a hole that pulls players in is the
/// difference between being caught by it and walking clear.
/// <para>
/// Behavioural, and only since the clock hook went on: the pulsing npc casts through
/// <c>AIActions.UseSkill</c>, so <c>GetLastSkillTime</c> moves with the harness clock.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SardhaBlackHoleTests
{
	private const int TiamatStronghold = 300510000;

	/// <summary>The member of the trio this port has doing the pulsing.</summary>
	private const int BlackHoleDmg = 283097;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(DistortedSpaceAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>The hole does not pulse in its first second.</b>
	/// </summary>
	[Fact]
	public void TheHoleDoesNotPulseInItsFirstSecond()
	{
		using BossAiHarness harness = NewHarness();
		Npc hole = harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(1000));

		Assert.Equal(0L, hole.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And opens at a second and a half.</b>
	/// </summary>
	[Fact]
	public void AndOpensAtASecondAndAHalf()
	{
		using BossAiHarness harness = NewHarness();
		Npc hole = harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));

		Assert.NotEqual(0L, hole.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And pulses every two seconds after that.</b>
	/// </summary>
	/// <remarks>
	/// Read as two stamps a beat apart rather than as a count: pulses land at 1.5 and 3.5 seconds, so a
	/// reading at 2 and another at 4 must differ. The repeat was already retail's; it is pinned so the
	/// opening fix cannot disturb it.
	/// </remarks>
	[Fact]
	public void AndPulsesEveryTwoSecondsAfterThat()
	{
		using BossAiHarness harness = NewHarness();
		Npc hole = harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));
		long first = hole.GetGameStats().GetLastSkillTime();

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));

		Assert.NotEqual(first, hole.GetGameStats().GetLastSkillTime());
	}

	/// <summary>
	/// <b>And the hole closes at ten seconds.</b>
	/// </summary>
	/// <remarks>
	/// Retail's, corrected in an earlier pass from Java's eight. Pinned here so the cadence work cannot
	/// quietly move it.
	/// </remarks>
	[Fact]
	public void AndTheHoleClosesAtTenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);

		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == BlackHoleDmg));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == BlackHoleDmg));
	}

	/// <summary>
	/// <b>And the summoner shuts it after two seconds.</b>
	/// </summary>
	/// <remarks>
	/// Retail's ten seconds are a backstop, not the mechanic: the branch that opens the hole arms
	/// <c>BTIMERI_INDEX_28</c> at 2000, and that timer broadcasts 31, which the hole answers by closing.
	/// This port had no close at all, so every hole ran the full ten — five times as long as a fight
	/// ever sees, on a hazard that drags players into it.
	/// </remarks>
	[Fact]
	public void AndTheSummonerShutsItAfterTwoSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);
		Npc hole = harness.Spawn(BlackHoleDmg, 1032f, 297f, 409f);
		BossAiHarness.MakeMutuallyKnown(caller, hole);
		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == BlackHoleDmg));

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(caller, DistortedSpaceAI.CloseHole, caller,
			BrigadeGeneralTerathAI.CloseHoleRange);

		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == BlackHoleDmg));
	}

	/// <summary>
	/// <b>And another message leaves it open.</b>
	/// </summary>
	[Fact]
	public void AndAnotherMessageLeavesItOpen()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(BlackHoleDmg, 1030f, 297f, 409f);
		Npc hole = harness.Spawn(BlackHoleDmg, 1032f, 297f, 409f);
		BossAiHarness.MakeMutuallyKnown(caller, hole);

		Aion.GameServer.Ai.NpcMessageBus.Broadcast(caller, 32, caller, 50f);

		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == BlackHoleDmg));
	}

	/// <summary>
	/// <b>And the close comes two seconds after the hole opens, not ten.</b>
	/// </summary>
	/// <remarks>
	/// The delay itself, from the timer Terath arms in the same branch. A table pin: the summoner's
	/// gravity event runs from a phase ladder the harness cannot walk without the full fight.
	/// </remarks>
	[Fact]
	public void AndTheCloseComesTwoSecondsAfterTheHoleOpens()
	{
		Assert.Equal(2000L, BrigadeGeneralTerathAI.CloseHoleDelayMillis);
	}
}
