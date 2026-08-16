using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="GatewayTrapGuardAI"/>, translated from retail patterns <c>GwLGuard_PhA</c>,
/// <c>GwLGuard_WhA</c>, <c>GwDGuard_PhA</c> and <c>GwDGuard_WhA</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// One mechanic across twelve guards, so what is pinned is the mechanic and one guard of each role
/// plus the two per-pattern quirks. The rest are the same table with different trap ids.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GatewayTrapGuardAiTests
{
	/// <summary>Inggison and Gelkmaros, where the gateway garrisons stand.</summary>
	private const int Inggison = 210050000;

	private const int ElyosPriest = 296449;
	private const int ElyosMage = 296451;
	private const int AsmodianPriest = 296458;
	private const int AsmodianMage = 296460;

	private const int NetTrap = 281477;
	private const int FlashTrap = 281478;
	private const int AcidTrap = 281479;
	private const int RuneTrap = 281480;
	private const int CloudTrap = 281481;

	private const int ArchonNetTrap = 281487;
	private const int ArchonMagicTrap = 281490;
	private const int SleepdustTrap = 281491;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Inggison).WithWorldSize(2048)
			.WithAi(typeof(GatewayTrapGuardAI), typeof(AggressiveNpcAI), typeof(TrapNpcAI))
			.Build();

	/// <summary>The guard and its quarry stand well apart, so where a trap lands is unambiguous.</summary>
	private static (BossAiHarness, Npc, Player) Engaged(int npcId)
	{
		BossAiHarness harness = NewHarness();
		Npc guard = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(360f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(guard, player);
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

	/// <summary>A guard nobody has touched lays nothing — the ladder hangs off entering the fight.</summary>
	[Fact]
	public void AnUnengagedGuardLaysNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(ElyosPriest, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, Count(harness, NetTrap));
	}

	/// <summary>A priest opens with a net trap at its own feet, the moment it is engaged.</summary>
	[Fact]
	public void APriestOpensWithANetTrapAtItsOwnFeet()
	{
		var (harness, guard, player) = Engaged(ElyosPriest);
		using BossAiHarness _h = harness;

		Npc trap = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == NetTrap));

		float toGuard = Math.Abs(trap.GetX() - guard.GetX());
		float toPlayer = Math.Abs(trap.GetX() - player.GetX());
		Assert.True(toGuard < toPlayer,
			$"a priest defends its ground: {toGuard:F1}m from the guard, {toPlayer:F1}m from the quarry");
	}

	/// <summary>A mage opens with a sleep trap on its quarry instead — the same rung, a different place.</summary>
	[Fact]
	public void AMageOpensWithASleepTrapOnItsQuarry()
	{
		var (harness, guard, player) = Engaged(AsmodianMage);
		using BossAiHarness _h = harness;

		Npc trap = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == SleepdustTrap));

		float toGuard = Math.Abs(trap.GetX() - guard.GetX());
		float toPlayer = Math.Abs(trap.GetX() - player.GetX());
		Assert.True(toPlayer < toGuard,
			$"a mage puts it on the player: {toPlayer:F1}m from the quarry, {toGuard:F1}m from the guard");
	}

	/// <summary>Below fifty it lays the second rung, and not before.</summary>
	[Fact]
	public void TheSecondRungWaitsForFiftyPercent()
	{
		var (harness, guard, player) = Engaged(ElyosPriest);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(guard, 55);
		Advance(harness, guard, player, 12);
		Assert.Equal(0, Count(harness, FlashTrap));

		BossAiHarness.SetExactPercent(guard, 49);
		Advance(harness, guard, player, 6);
		Assert.Equal(1, Count(harness, FlashTrap));
	}

	/// <summary>And only once — the rung carries a flag var, so it is a step rather than a regime.</summary>
	[Fact]
	public void TheSecondRungFiresOnlyOnce()
	{
		var (harness, guard, player) = Engaged(ElyosPriest);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(guard, 49);
		Advance(harness, guard, player, 40);

		// The one it laid expires after a minute, so a repeating rung would have several standing.
		Assert.Equal(1, Count(harness, FlashTrap));
	}

	/// <summary>
	/// A guard burned down past both rungs at once lays the <b>rune</b> trap, not the flash: the
	/// deeper rung outranks the shallower, which is how retail orders them.
	/// </summary>
	[Fact]
	public void BurnedDownFastItReachesForTheRuneTrap()
	{
		var (harness, guard, player) = Engaged(ElyosPriest);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(guard, 29);
		Advance(harness, guard, player, 6);

		Assert.Equal(1, Count(harness, RuneTrap));
		Assert.Equal(0, Count(harness, FlashTrap));
	}

	/// <summary>Both roles reach for the same rune trap at thirty; only the middle rung differs.</summary>
	[Fact]
	public void TheMagesMiddleRungIsAcidRatherThanFlash()
	{
		var (harness, guard, player) = Engaged(ElyosMage);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(guard, 49);
		Advance(harness, guard, player, 6);

		Assert.Equal(1, Count(harness, AcidTrap));
		Assert.Equal(0, Count(harness, FlashTrap));
		Assert.True(Count(harness, CloudTrap) == 1, "and its opening trap is still the cloud");
	}

	/// <summary>Each side lays its own traps: an asmodian priest opens with the archon's net.</summary>
	[Fact]
	public void EachFactionLaysItsOwnTraps()
	{
		var (harness, guard, player) = Engaged(AsmodianPriest);
		using BossAiHarness _h = harness;

		Assert.Equal(1, Count(harness, ArchonNetTrap));
		Assert.Equal(0, Count(harness, NetTrap));

		BossAiHarness.SetExactPercent(guard, 29);
		Advance(harness, guard, player, 6);

		Assert.Equal(1, Count(harness, ArchonMagicTrap));
		Assert.Equal(0, Count(harness, RuneTrap));
	}

	/// <summary>
	/// The elyos priest's opening net trap lives <b>fifty</b> seconds where every other trap in the
	/// family lives sixty — a per-pattern quirk, kept literal.
	/// </summary>
	[Fact]
	public void TheElyosPriestsOpeningTrapIsTenSecondsShorter()
	{
		var (harness, guard, player) = Engaged(ElyosPriest);
		using BossAiHarness _h = harness;

		Advance(harness, guard, player, 49);
		Assert.Equal(1, Count(harness, NetTrap));

		Advance(harness, guard, player, 2);
		Assert.Equal(0, Count(harness, NetTrap));
	}

	/// <summary>Its asmodian counterpart keeps the full minute, so the fifty is not a family constant.</summary>
	[Fact]
	public void TheAsmodianPriestsOpeningTrapKeepsTheFullMinute()
	{
		var (harness, guard, player) = Engaged(AsmodianPriest);
		using BossAiHarness _h = harness;

		Advance(harness, guard, player, 55);
		Assert.Equal(1, Count(harness, ArchonNetTrap));

		Advance(harness, guard, player, 6);
		Assert.Equal(0, Count(harness, ArchonNetTrap));
	}
}
