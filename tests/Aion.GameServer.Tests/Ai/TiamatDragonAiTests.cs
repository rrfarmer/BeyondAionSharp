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

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(TiamatDragonAI), typeof(AggressiveNpcAI), typeof(AggressiveNoLootNpcAI),
				typeof(GeneralNpcAI))
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
}
