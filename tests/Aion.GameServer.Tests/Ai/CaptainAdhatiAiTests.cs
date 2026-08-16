using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="CaptainAdhatiAI"/>, translated from retail pattern <c>Dread_DrakanBoss</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What this replaces: <c>xdrakanpriest</c>, a generic behaviour he shared with ninety-four other
/// NPCs — a three-percent chance per hit of calling up somebody else's servant. The pins are about
/// the five-rung escalation that behaviour had nothing to do with.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class CaptainAdhatiAiTests
{
	/// <summary>The Dreadgion, where he stands on the deck.</summary>
	private const int Dreadgion = 300230000;
	private const int Adhati = 214823;

	private const int Attacker = 281344;
	private const int Healer = 281345;
	private const int Buffer = 281346;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Dreadgion).WithWorldSize(2048)
			.WithAi(typeof(CaptainAdhatiAI), typeof(AggressiveNpcAI), typeof(ServantNpcAI))
			.Build();

	/// <summary>His opening pair lands on fixed marks near 485/805, so he fights where he stands.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize = 3)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Adhati, 485f, 800f, 421f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(490f + i, 800f, 421f));
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

	/// <summary>Nobody has pulled him, so the deck is empty — the whole chain hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledAdhatiCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Adhati, 485f, 800f, 421f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, Attacker));
	}

	/// <summary>Two attackers come out the moment he is engaged, onto their own marks.</summary>
	[Fact]
	public void HeOpensWithTwoServantsOnFixedMarks()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Npc[] pair = harness.LiveNpcs().Where(n => n.GetNpcId() == Attacker).ToArray();
		Assert.Equal(2, pair.Length);

		// 488.21 and 482.21 on x, both at 805.47 — fixed marks, not a scatter around him.
		Assert.Equal([482.21f, 488.21f], pair.Select(n => n.GetX()).Order().ToArray());
		Assert.All(pair, n => Assert.Equal(805.47f, n.GetY(), 1));
	}

	/// <summary>And they are the short-lived pair: twenty-five seconds, not the waves' thirty.</summary>
	[Fact]
	public void TheOpeningPairLastsTwentyFiveSeconds()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 24);
		Assert.Equal(2, Count(harness, Attacker));

		Advance(harness, boss, raid, 2);
		Assert.Equal(0, Count(harness, Attacker));
	}

	/// <summary>Below eighty, four more — and no healer or buffer yet.</summary>
	[Fact]
	public void TheFirstRungIsFourAttackers()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Past the opening pair's twenty-five seconds, so what is counted is the rung alone.
		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 79);
		Advance(harness, boss, raid, 11);

		Assert.Equal(4, Count(harness, Attacker));
		Assert.Equal(0, Count(harness, Healer));
		Assert.Equal(0, Count(harness, Buffer));
	}

	/// <summary>Below sixty-five the composition changes: one attacker and a buffer, and briefer.</summary>
	[Fact]
	public void TheSecondRungBringsABuffer()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// The heartbeat is on a known cadence -- ten seconds to the first tick and seven between the
		// fallbacks after it -- so the wave can be landed on an exact second rather than somewhere
		// inside a window. That matters for the lifetime assertion below: measured loosely, a
		// thirty-second wave and a twenty-two-second one both read as gone.
		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 64);
		Advance(harness, boss, raid, 5);

		Assert.Equal(1, Count(harness, Attacker));
		Assert.Equal(1, Count(harness, Buffer));
		Assert.Equal(0, Count(harness, Healer));

		// Twenty-two seconds, where every other wave lives thirty.
		Advance(harness, boss, raid, 20);
		Assert.Equal(1, Count(harness, Buffer));
		Advance(harness, boss, raid, 3);
		Assert.Equal(0, Count(harness, Buffer));
	}

	/// <summary>Below forty-five a healer arrives, which is the rung that makes the fight drag.</summary>
	[Fact]
	public void TheThirdRungBringsAHealer()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 44);
		Advance(harness, boss, raid, 11);

		Assert.Equal(3, Count(harness, Attacker));
		Assert.Equal(1, Count(harness, Healer));
		Assert.Equal(0, Count(harness, Buffer));
	}

	/// <summary>Below twenty he calls everything at once — six, one of each support.</summary>
	[Fact]
	public void TheLastRungCallsSixWithBothSupports()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 19);
		Advance(harness, boss, raid, 11);

		Assert.Equal(4, Count(harness, Attacker));
		Assert.Equal(1, Count(harness, Healer));
		Assert.Equal(1, Count(harness, Buffer));
	}

	/// <summary>
	/// Burned down past every rung at once he takes the deepest, not the shallowest: six servants,
	/// because the twenty-percent branch outranks the rest.
	/// </summary>
	[Fact]
	public void BurnedDownFastHeReachesForTheLastRung()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 19);
		Advance(harness, boss, raid, 11);

		Assert.Equal(1, Count(harness, Healer));
		Assert.Equal(1, Count(harness, Buffer));
	}

	/// <summary>Each rung is a one-shot, so sitting inside a band does not keep calling waves.</summary>
	[Fact]
	public void ARungFiresOnlyOnce()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 26);
		BossAiHarness.SetExactPercent(boss, 79);
		Advance(harness, boss, raid, 25);

		// One wave of four, still inside its thirty-second life. A repeating rung would have laid two.
		Assert.Equal(4, Count(harness, Attacker));
	}

	/// <summary>
	/// The heartbeat runs faster while no rung is due: ten seconds to the first tick, then <b>seven</b>
	/// between the idle ones, where a rung that fires re-arms at ten.
	/// </summary>
	/// <remarks>
	/// A one-second difference at the first opportunity, so it is measured after five idle ticks where
	/// it has grown to five: on retail's cadence the fourth rung-check falls at forty-five seconds, on
	/// a flat ten it would not come round until fifty.
	/// </remarks>
	[Fact]
	public void TheIdleHeartbeatIsFasterThanARungsReArm()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Ticks at 10, 17, 24, 31, 38, 45 — five idle ones, all finding him at full health.
		Advance(harness, boss, raid, 40);
		BossAiHarness.SetExactPercent(boss, 79);

		Advance(harness, boss, raid, 5);
		Assert.Equal(4, Count(harness, Attacker));
	}

	/// <summary>He rounds on somebody else as each wave lands, rather than staying on the tank.</summary>
	[Fact]
	public void EachRungRoundsOnSomebodyElse()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 26);
		Assert.Same(raid[0], boss.GetTarget());

		BossAiHarness.SetExactPercent(boss, 79);
		Advance(harness, boss, raid, 11);

		Assert.NotSame(raid[0], boss.GetTarget());
	}

	/// <summary>Dying takes the deck with him — retail clears the group on both death and reset.</summary>
	[Fact]
	public void DyingClearsHisServants()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		Assert.Equal(2, Count(harness, Attacker));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, Attacker));
	}
}
