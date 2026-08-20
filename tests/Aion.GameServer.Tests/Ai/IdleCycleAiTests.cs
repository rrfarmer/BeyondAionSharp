using System;
using System.Linq;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Guarded, multi-branch idle cycles: the wave controllers that never ran here.
/// </summary>
/// <remarks>
/// 59 retail patterns across 61 npcs, every one on a class that does nothing with a timer.
/// <c>IDForest_Wave_Phase1</c> below is the shape in miniature: retail's alternating-flag idiom, where
/// each rung fires once and hands over to the next, and the last one arms zero to stop.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IdleCycleAiTests
{
	private const int AnyMap = 300520000;

	/// <summary>A forest wave controller: two waves, then it stops.</summary>
	private const int Controller = 282240;

	/// <summary>The add it places, five then three.</summary>
	private const int Add = 282190;

	/// <summary>The one it places alongside the second wave.</summary>
	private const int Leader = 282191;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AnyMap).WithWorldSize(4096)
			.WithAi(typeof(IdleCycleAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>Nothing before the wake-up delay.</b></summary>
	[Fact]
	public void NothingBeforeTheWakeDelay()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Add);
	}

	/// <summary><b>The first rung places five.</b></summary>
	[Fact]
	public void TheFirstRungPlacesFive()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));

		Assert.Equal(5, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == Leader);
	}

	/// <summary>
	/// <b>Five seconds later the second rung places three more and the leader.</b> The flag on the
	/// first rung is spent, so the cycle falls through to the next one.
	/// </summary>
	[Fact]
	public void TheSecondRungFollowsFiveSecondsLater()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.Equal(8, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
		Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == Leader));
	}

	/// <summary>
	/// <b>And there is no third wave.</b> The second rung arms zero, which stops the timer — read as
	/// "next tick" it would place three more adds every tick for the life of the controller.
	/// <para>
	/// Sampled ten seconds after the second wave, not two minutes: the adds carry <c>live_time=60</c>,
	/// so by two minutes they have gone by themselves and the count is zero whether the bug is there or
	/// not. Written that way first, and the suite said so.
	/// </para>
	/// </summary>
	[Fact]
	public void ThereIsNoThirdWave()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Controller, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(3));
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		harness.Clock.Advance(TimeSpan.FromSeconds(10));

		Assert.Equal(8, harness.LiveNpcs().Count(n => n.GetNpcId() == Add));
	}

	/// <summary>
	/// <b>Every cycle in the table has a wake delay and at least one rung.</b> A controller with rungs
	/// and no delay never starts; one with a delay and no rungs wakes to do nothing.
	/// </summary>
	[Fact]
	public void EveryCycleHasBothHalves()
	{
		Assert.Equal(61, IdleCycles.WakeMillis.Count);

		foreach ((int npcId, int delay) in IdleCycles.WakeMillis)
		{
			Assert.True(delay > 0, $"npc {npcId} has no wake delay");
			Assert.NotEmpty(IdleCycles.CycleRungsFor(npcId));
			Assert.NotEmpty(IdleCycles.WakeRungFor(npcId));
		}
	}
}
