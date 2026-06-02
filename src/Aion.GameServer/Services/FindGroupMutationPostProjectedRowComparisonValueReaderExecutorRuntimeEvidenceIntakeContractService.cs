namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus
{
	BlockedLiveInputHandoffNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedOutputPreviewNotMaterializable,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement
{
	JavaArtifactRows,
	CSharpBoundaryRows,
	BoundaryExecutorObservation,
	RegistrySendObservation,
	RowIdentityMatching,
	ValueProjection,
	ResultOutputPrerequisites,
	RuntimeComparison,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus
{
	BlockedLiveInputHandoffNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedOutputPreviewNotMaterializable,
	BlockedRuntimeComparisonMissing,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement Requirement,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus Status,
	bool HasExistingProvider,
	bool HasRuntimeEvidence,
	bool RequiredForMaterialization,
	bool RequiredForVerifiedParity,
	bool CanMaterializeOutput,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow> Rows,
	bool HasLiveInputHandoff,
	bool HasRuntimeEvidenceChecklist,
	bool HasBlockedOutputPreview,
	bool HasAnyRuntimeEvidence,
	bool CanStartValueProjection,
	bool CanMaterializeOutputs,
	bool CanEmitResults,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live runtime-evidence intake contract for future
/// CM_FIND_GROUP action 2/6 value-reader executor materialization. It lists the
/// exact evidence still required before blocked output rows can become real
/// comparison results, but it never consumes runtime rows.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract Create(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract? liveInputHandoff = null,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist? runtimeEvidenceChecklist = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract? blockedOutputPreview = null)
	{
		liveInputHandoff ??= FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();
		runtimeEvidenceChecklist ??= FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(liveInputHandoff);
		blockedOutputPreview ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create();

		var status = DetermineStatus(liveInputHandoff, runtimeEvidenceChecklist, blockedOutputPreview);
		var rows = new[]
		{
			Row(
				1,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact,
				"Runtime-backed Java action 2 and action 6 mutation-post artifact rows generated from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addRecruitment/addApplication hooks.",
				"Checked-in Java artifact files and shape-valid fixture rows are not enough; intake requires capture-enabled runtime rows for both actions.",
				requiredForMaterialization: true),
			Row(
				2,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow,
				"Accepted live C# boundary rows for action 2 and action 6 with boundary acceptance, active-player facts, mutation facts, direct-packet facts, and side-effect guard fields.",
				"Disabled projections and synthetic rows remain rejected as non-live evidence.",
				requiredForMaterialization: true),
			Row(
				3,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.BoundaryExecutorObservation,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.BoundaryExecutorInvocation,
				"Observed CM_FIND_GROUP boundary executor invocation after packet acceptance and before comparison input envelope creation.",
				"Executor skeletons and envelopes are metadata until observed from the guarded live boundary.",
				requiredForMaterialization: true),
			Row(
				4,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RegistrySendObservation,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation,
				"Observed active-player registry sends in Java order: posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP, zero broadcasts, zero invite dispatches.",
				"Registry contracts name required sends but do not prove live sends or ordering.",
				requiredForMaterialization: true),
			Row(
				5,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching,
				"Matched row identities for action, mutationKind, activePlayerObjectId, and mutatedEntryObjectId across runtime Java and C# rows.",
				"Paired readiness remains metadata until runtime row identities are inspected.",
				requiredForMaterialization: true),
			Row(
				6,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection,
				"Projected Java and C# values for every equality field named by the value-reader result schema.",
				"Value contracts and reader plans name fields but deliberately do not read values.",
				requiredForMaterialization: true),
			OutputPrerequisiteRow(7, status, blockedOutputPreview),
			Row(
				8,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
				status,
				runtimeEvidenceChecklist,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison,
				"Deterministic Java/C# runtime or socket comparison for action 2/6 mutation, packet, side-effect, and result-output observations.",
				"Runtime comparison is required before verified parity can be claimed.",
				requiredForMaterialization: false),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract(
			status,
			rows,
			HasLiveInputHandoff: liveInputHandoff.Requirements.Count > 0,
			HasRuntimeEvidenceChecklist: runtimeEvidenceChecklist.Rows.Count > 0,
			HasBlockedOutputPreview: blockedOutputPreview.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanStartValueProjection: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			blockedOutputPreview.TraceName,
			blockedOutputPreview.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus DetermineStatus(
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract liveInputHandoff,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract blockedOutputPreview)
	{
		if (liveInputHandoff.Status == FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady
			|| runtimeEvidenceChecklist.Status == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady;

		if (!runtimeEvidenceChecklist.HasAnyRuntimeEvidence
			|| blockedOutputPreview.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow Row(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement requirement,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus intakeStatus,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist runtimeEvidenceChecklist,
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirement sourceRequirement,
		string requiredEvidence,
		string notes,
		bool requiredForMaterialization)
	{
		var sourceRow = runtimeEvidenceChecklist.Rows.FirstOrDefault(row => row.Requirement == sourceRequirement);
		var rowStatus = intakeStatus switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedLiveInputHandoffNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable when sourceRequirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedRuntimeComparisonMissing,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedOutputPreviewNotMaterializable,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedRuntimeEvidenceMissing,
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow(
			order,
			requirement,
			rowStatus,
			HasExistingProvider: sourceRow?.HasExistingProvider ?? false,
			HasRuntimeEvidence: false,
			requiredForMaterialization,
			RequiredForVerifiedParity: true,
			CanMaterializeOutput: false,
			requiredEvidence,
			$"sourceRequirement={sourceRequirement}; providerStatus={sourceRow?.ProviderStatus.ToString() ?? "Missing"}; hasRuntimeEvidence={sourceRow?.HasRuntimeEvidence.ToString() ?? "False"}; checklistStatus={runtimeEvidenceChecklist.Status}; canClaimVerifiedParity={runtimeEvidenceChecklist.CanClaimVerifiedParity}",
			notes);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow OutputPrerequisiteRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus intakeStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract blockedOutputPreview)
	{
		var rowStatus = intakeStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady
			? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedLiveInputHandoffNotReady
			: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedOutputPreviewNotMaterializable;

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
			rowStatus,
			HasExistingProvider: blockedOutputPreview.Rows.Count > 0,
			HasRuntimeEvidence: false,
			RequiredForMaterialization: true,
			RequiredForVerifiedParity: true,
			CanMaterializeOutput: false,
			"Blocked-output preview rows must be backed by row identity decisions, projected values, context attachment rules, and result emission eligibility for Matched, MissingJavaRow, MissingCSharpRow, FieldMismatch, and IgnoredRuntimeContext.",
			$"previewStatus={blockedOutputPreview.Status}; outputKinds={blockedOutputPreview.OutputKindCount}; materializableOutputs={blockedOutputPreview.MaterializableOutputCount}; emittableOutputs={blockedOutputPreview.EmittableOutputCount}; canEmitAnyResult={blockedOutputPreview.CanEmitAnyResult}",
			"Output preview rows are intake requirements only and cannot become materialized results without runtime rows.");
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady => "Value-reader executor runtime-evidence intake is blocked until live-input handoff metadata reaches runtime-artifact readiness.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor runtime-evidence intake is blocked because required Java/C# runtime evidence is missing.",
			_ => "Value-reader executor runtime-evidence intake is defined, but blocked-output preview rows still cannot materialize or emit results.",
		};
	}
}
