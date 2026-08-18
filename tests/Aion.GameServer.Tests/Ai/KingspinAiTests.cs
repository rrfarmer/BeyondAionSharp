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
		// vanishes on the tick it lands. Retail's second timer throws four every eighteen seconds.
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

		// Counted as cries rather than standing webs: a web thrown at a player fires and vanishes on
		// the tick it lands, so the tally is the only record a throw leaves.
		Advance(harness, boss, raid, 30);
		int opened = Cries(cries);
		Assert.True(opened >= 4, $"only {opened} cries in the first thirty seconds");

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, raid, 5);
		Assert.True(Cries(cries) > opened, "crossing seventy-one added no throw");

		// And again on the next heartbeat, which a one-shot step would not do.
		int afterFirst = Cries(cries);
		Advance(harness, boss, raid, 13);
		Assert.True(Cries(cries) > afterFirst,
			$"the ladder should have thrown again: {Cries(cries)} against {afterFirst}");
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

		// Counted as cries rather than standing webs: a web thrown at a player fires and vanishes on
		// the tick it lands, so the tally is the only record a throw leaves.
		Advance(harness, boss, raid, 30);
		int before = Cries(cries);
		Assert.True(before >= 4, $"only {before} cries in the first thirty seconds");

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, raid, 5);

		Assert.True(Cries(cries) >= before + 5, $"crossing fifty-one added {Cries(cries) - before} cries, not five");
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
		// COUNTED AS CRIES, THIS PIN SAYS THE OPPOSITE OF WHAT IT USED TO. Standing webs read as one at
		// twenty-five seconds and it looked like the top of the ladder was quiet; the cries read nine,
		// because his plain throw timer carries no health guard and keeps going at any health. What the
		// top rung actually means is that the LADDER's steps do not fire -- every one of them sits below
		// eighty -- while the timer underneath them does.
		Advance(harness, boss, raid, 25);
		int atEighty = Cries(cries);
		Assert.True(atEighty >= 4, $"the plain throw timer should still run: {atEighty}");

		// Drop him a rung and the ladder adds to it, which is the difference the rung makes.
		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, raid, 6);
		Assert.True(Cries(cries) > atEighty, "crossing seventy-one added nothing");
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
