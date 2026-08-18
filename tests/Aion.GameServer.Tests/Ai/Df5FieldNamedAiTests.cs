using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TidalsailSpiritAI"/>, <see cref="Df5MineAI"/> and
/// <see cref="InfernomaneVortileAI"/>, translated from retail patterns
/// <c>DF5_ItemNamed_6_Ra_01_SSH</c>, <c>DF5_ItemNamed_6_Ra_Summon_SSH</c> and
/// <c>DF5_ItemNamed_6_Wi_01_SSH</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two of the four <c>DF5</c> named field bosses, both HEROes on plain <c>aggressive</c>. One lays
/// eight mines across the raid and sets them off together; the other drops walking fires on a random
/// player and drops more of them, more often, once he is halfway down.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class Df5FieldNamedAiTests
{
	private const int Levinshor = 600100000;

	private const int Tidalsail = 219929;
	private const int Vortile = 219930;
	private const int Mine = 855920;
	private const int Blaze = 282390;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Levinshor).WithWorldSize(2048)
			.WithAi(typeof(TidalsailSpiritAI), typeof(Df5MineAI), typeof(InfernomaneVortileAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>A raid of five, spread out, so "a randomly chosen attacker" can be told apart.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int bossId)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(bossId, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 5; i++)
			raid.Add(harness.SpawnPlayer(304f + (i * 3f), 300f, 200f));

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Standing(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	// ---- the mine layer ----------------------------------------------------------------------

	/// <summary>Nothing is laid in the first ten seconds: the motion comes before the mines.</summary>
	[Fact]
	public void TheFirstPairComesAfterTheMotion()
	{
		var (harness, boss, raid) = Engaged(Tidalsail);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 10);
		Assert.Equal(0, Standing(harness, Mine));

		Advance(harness, raid, boss, 4);
		Assert.Equal(2, Standing(harness, Mine));
	}

	/// <summary><b>Four pairs, six seconds apart, eight mines in all.</b></summary>
	[Fact]
	public void FourPairsSixSecondsApart()
	{
		var (harness, boss, raid) = Engaged(Tidalsail);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 14);
		Assert.Equal(2, Standing(harness, Mine));

		Advance(harness, raid, boss, 6);
		Assert.Equal(4, Standing(harness, Mine));

		Advance(harness, raid, boss, 6);
		Assert.Equal(6, Standing(harness, Mine));

		Advance(harness, raid, boss, 6);
		Assert.Equal(8, Standing(harness, Mine));
	}

	/// <summary>
	/// <b>And six seconds after the last pair they all go off together.</b> Retail's word for it is a
	/// cast we cannot make; the eight of them vanishing at once is the half that shows.
	/// </summary>
	[Fact]
	public void ThenTheyAllGoOffTogether()
	{
		var (harness, boss, raid) = Engaged(Tidalsail);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 32);
		Assert.Equal(8, Standing(harness, Mine));

		Advance(harness, raid, boss, 6);
		Assert.Equal(0, Standing(harness, Mine));
	}

	/// <summary>
	/// <b>The mines are scattered across the raid, not stacked on one player.</b> Retail writes each
	/// spawn as its own <c>ATTACKERI_RANDOM_ONE</c>, and eight rolls over five players landing on one
	/// of them is a one-in-four-hundred-thousand event, so this reads as an ordering rather than a
	/// coin flip.
	/// </summary>
	[Fact]
	public void TheMinesAreScatteredAcrossTheRaid()
	{
		var (harness, boss, raid) = Engaged(Tidalsail);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 32);

		var xs = harness.LiveNpcs().Where(n => n.GetNpcId() == Mine).Select(n => n.GetX()).ToList();
		Assert.Equal(8, xs.Count);
		Assert.True(xs.Max() - xs.Min() > 3f, $"all eight within {xs.Max() - xs.Min():F1}m");
	}

	/// <summary>And the cycle turns over: eleven seconds after the blast, the next pair.</summary>
	[Fact]
	public void ThenTheCycleTurnsOver()
	{
		var (harness, boss, raid) = Engaged(Tidalsail);
		using BossAiHarness _h = harness;

		Advance(harness, raid, boss, 39);
		Assert.Equal(0, Standing(harness, Mine));

		Advance(harness, raid, boss, 18);
		Assert.Equal(2, Standing(harness, Mine));
	}

	// ---- the blaze dropper -------------------------------------------------------------------

	/// <summary>
	/// <b>Above fifty, the first pair of blazes lands twenty-six seconds in</b> — six for the opening
	/// timer and ten for each of the two steps before the drop.
	/// </summary>
	[Fact]
	public void AboveFiftyHeDropsPairs()
	{
		var (harness, boss, raid) = Engaged(Vortile);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 80);

		Advance(harness, raid, boss, 27);
		Assert.Equal(2, Standing(harness, Blaze));
	}

	/// <summary>
	/// <b>Below fifty the loop loses a step and the drops gain one.</b> Two changes in one rung, and
	/// between them the blaze rate nearly doubles.
	/// </summary>
	[Fact]
	public void BelowFiftyHeDropsThreeAndFaster()
	{
		var (harness, boss, raid) = Engaged(Vortile);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 40);

		int arrived = harness.WatchNew(90, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Blaze).Total;

		// Ninety seconds is two full four-step cycles below fifty: four drops of three.
		Assert.Equal(12, arrived);
	}

	/// <summary>Where the upper band's five-step loop fits only three drops of two in the same time.</summary>
	[Fact]
	public void AboveFiftyTheSameNinetySecondsGiveSix()
	{
		var (harness, boss, raid) = Engaged(Vortile);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 80);

		int arrived = harness.WatchNew(90, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Blaze).Total;

		Assert.Equal(6, arrived);
	}

	/// <summary>His death takes the fires with him.</summary>
	[Fact]
	public void DyingClearsTheFires()
	{
		var (harness, boss, raid) = Engaged(Vortile);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 80);

		Advance(harness, raid, boss, 27);
		Assert.Equal(2, Standing(harness, Blaze));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Standing(harness, Blaze));
	}

	/// <summary>
	/// <b>Each drop turns him onto a randomly chosen attacker first</b>, so the fires move around the
	/// raid instead of piling onto the tank.
	/// </summary>
	/// <remarks>
	/// Random, so it is read over nine drops rather than one: nine rolls over five players all landing
	/// on the same one is about four in a million. Added after a mutation sweep — removing the switch
	/// left every other pin green, because they all count fires rather than look at where they are.
	/// </remarks>
	[Fact]
	public void EachDropTurnsHimOntoSomebodyNew()
	{
		var (harness, boss, raid) = Engaged(Vortile);
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 40);

		var seen = new HashSet<int>();
		harness.WatchNew(200, () =>
		{
			foreach (Npc fire in harness.LiveNpcs().Where(n => n.GetNpcId() == Blaze))
				seen.Add((int)Math.Round(fire.GetX()));

			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Blaze);

		// Five players stand three metres apart, and the fires land within two of whoever was picked.
		Assert.True(seen.Max() - seen.Min() > 3, $"every fire within {seen.Max() - seen.Min()}m");
	}
}
