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

	/// <summary>The three gates, spawned as a set at whichever floor is due.</summary>
	private static readonly int[] Gates = { 219567, 219579, 219580 };

	/// <summary>Upstairs sits around z 216, downstairs around z 198.</summary>
	private const float UpstairsZ = 210f;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(UnstableSplinterpath)
			.WithWorldSize(2048)
			.WithAi(typeof(UnstableYamennesAI), typeof(AggressiveNpcAI), typeof(UnstableYamenessPortalSummonedAI))
			.Build();
		Npc boss = harness.Spawn(DurableYamennes, 330f, 730f, 216f);
		Player player = harness.SpawnPlayer(332f, 732f, 216f);
		harness.Engage(boss, player);
		return (harness, boss, player);
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
}
