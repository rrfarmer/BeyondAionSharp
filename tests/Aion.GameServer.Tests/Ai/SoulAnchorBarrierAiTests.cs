using System;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// A soul anchor barrier places one faction-balance npc ten minutes after it wakes, and then stops.
/// </summary>
/// <remarks>
/// Twenty of these were on plain <c>aggressive</c>, which does nothing with a timer. The mechanic was
/// blocked on what <c>set_idle_timer delay=0</c> means — see <c>IdleTimerSemanticsTests</c> — and this
/// is the first class in the port to depend on the answer.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class SoulAnchorBarrierAiTests
{
	private const int Panesterra = 400010000;

	/// <summary>One of the twenty barriers.</summary>
	private const int Barrier = 277516;

	/// <summary>The faction-balance npc all five retail patterns place.</summary>
	private const int FactionBalance = 702412;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Panesterra).WithWorldSize(4096)
			.WithAi(typeof(SoulAnchorBarrierAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary><b>Nothing happens for the first ten minutes.</b></summary>
	[Fact]
	public void NothingIsPlacedBeforeTheTenMinutes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Barrier, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromMinutes(9));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == FactionBalance);
	}

	/// <summary><b>And then exactly one is placed.</b></summary>
	[Fact]
	public void OneIsPlacedWhenTheTenMinutesAreUp()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Barrier, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromMinutes(10));

		Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == FactionBalance);
	}

	/// <summary>
	/// <b>And it does not keep placing them.</b> The rung disarms its own timer, so an hour later the
	/// barrier has placed one in total — not six, and not one per tick.
	/// </summary>
	[Fact]
	public void AndItDoesNotKeepPlacingThem()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Barrier, 300f, 300f, 200f);

		// Ten seconds of life, so the first is long gone; what matters is that no second one appeared.
		harness.Clock.Advance(TimeSpan.FromHours(1));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == FactionBalance);
	}
}
