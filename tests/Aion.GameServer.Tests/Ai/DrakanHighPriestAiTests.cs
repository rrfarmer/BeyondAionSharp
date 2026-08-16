using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DrakanHighPriestAI"/>, translated from retail pattern
/// <c>XDrakan_HighPriest</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two Gelkmaros priests on plain <c>aggressive</c>. The shape worth pinning is that he has three
/// summon relays rather than one ladder, that opening a new one does not stop the old, and that each
/// relay is a pair of timers whose interval is the sum of two delays.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrakanHighPriestAiTests
{
	private const int Gelkmaros = 220070000;

	private const int Malekor = 236449;
	private const int Nashuma = 236494;

	private const int Greater = 281824;
	private const int Lesser = 281825;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(4096)
			.WithAi(typeof(DrakanHighPriestAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId = Malekor)
	{
		BossAiHarness harness = NewHarness();
		Npc priest = harness.Spawn(npcId, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(304f + i, 300f, 200f));

		harness.Engage(priest, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(priest, member);

		return (harness, priest, raid);
	}

	/// <summary>How many of that kind <em>arrived</em> in the next stretch of fight.</summary>
	private static int Arrived(BossAiHarness harness, Npc priest, List<Player> raid, int seconds,
		params int[] npcIds) =>
		harness.WatchNew(seconds, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(priest, member);
				BossAiHarness.KeepAlive(member);
			}
		}, npcIds).Total;

	/// <summary>Untouched he calls nobody.</summary>
	[Fact]
	public void AnUnpulledPriestCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Malekor, 300f, 300f, 200f);

		Assert.Equal(0, harness.Watch(180, null, Greater, Lesser).Total);
	}

	/// <summary>
	/// <b>The base relay runs from twenty seconds and pays two every forty.</b> The interval is the sum
	/// of the relay's two delays, not either of them — three minutes is four payments, not eight.
	/// </summary>
	[Theory]
	[InlineData(Malekor)]
	[InlineData(Nashuma)]
	public void TheBaseRelayPaysTwoEveryFortySeconds(int priestId)
	{
		var (harness, priest, raid) = Engaged(priestId);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(priest, 90);

		// Nothing before the first hand-off completes at forty seconds.
		Assert.Equal(0, Arrived(harness, priest, raid, 35, Greater, Lesser));

		// Then two at a time: four payments in the next three minutes.
		int later = Arrived(harness, priest, raid, 180, Lesser);
		Assert.InRange(later, 8, 10);
	}

	/// <summary>
	/// <b>Crossing fifty adds a relay rather than replacing one.</b> A greater summon lands on the
	/// step, and from then on the room is being fed by two clocks at once.
	/// </summary>
	[Fact]
	public void CrossingFiftyAddsASecondRelay()
	{
		var (harness, priest, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(priest, 90);
		int alone = Arrived(harness, priest, raid, 180, Lesser);

		BossAiHarness.SetExactPercent(priest, 40);
		Assert.Equal(1, Arrived(harness, priest, raid, 10, Greater));

		// Counted rather than compared: the base relay pays eight in three minutes on its own, and a
		// second relay of three every thirty adds fifteen more. "More than before" is satisfied by
		// noise, and let a mutation that removed the second relay entirely through the first time.
		int together = Arrived(harness, priest, raid, 180, Lesser);
		Assert.InRange(together, 24, 32);
	}

	/// <summary>And below twenty-five a third one opens on top of those.</summary>
	[Fact]
	public void BelowTwentyFiveAThirdRelayOpens()
	{
		var (harness, priest, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(priest, 40);
		Arrived(harness, priest, raid, 20, Greater);
		int two = Arrived(harness, priest, raid, 180, Lesser);

		BossAiHarness.SetExactPercent(priest, 15);
		Assert.Equal(1, Arrived(harness, priest, raid, 10, Greater));

		// Forty-four in three minutes with all three running, against twenty-eight with two. The
		// window is tight enough to notice a relay paying two instead of three, which a "more than
		// before" comparison could not.
		int three = Arrived(harness, priest, raid, 180, Lesser);
		Assert.InRange(three, 40, 50);
	}

	/// <summary>Each band step pays its greater summon once, however long the fight stays there.</summary>
	[Fact]
	public void EachStepPaysItsGreaterSummonOnce()
	{
		var (harness, priest, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(priest, 40);
		Assert.Equal(1, Arrived(harness, priest, raid, 10, Greater));
		Assert.Equal(0, Arrived(harness, priest, raid, 120, Greater));
	}

	/// <summary>A summon keeps thirty seconds, which is what stops the relays becoming a crowd.</summary>
	[Fact]
	public void ASummonKeepsThirtySeconds()
	{
		var (harness, priest, raid) = Engaged();
		using BossAiHarness _h = harness;

		// The step fires on the first five-second tick, so it is already that old when caught.
		BossAiHarness.SetExactPercent(priest, 40);
		Arrived(harness, priest, raid, 6, Greater);
		Npc greater = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == Greater);

		Arrived(harness, priest, raid, 21, Greater);
		Assert.True(greater.IsSpawned(), "it went before its thirty seconds were up");

		Arrived(harness, priest, raid, 8, Greater);
		Assert.False(greater.IsSpawned(), "it outlived its thirty seconds");
	}

	/// <summary>Both exits clear whatever the relays have put out.</summary>
	[Fact]
	public void BothExitsClearTheRoom()
	{
		var (harness, priest, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(priest, 15);
		Arrived(harness, priest, raid, 60, Greater, Lesser);
		Assert.True(harness.LiveNpcs().Count(n => n.GetNpcId() is Greater or Lesser) > 0);

		priest.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() is Greater or Lesser));
	}
}
