using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Celestius' helpers, which used to join his patrol three at a time and never leave it.
/// </summary>
/// <remarks>
/// Retail <c>Elim_ComadAe</c> gives all three summons <c>live_time</c> 30, and this class calls them
/// <b>every twenty-five seconds for the whole fight</b>. With no lifetime that is three more walkers on
/// the path per cycle without bound — <b>thirty-six of them five minutes in</b>, each one pathing and
/// broadcasting. The lifetime caps it at two overlapping sets.
/// <para>
/// <b>This file was written, run, and deleted once</b> before the harness could carry it: the fixture
/// left <c>WALKER_DATA</c> null, so the moment this class started a helper walking it threw. The pins
/// below are the same ones, restored now that the holder exists.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CelestiusAiTests
{
	private const int TalocsHollow = 300190000;
	private const int Celestius = 215488;
	private const int Helper = 281514;

	private const int PerCycle = 3;

	private static (BossAiHarness, Npc) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(TalocsHollow).WithWorldSize(2048)
			.WithAi(typeof(CelestiusAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc boss = harness.Spawn(Celestius, 540f, 820f, 1377f);
		Player player = harness.SpawnPlayer(542f, 822f, 1377f);
		harness.Engage(boss, player);

		// His helper call starts on the first blow from a player, not on entering combat.
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		return (harness, boss);
	}

	private static int Helpers(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Helper);

	/// <summary>Three helpers on the first call.</summary>
	[Fact]
	public void ThreeHelpersAnswerTheFirstCall()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal(PerCycle, Helpers(harness));
	}

	/// <summary>
	/// <b>And they do not accumulate over a long fight.</b> Eight cycles in, the unbounded version was
	/// standing at twenty-four; retail's ceiling is two overlapping sets.
	/// </summary>
	/// <remarks>
	/// Pinned as a ceiling rather than an exact count: the thirty-second life overruns the twenty-five
	/// second cycle by five, so how many stand at any instant depends where in that overlap the clock is
	/// read. <b>The bug was unbounded growth, and a ceiling is what distinguishes it.</b>
	/// </remarks>
	[Fact]
	public void TheHelpersDoNotAccumulate()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(200));

		Assert.True(Helpers(harness) <= PerCycle * 2,
			$"helpers piled up: {Helpers(harness)} standing after eight calls");
	}

	/// <summary>
	/// <b>And the first three are gone.</b> Stated separately so the ceiling above cannot be met by a
	/// class that simply stopped calling — the originals have to actually leave.
	/// </summary>
	[Fact]
	public void TheFirstThreeLeaveAtThirtySeconds()
	{
		var (harness, _) = Engaged();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		var first = harness.LiveNpcs().Where(n => n.GetNpcId() == Helper).ToHashSet();
		Assert.Equal(PerCycle, first.Count);

		harness.Clock.Advance(TimeSpan.FromSeconds(31));

		Assert.DoesNotContain(harness.LiveNpcs(), n => first.Contains(n));
	}
}
