using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcWalkerMovementStateServiceTests
{
	[Fact]
	public void StartSingleRouteWalking_ChoosesClosestRouteStepAsCurrentTarget()
	{
		var service = new WorldNpcWalkerMovementStateService();
		var walker = new WorldNpcWalkerSpawnCandidate(1, 203000, "route-a", "", 0, 0, 0, 0);
		var routePlan = CreatePlan(
			routeSteps:
			[
				new WalkerRouteStepSummary(0, 0, 0, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 0, 200, 1, false),
				new WalkerRouteStepSummary(4, 0, 0, 300, 2, true),
			]);

		var state = service.StartSingleRouteWalking(walker, new WorldPosition(210010000, 3.5f, 0, 0, 0), routePlan);

		Assert.NotNull(state);
		Assert.False(state.IsFormationMember);
		Assert.Equal(2, state.CurrentStepIndex);
		Assert.Equal(2, state.TargetStepIndex);
		Assert.Equal(TimeSpan.Zero, state.RestDelay);
		Assert.Equal(300, state.Target.RestTime);
		Assert.False(state.Target.ShouldStop);
	}

	[Fact]
	public void AdvanceSingleRouteWalking_CarriesRestDelayAndChoosesNextStep()
	{
		var service = new WorldNpcWalkerMovementStateService();
		var walker = new WorldNpcWalkerSpawnCandidate(1, 203000, "route-a", "", 0, 0, 0, 0);
		var routePlan = CreatePlan(
			routeSteps:
			[
				new WalkerRouteStepSummary(0, 0, 0, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 0, 250, 1, false),
				new WalkerRouteStepSummary(20, 0, 0, 350, 2, true),
			]);
		var currentTarget = new WorldNpcWalkerRouteStepService().CreateWalkerTarget(walker, routePlan, stepIndex: 1);
		var currentState = WorldNpcWalkerMovementState.ForTarget(
			walker.ObjectId,
			walker.RouteId,
			walker.VersionRouteId,
			isFormationMember: false,
			currentTarget,
			restDelay: TimeSpan.Zero,
			groupStep: 0,
			sagittalShift: 0,
			coronalShift: 0);

		var advance = service.AdvanceSingleRouteWalking(currentState, walker, routePlan);

		Assert.False(advance.IsStopped);
		Assert.Equal(TimeSpan.FromMilliseconds(250), advance.RestDelay);
		Assert.NotNull(advance.State);
		Assert.Equal(2, advance.State.CurrentStepIndex);
		Assert.Equal(20, advance.State.Target.X);
		Assert.Equal(TimeSpan.FromMilliseconds(250), advance.State.RestDelay);
	}

	[Fact]
	public void AdvanceSingleRouteWalking_StopsAtLoopNoneLastStep()
	{
		var service = new WorldNpcWalkerMovementStateService();
		var walker = new WorldNpcWalkerSpawnCandidate(1, 203000, "route-a", "", 0, 0, 0, 0);
		var routePlan = CreatePlan(loopType: "NONE");
		var currentTarget = new WorldNpcWalkerRouteStepService().CreateWalkerTarget(walker, routePlan, stepIndex: 1);
		var currentState = WorldNpcWalkerMovementState.ForTarget(
			walker.ObjectId,
			walker.RouteId,
			walker.VersionRouteId,
			isFormationMember: false,
			currentTarget,
			restDelay: TimeSpan.Zero,
			groupStep: 0,
			sagittalShift: 0,
			coronalShift: 0);

		var advance = service.AdvanceSingleRouteWalking(currentState, walker, routePlan);

		Assert.True(advance.IsStopped);
		Assert.Null(advance.State);
		Assert.Equal(TimeSpan.Zero, advance.RestDelay);
	}

	[Fact]
	public void CreateFormationRouteState_ProjectsMembersAndUpdatesGroupStep()
	{
		var service = new WorldNpcWalkerMovementStateService();
		var routePlan = CreatePlan();
		var formation = Formation();

		var state = service.CreateFormationRouteState(
			formation,
			routePlan,
			currentStepIndex: 0,
			targetStepIndex: 1);

		Assert.Equal(1, state.GroupStep);
		Assert.Equal(1, state.TargetStepIndex);
		Assert.Equal([1, 2], state.MemberStates.Select(member => member.ObjectId).ToArray());
		Assert.All(state.MemberStates, member =>
		{
			Assert.True(member.IsFormationMember);
			Assert.Equal(1, member.CurrentStepIndex);
			Assert.Equal(1, member.GroupStep);
			Assert.Equal(TimeSpan.Zero, member.RestDelay);
		});
		Assert.Equal(1, state.MemberStates[0].Target.Y, precision: 4);
		Assert.Equal(-1, state.MemberStates[1].Target.Y, precision: 4);
		Assert.Equal(-1, state.MemberStates[0].SagittalShift);
		Assert.Equal(1, state.MemberStates[1].SagittalShift);
	}

	[Fact]
	public void AdvanceFormationRouteWalking_UsesReachedStepRestTimeAndWrapsGroupStep()
	{
		var service = new WorldNpcWalkerMovementStateService();
		var routePlan = CreatePlan(
			routeSteps:
			[
				new WalkerRouteStepSummary(0, 0, 7, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 9, 250, 1, true),
			]);
		var formation = Formation();
		var currentState = service.CreateFormationRouteState(
			formation,
			routePlan,
			currentStepIndex: 0,
			targetStepIndex: 1);

		var advance = service.AdvanceFormationRouteWalking(currentState, formation, routePlan);

		Assert.Equal(TimeSpan.FromMilliseconds(250), advance.RestDelay);
		Assert.Equal(0, advance.State.GroupStep);
		Assert.Equal(0, advance.State.TargetStepIndex);
		Assert.All(advance.State.MemberStates, member => Assert.Equal(0, member.CurrentStepIndex));
	}

	private static WorldNpcWalkerRoutePlan CreatePlan(
		string loopType = "NORMAL",
		IReadOnlyList<WalkerRouteStepSummary>? routeSteps = null)
	{
		return WorldNpcWalkerRoutePlan.Ready(
			"route-a",
			string.Empty,
			2,
			"SQUARE",
			loopType,
			[2],
			routeSteps ??
			[
				new WalkerRouteStepSummary(0, 0, 7, 100, 0, false),
				new WalkerRouteStepSummary(10, 0, 9, 200, 1, true),
			]);
	}

	private static WorldNpcWalkerFormationResult Formation()
	{
		return new WorldNpcWalkerFormationResult(
			WorldNpcWalkerFormationStatus.Ready,
			"route-a",
			string.Empty,
			[
				new WorldNpcWalkerFormationMember(1, 203000, 1, 0, 0, -1, 0),
				new WorldNpcWalkerFormationMember(2, 203000, 2, 0, 0, 1, 0),
			]);
	}
}
