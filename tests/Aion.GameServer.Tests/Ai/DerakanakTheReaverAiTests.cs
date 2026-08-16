using System.Reflection;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="DerakanakTheReaverAI"/>, translated from retail pattern
/// <c>IDVritra_Base_Drake_Nmd</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Three chains, one per health regime, each entered by a one-shot branch on the heartbeat. The pins
/// that matter most are the seams: the value where no chain matches, and the fact that phase three
/// stops the heartbeat and so locks phase two out for good.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DerakanakTheReaverAiTests
{
	private const int SauroSupplyBase = 301130000;
	private const int Derakanak = 233258;

	private const int LargeMagicMissile = 16987;
	private const int Flame = 16574;
	private const int FlameSpurt = 16918;
	private const int Fireball = 16919;
	private const int FearCasting = 17888;
	private const int CurseOfBlessing = 16702;
	private const int FearfulPanic = 20782;

	private const int PhaseTwoFlag = 1;
	private const int PhaseThreeFlag = 2;

	/// <summary>
	/// Sets health so the AI actually reads back the percentage asked for.
	/// </summary>
	/// <remarks>
	/// <c>BossAiHarness.SetHpPercent</c> floors on the way in and the percentage getter truncates a
	/// float on the way out, so asking for 80 lands on 79. That is invisible in the middle of a band
	/// and fatal at its edge — this boss's seam is exactly 80, and the test for it was silently
	/// testing 79 and passing for the wrong reason.
	/// <para>
	/// Note that 100 is not reachable for an NPC this size: <c>GetHpPercentage</c> computes
	/// <c>100f * currentHp / maxHp</c>, and above ~167k HP the single-precision product loses enough
	/// to truncate to 99 even at full health. That is not a porting slip — the Java reference has the
	/// identical expression, so it is kept. Band tests here use 90 for "healthy" rather than 100.
	/// </para>
	/// </remarks>
	private static void SetExactPercent(Npc npc, int percent)
	{
		var life = npc.GetLifeStats();
		int max = life.GetMaxHp();
		int hp = (int)Math.Ceiling(max * percent / 100.0);
		while (hp < max && (int)(100f * hp / max) < percent)
			hp++;
		life.SetCurrentHp(hp);
		Assert.Equal(percent, life.GetHpPercentage());
	}

	private static (BossAiHarness, Npc, Player) Engaged(int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(DerakanakTheReaverAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Derakanak, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		SetExactPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static List<BossAiHarness.QueuedCast> Over(BossAiHarness harness, Npc boss, Player player,
		int seconds)
	{
		var cast = new List<BossAiHarness.QueuedCast>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss));
		}
		return cast;
	}

	private static List<int> Ids(List<BossAiHarness.QueuedCast> cast) => cast.Select(c => c.SkillId).ToList();

	private static bool[] Flags(Npc boss) => (bool[])boss.GetAi().GetType().BaseType!
		.GetField("flags", BindingFlags.NonPublic | BindingFlags.Instance)!
		.GetValue(boss.GetAi())!;

	[Fact]
	public void HeOpensWithAMagicOrb()
	{
		var (harness, boss, _) = Engaged(90);
		using BossAiHarness _h = harness;

		Assert.Equal([LargeMagicMissile], BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
	}

	[Fact]
	public void TheHealthyChainRunsOrbFlameSpurtSpurtAndLoops()
	{
		var (harness, boss, player) = Engaged(90);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		// T1 at 10s, T2 at 20, T3 at 30, T4 at 40, and back round to T1 at 50.
		Assert.Equal([LargeMagicMissile, Flame, FlameSpurt, FlameSpurt, LargeMagicMissile],
			Ids(Over(harness, boss, player, 51)));
	}

	[Fact]
	public void PhaseTwoOpensWithTheFearPairThenRunsItsOwnChain()
	{
		var (harness, boss, player) = Engaged(70);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		// Phase two at 5s, then T5 at 20, T6 at 30, T7 at 42, T8 at 52, T9 at 62.
		Assert.Equal(
			[FearCasting, FearfulPanic, CurseOfBlessing, LargeMagicMissile, FlameSpurt, Flame,
				LargeMagicMissile, FlameSpurt],
			Ids(Over(harness, boss, player, 63)));
	}

	[Fact]
	public void PhaseTwoAnnouncesItselfOnlyOnce()
	{
		var (harness, boss, player) = Engaged(70);
		using BossAiHarness _h = harness;

		List<int> cast = Ids(Over(harness, boss, player, 200));

		// Nothing in the 41-80 chain casts fear, so the whole fight should carry exactly one pair.
		Assert.Equal(1, cast.Count(c => c == FearCasting));
		Assert.Equal(1, cast.Count(c => c == FearfulPanic));
	}

	/// <summary>The only branch in the fight that reaches past the top of the hate list.</summary>
	[Fact]
	public void PhaseThreeCursesTheSecondMostHatedAsWellAsTheTarget()
	{
		var (harness, boss, player) = Engaged(35);
		using BossAiHarness _h = harness;

		List<BossAiHarness.QueuedCast> cast = Over(harness, boss, player, 21);
		List<BossAiHarness.QueuedCast> curses = cast.Where(c => c.SkillId == CurseOfBlessing).ToList();

		Assert.Equal(2, curses.Count);
		Assert.Contains(curses, c => c.Target == NpcSkillTargetAttribute.MOST_HATED);
		Assert.Contains(curses, c => c.Target == NpcSkillTargetAttribute.SECOND_MOST_HATED);
	}

	[Fact]
	public void PhaseThreeRunsItsChainThroughToTheTail()
	{
		var (harness, boss, player) = Engaged(35);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		// Phase three at 5s, T10 at 20, T11 at 32, T12 at 42, T13 at 53, T14 at 63.
		Assert.Equal(
			[FearCasting, FearfulPanic, CurseOfBlessing, CurseOfBlessing, FlameSpurt, Fireball,
				Fireball, FlameSpurt, Fireball],
			Ids(Over(harness, boss, player, 64)));
	}

	/// <summary>
	/// The tail flag: the first pass through timer 14 hops back to timer 11 with a fireball, the next
	/// loops the whole way back to timer 10 and re-casts the fear pair.
	/// </summary>
	[Fact]
	public void ThePhaseThreeTailAlternatesBetweenAHopAndAFullLoop()
	{
		var (harness, boss, player) = Engaged(35);
		using BossAiHarness _h = harness;

		List<int> cast = Ids(Over(harness, boss, player, 110));

		// Phase three's own opener, plus exactly one more from the tail's second pass at ~104s.
		Assert.Equal(2, cast.Count(c => c == FearCasting));
	}

	/// <summary>
	/// Phase three does not re-arm the heartbeat, so a boss taken straight past 40 never gets another
	/// timer-0 tick and phase two is locked out for the rest of the fight.
	/// </summary>
	[Fact]
	public void DroppingStraightBelowFortySkipsPhaseTwoForGood()
	{
		var (harness, boss, player) = Engaged(35);
		using BossAiHarness _h = harness;

		Over(harness, boss, player, 120);

		Assert.True(Flags(boss)[PhaseThreeFlag]);
		Assert.False(Flags(boss)[PhaseTwoFlag]);
	}

	/// <summary>
	/// The healthy chain wants 81 or better and phase two wants strictly below 80, so at exactly 80 no
	/// step matches at all. Only the heartbeat keeps him ticking until he loses another point — the
	/// same seam the Ophidan Bridge fire bosses have at 40.
	/// </summary>
	[Fact]
	public void AtExactlyEightyNoChainMatchesUntilHeDropsAPoint()
	{
		var (harness, boss, player) = Engaged(80);
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(boss);

		Assert.Empty(Over(harness, boss, player, 40));

		SetExactPercent(boss, 79);

		Assert.Equal([FearCasting, FearfulPanic], Ids(Over(harness, boss, player, 6)));
	}
}
