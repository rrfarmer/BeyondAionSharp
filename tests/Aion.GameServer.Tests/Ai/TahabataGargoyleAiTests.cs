using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Pins for <see cref="TahabataGargoyleAI"/>, translated from retail pattern <c>Dragon_G1Slave</c>
/// (see <c>docs/retail-ai-fidelity.md</c>).
/// </summary>
/// <remarks>
/// It is a fuse, not a fighter. The aionemu class had the explosion right and the timing wrong, and
/// did not hear the ring call at all.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class TahabataGargoyleAiTests
{
	private const int DarkPoeta = 300040000;
	private const int FaithfulSubordinate = 281258;

	/// <summary>"Mana Regression", stack name <c>…_SELFBLOW_NR</c>.</summary>
	private const int SelfBlow = 18219;

	private static (BossAiHarness, Npc, Player) Engaged()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(TahabataGargoyleAI), typeof(AggressiveNpcAI)).Build();
		Npc gargoyle = harness.Spawn(FaithfulSubordinate, 1192f, 1254f, 140f);
		Player player = harness.SpawnPlayer(1194f, 1256f, 140f);
		harness.Engage(gargoyle, player);
		return (harness, gargoyle, player);
	}

	private static void Advance(BossAiHarness harness, Npc npc, Player player, int seconds)
	{
		for (int i = 0; i < seconds; i++)
		{
			BossAiHarness.Rehate(npc, player);
			BossAiHarness.KeepAlive(player);
			harness.Clock.Advance(TimeSpan.FromSeconds(1));
		}
	}

	/// <summary>Ten seconds after something engages it, it blows itself up.</summary>
	[Fact]
	public void ItBlowsUpTenSecondsAfterBeingEngaged()
	{
		var (harness, gargoyle, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, gargoyle, player, 8);
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(gargoyle), c => c.SkillId == SelfBlow);

		Advance(harness, gargoyle, player, 4);

		Assert.Contains(BossAiHarness.DrainQueuedSkills(gargoyle), c => c.SkillId == SelfBlow);
	}

	/// <summary>
	/// And four seconds later it is gone. The aionemu class removed it the instant the cast ended, so
	/// this gap did not exist — a small thing, but it is the difference between a corpse that lingers
	/// and one that vanishes mid-animation.
	/// </summary>
	[Fact]
	public void AndFourSecondsLaterItIsGone()
	{
		var (harness, gargoyle, player) = Engaged();
		using BossAiHarness _h = harness;

		Advance(harness, gargoyle, player, 12);
		Assert.True(gargoyle.IsSpawned(), "it should still be standing between the blast and the exit");

		Advance(harness, gargoyle, player, 3);

		Assert.False(gargoyle.IsSpawned());
	}

	/// <summary>
	/// The ring call sends it away without the explosion — it is being dismissed to make room for the
	/// next wave, not detonated.
	/// </summary>
	[Fact]
	public void TheRingCallSendsItAwayWithoutTheExplosion()
	{
		var (harness, gargoyle, player) = Engaged();
		using BossAiHarness _h = harness;
		BossAiHarness.DrainQueuedSkills(gargoyle);

		var listener = (Aion.GameServer.Ai.INpcMessageListener)gargoyle.GetAi();
		listener.OnNpcMessage(gargoyle, TahabataPyrelordAI.ClearTheOldWave, null);

		Assert.False(gargoyle.IsSpawned());
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(gargoyle), c => c.SkillId == SelfBlow);
	}

	/// <summary>
	/// Left alone it stands indefinitely. Retail arms the fuse in <c>on_enter_attack_state</c>, so one
	/// nobody touches is furniture — which is what makes the ring call the thing that clears them.
	/// </summary>
	[Fact]
	public void OneNobodyTouchesJustStandsThere()
	{
		BossAiHarness harness = BossAiHarness.For(DarkPoeta).WithWorldSize(2048)
			.WithAi(typeof(TahabataGargoyleAI), typeof(AggressiveNpcAI)).Build();
		using BossAiHarness _h = harness;
		Npc gargoyle = harness.Spawn(FaithfulSubordinate, 1192f, 1254f, 140f);

		harness.Clock.Advance(TimeSpan.FromSeconds(60));

		Assert.True(gargoyle.IsSpawned());
		Assert.DoesNotContain(BossAiHarness.DrainQueuedSkills(gargoyle), c => c.SkillId == SelfBlow);
	}
}
