using Aion.GameServer.Ai;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The pod assassins Ahserion's reserve assault leader drops, which used to stand still.
/// </summary>
/// <remarks>
/// Retail's <c>Gab1_Sub_Tank_Destroyer</c> spawns two pods and then broadcasts <b>23000</b> to twenty
/// metres naming its current target; <c>Gab1_Sub_Pod_Sum_Vri_As</c> answers that message by taking hate
/// on the named player and attacking. This port spawned the pods and never made the call, so an ambush
/// pair appeared beside their master and waited to be walked into.
/// <para>
/// <b>These pins reach the pods, not the leader, and three mutations survive because of it.</b>
/// <c>AhserionConstructDestroyerAI.HandleSpawned</c> casts its spawn template to
/// <c>AhserionsFlightSpawnTemplate</c>, which the harness does not build, so a leader placed here never
/// runs the branch that spawns pods and calls them -- driving him produces nothing to observe. What
/// survives, and is therefore held only by review: deleting his <c>broadcast_message</c> altogether,
/// widening its twenty-metre range, and the relative <c>z=5</c> the pods spawn at. The message contract
/// below is pinned; his end of it is not.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class AhserionPodAssassinTests
{
	private const int AhserionsFlight = 400030000;
	private const int Destroyer = 297185;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(AhserionsFlight).WithWorldSize(2048)
			.WithAi(typeof(AhserionAggressiveNpcAI), typeof(AhserionConstructDestroyerAI),
				typeof(AggressiveNpcAI), typeof(GeneralNpcAI)).Build();

	/// <summary>
	/// <b>A pod that hears the call goes for the named player.</b>
	/// </summary>
	/// <remarks>
	/// The pod is spawned away from the player and never touched, so nothing but the message can put it
	/// into the fight.
	/// </remarks>
	[Fact]
	public void APodThatHearsTheCallGoesForTheNamedPlayer()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Destroyer, 300f, 300f, 200f);
		Npc pod = harness.Spawn(AhserionAggressiveNpcAI.PodAssassin, 305f, 295f, 205f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(pod, player);

		Assert.Null(pod.GetTarget());

		NpcMessageBus.Broadcast(caller, AhserionAggressiveNpcAI.DestroyerCall, player, 20f);

		Assert.Equal(player, pod.GetTarget());
	}

	/// <summary>
	/// <b>A pod out of earshot does not.</b> Retail's <c>range_as_meter</c> is twenty.
	/// </summary>
	/// <remarks>
	/// Without this the range is unpinned and the call would read the same whether it reached twenty
	/// metres or the whole map.
	/// </remarks>
	[Fact]
	public void APodOutOfEarshotDoesNot()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Destroyer, 300f, 300f, 200f);
		Npc pod = harness.Spawn(AhserionAggressiveNpcAI.PodAssassin, 360f, 300f, 200f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(pod, player);
		BossAiHarness.MakeMutuallyKnown(caller, pod);

		NpcMessageBus.Broadcast(caller, AhserionAggressiveNpcAI.DestroyerCall, player, 20f);

		Assert.Null(pod.GetTarget());
	}

	/// <summary>
	/// <b>And a bystander on the same AI name ignores it.</b>
	/// </summary>
	/// <remarks>
	/// Message numbers are per encounter, and several unrelated npcs run <c>ahserion_aggressive_npc</c>
	/// whose retail patterns say nothing about 23000. A listener keyed on the number alone would have
	/// pulled every one of them onto the caller's target.
	/// </remarks>
	[Fact]
	public void AndABystanderOnTheSameAiNameAnswersItToo()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Destroyer, 300f, 300f, 200f);
		Npc bystander = harness.Spawn(277242, 305f, 295f, 200f);
		Player player = harness.SpawnPlayer(307f, 295f, 200f);
		BossAiHarness.MakeMutuallyKnown(bystander, player);

		NpcMessageBus.Broadcast(caller, AhserionAggressiveNpcAI.DestroyerCall, player, 20f);

		// This pin used to assert the bystander ignored the call, which is what the class did: it
		// answered for one hardcoded npc id. 23000 is the guard call for help, and 277242 answers it
		// in retail like the other fifteen npcs on this AI name.
		Assert.Equal(1, bystander.GetAggroList().GetHate(player));
	}

	/// <summary>
	/// <b>A pod ignores a message that is not the call.</b>
	/// </summary>
	/// <remarks>
	/// The npc-id scope alone is not enough: the destroyer also broadcasts 23002 on its wounded rungs,
	/// and a pod that answered anything addressed to it would take that as an order too.
	/// </remarks>
	[Fact]
	public void APodIgnoresAMessageThatIsNotTheCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc caller = harness.Spawn(Destroyer, 300f, 300f, 200f);
		Npc pod = harness.Spawn(AhserionAggressiveNpcAI.PodAssassin, 305f, 295f, 205f);
		Player player = harness.SpawnPlayer(340f, 300f, 200f);
		BossAiHarness.MakeMutuallyKnown(pod, player);

		NpcMessageBus.Broadcast(caller, 23002, player, 20f);

		Assert.Null(pod.GetTarget());
	}
}
