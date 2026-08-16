using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="FlarestormAI"/>, translated from retail pattern
/// <c>IDCT_Boss_ElementalFire</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// His ladder runs shallowest-first, which is the opposite of every other threshold pattern
/// translated here, so that is what most of these pin.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FlarestormAiTests
{
	private const int Catacombs = 300110000;
	private const int Flarestorm = 216249;
	private const int Calamity = 281646;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Catacombs).WithWorldSize(2048)
			.WithAi(typeof(FlarestormAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Flarestorm, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
		{
			raid.Add(harness.SpawnPlayer(305f + (i * 3), 300f, 200f));
			BossAiHarness.MakeMutuallyKnown(boss, raid[i]);
		}

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static void Hit(Npc boss, Player player) =>
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Calamity);

	/// <summary>Above eighty he calls nothing.</summary>
	[Fact]
	public void AboveEightyHeCallsNothing()
	{
		var (harness, boss, raid) = Engaged(8);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 85);
		for (int i = 0; i < 5; i++)
			Hit(boss, raid[0]);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>The first rung is three calamities, and it fires once.</summary>
	[Fact]
	public void TheFirstRungIsThree()
	{
		var (harness, boss, raid) = Engaged(8);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 79);
		for (int i = 0; i < 5; i++)
			Hit(boss, raid[0]);

		Assert.Equal(3, Count(harness));
	}

	/// <summary>The waves grow as he is worn down: three, then four, then five, then six.</summary>
	[Fact]
	public void TheWavesGrowRungByRung()
	{
		var (harness, boss, raid) = Engaged(8);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 79);
		Hit(boss, raid[0]);
		Assert.Equal(3, Count(harness));

		BossAiHarness.SetExactPercent(boss, 59);
		Hit(boss, raid[0]);
		Assert.Equal(7, Count(harness));

		BossAiHarness.SetExactPercent(boss, 39);
		Hit(boss, raid[0]);
		Assert.Equal(12, Count(harness));

		BossAiHarness.SetExactPercent(boss, 19);
		Hit(boss, raid[0]);
		Assert.Equal(18, Count(harness));
	}

	/// <summary>
	/// Burned down past every rung at once he takes the <b>shallowest</b>, not the deepest — and then
	/// works up the ladder a hit at a time, so every wave still lands.
	/// </summary>
	/// <remarks>
	/// This is the inverse of every other threshold pattern here. Deepest-first would have given him
	/// six calamities on the first hit and nothing after; retail gives three, then four, then five,
	/// then six, on four consecutive hits.
	/// </remarks>
	[Fact]
	public void BurnedDownFastHeWalksUpTheLadder()
	{
		var (harness, boss, raid) = Engaged(8);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 10);

		Hit(boss, raid[0]);
		Assert.Equal(3, Count(harness));

		Hit(boss, raid[0]);
		Assert.Equal(7, Count(harness));

		Hit(boss, raid[0]);
		Assert.Equal(12, Count(harness));

		Hit(boss, raid[0]);
		Assert.Equal(18, Count(harness));

		// And then nothing: all four flags are spent.
		Hit(boss, raid[0]);
		Assert.Equal(18, Count(harness));
	}

	/// <summary>The cap is a cap: a raid smaller than it gets one each and no more.</summary>
	[Fact]
	public void TheWaveIsCappedByTheRaidsSize()
	{
		var (harness, boss, raid) = Engaged(2);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 79);
		Hit(boss, raid[0]);

		Assert.Equal(2, Count(harness));
	}

	/// <summary>
	/// And it goes to the <b>most</b>-hated of that many, not the least — retail's
	/// <c>ORDERI_DESCENDING</c>, which decides whether the wave lands on the tanks or on the healers.
	/// </summary>
	/// <remarks>
	/// Four players twelve metres apart, against five metres of spawn scatter, so which player a
	/// calamity belongs to is unambiguous — and all four inside the fifty-metre <c>valid_distance</c>,
	/// which the first version of this pin was not: spreading them fifteen metres put the back three
	/// out of range entirely, so both orderings picked the same front three and the mutation lived.
	/// With everyone at equal hate the ordering cannot be observed at all, which is how the version
	/// before that missed it.
	/// </remarks>
	[Fact]
	public void TheWaveGoesToTheMostHated()
	{
		BossAiHarness harness = NewHarness();
		using BossAiHarness _h = harness;
		Npc boss = harness.Spawn(Flarestorm, 300f, 300f, 200f);

		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
		{
			raid.Add(harness.SpawnPlayer(305f + (i * 12), 300f, 200f));
			BossAiHarness.MakeMutuallyKnown(boss, raid[i]);
		}

		harness.Engage(boss, raid[0]);
		// Hate descends with the index, so the top three are raid[0..2].
		for (int i = 1; i < raid.Count; i++)
			boss.GetAggroList().AddHate(raid[i], 900 - (i * 100));

		BossAiHarness.SetExactPercent(boss, 79);
		Hit(boss, raid[0]);

		int[] claimed = harness.LiveNpcs().Where(n => n.GetNpcId() == Calamity)
			.Select(c => raid.OrderBy(p => Math.Abs(p.GetX() - c.GetX())).First().GetObjectId())
			.Distinct().OrderBy(id => id).ToArray();

		Assert.Equal(raid.Take(3).Select(p => p.GetObjectId()).OrderBy(id => id).ToArray(), claimed);
	}
}
