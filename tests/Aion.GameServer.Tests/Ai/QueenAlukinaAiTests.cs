using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="QueenAlukinaAI"/>, corrected against retail pattern
/// <c>IDArena_S8_Named_3</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Two changes, both invisible to every other test: her phase steps moved from 75/50/25 to 80/55/25,
/// and the seven azure blobbles she bursts into on death were spawned nowhere in the server.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class QueenAlukinaAiTests
{
	private const int EmpyreanCrucible = 300300000;
	private const int QueenAlukina = 217590;
	private const int AzureBlobble = 280713;

	private static BossAiHarness NewHarness() => BossAiHarness.For(EmpyreanCrucible)
		.WithAi(typeof(QueenAlukinaAI), typeof(AggressiveNpcAI))
		.Build();

	private static int Count(BossAiHarness harness) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == AzureBlobble);

	[Fact]
	public void StepsAtEightyFiftyFiveAndTwentyFive()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(QueenAlukina);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		// Every step runs the same opening cast, so the observable is which HP it arrives at. Walking a
		// point at a time is what distinguishes 80 from 75 rather than merely showing three steps happen.
		var at = new List<int>();
		int entered = 0;
		for (int hp = 100; hp >= 5; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			BossAiHarness.Rehate(boss, player);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);

			int now = PhasesEntered(boss);
			if (now > entered)
				at.Add(boss.GetLifeStats().GetHpPercentage());
			entered = now;
		}

		Assert.Equal([80, 55, 25], at);
	}

	/// <summary>Reads the ladder's own counter, since every step's casts look alike from outside.</summary>
	private static int PhasesEntered(Npc boss)
	{
		object phases = boss.GetAi().GetType()
			.GetField("hpPhases", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.GetValue(boss.GetAi())!;
		return (int)phases.GetType()
			.GetField("currentPhase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.GetValue(phases)!;
	}

	[Fact]
	public void BurstsIntoSevenAzureBlobblesThatLastThirtySeconds()
	{
		using BossAiHarness harness = NewHarness();
		Npc boss = harness.Spawn(QueenAlukina);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);
		Assert.Equal(0, Count(harness));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(7, Count(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(29));
		Assert.Equal(7, Count(harness));

		harness.Clock.Advance(TimeSpan.FromSeconds(1));
		Assert.Equal(0, Count(harness));
	}
}
