using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="UnstableTriroanAI"/> and <see cref="BabyElementalControllerAI"/>, translated
/// from retail patterns <c>IDLF2A_ElementalKingNmd</c> and <c>ND2_FhXSum2</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What this replaces: a Java class with eleven fixed health phases that spawned its own elementals.
/// Retail has one summon slot whose interval and count both change with the band, and the elementals
/// come from a controller standing in the room. Each of those is a pin here.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class UnstableTriroanAiTests
{
	private const int Lab = 310110000;

	private const int Triroan = 214669;
	private const int Controller = 280983;

	private const int Fire = 280975;
	private const int Water = 280976;
	private const int Earth = 280977;
	private const int Air = 280978;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Lab).WithWorldSize(1024)
			.WithAi(typeof(UnstableTriroanAI), typeof(BabyElementalControllerAI),
				typeof(TriroansSummonAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The repeat pokes are rolled for.</b> Retail guards the three band timers and the deep one with
	/// <c>test_probability 50</c> and the target peel with <c>33</c> — so the king's calls come in a
	/// cadence with gaps in it rather than on a metronome.
	/// </summary>
	/// <remarks>
	/// <b>This port omitted all five, so every poke landed.</b> Found by <c>audit_handler_guards.py</c>,
	/// which compares our guards branch by branch against the retail pattern the npc actually runs; a
	/// dropped guard is the dangerous direction, because it lets a branch fire where retail would have
	/// held it back.
	/// <para>
	/// <b>The opener is not rolled and must not be.</b> Branch 20 arms the summon slot and calls the
	/// first elementals with no probability guard at all — retail writes it that way — so a raid always
	/// gets the opening call however the dice land. What the rolls gate is the <em>repeat</em>, which is
	/// why this pin compares two runs rather than asserting nobody arrives. A first draft asserted zero
	/// and read seven: the opener, doing exactly what it should.
	/// </para>
	/// <para>
	/// Every other pin in this file forces the rolls to pass so its counts stay exact.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheRepeatPokesAreRolledFor()
	{
		int WithRolls(bool passing)
		{
			var (harness, king, raid) = Engaged();
			using BossAiHarness _h = harness;
			if (passing)
				BossAiHarness.AlwaysRolls(king);
			else
				BossAiHarness.NeverRolls(king);
			BossAiHarness.SetExactPercent(king, 70);
			return Arrived(harness, king, raid, 190);
		}

		int rolled = WithRolls(false);
		int always = WithRolls(true);

		Assert.True(rolled > 0, "the opener is not rolled for and should have called anyway");
		Assert.True(always > rolled,
			$"{always} with the rolls passing against {rolled} without — the guards are not there");
	}

	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc king = harness.Spawn(Triroan, 616f, 488f, 196f);
		Npc controller = harness.Spawn(Controller, 602f, 488f, 196f);
		BossAiHarness.MakeMutuallyKnown(king, controller);
		// Retail rolls for every band here -- fifty percent on the three summon bands and the deep one,
		// a third on the target peel. The rolls are forced to pass so the counts below stay exact; the
		// guard itself is pinned by TheBandsAreRolledFor, which forces them to fail instead.
		BossAiHarness.AlwaysRolls(king);

		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(620f + i, 488f, 196f));

		harness.Engage(king, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(king, raid[i]);

		return (harness, king, raid);
	}

	private static int Arrived(BossAiHarness harness, Npc king, List<Player> raid, int seconds) =>
		harness.WatchNew(seconds, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(king, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Fire, Water, Earth, Air).Total;

	/// <summary>Above eighty he calls nothing, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyHeCallsNothing()
	{
		var (harness, king, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(king, 90);

		Assert.Equal(0, Arrived(harness, king, raid, 120));
	}

	/// <summary>
	/// <b>The first call comes with the 61–80 step itself</b>, and then the band timer drives the slot
	/// about every twenty seconds — one elemental each time.
	/// </summary>
	/// <remarks>
	/// <b>Twenty and not the slot's own thirty.</b> Retail arms the summon slot two ways: the slot
	/// re-arms itself at the band's interval, and the band timer pokes it three seconds after every
	/// one of its own twenty-second ticks. The poke always lands first, so the band timer is the real
	/// clock and the slot's own re-arm never gets to fire. Reading the branch delays alone gives the
	/// wrong cadence — the mutation sweep is what caught it.
	/// </remarks>
	[Fact]
	public void InTheUpperBandTheBandTimerDrivesTheSlot()
	{
		var (harness, king, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(king, 70);

		// The step fires at the first five-second tick and calls one straight away.
		Assert.Equal(1, Arrived(harness, king, raid, 10));

		// Then one about every twenty seconds: eight or nine in the next three minutes.
		int later = Arrived(harness, king, raid, 180);
		Assert.True(later >= 8, $"only {later} calls in three minutes — the band timer has stopped poking");
	}

	/// <summary>
	/// And the middle band really is <b>two</b> at a time, counted rather than compared: eight calls in
	/// three minutes is sixteen elementals, and one-at-a-time would be half that.
	/// </summary>
	[Fact]
	public void TheMiddleBandCallsTwoAtATime()
	{
		var (harness, king, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(king, 30);
		Arrived(harness, king, raid, 40);

		int arrived = Arrived(harness, king, raid, 180);
		Assert.True(arrived >= 14, $"only {arrived} elementals in three minutes — that is one a call");
	}

	/// <summary>
	/// <b>Below twenty it is three at a time, every fifteen seconds.</b> Both halves change with the
	/// band — how many and how often — which is what the eleven fixed phases could not express.
	/// </summary>
	/// <remarks>
	/// <b>And a raid that skips straight there waits for it.</b> The chain that arms the summon slot
	/// starts at the 61–80 step, so dropped in at fifteen percent the king takes about thirty-three
	/// seconds to make his first call — five to the band step, twenty-five for the deep timer, three
	/// more for the slot. The first minute is two calls, not four, and the pin is written around that
	/// rather than around what the branch delays look like read on their own.
	/// </remarks>
	[Fact]
	public void BelowTwentyThreeAtATimeAndFaster()
	{
		var (harness, king, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(king, 15);

		int firstMinute = Arrived(harness, king, raid, 60);
		Assert.Equal(6, firstMinute);

		// Running now: four more calls of three in the next minute.
		int secondMinute = Arrived(harness, king, raid, 60);
		Assert.True(secondMinute >= 12, $"only {secondMinute} in the second minute — the slot has stalled");
	}

	/// <summary>And the middle band is two at a time, so the count really does track the band.</summary>
	[Fact]
	public void TheCountTracksTheBand()
	{
		var (harness, king, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(king, 70);
		int upper = Arrived(harness, king, raid, 70);

		BossAiHarness.SetExactPercent(king, 30);
		int middle = Arrived(harness, king, raid, 70);

		BossAiHarness.SetExactPercent(king, 15);
		int deep = Arrived(harness, king, raid, 70);

		Assert.True(middle > upper, $"middle band {middle} was no busier than the upper {upper}");
		Assert.True(deep > middle, $"the last third {deep} was no busier than the middle {middle}");
	}

	/// <summary>
	/// <b>He does not summon them himself.</b> Take the controller out of the room and the calls fall
	/// on nothing — which is the whole difference from the Java class this replaces.
	/// </summary>
	[Fact]
	public void WithoutTheControllerNothingArrives()
	{
		using BossAiHarness harness = NewHarness();
		Npc king = harness.Spawn(Triroan, 616f, 488f, 196f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(620f + i, 488f, 196f));
		harness.Engage(king, raid[0]);

		BossAiHarness.SetExactPercent(king, 15);

		Assert.Equal(0, Arrived(harness, king, raid, 60));
	}

	/// <summary>The controller answers each of the three numbers with that many elementals.</summary>
	[Theory]
	[InlineData(BabyElementalControllerAI.CallOne, 1)]
	[InlineData(BabyElementalControllerAI.CallTwo, 2)]
	[InlineData(BabyElementalControllerAI.CallThree, 3)]
	public void TheControllerCallsAsManyAsItIsTold(int message, int expected)
	{
		using BossAiHarness harness = NewHarness();
		Npc controller = harness.Spawn(Controller, 602f, 488f, 196f);
		Npc caller = harness.Spawn(Triroan, 604f, 488f, 196f);
		BossAiHarness.MakeMutuallyKnown(caller, controller);

		NpcMessageBus.Broadcast(caller, message, null, 100f);

		Assert.Equal(expected, harness.LiveNpcs()
			.Count(n => n.GetNpcId() is Fire or Water or Earth or Air));
	}

	/// <summary>
	/// And over enough calls it uses all four elements — the branch chain picks which, and a
	/// translation that collapsed it to one element would sit on one id forever.
	/// </summary>
	[Fact]
	public void OverManyCallsEveryElementTurnsUp()
	{
		var seen = new HashSet<int>();

		// Two hundred attempts rather than forty. The loop stops the moment all four have been seen,
		// so the extra cap costs nothing on the common path and buys the rare one: at forty this
		// failed about one full-suite run in seventy, which is what an element appearing roughly one
		// call in ten predicts.
		for (int i = 0; i < 200 && seen.Count < 4; i++)
		{
			using BossAiHarness harness = NewHarness();
			Npc controller = harness.Spawn(Controller, 602f, 488f, 196f);
			Npc caller = harness.Spawn(Triroan, 604f, 488f, 196f);
			BossAiHarness.MakeMutuallyKnown(caller, controller);

			NpcMessageBus.Broadcast(caller, BabyElementalControllerAI.CallOne, null, 100f);
			foreach (Npc npc in harness.LiveNpcs())
				if (npc.GetNpcId() is Fire or Water or Earth or Air)
					seen.Add(npc.GetNpcId());
		}

		Assert.Equal(4, seen.Count);
	}

	/// <summary>An elemental keeps thirty seconds and then goes.</summary>
	[Fact]
	public void AnElementalKeepsThirtySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc controller = harness.Spawn(Controller, 602f, 488f, 196f);
		Npc caller = harness.Spawn(Triroan, 604f, 488f, 196f);
		BossAiHarness.MakeMutuallyKnown(caller, controller);

		NpcMessageBus.Broadcast(caller, BabyElementalControllerAI.CallOne, null, 100f);
		Npc called = Assert.Single(harness.LiveNpcs(),
			n => n.GetNpcId() is Fire or Water or Earth or Air);

		harness.Clock.Advance(TimeSpan.FromSeconds(28));
		Assert.True(called.IsSpawned(), "it went before its thirty seconds were up");

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.False(called.IsSpawned(), "it outlived its thirty seconds");
	}
}
