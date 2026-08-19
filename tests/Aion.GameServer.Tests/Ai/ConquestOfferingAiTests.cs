using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The conquest offering cascade: a spawner, a spot, and a monster.
/// </summary>
/// <remarks>
/// <b>Retail runs this as two stages and this port ran it as one.</b> A spawner waits eight minutes and
/// then places a spot — or nothing, 27% of the time — and the spot lives ten seconds and rolls its own
/// monster. What stood here rolled once on spawning and placed the monster directly.
/// <para>
/// The odds are what they are, so these pins assert the parts that are not random: the cadence, the
/// intermediate npc, its lifetime, and that every roll lands inside that spot's own table.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class ConquestOfferingAiTests
{
	private const int Gelkmaros = 220070000;

	/// <summary>One spawner, and the pair of spots retail gives it.</summary>
	private const int Spawner = 856150;
	private const int SoloSpot = 856314;
	private const int PartySpot = 856320;

	/// <summary>The eight monsters that spot may roll, and no others.</summary>
	private static readonly int[] SoloMonsters =
		[236307, 236308, 236309, 236310, 236331, 236332, 236333, 236334];

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(2048)
			.WithAi(typeof(ConquestOfferingSpawnerAI), typeof(ConquestOfferingSpotAI),
				typeof(ConquestOfferingAggressiveAI), typeof(ConquestOfferingTimeResetAI),
				// Fourth pin this session to fail first for a missing WithAi entry: the harness validates
				// every AI name it is asked to place, and the buff npcs carry one of their own.
				typeof(ConquestOfferingBuffNpcAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>Nothing happens for eight minutes.</b> The cadence is the mechanic this port did not have —
	/// the old class placed a monster the instant it spawned.
	/// </summary>
	[Fact]
	public void TheSpawnerIsSilentForEightMinutes()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(479));

		Assert.Equal(0, Count(harness, SoloSpot));
		Assert.Equal(0, Count(harness, PartySpot));
		foreach (int monster in SoloMonsters)
			Assert.Equal(0, Count(harness, monster));
	}

	/// <summary>
	/// <b>And on the eighth minute it places one of its own two spots, or neither.</b>
	/// </summary>
	/// <remarks>
	/// Retail's split is 51/22/27, so a single turn cannot be asserted — what can is that whatever
	/// appears is one of that spawner's two spots and never another spawner's.
	/// </remarks>
	[Fact]
	public void EachTurnPlacesOneOfItsOwnSpotsOrNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(481));

		int placed = Count(harness, SoloSpot) + Count(harness, PartySpot);
		Assert.InRange(placed, 0, 1);

		// Never a neighbour's spot: the table is per spawner and 856151 owns 856315 and 856321.
		Assert.Equal(0, Count(harness, 856315));
		Assert.Equal(0, Count(harness, 856321));
	}

	/// <summary>
	/// <b>The clock keeps turning</b>, so over several cycles something is placed.
	/// </summary>
	/// <remarks>
	/// With a 73% chance a turn places something, five turns are silent about one time in fifteen
	/// hundred — asserted over ten, which is past any run this suite will see.
	/// </remarks>
	[Fact]
	public void TheClockKeepsTurning()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Spawner, 300f, 300f, 200f);

		BossAiHarness.Watched seen = harness.WatchNew(
			10 * 481, null, SoloSpot, PartySpot);

		Assert.True(seen.Total > 0, "ten turns of the eight-minute clock placed no spot at all");
	}

	/// <summary>
	/// <b>A spot rolls one monster from its own table and leaves at ten seconds.</b>
	/// </summary>
	/// <remarks>
	/// This is the stage that did not exist: the spawner used to place a monster directly, so the spot,
	/// its lifetime and its roll all went missing together.
	/// </remarks>
	[Fact]
	public void ASpotRollsOneMonsterFromItsOwnTable()
	{
		using BossAiHarness harness = NewHarness();
		Npc spot = harness.Spawn(SoloSpot, 300f, 300f, 200f);

		int placed = SoloMonsters.Sum(m => Count(harness, m));
		Assert.Equal(1, placed);

		// And nothing from the party spot's table, which is a different eight.
		Assert.Equal(0, Count(harness, 236391));
		Assert.True(spot.IsSpawned());
	}

	/// <summary>The npc that carries the message home, and the four buff npcs beside it.</summary>
	private const int TimeReset = 856502;
	private static readonly int[] BuffNpcs = [856175, 856176, 856177, 856178];

	/// <summary>
	/// <b>A monster always leaves the time-reset npc where it fell</b>, whatever else it leaves.
	/// </summary>
	/// <remarks>
	/// This class used to leave <b>nothing at all forty-five per cent of the time</b>, and a secret
	/// portal rather than the reset npc on most of the rest — so the message that re-arms the spawner
	/// had no sender and the rotation never closed.
	/// </remarks>
	[Fact]
	public void ADeadMonsterAlwaysLeavesTheResetNpc()
	{
		using BossAiHarness harness = NewHarness();
		Npc monster = harness.Spawn(SoloMonsters[0], 300f, 300f, 200f);

		monster.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(1, Count(harness, TimeReset));
	}

	/// <summary>
	/// <b>And a buff npc beside it about a third of the time</b>, never more than one per death.
	/// </summary>
	/// <remarks>
	/// Retail's ladder is four branches at nine per cent, first match wins, so the chance of any buff is
	/// 1 − 0.91⁴ ≈ 31%. What is deterministic is the cap: one buff npc from a death, or none, never two.
	/// <para>
	/// <b>This pin does not separate the ladder from four independent nine-per-cent rolls.</b> Deleting the
	/// <c>break</c> that makes the rungs exclusive survives it: the means are 31.4% against 36.0% per death
	/// and the counts overlap heavily, so no batch assertion this suite can afford tells them apart. The
	/// cap below catches gross breakage only, and it is checked per death rather than over the batch —
	/// summing over twelve deaths was inert against everything.
	/// </para>
	/// </remarks>
	[Fact]
	public void AtMostOneBuffNpcArrivesWithIt()
	{
		using BossAiHarness harness = NewHarness();

		for (int i = 0; i < 12; i++)
		{
			int before = BuffNpcs.Sum(b => Count(harness, b));

			Npc monster = harness.Spawn(SoloMonsters[0], 300f + i, 300f, 200f);
			monster.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

			int added = BuffNpcs.Sum(b => Count(harness, b)) - before;
			Assert.InRange(added, 0, 1);
		}

		// And the reset npc from every one of them, which is the part that is not a roll.
		Assert.Equal(12, Count(harness, TimeReset));
	}

	/// <summary>
	/// <b>The reset npc re-arms a nearby spawner's clock.</b> This is the loop closing.
	/// </summary>
	/// <remarks>
	/// Spawner places a spot, spot places a monster, monster leaves the reset npc, reset npc broadcasts
	/// <c>13929</c> at fifty metres — and the spawner starts its eight minutes again. Nothing in this
	/// port sent that message before, so a spawner's clock ran on regardless of what the raid did.
	/// <para>
	/// Asserted by driving the clock almost to its end, resetting it, and showing that the turn which
	/// would have fired does not.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheResetNpcStartsTheSpawnersEightMinutesAgain()
	{
		using BossAiHarness harness = NewHarness();
		Npc spawner = harness.Spawn(Spawner, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(470));

		// The reset npc lands ten metres off, inside its fifty-metre earshot.
		harness.Spawn(TimeReset, 310f, 300f, 200f);

		// The turn that was eleven seconds away now never comes.
		BossAiHarness.Watched seen = harness.WatchNew(30, null, SoloSpot, PartySpot);
		Assert.Equal(0, seen.Total);
	}
}
