using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Ashunatal Shadowslip's shadows, translated from retail patterns <c>Station_NinjaNM</c>
/// and <c>Station_Shadow1</c>, <c>_2</c>, <c>_3_1</c> and <c>_3_2</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class AshunatalShadowslipAiTests
{
	private const int AturamSkyFortress = 300240000;

	private const int Ashunatal = 217376;
	private const int ExplosionShadow = 217379;
	private const int DecayShadow = 217380;
	private const int DisruptionShadow = 217381;
	private const int DisruptionSpawn = 217387;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AturamSkyFortress).WithWorldSize(2048)
			.WithAi(typeof(AshunatalShadowslipAI), typeof(ExplosionShadowAI), typeof(DecayShadowAI),
				typeof(DisruptionShadowAI), typeof(DisruptionShadowSpawnAI),
				typeof(SummonerAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static List<Npc> Live(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Where(n => n.GetNpcId() == npcId).ToList();

	private static void Strike(Npc target, Creature attacker) =>
		target.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);

	/// <summary>
	/// <b>An explosion shadow is a bomb on a twelve-second fuse.</b> It arms on entering combat and
	/// never re-arms, so the fuse runs once and the shadow is gone.
	/// </summary>
	[Fact]
	public void AnExplosionShadowIsABombOnATwelveSecondFuse()
	{
		using BossAiHarness harness = NewHarness();
		Npc shadow = harness.Spawn(ExplosionShadow, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(shadow, raider);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(11000));
		Assert.Single(Live(harness, ExplosionShadow));

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));

		Assert.Empty(Live(harness, ExplosionShadow));
	}

	/// <summary>
	/// <b>A decay shadow is not a bomb.</b> The three shadows are three different things and this one
	/// has no self-despawn anywhere in its pattern — it stays until something kills it or clears it.
	/// </summary>
	[Fact]
	public void ADecayShadowIsNotABomb()
	{
		using BossAiHarness harness = NewHarness();
		Npc shadow = harness.Spawn(DecayShadow, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(shadow, raider);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(60000));

		Assert.Single(Live(harness, DecayShadow));
	}

	/// <summary>
	/// <b>A disruption shadow splits, once.</b> Fifteen seconds after engaging it puts one more of a
	/// different npc on the floor and then stops, because it never re-arms its timer.
	/// </summary>
	[Fact]
	public void ADisruptionShadowSplitsOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc shadow = harness.Spawn(DisruptionShadow, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(shadow, raider);

		harness.Clock.Advance(TimeSpan.FromMilliseconds(14000));
		Assert.Empty(Live(harness, DisruptionSpawn));

		harness.Clock.Advance(TimeSpan.FromMilliseconds(2000));
		int afterFirst = Live(harness, DisruptionSpawn).Count;

		harness.Clock.Advance(TimeSpan.FromMilliseconds(60000));

		Assert.InRange(afterFirst, 1, 2);
		Assert.Equal(afterFirst, Live(harness, DisruptionSpawn).Count);
		Assert.Single(Live(harness, DisruptionShadow));
	}

	/// <summary>
	/// <b>Sometimes two.</b> Retail rolls thirty percent for the branch that puts out a pair, so over
	/// forty splits both outcomes have to appear — a class that always sent one, or always two, would
	/// pass every other pin here.
	/// </summary>
	[Fact]
	public void SometimesTwo()
	{
		bool sawOne = false;
		bool sawTwo = false;

		for (int i = 0; i < 40 && !(sawOne && sawTwo); i++)
		{
			using BossAiHarness harness = NewHarness();
			Npc shadow = harness.Spawn(DisruptionShadow, 300f, 300f, 200f);
			Player raider = harness.SpawnPlayer(302f, 300f, 200f);
			harness.Engage(shadow, raider);
			harness.Clock.Advance(TimeSpan.FromMilliseconds(16000));

			int born = Live(harness, DisruptionSpawn).Count;
			sawOne |= born == 1;
			sawTwo |= born == 2;
		}

		Assert.True(sawOne, "never split into one");
		Assert.True(sawTwo, "never split into two");
	}

	/// <summary>
	/// <b>The message number is retail's, not ours.</b> Boss and all four shadows share one constant,
	/// so nothing else here would notice it changing — and <c>7063</c> is read out of the pattern dump
	/// rather than chosen.
	/// </summary>
	[Fact]
	public void TheMessageNumberIsRetails()
	{
		Assert.Equal(7063, AshunatalShadowslipAI.ClearTheBoard);
	}

	/// <summary>
	/// <b>At forty percent Ashunatal clears the board</b>, and the call reaches all four shadow types
	/// — including the children a disruption shadow made, which are not in his spawn group and which
	/// a group despawn could not have touched.
	/// </summary>
	[Fact]
	public void AtFortyPercentHeClearsTheBoard()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ashunatal, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(boss, raider);

		Npc explosion = harness.Spawn(ExplosionShadow, 305f, 300f, 200f);
		Npc decay = harness.Spawn(DecayShadow, 306f, 300f, 200f);
		Npc disruption = harness.Spawn(DisruptionShadow, 307f, 300f, 200f);
		Npc child = harness.Spawn(DisruptionSpawn, 308f, 300f, 200f);
		foreach (Npc shadow in new[] { explosion, decay, disruption, child })
			BossAiHarness.MakeMutuallyKnown(boss, shadow);

		BossAiHarness.SetExactPercent(boss, 39);
		Strike(boss, raider);

		Assert.Empty(Live(harness, ExplosionShadow));
		Assert.Empty(Live(harness, DecayShadow));
		Assert.Empty(Live(harness, DisruptionShadow));
		Assert.Empty(Live(harness, DisruptionSpawn));
	}

	/// <summary>
	/// <b>And only at forty.</b> Fifty percent is a summon step in retail and this class must not
	/// clear anything there.
	/// </summary>
	[Fact]
	public void AndOnlyAtForty()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ashunatal, 300f, 300f, 200f);
		Player raider = harness.SpawnPlayer(302f, 300f, 200f);
		harness.Engage(boss, raider);

		Npc decay = harness.Spawn(DecayShadow, 306f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, decay);

		BossAiHarness.SetExactPercent(boss, 49);
		Strike(boss, raider);

		Assert.Single(Live(harness, DecayShadow));
	}
}
