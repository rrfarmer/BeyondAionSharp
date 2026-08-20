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
	/// <b>An idle guard hearing the call does not turn to the player.</b> Retail's rung is
	/// <c>add_hate_point 1</c> then <c>attack_most_hating</c> — the plain form, which does <em>not</em>
	/// turn the npc; that is the busy rung's <c>switch_target</c>.
	/// </summary>
	/// <remarks>
	/// <b>The assertion is that it does NOT turn.</b> An earlier version asserted the guard ended up
	/// targeting the player, and that passed only because the class was using the switching helper here
	/// — the very thing that was wrong, so the pin was pinning the defect.
	/// <para>
	/// Asserting the hate instead would be better and does not work: <c>AddHate</c> registers nothing for
	/// an idle npc in this harness, even with the player in its known list, so the plain form leaves no
	/// measurable trace. That is a harness limit rather than a class one — the busy rung below shows the
	/// same helper landing hate once the npc is in a fight — and it is recorded in the fidelity doc.
	/// </para>
	/// </remarks>
	[Fact]
	public void AnIdleGuardHearingTheCallDoesNotTurn()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Answerer, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Answerer, 305f, 300f, 200f);
		Player player = harness.SpawnPlayer(300f, 250f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		// The listener has to be able to see the player. Retail's answerer is within twenty-five metres
		// of the caller and so has the puller in its known list; the aggro list here silently drops hate
		// on a creature the npc has never seen, which reads as a rung that did not fire.
		BossAiHarness.MakeMutuallyKnown(listener, player);
		Assert.Null(listener.GetTarget());

		NpcMessageBus.Broadcast(crier, FortressGuardCallAI.ThisOne, player, 25f);

		Assert.Null(listener.GetTarget());
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
		BossAiHarness.MakeMutuallyKnown(listener, named);
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
