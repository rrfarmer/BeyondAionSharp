using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="KingspinAI"/>, translated from retail pattern <c>IDTP_OctaNm</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// An ELITE boss on plain <c>aggressive</c> with no AI class, and the one NPC his fight is made of
/// reachable by nobody. His ladder is the first translated whose HP branches carry no flag var —
/// regimes rather than steps — so that is what most of these pin.
/// <para>
/// <b>Two timers throw webs, and the pins have to live with both.</b> Timer 0 is the ladder, timer 1
/// throws four on random targets every eighteen seconds from twelve. Every web after the opening
/// lasts eight seconds, so the room is empty at 20-29 and again at 38-47 — those windows are where a
/// count means what it looks like, and the pins use them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KingspinAiTests
{
	private const int LowerUdasTemple = 300160000;
	private const int Kingspin = 215792;
	private const int Web = 281391;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(LowerUdasTemple).WithWorldSize(2048)
			.WithAi(typeof(KingspinAI), typeof(KingspinWebAI), typeof(KingspinCryProbeAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>, Npc) Engaged(int raidSize)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kingspin, 300f, 300f, 200f);
		// Stands with him and tallies every web that speaks. A web thrown at a player fires and vanishes
		// on the tick it lands, so the cries are the only countable record of a throw.
		Npc cries = harness.SpawnWithAi(Kingspin, "kingspin_cry_probe", 301f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, cries);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
		{
			raid.Add(harness.SpawnPlayer(305f + (i * 2), 300f, 200f));
			BossAiHarness.MakeMutuallyKnown(boss, raid[i]);
		}

		harness.Engage(boss, raid[0]);
		foreach (Player member in raid)
			BossAiHarness.Rehate(boss, member);
		return (harness, boss, raid, cries);
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

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Web);

	/// <summary>How many times his throw clock has fired, which does not depend on where anyone stands.</summary>
	private static int Throws(Npc boss) =>
		((Aion.GameServer.Ai.Pattern.PatternAi)boss.GetAi()).TimerFireCount(1);

	/// <summary>How many webs have spoken, read off the probe's counter.</summary>
	private static int Cries(Npc probe)
	{
		var ai = (Aion.GameServer.Ai.Pattern.PatternAi)probe.GetAi();
		for (int n = 0; n <= 99; n++)
			if (ai.CounterEquals(0, n))
				return n;
		return -1;
	}

	/// <summary>Untouched he throws nothing — everything hangs off entering the fight.</summary>
	[Fact]
	public void AnUnpulledKingspinThrowsNothing()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(Kingspin, 300f, 300f, 200f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.Equal(0, Count(harness));
	}

	/// <summary>
	/// He opens by throwing four webs behind himself, at fixed offsets two metres up — the only thing
	/// in the pattern placed relative to the boss rather than on somebody.
	/// </summary>
	[Fact]
	public void HeOpensByThrowingFourBehindHimself()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		Npc[] behind = harness.LiveNpcs()
			.Where(n => n.GetNpcId() == Web && n.GetZ() > boss.GetZ() + 1f).ToArray();

		Assert.Equal(4, behind.Length);
		Assert.All(behind, w => Assert.True(w.GetX() <= boss.GetX() && w.GetY() <= boss.GetY(),
			$"they go behind him, at -15 and -5: {w.GetX():F0}/{w.GetY():F0} against {boss.GetX():F0}/{boss.GetY():F0}"));
	}

	/// <summary>Those four last six seconds, where everything he throws on a player lasts longer.</summary>
	[Fact]
	public void TheFourBehindHimLastSixSeconds()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 5);
		Assert.Equal(4, harness.LiveNpcs()
			.Count(n => n.GetNpcId() == Web && n.GetZ() > boss.GetZ() + 1f));

		Advance(harness, boss, raid, 2);
		Assert.Equal(0, harness.LiveNpcs()
			.Count(n => n.GetNpcId() == Web && n.GetZ() > boss.GetZ() + 1f));
	}

	/// <summary>
	/// The second timer throws four on random targets every eighteen seconds, from twelve — and it
	/// does so whatever his health is.
	/// </summary>
	[Fact]
	public void TheSecondTimerThrowsFourEveryEighteenSeconds()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		// Counted by what the webs say, not by how many stand: each one thrown at a player fires and
		// vanishes on the tick it lands. Retail's second timer throws four every eighteen seconds --
		// and only below fifty-one, which is where he has to be for this to be about the timer.
		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, boss, raid, 25);
		int afterFirst = Cries(cries);
		Assert.True(afterFirst >= 4, $"only {afterFirst} cries by twenty-five seconds");

		Advance(harness, boss, raid, 18);
		Assert.True(Cries(cries) >= afterFirst + 4,
			$"the second throw added {Cries(cries) - afterFirst} cries, not four");
	}

	/// <summary>
	/// Below seventy-one the ladder starts, and it <b>keeps</b> firing: the branch carries no flag var,
	/// so it is a regime rather than a step.
	/// </summary>
	/// <remarks>
	/// Counted as a delta over the second timer's four, in the window where nothing else is standing.
	/// </remarks>
	[Fact]
	public void BelowSeventyOneTheLadderKeepsThrowing()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		// Counted as throws off his own clock, not as cries. A throw puts four webs out and only the
		// ones that land on somebody speak, so the cry tally depends on where the raid is standing --
		// which is what made this pin fail roughly one run in twenty while the code was correct. Same
		// correction as BelowFiftyOneItThrowsFive, which was the sibling of this one.
		//
		// He opens on entering the fight whatever his health; after that, above fifty-one, the clock
		// runs empty. So the first thirty seconds are the opening and nothing else.
		Advance(harness, boss, raid, 30);
		int opened = Throws(boss);

		BossAiHarness.SetExactPercent(boss, 45);
		Advance(harness, boss, raid, 20);
		Assert.True(Throws(boss) > opened, "dropping into the throwing band added no throw");

		// And again on the next heartbeat, which a one-shot step would not do.
		int afterFirst = Throws(boss);
		Advance(harness, boss, raid, 13);
		Assert.True(Throws(boss) > afterFirst,
			$"the ladder should have thrown again: {Throws(boss)} against {afterFirst}");
	}

	/// <summary>
	/// Below fifty-one it throws <b>five</b> rather than four — and takes them from the other end of
	/// the hate list, which is the mechanic rather than a detail.
	/// </summary>
	[Fact]
	public void BelowFiftyOneItThrowsFive()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		// Counted as throws off his own clock, not as cries. This opening assertion was the last cry
		// count left in the file: a throw puts four webs out and only the ones landing on somebody
		// speak, so the tally depends on where the raid stands. Third time this diagnosis has applied,
		// and the third pin to be moved onto the clock the change is actually about.
		Advance(harness, boss, raid, 30);
		int throwsBefore = Throws(boss);
		Assert.True(throwsBefore >= 1, $"only {throwsBefore} throws in the first thirty seconds");

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, raid, 25);

		// Counted as THROWS rather than cries. A throw puts four webs out and only the ones that land on
		// somebody call, so the cry count depends on where the raid is standing and how the picks fall --
		// which made this pin flake. TimerFireCount(1) is the throw clock itself and does not vary.
		Assert.True(Throws(boss) > throwsBefore,
			$"crossing fifty-one added no throw: {Throws(boss)} against {throwsBefore}");
	}

	/// <summary>
	/// Between seventy-one and eighty-six the ladder throws nothing: its top rung is casts only, and
	/// it is the rung that matches there.
	/// </summary>
	/// <remarks>
	/// Measured at eighty rather than at full health, which is what makes it a test of that rung
	/// rather than of no rung at all — above eighty-six nothing matches and any mistake in the top
	/// rung is invisible.
	/// </remarks>
	[Fact]
	public void TheTopRungOfTheLadderThrowsNothing()
	{
		var (harness, boss, raid, cries) = Engaged(6);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);

		// Counted as cries rather than standing webs: a web thrown at a player fires and vanishes on
		// the tick it lands, so the tally is the only record a throw leaves.
		// At eighty he is above the throwing band entirely: retail's throw branch is guarded 0-51, and
		// the two branches above it keep the clock running and throw nothing. So the opening is all a
		// raid at this health ever sees.
		Advance(harness, boss, raid, 6);
		int opening = Cries(cries);

		// THIS IS THE ASSERTION THAT PINS THE GUARD: nineteen more seconds above fifty-one add nothing.
		// Without the 0-51 guard the clock throws every eighteen seconds at any health and this climbs.
		Advance(harness, boss, raid, 19);
		int atEighty = Cries(cries);
		Assert.Equal(opening, atEighty);

		// Drop him into the band and the throws start, which is what the guard is for.
		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, boss, raid, 20);
		Assert.True(Cries(cries) > atEighty,
			$"below fifty-one he should throw: {Cries(cries)} against {atEighty}");
	}

	/// <summary>
	/// <b>A web's cry makes him throw faster, and only inside his two windows.</b> Retail arms timers 3
	/// and 4 from <c>on_message</c> and each re-arms his throw clock at eight seconds against the
	/// eighteen it otherwise gets — so between 30 and 37, and between 45 and 53, the webs come more than
	/// twice as fast.
	/// </summary>
	/// <remarks>
	/// The loop is closed and internal: he throws the webs that call him. This pin drives it from the
	/// outside instead, sending the cry directly, so the acceleration is measured on its own rather than
	/// through however many webs happened to land.
	/// </remarks>
	[Fact]
	public void ACryInsideAWindowShortensHisThrowCycle()
	{
		int Thrown(int percent, bool cry)
		{
			var (harness, boss, raid, cries) = Engaged(4);
			using BossAiHarness _h = harness;

			BossAiHarness.SetExactPercent(boss, percent);
			Advance(harness, boss, raid, 20);
			int before = Cries(cries);

			// At the rate a fight supplies them. He throws four webs every eighteen seconds and only the
			// ones that land on somebody call, so cries arrive in bursts eighteen seconds apart -- not
			// on a metronome. An earlier draft sent one every five seconds, which drove the mechanism
			// far harder than the encounter can and found a degeneracy: each cry restarted the
			// eight-second clock before it could fire, and he threw nothing at all. That is a real
			// property of ArmTimer and not a defect, because the game cannot reach the rate.
			for (int i = 0; i < 3; i++)
			{
				if (cry)
					((Aion.GameServer.Ai.INpcMessageListener)boss.GetAi())
						.OnNpcMessage(boss, KingspinAI.WebCaught, null);
				Advance(harness, boss, raid, 18);
			}
			return Cries(cries) - before;
		}

		int withCry = Thrown(35, cry: true);
		int without = Thrown(35, cry: false);

		// THE CRIES NO LONGER SLOW HIM DOWN, which is the fix; whether they speed him up depends on the
		// watch. Arming now only ever shortens a pending timer, so a cry can bring the clock forward and
		// can never push it back. At retail's own burst rate over this watch the two come out level --
		// sixteen and sixteen -- because a cry that lands just after a throw shortens the next cycle and
		// the one after it lands just as that cycle fires. Under the old semantics the same watch read
		// twelve against sixteen, and the cries were a penalty.
		//
		// Asserted as "not worse" deliberately: that is what has been measured, and claiming a gain this
		// watch does not show would be the over-claim this thread has already made twice.
		Assert.True(withCry >= without,
			$"with the cries he threw {withCry} and without them {without}");
	}

}

