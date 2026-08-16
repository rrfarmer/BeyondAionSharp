using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for Gelkmaros Padmarashka's rockfall, translated from retail pattern <c>DF4_Dramata</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// What this replaces: a single ring of forty rocks around a fixed point at 10% health, with no
/// lifetime. Retail drops them on the players, capped, for twelve seconds, from five separate sources
/// on their own timers — so the pins are about <em>when</em> and <em>how many</em>, which is the whole
/// difference between the two.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PadmarashkaRockfallTests
{
	private const int Gelkmaros = 220070000;
	private const int Padmarashka = 216580;

	/// <summary>The heavy rock of the two low-health bursts, and the B rock of every earlier chain.</summary>
	private const int Rock = 281936;
	private const int RockB = 282140;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Gelkmaros).WithWorldSize(4096)
			.WithAi(typeof(GelkmarosPadmarashkaAI), typeof(AggressiveNpcAI), typeof(RockSlideAI))
			.Build();

	/// <summary>Her four shield NPCs sit around 2906..2963 / 859..878, so she is spawned where she stands.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Padmarashka, 2940.20f, 851.29f, 35.89f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
			raid.Add(harness.SpawnPlayer(2945f + i, 855f, 35.89f));
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

	/// <summary>Nobody has pulled her, so nothing falls — the whole chain hangs off the fight.</summary>
	[Fact]
	public void AnUnpulledPadmarashkaDropsNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Padmarashka, 2940.20f, 851.29f, 35.89f);

		harness.Clock.Advance(TimeSpan.FromSeconds(120));

		Assert.Equal(0, Count(harness, Rock));
		Assert.Equal(0, Count(harness, RockB));
	}

	/// <summary>
	/// The first rocks land on the <b>third</b> heartbeat tick, fifteen seconds in — not immediately.
	/// </summary>
	/// <remarks>
	/// Timer 0 re-arms every five seconds and its branches are one-shot steps, so the fight walks down
	/// them: tick one opens the long-cycle chains, tick two is a step that is all casts, tick three is
	/// the opening rockfall. Translating the cast-only step matters for exactly this reason — drop it
	/// and the rocks arrive five seconds early.
	/// </remarks>
	[Fact]
	public void TheOpeningRockfallLandsOnTheThirdTick()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 14);
		Assert.Equal(0, Count(harness, RockB));

		Advance(harness, boss, raid, 1);
		Assert.Equal(3, Count(harness, RockB));
	}

	/// <summary>Three, not one per player — retail's <c>total_set_to_spawn</c> is 3 on that step.</summary>
	[Fact]
	public void TheOpeningRockfallIsCappedAtThree()
	{
		var (harness, boss, raid) = Engaged(9);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 15);

		Assert.Equal(3, Count(harness, RockB));
	}

	/// <summary>Each rock lasts twelve seconds, so a fall clears before the next chain is due.</summary>
	[Fact]
	public void ARockLastsTwelveSeconds()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 15);
		Assert.Equal(3, Count(harness, RockB));

		Advance(harness, boss, raid, 11);
		Assert.Equal(3, Count(harness, RockB));

		Advance(harness, boss, raid, 2);
		Assert.Equal(0, Count(harness, RockB));
	}

	/// <summary>
	/// The timer-6 chain: four B rocks fifty seconds in — five for the first heartbeat tick, forty-five
	/// for the timer it arms — and again every ninety after that, because timers 6 and 7 hand off to
	/// each other at forty-five apiece.
	/// </summary>
	[Fact]
	public void TheLongChainDropsFourEveryNinetySeconds()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 49);
		Assert.Equal(0, Count(harness, RockB));

		Advance(harness, boss, raid, 1);
		Assert.Equal(4, Count(harness, RockB));

		// Gone by 62, and nothing until the pair comes back around at 140.
		Advance(harness, boss, raid, 88);
		Assert.Equal(0, Count(harness, RockB));

		Advance(harness, boss, raid, 2);
		Assert.Equal(4, Count(harness, RockB));
	}

	/// <summary>
	/// Below ten percent she drops <b>fifteen</b> at once: three draws of five, which is what makes the
	/// last of the fight different rather than just faster.
	/// </summary>
	[Fact]
	public void BelowTenPercentFifteenHeavyRocksFallAtOnce()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 20);
		Assert.Equal(0, Count(harness, Rock));

		BossAiHarness.SetExactPercent(boss, 9);
		Advance(harness, boss, raid, 6);

		Assert.Equal(15, Count(harness, Rock));
	}

	/// <summary>And once, not on every tick below the threshold — the step carries a flag var.</summary>
	[Fact]
	public void TheTenPercentBurstHappensOnlyOnce()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 9);
		Advance(harness, boss, raid, 20);

		// The burst fell and expired; what is left is the timer-2 chain it opened, which is four.
		Assert.True(Count(harness, Rock) <= 4,
			$"a repeating burst would leave far more than one chain's worth: {Count(harness, Rock)}");
	}

	/// <summary>
	/// Crossing ten percent also opens a chain that keeps dropping four heavy rocks: thirty seconds
	/// after the burst, then every ninety, from timers 2 and 3 handing off at forty-five apiece.
	/// </summary>
	/// <remarks>
	/// Worth its own pin because the burst hides it — a pin that only counts rocks shortly after the
	/// threshold sees fifteen either way, and the chain is what makes the last of the fight relentless
	/// rather than one bad moment.
	/// </remarks>
	[Fact]
	public void TenPercentAlsoOpensARepeatingHeavyChain()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 9);
		Advance(harness, boss, raid, 6);
		Assert.Equal(15, Count(harness, Rock));

		// Twelve-second lifetime: the burst is gone well before the chain is due.
		Advance(harness, boss, raid, 12);
		Assert.Equal(0, Count(harness, Rock));

		Advance(harness, boss, raid, 18);
		Assert.Equal(4, Count(harness, Rock));

		// And it comes back: timer 3 at forty-five, timer 2 forty-five after that, so ninety between
		// falls. Without the hand-off it would drop four once and never again.
		Advance(harness, boss, raid, 88);
		Assert.Equal(0, Count(harness, Rock));

		Advance(harness, boss, raid, 2);
		Assert.Equal(4, Count(harness, Rock));
	}

	/// <summary>Below five percent it happens again — a second burst, from its own step.</summary>
	[Fact]
	public void BelowFivePercentASecondBurstFalls()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 9);
		Advance(harness, boss, raid, 20);
		int afterTen = Count(harness, Rock);

		BossAiHarness.SetExactPercent(boss, 4);
		Advance(harness, boss, raid, 6);

		Assert.True(Count(harness, Rock) >= afterTen + 15,
			$"the five-percent step should add fifteen of its own: {afterTen} then {Count(harness, Rock)}");
	}

	/// <summary>A rock engages whoever it landed on rather than waiting to be walked into.</summary>
	/// <remarks>
	/// The hate is the observable rather than the state: the B rock is <c>aggressive</c> and lands on
	/// top of its target, so it would engage on its own within the tick. Natural aggression is worth one
	/// point and retail's <c>hatepoints_to_add</c> of one goes on top, so two is the fingerprint.
	/// </remarks>
	[Fact]
	public void ARockArrivesAlreadyFighting()
	{
		var (harness, boss, raid) = Engaged(1);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 16);

		Npc rock = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == RockB));
		Assert.Equal(AIState.FIGHT, rock.GetAi().GetState());
		Assert.Same(raid[0], rock.GetTarget());
		Assert.True(rock.GetAggroList().GetHate(raid[0]) >= 2,
			$"one point is what it would aggro on its own; the flag adds retail's own on top: "
			+ $"{rock.GetAggroList().GetHate(raid[0])}");
	}

	/// <summary>Killing her clears the field — retail's <c>on_die</c> despawns the rock group.</summary>
	/// <remarks>
	/// This is the half of the clear-up that is actually load-bearing. The Java-parity
	/// <c>HandleBackHome</c> already deletes both rock ids by hand, so the pattern's
	/// <c>on_leave_attack_state</c> branch is redundant with it and cannot be pinned; nothing clears
	/// them on death but this.
	/// </remarks>
	[Fact]
	public void DyingClearsTheRocks()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 15);
		Assert.Equal(3, Count(harness, RockB));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, RockB));
	}

	/// <summary>Losing her quarry clears the field — retail's <c>on_leave_attack_state</c>.</summary>
	[Fact]
	public void LeavingTheFightClearsTheRocks()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 15);
		Assert.Equal(3, Count(harness, RockB));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);

		Assert.Equal(0, Count(harness, RockB));
	}
}
