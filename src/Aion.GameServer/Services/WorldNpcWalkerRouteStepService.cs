using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerRouteStepService
{
	public WorldNpcWalkerRouteStepSelection? FindClosestRouteStep(
		WorldPosition position,
		WorldNpcWalkerRoutePlan routePlan)
	{
		// Java parity: ai/manager/WalkManager.findClosestRouteStep for path walkers without a WalkerGroup.
		if (routePlan.Status != WorldNpcWalkerRouteStatus.Ready || routePlan.RouteSteps.Count == 0)
			return null;

		WorldNpcWalkerRouteStepSelection? closest = null;
		var closestDistance = 0d;
		foreach (var step in routePlan.RouteSteps)
		{
			var distance = GetDistance(position.X, position.Y, position.Z, step.X, step.Y, step.Z);
			if (closest == null || distance < closestDistance)
			{
				closestDistance = distance;
				closest = new WorldNpcWalkerRouteStepSelection(step.StepIndex, step);
			}
		}

		return closest;
	}

	public int GetNextStepIndex(WorldNpcWalkerRoutePlan routePlan, int currentStepIndex)
	{
		// Java parity: controllers/movement/NpcMoveController.isNextRouteStepChosen wraps last routestep to index 0.
		var currentStep = GetStep(routePlan, currentStepIndex);
		return currentStep.IsLastStep ? 0 : currentStep.StepIndex + 1;
	}

	public WorldNpcWalkerRouteStepTarget CreateWalkerTarget(
		WorldNpcWalkerSpawnCandidate walker,
		WorldNpcWalkerRoutePlan routePlan,
		int stepIndex)
	{
		// Java parity: controllers/movement/NpcMoveController.setRouteStep for walkers outside a WalkerGroup.
		var step = GetStep(routePlan, stepIndex);
		return new WorldNpcWalkerRouteStepTarget(
			walker.ObjectId,
			step.StepIndex,
			step.X,
			step.Y,
			step.Z,
			step.RestTime,
			step.IsLastStep,
			IsStopAtStep(routePlan, step));
	}

	public IReadOnlyList<WorldNpcWalkerRouteStepTarget> CreateFormationTargets(
		WorldNpcWalkerFormationResult formation,
		WorldNpcWalkerRoutePlan routePlan,
		int currentStepIndex,
		int nextStepIndex)
	{
		// Java parity: controllers/movement/NpcMoveController.setRouteStep uses WalkerGroup.getLinePoint(currentStep, nextStep, memberShift).
		var currentStep = GetStep(routePlan, currentStepIndex);
		var nextStep = GetStep(routePlan, nextStepIndex);
		var origin = new WalkerPoint(currentStep.X, currentStep.Y);
		var destination = new WalkerPoint(nextStep.X, nextStep.Y);
		return formation.Members
			.Select(member =>
			{
				var point = WorldNpcWalkerFormationService.GetLinePoint(
					origin,
					destination,
					new WorldNpcWalkerShift(member.SagittalShift, member.CoronalShift));
				return new WorldNpcWalkerRouteStepTarget(
					member.ObjectId,
					nextStep.StepIndex,
					point.X,
					point.Y,
					currentStep.Z,
					nextStep.RestTime,
					nextStep.IsLastStep,
					ShouldStop: false);
			})
			.ToArray();
	}

	private static WalkerRouteStepSummary GetStep(WorldNpcWalkerRoutePlan routePlan, int stepIndex)
	{
		if (routePlan.Status != WorldNpcWalkerRouteStatus.Ready)
			throw new ArgumentException("Walker route plan must be ready.", nameof(routePlan));
		if (stepIndex < 0 || stepIndex >= routePlan.RouteSteps.Count)
			throw new ArgumentOutOfRangeException(nameof(stepIndex));

		return routePlan.RouteSteps[stepIndex];
	}

	private static bool IsStopAtStep(WorldNpcWalkerRoutePlan routePlan, WalkerRouteStepSummary step)
	{
		return string.Equals(routePlan.LoopType, "NONE", StringComparison.Ordinal) && step.IsLastStep;
	}

	private static double GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
	{
		var dx = x1 - x2;
		var dy = y1 - y2;
		var dz = z1 - z2;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}
}

public sealed record WorldNpcWalkerRouteStepSelection(
	int StepIndex,
	WalkerRouteStepSummary Step);

public sealed record WorldNpcWalkerRouteStepTarget(
	int ObjectId,
	int StepIndex,
	float X,
	float Y,
	float Z,
	int RestTime,
	bool IsLastStep,
	bool ShouldStop);
