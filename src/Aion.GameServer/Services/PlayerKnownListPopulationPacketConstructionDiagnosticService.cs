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
	PlayerKnownListPacketConstructionAttackSpeedFactSource RideAttackSpeedFactSource,
	PlayerKnownListAttackSpeedFactResolutionStatus? RideAttackSpeedResolutionStatus,
	PlayerKnownListPacketConstructionAbnormalEffectFactSource AbnormalEffectFactSource,
	PlayerKnownListAbnormalEffectFactResolutionStatus? AbnormalEffectResolutionStatus,
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

public sealed record PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic(
	int CandidatePlayerObjectId,
	int CandidateOrder,
	PlayerKnownListPopulationPacketConstructionFactPlanDirection Direction,
	PlayerKnownListPetVisibilityOrderPlanStatus VisibilityPlanStatus,
	PlayerKnownListPetVisibilityPacketConstructionStatus PacketConstructionStatus,
	int DescriptorCount,
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
	IReadOnlyList<PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic> PetVisibilityPacketConstructionResults,
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
	IReadOnlyDictionary<PlayerKnownListPacketConstructionAttackSpeedFactSource, int> RideAttackSpeedFactSourceCountsByKind,
	IReadOnlyDictionary<PlayerKnownListAttackSpeedFactResolutionStatus, int> RideAttackSpeedResolutionStatusCountsByKind,
	IReadOnlyDictionary<PlayerKnownListPacketConstructionAbnormalEffectFactSource, int> AbnormalEffectFactSourceCountsByKind,
	IReadOnlyDictionary<PlayerKnownListAbnormalEffectFactResolutionStatus, int> AbnormalEffectResolutionStatusCountsByKind,
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
	int PetVisibilityPacketConstructionPlanCount,
	int ConstructedPetVisibilityPacketConstructionPlanCount,
	int PartiallyConstructedPetVisibilityPacketConstructionPlanCount,
	int NoDescriptorPetVisibilityPacketConstructionPlanCount,
	int ConstructedPetPacketCount,
	int BlockedPetPacketCount,
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
		var petResultDiagnostics = candidateDiagnostics
			.SelectMany(candidate => candidate.PetVisibilityPacketConstructionResults)
			.ToArray();
		var constructedPlayerPacketCount = resultDiagnostics.Sum(result => result.ConstructedPacketCount);
		var blockedPlayerPacketCount = resultDiagnostics.Sum(result => result.BlockedPacketCount);
		var constructedPetPacketCount = petResultDiagnostics.Sum(result => result.ConstructedPacketCount);
		var blockedPetPacketCount = petResultDiagnostics.Sum(result => result.BlockedPacketCount);

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
			CountByKind(factPlans.Select(factPlan => factPlan.RideAttackSpeedFactSource)),
			CountByKind(factPlans
				.Where(factPlan => factPlan.RideAttackSpeedResolutionStatus is not null)
				.Select(factPlan => factPlan.RideAttackSpeedResolutionStatus!.Value)),
			CountByKind(factPlans.Select(factPlan => factPlan.AbnormalEffectFactSource)),
			CountByKind(factPlans
				.Where(factPlan => factPlan.AbnormalEffectResolutionStatus is not null)
				.Select(factPlan => factPlan.AbnormalEffectResolutionStatus!.Value)),
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
			petResultDiagnostics.Length,
			petResultDiagnostics.Count(result => result.PacketConstructionStatus == PlayerKnownListPetVisibilityPacketConstructionStatus.Constructed),
			petResultDiagnostics.Count(result => result.PacketConstructionStatus == PlayerKnownListPetVisibilityPacketConstructionStatus.PartiallyConstructed),
			petResultDiagnostics.Count(result => result.PacketConstructionStatus == PlayerKnownListPetVisibilityPacketConstructionStatus.NoDescriptors),
			constructedPetPacketCount,
			blockedPetPacketCount,
			CreateOverallStatus(
				factPlans,
				packetConstructionPlans,
				resultDiagnostics,
				constructedPlayerPacketCount,
				blockedPlayerPacketCount,
				petResultDiagnostics,
				constructedPetPacketCount,
				blockedPetPacketCount),
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Disabled diagnostic projection for KnownList.findVisibleObjects and PlayerController player/pet packet construction metadata; does not execute controller callbacks, mutate known-list state, or send packets.");
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
				factPlan.Plan.RideAttackSpeedFactSource,
				factPlan.Plan.RideAttackSpeedResolutionStatus,
				factPlan.Plan.AbnormalEffectFactSource,
				factPlan.Plan.AbnormalEffectResolutionStatus,
				factPlan.Plan.JavaSource))
			.ToArray();
		var factSources = candidatePlan.PacketConstructionFactSources ?? Array.Empty<PlayerKnownListPopulationPacketConstructionFactSource>();
		var packetConstructionResultDiagnostics = (candidatePlan.SideEffectPacketConstructionPlan?.Results ?? Array.Empty<PlayerKnownListOperationSideEffectPacketConstructionResult>())
			.Select(result => CreatePacketConstructionResultDiagnostic(candidatePlan, candidateOrder, result))
			.ToArray();
		var petVisibilityPacketConstructionResultDiagnostics = (candidatePlan.PetVisibilityPacketConstructionPlans ?? Array.Empty<PlayerKnownListPopulationPetVisibilityPacketConstructionAttachment>())
			.Select(result => CreatePetVisibilityPacketConstructionResultDiagnostic(candidatePlan, candidateOrder, result))
			.ToArray();

		return new PlayerKnownListPopulationPacketConstructionCandidateDiagnostic(
			candidatePlan.CandidatePlayerObjectId,
			candidateOrder,
			CreateCandidateStatus(
				factPlanDiagnostics,
				candidatePlan.SideEffectPacketConstructionPlan,
				packetConstructionResultDiagnostics,
				petVisibilityPacketConstructionResultDiagnostics),
			factPlanDiagnostics,
			factSources,
			packetConstructionResultDiagnostics,
			petVisibilityPacketConstructionResultDiagnostics,
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

	private static PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic CreatePetVisibilityPacketConstructionResultDiagnostic(
		PlayerKnownListPopulationCandidatePlan candidatePlan,
		int candidateOrder,
		PlayerKnownListPopulationPetVisibilityPacketConstructionAttachment result)
	{
		var packetResults = result.PacketConstructionPlan.Results;
		return new PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic(
			candidatePlan.CandidatePlayerObjectId,
			candidateOrder,
			result.Direction,
			result.VisibilityPlan.Status,
			result.PacketConstructionPlan.Status,
			result.VisibilityPlan.Descriptors.Count,
			packetResults.Count(packet => packet.Status == PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed),
			packetResults.Count(packet => packet.Status != PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed),
			result.PacketConstructionPlan.Results.FirstOrDefault(packet => packet.Status != PlayerKnownListPetVisibilityPacketConstructionResultStatus.Constructed)?.Notes
				?? result.VisibilityPlan.Notes);
	}

	private static PlayerKnownListPopulationPacketConstructionDiagnosticStatus CreateCandidateStatus(
		IReadOnlyList<PlayerKnownListPopulationFactPlanDiagnostic> factPlans,
		PlayerKnownListOperationSideEffectPacketConstructionPlan? packetConstructionPlan,
		IReadOnlyList<PlayerKnownListPopulationPacketConstructionResultDiagnostic> resultDiagnostics,
		IReadOnlyList<PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic> petResultDiagnostics)
	{
		if (factPlans.Count == 0 && packetConstructionPlan is null && petResultDiagnostics.Count == 0)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata;

		var hasBlockingOnlyMetadata =
			(factPlans.Count > 0 || petResultDiagnostics.Count > 0)
			&& factPlans.All(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked)
			&& resultDiagnostics.All(result => result.Status != PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed)
			&& petResultDiagnostics.All(result => result.ConstructedPacketCount == 0 && result.BlockedPacketCount > 0);
		if (hasBlockingOnlyMetadata)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.Blocked;

		if (factPlans.Any(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Blocked)
			|| packetConstructionPlan?.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed
			|| resultDiagnostics.Any(result => result.BlockedPacketCount > 0)
			|| petResultDiagnostics.Any(result => result.BlockedPacketCount > 0))
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
		int blockedPlayerPacketCount,
		IReadOnlyList<PlayerKnownListPopulationPetVisibilityPacketConstructionDiagnostic> petResultDiagnostics,
		int constructedPetPacketCount,
		int blockedPetPacketCount)
	{
		if (factPlans.Count == 0 && packetConstructionPlans.Count == 0 && petResultDiagnostics.Count == 0)
			return PlayerKnownListPopulationPacketConstructionDiagnosticStatus.NoPacketConstructionMetadata;

		var hasConstructedMetadata = constructedPlayerPacketCount > 0
			|| constructedPetPacketCount > 0
			|| packetConstructionPlans.Any(plan => plan.Status == PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed)
			|| factPlans.Any(factPlan => factPlan.Status == PlayerKnownListPacketConstructionFactPlanStatus.Complete);
		var hasBlockedMetadata = blockedPlayerPacketCount > 0
			|| blockedPetPacketCount > 0
			|| resultDiagnostics.Any(result => result.Status != PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed)
			|| petResultDiagnostics.Any(result => result.PacketConstructionStatus == PlayerKnownListPetVisibilityPacketConstructionStatus.PartiallyConstructed)
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
