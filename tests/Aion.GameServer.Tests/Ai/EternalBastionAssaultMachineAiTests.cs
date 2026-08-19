using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The Eternal Bastion assault pods and siege towers, which had twenty npcs and no pins at all.
/// </summary>
/// <remarks>
/// Retail runs both through condition spawn variables rather than pattern spawns: a pod's
/// <c>IDF5_TD_Wave_Pod_01..12</c> and a tower's <c>IDF5_TD_Wave4_Boss1..5</c> set a wave variable to 1
/// when the thing becomes active and to 2 when it dies, and the escorts come from the instance's own
/// condition spawns while it reads 1. This port says the same thing in its own terms — the AI places the
/// first escort group and the instance's wave clock repeats it while the pod or tower lives — so what
/// these pins assert is the composition of the group and the route it walks, which is the part retail's
/// tables and this class have to agree on.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class EternalBastionAssaultMachineAiTests
{
	private const int EternalBastion = 300540000;

	/// <summary>A pod that drops ambusher-and-gunners, and the strike npc every pod places.</summary>
	private const int PodOne = 231140;
	private const int Ambusher = 231106;
	private const int Gunner = 231108;
	private const int PodStrike = 284686;

	/// <summary>A drop pod, whose escort and strike npc are a different pair entirely.</summary>
	private const int DropPod = 231141;
	private const int Scout = 231105;
	private const int Trooper = 231107;
	private const int TbmStrike = 284699;

	/// <summary>The first siege tower, which places an escort and no strike npc at all.</summary>
	private const int TowerOne = 231143;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(EternalBastion).WithWorldSize(2048).WithWalkerRoutes()
			.WithAi(typeof(EternalBastionAssaultMachineAI), typeof(EternalBastionAssaulterNpcAI),
				typeof(UseSkillAndDieAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	private static int Count(BossAiHarness harness, int npcId) =>
		harness.LiveNpcs().Count(n => n.GetNpcId() == npcId);

	/// <summary>
	/// <b>A pod places its strike npc at once and its escort three seconds later.</b>
	/// </summary>
	/// <remarks>
	/// The strike npc is retail's <c>on_wake_up</c> spawn and it is immediate; the escort is this port's
	/// stand-in for the condition spawn the wave variable turns on, and it is delayed.
	/// </remarks>
	[Fact]
	public void APodPlacesItsStrikeNpcAtOnceAndItsEscortAfter()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(PodOne, 635.426f, 243.117f, 238.075f, 33);

		Assert.Equal(1, Count(harness, PodStrike));
		Assert.Equal(0, Count(harness, Ambusher));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		// One ambusher and two gunners, which is the shape every assault pod's group takes.
		Assert.Equal(1, Count(harness, Ambusher));
		Assert.Equal(2, Count(harness, Gunner));
	}

	/// <summary>
	/// <b>And every one of that escort walks a route.</b>
	/// </summary>
	/// <remarks>
	/// The escorts arrive down a corridor rather than standing where they are dropped. An escort with no
	/// walker id stands at the pod's feet for the rest of the instance, which is the failure this pins.
	/// </remarks>
	[Fact]
	public void EveryEscortWalksItsRoute()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(PodOne, 635.426f, 243.117f, 238.075f, 33);
		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		foreach (Npc escort in harness.LiveNpcs().Where(
			n => n.GetNpcId() == Ambusher || n.GetNpcId() == Gunner))
		{
			Assert.Equal("NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01", escort.GetSpawn().GetWalkerId());
		}
	}

	/// <summary>
	/// <b>A drop pod is a different npc with a different pair.</b>
	/// </summary>
	/// <remarks>
	/// Retail splits the twelve pods eight-to-four between <c>BIDF5_TD_AssultPodStrike</c> and
	/// <c>BIDF5_TD_AssultTBMStrike</c>, and the escorts split the same way — scouts and troopers rather
	/// than ambushers and gunners. Reading that split backwards would put the wrong four npcs in a room.
	/// </remarks>
	[Fact]
	public void ADropPodPlacesTheOtherStrikeNpcAndTheOtherEscort()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(DropPod, 666.361f, 294.435f, 225.698f, 20);

		// Counted before the clock moves: the strike npc runs useSkillAndDie, so it casts once and is
		// gone inside the three seconds the escort takes to arrive.
		Assert.Equal(1, Count(harness, TbmStrike));
		Assert.Equal(0, Count(harness, PodStrike));

		harness.Clock.Advance(TimeSpan.FromSeconds(4));
		Assert.Equal(1, Count(harness, Scout));
		Assert.Equal(2, Count(harness, Trooper));
		Assert.Equal(0, Count(harness, Ambusher));
	}

	/// <summary>
	/// <b>A siege tower places an escort and no strike npc.</b>
	/// </summary>
	/// <remarks>
	/// Retail's <c>IDF5_TD_Wave4_Boss1..5</c> carry no spawn at all — only the wave variable — so the
	/// strike npc that every pod drops is exactly what a tower must not have.
	/// </remarks>
	[Fact]
	public void ASiegeTowerPlacesAnEscortAndNoStrikeNpc()
	{
		using BossAiHarness harness = NewHarness();
		harness.Spawn(TowerOne, 613.231f, 262.163f, 227.255f, 3);
		harness.Clock.Advance(TimeSpan.FromSeconds(4));

		Assert.Equal(0, Count(harness, PodStrike));
		Assert.Equal(0, Count(harness, TbmStrike));
		Assert.Equal(2, Count(harness, Scout));
		Assert.Equal(1, Count(harness, Trooper));
	}
}
