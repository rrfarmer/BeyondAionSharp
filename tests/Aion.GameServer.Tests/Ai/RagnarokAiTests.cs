using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="RagnarokAI"/>, translated from retail pattern <c>DF4_FieldRaid</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// A LEGENDARY world boss that was on plain <c>aggressive</c>: he auto-attacked and nothing else, and
/// both NPCs his fight is made of were reachable by nobody.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RagnarokAiTests
{
	private const int Gelkmaros = 220070000;
	private const int Ragnarok = 216576;

	private const int Parasite = 281950;
	private const int Slime = 281951;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(4096)
			.WithAi(typeof(RagnarokAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ragnarok, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(305f + i, 300f, 200f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, Npc boss, List<Player> raid, int seconds)
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

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Nobody has pulled him, so nothing comes — the ladder hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledRagnarokCallsNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Ragnarok, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, Parasite));
	}

	/// <summary>
	/// The first rung is at eighty-five: five parasites on the tank and one on each of the others.
	/// </summary>
	[Fact]
	public void TheFirstRungIsFiveOnTheTankAndOneOnEveryoneElse()
	{
		var (harness, boss, raid) = Engaged(4);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, boss, raid, 7);
		Assert.Equal(0, Count(harness, Parasite));

		BossAiHarness.SetExactPercent(boss, 84);
		Advance(harness, boss, raid, 6);

		// Five on the tank, plus one for each of the four in range.
		Assert.Equal(9, Count(harness, Parasite));
		Assert.Equal(0, Count(harness, Slime));
	}

	/// <summary>The slime only arrives at the deeper rungs, and never on the tank.</summary>
	[Fact]
	public void TheSlimeWaitsForFortyFive()
	{
		var (harness, boss, raid) = Engaged(4);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 64);
		Advance(harness, boss, raid, 6);
		Assert.Equal(0, Count(harness, Slime));

		BossAiHarness.SetExactPercent(boss, 44);
		Advance(harness, boss, raid, 6);
		Assert.Equal(4, Count(harness, Slime));
	}

	/// <summary>
	/// Below thirty-five and below thirty do the same thing behind two flags — retail gives the
	/// slime step twice on the way down, and reading it as one would halve it.
	/// </summary>
	[Fact]
	public void TheSlimeStepHappensTwiceOnTheWayDown()
	{
		var (harness, boss, raid) = Engaged(4);
		using BossAiHarness _h = harness;

		// Walked down one rung at a time, because dropping straight to 34 would let the below-45 rung
		// fire there instead and hide a missing below-35 entirely -- it brings slime too.
		BossAiHarness.SetExactPercent(boss, 44);
		Advance(harness, boss, raid, 6);
		Assert.Equal(4, Count(harness, Slime));

		BossAiHarness.SetExactPercent(boss, 34);
		Advance(harness, boss, raid, 6);
		Assert.Equal(8, Count(harness, Slime));

		BossAiHarness.SetExactPercent(boss, 29);
		Advance(harness, boss, raid, 6);
		Assert.Equal(12, Count(harness, Slime));
	}

	/// <summary>Each rung is a one-shot, so sitting in a band does not keep calling.</summary>
	[Fact]
	public void ARungFiresOnlyOnce()
	{
		var (harness, boss, raid) = Engaged(4);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 84);
		Advance(harness, boss, raid, 40);

		Assert.Equal(9, Count(harness, Parasite));
	}

	/// <summary>
	/// Burned down past every rung at once he takes the deepest — the twenty-five branch outranks the
	/// rest, so a raid that melts him gets one wave rather than six.
	/// </summary>
	[Fact]
	public void BurnedDownFastHeTakesTheDeepestRung()
	{
		var (harness, boss, raid) = Engaged(4);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, boss, raid, 6);

		Assert.Equal(9, Count(harness, Parasite));
		Assert.Equal(0, Count(harness, Slime));
	}

	/// <summary>The cap is retail's: a raid larger than it does not get one each.</summary>
	[Fact]
	public void TheSlimeIsCappedAtFive()
	{
		var (harness, boss, raid) = Engaged(9);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 34);
		Advance(harness, boss, raid, 6);

		Assert.Equal(5, Count(harness, Slime));
	}

	/// <summary>Everything he calls arrives already fighting, and stays five minutes.</summary>
	[Fact]
	public void HisAddsArriveFightingAndStayFiveMinutes()
	{
		var (harness, boss, raid) = Engaged(1);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 84);
		Advance(harness, boss, raid, 7);

		Npc[] parasites = harness.LiveNpcs().Where(n => n.GetNpcId() == Parasite).ToArray();
		Assert.NotEmpty(parasites);
		Assert.All(parasites, p => Assert.Same(raid[0], p.GetTarget()));

		Advance(harness, boss, raid, 290);
		Assert.NotEmpty(harness.LiveNpcs().Where(n => n.GetNpcId() == Parasite));

		Advance(harness, boss, raid, 10);
		Assert.Empty(harness.LiveNpcs().Where(n => n.GetNpcId() == Parasite));
	}
}
