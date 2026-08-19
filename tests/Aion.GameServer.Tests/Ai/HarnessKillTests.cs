using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Controllers.Observer;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <see cref="BossAiHarness.Kill"/>, which is a death rather than an announcement of one.
/// </summary>
/// <remarks>
/// Raising <c>AiEventType.Died</c> reaches the NPC's own <c>HandleDied</c> and nothing else: no
/// <c>DeathObserver</c>, no friend notice, no respawn scheduling. Every mechanic built on one NPC
/// watching another die is invisible to a pin written that way, and reads as a missing feature rather
/// than an untestable one — a mutation deleting an entire route lookup survived on exactly that.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HarnessKillTests
{
	private const int TiamatStronghold = 300510000;
	private const int GuardingEye = 219390;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(TiamatStronghold).WithWorldSize(2048)
			.WithAi(typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>A killed NPC is dead.</b>
	/// </summary>
	/// <remarks>
	/// Its AI state is <i>not</i> asserted: the move to <c>DIED</c> is made further along the server's
	/// death path than the harness runs, and claiming it here would be claiming more than the helper
	/// does. What the helper is for is the observers, which the next pin covers.
	/// </remarks>
	[Fact]
	public void AKilledNpcIsDead()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(GuardingEye, 900f, 1300f, 397f);
		Player killer = harness.SpawnPlayer(903f, 1300f, 397f);

		BossAiHarness.Kill(npc, killer);

		Assert.True(npc.IsDead(), "Kill left the NPC alive");
	}

	/// <summary>
	/// <b>And a watcher hears about it.</b> This is the whole point of the helper: it is the only way a
	/// pin can drive a mechanic that counts other NPCs dying.
	/// </summary>
	[Fact]
	public void AWatcherIsNotifiedOfTheDeath()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(GuardingEye, 900f, 1300f, 397f);
		Player killer = harness.SpawnPlayer(903f, 1300f, 397f);
		int seen = 0;
		npc.GetObserveController().Attach(new DeathObserver(_ => seen++));

		BossAiHarness.Kill(npc, killer);

		Assert.Equal(1, seen);
	}

	/// <summary>
	/// <b>Raising the event alone does not.</b> The contrast is the reason the helper exists, and without
	/// it somebody will reach for <c>OnGeneralEvent(Died)</c> again and read the silence as a bug.
	/// </summary>
	[Fact]
	public void RaisingTheDiedEventNotifiesNobody()
	{
		using BossAiHarness harness = NewHarness();
		Npc npc = harness.Spawn(GuardingEye, 900f, 1300f, 397f);
		int seen = 0;
		npc.GetObserveController().Attach(new DeathObserver(_ => seen++));

		npc.GetAi().OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.Died);

		Assert.Equal(0, seen);
	}
}
