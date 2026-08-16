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
			.WithAi(typeof(KingspinAI), typeof(AggressiveNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged(int raidSize)
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Kingspin, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < raidSize; i++)
		{
			raid.Add(harness.SpawnPlayer(305f + (i * 2), 300f, 200f));
			BossAiHarness.MakeMutuallyKnown(boss, raid[i]);
		}

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

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == Web);

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
		var (harness, boss, raid) = Engaged(6);
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
		var (harness, boss, raid) = Engaged(6);
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
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		// 20..29 is empty: the opening's four are gone at six and this timer's first four at twenty.
		Advance(harness, boss, raid, 25);
		Assert.Equal(1, Count(harness));

		Advance(harness, boss, raid, 6);
		Assert.Equal(4, Count(harness));
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
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 30);
		Assert.Equal(4, Count(harness));

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, boss, raid, 5);
		Assert.Equal(8, Count(harness));

		// And again on the next heartbeat, which a one-shot step would not do.
		Advance(harness, boss, raid, 8);
		Assert.Equal(4, Count(harness));

		Advance(harness, boss, raid, 5);
		Assert.True(Count(harness) >= 4, $"the ladder should have thrown again: {Count(harness)}");
	}

	/// <summary>
	/// Below fifty-one it throws <b>five</b> rather than four — and takes them from the other end of
	/// the hate list, which is the mechanic rather than a detail.
	/// </summary>
	[Fact]
	public void BelowFiftyOneItThrowsFive()
	{
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		Advance(harness, boss, raid, 30);
		Assert.Equal(4, Count(harness));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, boss, raid, 5);

		Assert.Equal(9, Count(harness));
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
		var (harness, boss, raid) = Engaged(6);
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 80);

		// The second timer's four are gone by twenty; nothing replaces them until it fires again.
		Advance(harness, boss, raid, 25);

		Assert.Equal(1, Count(harness));
	}
}
