using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for Tiamat's three incarnations, translated from their retail patterns
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The mechanic they replace was invented — two hazards on two random players every 30s, running on a
/// timer that started when the boss activated rather than when anyone fought it — so what is asserted
/// here is where the hazards come from, not merely that they appear. The three share one fight with a
/// different element each, which is why the same assertions run against all three.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatsIncarnationAiTests
{
	private const int DragonLordsRefuge = 300520000;

	private const int Fissurefang = 219365;
	private const int Graviwing = 219366;
	private const int Petriscale = 219368;

	private const int CavityOfEarth = 282735;
	private const int GravityWhirlpool = 282727;
	private const int PetrificationCrystal = 282731;

	private const int BurrowingAttack = 283060;

	private const int Wrathclaw = 219367;
	private const int SphereOfWrath = 282979;
	private const int SphereOfPeace = 282733;

	private const int Smash = 20145;
	private const int IncarnateSurge = 20146;
	private const int Bite = 20105;

	private static readonly TimeSpan FirstPowerAtk = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan FirstAreaAtk = TimeSpan.FromSeconds(15);

	public static TheoryData<int, int> Incarnations => new TheoryData<int, int>
	{
		{ Fissurefang, CavityOfEarth },
		{ Graviwing, GravityWhirlpool },
		{ Petriscale, PetrificationCrystal },
	};

	private static BossAiHarness NewHarness() => BossAiHarness.For(DragonLordsRefuge)
		.WithWorldSize(2048)
		.WithAi(typeof(TiamatsIncarnationAI), typeof(TiamatsIncarnationSpawnsAI), typeof(AggressiveNpcAI),
			typeof(GeneralNpcAI))
		.Build();

	/// <summary>The crack they close on death sits near 478/514, so they fight where they stand.</summary>
	private static Npc SpawnBoss(BossAiHarness harness, int npcId) =>
		harness.Spawn(npcId, 470f, 510f, 418f);

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// Retail caps every <c>spawn_on_multi_target</c> with <c>total_set_to_spawn</c>, and each
	/// incarnation's area attack has its own number: three for Fissurefang and Petriscale, one for
	/// Graviwing. Uncapped — as this port originally was — a full alliance takes one hazard each, so
	/// Fissurefang dropped one per player instead of three.
	/// </summary>
	[Theory]
	[InlineData(Fissurefang, CavityOfEarth, 3)]
	[InlineData(Petriscale, PetrificationCrystal, 3)]
	[InlineData(Graviwing, GravityWhirlpool, 1)]
	public void TheAreaAttackHazardStopsAtItsRetailCap(int npcId, int hazard, int cap)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);

		// Six in range and all hating it — comfortably more than any of the three caps.
		var raid = new List<Player>();
		for (int i = 0; i < 6; i++)
			raid.Add(harness.SpawnPlayer(472f + i, 512f, 418f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		// Past the power attack at 3s, and on to the area attack at 15s.
		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		int beforeArea = Count(harness, hazard);
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(cap, Count(harness, hazard) - beforeArea);
	}

	/// <summary>
	/// The three hazards do not live equally long: 25 seconds for Fissurefang, 20 for Petriscale and
	/// only 12 for Graviwing. The port used to give all three 20.
	/// </summary>
	/// <remarks>
	/// The power attack keeps dropping its own hazards every nine seconds, so a count cannot tell you
	/// which ones aged out. This follows the exact objects the area attack placed.
	/// </remarks>
	[Theory]
	[InlineData(Fissurefang, CavityOfEarth, 25)]
	[InlineData(Petriscale, PetrificationCrystal, 20)]
	[InlineData(Graviwing, GravityWhirlpool, 12)]
	public void EachAreaHazardLivesForItsOwnRetailSpan(int npcId, int hazard, int seconds)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		HashSet<Npc> before = harness.LiveNpcs().Where(n => n.GetNpcId() == hazard).ToHashSet();
		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		List<Npc> fromArea = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == hazard && !before.Contains(n)).ToList();
		Assert.NotEmpty(fromArea);

		harness.Clock.Advance(TimeSpan.FromSeconds(seconds - 2));
		Assert.All(fromArea, h => Assert.True(h.IsSpawned(),
			$"hazard should still be standing two seconds short of {seconds}"));

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.All(fromArea, h => Assert.False(h.IsSpawned(),
			$"hazard should have aged out a second past {seconds}"));
	}

	/// <summary>
	/// Petriscale is the only one whose <i>power</i> attack is raid-wide, and retail caps that at two
	/// — a tighter cap than its area attack's three.
	/// </summary>
	[Fact]
	public void PetriscalesPowerAttackCrystalsStopAtTwo()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Petriscale);
		var raid = new List<Player>();
		for (int i = 0; i < 6; i++)
			raid.Add(harness.SpawnPlayer(472f + i, 512f, 418f));
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Assert.Equal(2, Count(harness, PetrificationCrystal));
	}

	[Theory]
	[MemberData(nameof(Incarnations))]
	public void DropsItsFirstHazardWithItsPowerAttackThreeSecondsIn(int npcId, int hazard)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);
		BossAiHarness.DrainQueuedSkills(boss);

		// Nothing at all until the first timer comes due: the old cycle placed hazards on a schedule
		// that began at activation, whether or not the fight had started.
		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, hazard));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.True(Count(harness, hazard) > 0, "the power attack should have left a hazard behind");
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Smash);
	}

	[Theory]
	[MemberData(nameof(Incarnations))]
	public void RepeatsThatPowerAttackEveryNineSeconds(int npcId, int hazard)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);
		harness.Clock.Advance(FirstPowerAtk);
		BossAiHarness.DrainQueuedSkills(boss);

		harness.Clock.Advance(TimeSpan.FromSeconds(8));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Smash);

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Smash);
	}

	[Fact]
	public void PutsItsAreaAttackHazardOnEveryTargetRatherThanOnTwoOfThem()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Fissurefang);
		var players = new List<Player>
		{
			harness.SpawnPlayer(472f, 512f, 418f),
			harness.SpawnPlayer(473f, 512f, 418f),
			harness.SpawnPlayer(474f, 512f, 418f),
		};
		harness.Engage(boss, players[0]);
		foreach (Player extra in players.Skip(1))
		{
			BossAiHarness.MakeMutuallyKnown(boss, extra);
			BossAiHarness.Rehate(boss, extra);
		}

		// Advance past the power attack at 3s so its single hazard is out of the way, then let the area
		// attack at 15s land.
		harness.Clock.Advance(FirstPowerAtk);
		int afterPowerAtk = Count(harness, CavityOfEarth);
		BossAiHarness.DrainQueuedSkills(boss);

		harness.Clock.Advance(FirstAreaAtk - FirstPowerAtk);

		// One per target on the aggro list, which is the whole point: the invented version dropped two
		// however many people were fighting.
		Assert.Equal(afterPowerAtk + players.Count, Count(harness, CavityOfEarth));
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == IncarnateSurge);
	}

	[Fact]
	public void HoldsItsBindUntilThirtyPercentAndThenRepeatsOnTheLongerTimer()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Fissurefang);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		// The bind timer is armed at 20s and re-checks every 3s while it is not allowed to fire.
		harness.Clock.Advance(TimeSpan.FromSeconds(40));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Bite);

		// Just above the threshold, so the assertion is about 30 specifically rather than about being
		// hurt at all -- at full health any threshold would hold it back.
		BossAiHarness.SetHpPercent(boss, 45);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(9));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Bite);

		BossAiHarness.SetHpPercent(boss, 25);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Bite);

		// Having fired, it goes onto its own 30s cadence rather than the 3s re-check.
		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == Bite);
	}

	[Fact]
	public void WrathclawPutsBothSpheresOutTheMomentHeSpawns()
	{
		using BossAiHarness harness = NewHarness();

		// Before he exists, nothing is placed. He is the only NPC that puts these out, and until now he
		// had no AI at all, so neither sphere was ever spawned by anything.
		Assert.Equal(0, Count(harness, SphereOfWrath));

		SpawnBoss(harness, Wrathclaw);

		Npc wrath = harness.LiveNpcs().Single(n => n.GetNpcId() == SphereOfWrath);
		Npc peace = harness.LiveNpcs().Single(n => n.GetNpcId() == SphereOfPeace);
		Assert.Equal(214f, wrath.GetX(), 1f);
		Assert.Equal(185f, peace.GetX(), 1f);
	}

	[Fact]
	public void WrathclawKeepsExactlyOneOfEachSphereHoweverManyAreaAttacksLand()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Wrathclaw);
		// This pin is about variety, so it opts back into rolling: the harness forces rolled guards to
		// pass by default, which makes counts exact and makes a coin-toss branch look certain. A seed
		// pin hands back the production dice: a fixed seed per npc would make every attempt identical.
		BossAiHarness.RandomRolls(boss);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		// Each area attack clears the pair and puts a fresh pair out. Getting the despawn wrong would
		// leave a growing pile of spheres rather than the two the fight is about.
		for (int i = 0; i < 6; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(25));
			Assert.Equal(1, Count(harness, SphereOfWrath));
			Assert.Equal(1, Count(harness, SphereOfPeace));
		}
	}

	[Fact]
	public void WrathclawSwapsHisSpheresBetweenTheTwoPoints()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Wrathclaw);
		// This pin is about the variety a rolled guard produces, so it hands back the production dice.
		// The harness forces rolled guards to pass by default, which makes counts exact and makes a
		// coin-toss branch look certain. A fixed seed would not help: a fresh npc per attempt with the
		// same seed makes every attempt identical.
		BossAiHarness.RandomRolls(boss);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);

		// Two thirds of his area attacks come back swapped, so over a long fight the sphere of wrath
		// must have stood at both points. A fixed layout would pin it to one.
		var wrathSeenAt = new HashSet<int>();
		for (int i = 0; i < 30; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(25));
			wrathSeenAt.Add((int)MathF.Round(harness.LiveNpcs().Single(n => n.GetNpcId() == SphereOfWrath).GetX()));
		}

		Assert.Equal([185, 214], wrathSeenAt.Order());
	}

	[Theory]
	[MemberData(nameof(Incarnations))]
	public void ClearsItsHazardsOnDeathAndLeavesTheClosingEffectsBehind(int npcId, int hazard)
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, npcId);
		Player player = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, player);
		harness.Clock.Advance(FirstPowerAtk);
		Assert.True(Count(harness, hazard) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		// The hazards go with the fight; the effects that play over the closing crack do not, even
		// though retail files both under the same spawn id.
		Assert.Equal(0, Count(harness, hazard));
		Assert.Equal(1, Count(harness, BurrowingAttack));

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(0, Count(harness, BurrowingAttack));
	}

	/// <summary>
	/// Fissurefang's power-attack earthquake arrives <b>already fighting the tank</b>, with ten million
	/// hate — retail's way of saying it will not peel. Its area-attack twin does not, and neither of
	/// the other two incarnations carries the flag anywhere.
	/// </summary>
	/// <remarks>
	/// The class used to say this was left to the add's own aggressive AI, which is a different thing:
	/// aggression picks up whoever wanders into range after its own delay, where the flag locks the
	/// hazard onto the player it was dropped on the moment it lands. State and target are what is
	/// observed — hate added against a creature unaware of the attacker does not survive our aggro
	/// rules.
	/// </remarks>
	[Fact]
	public void FissurefangsEarthquakeArrivesFightingTheTank()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Fissurefang);
		Player tank = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, tank);

		// The power attack lands at three seconds; the provoke is deferred one tick past that.
		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		Npc quake = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == CavityOfEarth));
		Assert.Equal(AIState.FIGHT, quake.GetAi().GetState());
		Assert.Same(tank, quake.GetTarget());
	}

	/// <summary>Graviwing's whirlpool does not: its pattern writes the flag FALSE throughout.</summary>
	[Fact]
	public void GraviwingsWhirlpoolArrivesInert()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = SpawnBoss(harness, Graviwing);
		Player tank = harness.SpawnPlayer(472f, 512f, 418f);
		harness.Engage(boss, tank);

		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		Npc whirl = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == GravityWhirlpool));
		Assert.NotEqual(AIState.FIGHT, whirl.GetAi().GetState());
	}
}
