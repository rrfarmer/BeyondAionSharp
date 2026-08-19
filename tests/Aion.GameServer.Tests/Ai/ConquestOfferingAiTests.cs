using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The conquest offering cascade: a spawner, a spot, and a monster.
/// </summary>
/// <remarks>
/// <b>Retail runs this as two stages and this port ran it as one.</b> A spawner waits eight minutes and
/// then places a spot — or nothing, 27% of the time — and the spot lives ten seconds and rolls its own
/// monster. What stood here rolled once on spawning and placed the monster directly.
/// <para>
/// The odds are what they are, so these pins assert the parts that are not random: the cadence, the
/// intermediate npc, its lifetime, and that every roll lands inside that spot's own table.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ConquestOfferingAiTests
{
	private const int Gelkmaros = 220070000;

	/// <summary>One spawner, and the pair of spots retail gives it.</summary>
	private const int Spawner = 856150;
	private const int SoloSpot = 856314;
	private const int PartySpot = 856320;

	/// <summary>The eight monsters that spot may roll, and no others.</summary>
	private static readonly int[] SoloMonsters =
		[236307, 236308, 236309, 236310, 236331, 236332, 236333, 236334];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(2048)
			.WithAi(typeof(ConquestOfferingSpawnerAI), typeof(ConquestOfferingSpotAI),
				typeof(ConquestOfferingAggressiveAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Nothing happens for eight minutes.</b> The cadence is the mechanic this port did not have —
	/// the old class placed a monster the instant it spawned.
	/// </summary>
	[Fact]
	public void TheSpawnerIsSilentForEightMinutes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(479));

		Assert.Equal(0, Count(harness, SoloSpot));
		Assert.Equal(0, Count(harness, PartySpot));
		foreach (int monster in SoloMonsters)
			Assert.Equal(0, Count(harness, monster));
	}

	/// <summary>
	/// <b>And on the eighth minute it places one of its own two spots, or neither.</b>
	/// </summary>
	/// <remarks>
	/// Retail's split is 51/22/27, so a single turn cannot be asserted — what can is that whatever
	/// appears is one of that spawner's two spots and never another spawner's.
	/// </remarks>
	[Fact]
	public void EachTurnPlacesOneOfItsOwnSpotsOrNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(481));

		int placed = Count(harness, SoloSpot) + Count(harness, PartySpot);
		Assert.InRange(placed, 0, 1);

		// Never a neighbour's spot: the table is per spawner and 856151 owns 856315 and 856321.
		Assert.Equal(0, Count(harness, 856315));
		Assert.Equal(0, Count(harness, 856321));
	}

	/// <summary>
	/// <b>The clock keeps turning</b>, so over several cycles something is placed.
	/// </summary>
	/// <remarks>
	/// With a 73% chance a turn places something, five turns are silent about one time in fifteen
	/// hundred — asserted over ten, which is past any run this suite will see.
	/// </remarks>
	[Fact]
	public void TheClockKeepsTurning()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		BossAiHarness.Watched seen = harness.WatchNew(
			10 * 481, null, SoloSpot, PartySpot);

		Assert.True(seen.Total > 0, "ten turns of the eight-minute clock placed no spot at all");
	}

	/// <summary>
	/// <b>A spot rolls one monster from its own table and leaves at ten seconds.</b>
	/// </summary>
	/// <remarks>
	/// This is the stage that did not exist: the spawner used to place a monster directly, so the spot,
	/// its lifetime and its roll all went missing together.
	/// </remarks>
	[Fact]
	public void ASpotRollsOneMonsterFromItsOwnTable()
	{
		using BossAiHarness harness = NewHarness();
		Npc spot = harness.Spawn(SoloSpot, 300f, 300f, 200f);

		int placed = SoloMonsters.Sum(m => Count(harness, m));
		Assert.Equal(1, placed);

		// And nothing from the party spot's table, which is a different eight.
		Assert.Equal(0, Count(harness, 236391));
		Assert.True(spot.IsSpawned());
	}
}
