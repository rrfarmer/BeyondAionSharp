using System.Reflection;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pin for <see cref="AdjutantAnuhartAI"/>'s HP-phase ladder.
/// </summary>
/// <remarks>
/// This is the second shape of retail-fidelity change (see <c>docs/retail-ai-fidelity.md</c>): not a
/// hand-written rotation but a corrected <c>HpPhases</c> threshold list — 70/40/22, from pattern
/// IDTiamat_Anuhart, replacing the 50/25/10 that were derived from watching the fight. A renumbering like
/// that is invisible to <c>dotnet build</c> and to every existing test, which is exactly the gap here.
/// <para>
/// Each phase casts the next of three escalating self-buffs, so the observable is the buff itself landing
/// on the boss. An earlier version watched for <c>AIActions.TargetSelf</c> instead, which was only a valid
/// signal while the harness could not execute skills; once it could, the boss stayed targeted on itself
/// after the first phase and every subsequent tick looked like a phase entry.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AdjutantAnuhartAiTests
{
	private const int AdjutantAnuhart = 219357;

	private static BossAiHarness NewHarness() => BossAiHarness.For()
		.WithAi(typeof(AdjutantAnuhartAI))
		.Build();

	[Fact]
	public void EntersItsThreePhasesAtTheRetailThresholdsAndOnlyOnce()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(AdjutantAnuhart);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		AssertPhasesAt(harness, boss, player, 70, 40, 22);
	}

	[Fact]
	public void RearmsItsPhasesWhenItResets()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(AdjutantAnuhart);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		Attack(harness, boss, player, 20);
		// How many steps a single drop to 20% consumes is not the point here, only that the ladder moved.
		Assert.True(PhaseOf(boss) > 0, "expected the ladder to have advanced before the reset");

		// HandleBackHome resets the ladder, so a re-pull replays the phases from the top.
		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);
		BossAiHarness.SetHpPercent(boss, 100);
		harness.Engage(boss, player);

		AssertPhasesAt(harness, boss, player, 70, 40, 22);
	}

	/// <summary>
	/// Reads the AI's own phase counter.
	/// </summary>
	/// <remarks>
	/// The ladder is what this change altered, so it is what gets observed. Earlier attempts watched a
	/// downstream side effect instead — first the self-retarget, then the self-buff landing — and both
	/// carry their own async lifecycle: the cast resolves through scheduled work, the AI will not start
	/// one while the previous is running, and the three buffs replace each other. Observing them produced
	/// a phase "at 38" that was really the 40 phase's buff arriving two ticks late.
	/// </remarks>
	private static int PhaseOf(Npc boss)
	{
		object ai = boss.GetAi();
		FieldInfo field = ai.GetType().GetField("hpPhases", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("AdjutantAnuhartAI no longer has an hpPhases field");
		var phases = (HpPhases)field.GetValue(ai)!;
		return phases.GetCurrentPhase();
	}

	/// <summary>
	/// One attack against a boss held in combat.
	/// </summary>
	/// <remarks>
	/// Hate is what keeps the boss engaged. Without it the AI finds no most-hated target, goes home
	/// between steps, and <c>HandleBackHome</c> resets the ladder — so the phase advances and is then
	/// wiped before the next assertion, which reads as the ladder never moving at all.
	/// <para>
	/// No clock advance: the phase check is synchronous inside HandleAttack, and letting time pass only
	/// gives the AI more opportunity to disengage.
	/// </para>
	/// </remarks>
	private static void Attack(BossAiHarness harness, Npc boss, Player player, int hpPercent)
	{
		BossAiHarness.SetHpPercent(boss, hpPercent);
		boss.GetAggroList().AddHate(player, 1000);
		boss.GetAi().SetStateIfNot(AIState.FIGHT);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
	}

	/// <summary>
	/// Steps onto each threshold and onto a point clear of it, asserting the ladder advances only on the
	/// threshold itself.
	/// </summary>
	private static void AssertPhasesAt(BossAiHarness harness, Npc boss, Player player, params int[] thresholds)
	{
		int phase = PhaseOf(boss);
		foreach (int threshold in thresholds)
		{
			// SetCurrentHpPercent truncates, so asking for threshold+1 can land ON the threshold and
			// advance the ladder early. Step clear of it and confirm from the read-back.
			Attack(harness, boss, player, threshold + 3);
			Assert.True(boss.GetLifeStats().GetHpPercentage() > threshold,
				$"expected to be above {threshold}, was {boss.GetLifeStats().GetHpPercentage()}");
			Assert.Equal(phase, PhaseOf(boss));

			Attack(harness, boss, player, threshold);
			Assert.Equal(++phase, PhaseOf(boss));
		}
	}
}
