namespace Aion.GameServer.Services;

public enum PlayerKnownListPopulationPacketConstructionDiagnosticStatus
{
	NoPacketConstructionMetadata,
	Complete,
	Partial,
	Blocked,
}

public sealed record PlayerKnownListPopulationFactPlanDiagnostic(
	int CandidatePlayerObjectId,
	int CandidateOrder,
	PlayerKnownListPopulationPacketConstructionFactPlanDirection Direction,
	PlayerKnownListPacketConstructionFactPlanStatus Status,
	IReadOnlyList<PlayerKnownListPacketConstructionFactBlocker> Blockers,
	string JavaSource);

public sealed record PlayerKnownListPopulationPacketConstructionResultDiagnostic(
	int CandidatePlayerObjectId,
	int CandidateOrder,
	PlayerKnownListTwoWayOperationStepKind OperationStepKind,
	PlayerKnownListOperationSideEffectPacketConstructionResultStatus Status,
	PlayerKnownListPlayerSideEffectPacketConstructionStatus? PlayerPacketConstructionStatus,
	int ConstructedPacketCount,
	int BlockedPacketCount,
	string Notes);

public sealed record PlayerKnownListPopulationPacketConstructionCandidateDiagnostic(
	int CandidatePlayerObjectId,
	int CandidateOrder,
	PlayerKnownListPopulationPacketConstructionDiagnosticStatus Status,
	IReadOnlyList<PlayerKnownListPopulationFactPlanDiagnostic> FactPlans,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionFactSource> PacketConstructionFactSources,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionResultDiagnostic> PacketConstructionResults,
	bool HasPacketConstructionPlan);

public sealed record PlayerKnownListPopulationPacketConstructionDiagnosticPlan(
	int OwnerPlayerObjectId,
	IReadOnlyList<PlayerKnownListPopulationPacketConstructionCandidateDiagnostic> CandidateDiagnostics,
	int CandidateCount,
	int MissingCandidateFactCount,
	int CandidatesWithRangePlanCount,
	int AttachedSideEffectDescriptorCandidateCount,
	int AttachedSideEffectCount,
	int CandidateFactPlanCount,
	int CompletedFactPlanCount,
	int BlockedFactPlanCount,
	IReadOnlyDictionary<PlayerKnownListPacketConstructionFactBlocker, int> FactPlanBlockerCountsByKind,
	int PacketConstructionFactSourceCount,
	int RequestPacketConstructionFactSourceCount,
	int GeneratedPacketConstructionFactSourceCount,
	int IgnoredGeneratedPacketConstructionFactSourceCount,
	IReadOnlyDictionary<PlayerKnownListPopulationPacketConstructionFactSourceKind, int> PacketConstructionFactSourceCountsByKind,
	int PacketConstructionPlanCount,
	int ConstructedPacketConstructionPlanCount,
	int PartiallyConstructedPacketConstructionPlanCount,
	int NoAttachedSideEffectPacketConstructionPlanCount,
	int PacketConstructionResultCount,
	int ConstructedPacketConstructionResultCount,
	int PartiallyConstructedPacketConstructionResultCount,
	int BlockedMissingSubjectFactsResultCount,
	int ConstructedPlayerPacketCount,
	int BlockedPlayerPacketCount,
	IReadOnlyDictionary<PlayerKnownListPlayerSideEffectPacketConstructionResultStatus, int> PlayerPacketResultStatusCountsByKind,
	IReadOnlyDictionary<PlayerKnownListPlayerSideEffectKind, int> ConstructedPacketCountsByKind,
	PlayerKnownListPopulationPacketConstructionDiagnosticStatus Status,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource);