/// <summary>Counts Kingspin's webs by what they say, since a web that lands on somebody does not last.</summary>
/// <remarks>
/// A throw aimed at four players produces four blasts, four roots, four cries and <b>zero standing
/// webs</b> — so counting objects measures the debris and counting cries measures the mechanic. The
/// tally lives in counter slot 0 and is read back through <c>CounterEquals</c>.
/// </remarks>
[Aion.GameServer.Ai.AIName("kingspin_cry_probe")]
public class KingspinCryProbeAI : Aion.GameServer.Ai.Pattern.PatternAi
{
	private static readonly Aion.GameServer.Ai.Pattern.AiPattern Pattern_ =
		new Aion.GameServer.Ai.Pattern.AiPattern
		{
			OnMessage = Aion.GameServer.Ai.Pattern.AiPattern.Of(
				Aion.GameServer.Ai.Pattern.AiPattern.Branch(1, "a web spoke",
					[Aion.GameServer.Ai.Pattern.When.Message(KingspinAI.WebCaught)],
					Aion.GameServer.Ai.Pattern.Do.Increment(0, 0, 99))),
		};

	public KingspinCryProbeAI(Npc owner)
		: base(owner)
	{
	}

	protected override Aion.GameServer.Ai.Pattern.AiPattern Pattern => Pattern_;
}
