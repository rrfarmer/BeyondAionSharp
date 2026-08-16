using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the shadow flame added to <see cref="CalindiFlamelordAI"/> from retail pattern
/// <c>IDTiamat_Kalrindy</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Kalindi of the Dragon Lord's Refuge (219359), not Dark Poeta's Calindi (215281) — the names differ
/// by a letter and the encounters are unrelated.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KalindiShadowFlameTests
{
	private const int DragonLordsRefuge = 300520000;
	private const int Kalindi = 219359;
	private const int ShadowFlame = 283132;
	private const int DispelWorm = 283059;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(CalindiFlamelordAI), typeof(NoActionAI), typeof(AggressiveNpcAI)).Build();

	private static (BossAiHarness, Npc, Player[]) Engaged(int hpPercent, int players)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kalindi, 470f, 514f, 420f);
		var all = new Player[players];
		for (int i = 0; i < players; i++)
		{
			all[i] = harness.SpawnPlayer(480f + (i * 20f), 514f, 420f);
			BossAiHarness.MakeMutuallyKnown(boss, all[i]);
			BossAiHarness.Rehate(boss, all[i]);
		}

		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, all[0]);
		return (harness, boss, all);
	}

	private static int Flames(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == ShadowFlame);

	/// <summary>
	/// One flame per player at the first rung — the mechanic is per-player, not per-boss.
	/// </summary>
	[Fact]
	public void TheFirstRungDropsOneFlameOnEveryPlayer()
	{
		var (harness, boss, players) = Engaged(79, 3);
		using BossAiHarness _h = harness;

		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, players[0]);

		Assert.Equal(3, Flames(harness));
	}

	/// <summary>
	/// The count climbs with the rungs — one flame each at 80%, then two, three and four.
	/// </summary>
	/// <remarks>
	/// Walked down one rung at a time rather than dropped straight to each threshold. The HP ladder
	/// fires <b>every</b> rung it has crossed, so setting a boss to 39% and hitting it runs the 80, 60
	/// and 40 steps together — six flames a player, not three. The first version of this pin asked for
	/// three and got twelve, which is the ladder behaving correctly and the test asking the wrong
	/// question.
	/// <para>
	/// Flames are allowed to burn out between rungs so each measurement is that rung's alone.
	/// </para>
	/// </remarks>
	[Fact]
	public void EachRungDropsMoreThanTheLast()
	{
		var (harness, boss, players) = Engaged(90, 2);
		using BossAiHarness _h = harness;

		foreach ((int hp, int perPlayer) in new[] { (79, 1), (59, 2), (39, 3), (24, 4) })
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, players[0]);

			Assert.Equal(perPlayer * 2, Flames(harness));

			harness.Clock.Advance(TimeSpan.FromSeconds(16));
			Assert.Equal(0, Flames(harness));
		}
	}

	/// <summary>They land on the players rather than on the boss, which is what makes them dodgeable.</summary>
	[Fact]
	public void TheFlamesLandOnThePlayers()
	{
		var (harness, boss, players) = Engaged(79, 2);
		using BossAiHarness _h = harness;

		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, players[0]);

		Npc[] flames = harness.LiveNpcs().Where(n => n.GetNpcId() == ShadowFlame).ToArray();
		Assert.Equal(2, flames.Length);
		foreach (Npc flame in flames)
		{
			float toNearestPlayer = players.Min(p => Math.Abs(p.GetX() - flame.GetX()));
			Assert.True(toNearestPlayer < Math.Abs(boss.GetX() - flame.GetX()),
				$"a flame at {flame.GetX():F1} should be nearer a player than the boss at {boss.GetX():F1}");
		}
	}

	/// <summary>Each flame lasts fifteen seconds — retail's <c>live_time</c>, and nothing else clears it.</summary>
	[Fact]
	public void TheFlamesBurnOutAfterFifteenSeconds()
	{
		var (harness, boss, players) = Engaged(79, 2);
		using BossAiHarness _h = harness;
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, players[0]);
		Assert.Equal(2, Flames(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		Assert.Equal(2, Flames(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(0, Flames(harness));
	}

	private static int Worms(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == DispelWorm);

	private static void Beat(BossAiHarness harness, Npc boss, Player[] players, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player p in players)
			{
				BossAiHarness.Rehate(boss, p);
				BossAiHarness.KeepAlive(p);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary>
	/// Between 16% and 70% she plants a burrowing dispel on somebody, every twenty-two seconds.
	/// </summary>
	[Fact]
	public void InTheBandSheePlantsADispelWorm()
	{
		var (harness, boss, players) = Engaged(60, 3);
		using BossAiHarness _h = harness;

		Beat(harness, boss, players, 5);

		Assert.Equal(1, Worms(harness));
	}

	/// <summary>Above the band she plants none, however long the fight runs.</summary>
	/// <remarks>
	/// Watched every second rather than counted at the end. A worm lives ten seconds and the interval
	/// is twenty-two, so at any chosen moment the field is usually empty whether the band is honoured
	/// or not — the first version looked at forty seconds and a mutation that ignored the band passed,
	/// because both worms it planted had already burrowed away.
	/// </remarks>
	[Fact]
	public void AboveTheBandSheePlantsNone()
	{
		var (harness, boss, players) = Engaged(90, 3);
		using BossAiHarness _h = harness;

		int everSeen = 0;
		for (int i = 0; i < 40; i++)
		{
			Beat(harness, boss, players, 1);
			everSeen += Worms(harness);
		}

		Assert.Equal(0, everSeen);
	}

	/// <summary>
	/// It lands on somebody who is <b>not</b> the tank — the point of
	/// <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c>.
	/// </summary>
	/// <remarks>
	/// The tank is parked twenty metres from the others so the landing spot is unambiguous. A dispel
	/// on the tank is a dispel on somebody expecting it; the mechanic is that it lands elsewhere.
	/// </remarks>
	[Fact]
	public void TheWormLandsOnSomebodyOtherThanTheTank()
	{
		var (harness, boss, players) = Engaged(60, 3);
		using BossAiHarness _h = harness;
		boss.SetTarget(players[0]);

		Beat(harness, boss, players, 5);

		Npc worm = harness.LiveNpcs().First(n => n.GetNpcId() == DispelWorm);
		Assert.True(Math.Abs(worm.GetX() - players[0].GetX()) > 1f,
			$"the worm at {worm.GetX():F1} should not be on the tank at {players[0].GetX():F1}");
	}

	/// <summary>And it burns out after ten seconds.</summary>
	[Fact]
	public void TheWormBurrowsAwayAfterTenSeconds()
	{
		var (harness, boss, players) = Engaged(60, 3);
		using BossAiHarness _h = harness;
		Beat(harness, boss, players, 5);
		Assert.Equal(1, Worms(harness));

		// Planted on the first three-second beat, so it goes at thirteen; the next is not due until
		// twenty-five, which is what makes fourteen an unambiguous moment to look.
		Beat(harness, boss, players, 9);

		Assert.Equal(0, Worms(harness));
	}
}
