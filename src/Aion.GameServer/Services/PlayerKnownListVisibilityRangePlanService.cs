namespace Aion.GameServer.Services;

public sealed record PlayerKnownListVisibilityRangeObject(
	int PlayerObjectId,
	int WorldId,
	int InstanceId,
	float X,
	float Y,
	float Z,
	float VisibleDistance = WorldVisibility.DefaultVisibleDistance,
	bool IsAwareOfOther = true,
	bool CanSeeOther = true,
	bool KnowsOther = false);

public sealed record PlayerKnownListVisibilityRangePlan(
	int OwnerPlayerObjectId,
	int CandidatePlayerObjectId,
	float DetectionDistance,
	float SquaredDistance,
	bool SameWorldAndInstance,
	bool IsInJavaRange,
	bool UsesStrictLessThanRange,
	bool UsesMaxVisibleDistanceRule,
	bool OwnerCanSeeCandidate,
	bool CandidateCanSeeOwner,
	PlayerKnownListTwoWayOperationPlan OperationPlan,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListVisibilityRangePlanService
{
	private readonly PlayerKnownListTwoWayOperationPlanService _operationPlanService;

	public PlayerKnownListVisibilityRangePlanService(PlayerKnownListTwoWayOperationPlanService? operationPlanService = null)
	{
		_operationPlanService = operationPlanService ?? new PlayerKnownListTwoWayOperationPlanService();
	}

	public PlayerKnownListVisibilityRangePlan Plan(PlayerKnownListVisibilityRangeObject owner, PlayerKnownListVisibilityRangeObject candidate)
	{
		// Java parity breadcrumb: KnownList.isInRange uses max(owner visible
		// distance, candidate visible distance) and PositionUtil.isInRange
		// performs same world/instance plus strict squared-distance comparison.
		var detectionDistance = Math.Max(owner.VisibleDistance, candidate.VisibleDistance);
		var sameWorldAndInstance = owner.WorldId == candidate.WorldId && owner.InstanceId == candidate.InstanceId;
		var squaredDistance = GetSquaredDistance(owner, candidate);
		var isInRange = sameWorldAndInstance && squaredDistance < detectionDistance * detectionDistance;
		var state = new PlayerKnownListTwoWayOperationState(
			owner.PlayerObjectId,
			candidate.PlayerObjectId,
			owner.KnowsOther,
			candidate.KnowsOther,
			owner.IsAwareOfOther,
			candidate.IsAwareOfOther,
			owner.CanSeeOther,
			candidate.CanSeeOther);
		var operationPlan = isInRange
			? _operationPlanService.PlanAdd(state)
			: _operationPlanService.PlanRemove(state);

		return new PlayerKnownListVisibilityRangePlan(
			owner.PlayerObjectId,
			candidate.PlayerObjectId,
			detectionDistance,
			squaredDistance,
			sameWorldAndInstance,
			isInRange,
			UsesStrictLessThanRange: true,
			UsesMaxVisibleDistanceRule: true,
			owner.CanSeeOther,
			candidate.CanSeeOther,
			operationPlan,
			IsJavaRegionKnownListParity: false,
			"com.aionemu.gameserver.world.knownlist.KnownList.isInRange -> Math.max visible distances + PositionUtil.isInRange strict squared comparison",
			IsLive: false);
	}

	private static float GetSquaredDistance(PlayerKnownListVisibilityRangeObject owner, PlayerKnownListVisibilityRangeObject candidate)
	{
		var dx = owner.X - candidate.X;
		var dy = owner.Y - candidate.Y;
		var dz = owner.Z - candidate.Z;
		return dx * dx + dy * dy + dz * dz;
	}
}
