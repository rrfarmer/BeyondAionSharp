using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Brigade General Laksyaka, whose skeleton wave hung off a three per cent roll per blow.
/// </summary>
/// <remarks>
/// Retail runs him on three battle timers: the eye at sixteen seconds and every sixteen after, the
/// skeletons at fifteen and then every twenty, and the rage below fifteen per cent. This class rolled
/// three per cent on every blow for the skeletons and ran the eye at five seconds and then every forty.
/// <para>
/// Found by <c>audit_timer_drift.py</c>, which compares every scheduled delay in a class against the
/// delays its retail pattern actually uses.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class BrigadeGeneralLaksyakaAiTests
{
	private const int TiamatStronghold = 300510000;

	private const int Laksyaka = 219356;
	private const int Skeleton = 283115;


	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(BrigadeGeneralLaksyakaAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	private static Npc Engaged(BossAiHarness harness)
	{
		Npc boss = harness.Spawn(Laksyaka, 629f, 1319f, 501f);
		Player player = harness.SpawnPlayer(633f, 1319f, 501f);
		harness.Engage(boss, player);
		return boss;
	}

	/// <summary>
	/// <b>The first wave is fifteen seconds in, and it is four skeletons.</b>
	/// </summary>
	/// <remarks>
	/// There was no clock at all before this — the wave came when a three per cent roll happened to land,
	/// so a hard-hitting group saw several in the first few seconds and a careful one saw none.
	/// </remarks>
	[Fact]
	public void TheFirstWaveArrivesAtFifteenSeconds()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(14));
		Assert.Equal(0, Count(harness, Skeleton));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(4, Count(harness, Skeleton));
	}

	/// <summary>
	/// <b>They last twenty seconds and the next wave follows at twenty.</b>
	/// </summary>
	/// <remarks>
	/// Retail gives the skeletons a twenty-second <c>live_time</c> and re-arms the rung at twenty, so one
	/// wave is clearing as the next arrives. The "only if none are standing" guard this class had would
	/// have suppressed every wave after the first once the timings were right — retail's rung has no such
	/// guard and does not need one.
	/// </remarks>
	[Fact]
	public void EachWaveClearsAsTheNextArrives()
	{
		using BossAiHarness harness = NewHarness();
		Engaged(harness);

		harness.Clock.Advance(TimeSpan.FromSeconds(16));
		Assert.Equal(4, Count(harness, Skeleton));

		// First wave placed at 15 and gone by 35; the second lands at 35 too.
		harness.Clock.Advance(TimeSpan.FromSeconds(20));
		Assert.Equal(4, Count(harness, Skeleton));

		// And a third at 55, so three waves have come inside a minute.
		BossAiHarness.Watched seen = harness.WatchNew(25, null, Skeleton);
		Assert.Equal(4, seen.Total);
	}

	/// <summary>
	/// <b>Below fifteen per cent the waves stop.</b>
	/// </summary>
	/// <remarks>
	/// Retail guards both rungs with <c>is_hp_in_boundary larger_than=15</c>. This class had no health
	/// guard on the wave at all.
	/// </remarks>
	[Fact]
	public void BelowFifteenPerCentTheWavesStop()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 12);
		BossAiHarness.Watched seen = harness.WatchNew(45, null, Skeleton);

		Assert.Equal(0, seen.Total);
	}

	/// <summary>
	/// <b>But at thirty per cent they keep coming.</b> The floor is fifteen, not "wounded".
	/// </summary>
	[Fact]
	public void AtThirtyPerCentTheWavesKeepComing()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 30);
		harness.Clock.Advance(TimeSpan.FromSeconds(16));

		Assert.Equal(4, Count(harness, Skeleton));
	}

	/// <summary>
	/// <b>He enrages at fifteen per cent, not twenty-five.</b>
	/// </summary>
	[Fact]
	public void TheRageWaitsForFifteenPerCent()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = Engaged(harness);

		BossAiHarness.SetHpPercent(boss, 20);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, boss);
		Assert.False(boss.GetEffectController().HasAbnormalEffect(20731),
			"Laksyaka enraged at twenty per cent, where retail waits for fifteen");

		BossAiHarness.SetHpPercent(boss, 14);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, boss);
		Assert.True(boss.GetEffectController().HasAbnormalEffect(20731),
			"Laksyaka did not enrage at fourteen per cent");
	}

}
