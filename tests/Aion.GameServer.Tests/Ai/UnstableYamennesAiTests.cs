using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the portal cadence in <see cref="UnstableYamennesAI"/>, corrected against retail patterns
/// <c>IDAbRe_Core_NamedD_02</c> and <c>IDAbRe_Core_NamedD_Hard_02</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The alternation itself was already right and is pinned here too, since nothing else covered it: a
/// wave upstairs, then one downstairs, then upstairs again. Only the timing changed — retail opens at
/// 30s and repeats every 65, where this waited a flat 60 both times.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class UnstableYamennesAiTests
{
	private const int UnstableSplinterpath = 300600000;
	private const int DurableYamennes = 219555;
	private const int Painflare = 219563;

	private const int ProtectorsFury = 281819;
	private const int YamennesSliver = 282065;

	/// <summary>The three gates, spawned as a set at whichever floor is due.</summary>
	private static readonly int[] Gates = { 219567, 219579, 219580 };

	/// <summary>Upstairs sits around z 216, downstairs around z 198.</summary>
	private const float UpstairsZ = 210f;

	private static (BossAiHarness, Npc, Player) Engaged() => EngagedSingle(DurableYamennes);

	private static (BossAiHarness, Npc, Player) EngagedSingle(int npcId)
	{
		BossAiHarness harness = BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(UnstableYamenessPortalSummonedAI))
			.Build();
		Npc boss = harness.Spawn(npcId, 330f, 730f, 216f);
		Player player = harness.SpawnPlayer(332f, 732f, 216f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId, int raidSize)
	{
		BossAiHarness harness = BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(UnstableYamenessPortalSummonedAI))
			.Build();
		Npc boss = harness.Spawn(npcId, 330f, 730f, 216f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(332f + i, 732f, 216f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static List<Npc> LiveGates(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => Gates.Contains(n.GetNpcId())).ToList();

	[Fact]
	public void OpensItsFirstPortalsAtThirtySecondsNotSixty()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(29));
			Assert.Empty(LiveGates(harness));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(3, LiveGates(harness).Count);
		}
	}

	[Fact]
	public void AlternatesFloorsOnASixtyFiveSecondCycle()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(30));
			bool firstUpstairs = LiveGates(harness).All(g => g.GetZ() > UpstairsZ);

			// Nothing new at the old 60s mark; the first wave is at 30 and the next 65 later, at 95.
			harness.Clock.Advance(TimeSpan.FromSeconds(64));
			Assert.Equal(3, LiveGates(harness).Count);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			List<Npc> second = LiveGates(harness);
			Assert.True(second.Count > 3, "a second set of portals should have opened at 95s");

			// The new ones are on the other floor.
			bool secondUpstairs = second.OrderByDescending(g => g.GetObjectId()).First().GetZ() > UpstairsZ;
			Assert.NotEqual(firstUpstairs, secondUpstairs);

			// The first set times out 70s after it opened, at 100s, leaving only the second. Without a
			// lifetime the two sets would simply accumulate, which is what let the old version stall
			// once nobody killed the portals.
			harness.Clock.Advance(TimeSpan.FromSeconds(6));
			List<Npc> remaining = LiveGates(harness);
			Assert.Equal(3, remaining.Count);
			Assert.All(remaining, g => Assert.Equal(secondUpstairs, g.GetZ() > UpstairsZ));
		}
	}

	/// <summary>
	/// Protector's fury, retail's <c>IDCatacombs_Hard_Buff</c>: one on each of the most-hated, a minute
	/// into the fight and every twenty seconds after. Neither this nor the sliver below was spawned by
	/// anything in the server before — the portal cadence was corrected here earlier without noticing
	/// that two of the encounter's adds had no source at all.
	/// </summary>
	[Theory]
	[InlineData(DurableYamennes, 2)]
	[InlineData(Painflare, 3)]
	public void ItDropsProtectorsFuryOnTheMostHated(int npcId, int expected)
	{
		var (harness, boss, raid) = Engaged(npcId, raidSize: 5);
		using BossAiHarness _h = harness;

		// Nothing before the minute is up.
		for (int i = 0; i < 58; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		for (int i = 0; i < 4; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(expected, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));
	}

	/// <summary>Each fury lasts ten seconds, so a wave is gone well before the next is due.</summary>
	[Fact]
	public void EachFuryWaveTimesOutBeforeTheNext()
	{
		var (harness, boss, raid) = Engaged(DurableYamennes, raidSize: 5);
		using BossAiHarness _h = harness;

		void Tick(int seconds)
		{
			for (int i = 0; i < seconds; i++)
			{
				foreach (Player member in raid)
				{
					BossAiHarness.Rehate(boss, member);
					BossAiHarness.KeepAlive(member);
				}
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}
		}

		Tick(62);
		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		Tick(12);
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));

		// And the next wave arrives on the twenty-second cycle.
		Tick(10);
		Assert.Equal(2, harness.LiveNpcs().Count(n => n.GetNpcId() == ProtectorsFury));
	}

	/// <summary>
	/// Yamennes slivers, retail's <c>IDAbRe_Core_Sum_NamedD_onDie</c>: left where the top of the hate
	/// list stood, one for Durable and two for Painflare, and they have no lifetime.
	/// </summary>
	[Theory]
	[InlineData(DurableYamennes, 1)]
	[InlineData(Painflare, 2)]
	public void ItLeavesSliversBehindWhenItFalls(int npcId, int expected)
	{
		var (harness, boss, player) = EngagedSingle(npcId);
		using BossAiHarness _h = harness;
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == YamennesSliver));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(expected, harness.LiveNpcs().Count(n => n.GetNpcId() == YamennesSliver));
	}
}
