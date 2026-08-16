using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for the corrections made to <see cref="TahabataPyrelordAI"/> against retail pattern
/// <c>Dragon_G1</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// The class predates this work and keeps its own skill-hook mechanic; what is pinned here is only
/// what was corrected — when the enrage starts, how long it runs, and the primal dragon he leaves.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TahabataPyrelordAiTests
{
	private const int DarkPoeta = 300040000;
	private const int Tahabata = 215280;
	private const int PrimalDragon = 281265;

	/// <summary>The enrage he casts when the ten minutes run out.</summary>
	private const int YouAreUnworthy = 19679;

	private static (BossAiHarness, Npc, Player) Spawned()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(TahabataPyrelordAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Tahabata, 1180f, 1235f, 143f);

		// Well out of his aggro range: he is an aggressive NPC and will pull anyone standing next to
		// him, which is exactly what the idle test needs not to happen.
		Player player = harness.SpawnPlayer(1600f, 1600f, 143f);
		return (harness, boss, player);
	}

	private static void Advance(BossAiHarness harness, Npc boss, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary>
	/// Retail arms the enrage in <c>on_enter_attack_state</c>. It used to start on spawn, so a group
	/// that spent four minutes reaching him arrived with one minute left.
	/// </summary>
	[Fact]
	public void TheEnrageDoesNotStartUntilHeIsEngaged()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;

		// Eleven minutes standing idle: nothing should be counting.
		harness.Clock.Advance(TimeSpan.FromSeconds(660));
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
		Assert.True(boss.IsSpawned(), "he should not have wiped the room while unengaged");
	}

	/// <summary>Ten minutes from the pull, not five.</summary>
	[Fact]
	public void TheEnrageComesAtTenMinutesFromThePull()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		Advance(harness, boss, player, 560);
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);

		Advance(harness, boss, player, 60);
		Assert.Contains(BossAiHarness.DrainQueuedSkills(boss), c => c.SkillId == YouAreUnworthy);
	}

	/// <summary>
	/// The fuse is lit once, on the first swing. Every later hit arrives through the same handler, and
	/// scheduling is not cancelling — so without a latch each hit would book <i>another</i> enrage,
	/// and the room would be wiped once per swing from the ten-minute mark onwards rather than once.
	/// </summary>
	[Fact]
	public void BeingHitDoesNotPostponeTheEnrage()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		// Hit him steadily for the whole ten minutes.
		for (int i = 0; i < 620; i++)
		{
			BossAiHarness.Rehate(boss, player);
			BossAiHarness.KeepAlive(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(1, BossAiHarness.DrainQueuedSkills(boss).Count(c => c.SkillId == YouAreUnworthy));
	}

	/// <summary>Spawned by nothing anywhere before this.</summary>
	[Fact]
	public void HeLeavesAPrimalDragonWhereHeFalls()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		Assert.Equal(0, harness.LiveNpcs().Count(n => n.GetNpcId() == PrimalDragon));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Npc dragon = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == PrimalDragon));
		Assert.Equal(boss.GetX(), dragon.GetX());
		Assert.Equal(boss.GetY(), dragon.GetY());
	}
}
