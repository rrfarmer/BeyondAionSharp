using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="VirhanaTheGreatAI"/>, translated from retail pattern
/// <c>IDCTH_Boss_StatueDrakan</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The two halves of his fight used to be crossed: nothing happened for seventy seconds but the
/// opening buff, then Earthly Retribution ran on the ten-second chain where Blade of Lunacy belongs,
/// twelve times, and the whole wait began again. So what is pinned here is which skill is on which
/// timer, and that the fifteen-second chain exists at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class VirhanaTheGreatAiTests
{
	private const int BeshmundirTemple = 300170000;
	private const int Virhana = 216165;

	private const int BladeOfLunacy = 18602;
	private const int EarthlyRetribution = 18897;
	private const int SealOfReflection = 19121;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(BeshmundirTemple)
			.WithWorldSize(2048)
			.WithAi(typeof(VirhanaTheGreatAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(Virhana, 558f, 1369f, 224.8f);
		Player player = harness.SpawnPlayer(560f, 1372f, 224.8f);
		harness.Engage(boss, player);
		return (harness, boss, player);
	}

	/// <summary>Runs the clock in short steps, topping hate up, and collects everything he casts.</summary>
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
	public void OpensWithSealOfReflectionOnHimself()
	{
		var (harness, boss, _) = Engaged();
		using (harness)
		{
			BossAiHarness.QueuedCast opener = Assert.Single(BossAiHarness.DrainQueuedSkills(boss));
			Assert.Equal(SealOfReflection, opener.SkillId);
			Assert.Equal(NpcSkillTargetAttribute.ME, opener.Target);
		}
	}

	[Fact]
	public void SweepsWithEarthlyRetributionLongBeforeTheSeventySecondMark()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);

			// The old version did nothing at all in this window. The first sweep is due at 12s.
			var cast = CastsOver(harness, boss, player, 13);

			Assert.Contains(EarthlyRetribution, cast);
			Assert.DoesNotContain(BladeOfLunacy, cast);
		}
	}

	[Fact]
	public void HoldsBladeOfLunacyUntilSeventySecondsThenPairsIt()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);

			var early = CastsOver(harness, boss, player, 69);
			Assert.DoesNotContain(BladeOfLunacy, early);

			// The branch that opens the chain casts it twice: at the tank and at a second player.
			var opening = CastsOver(harness, boss, player, 1);
			Assert.Equal(2, opening.Count(id => id == BladeOfLunacy));
		}
	}

	[Fact]
	public void KeepsTheBladeChainRunningRatherThanStoppingAfterTwelve()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);
			CastsOver(harness, boss, player, 70);

			// Ours stopped after twelve casts and restarted the seventy-second wait. Retail's chain
			// re-arms itself and never stops, so a long window keeps producing them at 10s intervals.
			var later = CastsOver(harness, boss, player, 200);
			Assert.Equal(20, later.Count(id => id == BladeOfLunacy));
		}
	}

	[Fact]
	public void CastsEarthlyRetributionAtItselfAndBladeOfLunacyAtPlayers()
	{
		var (harness, boss, player) = Engaged();
		using (harness)
		{
			BossAiHarness.DrainQueuedSkills(boss);

			var targets = new Dictionary<int, HashSet<NpcSkillTargetAttribute>>();
			for (int i = 0; i < 120; i++)
			{
				BossAiHarness.Rehate(boss, player);
				harness.Clock.Advance(TimeSpan.FromSeconds(1));
				foreach (var c in BossAiHarness.DrainQueuedSkills(boss))
				{
					if (!targets.TryGetValue(c.SkillId, out var seen))
						targets[c.SkillId] = seen = new HashSet<NpcSkillTargetAttribute>();
					seen.Add(c.Target);
				}
			}

			// The sweep is centred on him and the blade is not: that distinction is what the old
			// version had backwards.
			Assert.Equal([NpcSkillTargetAttribute.ME], targets[EarthlyRetribution]);
			Assert.DoesNotContain(NpcSkillTargetAttribute.ME, targets[BladeOfLunacy]);
		}
	}
}
