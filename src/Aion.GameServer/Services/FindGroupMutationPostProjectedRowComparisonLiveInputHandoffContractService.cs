namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus
{
	BlockedSummaryNotReady,
	BlockedMissingRuntimeArtifacts,
}

public enum FindGroupMutationPostProjectedRowComparisonLiveInputRequirement
{
	ProjectedRowReadinessSummary,
	ValueReaderReadinessSummary,
	JavaRuntimeTraceArtifact,
	CSharpLiveBoundaryRow,
	BoundaryExecutorInvocation,
	RegistrySendObservation,
	ValueProjection,
	RowIdentityMatching,
	ResultEmission,
	LiveDispatchGuard,
	RuntimeSocketComparison,
}

public enum FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedSummaryNotReady,
	BlockedMissingRuntimeEvidence,
	BlockedLiveDispatchDisabled,
	BlockedComparisonNotExecuted,
}

public sealed record FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonLiveInputRequirement Requirement,
	FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus Status,
	bool IsRuntimeEvidence,
	bool BlocksLiveComparison,
	string RequiredArtifact,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract(
	FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow> Requirements,
	bool HasReadinessSummary,
	bool HasNonLiveMetadata,
	bool HasRequiredRuntimeEvidence,
	bool CanStartLiveComparison,
	bool CanEnableLiveDispatch,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live handoff contract for CM_FIND_GROUP action 2/6
/// projected-row comparison. It enumerates the runtime artifacts still required
/// after metadata readiness, but it never executes comparison or live dispatch.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService
{
	public static FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract Create(
		FindGroupMutationPostProjectedRowComparisonReadinessSummary? readinessSummary = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary? valueReaderReadinessSummary = null)
	{
		readinessSummary ??= FindGroupMutationPostProjectedRowComparisonReadinessSummaryService.Create();
		valueReaderReadinessSummary ??= CreateDefaultValueReaderReadinessSummary();
		var summaryReadyForRuntimeInputs = readinessSummary.Status == FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred
			|| readinessSummary.Status == FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedResultEmissionUnavailable;
		var status = summaryReadyForRuntimeInputs
			? FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedMissingRuntimeArtifacts
			: FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady;
		var rows = new[]
		{
			SummaryRow(readinessSummary, summaryReadyForRuntimeInputs),
			ValueReaderSummaryRow(valueReaderReadinessSummary),
			RuntimeRow(
				3,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact,
				summaryReadyForRuntimeInputs,
				"Runtime-backed Java action 2/6 mutation-post trace rows from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addRecruitment/addApplication.",
				$"readinessStatus={readinessSummary.Status}; javaSource={readinessSummary.JavaSource}",
				"Shape-valid repository Java artifacts are metadata only; comparison needs runtime-backed Java rows."),
			RuntimeRow(
				4,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow,
				summaryReadyForRuntimeInputs,
				"Accepted C# live boundary rows for action 2 and action 6 with boundary acceptance, active player, mutation state, direct packet, and side-effect guard fields.",
				$"hasAllPairedInputs={readinessSummary.HasAllPairedInputs}; canCompareRows={readinessSummary.CanCompareRows}",
				"Disabled C# projections and synthetic rows are not live boundary evidence."),
			RuntimeRow(
				5,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.BoundaryExecutorInvocation,
				summaryReadyForRuntimeInputs,
				"Evidence that the guarded comparison input was produced by the CM_FIND_GROUP boundary executor after packet acceptance.",
				$"canProjectValues={readinessSummary.CanProjectValues}",
				"Executor metadata is not enough; future rows must prove boundary-driven execution."),
			RuntimeRow(
				6,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation,
				summaryReadyForRuntimeInputs,
				"Observed active-player registry sends in Java order: posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP, zero broadcasts, zero invite dispatches.",
				"expected direct sends per action=2; expected broadcast count=0; expected invite count=0",
				"Registry observation is required before direct-packet parity can be compared."),
			RuntimeRow(
				7,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection,
				summaryReadyForRuntimeInputs,
				"Projected Java and C# values for every required equality field in the result contract.",
				$"canProjectValues={readinessSummary.CanProjectValues}",
				"Value-source metadata names fields only; it does not read values."),
			RuntimeRow(
				8,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching,
				summaryReadyForRuntimeInputs,
				"Matched row keys for action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId across Java and C# rows.",
				$"hasAllPairedInputs={readinessSummary.HasAllPairedInputs}",
				"Paired readiness is metadata until live Java/C# row identities are inspected."),
			RuntimeRow(
				9,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ResultEmission,
				summaryReadyForRuntimeInputs,
				"Real comparison output rows for Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, and IgnoredRuntimeContext when applicable.",
				$"canEmitResults={readinessSummary.CanEmitResults}",
				"Blocked-result rows describe output shapes but cannot emit results."),
			LiveDispatchGuardRow(10, summaryReadyForRuntimeInputs),
			RuntimeRow(
				11,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison,
				summaryReadyForRuntimeInputs,
				"Deterministic Java/C# runtime or socket comparison showing equivalent action 2/6 mutation, packet, and side-effect observations.",
				"isLive=false; runtimeComparisonExecuted=false",
				"Runtime comparison is required before verified parity can be claimed."),
		};

		return new FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract(
			status,
			rows,
			HasReadinessSummary: readinessSummary.Stages.Count > 0,
			HasNonLiveMetadata: rows.Any(row => row.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata),
			HasRequiredRuntimeEvidence: false,
			CanStartLiveComparison: false,
			CanEnableLiveDispatch: false,
			DecisionFor(status),
			readinessSummary.TraceName,
			readinessSummary.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow SummaryRow(
		FindGroupMutationPostProjectedRowComparisonReadinessSummary readinessSummary,
		bool summaryReadyForRuntimeInputs)
	{
		return new FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
			1,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ProjectedRowReadinessSummary,
			summaryReadyForRuntimeInputs
				? FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedSummaryNotReady,
			IsRuntimeEvidence: false,
			BlocksLiveComparison: !summaryReadyForRuntimeInputs,
			"Non-live readiness summary linking dry-run, executor skeleton, value contract, and blocked-result report.",
			$"status={readinessSummary.Status}; stages={readinessSummary.Stages.Count}; canCompareRows={readinessSummary.CanCompareRows}; canProjectValues={readinessSummary.CanProjectValues}; canEmitResults={readinessSummary.CanEmitResults}",
			summaryReadyForRuntimeInputs
				? "Metadata chain is present, but runtime artifacts are still required."
				: "Complete the projected-row readiness summary gates before collecting runtime inputs.");
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary CreateDefaultValueReaderReadinessSummary()
	{
		var gate = new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport(
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedComparatorNotAllowed,
			[],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasRuntimeEvidence: true,
			CanImplementComparator: false,
			CanExecuteComparator: false,
			CanClaimVerifiedParity: false,
			CanEnableLiveDispatch: false,
			"Runtime evidence remains non-live metadata; value-reader implementation is deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
		var design = FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create(gate);
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);
		var mismatchContextPreflight = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(preflight);
		var skeleton = FindGroupMutationPostProjectedRowComparisonValueReaderSkeletonService.Create(design);
		var blockedReport = FindGroupMutationPostProjectedRowComparisonValueReaderBlockedResultReportService.Create(skeleton);

		return FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create(design, preflight, mismatchContextPreflight, skeleton, blockedReport);
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow ValueReaderSummaryRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary valueReaderReadinessSummary)
	{
		var hasSummary = valueReaderReadinessSummary.Stages.Count > 0;
		return new FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
			2,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary,
			hasSummary
				? FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedSummaryNotReady,
			IsRuntimeEvidence: false,
			BlocksLiveComparison: !hasSummary,
			"Non-live value-reader readiness summary linking design contract, typed-reader preflight, mismatch-context preflight, reader skeleton, and blocked-result report.",
			$"status={valueReaderReadinessSummary.Status}; stages={valueReaderReadinessSummary.Stages.Count}; canReadValues={valueReaderReadinessSummary.CanReadValues}; canCompareValues={valueReaderReadinessSummary.CanCompareValues}; canEmitComparisonResult={valueReaderReadinessSummary.CanEmitComparisonResult}",
			hasSummary
				? "Value-reader metadata chain is present, but it reads no Java/C# values and is not runtime evidence."
				: "Complete the value-reader readiness summary before collecting value-reader runtime inputs.");
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow RuntimeRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirement requirement,
		bool summaryReadyForRuntimeInputs,
		string requiredArtifact,
		string evidence,
		string notes)
	{
		return new FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
			order,
			requirement,
			summaryReadyForRuntimeInputs
				? FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedMissingRuntimeEvidence
				: FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedSummaryNotReady,
			IsRuntimeEvidence: false,
			BlocksLiveComparison: true,
			requiredArtifact,
			evidence,
			notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow LiveDispatchGuardRow(
		int order,
		bool summaryReadyForRuntimeInputs)
	{
		return new FindGroupMutationPostProjectedRowComparisonLiveInputRequirementRow(
			order,
			FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard,
			summaryReadyForRuntimeInputs
				? FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedLiveDispatchDisabled
				: FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedSummaryNotReady,
			IsRuntimeEvidence: false,
			BlocksLiveComparison: true,
			"Production GameServerConnection.ProcessPacketAsync CmFindGroup dispatch remains disabled until runtime evidence and comparison are ready.",
			"canEnableLiveDispatch=false; processPacketCmFindGroupWired=false",
			"Live dispatch must not be enabled by this handoff contract.");
	}

	private static string DecisionFor(FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady => "Live comparison input handoff is blocked until the projected-row readiness summary reaches the runtime-input handoff stage.",
			_ => "Live comparison input handoff is blocked because required Java/C# runtime artifacts, registry observations, value projection, result emission, and runtime comparison are missing.",
		};
	}
}
