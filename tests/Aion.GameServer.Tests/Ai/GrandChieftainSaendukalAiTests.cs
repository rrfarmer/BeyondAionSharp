using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="GrandChieftainSaendukalAI"/>, translated from retail pattern <c>ND2_RnI</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// A peel ladder and nothing else: four bands, four relays, and a different rule in each. The raid is
/// staggered by hate and one member is wounded, so "second most hated", "third most hated" and
/// "closest to dying" are three different people and a pin can tell which rule ran.
/// <para>
/// <b>The turn retail writes into the pull is not pinned, because it cannot do anything.</b> On
/// entering combat the hate list holds only the player who pulled him, so
/// <c>switch_target_by_attacker_indicator RANDOM_ONE</c> has one candidate and picks it — the same
/// no-op this log recorded for Anuhart's enter-attack switch. It is ported because retail wrote it, and
/// left unpinned because there is nothing to assert.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GrandChieftainSaendukalAiTests
{
	private const int Beluslan = 210050000;
	private const int Saendukal = 211040;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(GrandChieftainSaendukalAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>raid[0] most hated, raid[3] least — and raid[3] is the one we wound.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Saendukal, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(304f + i, 300f, 200f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Keeps the hate order and leaves the wounded wounded.</summary>
	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary>Above eighty he holds whoever he opened on, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyHeHoldsWhoeverHeOpenedOn()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 90);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Creature? opened = boss.GetTarget() as Creature;
		Advance(harness, raid, boss, 90);

		Assert.Same(opened, boss.GetTarget());
	}

	/// <summary><b>Crossing eighty takes the weakest, and keeps taking them.</b></summary>
	[Fact]
	public void CrossingEightyTakesTheWeakest()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 70);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);

		Advance(harness, raid, boss, 15);
		Assert.Same(raid[3], boss.GetTarget());

		// It keeps going: heal that one, wound another, and the forty-second relay moves him.
		raid[3].GetLifeStats().SetCurrentHpPercent(100);
		raid[2].GetLifeStats().SetCurrentHpPercent(5);
		Advance(harness, raid, boss, 45);

		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary><b>Crossing fifty takes the second-most-hated instead</b>, on a relay of its own.</summary>
	[Fact]
	public void CrossingFiftyTakesTheSecondMostHated()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Nobody is wounded, so the weakest rule has no opinion and only the hate rule shows.
		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 15);

		Assert.Same(raid[1], boss.GetTarget());
	}

	/// <summary><b>And below twenty, the third.</b></summary>
	[Fact]
	public void BelowTwentyTakesTheThird()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, raid, boss, 15);

		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>
	/// <b>A band's relay falls silent when he leaves the band.</b> Every relay but the last carries its
	/// own health guard as well as its timer, so the weakest-player rule that opened at eighty stops the
	/// moment he drops into a band with a different rule — which is the opposite of what a ladder of
	/// relays looks like it should do.
	/// </summary>
	/// <remarks>
	/// Written first as "the relays stack", which is what the Akairun of Medeus does and what this
	/// pattern's shape suggests. It is not what the guards say, and one <c>is_hp_in_boundary</c> per
	/// relay branch is the whole difference between the two bosses.
	/// <para>
	/// Read in the fifty band rather than the sixty-five one, because sixty-five peels by the same rule
	/// as eighty and a pin cannot tell a silenced relay from a running one when both would do the same
	/// thing.
	/// </para>
	/// </remarks>
	[Fact]
	public void ABandsRelayFallsSilentWhenHeLeavesTheBand()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		// Open the eighty band, whose relay takes the weakest.
		BossAiHarness.SetExactPercent(boss, 70);
		raid[3].GetLifeStats().SetCurrentHpPercent(5);
		Advance(harness, raid, boss, 15);
		Assert.Same(raid[3], boss.GetTarget());

		// Down to the fifty band, which peels by hate instead. Its rung takes the second-most-hated.
		BossAiHarness.SetExactPercent(boss, 40);
		Advance(harness, raid, boss, 15);
		Assert.Same(raid[1], boss.GetTarget());

		// raid[3] is still the weakest by a long way. Forty-five seconds is more than a full turn of
		// the eighty relay's clock, and it does not come round.
		Advance(harness, raid, boss, 45);

		Assert.NotSame(raid[3], boss.GetTarget());
	}

	/// <summary>
	/// <b>The last relay is the exception: it carries no health guard, and it keeps coming.</b> Every
	/// other relay stops the moment he leaves its band; this one has nothing to stop it.
	/// </summary>
	/// <remarks>
	/// Counted as arrivals rather than as visits: the rung that opens the band already turned him once,
	/// so "was he ever on the third" is answered by the opening and says nothing about the relay. What
	/// the relay does is bring him <em>back</em>.
	/// <para>
	/// A version of this pin healed him to full first, to show the relay is not bounded from above
	/// either. It stopped firing — which is the harness rather than the mechanic, since
	/// <c>SetExactPercent</c> is not a heal the fight knows about, so that half of the claim is left to
	/// the pattern and not asserted here.
	/// </para>
	/// </remarks>
	[Fact]
	public void TheLastRelayNeverStops()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 15);
		Advance(harness, raid, boss, 15);
		Assert.Same(raid[2], boss.GetTarget());

		// He is put back on the tank every ten seconds, because a target set by a branch is sticky:
		// the relay re-selects the same player and nothing moves him in between, so without a nudge
		// every firing after the first is invisible. Each arrival back on the third is one firing.
		int arrivals = 0;
		for (int i = 0; i < 200; i++)
		{
			foreach (Player member in raid)
				BossAiHarness.Rehate(boss, member);

			if (i % 10 == 0)
				boss.SetTarget(raid[0]);

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (ReferenceEquals(boss.GetTarget(), raid[2]))
			{
				arrivals++;
				boss.SetTarget(raid[0]);
			}
		}

		Assert.True(arrivals >= 2, $"the relay brought him back {arrivals} times in two hundred seconds");
	}
}
