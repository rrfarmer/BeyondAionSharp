using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Tiamat's breath beacons, which burned twice as often for half again as long.
/// </summary>
/// <remarks>
/// Retail's beacons are controllers: <c>on_wake_up</c> sets an idle timer of 2000 and each firing lays
/// a row of two-second damage npcs. <c>IDTiamat_Tiamat_Dragon_Dying_Named_60_Al</c> spawns every
/// variant with <c>live_time=7</c>. This port opened at 500, repeated at 2000 and stood for 11000 — six
/// pulses where retail runs three.
/// <para>
/// The "4s" and "8s" in the beacon names are not lifetimes; every variant gets the same seven seconds.
/// Eleven looks like somebody splitting the difference.
/// </para>
/// <para>
/// <b>The lifetime is behavioural; the cadence is a table, and the clock hook does not help here.</b>
/// These two classes do not cast — they call <c>ApplyEffectDirectly</c> on each player in front of
/// them, so <c>GetLastSkillTime</c> never moves and there is nothing for the newly-live combat clock to
/// show. A first draft pinned "it does not burn on landing" against that timestamp and <b>passed on a
/// value that is always zero</b>; it was caught by the pulse-counting pin beside it reading zero too.
/// The observable route for these would be the effect landing on a player, which the harness's
/// invulnerable stand-in does not take.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TiamatBreathBeaconTests
{
	private const int DragonLordsRefuge = 300520000;

	/// <summary>One beacon on each class: the four-second row and the eight.</summary>
	private const int CalculatedBeacon = 283238;
	private const int UltimateBeacon = 283240;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(DragonLordsRefuge).WithWorldSize(2048)
			.WithAi(typeof(CalculatedAtrocityAI), typeof(UltimateAtrocityAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>The beacon is gone at seven seconds, not eleven.</b>
	/// </summary>
	/// <remarks>
	/// Every variant, four-second and eight alike, is spawned with <c>live_time=7</c>. Four extra
	/// seconds on a breath strip is two more pulses at its own cadence.
	/// </remarks>
	[Theory]
	[InlineData(CalculatedBeacon)]
	[InlineData(UltimateBeacon)]
	public void TheBeaconIsGoneAtSevenSeconds(int npcId)
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(npcId, 470f, 514f, 417f);

		harness.Clock.Advance(TimeSpan.FromSeconds(6));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == npcId));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == npcId));
	}

	/// <summary>
	/// <b>It opens two seconds after landing, and pulses every two.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>set_idle_timer</c>, on waking and on every firing alike. Half a second was this
	/// port's, and it gave a player standing on a strip as it appeared no time to leave.
	/// </remarks>
	[Fact]
	public void ItOpensTwoSecondsAfterLandingAndPulsesEveryTwo()
	{
		Assert.Equal(2000L, CalculatedAtrocityAI.OpeningMillis);
		Assert.Equal(2000L, CalculatedAtrocityAI.RepeatMillis);
		Assert.Equal(2000L, UltimateAtrocityAI.OpeningMillis);
		Assert.Equal(2000L, UltimateAtrocityAI.RepeatMillis);
	}

	/// <summary>
	/// <b>Which leaves room for three pulses, not six.</b>
	/// </summary>
	/// <remarks>
	/// The arithmetic the two numbers above exist for, stated so a future change to either has to face
	/// it: seven seconds of life at a two-second beat starting at two is three firings. The old
	/// 500-then-2000 over eleven seconds was six.
	/// </remarks>
	[Fact]
	public void WhichLeavesRoomForThreePulsesNotSix()
	{
		long life = CalculatedAtrocityAI.BeaconLifeMillis;
		long first = CalculatedAtrocityAI.OpeningMillis;
		long beat = CalculatedAtrocityAI.RepeatMillis;

		int pulses = 0;
		for (long at = first; at <= life; at += beat)
			pulses++;

		Assert.Equal(3, pulses);
	}
}
