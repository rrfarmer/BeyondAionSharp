using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ElementalLordAI"/>, translated from retail patterns <c>ND2_FeJ</c>,
/// <c>ND2_AeD</c>, <c>ND2_PeD</c> and <c>ND2_WeH</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Four bosses on plain <c>aggressive</c> and eight adds our server never spawned. The shape worth
/// pinning is that the ladder is a step per band rather than a wave, that leaving the fight clears the
/// room, and that Iprita is the one of the four that does not peel the way the others do — the sort of
/// difference a shared builder makes easy to lose.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ElementalLordAiTests
{
	/// <summary>Theobomos Lab, where all four stand.</summary>
	private const int Lab = 310110000;

	private const int Iprita = 214663;
	private const int Syripne = 214664;
	private const int Nomura = 214665;
	private const int Undine = 214666;

	public static TheoryData<int, int, int> EveryLord => new()
	{
		{ Iprita, 280986, 280987 },
		{ Syripne, 280992, 280993 },
		{ Nomura, 280990, 280991 },
		{ Undine, 280988, 280989 },
	};

	/// <summary>The three that peel; Iprita is deliberately not here.</summary>
	public static TheoryData<int> ThePeelers => new() { Syripne, Nomura, Undine };

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Lab).WithWorldSize(1024)
			.WithAi(typeof(ElementalLordAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId, int raidSize = 3)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 400f, 500f, 186f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(404f + i, 500f, 186f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raidSize; i++)
			for (int n = raidSize - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds,
		bool heal = true)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				if (heal)
					BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Untouched, each of the four calls nobody: the lesser one arrives with the fight.</summary>
	[Theory]
	[MemberData(nameof(EveryLord))]
	public void UnpulledEachLordCallsNobody(int lord, int lesser, int greater)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(lord, 400f, 500f, 186f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, lesser));
		Assert.Equal(0, Count(harness, greater));
	}

	/// <summary>
	/// <b>The ladder is a step per band, not a wave.</b> One lesser elemental with the pull, one greater
	/// on crossing seventy-five, and one of each on crossing thirty — four standing at the end and never
	/// more, however long the fight is spent in any band.
	/// </summary>
	[Theory]
	[MemberData(nameof(EveryLord))]
	public void EachBandPaysItsOwnStepOnce(int lord, int lesser, int greater)
	{
		var (harness, boss, raid) = Engaged(lord);
		using BossAiHarness _h = harness;

		// Ten seconds, not two: the ladder's first tick is at five, and a pin that reads before it
		// passes for a 31-75 band widened to a hundred.
		Advance(harness, raid, boss, 10);
		Assert.Equal(1, Count(harness, lesser));
		Assert.Equal(0, Count(harness, greater));

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 8);
		Assert.Equal(1, Count(harness, lesser));
		Assert.Equal(1, Count(harness, greater));

		// Standing in the band pays nothing more: the rung carries a flag var.
		Advance(harness, raid, boss, 60);
		Assert.Equal(1, Count(harness, greater));

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 8);
		Assert.Equal(2, Count(harness, lesser));
		Assert.Equal(2, Count(harness, greater));

		Advance(harness, raid, boss, 60);
		Assert.Equal(2, Count(harness, lesser));
		Assert.Equal(2, Count(harness, greater));
	}

	/// <summary>
	/// <b>A raid that pushes straight to the end skips a step.</b> The 31–75 rung is out of range from
	/// twenty, so only the deep one lands — two lesser elementals and one greater rather than the four
	/// a fight walked down through the bands produces.
	/// </summary>
	[Fact]
	public void PushedStraightToTwentyItSkipsTheMiddleStep()
	{
		var (harness, boss, raid) = Engaged(Undine);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 20);

		Assert.Equal(2, Count(harness, 280988));
		Assert.Equal(1, Count(harness, 280989));
	}

	/// <summary>
	/// An elemental keeps <b>five minutes</b> and then goes. Followed rather than counted, because the
	/// ladder puts more of them out while the first is still standing.
	/// </summary>
	[Fact]
	public void AnElementalKeepsFiveMinutes()
	{
		var (harness, boss, raid) = Engaged(Nomura);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 2);
		Npc first = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == 280990);

		Advance(harness, raid, boss, 295);
		Assert.True(first.IsSpawned(), "it went before its five minutes were up");

		Advance(harness, raid, boss, 10);
		Assert.False(first.IsSpawned(), "it outlived its five minutes");
	}

	/// <summary>
	/// <b>Losing the fight clears the room.</b> Retail despawns the group on
	/// <c>on_leave_attack_state</c> outright, so a reset does not leave four elementals standing.
	/// </summary>
	[Theory]
	[MemberData(nameof(EveryLord))]
	public void GoingHomeClearsEveryElemental(int lord, int lesser, int greater)
	{
		var (harness, boss, raid) = Engaged(lord);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 20);
		Assert.True(Count(harness, lesser) + Count(harness, greater) >= 3);

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);

		Assert.Equal(0, Count(harness, lesser));
		Assert.Equal(0, Count(harness, greater));
	}

	/// <summary>
	/// <b>Three of the four come off the tank on crossing seventy-five.</b> The second-most-hated
	/// player, and it is what makes the greater elemental's arrival a moment rather than a spawn.
	/// </summary>
	[Theory]
	[MemberData(nameof(ThePeelers))]
	public void ThePeelersTurnOnTheSecondMostHated(int lord)
	{
		var (harness, boss, raid) = Engaged(lord);
		using BossAiHarness _h = harness;

		Assert.Same(raid[0], boss.GetTarget());
		Assert.Same(raid[1], boss.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 8);

		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// <b>And Iprita does not.</b> Her rung calls the same elemental and stays on the tank — a
	/// deliberate difference in an otherwise identical family.
	/// </summary>
	[Fact]
	public void IpritaCallsHerGreaterWithoutPeeling()
	{
		var (harness, boss, raid) = Engaged(Iprita);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 8);

		Assert.Equal(1, Count(harness, 280987));
		Assert.Same(raid[0], boss.GetTarget());
	}

	/// <summary>
	/// <b>Below thirty the three keep peeling, every fifteen seconds.</b> The hate order is turned over
	/// between the two readings, because turning twice onto the same player proves nothing.
	/// </summary>
	[Theory]
	[MemberData(nameof(ThePeelers))]
	public void BelowThirtyThePeelRepeats(int lord)
	{
		var (harness, boss, raid) = Engaged(lord);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 8);
		Assert.Same(raid[1], boss.GetTarget());

		// The peeled-onto player now holds it, which puts the old tank second.
		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(boss, raid[1]);

		Assert.Same(raid[0], boss.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		Advance(harness, raid, boss, 25);
		Assert.Same(raid[0], boss.GetTarget());

		// A third turn, fifteen seconds after the second. Only this one shows the rung re-arming
		// itself rather than firing once off the slot the fight opened with.
		for (int i = 0; i < 10; i++)
			BossAiHarness.Rehate(boss, raid[2]);

		Assert.Same(raid[1], boss.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED));

		Advance(harness, raid, boss, 20);
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// And Iprita's deep rung is a cast: she turns once, at the thirty crossing, and then holds.
	/// </summary>
	[Fact]
	public void IpritaTurnsOnceBelowThirtyAndThenHolds()
	{
		var (harness, boss, raid) = Engaged(Iprita);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 8);
		Assert.Same(raid[1], boss.GetTarget());

		for (int i = 0; i < 5; i++)
			BossAiHarness.Rehate(boss, raid[1]);

		Advance(harness, raid, boss, 60);
		Assert.Same(raid[1], boss.GetTarget());
	}
}
