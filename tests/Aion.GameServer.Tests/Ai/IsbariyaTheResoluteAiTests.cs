using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Isbariya the Resolute, whose whole phase ladder was off by a few points.
/// </summary>
/// <remarks>
/// Retail's bands are 70, 49 and 29; this class used 75, 50 and 25. The mapping is not in doubt — each
/// band sends its own system message and this class already sent the matching one on each rung — so the
/// thresholds were simply wrong, and the wave counts and re-arm intervals with them.
/// <para>
/// Found by <c>audit_timer_drift.py</c>, which reported 0/24000 against a pattern containing neither.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IsbariyaTheResoluteAiTests
{
	private const int BeshmundirTemple = 300150000;

	private const int Isbariya = 216263;

	/// <summary>The two servants, and the sacrificial souls.</summary>
	private const int Shield = 281659;
	private const int Taros = 281660;
	private const int Soul = 281645;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(BeshmundirTemple).WithWorldSize(2048)
			.WithAi(typeof(IsbariyaTheResoluteAI), typeof(IsbariyaServantsAI), typeof(SacrificialSoulAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static (Npc Boss, Player Player) Engaged(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(Isbariya, 1585f, 1575f, 305f);
		Player player = harness.SpawnPlayer(1589f, 1575f, 305f);
		harness.Engage(boss, player);
		return (boss, player);
	}

	private static void StepTo(BossAiHarness harness, Npc boss, Player player, params int[] rungs)
	{
		foreach (int percent in rungs)
		{
			BossAiHarness.SetHpPercent(boss, percent);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		}
	}

	/// <summary>
	/// <b>The first band opens at seventy per cent, not seventy-five.</b>
	/// </summary>
	[Fact]
	public void TheFirstBandOpensAtSeventy()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		StepTo(harness, boss, player, 73);
		Assert.Equal(0, Count(harness, Soul));

		StepTo(harness, boss, player, 70);
		Assert.True(Count(harness, Soul) > 0, "no souls at seventy per cent");
	}

	/// <summary>
	/// <b>Three Taros on the middle band, not five.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>total_set_to_spawn</c> is 3. Two extra adds per turn on a band that repeats every
	/// eighteen seconds is a different fight.
	/// </remarks>
	[Fact]
	public void TheMiddleBandSendsThreeTaros()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		StepTo(harness, boss, player, 70, 49);

		// The band changes what the next turn does; it does not fire one itself. The first turn ran on
		// entering the top band, so the next is twenty seconds later.
		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.Equal(3, Count(harness, Taros));
	}

	/// <summary>
	/// <b>And two shields on the deepest, not one.</b>
	/// </summary>
	[Fact]
	public void TheDeepestBandSendsTwoShields()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);

		StepTo(harness, boss, player, 70, 49, 29);
		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		Assert.Equal(2, Count(harness, Shield));
	}

	/// <summary>
	/// <b>A shield lasts seven seconds and a Taros twenty.</b>
	/// </summary>
	/// <remarks>
	/// The two were swapped and neither was right: the shield had twenty and everything else ten, so the
	/// short-lived one outlasted the long-lived one by a factor of three.
	/// </remarks>
	[Fact]
	public void TheServantsKeepTheirOwnLifetimes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Shield, 1585f, 1575f, 305f);
		harness.Spawn(Taros, 1586f, 1575f, 305f);

		harness.Clock.Advance(TimeSpan.FromSeconds(8));
		Assert.Equal(0, Count(harness, Shield));
		Assert.Equal(1, Count(harness, Taros));

		harness.Clock.Advance(TimeSpan.FromSeconds(13));
		Assert.Equal(0, Count(harness, Taros));
	}

	/// <summary>
	/// <b>The deepest band is the fastest, not the slowest.</b>
	/// </summary>
	/// <remarks>
	/// Retail re-arms it at eight seconds against the middle band's eighteen; this class had twenty
	/// there, so the phase retail makes frantic was its most sedate. Counted by arrivals over a window,
	/// with the health held down because a boss left alone regenerates.
	/// </remarks>
	[Fact]
	public void TheDeepestBandComesFastest()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);
		StepTo(harness, boss, player, 70, 49, 29);

		// Past the first turn, so the eight-second rung is the one re-arming.
		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		// Forty seconds of eight-second turns is five, two shields each; at twenty it would be two turns.
		BossAiHarness.Watched seen = harness.WatchNew(
			40, () => BossAiHarness.SetHpPercent(boss, 25), Shield);

		Assert.True(seen.Total >= 8, $"only {seen.Total} shields in forty seconds");
	}

	/// <summary>
	/// <b>And the middle band comes every eighteen seconds, not every ten.</b>
	/// </summary>
	/// <remarks>
	/// The three counts and the deepest rung were pinned before this and the middle interval was not, so
	/// speeding it up to ten seconds survived the mutation sweep. Counted as an upper bound, because the
	/// error to catch here makes the wave arrive <i>more</i> often.
	/// </remarks>
	[Fact]
	public void TheMiddleBandComesEveryEighteenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		(Npc boss, Player player) = Engaged(harness);
		StepTo(harness, boss, player, 70, 49);
		harness.Clock.Advance(TimeSpan.FromSeconds(21));

		// Forty seconds of eighteen-second turns is two, three Taros each; at ten it would be four turns.
		BossAiHarness.Watched seen = harness.WatchNew(
			40, () => BossAiHarness.SetHpPercent(boss, 45), Taros);

		Assert.InRange(seen.Total, 1, 9);
	}
}
