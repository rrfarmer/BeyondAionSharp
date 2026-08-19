using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Chief maid Miladi, who summoned nothing at all until now.
/// </summary>
/// <remarks>
/// She ran <c>aggressive</c>, so retail's sixteen timer branches and six summons were a plain melee npc.
/// <b>Her mechanic is that the succubi land on players rather than on her</b> — the second and third most
/// hated get one each — so pinning her means checking <i>where</i> the adds appear, not how many.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ChiefMaidMiladiAiTests
{
	private const int AdmaStronghold = 320130000;
	private const int Miladi = 214693;
	private const int Succubus = 280963;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AdmaStronghold).WithWorldSize(2048)
			.WithAi(typeof(ChiefMaidMiladiAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Succubi(BossAiHarness harness) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == Succubus).ToList();

	/// <summary><b>Engaging places one succubus.</b></summary>
	[Fact]
	public void EngagingPlacesASuccubus()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(499f, 577f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		Assert.Single(Succubi(harness));
	}

	/// <summary>
	/// <b>And it lands on the player, not on her.</b> The whole point of
	/// <c>spawn_on_target_by_attacker_indicator</c>: a succubus at her feet would be a different fight.
	/// </summary>
	/// <remarks>
	/// <b>This pin was flaky, and the flakiness was a real bug.</b> Its first version asserted only that
	/// the succubus stood nearer the player than Miladi, which retail's <c>spawn_range=0</c> makes
	/// trivially true — but the class had read retail's <c>valid_distance=50</c> as the scatter, so the
	/// add went anywhere within fifty metres of the player. Fifty is further than she stands from the
	/// raid, so it sometimes landed nearer her, and the pin failed about one run in six. Now the
	/// assertion is the exact one: <b>on the player</b>.
	/// </remarks>
	[Fact]
	public void TheSuccubusLandsOnThePlayer()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(520f, 600f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		Npc succubus = Assert.Single(Succubi(harness));

		Assert.Equal(tank.GetX(), succubus.GetX(), 1);
		Assert.Equal(tank.GetY(), succubus.GetY(), 1);
	}

	/// <summary>
	/// <b>And an attacker further off than fifty metres gets none.</b> Retail's <c>valid_distance</c>,
	/// which is the number the scatter used to be read as.
	/// </summary>
	[Fact]
	public void AnAttackerBeyondFiftyMetresGetsNoSuccubus()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player distant = harness.SpawnPlayer(497f, 700f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, distant);
		harness.Engage(miladi, distant);

		Assert.Empty(Succubi(harness));
	}

	/// <summary><b>And it leaves at twelve seconds</b>, which is retail's <c>live_time</c>.</summary>
	[Fact]
	public void TheSuccubusLeavesAtTwelveSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		Player tank = harness.SpawnPlayer(499f, 577f, 189.49f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(miladi, tank);
		harness.Engage(miladi, tank);

		var first = Succubi(harness).ToHashSet();
		Assert.NotEmpty(first);

		harness.Clock.Advance(TimeSpan.FromSeconds(11));
		Assert.All(first, s => Assert.Contains(s, harness.LiveNpcs()));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}

	private static float PositionUtilDistance(Npc a, Creature b) =>
		(float)Math.Sqrt(((a.GetX() - b.GetX()) * (a.GetX() - b.GetX()))
			+ ((a.GetY() - b.GetY()) * (a.GetY() - b.GetY())));

	/// <summary>
	/// Engages her with a raid, so the second and third most hated exist and are distinct.
	/// </summary>
	private static (BossAiHarness, Npc, List<Player>) EngagedByARaid()
	{
		BossAiHarness harness = NewHarness();
		Npc miladi = harness.Spawn(Miladi, 497f, 575f, 189.49f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(499f + i, 577f, 189.49f, race: Race.ELYOS));

		foreach (Player member in raid)
			BossAiHarness.MakeMutuallyKnown(miladi, member);
		harness.Engage(miladi, raid[0]);

		// Distinct hate, so MOST/SECOND/THIRD_MOST_HATED pick three different people.
		for (int i = 0; i < raid.Count; i++)
			for (int j = 0; j <= i; j++)
				BossAiHarness.Rehate(miladi, raid[raid.Count - 1 - i]);

		return (harness, miladi, raid);
	}

	/// <summary>
	/// <b>Between seventy-five and thirty-one she puts one on the second most hated</b>, once.
	/// </summary>
	/// <remarks>
	/// <b>Four of this class's five spawns were asserted by no pin</b> — only the opener was — which the
	/// mutation harness found by deleting each in turn and watching the suite stay green. The bands are
	/// the fight: the opener is one succubus on the tank, and everything that makes her interesting is
	/// below seventy-five.
	/// </remarks>
	[Fact]
	public void TheMidBandPlacesOneOnTheSecondMostHated()
	{
		var (harness, miladi, raid) = EngagedByARaid();
		using BossAiHarness _h = harness;

		int opener = Succubi(harness).Count;
		BossAiHarness.SetHpPercent(miladi, 60);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		Assert.Equal(opener + 1, Succubi(harness).Count);
	}

	/// <summary>
	/// <b>And below thirty she opens on two at once</b> — the second and third most hated — and turns
	/// onto the third.
	/// </summary>
	[Fact]
	public void TheLowBandPlacesTwoAndSwitchesToTheThird()
	{
		var (harness, miladi, raid) = EngagedByARaid();
		using BossAiHarness _h = harness;

		int opener = Succubi(harness).Count;
		BossAiHarness.SetHpPercent(miladi, 25);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		Assert.Equal(opener + 2, Succubi(harness).Count);
	}

	/// <summary>
	/// <b>And the low band keeps placing one every ten seconds</b> after it opens, on the third most
	/// hated — retail's <c>BTIMERI_INDEX_1</c>, which is a regime rather than a step.
	/// </summary>
	[Fact]
	public void TheLowBandKeepsPlacingAfterItOpens()
	{
		var (harness, miladi, raid) = EngagedByARaid();
		using BossAiHarness _h = harness;

		BossAiHarness.SetHpPercent(miladi, 25);
		harness.Clock.Advance(TimeSpan.FromSeconds(6));

		// Counted as arrivals: a succubus lives twelve seconds, so the opening pair has gone by the
		// time the third cycle lands and a standing count would not separate them.
		BossAiHarness.Watched later = harness.WatchNew(
			22, () => { foreach (Player member in raid) BossAiHarness.Rehate(miladi, member); },
			Succubus);

		Assert.True(later.Total >= 2, $"the ten-second loop placed {later.Total} in twenty-two seconds");
	}

	/// <summary>
	/// <b>Above seventy-five she adds nothing to the opener.</b>
	/// </summary>
	/// <remarks>
	/// The band pins assert that each band fires; this asserts that <b>nothing fires outside one</b>,
	/// which is the half they cannot cover. Found by mutating her HP guards: widening either of them to
	/// the full range left the suite green, because every pin stood inside the band it was testing.
	/// <para>
	/// A full minute, so the five-second heartbeat has turned a dozen times.
	/// </para>
	/// </remarks>
	[Fact]
	public void AboveSeventyFiveSheAddsNothingToTheOpener()
	{
		var (harness, miladi, raid) = EngagedByARaid();
		using BossAiHarness _h = harness;

		int opener = Succubi(harness).Count;
		Assert.Equal(1, opener);

		BossAiHarness.SetHpPercent(miladi, 90);
		BossAiHarness.Watched later = harness.WatchNew(
			60, () => { foreach (Player member in raid) BossAiHarness.Rehate(miladi, member); },
			Succubus);

		Assert.Equal(0, later.Total);
	}
}
