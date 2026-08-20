using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The fortress aggro call, both halves: 23200 (see docs/retail-ai-fidelity.md).
/// </summary>
/// <remarks>
/// <b>Pull a guard and the guards around it come.</b> 986 npcs broadcast this in retail and 282 answer
/// it; forty sent it here and ninety-nine answered. Without the mechanic a raid takes a fortress apart
/// one npc at a time.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class FortressGuardCallTests
{
	private const int Kaldor = 600090000;

	/// <summary>
	/// <c>LDF5a_E5_Guard_Kn_D</c> — one of the 183 guards that could not hear the call.
	/// </summary>
	/// <remarks>
	/// <b>It is an Asmodian guard, so the raid here is Elyos.</b> Both of retail's rungs are guarded on
	/// <c>is_enemy</c> and the aggro list enforces the same thing underneath, so a pin written with the
	/// default Asmodian player measures a guard correctly refusing to attack its own side. That has now
	/// caught this project three times, in three different mechanics.
	/// </remarks>
	private const int Answerer = 231233;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Kaldor).WithWorldSize(4096)
			.WithAi(typeof(FortressGuardCallAI), typeof(FortressGuardAnswerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>An idle guard hearing the call goes for the player named in it.</b> Retail's rung adds one
	/// point of hate and then <c>attack_most_hating</c> — and the second half is what actually starts it
	/// moving. One point on its own is a note the guard never acts on, which is what this class did.
	/// </summary>
	[Fact]
	public void AnIdleGuardHearingTheCallGoesForThePlayer()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Answerer, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Answerer, 305f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		Assert.Null(listener.GetTarget());

		NpcMessageBus.Broadcast(crier, FortressGuardCallAI.ThisOne, player, 25f);

		Assert.Same(player, listener.GetTarget());
	}

	/// <summary>
	/// <b>And a guard already fighting turns to the named player.</b> Retail's busy rung is a
	/// <c>switch_target</c>, not a hate nudge, so the guard leaves what it was on.
	/// </summary>
	[Fact]
	public void AndAGuardAlreadyFightingTurnsToTheNamedPlayer()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Answerer, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Answerer, 305f, 300f, 200f);
		Player busyWith = harness.SpawnPlayer(302f, 250f, 200f, race: Race.ELYOS);
		Player named = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		harness.Engage(listener, busyWith);
		// Far more hate than the call's hundred points, so only an actual switch_target can move it.
		listener.GetAggroList().AddHate(busyWith, 500_000);
		Assert.Same(busyWith, listener.GetTarget());

		NpcMessageBus.Broadcast(crier, FortressGuardCallAI.ThisOne, named, 25f);

		Assert.Same(named, listener.GetTarget());
	}

	/// <summary>
	/// <b>A call naming somebody it cannot fight is ignored.</b> Retail guards both rungs on
	/// <c>is_enemy</c>, and without it a guard would turn on its own side.
	/// </summary>
	[Fact]
	public void ACallNamingSomebodyItCannotFightIsIgnored()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Answerer, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Answerer, 305f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		var seen = new List<int>();

		using (NpcMessageBusProbe probe = NpcMessageBusProbe.Watch(seen))
			NpcMessageBus.Broadcast(crier, FortressGuardCallAI.ThisOne, crier, 25f);

		Assert.Null(listener.GetTarget());
	}
}
