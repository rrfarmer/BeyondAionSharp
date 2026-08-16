using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="GatewayGuardAI"/>, translated from retail patterns
/// <c>GwLGuard_FlA</c> and <c>GwDGuard_FlA</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Eight guards, four a side, on two identical patterns. The substance is the trap ladder and the fact
/// that each guard reaches for its own faction's traps.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GatewayGuardAiTests
{
	private const int Inggison = 210050000;
	private const int Gelkmaros = 220070000;

	private const int Trigon = 296444;
	private const int Matigium = 296453;

	private const int ElyosSnare = 281472;
	private const int ElyosThrow = 281473;
	private const int ElyosExplosion = 281474;
	private const int ElyosMine = 281475;

	private const int AsmodianSnare = 281482;
	private const int AsmodianThrow = 281483;
	private const int AsmodianExplosion = 281484;
	private const int AsmodianMine = 281485;

	private static (BossAiHarness, Npc, Player) Engaged(int mapId, int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(mapId).WithWorldSize(2048)
			.WithAi(typeof(GatewayGuardAI), typeof(TrapNpcAI), typeof(AggressiveNpcAI)).Build();
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(guard, hpPercent);
		harness.Engage(guard, player);
		return (harness, guard, player);
	}

	private static void Advance(BossAiHarness harness, Npc guard, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(guard, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>Each side lays its own traps — the two patterns differ only in these ids.</summary>
	[Theory]
	[InlineData(Inggison, Trigon, ElyosSnare, AsmodianSnare)]
	[InlineData(Gelkmaros, Matigium, AsmodianSnare, ElyosSnare)]
	public void EngagingLaysASnareOfItsOwnFaction(int mapId, int npcId, int ours, int theirs)
	{
		var (harness, guard, _) = Engaged(mapId, npcId, 100);
		using BossAiHarness _h = harness;

		Assert.Equal(1, Count(harness, ours));
		Assert.Equal(0, Count(harness, theirs));
	}

	/// <summary>
	/// The ladder, one rung at a time. Deepest threshold wins, so a guard dropped straight to 25 lays
	/// the mine rather than walking up from the throw.
	/// </summary>
	[Theory]
	[InlineData(65, AsmodianThrow)]
	[InlineData(45, AsmodianExplosion)]
	[InlineData(25, AsmodianMine)]
	public void EachThresholdLaysItsOwnTrap(int hpPercent, int expected)
	{
		var (harness, guard, player) = Engaged(Gelkmaros, Matigium, hpPercent);
		using BossAiHarness _h = harness;

		Advance(harness, guard, player, 12);

		Assert.Equal(1, Count(harness, expected));

		// And nothing from a rung this guard has not reached. Asserting only the expected trap lets a
		// widened threshold pass unnoticed, because the ladder walks down and lays the right one a tick
		// later anyway.
		foreach (int deeper in new[] { AsmodianThrow, AsmodianExplosion, AsmodianMine })
			if (deeper != expected)
				Assert.Equal(0, Count(harness, deeper));
	}

	/// <summary>Each rung is a one-shot: timer 0 keeps ticking every five seconds regardless.</summary>
	[Fact]
	public void ARungLaysOnceNotOnEveryTick()
	{
		var (harness, guard, player) = Engaged(Gelkmaros, Matigium, 65);
		using BossAiHarness _h = harness;
		var seen = new HashSet<Npc>();

		for (int i = 0; i < 40; i++)
		{
			Advance(harness, guard, player, 1);
			foreach (Npc trap in harness.LiveNpcs().Where(n => n.GetNpcId() == AsmodianThrow))
				seen.Add(trap);
		}

		Assert.Single(seen);
	}

	/// <summary>
	/// The empty rungs at 60, 40 and 20 spend a tick each. A guard fought steadily down should walk
	/// the whole ladder and end with all four traps having been laid.
	/// </summary>
	[Fact]
	public void FoughtAllTheWayDownItLaysEveryTrap()
	{
		var (harness, guard, player) = Engaged(Gelkmaros, Matigium, 100);
		using BossAiHarness _h = harness;
		Assert.Equal(1, Count(harness, AsmodianSnare));

		foreach (int hp in new[] { 65, 45, 25 })
		{
			BossAiHarness.SetHpPercent(guard, hp);
			Advance(harness, guard, player, 20);
		}

		Assert.Equal(1, Count(harness, AsmodianThrow));
		Assert.Equal(1, Count(harness, AsmodianExplosion));
		Assert.Equal(1, Count(harness, AsmodianMine));
	}

	[Fact]
	public void TheTrapsLastAMinuteAndGo()
	{
		var (harness, guard, player) = Engaged(Gelkmaros, Matigium, 100);
		using BossAiHarness _h = harness;
		List<Npc> snare = harness.LiveNpcs().Where(n => n.GetNpcId() == AsmodianSnare).ToList();
		Assert.Single(snare);

		Advance(harness, guard, player, 58);
		Assert.All(snare, t => Assert.True(t.IsSpawned(), "should still stand short of a minute"));

		Advance(harness, guard, player, 4);
		Assert.All(snare, t => Assert.False(t.IsSpawned(), "should have gone after a minute"));
	}

	/// <summary>
	/// A guard pulled at five percent walks the whole ladder rather than skipping to the bottom: every
	/// threshold below seventy matches, and each is a separate one-shot, so they fire in turn a tick
	/// apart and all three traps go down over about fifteen seconds.
	/// </summary>
	/// <remarks>
	/// The deepest rung, below ten, lays nothing — it only calls out — so the count stops at three plus
	/// the snare from engaging, not four.
	/// </remarks>
	[Fact]
	public void PulledNearlyDeadItStillWalksEveryRung()
	{
		var (harness, guard, player) = Engaged(Gelkmaros, Matigium, 5);
		using BossAiHarness _h = harness;
		Assert.Equal(1, Count(harness, AsmodianSnare));

		// The rungs fire five seconds apart from the deepest up: mine at fifteen seconds, explosion at
		// twenty-five, throw at thirty-five. All three are still standing on their sixty-second life.
		// At thirty seconds the throw is still one rung away — the empty rungs at sixty and forty each
		// spend a tick getting there, and without them it would already be down.
		Advance(harness, guard, player, 30);
		Assert.Equal(0, Count(harness, AsmodianThrow));

		Advance(harness, guard, player, 15);

		Assert.Equal(1, Count(harness, AsmodianThrow));
		Assert.Equal(1, Count(harness, AsmodianExplosion));
		Assert.Equal(1, Count(harness, AsmodianMine));
	}
}
