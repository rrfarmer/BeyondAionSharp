using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="CaptainXastaAI"/>'s first form, rebuilt from retail pattern
/// IDYun_Nmd3 (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What he used to do — walk a path, summon two Inhibitor Sikars, raise a sanctuary shield — is absent
/// from the pattern, and what the pattern does have he did not do at all: neither the flames nor the
/// artillerymen were ever spawned, and both sat in npc_templates unreferenced. So every assertion here
/// covers behaviour that is new, and the walk it replaced is gone rather than merely unused.
/// <para>
/// His second form (217310) shares this class but runs its own pattern and is untouched, so nothing
/// here drives it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CaptainXastaAiTests
{
	private const int RentusBase = 300280000;
	private const int CaptainXasta = 217309;

	private const int DragonBreath = 19657;
	private const int MagicFlame = 282390;
	private const int SiegeArtilleryman = 282606;

	private static readonly TimeSpan FirstTimer = TimeSpan.FromSeconds(6);
	private static readonly TimeSpan BeatPeriod = TimeSpan.FromSeconds(9);
	private static readonly TimeSpan SummonPeriod = TimeSpan.FromSeconds(6);

	private static BossAiHarness NewHarness() => BossAiHarness.For(RentusBase)
		.WithAi(typeof(CaptainXastaAI), typeof(AggressiveNpcAI))
		.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Drops him to <paramref name="percent"/> and lets exactly one summon tick elapse.</summary>
	private static void SummonTickAt(BossAiHarness harness, Npc boss, Player player, int percent)
	{
		BossAiHarness.SetHpPercent(boss, percent);
		BossAiHarness.Rehate(boss, player);
		harness.Clock.Advance(SummonPeriod);
	}

	[Fact]
	public void BreathesOnHimselfEveryNineSecondsAfterAnOpeningSix()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Engaging arms the timers; nothing is cast until the first one comes due.
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));

		harness.Clock.Advance(FirstTimer);
		BossAiHarness.QueuedCast first = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(DragonBreath, first.SkillId);

		// The pattern casts it at OBJI_SELF, and its level comes from his own npc_skills entry rather
		// than the hardcoded 60 the old sanctuary event used.
		Assert.Equal(NpcSkillTargetAttribute.ME, first.Target);
		Assert.Equal(60, first.Level);

		harness.Clock.Advance(BeatPeriod - TimeSpan.FromSeconds(1));
		Assert.Empty(BossAiHarness.DrainQueuedSkills(boss));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(DragonBreath, Assert.Single(BossAiHarness.DrainQueuedSkills(boss)).SkillId);
	}

	[Fact]
	public void DropsThreeFlamesOnHisTargetPerBeatAndLetsThemBurnOutAfterFifteenSeconds()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		harness.Clock.Advance(FirstTimer);
		Assert.Equal(3, Count(harness, MagicFlame));

		// They land on whoever he is facing, not on himself: the whole point of a self-cast breath.
		foreach (Npc flame in harness.LiveNpcs().Where(n => n.GetNpcId() == MagicFlame))
		{
			float dx = flame.GetX() - player.GetX();
			float dy = flame.GetY() - player.GetY();
			Assert.True(MathF.Sqrt((dx * dx) + (dy * dy)) <= 4f,
				$"flame at ({flame.GetX()}, {flame.GetY()}) is outside the 4m spread around the target");
		}

		// Second beat at 15s, so both batches are up.
		harness.Clock.Advance(BeatPeriod);
		Assert.Equal(6, Count(harness, MagicFlame));

		// The first batch's 15s life expires at 21s, before the third beat at 24s.
		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(3, Count(harness, MagicFlame));
	}

	[Fact]
	public void SendsOneArtillerymanTheFirstTimeHePassesEachOfHisFourSteps()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Above the first step the summon timer ticks and does nothing.
		SummonTickAt(harness, boss, player, 90);
		Assert.Equal(0, Count(harness, SiegeArtilleryman));

		int expected = 0;
		foreach (int step in new[] { 84, 64, 44, 19 })
		{
			SummonTickAt(harness, boss, player, step);
			Assert.Equal(++expected, Count(harness, SiegeArtilleryman));

			// A step already taken must not fire again while HP sits in the same band.
			SummonTickAt(harness, boss, player, step);
			Assert.Equal(expected, Count(harness, SiegeArtilleryman));
		}

		// Four steps and then no more, however long the fight runs on.
		harness.Clock.Advance(TimeSpan.FromMinutes(2));
		Assert.Equal(4, Count(harness, SiegeArtilleryman));
	}

	[Fact]
	public void TakesEachStepOnItsOwnTickWhenHisHealthFallsPastSeveralAtOnce()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Retail's branches are one priority chain guarded by test-and-set flags, so a burst that takes
		// him from full to 10% does not send all four at once — it sends one per timer tick, in order.
		BossAiHarness.SetHpPercent(boss, 10);
		var perTick = new List<int>();
		for (int i = 0; i < 5; i++)
		{
			int before = Count(harness, SiegeArtilleryman);
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(SummonPeriod);
			perTick.Add(Count(harness, SiegeArtilleryman) - before);
		}

		Assert.Equal([1, 1, 1, 1, 0], perTick);
	}

	[Fact]
	public void ClearsFlamesAndArtillerymenWhenHeResets()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		SummonTickAt(harness, boss, player, 84);
		Assert.True(Count(harness, SiegeArtilleryman) > 0 && Count(harness, MagicFlame) > 0,
			"the fight should have produced both kinds of add before the reset");

		boss.GetAi().OnGeneralEvent(AiEventType.BACK_HOME);

		// Both kinds share SPAWN_ID_1 in the pattern, so leaving the fight despawns them together.
		Assert.Equal(0, Count(harness, SiegeArtilleryman));
		Assert.Equal(0, Count(harness, MagicFlame));
	}

	[Fact]
	public void StopsBothTimersWhenHeDies()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(CaptainXasta);
		Player player = harness.SpawnPlayer();

		// The stand-in player arms timers of its own, so this counts against what was already running
		// rather than against zero. Nothing is advanced first, so the two the fight adds are the two
		// this class arms and no flame deletions are outstanding.
		int idle = harness.Clock.ArmedTimerCount;
		harness.Engage(boss, player);
		Assert.Equal(idle + 2, harness.Clock.ArmedTimerCount);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		// Asserting on the timers rather than their effects is the point: both bodies bail on IsDead(),
		// so a leaked repeating task is invisible from outside while still running forever.
		Assert.Equal(idle, harness.Clock.ArmedTimerCount);
	}
}
