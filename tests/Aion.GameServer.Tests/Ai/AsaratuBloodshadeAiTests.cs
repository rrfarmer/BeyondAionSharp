using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="AsaratuBloodshadeAI"/>, translated from retail pattern
/// <c>Dragon_G4</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// He ran on plain <c>aggressive</c> until now, so none of this happened: no flame centers, no
/// summoning below 20%, and no chain at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AsaratuBloodshadeAiTests
{
	private const int DarkPoeta = 300040000;
	private const int AsaratuBloodshade = 215283;
	private const int FlameCenter = 281246;
	private const int FaithfulSubordinate = 281245;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta)
			.WithWorldSize(2048)
			.WithAi(typeof(AsaratuBloodshadeAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(AsaratuBloodshade, 1182f, 1235f, 143f);
		Player player = harness.SpawnPlayer(1184f, 1237f, 143f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static void Fight(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	[Fact]
	public void LeavesNoFlameWhileHeIsHealthy()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			// The full-health band runs the chain but drops nothing; the flames start below 80.
			Fight(harness, boss, player, 60);
			Assert.Equal(0, Count(harness, FlameCenter));
		}
	}

	[Fact]
	public void LeavesFlamesOnceHeIsBelowEighty()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 70);
			Fight(harness, boss, player, 40);
			Assert.True(Count(harness, FlameCenter) > 0,
				"the 51-80 step should have left a flame center behind");
		}
	}

	[Fact]
	public void KeepsLeavingFlamesFurtherDown()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 40);
			Fight(harness, boss, player, 60);
			Assert.True(Count(harness, FlameCenter) > 0,
				"the 21-50 step should have left a flame center behind");
		}
	}

	[Fact]
	public void SummonsOnlyBelowTwenty()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 40);
			Fight(harness, boss, player, 60);
			Assert.Equal(0, Count(harness, FaithfulSubordinate));

			BossAiHarness.SetHpPercent(boss, 15);
			Fight(harness, boss, player, 40);
			Assert.True(Count(harness, FaithfulSubordinate) > 0,
				"below 20% timer 9 should have started summoning");
		}
	}

	[Fact]
	public void ClearsEverythingWhenHeDies()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.SetHpPercent(boss, 15);
			Fight(harness, boss, player, 60);
			Assert.True(Count(harness, FlameCenter) + Count(harness, FaithfulSubordinate) > 0);

			boss.GetAi().OnGeneralEvent(AiEventType.Died);
			Assert.Equal(0, Count(harness, FlameCenter));
			Assert.Equal(0, Count(harness, FaithfulSubordinate));
		}
	}
}
