using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for bosses given their retail summons through <c>ai/spawn_helpers.xml</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// These are data-only changes — an <c>ai=</c> flip to <c>summoner</c> plus a summon table — but they
/// are as invisible to the build as any threshold edit, and the adds involved were previously spawned
/// by nothing at all. Two of the eight are pinned here: the one whose waves grow as it weakens, and
/// the one that sends a different add at each step. Both shapes cover the rest.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class RetailSummonTests
{
	private const int IdianDepths = 210090000;
	private const int QueenSerusia = 231003;
	private const int SerusiaEgg = 284273;

	private const int AturamSkyFortress = 300240000;
	private const int AshunatalShadowslip = 217376;
	private const int ExplosionShadows = 217379;
	private const int DecayShadows = 217380;
	private const int DisruptionShadows = 217381;

	/// <summary>Drops HP to a point and lets the summoner's scheduled spawns land.</summary>
	private static void DriveTo(BossAiHarness harness, Npc boss, Player player, int hpPercent)
	{
		BossAiHarness.SetHpPercent(boss, hpPercent);
		BossAiHarness.Rehate(boss, player);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void QueenSerusiaLaysMoreEggsAsSheWeakens()
	{
		using var harness = BossAiHarness.For(IdianDepths)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(QueenSerusia);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int Eggs() => harness.LiveNpcs().Count(n => n.GetNpcId() == SerusiaEgg);
		Assert.Equal(0, Eggs());

		// Retail pattern NeutQueen_N_65_Ah: one egg at 75%, two at 50%, three at 25%.
		DriveTo(harness, boss, player, 74);
		Assert.Equal(1, Eggs());

		DriveTo(harness, boss, player, 49);
		Assert.Equal(3, Eggs());

		DriveTo(harness, boss, player, 24);
		Assert.Equal(6, Eggs());
	}

	[Fact]
	public void AshunatalSplitsOffADifferentShadowAtEachStep()
	{
		using var harness = BossAiHarness.For(AturamSkyFortress)
			.WithAi(typeof(SummonerAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(AshunatalShadowslip);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int Count(int npcId) => harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

		// Retail pattern Station_NinjaNM: decay at 90%, then three explosion at 70%, then two
		// disruption at 50%. None of the three was ever spawned before this.
		DriveTo(harness, boss, player, 89);
		Assert.Equal(1, Count(DecayShadows));
		Assert.Equal(0, Count(ExplosionShadows));

		DriveTo(harness, boss, player, 69);
		Assert.Equal(3, Count(ExplosionShadows));
		Assert.Equal(0, Count(DisruptionShadows));

		DriveTo(harness, boss, player, 49);
		Assert.Equal(2, Count(DisruptionShadows));

		// Each step fires once; the counts do not grow on further hits.
		DriveTo(harness, boss, player, 40);
		Assert.Equal(1, Count(DecayShadows));
		Assert.Equal(3, Count(ExplosionShadows));
		Assert.Equal(2, Count(DisruptionShadows));
	}
}
