using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="GoldenTatarAI"/>, translated from retail pattern
/// <c>LDF4b_Golden_Gururu</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two world bosses share the class, so the shared assertions run against both. All three adds were
/// spawned by nothing before this, which makes the caps and the thresholds the whole substance of the
/// port — there are no casts to pin, because neither boss has a skill list at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GoldenTatarAiTests
{
	private const int Cygnea = 210070000;
	private const int AurelianDadar = 235966;
	private const int TatarsBlaze = 220019;

	private const int Clone = 282743;
	private const int ParalysisEye = 282744;
	private const int Lava = 282746;

	public static TheoryData<int> Bosses => new() { AurelianDadar, TatarsBlaze };

	private static (BossAiHarness, Npc, List<Player>) Engaged(int npcId, int hpPercent, int raidSize = 9)
	{
		BossAiHarness harness = BossAiHarness.For(Cygnea).WithWorldSize(2048)
			.WithAi(typeof(GoldenTatarAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
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

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	[Theory]
	[MemberData(nameof(Bosses))]
	public void AtFullHealthItCallsUpNothing(int npcId)
	{
		var (harness, boss, raid) = Engaged(npcId, 95);
		using BossAiHarness _h = harness;

		// The first check lands at 10s; the clone wants below 85, the eye below 60, the lava below 90.
		Advance(harness, boss, raid, 30);

		Assert.Equal(0, Count(harness, Clone));
		Assert.Equal(0, Count(harness, ParalysisEye));
		Assert.Equal(0, Count(harness, Lava));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void BelowEightyFiveItSplitsOffClonesOnTheEightMostHated(int npcId)
	{
		var (harness, boss, raid) = Engaged(npcId, 84);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 11);

		// Nine in the raid, and retail's cap is eight.
		Assert.Equal(8, Count(harness, Clone));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void BelowSixtyTwoPlayersGetAParalysisEye(int npcId)
	{
		var (harness, boss, raid) = Engaged(npcId, 59);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 11);

		Assert.Equal(2, Count(harness, ParalysisEye));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void TheLavaCapsAtSixEvenWithNineInRange(int npcId)
	{
		var (harness, boss, raid) = Engaged(npcId, 89);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 11);

		Assert.Equal(6, Count(harness, Lava));
	}

	/// <summary>
	/// Each threshold is a one-shot, so crossing 90 and then 70 gives two bursts and not an endless
	/// stream. Without the flags the timer-6 chain would re-fire every six seconds forever.
	/// </summary>
	[Fact]
	public void EachLavaThresholdFiresExactlyOnce()
	{
		var (harness, boss, raid) = Engaged(AurelianDadar, 89);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 11);
		Assert.Equal(6, Count(harness, Lava));

		// Sit below 90 for a long while: the 90 step has been spent, so nothing more should arrive.
		Advance(harness, boss, raid, 40);
		Assert.Equal(6, Count(harness, Lava));

		// Cross 70 and exactly one more burst lands.
		BossAiHarness.SetHpPercent(boss, 69);
		Advance(harness, boss, raid, 10);
		Assert.Equal(12, Count(harness, Lava));
	}

	/// <summary>
	/// The threshold branch rests fifty seconds, its repeat branch six. A boss that has already made
	/// clones should not make more for the best part of a minute.
	/// </summary>
	[Fact]
	public void AfterCloningItWaitsFiftySecondsNotSix()
	{
		var (harness, boss, raid) = Engaged(AurelianDadar, 84);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 11);
		Assert.Equal(8, Count(harness, Clone));

		// Well past the six-second recheck, nowhere near the fifty-second rest.
		Advance(harness, boss, raid, 20);

		Assert.Equal(8, Count(harness, Clone));
	}

	[Theory]
	[MemberData(nameof(Bosses))]
	public void DyingClearsEverythingItCalledUp(int npcId)
	{
		var (harness, boss, raid) = Engaged(npcId, 59);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 11);
		Assert.True(Count(harness, ParalysisEye) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, Clone));
		Assert.Equal(0, Count(harness, ParalysisEye));
		Assert.Equal(0, Count(harness, Lava));
	}

	[Fact]
	public void LeavingTheFightClearsThemToo()
	{
		var (harness, boss, raid) = Engaged(AurelianDadar, 84);
		using BossAiHarness _h = harness;
		Advance(harness, boss, raid, 11);
		Assert.True(Count(harness, Clone) > 0);

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness, Clone));
	}
}
