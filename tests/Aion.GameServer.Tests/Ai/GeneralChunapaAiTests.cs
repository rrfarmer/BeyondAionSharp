using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="GeneralChunapaAI"/>, translated from retail pattern
/// <c>LDF4a_SandWarm_General</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// One mechanic: burrows under the two most-hated, every forty-five seconds between 51 and 75. The
/// route into that band runs through a heartbeat that switches itself off, so the pins cover both a
/// pull inside the band and a boss fought down into it.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GeneralChunapaAiTests
{
	private const int Cygnea = 210070000;
	private const int Chunapa = 218183;
	private const int ShirikBurrow = 282556;

	private static (BossAiHarness, Npc, List<Player>) Engaged(int hpPercent, int raidSize = 5)
	{
		BossAiHarness harness = BossAiHarness.For(Cygnea).WithWorldSize(2048)
			.WithAi(typeof(GeneralChunapaAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Chunapa, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(302f + i, 302f, 200f));
		BossAiHarness.SetHpPercent(boss, hpPercent);
		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, Npc boss, List<Player> raid, int seconds)
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

	private static int Burrows(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == ShirikBurrow);

	[Fact]
	public void AboveSeventyFiveNoBurrowOpens()
	{
		var (harness, boss, raid) = Engaged(90);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 60);

		Assert.Equal(0, Burrows(harness));
	}

	/// <summary>Two, not one per player — the cap is retail's <c>total_set_to_spawn</c>.</summary>
	[Fact]
	public void InTheBandTwoBurrowsOpenEvenWithFiveInRange()
	{
		var (harness, boss, raid) = Engaged(60);
		using BossAiHarness _h = harness;

		// Timer 0 and timer 1 both come due at three seconds, and phase two's branch re-arms timer 1
		// as it passes, so the first pair lands on the tick after that rather than on it.
		Advance(harness, boss, raid, 8);

		Assert.Equal(2, Burrows(harness));
	}

	/// <summary>
	/// The heartbeat on timer 0 is what notices the crossing at 75 and lights the burrow timer. A boss
	/// pulled at full health has to get there through it, which is every real pull.
	/// </summary>
	[Fact]
	public void AHealthyBossFoughtDownStillOpensThem()
	{
		var (harness, boss, raid) = Engaged(90);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 30);
		Assert.Equal(0, Burrows(harness));

		BossAiHarness.SetHpPercent(boss, 60);
		Advance(harness, boss, raid, 10);

		Assert.Equal(2, Burrows(harness));
	}

	/// <summary>
	/// Forty-five seconds between waves. The burrows last sixty-four, so the first pair is still
	/// standing when the second opens.
	/// </summary>
	[Fact]
	public void TheyOpenAgainEveryFortyFiveSeconds()
	{
		var (harness, boss, raid) = Engaged(60);
		using BossAiHarness _h = harness;
		// Three seconds, not eight: retail's phase-two opener arms the burrow clock at 3s and does not
		// re-arm the heartbeat, so it fires once and starts the cycle immediately. This pin expected
		// eight and two-forever, which was the shape when ArmTimer restarted a pending timer and the
		// opener's short arm was swallowed by the burrow branch's own forty-five.
		Advance(harness, boss, raid, 3);
		Assert.Equal(2, Burrows(harness));

		// And again forty-five seconds after the first pair.
		Advance(harness, boss, raid, 45);
		Assert.Equal(4, Burrows(harness));

		Advance(harness, boss, raid, 8);
		Assert.Equal(4, Burrows(harness));
	}

	[Fact]
	public void DroppingBelowFiftyOneStopsThem()
	{
		var (harness, boss, raid) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 8);
		Assert.Equal(2, Burrows(harness));

		BossAiHarness.SetHpPercent(boss, 30);
		Advance(harness, boss, raid, 90);

		Assert.Equal(0, Burrows(harness));
	}

	[Fact]
	public void DyingClearsTheBurrows()
	{
		var (harness, boss, raid) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 8);
		Assert.Equal(2, Burrows(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Burrows(harness));
	}

	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, raid) = Engaged(60);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 8);
		Assert.Equal(2, Burrows(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Burrows(harness));
	}
}
