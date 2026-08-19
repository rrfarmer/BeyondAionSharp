using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Unstable Splinterpath's four spider eggs, which hatched a lottery instead of their own brood.
/// </summary>
/// <remarks>
/// <b>There are four eggs, not one egg with four formations.</b> The class rolled a die between
/// retail's four compositions, so which egg the raid broke decided nothing — and it hatched the
/// <c>idabre</c>-prefixed spider family, <b>which no retail pattern spawns anywhere</b>. Found by
/// <c>audit_invented_spawns.py</c>.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class UnstableKaluvaEggAiTests
{
	private const int UnstableSplinterpath = 300600000;

	private const int SpiderBig = 283208;
	private const int SpiderSmall = 283209;

	/// <summary>The three the class used to hatch. None of them is placed by any retail pattern.</summary>
	private static readonly int[] InventedSpiders = [219572, 219573, 219584];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(UnstableSplinterpath).WithWorldSize(2048)
			.WithAi(typeof(UnstableKaluvaSpawnAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Each egg hatches its own brood</b>, and the same egg hatches the same thing every time.
	/// </summary>
	/// <remarks>
	/// The lottery is what this pins: under it, any egg produced any of the four compositions, so the
	/// theory below would have failed on most runs for most rows rather than deterministically.
	/// </remarks>
	[Theory]
	[InlineData(219564, 0, 12)]
	[InlineData(219581, 2, 0)]
	[InlineData(219582, 1, 0)]
	[InlineData(219583, 1, 3)]
	public void EachEggHatchesItsOwnBrood(int eggId, int big, int small)
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(eggId, 300f, 300f, 200f);

		Hatch(egg);

		Assert.Equal(big, Count(harness, SpiderBig));
		Assert.Equal(small, Count(harness, SpiderSmall));
	}

	/// <summary><b>And none of the three invented spiders appears at all.</b></summary>
	[Theory]
	[InlineData(219564)]
	[InlineData(219581)]
	[InlineData(219582)]
	[InlineData(219583)]
	public void TheInventedSpidersNeverHatch(int eggId)
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(eggId, 300f, 300f, 200f);

		Hatch(egg);

		foreach (int invented in InventedSpiders)
			Assert.Equal(0, Count(harness, invented));
	}

	/// <summary>
	/// <b>They scatter within five metres and leave at five minutes</b>, which is retail's
	/// <c>spawn_range</c> and <c>live_time</c>. Both were missing: the brood stacked on the egg's own
	/// point and stood for the rest of the instance.
	/// </summary>
	[Fact]
	public void TheBroodScattersAndExpires()
	{
		using BossAiHarness harness = NewHarness();
		Npc egg = harness.Spawn(219564, 300f, 300f, 200f);

		Hatch(egg);
		var spiders = harness.LiveNpcs().Where(n => n.GetNpcId() == SpiderSmall).ToList();
		Assert.Equal(12, spiders.Count);

		// Scattered, not stacked: twelve on one point would be a single distinct position.
		Assert.True(spiders.Select(s => (s.GetX(), s.GetY())).Distinct().Count() > 1,
			"the brood stacked on the egg's own point");
		Assert.All(spiders, s => Assert.True(
			Math.Abs(s.GetX() - egg.GetX()) <= 5.001f && Math.Abs(s.GetY() - egg.GetY()) <= 5.001f,
			"a spider hatched further than five metres from its egg"));

		harness.Clock.Advance(TimeSpan.FromSeconds(299));
		Assert.Equal(12, Count(harness, SpiderSmall));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, SpiderSmall));
	}

	/// <summary>
	/// Drives the hatch directly. Retail's trigger is a message chain this port does not carry, and the
	/// class's own is a debuff on Kaluva; neither is what these pins are about.
	/// </summary>
	private static void Hatch(Npc egg) =>
		typeof(UnstableKaluvaSpawnAI)
			.GetMethod("HatchAdds", System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Instance)!
			.Invoke(egg.GetAi(), null);
}
