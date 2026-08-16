using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="NobleLapilimaAI"/>, translated from retail pattern
/// <c>IDAbRe_Core_FlyingWorm</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class NobleLapilimaAiTests
{
	private const int AbyssalReliquary = 300240000;
	private const int NobleLapilima = 216946;
	private static readonly int[] Flash = [281918, 281919, 281896];

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(AbyssalReliquary).WithWorldSize(2048)
			.WithAi(typeof(NobleLapilimaAI), typeof(AggressiveNpcAI)).Build();
		Npc worm = harness.Spawn(NobleLapilima, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(worm, player);
		return (harness, worm, player);
	}

	private static void Advance(BossAiHarness harness, Npc worm, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(worm, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Splinters(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Flash.Contains(n.GetNpcId()));

	/// <summary>One that nobody has touched splits nothing off — the chain hangs off being engaged.</summary>
	[Fact]
	public void AnUntouchedWormSplitsNothing()
	{
		BossAiHarness harness = BossAiHarness.For(AbyssalReliquary).WithWorldSize(2048)
			.WithAi(typeof(NobleLapilimaAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		harness.Spawn(NobleLapilima, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, Splinters(harness));
	}

	/// <summary>Ten seconds in it splits off three — one of each flash lapilimo, not three of one.</summary>
	[Fact]
	public void TenSecondsInItSplitsOffThreeDistinctWorms()
	{
		var (harness, worm, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, worm, player, 8);
		Assert.Equal(0, Splinters(harness));

		Advance(harness, worm, player, 4);

		Assert.Equal(3, Splinters(harness));
		Assert.Equal(3, harness.LiveNpcs().Where(n => Flash.Contains(n.GetNpcId()))
			.Select(n => n.GetNpcId()).Distinct().Count());
	}

	/// <summary>
	/// And again every fifteen seconds, uncapped — a fight that drags becomes a swarm, which is why
	/// this worm is meant to be killed rather than tanked.
	/// </summary>
	[Fact]
	public void ItKeepsSplittingEveryFifteenSeconds()
	{
		var (harness, worm, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, worm, player, 12);
		Assert.Equal(3, Splinters(harness));

		Advance(harness, worm, player, 15);
		Assert.Equal(6, Splinters(harness));

		Advance(harness, worm, player, 15);
		Assert.Equal(9, Splinters(harness));
	}

	/// <summary>They land at its feet, which is what makes the swarm build where the worm is.</summary>
	[Fact]
	public void TheSplintersLandAtItsFeet()
	{
		var (harness, worm, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, worm, player, 12);

		foreach (Npc splinter in harness.LiveNpcs().Where(n => Flash.Contains(n.GetNpcId())))
			Assert.True(Math.Abs(splinter.GetX() - worm.GetX()) <= 4f,
				$"a splinter at {splinter.GetX():F1} should be within three metres of {worm.GetX():F1}");
	}
}
