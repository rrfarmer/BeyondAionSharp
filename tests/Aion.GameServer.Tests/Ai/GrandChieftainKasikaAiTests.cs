using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Grand Chieftain Kasika's four guard tiers, two of which never appeared.
/// </summary>
/// <remarks>
/// Retail's <c>NLycan_LELA</c> runs an escalating ladder — a different guard per health band and a rising
/// count:
/// <list type="table">
/// <item><term>61-80</term><description>two of 280469</description></item>
/// <item><term>41-60</term><description>three of 280470</description></item>
/// <item><term>21-40</term><description>four of 280471</description></item>
/// <item><term>below 20</term><description>six of 280472</description></item>
/// </list>
/// <para>
/// Our <c>spawn_helpers.xml</c> spawned the same mix of four at every band — three 280472 and one 280469
/// at the top two, three 280469 and one 280472 at the bottom two — and <b>280470 and 280471 appeared
/// nowhere in our data at all</b>. Two of the four tiers did not exist and the fight did not escalate.
/// </para>
/// <para>
/// Found by <c>audit_summon_numbers.py</c>, which compares the counts and ranges in our summon data
/// against the totals retail's rungs place. This was its clearest row.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GrandChieftainKasikaAiTests
{
	private const int Heiron = 210040000;

	private const int Kasika = 212874;

	/// <summary>Retail's four tiers and their counts, written out rather than read from the data file.</summary>
	public static TheoryData<int, int, int> Tiers() => new TheoryData<int, int, int>
	{
		{ 79, 280469, 2 },
		{ 59, 280470, 3 },
		{ 39, 280471, 4 },
		{ 19, 280472, 6 },
	};

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc kasika = harness.Spawn(Kasika, 2930f, 880f, 369f);
		Player player = harness.SpawnPlayer(2934f, 880f, 369f);
		harness.Engage(kasika, player);
		return kasika;
	}

	/// <summary>
	/// <b>Each health band brings its own tier, in its own number.</b> The whole defect was one mix
	/// repeated, so a pin that only counted adds would have passed against it.
	/// </summary>
	[Theory]
	[MemberData(nameof(Tiers))]
	public void EachBandBringsItsOwnTier(int percent, int guard, int count)
	{
		using BossAiHarness harness = NewHarness();
		Npc kasika = Engaged(harness);

		BossAiHarness.SetExactPercent(kasika, percent);
		kasika.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, kasika);
		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(count, harness.LiveNpcs().Count(n => n.GetNpcId() == guard));
	}

	/// <summary>
	/// <b>All four tiers exist.</b> Two of them were absent from our data entirely, which is invisible to
	/// any pin that only looks at the band it is testing.
	/// </summary>
	[Fact]
	public void AllFourTiersAreReachable()
	{
		using BossAiHarness harness = NewHarness();
		Npc kasika = Engaged(harness);
		HashSet<int> seen = new HashSet<int>();

		foreach (int percent in new[] { 79, 59, 39, 19 })
		{
			BossAiHarness.SetExactPercent(kasika, percent);
			kasika.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, kasika);
			harness.Clock.Advance(TimeSpan.FromSeconds(2));
			foreach (Npc n in harness.LiveNpcs())
				if (n.GetNpcId() is >= 280469 and <= 280472)
					seen.Add(n.GetNpcId());
		}

		Assert.Equal(new[] { 280469, 280470, 280471, 280472 }, seen.OrderBy(i => i));
	}
}
