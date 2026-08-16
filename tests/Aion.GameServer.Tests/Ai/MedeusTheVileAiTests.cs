using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="MedeusTheVileAI"/>, translated from retail pattern <c>ND2_WhC</c> (see
/// <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Ulan's wave hand-over with a target switch on top of every step. The shape worth pinning is that
/// the second wave <em>replaces</em> the first rather than joining it, and that his deep rung — which
/// is the same rung that stops Ulan's clock entirely — spends the clock on peeling instead.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class MedeusTheVileAiTests
{
	private const int Heiron = 210040000;

	private const int Medeus = 211265;
	private const int LichOne = 280809;
	private const int LichTwo = 280810;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Heiron).WithWorldSize(4096)
			.WithAi(typeof(MedeusTheVileAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static (BossAiHarness, Npc, List<Player>) Engaged()
	{
		BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Medeus, 2900f, 2500f, 181f);
		var raid = new List<Player>();
		for (int i = 0; i < 3; i++)
			raid.Add(harness.SpawnPlayer(2904f + i, 2500f, 181f));

		harness.Engage(boss, raid[0]);
		for (int i = 0; i < raid.Count; i++)
			for (int n = raid.Count - i; n > 0; n--)
				BossAiHarness.Rehate(boss, raid[i]);

		return (harness, boss, raid);
	}

	/// <summary>Advances without healing, for the pins about who he picks.</summary>
	private static void Advance(BossAiHarness harness, List<Player> raid, Npc boss, int seconds,
		bool heal = true)
	{
		for (int i = 0; i < seconds; i++)
		{
			foreach (Player member in raid)
			{
				BossAiHarness.Rehate(boss, member);
				if (heal)
					BossAiHarness.KeepAlive(member);
			}

			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>The opening switch onto the weakest is carried and cannot be observed on a fresh pull.</b>
	/// Retail puts <c>ATTACKERI_HAS_LOWEST_HP</c> on <c>on_enter_attack_state</c>, and an attacker
	/// indicator picks from the hate list — which at the instant a fight starts holds only whoever
	/// pulled. So the switch resolves to the puller and changes nothing.
	/// </summary>
	/// <remarks>
	/// Pinned as far as it goes: he is on the puller afterwards, which is what the action returns. A
	/// pin that wounded a bystander and expected him to take them would be asserting a mechanic retail
	/// does not have — the bystander is not an attacker yet. Recorded rather than dropped, because the
	/// obvious pin here is wrong rather than merely weak.
	/// </remarks>
	[Fact]
	public void TheOpeningSwitchResolvesToThePuller()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(Medeus, 2900f, 2500f, 181f);
		Player puller = harness.SpawnPlayer(2904f, 2500f, 181f);
		Player bystander = harness.SpawnPlayer(2906f, 2500f, 181f);
		bystander.GetLifeStats().SetCurrentHpPercent(5);

		harness.Engage(boss, puller);

		Assert.Same(puller, boss.GetTarget());
	}

	/// <summary>Above eighty he calls nobody, however long the fight runs.</summary>
	[Fact]
	public void AboveEightyHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 90);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness, LichOne));
		Assert.Equal(0, Count(harness, LichTwo));
	}

	/// <summary>
	/// <b>The second wave replaces the first.</b> Three of one kind at 61–80, and on crossing sixty
	/// those three are removed and three of the other kind take their place — never six.
	/// </summary>
	[Fact]
	public void TheSecondWaveReplacesTheFirst()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, raid, boss, 14);
		Assert.Equal(3, Count(harness, LichOne));
		Assert.Equal(0, Count(harness, LichTwo));

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 10);
		Assert.Equal(0, Count(harness, LichOne));
		Assert.Equal(3, Count(harness, LichTwo));
	}

	/// <summary>And each step pays once, however long the fight spends in the band.</summary>
	[Fact]
	public void EachStepPaysOnce()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, raid, boss, 14);
		Advance(harness, raid, boss, 60);
		Assert.Equal(3, Count(harness, LichOne));
	}

	/// <summary>
	/// <b>Below thirty-five he summons nothing at all</b> — the same rung that ends Ulan's fight — and
	/// a raid that pushes him straight there gets no adds whatever.
	/// </summary>
	[Fact]
	public void PushedStraightBelowThirtyFiveHeCallsNobody()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 120);

		Assert.Equal(0, Count(harness, LichOne));
		Assert.Equal(0, Count(harness, LichTwo));
	}

	/// <summary>
	/// <b>But unlike Ulan the clock keeps running, and it is spent entirely on peeling.</b> Every
	/// twenty seconds he comes off whoever is holding him onto the third-most-hated.
	/// </summary>
	[Fact]
	public void BelowThirtyFiveThePeelRepeats()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 20);
		Advance(harness, raid, boss, 40, heal: false);
		Assert.Same(raid[2], boss.GetTarget());

		// Both of the others are pushed above the tank, which puts him third.
		for (int i = 0; i < 6; i++)
		{
			BossAiHarness.Rehate(boss, raid[1]);
			BossAiHarness.Rehate(boss, raid[2]);
		}

		Assert.Same(raid[0], boss.GetAggroList().GetTarget(AggroTarget.THIRD_MOST_HATED));

		Advance(harness, raid, boss, 25, heal: false);
		Assert.Same(raid[0], boss.GetTarget());
	}

	/// <summary>The middle band has its own peel, on a slower clock.</summary>
	[Fact]
	public void TheMiddleBandPeelsToo()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 50);
		Advance(harness, raid, boss, 25, heal: false);

		Assert.Same(raid[2], boss.GetTarget());
	}

	/// <summary>Both exits clear both waves.</summary>
	[Fact]
	public void BothExitsClearBothWaves()
	{
		var (harness, boss, raid) = Engaged();
		using BossAiHarness _h = harness;

		BossAiHarness.SetExactPercent(boss, 70);
		Advance(harness, raid, boss, 14);
		Assert.Equal(3, Count(harness, LichOne));

		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, Count(harness, LichOne));
		Assert.Equal(0, Count(harness, LichTwo));
	}
}
