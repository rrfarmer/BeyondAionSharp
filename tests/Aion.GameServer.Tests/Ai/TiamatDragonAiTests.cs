using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Tiamat's dragon form and the four drakan mages he calls to the corners of the platform.
/// </summary>
/// <remarks>
/// Retail <c>IDTiamat_Tiamat_Dragon_Named_60_Al</c> arms a fifteen-second timer when he engages, whose
/// branch arms a four-second one, whose branch places one mage at each corner. <b>This class made no
/// spawns at all</b>, so all four sat in <c>npc_templates</c> summoned by nothing.
/// <para>
/// The larger half of that pattern — roughly twenty drakan sent along <c>path_tiamatdrakan_*</c> walker
/// paths — is <b>not built</b>, because this port has no waypoint support in either AI layer.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatDragonAiTests
{
	private const int DragonLordsRefuge = 300520000;
	private const int Dragon = 219361;

	private static readonly int[] Mages = [283163, 283164, 283165, 283166];

	private const int ShapeChangeFlash = 283174;
	private const int InfernoSpirit = 283067;
	private const int BurrowingArrival = 283062;
	private const int ThickDust = 283134;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatDragonAI), typeof(ThickDustAI), typeof(AggressiveNpcAI),
				typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI))
			.Build();
		Npc boss = harness.Spawn(Dragon, 504f, 514f, 417.5f);
		Player player = harness.SpawnPlayer(506f, 516f, 417.5f);
		harness.Engage(boss, player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		return (harness, boss, player);
	}

	private static int MagesUp(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => Mages.Contains(n.GetNpcId()));

	/// <summary><b>Four mages, nineteen seconds after he engages.</b> One of each, not four of one.</summary>
	[Fact]
	public void FourMagesArriveNineteenSecondsIn()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(18));
		Assert.Equal(0, MagesUp(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.Equal(4, MagesUp(harness));
		Assert.Equal(4, harness.LiveNpcs()
			.Where(n => Mages.Contains(n.GetNpcId()))
			.Select(n => n.GetNpcId())
			.Distinct()
			.Count());
	}

	/// <summary>
	/// <b>And they come once, not on every blow.</b> Retail guards the step with a test-and-set, and this
	/// class hangs it off <c>HandleAttack</c> — which fires on every hit, so without the latch a raid
	/// would bury the platform in mages.
	/// </summary>
	[Fact]
	public void TheMagesAreCalledOncePerFight()
	{
		var (harness, boss, player) = Engaged();
		using BossAiHarness _h = harness;

		for (int i = 0; i < 5; i++)
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

		harness.Clock.Advance(TimeSpan.FromSeconds(20));

		Assert.Equal(4, MagesUp(harness));
	}

	/// <summary>
	/// <b>And a dragon killed before they arrive calls nobody.</b> The scheduled step outlives the boss,
	/// so it has to check.
	/// </summary>
	[Fact]
	public void ADeadDragonCallsNoMages()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		boss.GetAi().OnGeneralEvent(AiEventType.Died);
		harness.Clock.Advance(TimeSpan.FromSeconds(20));

		Assert.Equal(0, MagesUp(harness));
	}
	/// <summary>
	/// <b>He arrives with retail's three effects.</b> The flash on its fixed mark, the inferno elemental
	/// and the burrowing arrival at his own feet — all of which the hard variant already placed and this
	/// one did not, so the normal form arrived in silence.
	/// </summary>
	[Fact]
	public void HeArrivesWithFlashSpiritAndBurrowing()
	{
		using BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatDragonAI), typeof(ThickDustAI), typeof(AggressiveNpcAI),
				typeof(AggressiveNoLootNpcAI), typeof(GeneralNpcAI))
			.Build();
		harness.Spawn(Dragon, 504f, 514f, 417.5f);

		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == ShapeChangeFlash);
		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == InfernoSpirit);
		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == BurrowingArrival);
	}

	/// <summary>
	/// <b>And the effects expire on retail's own seconds</b> — six, eight and ten — rather than standing
	/// in the room. The flash outlasts the other two, which is what orders this pin.
	/// </summary>
	[Fact]
	public void TheArrivalEffectsExpireOnTheirOwnClocks()
	{
		var (harness, _, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(7));
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == InfernoSpirit);
		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == ShapeChangeFlash);

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == ShapeChangeFlash);
	}

	/// <summary><b>And he leaves a dust cloud.</b> Retail's <c>on_die</c>, six seconds at his feet.</summary>
	[Fact]
	public void HeLeavesDustBehind()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == ThickDust);
	}

	/// <summary>
	/// <b>And the dust clears at six seconds, not ten.</b> Retail writes six; Java left it at ten.
	/// </summary>
	/// <remarks>
	/// <b>The clock lives in <c>ThickDustAI</c>, not here.</b> An earlier pass gave this spawn call a
	/// six-second lifetime, which took effect only because it was shorter than the add's own ten and so
	/// won the race — and no pin noticed, because the only dust pin asserted that dust appeared. Five
	/// seconds against seven is the window that separates the two numbers.
	/// </remarks>
	[Fact]
	public void TheDustClearsAtSixSeconds()
	{
		var (harness, boss, _) = Engaged();
		using BossAiHarness _h = harness;

		boss.GetAi().OnGeneralEvent(AiEventType.Died);
		var dust = harness.LiveNpcs().Where(n => n.GetNpcId() == ThickDust).ToHashSet();
		Assert.NotEmpty(dust);

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.All(dust, d => Assert.Contains(d, harness.LiveNpcs()));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.DoesNotContain(harness.LiveNpcs(), n => dust.Contains(n));
	}
}
