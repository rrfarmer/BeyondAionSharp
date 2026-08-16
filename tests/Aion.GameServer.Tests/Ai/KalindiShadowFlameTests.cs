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
}
