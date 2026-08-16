using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AkairunOfMedeusAI"/>, translated from retail pattern <c>ND2_AhB</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The odd one out in this family: his fight is almost entirely about who he is hitting. Every band
/// opens a target-switch loop of its own and none of them ever closes, so a raid that walks him down
/// is being peeled from three clocks at once.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AkairunOfMedeusAiTests
{
	private const int Heiron = 210040000;

	private const int Akairun = 212008;
	private const int Protector = 280816;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(AkairunOfMedeusAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>Four players, so "second most hated" and "weakest" can be different people.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Akairun, 2900f, 2600f, 181f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(2904f + i, 2600f, 181f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Keeps the hate order and leaves the wounded wounded.</summary>
	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Protector);

	/// <summary>Above eighty-five nothing opens: he holds whoever is holding him.</summary>
	[Fact]
	public void AboveEightyFiveHeHoldsHisTank()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 60);

		Assert.Same(raid[0], boss.GetTarget());
	}

	/// <summary>
	/// <b>Crossing eighty-five opens a loop that takes whoever is closest to dying</b>, every
	/// twenty-five seconds from then on.
	/// </summary>
	[Fact]
	public void TheFirstLoopTakesTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 30);
		Assert.Same(raid[3], boss.GetTarget());

		// It keeps going: heal that one, wound another, and the next tick moves him.
		raid[3].GetLifeStats().SetCurrentHpPercent(100);
		raid[2].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 30);
		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>
	/// <b>And crossing sixty-five opens a second one that takes the second-most-hated instead</b> —
	/// a different rule on a different clock, running alongside the first.
	/// </summary>
	[Fact]
	public void TheSecondLoopTakesTheSecondMostHated()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Nobody is wounded, so the weakest-loop has no opinion and only the hate rule shows.
		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 30);

		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>
	/// <b>The wave comes at 46–65, and our version is a quarter of retail's.</b> Three of the four are
	/// placed at the start of a walker route we cannot resolve; the fourth is at his own feet.
	/// </summary>
	[Fact]
	public void OneProtectorArrivesWhereRetailPlacesFour()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, raid, boss, 30);
		Assert.Equal(0, Count(harness));

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 12);
		Assert.Equal(1, Count(harness));

		// Once, however long he stays in the band.
		Advance(harness, raid, boss, 90);
		Assert.Equal(1, Count(harness));
	}

	/// <summary>
	/// And it is the band that holds the wave, not the ladder stopping: at thirty-five percent the
	/// clock is still running and no protector comes.
	/// </summary>
	/// <remarks>
	/// Below twenty-five the ladder is dead, so a wave band widened downward would be invisible there
	/// — the deep rung wins first-match and takes the clock with it. Thirty-five is the health at
	/// which a wrong lower bound actually shows.
	/// </remarks>
	[Fact]
	public void AtThirtyFiveTheWaveStillDoesNotCome()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 35);
		Advance(harness, raid, boss, 90);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>
	/// <b>Below twenty-five the ladder stops.</b> A raid that pushes him straight there opens no band
	/// loops and gets no wave — only the fast deep loop.
	/// </summary>
	[Fact]
	public void PushedStraightBelowTwentyFiveNoWaveComes()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>And the deep loop itself keeps taking the weakest, on a faster clock.</summary>
	[Fact]
	public void TheDeepLoopKeepsTakingTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 20);
		Assert.Same(raid[3], boss.GetTarget());

		raid[3].GetLifeStats().SetCurrentHpPercent(100);
		raid[1].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 30);
		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary>Both exits clear the wave.</summary>
	[Fact]
	public void BothExitsClearTheWave()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Advance(harness, raid, boss, 12);
		Assert.Equal(1, Count(harness));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness));
	}
}
