using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="IllusionGateAI"/>, translated from retail pattern
/// <c>BGuard_DrGateChiefD</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The gate is a spawner that closes behind its own guards. It only became reachable once the chamber
/// lords were ported, which is why its three guards were still missing after that work.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class IllusionGateAiTests
{
	private const int KrotanChamber = 300140000;
	private const int IllusionGate = 281226;

	private const int Warguard = 281227;
	private const int Bowguard = 281228;
	private const int Aetherguard = 281229;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(KrotanChamber).WithWorldSize(2048)
			.WithAi(typeof(IllusionGateAI), typeof(AggressiveNpcAI)).Build();
		Npc gate = harness.Spawn(IllusionGate, 526f, 845f, 190f);
		Player player = harness.SpawnPlayer(528f, 847f, 190f);
		harness.Engage(gate, player);
		return (harness, gate, player);
	}

	private static void Advance(BossAiHarness harness, Npc gate, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(gate, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	[Fact]
	public void NothingComesOutBeforeTheFirstFiveSeconds()
	{
		var (harness, gate, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 3);

		Assert.Equal(0, Count(harness, Warguard));
		Assert.Equal(0, Count(harness, Aetherguard));
	}

	[Fact]
	public void TheFirstPairIsAWarguardAndAnAetherguard()
	{
		var (harness, gate, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 7);

		Assert.Equal(1, Count(harness, Warguard));
		Assert.Equal(1, Count(harness, Aetherguard));
		Assert.Equal(0, Count(harness, Bowguard));
	}

	/// <summary>Thirty seconds later, and the second wave is weighted differently.</summary>
	[Fact]
	public void TheSecondWaveIsABowguardAndTwoMoreAetherguards()
	{
		var (harness, gate, player) = Engaged();
		using BossAiHarness _h = harness;
		Advance(harness, gate, player, 7);

		// Still just the first pair most of the way to the second wave.
		Advance(harness, gate, player, 25);
		Assert.Equal(1, Count(harness, Aetherguard));

		Advance(harness, gate, player, 5);
		Assert.Equal(1, Count(harness, Bowguard));
		Assert.Equal(3, Count(harness, Aetherguard));
		Assert.Equal(1, Count(harness, Warguard));
	}

	/// <summary>The gate closes five seconds after the second wave, leaving the guards behind.</summary>
	[Fact]
	public void TheGateClosesBehindItsGuards()
	{
		var (harness, gate, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 39);
		Assert.True(gate.IsSpawned(), "should still be open just before the close");

		Advance(harness, gate, player, 4);

		Assert.False(gate.IsSpawned());
		Assert.Equal(5, Count(harness, Warguard) + Count(harness, Bowguard) + Count(harness, Aetherguard));
	}

	/// <summary>
	/// The chamber lord broadcasts 10009 when it leaves the fight, and the gate shuts on hearing it.
	/// The two halves were only visible read together.
	/// </summary>
	[Fact]
	public void TheLordDisengagingShutsTheGate()
	{
		var (harness, gate, player) = Engaged();
		using BossAiHarness _h = harness;
		Advance(harness, gate, player, 7);
		Assert.True(gate.IsSpawned());

		var listener = (Aion.GameServer.Ai.INpcMessageListener)gate.GetAi();
		listener.OnNpcMessage(gate, IllusionGateAI.LordDisengaged, null);

		Assert.False(gate.IsSpawned());
	}

	/// <summary>The fortress duke's gate — the same mechanic, its own three guards.</summary>
	private const int DukesGate = 284978;
	private const int DukesWarguard = 284979;
	private const int DukesBowguard = 284980;
	private const int DukesAetherguard = 284981;

	private static (BossAiHarness, Npc, Player) DukesGateEngaged()
	{
		BossAiHarness harness = BossAiHarness.For(KrotanChamber).WithWorldSize(2048)
			.WithAi(typeof(IllusionGateAI), typeof(AggressiveNpcAI)).Build();
		Npc gate = harness.Spawn(DukesGate, 526f, 845f, 190f);
		Player player = harness.SpawnPlayer(528f, 847f, 190f);
		harness.Engage(gate, player);
		return (harness, gate, player);
	}

	/// <summary>
	/// The duke's gate pours out <b>its own</b> guards, not the chamber lord's.
	/// </summary>
	/// <remarks>
	/// It was pouring out the chamber lord's. Both gates carry the same <c>ai_name</c> and the class
	/// had one hardcoded set, so 284978 opened and 281227/281228/281229 came through — while its own
	/// three were in nobody's reach. A shared AI name is not a shared guard list.
	/// </remarks>
	[Fact]
	public void TheDukesGatePoursOutItsOwnGuards()
	{
		var (harness, gate, player) = DukesGateEngaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 6);

		Assert.Equal(1, Count(harness, DukesWarguard));
		Assert.Equal(1, Count(harness, DukesAetherguard));
		Assert.Equal(0, Count(harness, Warguard));
		Assert.Equal(0, Count(harness, Aetherguard));
	}

	/// <summary>And its second wave is its own too — a bowguard and two more aetherguards.</summary>
	[Fact]
	public void TheDukesGatesSecondWaveIsItsOwn()
	{
		var (harness, gate, player) = DukesGateEngaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 36);

		Assert.Equal(1, Count(harness, DukesBowguard));
		Assert.Equal(3, Count(harness, DukesAetherguard));
		Assert.Equal(0, Count(harness, Bowguard));
	}

	/// <summary>Its timings are the chamber lord gate's: five seconds, thirty more, then it closes.</summary>
	[Fact]
	public void TheDukesGateKeepsTheSameClock()
	{
		var (harness, gate, player) = DukesGateEngaged();
		using BossAiHarness _h = harness;

		Advance(harness, gate, player, 4);
		Assert.Equal(0, Count(harness, DukesWarguard));

		Advance(harness, gate, player, 2);
		Assert.Equal(1, Count(harness, DukesWarguard));

		// Five seconds after the second wave the gate itself closes, leaving the guards behind.
		Advance(harness, gate, player, 35);
		Assert.DoesNotContain(harness.LiveNpcs(), n => n.GetNpcId() == DukesGate);
		Assert.Equal(1, Count(harness, DukesBowguard));
	}
}
