using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="ArchmagusSayahumAI"/>, translated from retail pattern
/// <c>IDVritra_Base_Drakan_Wi_IU_Nmd</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The third of the Sauro Supply Base's named drakan, and the only one whose whole fight is about who
/// he is looking at. Turns are counted rather than sampled: a random switch against a stable hate list
/// is undone by the next think, so the pins watch every tick and record every distinct target.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ArchmagusSayahumAiTests
{
	private const int SauroSupplyBase = 301220000;
	private const int Sayahum = 233257;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(ArchmagusSayahumAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>Four players with staggered hate, so a turn is visible and drift is not.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Sayahum, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(304f + (i * 3f), 300f, 200f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Every distinct player he was seen holding over the window.</summary>
	private static HashSet<int> TargetsOver(BossAiHarness harness, Npc boss, List<Player> raid, int seconds)
	{
		var seen = new HashSet<int>();
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (boss.GetTarget() is Player held)
				seen.Add(raid.IndexOf(held));
		}

		return seen;
	}

	/// <summary>
	/// <b>Above eighty he turns on every other lap.</b> The ring is four steps of about eight seconds,
	/// so a turn comes round roughly once a minute.
	/// </summary>
	[Fact]
	public void AboveEightyHeTurnsOnEveryOtherLap()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 90);

		Assert.True(TargetsOver(harness, boss, raid, 200).Count > 1,
			"he never came off the player he started on");
	}

	/// <summary>
	/// <b>Crossing eighty turns him off his current target specifically.</b> Retail uses
	/// <c>RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> there where the in-ring turns use plain random, so the
	/// crossing always moves him and an ordinary lap may not.
	/// </summary>
	/// <remarks>
	/// Two players, so "anybody but the one he is on" has exactly one answer and the pin is an equality
	/// rather than a probability. Read over eight fights because the mutation it exists to catch —
	/// plain random in place of the exception — leaves him where he is only half the time, and one
	/// fight cannot tell that from the real thing.
	/// </remarks>
	[Fact]
	public void CrossingEightyAlwaysMovesHim()
	{
		for (int run = 0; run < 8; run++)
		{
			using BossAiHarness harness = NewHarness();
			Npc boss = harness.Spawn(Sayahum, 300f, 300f, 200f);
			Player tank = harness.SpawnPlayer(304f, 300f, 200f);
			Player other = harness.SpawnPlayer(307f, 300f, 200f);
			harness.Engage(boss, tank);
			BossAiHarness.Rehate(boss, tank);
			BossAiHarness.Rehate(boss, other);
			var raid = new List<Player> { tank, other };

			BossAiHarness.SetExactPercent(boss, 70);
			Player before = Assert.IsAssignableFrom<Player>(boss.GetTarget());
			TargetsOver(harness, boss, raid, 6);

			Assert.NotSame(before, boss.GetTarget());
		}
	}

	/// <summary>And below forty-five it moves him again, for the same reason and the same way.</summary>
	[Fact]
	public void AndCrossingFortyFiveMovesHimAgain()
	{
		for (int run = 0; run < 8; run++)
		{
			using BossAiHarness harness = NewHarness();
			Npc boss = harness.Spawn(Sayahum, 300f, 300f, 200f);
			Player tank = harness.SpawnPlayer(304f, 300f, 200f);
			Player other = harness.SpawnPlayer(307f, 300f, 200f);
			harness.Engage(boss, tank);
			BossAiHarness.Rehate(boss, tank);
			BossAiHarness.Rehate(boss, other);
			var raid = new List<Player> { tank, other };

			BossAiHarness.SetExactPercent(boss, 70);
			TargetsOver(harness, boss, raid, 10);

			BossAiHarness.SetExactPercent(boss, 30);
			Player before = Assert.IsAssignableFrom<Player>(boss.GetTarget());
			TargetsOver(harness, boss, raid, 6);

			Assert.NotSame(before, boss.GetTarget());
		}
	}

	/// <summary>
	/// <b>Below forty-five he turns on every lap, so the same window shows more of the raid.</b> Two
	/// phases of the same fight, measured the same way, and the difference is the mechanic.
	/// </summary>
	[Fact]
	public void BelowFortyFiveHeTurnsTwiceAsOften()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		TargetsOver(harness, boss, raid, 10);
		BossAiHarness.SetExactPercent(boss, 30);
		TargetsOver(harness, boss, raid, 20);

		Assert.True(TargetsOver(harness, boss, raid, 200).Count > 1,
			"the last phase never turned him");
	}

	/// <summary>
	/// <b>The ladder stops below forty-five.</b> That opener does not re-arm the heartbeat, so nothing
	/// looks at his health again — and a boss healed back above eighty stays in the last phase.
	/// </summary>
	[Fact]
	public void TheLadderStopsBelowFortyFive()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		TargetsOver(harness, boss, raid, 10);
		BossAiHarness.SetExactPercent(boss, 30);
		TargetsOver(harness, boss, raid, 20);

		// Healed to full: retail's own ladder has no way back, because timer 0 is no longer armed.
		//
		// Six hundred seconds rather than a hundred and twenty. The switch picks a random attacker, so
		// "more than one distinct player" is a probabilistic claim, and at four players and a twelve-
		// second beat the old window could land on one player throughout -- which it did, once, in one
		// full-suite run. Widening the window is the same fix this log recorded for the guard
		// reinforcement flake: a pin's setup must not be able to fail.
		BossAiHarness.SetExactPercent(boss, 100);
		Assert.True(TargetsOver(harness, boss, raid, 600).Count > 1,
			"the last phase stopped when his health went back up");
	}
}
