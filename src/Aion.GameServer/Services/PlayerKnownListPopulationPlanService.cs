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
	bool CandidateAwareOfOwner = true,
	PlayerKnownListOperationSideEffectDirectionFacts? OwnerViewingCandidateSideEffectFacts = null,
	PlayerKnownListOperationSideEffectDirectionFacts? CandidateViewingOwnerSideEffectFacts = null,
	PlayerKnownListPacketConstructionFactPlanRequest? OwnerViewingCandidatePacketFactPlanRequest = null,
	PlayerKnownListPacketConstructionFactPlanRequest? CandidateViewingOwnerPacketFactPlanRequest = null);

public enum PlayerKnownListPopulationPacketConstructionFactPlanDirection
{
	OwnerViewingCandidate,
	CandidateViewingOwner,
}

public sealed record PlayerKnownListPopulationPacketConstructionFactPlanAttachment(
	PlayerKnownListPopulationPacketConstructionFactPlanDirection Direction,
	PlayerKnownListPacketConstructionFactPlanRequest Request,
	PlayerKnownListPacketConstructionFactPlan Plan);

public enum PlayerKnownListPopulationPacketConstructionFactSourceKind
{
	Request,
	GeneratedFactPlan,
	GeneratedFactPlanIgnoredByRequest,
}

public sealed record PlayerKnownListPopulationPacketConstructionFactSource(
	int SubjectPlayerObjectId,
	PlayerKnownListPopulationPacketConstructionFactSourceKind Kind,
	PlayerKnownListPopulationPacketConstructionFactPlanDirection? GeneratedFromDirection,
	string Notes);

public sealed record PlayerKnownListPopulationPlanRequest(
	PlayerKnownListRegionSnapshot RegionSnapshot,
	PlayerKnownListVisibilityRangeObject Owner,
	IEnumerable<PlayerKnownListPopulationCandidateFact>? CandidateFacts,
	bool ExecuteMembershipMutation = false,
	IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>? PacketConstructionFactsByPlayerObjectId = null);

public sealed record PlayerKnownListPopulationCandidatePlan(
	int CandidatePlayerObjectId,
	bool WasPresentInRegionSnapshot,
	PlayerKnownListVisibilityRangePlan? VisibilityRangePlan,
	PlayerKnownListTwoWayMembershipAdapterResult? MembershipAdapterResult,
	string JavaSource,
	PlayerKnownListOperationSideEffectAttachmentPlan? SideEffectAttachmentPlan = null,
	PlayerKnownListOperationSideEffectPacketConstructionPlan? SideEffectPacketConstructionPlan = null,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactPlanAttachment>? SideEffectFactPlans = null,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactSource>? PacketConstructionFactSources = null);

internal sealed record PlayerKnownListPopulationPacketConstructionFactMergeResult(
	IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts> FactsByPlayerObjectId,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactSource> Sources);

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
	bool IsLive,
	bool AttachedControllerSideEffectDescriptors = false,
	bool ConstructedControllerSideEffectPackets = false);

public sealed class PlayerKnownListPopulationPlanService
{
	private readonly PlayerKnownListVisibilityRangePlanService _visibilityRangePlanService;
	private readonly PlayerKnownListTwoWayMembershipAdapterService _membershipAdapterService;
	private readonly PlayerKnownListOperationSideEffectAttachmentService _sideEffectAttachmentService;
	private readonly PlayerKnownListOperationSideEffectPacketConstructionService _sideEffectPacketConstructionService;
	private readonly PlayerKnownListPacketConstructionFactPlanService _factPlanService;

