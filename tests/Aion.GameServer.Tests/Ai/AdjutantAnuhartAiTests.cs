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
/// The phase handler's own effect (a self-buff through <c>AIActions.UseSkill</c>) needs the skill engine's
/// execution side, which the harness deliberately does not stand up. What it does leave observable is the
/// <c>AIActions.TargetSelf</c> that every branch of the handler performs first, so "the boss retargeted
/// itself" is a faithful one-bit signal that a phase was entered — and the tick it happens on pins the
/// threshold.
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

		var enteredAt = new List<int>();
		// Walk HP down one point at a time; the phase check runs on every attack, as it does in the real fight.
		// SetCurrentHpPercent truncates, so the observed percentage is read back rather than assumed.
		for (int hp = 100; hp >= 5; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
			if (ReferenceEquals(boss.GetTarget(), boss))
				enteredAt.Add(boss.GetLifeStats().GetHpPercentage());
		}

		Assert.Equal([70, 40, 22], enteredAt);
	}

	[Fact]
	public void RearmsItsPhasesWhenItResets()
	{
		using var harness = NewHarness();
		Npc boss = harness.Spawn(AdjutantAnuhart);
		Player player = harness.SpawnPlayer();
		harness.Engage(boss, player);

		BossAiHarness.SetHpPercent(boss, 20);
		boss.SetTarget(player);
		boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
		Assert.Same(boss, boss.GetTarget()); // first phase entered

		// HandleBackHome resets the ladder, so a re-pull replays the phases from the top.
		boss.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.BackHome);
		BossAiHarness.SetHpPercent(boss, 100);
		harness.Engage(boss, player);

		var enteredAt = new List<int>();
		for (int hp = 100; hp >= 5; hp--)
		{
			BossAiHarness.SetHpPercent(boss, hp);
			boss.SetTarget(player);
			boss.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, player);
			if (ReferenceEquals(boss.GetTarget(), boss))
				enteredAt.Add(boss.GetLifeStats().GetHpPercentage());
		}

		Assert.Equal([70, 40, 22], enteredAt);
	}
}
