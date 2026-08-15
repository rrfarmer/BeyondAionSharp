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
}
