using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DeathDropBossAI"/> and <see cref="TakahanAI"/>, translated from retail
/// patterns <c>FD2_FrA</c>, <c>NLehpar_BhA</c>, <c>BLehpar_FhA</c> and <c>Dread02_SurkanaNm06</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Four bosses whose only index-free line is a spawn. Three leave something behind when a player
/// kills them; the fourth drops traps on a timer.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DeathDropBossesAiTests
{
	private const int Theobomos = 220050000;

	private const int Menotios = 251001;
	private const int TitanCore = 290116;

	private const int Rm78c = 212211;
	private const int StrangeCreature = 280790;

	private const int Ra45c = 213764;
	private const int StrangeObject = 280714;

	private const int Takahan = 216884;
	private const int ExplosiveTrap = 281619;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Theobomos).WithWorldSize(2048)
			.WithAi(typeof(DeathDropBossAI), typeof(TakahanAI), typeof(AggressiveNpcAI),
				typeof(NTrapAI), typeof(StrangeCreatureAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Each of the three leaves its own thing, and the table says which.</summary>
	[Theory]
	[InlineData(Menotios, TitanCore, 20)]
	[InlineData(Rm78c, StrangeCreature, 120)]
	[InlineData(Ra45c, StrangeObject, 120)]
	public void EachBossLeavesItsOwnThing(int boss, int drop, int life)
	{
		Assert.Equal(drop, DeathDropBossAI.DropFor(boss));
		Assert.Equal(life, DeathDropBossAI.DropLifeFor(boss));
	}

	/// <summary>A boss not in the table leaves nothing rather than somebody else's.</summary>
	[Fact]
	public void AnUnlistedBossLeavesNothing()
	{
		Assert.Equal(0, DeathDropBossAI.DropFor(123456));
	}

	/// <summary>Menotios leaves a titan core where he fell, and only when he dies.</summary>
	[Fact]
	public void MenotiosLeavesATitanCoreWhereHeFell()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Menotios, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(30));
		Assert.Equal(0, Count(harness, TitanCore));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Npc core = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == TitanCore));
		Assert.Equal(300f, core.GetX(), 1);
	}

	/// <summary>
	/// The lifetimes a boss gives its drop are pinned at the table rather than by survival, and that
	/// is not a shortcut — <b>every one of the three adds ends itself sooner than its boss allows</b>.
	/// </summary>
	/// <remarks>
	/// The titan core and Takahan's trap are <c>ntrap</c>, whose pattern is "cast once, then
	/// <c>despawn_self</c>", so the twenty seconds Menotios supplies is a ceiling the trap never
	/// reaches. The strange creature deletes itself after six and a half seconds against retail's two
	/// minutes, which is a Java-parity clock in <c>StrangeCreatureAI</c> and a genuine open question —
	/// recorded in docs/retail-ai-fidelity.md and belonging to that class rather than to these bosses.
	/// </remarks>
	[Fact]
	public void TheDropsEndThemselvesBeforeTheirBossesLifetimes()
	{
		using BossAiHarness harness = NewHarness();
		Npc menotios = harness.Spawn(Menotios, 300f, 300f, 200f);

		menotios.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, TitanCore));

		// Well inside the twenty seconds the boss asks for.
		harness.Clock.Advance(TimeSpan.FromSeconds(18));
		Assert.Equal(0, Count(harness, TitanCore));
	}

	/// <summary>And the other two leave theirs, whatever their own classes then do with it.</summary>
	[Fact]
	public void TheOtherTwoLeaveTheirsAsWell()
	{
		using BossAiHarness harness = NewHarness();
		Npc rm78c = harness.Spawn(Rm78c, 300f, 300f, 200f);
		Npc ra45c = harness.Spawn(Ra45c, 400f, 300f, 200f);

		rm78c.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, StrangeCreature));

		ra45c.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, StrangeObject));
	}

	/// <summary>
	/// Takahan's fight, engaged at a given health, driven for <paramref name="seconds"/>.
	/// </summary>
	/// <remarks>
	/// The player stands forty metres out: far enough that a trap laid on him cannot be mistaken for
	/// one at the boss's feet, and inside retail's fifty-metre <c>valid_distance</c>.
	/// </remarks>
	private static (BossAiHarness, Npc, Player) TakahanAt(int percent)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Takahan, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, percent);
		return (harness, boss, player);
	}

	private static void Drive(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary>
	/// The trap lands on his quarry at twenty-five seconds, when he is inside the band. Counted as
	/// arrivals rather than survivors: a trap is <c>ntrap</c> and removes itself on its own clock.
	/// </summary>
	[Fact]
	public void TakahanLaysHisTrapOnHisQuarryAtTwentyFiveSeconds()
	{
		var (harness, boss, player) = TakahanAt(50);
		using BossAiHarness _h = harness;

		Drive(harness, boss, player, 24);
		Assert.Equal(0, Count(harness, ExplosiveTrap));

		Drive(harness, boss, player, 2);

		Npc trap = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == ExplosiveTrap));
		Assert.True(Math.Abs(trap.GetX() - player.GetX()) < Math.Abs(trap.GetX() - boss.GetX()),
			$"the trap goes on his quarry: {trap.GetX():F0} against player {player.GetX():F0} "
			+ $"and boss {boss.GetX():F0}");
	}

	/// <summary>
	/// <b>One trap, not a loop.</b> The branch carries a test-and-set flag var, so however long the
	/// fight stays in the band nothing more is laid — the earlier translation of this class read it as
	/// a six-second loop and produced a trap every six seconds for the rest of the fight.
	/// </summary>
	[Fact]
	public void HeLaysExactlyOneTrapHoweverLongTheBandLasts()
	{
		var (harness, boss, player) = TakahanAt(50);
		using BossAiHarness _h = harness;

		// Counted across the whole fight rather than after the first trap: `Watch` counts by object
		// id, and a trap already standing when the window opens is one it has seen. Two minutes is
		// twenty turns of the six-second re-arm the old reading looped on.
		BossAiHarness.Watched whole = harness.Watch(
			120, () => { BossAiHarness.Rehate(boss, player); BossAiHarness.KeepAlive(player); },
			ExplosiveTrap);

		Assert.Equal(1, whole.Total);
	}

	/// <summary>
	/// <b>Above the band he lays nothing.</b> A fight that never drops him under seventy never sees a
	/// trap at all, which is the half of the guard a threshold reading would still have missed.
	/// </summary>
	[Fact]
	public void AboveSeventyHeLaysNoTrap()
	{
		var (harness, boss, player) = TakahanAt(85);
		using BossAiHarness _h = harness;

		Drive(harness, boss, player, 120);
		Assert.Equal(0, Count(harness, ExplosiveTrap));
	}

	/// <summary>
	/// <b>And below it he lays nothing either.</b> Taken straight past the band, the trap is skipped
	/// for good — timer 2 hands off to timer 3 and the flag var is never spent.
	/// </summary>
	[Fact]
	public void BelowThirtyFiveTheTrapIsSkippedForGood()
	{
		var (harness, boss, player) = TakahanAt(20);
		using BossAiHarness _h = harness;

		Drive(harness, boss, player, 150);
		Assert.Equal(0, Count(harness, ExplosiveTrap));
	}

	/// <summary>
	/// <b>Below thirty-five the chain leaves its own timer and comes back slowly.</b> Timer 2 hands off
	/// to timer 3 at nine seconds and timer 3 returns it at eighteen — so a boss dragged under the band
	/// and pulled back into it waits about twenty-seven seconds for his chance, not the six the
	/// fallback would give. Pinned by walking the fight through the hand-off, because the delay is the
	/// only thing that rung changes for us.
	/// </summary>
	[Fact]
	public void TheHandOffBelowThirtyFiveCostsHimNearlyHalfAMinute()
	{
		var (harness, boss, player) = TakahanAt(20);
		using BossAiHarness _h = harness;

		// t=25 timer 2 fires under the band and hands to timer 3.
		Drive(harness, boss, player, 26);
		BossAiHarness.SetExactPercent(boss, 50);

		// t=34 timer 3 returns it at eighteen, so timer 2 is not back until about t=52.
		Drive(harness, boss, player, 19);
		Assert.Equal(0, Count(harness, ExplosiveTrap));

		Drive(harness, boss, player, 10);
		Assert.Equal(1, Count(harness, ExplosiveTrap));
	}

	/// <summary>
	/// Entering the band late still gets him his trap: the chain keeps polling above seventy on its
	/// seventeen-second re-arm, so the trap follows the raid's damage rather than the clock alone.
	/// </summary>
	[Fact]
	public void EnteringTheBandLateStillLaysIt()
	{
		var (harness, boss, player) = TakahanAt(85);
		using BossAiHarness _h = harness;

		Drive(harness, boss, player, 30);
		Assert.Equal(0, Count(harness, ExplosiveTrap));

		BossAiHarness.SetExactPercent(boss, 50);
		BossAiHarness.Watched after = harness.Watch(
			25, () => { BossAiHarness.Rehate(boss, player); BossAiHarness.KeepAlive(player); },
			ExplosiveTrap);

		Assert.Equal(1, after.Total);
	}
}
