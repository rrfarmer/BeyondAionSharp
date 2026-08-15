using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for the two Empyrean Crucible preceptors, whose HP steps were corrected against
/// retail patterns IDArena_S7_Named_3 and IDArena_S7_Named_4 (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Neither boss reaches its add spawn synchronously: both run a chain of scheduled steps off the phase,
/// so the virtual clock has to be advanced past the whole chain before the adds exist. That is the point
/// of asserting on the adds rather than on the casts — these two cast through
/// <c>SkillEngine.UseNoAnimationSkill</c> rather than the queue, so the skill engine's execution side
/// would have to be stood up to see them, while the spawns are directly observable.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class PreceptorAiTests
{
	private const int EmpyreanCrucible = 300300000;

	private const int MagePreceptor = 217580;
	private const int MagmaElemental = 282364;
	private const int TempestElemental = 282363;

	private const int PriestPreceptor = 217581;
	private static readonly int[] PriestAdds = { 282366, 282367, 282368 };

	/// <summary>Long enough to drain either boss's scheduled chain (Mage 3s+4.5s, Priest 5s+2s).</summary>
	private static readonly TimeSpan ChainDrain = TimeSpan.FromSeconds(30);

	private static BossAiHarness NewHarness(Type bossAi) => BossAiHarness.For(EmpyreanCrucible)
		.WithAi(bossAi, typeof(AggressiveNpcAI))
		.Build();

	/// <summary>
	/// Walks HP down until the adds first appear, draining the scheduled chain after each step, and
	/// reports the HP that produced them.
	/// </summary>
	/// <remarks>
	/// The walk stops at the first appearance deliberately. Draining a multi-second chain at every HP
	/// point gives the AI enough idle time to disengage; <c>HandleBackHome</c> then restores its HP,
	/// cleans up the adds and re-arms <c>HpPhases</c>, so a longer walk observes the same step firing a
	/// second time. That is an artifact of driving the fight this way, not boss behaviour, and the claim
	/// under test is only where the adds first arrive.
	/// </remarks>
	private static int? FirstAddsAppearAt(BossAiHarness harness, Npc boss, Player player, Func<int> countAdds)
	{
		int seen = countAdds();
		for (int hp = 100; hp >= 3; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			BossAiHarness.KeepAlive(player);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			int observed = boss.GetLifeStats().GetHpPercentage();
			harness.Clock.Advance(ChainDrain);
			if (countAdds() > seen)
				return observed;
		}
		return null;
	}

	[Fact]
	public void MagePreceptorSummonsBothElementalsAtSixty()
	{
		using var harness = NewHarness(typeof(MagePreceptorAI));
		Npc boss = harness.Spawn(MagePreceptor);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int? at = FirstAddsAppearAt(harness, boss, player,
			() => harness.LiveNpcs().Count(n => n.GetNpcId() is MagmaElemental or TempestElemental));

		// Retail's first step is at 60, where we had an invented 75/50 split. Both elementals come
		// together, which is what makes this one step rather than two.
		Assert.Equal(60, at);
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == MagmaElemental));
		Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == TempestElemental));
	}

	[Fact(Skip = "One layer deeper than the harness currently reaches. The invulnerable stand-in and the " +
		"geo fixes cleared this boss's first two blockers (player death into PvpService, then " +
		"GeoService lookups from movement and StaggerEffect), and his sibling above now passes in the " +
		"full suite because of them. What remains is an NRE inside Effect.ApplyEffect while applying " +
		"skill 8217 to the stand-in — a sub-effect touching player state the harness does not build. " +
		"His threshold is recorded in docs/retail-ai-fidelity.md; unskip when the stand-in is a more " +
		"complete Player.")]
	public void PriestPreceptorSummonsHisTrioAtThirty()
	{
		using var harness = NewHarness(typeof(PriestPreceptorAI));
		Npc boss = harness.Spawn(PriestPreceptor);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		int? at = FirstAddsAppearAt(harness, boss, player,
			() => harness.LiveNpcs().Count(n => PriestAdds.Contains(n.GetNpcId())));

		// Retail's add wave is at 30, not the 25 we had.
		Assert.Equal(30, at);
		Assert.All(PriestAdds, id => Assert.Equal(1, harness.LiveNpcs().Count(n => n.GetNpcId() == id)));
	}
}
