using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TiamatDrakanMageAI"/>, <see cref="DreadgionDrakanMageAI"/> and
/// <see cref="GreatMagicalBarrierAI"/>, translated from retail patterns
/// <c>IDTiamat_*_DrakanWi_60_Ae</c>, <c>IDDreadgion_03_DrakanWi_*</c> and <c>IDYun_Temp_65</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Seven bosses on plain <c>aggressive</c> across two instances, and a barrier that is a hazard engine
/// rather than a debuff. The shape worth pinning is that the Stronghold mages pay exactly twice, that
/// the Dreadgion ones drop a barrier on a random attacker every fifteen seconds, and that the barrier
/// then re-hazards the ground under it every two.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DrakanMageAiTests
{
	private const int Stronghold = 300510000;
	private const int Dredgion = 300440000;

	private const int Magistrate = 219375;
	private const int Anusa = 233371;
	private const int Thaumaturge = 233354;

	private const int MagicHand = 282989;
	private const int Barrier = 282984;
	private const int Pulse = 282985;

	private static BossAiHarness NewHarness(int map) =>
		BossAiHarness.For(map).WithWorldSize(2048)
			.WithAi(typeof(TiamatDrakanMageAI), typeof(DreadgionDrakanMageAI),
				typeof(GreatMagicalBarrierAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int map, int npcId, int raidSize = 3)
	{
		BossAiHarness harness = NewHarness(map);
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(303f + i, 300f, 200f));

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// How many of this kind <em>arrived</em> in the next stretch of fight. Both summons here expire —
	/// a hand in a minute, a barrier in eight seconds — so counting what is standing at the end of a
	/// phase measures the lifetime and not the rung; and these pins have several phases, so a summon
	/// that survives into the next one would otherwise be counted twice. Hence
	/// <see cref="BossAiHarness.WatchNew"/> rather than a count or a plain watch.
	/// </summary>
	private static int Arrived(BossAiHarness harness, Npc boss, List<Player> raid, int seconds,
		int npcId) =>
		harness.WatchNew(seconds, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, npcId).Total;

	// ---- Tiamat's Stronghold --------------------------------------------------------------------

	/// <summary>Above eighty a Stronghold magister calls nothing, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyTheStrongholdMageCallsNothing()
	{
		var (harness, boss, raid) = Engaged(Stronghold, Magistrate);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);

		Assert.Equal(0, Arrived(harness, boss, raid, 120, MagicHand));
	}

	/// <summary>
	/// <b>Two hands, and only two.</b> One on crossing eighty and one on crossing thirty, each once
	/// however long the fight spends in either band.
	/// </summary>
	[Fact]
	public void OneHandOnEachCrossingAndNoMore()
	{
		var (harness, boss, raid) = Engaged(Stronghold, Magistrate);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Assert.Equal(1, Arrived(harness, boss, raid, 12, MagicHand));

		// Standing in the band pays nothing more: the rung carries a flag var, and the slot is still
		// ticking every six or seven seconds while it does nothing.
		Assert.Equal(0, Arrived(harness, boss, raid, 40, MagicHand));

		BossAiHarness.SetExactPercent(boss, 20);
		Assert.Equal(1, Arrived(harness, boss, raid, 20, MagicHand));

		Assert.Equal(0, Arrived(harness, boss, raid, 60, MagicHand));
	}

	/// <summary>
	/// A raid that pushes straight to the end still pays both — the two rungs sit on one slot and it
	/// keeps ticking, so the second follows the first seven seconds later rather than being skipped.
	/// </summary>
	[Fact]
	public void PushedStraightToTwentyItStillPaysBoth()
	{
		var (harness, boss, raid) = Engaged(Stronghold, Magistrate);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);

		Assert.Equal(2, Arrived(harness, boss, raid, 25, MagicHand));
	}

	/// <summary>A hand keeps a minute and then goes.</summary>
	[Fact]
	public void AHandKeepsAMinute()
	{
		var (harness, boss, raid) = Engaged(Stronghold, Magistrate);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, raid, boss, 12);
		Npc hand = Assert.Single(harness.LiveNpcs(), n => n.GetNpcId() == MagicHand);

		Advance(harness, raid, boss, 55);
		Assert.True(hand.IsSpawned(), "it went before its minute was up");

		Advance(harness, raid, boss, 10);
		Assert.False(hand.IsSpawned(), "it outlived its minute");
	}

	// ---- the Dreadgion --------------------------------------------------------------------------

	/// <summary>
	/// <b>A barrier lands on somebody every fifteen seconds.</b> Watched rather than counted, because
	/// it keeps eight seconds against a fifteen-second window and the ground is usually clear.
	/// </summary>
	[Fact]
	public void ABarrierEveryFifteenSeconds()
	{
		var (harness, boss, raid) = Engaged(Dredgion, Thaumaturge);
		using BossAiHarness _h = harness;

		BossAiHarness.Watched seen = harness.Watch(40, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Barrier);

		Assert.Equal(3, seen.Total);
		Assert.Equal(1, seen.Peak);
	}

	/// <summary>
	/// <b>And the barrier is a hazard engine, not a debuff.</b> It arrives fixed on the player it was
	/// dropped on, which puts it in combat, and from then on it re-hazards the ground under it every
	/// two seconds until its eight are up.
	/// </summary>
	[Fact]
	public void TheBarrierPulsesTheGroundUnderIt()
	{
		var (harness, boss, raid) = Engaged(Dredgion, Thaumaturge);
		using BossAiHarness _h = harness;

		BossAiHarness.Watched pulses = harness.Watch(40, () =>
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}
		}, Pulse);

		Assert.True(pulses.Total >= 6, $"three barriers should pulse repeatedly; saw {pulses.Total}");
	}

	/// <summary>
	/// <b>And it lands on a random attacker, not on the tank.</b> Ten barriers over two and a half
	/// minutes, with the raid spread out enough to tell who each one arrived on.
	/// </summary>
	/// <remarks>
	/// Pinned by position because nothing else separates the two: a barrier is aggressive and lands on
	/// top of somebody, so it engages whoever that is whether or not retail's single hate point was
	/// seeded. That is why <c>attack_target_after_spawn</c> is carried but left as a deliberate
	/// mutation survivor — see the class remarks.
	/// </remarks>
	[Fact]
	public void TheBarrierLandsOnARandomAttacker()
	{
		BossAiHarness harness = NewHarness(Dredgion);
		using BossAiHarness _h = harness;
		Npc boss = harness.Spawn(Thaumaturge, 300f, 300f, 200f);

		// Twenty metres apart, so the nearest player to a barrier is unambiguously the one it landed on.
		var raid = new List<Player>
		{
			harness.SpawnPlayer(305f, 300f, 200f),
			harness.SpawnPlayer(305f, 320f, 200f),
			harness.SpawnPlayer(305f, 340f, 200f),
		};

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);

		var landedOn = new HashSet<int>();
		for (int i = 0; i < 150; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));

			foreach (Npc placed in harness.LiveNpcs().Where(n => n.GetNpcId() == Barrier))
			{
				Player nearest = raid.OrderBy(p =>
					Math.Abs(p.GetY() - placed.GetY()) + Math.Abs(p.GetX() - placed.GetX())).First();
				landedOn.Add(nearest.GetObjectId());
			}
		}

		Assert.True(landedOn.Count > 1,
			"every barrier in ten windows landed on the same player — it is not picking at random");
	}

	/// <summary>Above thirty no hand comes; below it, one lands on the tank and only one.</summary>
	[Fact]
	public void TheDreadgionHandComesOnceBelowThirty()
	{
		var (harness, boss, raid) = Engaged(Dredgion, Thaumaturge);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 60);
		Assert.Equal(0, Arrived(harness, boss, raid, 40, MagicHand));

		BossAiHarness.SetExactPercent(boss, 20);
		Assert.Equal(1, Arrived(harness, boss, raid, 10, MagicHand));

		// The rung does not re-arm its slot, so the clock carrying it is over.
		Assert.Equal(0, Arrived(harness, boss, raid, 60, MagicHand));
	}

	/// <summary>Dying clears everything a Dreadgion magister put out.</summary>
	[Fact]
	public void DyingClearsWhatTheThaumaturgePlaced()
	{
		var (harness, boss, raid) = Engaged(Dredgion, Thaumaturge);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 10);
		Assert.True(Count(harness, MagicHand) + Count(harness, Barrier) >= 1);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, MagicHand));
		Assert.Equal(0, Count(harness, Barrier));
	}

	/// <summary>
	/// <b>Captain Anusa clears up on waking instead.</b> Retail moves his despawn from
	/// <c>on_die</c> to <c>on_wake_up</c>, so a second pull starts clean rather than the first kill
	/// tidying after itself.
	/// </summary>
	[Fact]
	public void AnusaClearsOnWakingRatherThanOnDying()
	{
		var (harness, boss, raid) = Engaged(Dredgion, Anusa);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 10);
		int placed = Count(harness, MagicHand) + Count(harness, Barrier);
		Assert.True(placed >= 1);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(placed, Count(harness, MagicHand) + Count(harness, Barrier));
	}
}
