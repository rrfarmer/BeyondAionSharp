using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for the Danuar Reliquary frost summons, translated from retail patterns
/// <c>Rune_FrostNmd_TankSum_65_Ae</c> and <c>Rune_FrostNmd_DealSum_65_Ae</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The first bosses in this work whose <em>casts</em> are translated rather than left to npc_skills,
/// so these assert which skill lands on which step — the thing every earlier port had to decline.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class DanuarFrostSummonAiTests
{
	private const int DanuarReliquary = 301110000;
	private const int Novun = 284377;
	private const int Lapilima = 284378;

	private const int TankStrike = 16516;
	private const int InsanityEruption = 17949;
	private const int BoostPhysicalDefense = 17029;
	private const int DealerStrike = 16540;
	private const int PowerAttack = 16984;

	private static (BossAiHarness, Npc, Player) Engaged(int npcId, params Type[] ai)
	{
		BossAiHarness harness = BossAiHarness.For(DanuarReliquary)
			.WithWorldSize(2048)
			.WithAi(ai).Build();
		Npc boss = harness.Spawn(npcId, 300f, 300f, 200f);
		Player player = harness.SpawnPlayer(302f, 302f, 200f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	private static List<int> CastsOver(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		var cast = new List<int>();
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
			cast.AddRange(BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));
		}
		return cast;
	}

	[Fact]
	public void TheTankShieldsItselfBeforeAnyoneTouchesIt()
	{
		using BossAiHarness harness = BossAiHarness.For(DanuarReliquary).WithWorldSize(2048)
			.WithAi(typeof(DanuarFrostTankAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Novun, 300f, 300f, 200f);

		// on_wake_up, so it happens on spawning rather than on the pull.
		BossAiHarness.QueuedCast opener = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
		Assert.Equal(BoostPhysicalDefense, opener.SkillId);
		Assert.Equal(NpcSkillTargetAttribute.ME, opener.Target);
	}

	[Fact]
	public void TheTankOpensWithAStrikeThenWorksItsChain()
	{
		var (harness, boss, player) = Engaged(Novun, typeof(DanuarFrostTankAI), typeof(AggressiveNpcAI));
		using (harness)
		{
			// The pull casts index 0 immediately; the chain then re-shields on its second step.
			Assert.Contains(TankStrike, BossAiHarness.DrainQueuedSkills(boss).Select(c => c.SkillId));

			// The chain's second step re-shields, and like the opener it is cast at itself.
			var chain = new List<BossAiHarness.QueuedCast>();
			for (int i = 0; i < 20; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
				chain.AddRange(BossAiHarness.DrainQueuedSkills(boss));
			}
			Assert.Contains(chain, c => c.SkillId == TankStrike);
			BossAiHarness.QueuedCast shield = Assert.Single(chain, c => c.SkillId == BoostPhysicalDefense);
			Assert.Equal(NpcSkillTargetAttribute.ME, shield.Target);
		}
	}

	[Fact]
	public void TheTankSavesItsEruptionForTheEndOfTheChain()
	{
		var (harness, boss, player) = Engaged(Novun, typeof(DanuarFrostTankAI), typeof(AggressiveNpcAI));
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);

			// Index 1 sits on the last step, so it lands only after the four before it.
			Assert.DoesNotContain(InsanityEruption, CastsOver(harness, boss, player, 30));
			Assert.Contains(InsanityEruption, CastsOver(harness, boss, player, 30));
		}
	}

	[Fact]
	public void TheDealerHasNoShieldAndOnlyTwoSkills()
	{
		var (harness, boss, player) = Engaged(Lapilima, typeof(DanuarFrostDealerAI), typeof(AggressiveNpcAI));
		using (harness)
		{
			var cast = CastsOver(harness, boss, player, 60);
			Assert.Contains(DealerStrike, cast);
			Assert.Contains(PowerAttack, cast);
			Assert.DoesNotContain(BoostPhysicalDefense, cast);
		}
	}

	[Fact]
	public void NeitherRoundsOnSomeoneElseWhileHealthy()
	{
		var (harness, boss, player) = Engaged(Novun, typeof(DanuarFrostTankAI), typeof(AggressiveNpcAI));
		using (harness)
		{
			Player bystander = harness.SpawnPlayer(304f, 304f, 200f);
			BossAiHarness.MakeMutuallyKnown(boss, bystander);
			boss.GetAggroList().AddHate(bystander, 1);

			// Above half health the switch branches never match, however often it is hit. Asserting on
			// the flag they consume rather than on the resulting target: one branch switches to a
			// random attacker and with two players in the room it can legitimately pick the same one.
			for (int i = 0; i < 10; i++)
				boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			Assert.False(WentWild(boss));
		}
	}

	[Fact]
	public void BothRoundOnSomeoneElseOnceBelowHalf()
	{
		foreach ((int npcId, Type ai) in new[] { (Novun, typeof(DanuarFrostTankAI)), (Lapilima, typeof(DanuarFrostDealerAI)) })
		{
			var (harness, boss, player) = Engaged(npcId, ai, typeof(AggressiveNpcAI));
			using (harness)
			{
				Assert.False(WentWild(boss));

				BossAiHarness.SetHpPercent(boss, 40);
				boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

				// on_attacked runs on every hit, so the first one past half health trips it.
				Assert.True(WentWild(boss), $"{npcId} should have rounded on someone below half health");
			}
		}
	}

	/// <summary>Reads the retail flag var the two reaction branches consume.</summary>
	private static bool WentWild(Npc boss)
	{
		var flags = (bool[])boss.GetAi().GetType().BaseType!.BaseType!
			.GetField("flags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.GetValue(boss.GetAi())!;
		return flags[4];
	}
}