	public PlayerKnownListPopulationPlanService(
		PlayerKnownListVisibilityRangePlanService? visibilityRangePlanService = null,
		PlayerKnownListTwoWayMembershipAdapterService? membershipAdapterService = null,
		PlayerKnownListOperationSideEffectAttachmentService? sideEffectAttachmentService = null,
		PlayerKnownListOperationSideEffectPacketConstructionService? sideEffectPacketConstructionService = null,
		PlayerKnownListPacketConstructionFactPlanService? factPlanService = null)
	{
		_visibilityRangePlanService = visibilityRangePlanService ?? new PlayerKnownListVisibilityRangePlanService();
		_membershipAdapterService = membershipAdapterService ?? new PlayerKnownListTwoWayMembershipAdapterService(new PlayerKnownListMembershipService());
		_sideEffectAttachmentService = sideEffectAttachmentService ?? new PlayerKnownListOperationSideEffectAttachmentService();
		_sideEffectPacketConstructionService = sideEffectPacketConstructionService ?? new PlayerKnownListOperationSideEffectPacketConstructionService();
		_factPlanService = factPlanService ?? new PlayerKnownListPacketConstructionFactPlanService();
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
			var sideEffectAttachmentPlan = _sideEffectAttachmentService.Attach(new PlayerKnownListOperationSideEffectAttachmentRequest(
				visibilityRangePlan.OperationPlan,
				fact.OwnerViewingCandidateSideEffectFacts ?? new PlayerKnownListOperationSideEffectDirectionFacts(),
				fact.CandidateViewingOwnerSideEffectFacts ?? new PlayerKnownListOperationSideEffectDirectionFacts()));
			var sideEffectFactPlans = CreateFactPlans(fact);
			var packetConstructionFacts = CreatePacketConstructionFacts(
				request.PacketConstructionFactsByPlayerObjectId,
				sideEffectFactPlans,
				sideEffectAttachmentPlan);
			var sideEffectPacketConstructionPlan = packetConstructionFacts is null
				? null
				: _sideEffectPacketConstructionService.Construct(new PlayerKnownListOperationSideEffectPacketConstructionRequest(
					sideEffectAttachmentPlan,
					packetConstructionFacts.FactsByPlayerObjectId));

			candidatePlans.Add(new PlayerKnownListPopulationCandidatePlan(
				candidateId,
				WasPresentInRegionSnapshot: true,
				visibilityRangePlan,
				membershipResult,
				"KnownList.findVisibleObjects candidate composed through range/canSee, two-way membership metadata, controller side-effect descriptors, optional fact-plan metadata, and optional packet construction metadata",
				sideEffectAttachmentPlan,
				sideEffectPacketConstructionPlan,
				sideEffectFactPlans,
				packetConstructionFacts?.Sources));
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
			IsLive: false,
			AttachedControllerSideEffectDescriptors: candidatePlans.Any(plan => plan.SideEffectAttachmentPlan?.AttachedSideEffects.Count > 0),
			ConstructedControllerSideEffectPackets: candidatePlans.Any(plan => plan.SideEffectPacketConstructionPlan?.Results.Count > 0));
	}

	private IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactPlanAttachment> CreateFactPlans(
		PlayerKnownListPopulationCandidateFact fact)
	{
		var plans = new List<PlayerKnownListPopulationPacketConstructionFactPlanAttachment>();
		if (fact.OwnerViewingCandidatePacketFactPlanRequest is { } ownerViewingCandidate)
		{
			plans.Add(new PlayerKnownListPopulationPacketConstructionFactPlanAttachment(
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.OwnerViewingCandidate,
				ownerViewingCandidate,
				_factPlanService.Plan(ownerViewingCandidate)));
		}

		if (fact.CandidateViewingOwnerPacketFactPlanRequest is { } candidateViewingOwner)
		{
			plans.Add(new PlayerKnownListPopulationPacketConstructionFactPlanAttachment(
				PlayerKnownListPopulationPacketConstructionFactPlanDirection.CandidateViewingOwner,
				candidateViewingOwner,
				_factPlanService.Plan(candidateViewingOwner)));
		}

		return plans;
	}

	private static PlayerKnownListPopulationPacketConstructionFactMergeResult? CreatePacketConstructionFacts(
		IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>? suppliedFacts,
		IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactPlanAttachment> factPlans,
		PlayerKnownListOperationSideEffectAttachmentPlan attachmentPlan)
	{
		if (suppliedFacts is null && factPlans.Count == 0)
			return null;

		var sources = new List<PlayerKnownListPopulationPacketConstructionFactSource>();
		var attachedSubjectIds = attachmentPlan.AttachedSideEffects
			.Select(attachment => attachment.SideEffectPlan.SubjectPlayerObjectId)
			.ToHashSet();
		var factsByPlayerObjectId = suppliedFacts is null
			? new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>()
			: new Dictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts>(suppliedFacts);
		if (suppliedFacts is not null)
		{
			sources.AddRange(suppliedFacts.Keys.Where(attachedSubjectIds.Contains).Select(subjectPlayerObjectId =>
				new PlayerKnownListPopulationPacketConstructionFactSource(
					subjectPlayerObjectId,
					PlayerKnownListPopulationPacketConstructionFactSourceKind.Request,
					GeneratedFromDirection: null,
					"Packet construction facts were supplied by the population plan request and remain authoritative over generated fact-plan facts.")));
		}

		foreach (var factPlan in factPlans)
		{
			if (factPlan.Plan.Facts is { } facts)
			{
				var sourceKind = factsByPlayerObjectId.TryAdd(facts.SubjectPlayer.ObjectId, facts)
					? PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan
					: PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlanIgnoredByRequest;
				sources.Add(new PlayerKnownListPopulationPacketConstructionFactSource(
					facts.SubjectPlayer.ObjectId,
					sourceKind,
					factPlan.Direction,
					sourceKind == PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan
						? "Packet construction facts were generated from a completed supplied-snapshot fact plan."
						: "Generated fact-plan facts were ignored because request-level packet construction facts for the same subject remain authoritative."));
			}
		}

		return new PlayerKnownListPopulationPacketConstructionFactMergeResult(factsByPlayerObjectId, sources);
	}
}
