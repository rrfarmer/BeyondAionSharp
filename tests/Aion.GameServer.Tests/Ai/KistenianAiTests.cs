using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Behavioural pins for <see cref="KistenianAI"/>, translated from retail pattern
/// <c>DGuard_Kistenian</c> (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// Only the two adds that need nothing else are pinned. The third, the fire spirits, arrives on a
/// message his own companion pattern sends and is not implemented.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class KistenianAiTests
{
	private const int Beluslan = 220040000;
	private const int Kistenian = 204753;
	private const int FlameOfKistenian = 295179;
	private const int DespawnEffect = 295181;

	private static (BossAiHarness, Npc, Player) Spawned()
	{
		BossAiHarness harness = BossAiHarness.For(Beluslan).WithWorldSize(2048)
			.WithAi(typeof(KistenianAI), typeof(AggressiveNpcAI)).Build();
		Npc boss = harness.Spawn(Kistenian, 300f, 300f, 200f);

		// Out of aggro range: he pulls anyone standing beside him, and the idle pin needs him not to.
		Player player = harness.SpawnPlayer(700f, 700f, 200f);
		return (harness, boss, player);
	}

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	[Fact]
	public void NoFlameStandsBeforeHeIsEngaged()
	{
		var (harness, boss, _) = Spawned();
		using BossAiHarness _h = harness;

		harness.Clock.Advance(TimeSpan.FromSeconds(30));

		Assert.Equal(0, Count(harness, FlameOfKistenian));
	}

	[Fact]
	public void EngagingLightsAFlameBesideHim()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);

		harness.Engage(boss, player);

		Npc flame = Assert.Single(harness.LiveNpcs().Where(n => n.GetNpcId() == FlameOfKistenian));
		Assert.InRange(flame.GetX(), boss.GetX() - 4f, boss.GetX() + 4f);
	}

	/// <summary>One flame for the fight — every later swing comes through the same handler.</summary>
	[Fact]
	public void BeingHitAgainDoesNotLightASecondFlame()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);

		for (int i = 0; i < 20; i++)
		{
			boss.GetAi().OnCreatureEvent(AiEventType.Attack, player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.Equal(1, Count(harness, FlameOfKistenian));
	}

	/// <summary>Retail's leave-attack branch clears what he called up; the flame has no lifetime.</summary>
	[Fact]
	public void LeavingTheFightPutsTheFlameOut()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		Assert.Equal(1, Count(harness, FlameOfKistenian));

		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);

		Assert.Equal(0, Count(harness, FlameOfKistenian));
	}

	[Fact]
	public void DyingPutsTheFlameOutAndLeavesTheEffect()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		Assert.Equal(1, Count(harness, FlameOfKistenian));

		boss.GetAi().OnGeneralEvent(AiEventType.Died);

		Assert.Equal(0, Count(harness, FlameOfKistenian));
		Assert.Equal(1, Count(harness, DespawnEffect));
	}

	/// <summary>Six seconds, then it is gone — it is an effect, not a reinforcement.</summary>
	[Fact]
	public void TheDeathEffectLastsSixSeconds()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		boss.GetAi().OnGeneralEvent(AiEventType.Died);
		Assert.Equal(1, Count(harness, DespawnEffect));

		harness.Clock.Advance(TimeSpan.FromSeconds(5));
		Assert.Equal(1, Count(harness, DespawnEffect));

		harness.Clock.Advance(TimeSpan.FromSeconds(2));
		Assert.Equal(0, Count(harness, DespawnEffect));
	}

	/// <summary>A fresh pull lights a new flame — the latch resets with the fight.</summary>
	[Fact]
	public void AFreshPullLightsAnotherFlame()
	{
		var (harness, boss, player) = Spawned();
		using BossAiHarness _h = harness;
		BossAiHarness.MakeMutuallyKnown(boss, player);
		harness.Engage(boss, player);
		boss.GetAi().OnGeneralEvent(AiEventType.BackHome);
		Assert.Equal(0, Count(harness, FlameOfKistenian));

		harness.Engage(boss, player);

		Assert.Equal(1, Count(harness, FlameOfKistenian));
	}
}
