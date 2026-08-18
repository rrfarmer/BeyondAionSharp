using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="DarkbladeOvanukaAI"/> and <see cref="ShebanBladesmanAI"/>, translated from
/// retail patterns <c>IDVritra_Base_Drakan_As_IU_Nmd</c> and <c>…_Sum2</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two phases of this fight are reached only by wandering, which we cannot do, so what is pinned is
/// the turning and the one order that survives: at eighty percent he names a player and his
/// bladesmen take them.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DarkbladeOvanukaAiTests
{
	private const int SauroSupplyBase = 301220000;

	private const int Ovanuka = 233256;
	private const int Bladesman = 233286;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(SauroSupplyBase).WithWorldSize(2048)
			.WithAi(typeof(DarkbladeOvanukaAI), typeof(ShebanBladesmanAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary>Four players in a line, so a random turn is visible as a change of target.</summary>
	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ovanuka, 300f, 300f, 200f);
		var raid = new List<Player>();
		for (int i = 0; i < 4; i++)
			raid.Add(harness.SpawnPlayer(304f + (i * 3f), 300f, 200f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds)
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

	/// <summary>Above eighty he calls nobody, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyHeCallsNobody()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ovanuka, 300f, 300f, 200f);
		Npc bladesman = harness.Spawn(Bladesman, 315f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, bladesman);
		BossAiHarness.MakeMutuallyKnown(bladesman, player);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 90);

		var only = new List<Player> { player };
		Advance(harness, only, boss, 60);

		Assert.Null(bladesman.GetTarget());
	}

	/// <summary>
	/// <b>Crossing eighty he names the player he is fighting and his bladesmen take them.</b> The
	/// player stands forty metres from the bladesman, so only the order could have delivered it.
	/// </summary>
	[Fact]
	public void CrossingEightyCallsTheBladesmen()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ovanuka, 300f, 300f, 200f);
		Npc bladesman = harness.Spawn(Bladesman, 315f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, bladesman);
		BossAiHarness.MakeMutuallyKnown(bladesman, player);
		// And one fifty metres the other way, outside retail's thirty-metre order.
		Npc distant = harness.Spawn(Bladesman, 250f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, distant);
		BossAiHarness.MakeMutuallyKnown(distant, player);

		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 70);

		var only = new List<Player> { player };
		Advance(harness, only, boss, 8);

		Assert.Same(player, bladesman.GetTarget());
		Assert.Null(distant.GetTarget());
	}

	/// <summary>And once: the flag var means the second crossing tick calls nobody again.</summary>
	[Fact]
	public void AndHeCallsThemOnce()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Ovanuka, 300f, 300f, 200f);
		Npc first = harness.Spawn(Bladesman, 315f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 260f, 200f);
		BossAiHarness.MakeMutuallyKnown(boss, first);
		BossAiHarness.MakeMutuallyKnown(first, player);
		harness.Engage(boss, player);
		BossAiHarness.SetExactPercent(boss, 70);

		var only = new List<Player> { player };
		Advance(harness, only, boss, 8);

		// A bladesman that arrives after the crossing hears nothing, however long it waits.
		Npc latecomer = harness.Spawn(Bladesman, 316f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(latecomer, player);
		Advance(harness, only, boss, 60);

		Assert.Null(latecomer.GetTarget());
	}

	/// <summary>
	/// <b>Above eighty he turns onto a random attacker every thirty seconds.</b> His first step is
	/// twenty-three seconds in, and the loop brings it round again.
	/// </summary>
	[Fact]
	public void AboveEightyHeTurnsOnSomebodyEveryThirtySeconds()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.SetExactPercent(boss, 90);

		var seen = new HashSet<int>();
		for (int i = 0; i < 200; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			if (boss.GetTarget() is Player held)
				seen.Add(held.GetObjectId());
		}

		// Six turns over two hundred seconds; all six landing on the same player is one in a thousand.
		Assert.True(seen.Count > 1, "he never came off the first player");
	}

	/// <summary>
	/// <b>Below thirty-five the shorter loop turns him twice more, and then the chain runs out.</b>
	/// </summary>
	/// <remarks>
	/// Two mistakes were made before this settled. Sampling from the eighty-percent crossing counts
	/// <em>that</em> turn and the drift back to the most-hated afterwards, so the mutations that delete
	/// the last phase survived it; the fix is to let the crossing settle first and take the target he is
	/// left on as the baseline. And a decoy with one hate point measures nothing, because a random turn
	/// onto it is undone by the next think before a one-second sample can see it — the turn is real and
	/// the observation is not.
	/// <para>
	/// Five separate fights with an early exit, because the last phase has only two turns left in it and
	/// each is a one-in-four chance of landing back where it started.
	/// </para>
	/// </remarks>
	[Fact]
	public void BelowThirtyFiveTheChainStillTurnsHim()
	{
		bool turned = false;

		for (int run = 0; run < 5 && !turned; run++)
		{
			var (harness, boss, raid) = Engaged();
			using BossAiHarness _h = harness;

			// The eighty crossing, and long enough after it for his hate list to have the last word.
			BossAiHarness.SetExactPercent(boss, 70);
			Advance(harness, raid, boss, 30);
			VisibleObject? settled = boss.GetTarget();

			BossAiHarness.SetExactPercent(boss, 30);
			for (int i = 0; i < 60 && !turned; i++)
			{
				foreach (Player member in raid)
				{
					BossAiHarness.Rehate(boss, member);
					BossAiHarness.KeepAlive(member);
				}

				harness.Clock.Advance(TimeSpan.FromSeconds(1));
				turned |= !ReferenceEquals(boss.GetTarget(), settled);
			}
		}

		Assert.True(turned, "the last phase never took him off the player he was left on");
	}
}
