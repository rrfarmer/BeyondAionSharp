using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="VanukaInfernusAI"/>, translated from retail pattern
/// <c>Dragon_G3</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// He had no AI at all, in an instance where half the boss roster is implemented. Both NPCs asserted
/// here were spawned by nothing anywhere in the server.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VanukaInfernusAiTests
{
	private const int DarkPoeta = 300040000;
	private const int VanukaInfernus = 215282;
	private const int FlameCenter = 281276;
	private const int FaithfulSubordinate = 281275;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta)
			.WithWorldSize(2048)
			.WithAi(typeof(VanukaInfernusAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(VanukaInfernus, 1182f, 1235f, 143f);
		Player player = harness.SpawnPlayer(1184f, 1237f, 143f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Runs the clock while keeping him engaged, and reports the most flames seen at once.</summary>
	private static int PeakFlames(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		int peak = 0;
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			peak = Math.Max(peak, Count(harness, FlameCenter));
		}
		return peak;
	}

	[Fact]
	public void LightsTwoFlamesTheMomentTheFightStarts()
	{
		var (harness, _, _) = Engaged();
		using (harness)
		{
			// Nothing in the server spawned this NPC before; the opener drops a pair.
			Assert.Equal(2, Count(harness, FlameCenter));
		}
	}

	[Fact]
	public void LetsTheOpeningFlamesBurnOutAfterTenSeconds()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			harness.Clock.Advance(TimeSpan.FromSeconds(9));
			Assert.Equal(2, Count(harness, FlameCenter));

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			Assert.Equal(0, Count(harness, FlameCenter));
		}
	}

	[Fact]
	public void DropsAFullRingOfFourOnceHeIsHurt()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 70);

			// Let the opening pair burn out first. They last ten seconds and the first ring lands at
			// six, so measuring from the start would count 2 + 4 and say nothing about the ring.
			harness.Clock.Advance(TimeSpan.FromSeconds(11));

			// The mid-fight steps drop all four points at once, not one. A table that kept only the
			// last spawn per branch would give one flame here, which is how this was nearly written.
			Assert.Equal(4, PeakFlames(harness, boss, player, 40));
		}
	}

	[Fact]
	public void PutsThemAtFourDistinctPoints()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 70);
			for (int i = 0; i < 40 && Count(harness, FlameCenter) < 4; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}

			var points = harness.LiveNpcs().Where(n => n.GetNpcId() == FlameCenter)
				.Select(n => ((int)MathF.Round(n.GetX()), (int)MathF.Round(n.GetY()))).Distinct().ToList();
			Assert.Equal(4, points.Count);
		}
	}

	[Fact]
	public void SwitchesToSummoningBelowThirty()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 25);

			// Below 30 timer 0 hands over to a second chain that summons instead of burning.
			for (int i = 0; i < 60; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
			}
			Assert.True(Count(harness, FaithfulSubordinate) > 0,
				"below 30% he should have summoned a subordinate");
		}
	}

	[Fact]
	public void ClearsEverythingWhenHeDies()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			Assert.True(Count(harness, FlameCenter) > 0);
			boss.GetAi().OnGeneralEvent(AiEventType.Died);
			Assert.Equal(0, Count(harness, FlameCenter));
			Assert.Equal(0, Count(harness, FaithfulSubordinate));
		}
	}
}
