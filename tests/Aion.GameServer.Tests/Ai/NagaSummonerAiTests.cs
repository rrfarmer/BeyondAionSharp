using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="NagaSummonerAI"/> and <see cref="NagaSubordinateAI"/>, translated from retail
/// patterns <c>Naga_WrF2</c>, <c>Naga_WrF3</c> and <c>Naga_Sum_WrF2</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Heiron's two naga field bosses, both on plain <c>aggressive</c>. The shapes worth pinning are that
/// the wave lands <b>on the player he is fighting</b> rather than at his own feet, that a relay keeps
/// adding to it, and that crossing forty <b>dismisses every one of them</b> — a mechanic hidden in
/// retail behind what looks like an ordinary cast branch.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NagaSummonerAiTests
{
	private const int Heiron = 210040000;

	private const int Brashuna = 212310;
	private const int Gitimuka = 212307;
	private const int Subordinate = 280797;
	private const int GitimukaSubordinate = 280799;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(NagaSummonerAI), typeof(NagaSubordinateAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>Four players, so "third most hated" and "closest to dying" can be different people.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int bossId = Brashuna)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(bossId, 2900f, 2600f, 181f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(2904f + i, 2600f, 181f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Keeps the hate order and leaves the wounded wounded.</summary>
	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId = Subordinate) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static float Apart(VisibleObject a, VisibleObject b)
	{
		float dx = a.GetX() - b.GetX();
		float dy = a.GetY() - b.GetY();
		return MathF.Sqrt((dx * dx) + (dy * dy));
	}

	/// <summary>Above ninety he calls nobody, however long the fight runs.</summary>
	[Fact]
	public void AboveNinetyHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 95);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>Nor at seventy, nor at sixty-one: the wave belongs to one band only.</summary>
	[Fact]
	public void NorAnywhereAboveSixty()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness));
	}

	/// <summary><b>Crossing sixty drops three faithful subordinates.</b></summary>
	[Fact]
	public void CrossingSixtyDropsThree()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 12);

		Assert.Equal(3, Count(harness));
	}

	/// <summary>
	/// <b>And they land on the player he is fighting, not at his own feet.</b> Retail's
	/// <c>spawn_on_target</c> is the whole reason the wave is dangerous, and a spawn-at-his-feet would
	/// pass every other pin here.
	/// </summary>
	[Fact]
	public void TheyLandOnHisQuarryRatherThanOnHim()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Brashuna, 2900f, 2600f, 181f);
		Player quarry = harness.SpawnPlayer(2925f, 2600f, 181f);
		harness.Engage(boss, quarry);
		var only = new List<Player> { quarry };

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, only, boss, 12);

		List<Npc> wave = harness.LiveNpcs().Where(n => n.GetNpcId() == Subordinate).ToList();
		Assert.Equal(3, wave.Count);
		Assert.All(wave, n => Assert.True(Apart(n, quarry) < 9f, $"{Apart(n, quarry)}m from the quarry"));
		Assert.All(wave, n => Assert.True(Apart(n, boss) > 15f, $"{Apart(n, boss)}m from the boss"));
	}

	/// <summary>
	/// <b>A relay adds one more every thirty seconds while he is in the band.</b> Three at the step and
	/// one each at forty, seventy and a hundred seconds.
	/// </summary>
	[Fact]
	public void ARelayAddsOneEveryThirtySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Brashuna, 2900f, 2600f, 181f);
		Player quarry = harness.SpawnPlayer(2905f, 2600f, 181f);
		harness.Engage(boss, quarry);

		BossAiHarness.SetExactPercent(boss, 50);
		int arrived = harness.WatchNew(105, () =>
		{
			BossAiHarness.Rehate(boss, quarry);
			BossAiHarness.KeepAlive(quarry);
		}, Subordinate).Total;

		Assert.Equal(6, arrived);
	}

	/// <summary>
	/// <b>The order names whoever he is fighting.</b> He holds the quarry at arm's length and the
	/// witness stands thirty metres behind him, seventy from anybody — inside his fifty-metre order and
	/// far outside its own reach, so the only way it acquires the quarry is by being told to.
	/// </summary>
	[Fact]
	public void TheOrderNamesWhoeverHeIsFighting()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Brashuna, 2900f, 2600f, 181f);
		Player quarry = harness.SpawnPlayer(2940f, 2600f, 181f);
		harness.Engage(boss, quarry);
		var only = new List<Player> { quarry };

		Npc witness = harness.Spawn(Subordinate, 2870f, 2600f, 181f);
		BossAiHarness.MakeMutuallyKnown(witness, quarry);
		Assert.Null(witness.GetTarget());

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, only, boss, 12);

		// retail's add_hate_point on a message parameter adds hate and leaves the target alone, so the
		// turn this used to assert was ours rather than retail's.
		//
		// AND THE HATE DOES NOT LAND EITHER. AggroList.IsAware refuses hate aimed at a creature the
		// owner is not hostile to, and this answerer is tribe NNAGA, which is not hostile to a player
		// race -- so the answer adds nothing at all and the listener never joins the fight. The forced
		// target was the only thing that ever made this encounter look alive. Asserted as zero and
		// null deliberately: both go red the day the tribe is sorted out. See
		// docs/retail-ai-fidelity.md.
		Assert.Equal(0, witness.GetAggroList().GetHate(quarry));
		Assert.Null(witness.GetTarget());
	}

	/// <summary>
	/// <b>And the relay keeps issuing it.</b> This witness arrives after the step has already spoken,
	/// so the only order it can hear is the relay's thirty seconds later.
	/// </summary>
	/// <remarks>
	/// Added after a mutation sweep: dropping the broadcast from the relay branch left every other pin
	/// green, because they all read the step's order instead.
	/// </remarks>
	[Fact]
	public void AndTheRelayKeepsIssuingIt()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Brashuna, 2900f, 2600f, 181f);
		Player quarry = harness.SpawnPlayer(2940f, 2600f, 181f);
		harness.Engage(boss, quarry);
		var only = new List<Player> { quarry };

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, only, boss, 12);

		Npc latecomer = harness.Spawn(Subordinate, 2870f, 2600f, 181f);
		BossAiHarness.MakeMutuallyKnown(latecomer, quarry);
		Assert.Null(latecomer.GetTarget());

		Advance(harness, only, boss, 30);

		// retail's add_hate_point on a message parameter adds hate and leaves the target alone, so the
		// turn this used to assert was ours rather than retail's.
		//
		// AND THE HATE DOES NOT LAND EITHER. AggroList.IsAware refuses hate aimed at a creature the
		// owner is not hostile to, and this answerer is tribe NNAGA, which is not hostile to a player
		// race -- so the answer adds nothing at all and the listener never joins the fight. The forced
		// target was the only thing that ever made this encounter look alive. Asserted as zero and
		// null deliberately: both go red the day the tribe is sorted out. See
		// docs/retail-ai-fidelity.md.
		Assert.Equal(0, latecomer.GetAggroList().GetHate(quarry));
		Assert.Null(latecomer.GetTarget());
	}

	/// <summary>
	/// <b>Crossing forty dismisses every one of them.</b> Retail's branch is a timer arm and a cast,
	/// which reads like the cast-only branches dropped elsewhere in this log — but the timer it arms
	/// leads to <c>despawn_self</c>, so this is how he clears his own wave.
	/// </summary>
	[Fact]
	public void CrossingFortyDismissesThemAll()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 12);
		Assert.Equal(3, Count(harness));

		BossAiHarness.SetExactPercent(boss, 30);
		Advance(harness, raid, boss, 20);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>And while he stays in the band they stay with him.</summary>
	[Fact]
	public void AboveFortyTheyStay()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 120);

		Assert.True(Count(harness) >= 3, $"{Count(harness)} left standing");
	}

	/// <summary>
	/// <b>Crossing seventy-six takes him off the tank and onto whoever is closest to dying</b>, and the
	/// relay keeps doing it.
	/// </summary>
	[Fact]
	public void SeventySixToNinetyTakesTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 12);
		Assert.Same(raid[3], boss.GetTarget());

		// Heal that one, wound another, and the fifteen-second relay moves him.
		raid[3].GetLifeStats().SetCurrentHpPercent(100);
		raid[2].GetLifeStats().SetCurrentHpPercent(5);

		// The rung armed the relay at thirty seconds, so the move comes at forty rather than at once.
		Advance(harness, raid, boss, 35);
		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary><b>Below twenty he goes for the third-most-hated instead</b>, again and again.</summary>
	[Fact]
	public void BelowTwentyHeTakesTheThird()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Nobody is wounded, so the weakest rule has no opinion and only the hate rule shows.
		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, raid, boss, 12);

		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>Both of his exits take the wave with him.</summary>
	[Fact]
	public void LeavingTheFightClearsTheWave()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 12);
		Assert.Equal(3, Count(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness));
	}

	/// <summary>
	/// <b>Commander Gitimuka is the same fight with his own subordinate and his own delays.</b> He is
	/// the reason the pattern is a builder rather than a class: three delays and one npc id apart.
	/// </summary>
	[Fact]
	public void GitimukaRunsTheSameFightWithHisOwnSubordinate()
	{
		var (harness, boss, raid) = Engaged(Gitimuka);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 12);

		Assert.Equal(3, Count(harness, GitimukaSubordinate));
		Assert.Equal(0, Count(harness));
	}
}
