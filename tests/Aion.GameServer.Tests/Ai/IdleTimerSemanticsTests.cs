using System;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// What <c>set_idle_timer delay=0</c> means, which this port had guessed and had never exercised.
/// </summary>
/// <remarks>
/// Retail uses <c>set_idle_timer</c> 6,093 times; <b>1,090 carry zero and 1,006 of those sit inside
/// <c>on_idle_timer</c></b>, re-arming the timer that just fired. <c>PatternAi</c> documented zero as
/// "next tick" — a guess, never tested, because all six classes using the timer passed a real delay.
/// <para>
/// It is <b>stop</b>. Retail has no cancel action at all — only <c>add_battle_timer</c> and this — so
/// zero is the only way a pattern can end a cycle. <c>Ab1_N_ControlNoShowNPC_08</c> settles it: two
/// flag-guarded rungs each fire once and re-arm at 120 seconds, then an <b>unguarded</b> fallback
/// prints the last message of a three-stage spawn alarm and arms zero. Read as "next tick" that message
/// repeats every tick forever, and 457 of the 1,006 are unguarded like it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IdleTimerSemanticsTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>A middle beacon, which arms a real 2000ms delay and lays eleven hits.</summary>
	private const int Beacon = 283156;

	/// <summary><b>A zero delay stops the timer instead of arming it.</b></summary>
	[Fact]
	public void ZeroDisarmsTheIdleTimer()
	{
		using BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc beacon = harness.Spawn(Beacon, 460f, 514f, 417f);

		// Its wake-up rung armed 2000ms. Zero replaces that with nothing at all.
		((PatternAi)beacon.GetAi()).SetIdleTimer(0);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));

		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == 283068);
	}

	/// <summary>
	/// <b>And a real delay still arms.</b> The same npc, left alone, lays its breath.
	/// </summary>
	/// <remarks>
	/// Two seconds, not the five the disarm test uses: the hits carry <c>live_time=2</c>, so by five
	/// seconds they have laid and gone again. Written as five first, and the failure was this test
	/// rather than the change.
	/// </remarks>
	[Fact]
	public void ARealDelayStillArms()
	{
		using BossAiHarness harness = BossAiHarness.For(DragonLordsRefuge).WithWorldSize(4096)
			.WithAi(typeof(TiamatBeaconAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();
		Npc beacon = harness.Spawn(Beacon, 460f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(2));

		// The question is whether the timer armed, and the beacon's own spawn count answers it. The
		// breath npcs are hazards that cast and remove themselves, so counting them finds nothing.
		Assert.Equal(11, ((PatternAi)beacon.GetAi()).SpawnCount);
	}
}
