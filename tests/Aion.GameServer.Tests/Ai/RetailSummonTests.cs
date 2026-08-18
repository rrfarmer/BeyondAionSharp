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
	/// <param name="settle">
	/// How long to let the clock run afterwards. Five seconds by default, which is enough for the
	/// scheduled spawn; Queen Serusia needs a shorter one, because her eggs hatch on a fifteen-second
	/// timer and three five-second steps would reach it mid-test.
	/// </param>
	private static void DriveTo(BossAiHarness harness, Npc boss, Player player, int hpPercent,
		TimeSpan? settle = null)
	{
		BossAiHarness.SetHpPercent(boss, hpPercent);
		BossAiHarness.Rehate(boss, player);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
		harness.Clock.Advance(settle ?? TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void QueenSerusiaLaysMoreEggsAsSheWeakens()
	{
		using var harness = BossAiHarness.For(IdianDepths)
			.WithAi(typeof(QueenSerusiaAI), typeof(SerusiaEggAI), typeof(SerusiaLarvaAI),
				typeof(SummonerAI), typeof(AggressiveNpcAI))
			.Build();
		Npc boss = harness.Spawn(QueenSerusia);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int Eggs() => harness.LiveNpcs().Count(n => n.GetNpcId() == SerusiaEgg);
		Assert.Equal(0, Eggs());

		// Retail pattern NeutQueen_N_65_Ah: one egg at 75%, two at 50%, three at 25%. The two-second
		// steps keep the whole run inside the first clutch's fifteen-second incubation, so this pin
		// still measures the laying rather than the hatching -- QueenSerusiaAiTests measures that.
		TimeSpan brief = TimeSpan.FromSeconds(2);
		DriveTo(harness, boss, player, 74, brief);
		Assert.Equal(1, Eggs());

		DriveTo(harness, boss, player, 49, brief);
		Assert.Equal(3, Eggs());

		DriveTo(harness, boss, player, 24, brief);
		Assert.Equal(6, Eggs());
	}

	[Fact]
	public void AshunatalSplitsOffADifferentShadowAtEachStep()
	{
		using var harness = BossAiHarness.For(AturamSkyFortress)
			.WithAi(typeof(AshunatalShadowslipAI), typeof(ExplosionShadowAI), typeof(DecayShadowAI),
				typeof(DisruptionShadowAI), typeof(DisruptionShadowSpawnAI),
				typeof(SummonerAI), typeof(AggressiveNpcAI))
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

		// Each step fires once; the counts do not grow on further hits. Stopping at 45 rather than 40
		// keeps this above his clear-the-board step, which AshunatalShadowslipAiTests measures.
		DriveTo(harness, boss, player, 45);
		Assert.Equal(1, Count(DecayShadows));
		Assert.Equal(2, Count(DisruptionShadows));

		// The explosion shadows are gone rather than still standing, and that is the point of them:
		// twelve seconds after engaging, each one goes off and leaves. See ExplosionShadowAI.
		Assert.Equal(0, Count(ExplosionShadows));
	}
}
