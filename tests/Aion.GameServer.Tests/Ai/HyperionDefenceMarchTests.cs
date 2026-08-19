using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Hyperion's attackers marching in, which they did not do.
/// </summary>
/// <remarks>
/// Retail hangs a <c>pathname</c> on every spawn action of the eight <c>BIDRuneWP_Main_CallVritra*</c>
/// controllers: the plain callers put their trooper on <c>NPCPathVriAss_Path01</c> and the <c>B</c>
/// callers on <c>NPCPathVriAss_Path02</c>. The trooper appears at its caller's feet and walks a ten-point
/// lane to the objective, and at the end of it runs <c>attack_most_hating</c>.
/// <para>
/// Neither lane was in our data. Both are now <c>300800000_Infinity_Shard.xml</c>. The caller table this
/// port generates from the same patterns carried the spawn coordinates and dropped the pathname, so the
/// troopers were arriving at the objective rather than walking to it.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class HyperionDefenceMarchTests
{
	private const int InfinityShard = 300800000;

	/// <summary>One trooper from each lane, and retail's route ids, written out.</summary>
	private const int LaneOneTrooper = 231096;
	private const int LaneTwoTrooper = 231099;
	private const string LaneOneRoute = "3008000001";
	private const string LaneTwoRoute = "3008000002";

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(InfinityShard).WithWorldSize(256).WithWalkerRoutes()
			.WithAi(typeof(HyperionDefenceAI), typeof(AggressiveNpcAI), typeof(GeneralNpcAI))
			.Build();

	/// <summary>
	/// <b>Each trooper is put on its own lane.</b> The lane is a property of the caller in retail, but no
	/// npc id appears under both, so the trooper can find it from its own id -- which is what lets this
	/// work without the caller handing one over.
	/// </summary>
	[Fact]
	public void EachTrooperMarchesItsOwnLane()
	{
		using BossAiHarness harness = NewHarness();

		Npc one = harness.Spawn(LaneOneTrooper, 150.03f, 145.5f, 125.2f);
		Npc two = harness.Spawn(LaneTwoTrooper, 108.07f, 128.86f, 124.76f);

		Assert.Equal(LaneOneRoute, one.GetMoveController().GetWalkerTemplate()?.GetRouteId());
		Assert.Equal(LaneTwoRoute, two.GetMoveController().GetWalkerTemplate()?.GetRouteId());
	}

	/// <summary>
	/// <b>Each lane is ten points.</b> Written out rather than counted from the walker file, which cannot
	/// notice the file changing.
	/// </summary>
	[Fact]
	public void EachLaneIsTenPoints()
	{
		using BossAiHarness harness = NewHarness();
		Npc one = harness.Spawn(LaneOneTrooper, 150.03f, 145.5f, 125.2f);
		Npc two = harness.Spawn(LaneTwoTrooper, 108.07f, 128.86f, 124.76f);

		Assert.Equal(10, one.GetMoveController().GetWalkerTemplate().GetRouteSteps().Count);
		Assert.Equal(10, two.GetMoveController().GetWalkerTemplate().GetRouteSteps().Count);
	}

	/// <summary>
	/// <b>The march ends at the end of the lane.</b>
	/// </summary>
	/// <remarks>
	/// This is the half of <c>attack_most_hating</c> that matters even with nobody on the hate list.
	/// Neither lane carries a <c>loop_type</c>, so they default to <c>NORMAL</c> and wrap: without the
	/// rung the trooper walks back to the start and marches the lane again, for ever.
	/// </remarks>
	[Fact]
	public void TheMarchStopsAtTheEndOfTheLane()
	{
		using BossAiHarness harness = NewHarness();
		Npc trooper = harness.Spawn(LaneOneTrooper, 150.03f, 145.5f, 125.2f);
		Assert.True(trooper.GetAi().IsInState(AIState.WALKING), "the trooper never started marching");

		ArriveAt(trooper, 9);

		Assert.False(trooper.GetAi().IsInState(AIState.WALKING),
			"the trooper walked past the end of its lane and started the march again");
	}

	/// <summary>
	/// <b>It keeps marching through the points before the end.</b> A trooper that stops at the second
	/// corner never reaches the objective.
	/// </summary>
	[Fact]
	public void ItKeepsMarchingThroughTheEarlierPoints()
	{
		using BossAiHarness harness = NewHarness();
		Npc trooper = harness.Spawn(LaneOneTrooper, 150.03f, 145.5f, 125.2f);

		for (int step = 1; step <= 8; step++)
		{
			ArriveAt(trooper, step);
			Assert.True(trooper.GetAi().IsInState(AIState.WALKING),
				$"the trooper stopped marching at point {step} of ten");
		}
	}

	/// <summary>
	/// <b>Reaching the end engages whoever it has hated on the way in.</b>
	/// </summary>
	[Fact]
	public void ReachingTheEndEngagesTheMostHated()
	{
		using BossAiHarness harness = NewHarness();
		Npc trooper = harness.Spawn(LaneOneTrooper, 150.03f, 145.5f, 125.2f);
		Player defender = harness.SpawnPlayer(152f, 145.5f, 125.2f);
		BossAiHarness.MakeMutuallyKnown(trooper, defender);
		BossAiHarness.Rehate(trooper, defender);

		ArriveAt(trooper, 9);

		Assert.Same(defender,
			trooper.GetAggroList().GetTarget(Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
		Assert.True(trooper.GetAi().IsInState(AIState.FIGHT), "the trooper reached the objective and did nothing");
	}

	/// <summary>
	/// <b>Every trooper a caller can put on the floor has a lane.</b>
	/// </summary>
	/// <remarks>
	/// This pins the generator rather than the AI. <c>VritraCallers.cs</c> is emitted from the same spawn
	/// actions as the coordinates beside it, and the first version of that emitter <b>dropped the
	/// pathname</b> -- which is the whole reason the wave arrived at the objective instead of walking to
	/// it. A placement with no lane is that bug coming back, and it is invisible in every other pin here
	/// because those name their troopers explicitly.
	/// </remarks>
	[Fact]
	public void EveryTrooperACallerSpawnsHasALane()
	{
		List<int> spawnable = VritraCallers.ByCaller.Values
			.SelectMany(options => options)
			.SelectMany(option => option.Spawns)
			.Select(spawn => spawn.NpcId)
			.Distinct()
			.ToList();

		Assert.NotEmpty(spawnable);
		Assert.All(spawnable, npcId => Assert.True(VritraCallers.LaneOf.ContainsKey(npcId),
			$"trooper {npcId} can be spawned but has no lane to march"));
	}

	/// <summary>
	/// <b>The lane on each placement agrees with the lane the trooper looks up.</b> The AI reads
	/// <c>LaneOf</c> by npc id; the placement carries the lane its caller actually named. They are two
	/// views of one fact and nothing keeps them together but the emitter.
	/// </summary>
	[Fact]
	public void ThePlacementLaneAgreesWithTheTrooperLane()
	{
		foreach (VritraCallers.Option[] options in VritraCallers.ByCaller.Values)
		{
			foreach (VritraCallers.Option option in options)
			{
				foreach (VritraCallers.Placement spawn in option.Spawns)
				{
					Assert.NotNull(spawn.Lane);
					Assert.Equal(spawn.Lane, VritraCallers.LaneOf[spawn.NpcId]);
				}
			}
		}
	}

	private static void ArriveAt(Npc npc, int stepIndex)
	{
		npc.GetMoveController().SetRouteStep(
			npc.GetMoveController().GetWalkerTemplate().GetRouteSteps()[stepIndex]);
		npc.GetAi().OnGeneralEvent(AiEventType.MoveArrived);
	}
}
