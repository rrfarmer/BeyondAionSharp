using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="NagaCaptainAI"/>, translated from retail pattern <c>Naga_WrF</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two NPCs share the class. The mechanic is four slaves on the current target, once on entering
/// 41-60 and then every ninety seconds while the fight stays there.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NagaCaptainAiTests
{
	private const int Reshanta = 400010000;
	private const int NagaSorcerer = 290126;
	private const int CaptainLahbri = 256115;
	private const int NagaSlave = 290127;

	public static TheoryData<int> Captains => new() { NagaSorcerer, CaptainLahbri };

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, int hpPercent)
	{
		BossAiHarness harness = BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(NagaCaptainAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Slaves(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == NagaSlave);

	[Theory]
	[MemberData(nameof(Captains))]
	public void AboveSixtyNoSlaveIsCalled(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 80);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 120);

		Assert.Equal(0, Slaves(harness));
	}

	[Theory]
	[MemberData(nameof(Captains))]
	public void InTheSummonBandFourSlavesArrive(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 50);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 15);

		Assert.Equal(4, Slaves(harness));
	}

	/// <summary>
	/// The first call is a one-shot on timer 1; the repeat rides timer 4 at ninety seconds. Without
	/// the one-shot the six-second heartbeat would call four more every six seconds.
	/// </summary>
	[Fact]
	public void TheCallRepeatsEveryNinetySecondsAndNotSooner()
	{
		var (harness, boss, player) = Engaged(CaptainLahbri, 50);
		using BossAiHarness _h = harness;

		Advance(harness, boss, player, 15);
		Assert.Equal(4, Slaves(harness));

		// Most of the way to the repeat, and still only the first four.
		Advance(harness, boss, player, 80);
		Assert.Equal(4, Slaves(harness));

		Advance(harness, boss, player, 15);
		Assert.Equal(8, Slaves(harness));

		// And again, because the first repeat rides the timer the *opening* branch armed — watching
		// only that far cannot tell a ninety-second repeat from a nine-second one, nor from a branch
		// that never re-arms at all.
		Advance(harness, boss, player, 80);
		Assert.Equal(8, Slaves(harness));

		Advance(harness, boss, player, 15);
		Assert.Equal(12, Slaves(harness));
	}

	/// <summary>
	/// Below the band the ninety-second timer stops matching, so a captain fought past it gets no
	/// further reinforcements.
	/// </summary>
	[Fact]
	public void DroppingOutOfTheBandStopsTheReinforcements()
	{
		var (harness, boss, player) = Engaged(CaptainLahbri, 50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 15);
		Assert.Equal(4, Slaves(harness));

		BossAiHarness.SetHpPercent(boss, 30);
		Advance(harness, boss, player, 120);

		Assert.Equal(4, Slaves(harness));
	}

	/// <summary>
	/// A captain pulled at full health still reaches the band: timer 1's heartbeat is what carries it
	/// down through the bands this port does not translate.
	/// </summary>
	[Fact]
	public void ACaptainFoughtDownFromFullStillCallsThem()
	{
		var (harness, boss, player) = Engaged(CaptainLahbri, 90);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 40);
		Assert.Equal(0, Slaves(harness));

		BossAiHarness.SetHpPercent(boss, 50);
		Advance(harness, boss, player, 10);

		Assert.Equal(4, Slaves(harness));
	}

	[Theory]
	[MemberData(nameof(Captains))]
	public void DyingClearsTheSlaves(int npcId)
	{
		var (harness, boss, player) = Engaged(npcId, 50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 15);
		Assert.Equal(4, Slaves(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Slaves(harness));
	}

	/// <summary>
	/// Our stand-in for retail's <c>despawn_at_attack_state</c>: the slaves live fifty minutes, so a
	/// reset that left them standing would strand four elites in the abyss.
	/// </summary>
	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, player) = Engaged(CaptainLahbri, 50);
		using BossAiHarness _h = harness;
		Advance(harness, boss, player, 15);
		Assert.Equal(4, Slaves(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Slaves(harness));
	}
}
