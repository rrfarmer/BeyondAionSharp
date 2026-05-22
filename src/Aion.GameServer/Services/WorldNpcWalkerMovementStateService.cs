using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerMovementStateService
{
	private readonly WorldNpcWalkerRouteStepService _routeSteps;

	public WorldNpcWalkerMovementStateService()
		: this(new WorldNpcWalkerRouteStepService())
	{
	}

	public WorldNpcWalkerMovementStateService(WorldNpcWalkerRouteStepService routeSteps)
	{
		_routeSteps = routeSteps;
	}

	public WorldNpcWalkerMovementState? StartSingleRouteWalking(
		WorldNpcWalkerSpawnCandidate walker,
		WorldPosition currentPosition,
		WorldNpcWalkerRoutePlan routePlan)
	{
		// Java parity: ai/manager/WalkManager.startRouteWalking initializes the walker template at step 0, then uses findClosestRouteStep.
		var closestStep = _routeSteps.FindClosestRouteStep(currentPosition, routePlan);
		if (closestStep == null)
			return null;

		var target = _routeSteps.CreateWalkerTarget(walker, routePlan, closestStep.StepIndex);
		return WorldNpcWalkerMovementState.ForTarget(
			walker.ObjectId,
			walker.RouteId,
			walker.VersionRouteId,
			isFormationMember: false,
			target,
			restDelay: TimeSpan.Zero,
			groupStep: 0,
			sagittalShift: 0,
			coronalShift: 0);
	}

	public WorldNpcWalkerMovementAdvance AdvanceSingleRouteWalking(
		WorldNpcWalkerMovementState currentState,
		WorldNpcWalkerSpawnCandidate walker,
		WorldNpcWalkerRoutePlan routePlan)
	{
		// Java parity: ai/manager/WalkManager.chooseNextRouteStep waits currentStep.restTime, then NpcMoveController.isNextRouteStepChosen sets the next step.
		if (currentState.Target.ShouldStop)
			return WorldNpcWalkerMovementAdvance.Stopped(currentState.ObjectId);

		var restDelay = TimeSpan.FromMilliseconds(currentState.Target.RestTime);
		var nextStepIndex = _routeSteps.GetNextStepIndex(routePlan, currentState.CurrentStepIndex);
		var target = _routeSteps.CreateWalkerTarget(walker, routePlan, nextStepIndex);
		var nextState = WorldNpcWalkerMovementState.ForTarget(
			walker.ObjectId,
			walker.RouteId,
			walker.VersionRouteId,
			isFormationMember: false,
			target,
			restDelay,
			groupStep: 0,
			sagittalShift: 0,
			coronalShift: 0);
		return WorldNpcWalkerMovementAdvance.Moving(nextState, restDelay);
	}

	public WorldNpcWalkerFormationMovementState CreateFormationRouteState(
		WorldNpcWalkerFormationResult formation,
		WorldNpcWalkerRoutePlan routePlan,
		int currentStepIndex,
		int targetStepIndex,
		int previousGroupStep = 0)
	{
		// Java parity: controllers/movement/NpcMoveController.setRouteStep projects each member target and WalkerGroup.setStep updates groupStep.
		var targets = _routeSteps.CreateFormationTargets(formation, routePlan, currentStepIndex, targetStepIndex);
		var membersByObjectId = formation.Members.ToDictionary(member => member.ObjectId);
		var groupStep = targetStepIndex > previousGroupStep || targetStepIndex == 0
			? targetStepIndex
			: previousGroupStep;
		var memberStates = targets
			.Select(target =>
			{
				var member = membersByObjectId[target.ObjectId];
				return WorldNpcWalkerMovementState.ForTarget(
					target.ObjectId,
					formation.RouteId,
					formation.VersionRouteId,
					isFormationMember: true,
					target,
					restDelay: TimeSpan.Zero,
					groupStep,
					member.SagittalShift,
					member.CoronalShift);
			})
			.ToArray();
		return new WorldNpcWalkerFormationMovementState(
			formation.RouteId,
			formation.VersionRouteId,
			currentStepIndex,
			targetStepIndex,
			groupStep,
			memberStates);
	}

	public WorldNpcWalkerFormationMovementAdvance AdvanceFormationRouteWalking(
		WorldNpcWalkerFormationMovementState currentState,
		WorldNpcWalkerFormationResult formation,
		WorldNpcWalkerRoutePlan routePlan)
	{
		// Java parity: WalkerGroup.targetReached eventually funnels arrived members through WalkManager.chooseNextRouteStep.
		var restDelay = TimeSpan.FromMilliseconds(routePlan.RouteSteps[currentState.TargetStepIndex].RestTime);
		var nextStepIndex = _routeSteps.GetNextStepIndex(routePlan, currentState.GroupStep);
		var nextState = CreateFormationRouteState(
			formation,
			routePlan,
			currentState.GroupStep,
			nextStepIndex,
			currentState.GroupStep);
		return new WorldNpcWalkerFormationMovementAdvance(nextState, restDelay);
	}
}

public sealed record WorldNpcWalkerMovementState(
	int ObjectId,
	string RouteId,
	string VersionRouteId,
	bool IsFormationMember,
	int CurrentStepIndex,
	int TargetStepIndex,
	TimeSpan RestDelay,
	int GroupStep,
	float SagittalShift,
	float CoronalShift,
	WorldNpcWalkerRouteStepTarget Target)
{
	public static WorldNpcWalkerMovementState ForTarget(
		int objectId,
		string routeId,
		string versionRouteId,
		bool isFormationMember,
		WorldNpcWalkerRouteStepTarget target,
		TimeSpan restDelay,
		int groupStep,
		float sagittalShift,
		float coronalShift)
	{
		return new WorldNpcWalkerMovementState(
			objectId,
			routeId,
			versionRouteId,
			isFormationMember,
			target.StepIndex,
			target.StepIndex,
			restDelay,
			groupStep,
			sagittalShift,
			coronalShift,
			target);
	}
}

public sealed record WorldNpcWalkerMovementAdvance(
	int ObjectId,
	bool IsStopped,
	TimeSpan RestDelay,
	WorldNpcWalkerMovementState? State)
{
	public static WorldNpcWalkerMovementAdvance Moving(
		WorldNpcWalkerMovementState state,
		TimeSpan restDelay)
	{
		return new WorldNpcWalkerMovementAdvance(state.ObjectId, IsStopped: false, restDelay, state);
	}

	public static WorldNpcWalkerMovementAdvance Stopped(int objectId)
	{
		return new WorldNpcWalkerMovementAdvance(objectId, IsStopped: true, RestDelay: TimeSpan.Zero, State: null);
	}
}

public sealed record WorldNpcWalkerFormationMovementState(
	string RouteId,
	string VersionRouteId,
	int CurrentStepIndex,
	int TargetStepIndex,
	int GroupStep,
	IReadOnlyList<WorldNpcWalkerMovementState> MemberStates);

public sealed record WorldNpcWalkerFormationMovementAdvance(
	WorldNpcWalkerFormationMovementState State,
	TimeSpan RestDelay);
