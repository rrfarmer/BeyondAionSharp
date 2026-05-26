namespace Aion.GameServer.Services;

public sealed record PlayerKnownListPopulationCandidateFact(
	int PlayerObjectId,
	float X,
	float Y,
	float Z,
	float VisibleDistance = WorldVisibility.DefaultVisibleDistance,
	bool OwnerCanSeeCandidate = true,
	bool CandidateCanSeeOwner = true,
	bool OwnerKnowsCandidate = false,
	bool CandidateKnowsOwner = false,
	bool OwnerAwareOfCandidate = true,
	bool CandidateAwareOfOwner = true);

public sealed record PlayerKnownListPopulationPlanRequest(
	PlayerKnownListRegionSnapshot RegionSnapshot,
	PlayerKnownListVisibilityRangeObject Owner,
	IEnumerable<PlayerKnownListPopulationCandidateFact>? CandidateFacts,
	bool ExecuteMembershipMutation = false);

public sealed record PlayerKnownListPopulationCandidatePlan(
	int CandidatePlayerObjectId,
	bool WasPresentInRegionSnapshot,
	PlayerKnownListVisibilityRangePlan? VisibilityRangePlan,
	PlayerKnownListTwoWayMembershipAdapterResult? MembershipAdapterResult,
	string JavaSource);

public sealed record PlayerKnownListPopulationPlan(
	int OwnerPlayerObjectId,
	PlayerKnownListRegionSnapshot RegionSnapshot,
	IReadOnlyList<PlayerKnownListPopulationCandidatePlan> CandidatePlans,
	int MissingCandidateFactCount,
	bool ExecuteMembershipMutation,
	bool MutatedMembership,
	bool ExecutedControllerSideEffects,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListPopulationPlanService
{
	private readonly PlayerKnownListVisibilityRangePlanService _visibilityRangePlanService;
	private readonly PlayerKnownListTwoWayMembershipAdapterService _membershipAdapterService;

	public PlayerKnownListPopulationPlanService(
		PlayerKnownListVisibilityRangePlanService? visibilityRangePlanService = null,
		PlayerKnownListTwoWayMembershipAdapterService? membershipAdapterService = null)
	{
		_visibilityRangePlanService = visibilityRangePlanService ?? new PlayerKnownListVisibilityRangePlanService();
		_membershipAdapterService = membershipAdapterService ?? new PlayerKnownListTwoWayMembershipAdapterService(new PlayerKnownListMembershipService());
	}

	public PlayerKnownListPopulationPlan Plan(PlayerKnownListPopulationPlanRequest request)
	{
		// Java parity breadcrumb: KnownList.update() forgets/updates existing entries,
		// then findVisibleObjects scans MapRegion neighbours. This composition only
		// joins precomputed region candidates with supplied range/canSee facts.
		var factsByCandidateId = (request.CandidateFacts ?? Array.Empty<PlayerKnownListPopulationCandidateFact>())
			.GroupBy(fact => fact.PlayerObjectId)
			.ToDictionary(group => group.Key, group => group.First());
		var candidatePlans = new List<PlayerKnownListPopulationCandidatePlan>();
		var missingFactCount = 0;

		foreach (var candidateId in request.RegionSnapshot.CandidatePlayerObjectIds)
		{
			if (!factsByCandidateId.TryGetValue(candidateId, out var fact))
			{
				missingFactCount++;
				candidatePlans.Add(new PlayerKnownListPopulationCandidatePlan(
					candidateId,
					WasPresentInRegionSnapshot: true,
					VisibilityRangePlan: null,
					MembershipAdapterResult: null,
					"KnownList.findVisibleObjects candidate omitted because no supplied visibility/range fact was available"));
				continue;
			}

			var candidate = new PlayerKnownListVisibilityRangeObject(
				fact.PlayerObjectId,
				request.Owner.WorldId,
				request.Owner.InstanceId,
				fact.X,
				fact.Y,
				fact.Z,
				fact.VisibleDistance,
				fact.CandidateAwareOfOwner,
				fact.CandidateCanSeeOwner,
				fact.CandidateKnowsOwner);
			var owner = request.Owner with
			{
				IsAwareOfOther = fact.OwnerAwareOfCandidate,
				CanSeeOther = fact.OwnerCanSeeCandidate,
				KnowsOther = fact.OwnerKnowsCandidate,
			};
			var visibilityRangePlan = _visibilityRangePlanService.Plan(owner, candidate);
			var membershipResult = _membershipAdapterService.Apply(new PlayerKnownListTwoWayMembershipAdapterRequest(
				visibilityRangePlan.OperationPlan,
				request.ExecuteMembershipMutation));

			candidatePlans.Add(new PlayerKnownListPopulationCandidatePlan(
				candidateId,
				WasPresentInRegionSnapshot: true,
				visibilityRangePlan,
				membershipResult,
				"KnownList.findVisibleObjects candidate composed through range/canSee and two-way membership metadata"));
		}

		return new PlayerKnownListPopulationPlan(
			request.RegionSnapshot.OwnerPlayerObjectId,
			request.RegionSnapshot,
			candidatePlans,
			missingFactCount,
			request.ExecuteMembershipMutation,
			MutatedMembership: candidatePlans.Any(plan => plan.MembershipAdapterResult?.MutatedMembership == true),
			ExecutedControllerSideEffects: false,
			IsJavaRegionKnownListParity: false,
			"Non-live composition of KnownList.findVisibleObjects prerequisites; does not execute Java region storage, controller packets, or world lifecycle",
			IsLive: false);
	}
}
