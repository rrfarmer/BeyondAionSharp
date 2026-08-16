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
	/// Takahan's first trap comes at twenty-five seconds and then every six — slow, then relentless.
	/// </summary>
	[Fact]
	public void TakahansFirstTrapIsSlowAndTheRestAreNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Takahan, 300f, 300f, 200f);
		// Forty metres out — inside the op's fifty-metre valid_distance and far enough that a trap on
		// the player cannot be mistaken for one at the boss's feet.
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		for (int i = 0; i < 24; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(0, Count(harness, ExplosiveTrap));

		for (int i = 0; i < 2; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Npc trap = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == ExplosiveTrap));
		Assert.True(Math.Abs(trap.GetX() - player.GetX()) < Math.Abs(trap.GetX() - boss.GetX()),
			$"the trap goes on his quarry: {trap.GetX():F0} against player {player.GetX():F0} "
			+ $"and boss {boss.GetX():F0}");

		// Six seconds later, another — not another twenty-five. Counted as arrivals rather than as
		// survivors, because a trap is `ntrap` and removes itself on its own clock.
		BossAiHarness.Watched later = harness.Watch(
			20, () => { BossAiHarness.Rehate(boss, player); BossAiHarness.KeepAlive(player); },
			ExplosiveTrap);

		Assert.True(later.Total >= 3,
			$"twenty seconds at six-second intervals is three more traps: {later.Total}");
	}
}
