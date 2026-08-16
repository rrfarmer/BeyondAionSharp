using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="AnvilfaceAI"/> and <see cref="DebilkarimTheMakerAI"/>, translated from retail
/// patterns <c>IDTP_NepEx1</c> and <c>IDTP_NepBoss1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class UdasTempleBossesAiTests
{
	private const int LowerUdasTemple = 300160000;

	private const int Anvilface = 215794;
	private const int Shatter = 281424;

	private const int Debilkarim = 215795;
	private const int Nucleus = 281420;
	private const int PyreSoul = 281421;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(LowerUdasTemple).WithWorldSize(2048)
			.WithAi(typeof(AnvilfaceAI), typeof(DebilkarimTheMakerAI), typeof(AggressiveNpcAI))
			.Build();

	/// <summary>Three players at distinct hate, so "third-most-hated" is unambiguous.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
		{
			raid.Add(harness.SpawnPlayer(320f + (i * 20), 300f, 200f));
			BossAiHarness.MakeMutuallyKnown(boss, raid[i]);
		}

		harness.Engage(boss, raid[0]);
		// Descending hate: raid[0] highest, raid[2] lowest, so raid[2] is the third-most-hated.
		boss.GetAggroList().AddHate(raid[1], 500);
		boss.GetAggroList().AddHate(raid[2], 100);
		return (harness, boss, raid);
	}

	private static void Hit(Npc boss, Player player) =>
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Anvilface calls nothing while he is healthy.</summary>
	[Fact]
	public void AnvilfaceCallsNothingAboveFifty()
	{
		var (harness, boss, raid) = Engaged(Anvilface);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Hit(boss, raid[0]);

		Assert.Equal(0, Count(harness, Shatter));
	}

	/// <summary>
	/// At fifty he calls a shatter onto the <b>third</b>-most-hated — not the tank, and not at random.
	/// </summary>
	[Fact]
	public void AnvilfaceCallsAShatterOntoTheThirdMostHated()
	{
		var (harness, boss, raid) = Engaged(Anvilface);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 49);
		Hit(boss, raid[0]);

		Npc shatter = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Shatter));
		Player nearest = raid.OrderBy(p => Math.Abs(p.GetX() - shatter.GetX())).First();
		Assert.Equal(raid[2].GetObjectId(), nearest.GetObjectId());
	}

	/// <summary>Once at fifty and once at thirty — one-shots, not a regime.</summary>
	[Fact]
	public void AnvilfaceCallsOnceAtEachThreshold()
	{
		var (harness, boss, raid) = Engaged(Anvilface);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 49);
		for (int i = 0; i < 5; i++)
			Hit(boss, raid[0]);
		Assert.Equal(1, Count(harness, Shatter));

		BossAiHarness.SetExactPercent(boss, 29);
		for (int i = 0; i < 5; i++)
			Hit(boss, raid[0]);
		Assert.Equal(2, Count(harness, Shatter));
	}

	/// <summary>It arrives already fighting the player it was called onto.</summary>
	/// <remarks>
	/// The hate is what has to be asserted, not the target: a shatter is <c>aggressive</c> and lands on
	/// top of the player, so it engages them by itself within the tick and the target reads the same
	/// either way. Natural aggression is worth one point and retail's <c>hatepoints_to_add</c> of one
	/// goes on top, so two is the fingerprint.
	/// </remarks>
	[Fact]
	public void TheShatterArrivesAlreadyFighting()
	{
		var (harness, boss, raid) = Engaged(Anvilface);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 49);
		Hit(boss, raid[0]);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Npc shatter = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Shatter));
		Assert.Same(raid[2], shatter.GetTarget());
		Assert.True(shatter.GetAggroList().GetHate(raid[2]) >= 2,
			$"one point is what it would aggro on its own; the flag adds retail's on top: "
			+ $"{shatter.GetAggroList().GetHate(raid[2])}");
	}

	/// <summary>
	/// Debilkarim raises seven nuclei at half health, in four rings: two at five metres, two at ten,
	/// two at fifteen and one at twenty.
	/// </summary>
	[Fact]
	public void DebilkarimRaisesSevenNucleiInFourRings()
	{
		var (harness, boss, raid) = Engaged(Debilkarim);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Hit(boss, raid[0]);
		Assert.Equal(0, Count(harness, Nucleus));

		BossAiHarness.SetExactPercent(boss, 50);
		Hit(boss, raid[0]);

		Assert.Equal(7, Count(harness, Nucleus));
	}

	/// <summary>And only once, however long the fight sits in that band.</summary>
	[Fact]
	public void TheNucleiComeOnlyOnce()
	{
		var (harness, boss, raid) = Engaged(Debilkarim);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		for (int i = 0; i < 8; i++)
			Hit(boss, raid[0]);

		Assert.Equal(7, Count(harness, Nucleus));
	}

	/// <summary>
	/// The pyre souls only ever come below nineteen. They are a one-in-ten roll per hit, so what is
	/// pinned is the band rather than the odds.
	/// </summary>
	[Fact]
	public void ThePyreSoulsNeverComeAboveNineteen()
	{
		var (harness, boss, raid) = Engaged(Debilkarim);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 25);
		for (int i = 0; i < 200; i++)
			Hit(boss, raid[0]);

		Assert.Equal(0, Count(harness, PyreSoul));
	}

	/// <summary>And below it they do come — three at a time, on whoever he is fighting.</summary>
	[Fact]
	public void ThePyreSoulsComeInThreesBelowNineteen()
	{
		var (harness, boss, raid) = Engaged(Debilkarim);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 18);
		for (int i = 0; i < 400; i++)
			Hit(boss, raid[0]);

		int souls = Count(harness, PyreSoul);
		Assert.True(souls > 0, "four hundred hits at one in ten should have raised some");
		Assert.True(souls % 3 == 0, $"they come three at a time: {souls}");

		// And it is a roll rather than a certainty: one in ten of four hundred hits is around forty
		// calls, so a hundred and twenty souls. Every hit calling would be twelve hundred, and the
		// bound is far enough out that the roll's own variance cannot reach it.
		Assert.True(souls < 400, $"the one-in-ten roll should have skipped most hits: {souls}");
	}
}