public sealed class PlayerKnownListPopulationPacketConstructionDiagnosticService
{
	public PlayerKnownListPopulationPacketConstructionDiagnosticPlan Summarize(
		PlayerKnownListPopulationPlan populationPlan)
	{
		// Java parity breadcrumb: KnownList.findVisibleObjects preserves scan order,
		// then KnownList.updateVisibility/del produce directional PlayerController
		// callbacks. This projection summarizes existing non-live metadata only.
		var candidateDiagnostics = populationPlan.CandidatePlans
			.Select((candidatePlan, index) => CreateCandidateDiagnostic(candidatePlan, index))
			.ToArray();
		var factPlans = candidateDiagnostics.SelectMany(candidate => candidate.FactPlans).ToArray();
		var factSources = candidateDiagnostics.SelectMany(candidate => candidate.PacketConstructionFactSources).ToArray();
		var packetConstructionPlans = populationPlan.CandidatePlans
			.Select(candidate => candidate.SideEffectPacketConstructionPlan)
			.Where(plan => plan is not null)
			.Select(plan => plan!)
			.ToArray();
		var playerPacketResults = packetConstructionPlans
			.SelectMany(plan => plan.Results)
			.SelectMany(result => result.PacketConstructionPlan?.Results ?? Array.Empty<PlayerKnownListPlayerSideEffectPacketConstructionResult>())
			.ToArray();
		var resultDiagnostics = candidateDiagnostics
			.SelectMany(candidate => candidate.PacketConstructionResults)
			.ToArray();
		var constructedPlayerPacketCount = resultDiagnostics.Sum(result => result.ConstructedPacketCount);
		var blockedPlayerPacketCount = resultDiagnostics.Sum(result => result.BlockedPacketCount);

		return new PlayerKnownListPopulationPacketConstructionDiagnosticPlan(
			populationPlan.OwnerPlayerObjectId,
			candidateDiagnostics,
			populationPlan.CandidatePlans.Count,
			populationPlan.MissingCandidateFactCount,
			populationPlan.CandidatePlans.Count(candidate => candidate.VisibilityRangePlan is not null),
			populationPlan.CandidatePlans.Count(candidate => candidate.SideEffectAttachmentPlan?.AttachedSideEffects.Count > 0),
			populationPlan.CandidatePlans.Sum(candidate => candidate.SideEffectAttachmentPlan?.AttachedSideEffects.Count ?? 0),
			factPlans.Length,
			factPlans.Count(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Complete),
			factPlans.Count(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked),
			CountByKind(factPlans.SelectMany(factPlan => factPlan.Blockers)),
			factSources.Length,
			factSources.Count(source => source.Kind == PlayerKnownListPopulationPacketConstructionFactSourceKind.Request),
			factSources.Count(source => source.Kind == PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlan),
			factSources.Count(source => source.Kind == PlayerKnownListPopulationPacketConstructionFactSourceKind.GeneratedFactPlanIgnoredByRequest),
			CountByKind(factSources.Select(source => source.Kind)),
			packetConstructionPlans.Length,
			packetConstructionPlans.Count(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed),
			packetConstructionPlans.Count(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed),
			packetConstructionPlans.Count(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.NoAttachedSideEffects),
			resultDiagnostics.Length,
			resultDiagnostics.Count(result => result.Status == PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed),
			resultDiagnostics.Count(result => result.Status == PlayerKnownListOperationSideEffectPacketConstructionResultStatus.PartiallyConstructed),
			resultDiagnostics.Count(result => result.Status == PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts),
			constructedPlayerPacketCount,
			blockedPlayerPacketCount,
			CountByKind(playerPacketResults.Select(result => result.Status)),
			CountByKind(playerPacketResults
				.Where(result => result.Status == PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed)
				.Select(result => result.Descriptor.Kind)),
			CreateOverallStatus(factPlans, packetConstructionPlans, resultDiagnostics, constructedPlayerPacketCount, blockedPlayerPacketCount),
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Disabled diagnostic projection for KnownList.findVisibleObjects and PlayerController packet construction metadata; does not execute controller callbacks, mutate known-list state, or send packets.");
	}

	private static PlayerKnownListPopulationPacketConstructionCandidateDiagnostic CreateCandidateDiagnostic(
		PlayerKnownListPopulationCandidatePlan candidatePlan,
		int candidateOrder)
	{
		var factPlanDiagnostics = (candidatePlan.SideEffectFactPlans ?? Array.Empty<PlayerKnownListPopulationPacketConstructionFactPlanAttachment>())
			.Select(factPlan => new PlayerKnownListPopulationFactPlanDiagnostic(
				candidatePlan.CandidatePlayerObjectId,
				candidateOrder,
				factPlan.Direction,
				factPlan.Plan.Status,
				factPlan.Plan.Blockers,
				factPlan.Plan.JavaSource))
			.ToArray();
		var factSources = candidatePlan.PacketConstructionFactSources ?? Array.Empty<PlayerKnownListPopulationPacketConstructionFactSource>();
		var packetConstructionResultDiagnostics = (candidatePlan.SideEffectPacketConstructionPlan?.Results ?? Array.Empty<PlayerKnownListOperationSideEffectPacketConstructionResult>())
			.Select(result => CreatePacketConstructionResultDiagnostic(candidatePlan, candidateOrder, result))
			.ToArray();

		return new PlayerKnownListPopulationPacketConstructionCandidateDiagnostic(
			candidatePlan.CandidatePlayerObjectId,
			candidateOrder,
			CreateCandidateStatus(factPlanDiagnostics, candidatePlan.SideEffectPacketConstructionPlan, packetConstructionResultDiagnostics),
			factPlanDiagnostics,
			factSources,
			packetConstructionResultDiagnostics,
			candidatePlan.SideEffectPacketConstructionPlan is not null);
	}

	private static PlayerKnownListPopulationPacketConstructionResultDiagnostic CreatePacketConstructionResultDiagnostic(
		PlayerKnownListPopulationCandidatePlan candidatePlan,
		int candidateOrder,
		PlayerKnownListOperationSideEffectPacketConstructionResult result)
	{
		var packetResults = result.PacketConstructionPlan?.Results ?? Array.Empty<PlayerKnownListPlayerSideEffectPacketConstructionResult>();
		return new PlayerKnownListPopulationPacketConstructionResultDiagnostic(
			candidatePlan.CandidatePlayerObjectId,
			candidateOrder,
			result.AttachedSideEffect.OperationStep.Kind,
			result.Status,
			result.PacketConstructionPlan?.Status,
			packetResults.Count(packet => packet.Status == PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed),
			packetResults.Count(packet => packet.Status != PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed),
			result.Notes);
	}

	private static PlayerKnownListPopulationPacketConstructionDiagnosticStatus CreateCandidateStatus(
		IReadOnlyList<PlayerKnownListPopulationFactPlanDiagnostic> factPlans,
		PlayerKnownListOperationSideEffectPacketConstructionPlan? packetConstructionPlan,
		IReadOnlyList<PlayerKnownListPopulationPacketConstructionResultDiagnostic> resultDiagnostics)
	{
		if (factPlans.Count == 0 && packetConstructionPlan is null)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata;

		var hasBlockingOnlyMetadata =
			factPlans.Count > 0
			&& factPlans.All(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked)
			&& resultDiagnostics.All(result => result.Status != PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed);
		if (hasBlockingOnlyMetadata)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Blocked;

		if (factPlans.Any(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked)
			|| packetConstructionPlan?.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed
			|| resultDiagnostics.Any(result => result.BlockedPacketCount > 0))
		{
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Partial;
		}

		return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Complete;
	}

	private static PlayerKnownListPopulationPacketConstructionDiagnosticStatus CreateOverallStatus(
		IReadOnlyList<PlayerKnownListPopulationFactPlanDiagnostic> factPlans,
		IReadOnlyList<PlayerKnownListOperationSideEffectPacketConstructionPlan> packetConstructionPlans,
		IReadOnlyList<PlayerKnownListPopulationPacketConstructionResultDiagnostic> resultDiagnostics,
		int constructedPlayerPacketCount,
		int blockedPlayerPacketCount)
	{
		if (factPlans.Count == 0 && packetConstructionPlans.Count == 0)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata;

		var hasConstructedMetadata = constructedPlayerPacketCount > 0
			|| packetConstructionPlans.Any(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed)
			|| factPlans.Any(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Complete);
		var hasBlockedMetadata = blockedPlayerPacketCount > 0
			|| resultDiagnostics.Any(result => result.Status != PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed)
			|| factPlans.Any(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked)
			|| packetConstructionPlans.Any(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed);

		if (!hasConstructedMetadata && hasBlockedMetadata)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Blocked;

		return hasBlockedMetadata
			? PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Partial
			: PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Complete;
	}

	private static IReadOnlyDictionary<T, int> CountByKind<T>(IEnumerable<T> values)
		where T : notnull =>
		values
			.GroupBy(value => value)
			.ToDictionary(group => group.Key, group => group.Count());
}
