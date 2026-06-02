namespace Aion.GameServer.Services;

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus
{
	BlockedUpstreamMetadataNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedResultEmissionDeferred,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement
{
	BlockedOutputPreview,
	RuntimeEvidenceIntake,
	MaterializationPreflight,
	ResultEmissionGate,
	RuntimeComparison,
}

public enum FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus
{
	BlockedUpstreamMetadataNotReady,
	BlockedRuntimeEvidenceMissing,
	BlockedMaterializationUnavailable,
	BlockedResultEmissionDisabled,
	BlockedRuntimeComparisonMissing,
}

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow(
	int Order,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement Requirement,
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus Status,
	bool HasProvider,
	bool HasRuntimeEvidence,
	bool BlocksExecutorImplementation,
	bool BlocksVerifiedParity,
	string ProviderStatus,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract(
	FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus Status,
	IReadOnlyList<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow> Rows,
	bool HasBlockedOutputPreview,
	bool HasRuntimeEvidenceIntake,
	bool HasMaterializationPreflight,
	bool HasResultEmissionGate,
	bool HasAnyRuntimeEvidence,
	bool CanImplementExecutor,
	bool CanExecuteExecutor,
	bool CanMaterializeOutputs,
	bool CanEmitResults,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: final non-live evidence summary for future
/// CM_FIND_GROUP action 2/6 value-reader executor implementation. It rolls up
/// output preview, runtime intake, materialization preflight, and emission gate
/// metadata, but it never reads values, compares rows, or emits results.
/// </summary>
public static class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService
{
	public static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract Create(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract? blockedOutputPreview = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract? runtimeEvidenceIntake = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract? materializationPreflight = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract? resultEmissionGate = null)
	{
		blockedOutputPreview ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create();
		runtimeEvidenceIntake ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create(blockedOutputPreview: blockedOutputPreview);
		materializationPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create(runtimeEvidenceIntake, blockedOutputPreview);
		resultEmissionGate ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create(materializationPreflight);

		var status = StatusFor(blockedOutputPreview, runtimeEvidenceIntake, materializationPreflight, resultEmissionGate);
		var rows = new[]
		{
			PreviewRow(1, status, blockedOutputPreview),
			IntakeRow(2, status, runtimeEvidenceIntake),
			MaterializationRow(3, status, materializationPreflight),
			EmissionGateRow(4, status, resultEmissionGate),
			RuntimeComparisonRow(5, status, runtimeEvidenceIntake, resultEmissionGate),
		};

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContract(
			status,
			rows,
			HasBlockedOutputPreview: blockedOutputPreview.Rows.Count > 0,
			HasRuntimeEvidenceIntake: runtimeEvidenceIntake.Rows.Count > 0,
			HasMaterializationPreflight: materializationPreflight.Rows.Count > 0,
			HasResultEmissionGate: resultEmissionGate.Rows.Count > 0,
			HasAnyRuntimeEvidence: false,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			blockedOutputPreview.TraceName,
			blockedOutputPreview.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus StatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract blockedOutputPreview,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract runtimeEvidenceIntake,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract materializationPreflight,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract resultEmissionGate)
	{
		if (blockedOutputPreview.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedImplementationPlanNotReady
			|| runtimeEvidenceIntake.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady
			|| materializationPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady
			|| resultEmissionGate.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady;

		if (blockedOutputPreview.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing
			|| runtimeEvidenceIntake.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing
			|| materializationPreflight.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing
			|| resultEmissionGate.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing)
			return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedResultEmissionDeferred;
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow PreviewRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract preview) =>
		new(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.BlockedOutputPreview,
			RowStatusFor(summaryStatus),
			HasProvider: preview.Rows.Count > 0,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			preview.Status.ToString(),
			"Blocked-output preview must enumerate every result kind before executor implementation can consume output metadata.",
			$"rows={preview.Rows.Count}; outputKinds={preview.OutputKindCount}; materializableOutputs={preview.MaterializableOutputCount}; emittableOutputs={preview.EmittableOutputCount}; canClaimVerifiedParity={preview.CanClaimVerifiedParity}",
			"Preview rows are output metadata only and do not materialize results.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow IntakeRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract intake) =>
		new(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeEvidenceIntake,
			RowStatusFor(summaryStatus),
			HasProvider: intake.Rows.Count > 0,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			intake.Status.ToString(),
			"Runtime-evidence intake must provide Java artifact rows, C# boundary rows, executor observation, registry observation, row identity, value projection, and output prerequisites.",
			$"rows={intake.Rows.Count}; hasAnyRuntimeEvidence={intake.HasAnyRuntimeEvidence}; canStartValueProjection={intake.CanStartValueProjection}; canEmitResults={intake.CanEmitResults}; canClaimVerifiedParity={intake.CanClaimVerifiedParity}",
			"Intake rows remain prerequisites until runtime-backed Java/C# evidence exists.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow MaterializationRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract preflight) =>
		new(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.MaterializationPreflight,
			summaryStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedUpstreamMetadataNotReady
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedMaterializationUnavailable,
			HasProvider: preflight.Rows.Count > 0,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			preflight.Status.ToString(),
			"Materialization preflight must prove every output row has satisfied intake prerequisites before result rows can exist.",
			$"rows={preflight.Rows.Count}; materializableOutputs={preflight.MaterializableOutputCount}; emittableOutputs={preflight.EmittableOutputCount}; canEmitAnyResult={preflight.CanEmitAnyResult}; canClaimVerifiedParity={preflight.CanClaimVerifiedParity}",
			"Materialization remains disabled for all output kinds.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow EmissionGateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract gate) =>
		new(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.ResultEmissionGate,
			summaryStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady
				? FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedUpstreamMetadataNotReady
				: FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedResultEmissionDisabled,
			HasProvider: gate.Rows.Count > 0,
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			gate.Status.ToString(),
			"Result-emission gate must prove a materialized row, runtime comparison evidence, and output-specific emission conditions before any result can be emitted.",
			$"rows={gate.Rows.Count}; emittableOutputs={gate.EmittableOutputCount}; canEmitAnyResult={gate.CanEmitAnyResult}; canClaimVerifiedParity={gate.CanClaimVerifiedParity}",
			"Emission remains disabled and ignored runtime context cannot emit standalone.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRow RuntimeComparisonRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus summaryStatus,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract intake,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract gate) =>
		new(
			order,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeComparisonMissing,
			HasProvider: intake.Rows.Any(row => row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison)
				&& gate.Rows.All(row => row.RequiresRuntimeComparison),
			HasRuntimeEvidence: false,
			BlocksExecutorImplementation: true,
			BlocksVerifiedParity: true,
			summaryStatus.ToString(),
			"Deterministic Java/C# runtime or socket comparison must exist for action 2 and action 6 mutation, packet, side-effect, and result-output observations.",
			$"intakeRuntimeComparisonRows={intake.Rows.Count(row => row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison)}; gateRowsRequireRuntimeComparison={gate.Rows.Count(row => row.RequiresRuntimeComparison)}; canClaimVerifiedParity={gate.CanClaimVerifiedParity}",
			"Runtime comparison is still missing, so verified parity remains blocked.");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus RowStatusFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedUpstreamMetadataNotReady,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeEvidenceMissing,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedResultEmissionDisabled,
		};
	}

	private static string DecisionFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus status)
	{
		return status switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady => "Value-reader executor evidence summary is blocked until upstream output-preview, intake, materialization, and emission-gate metadata are ready.",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing => "Value-reader executor evidence summary is blocked because runtime-backed Java rows, accepted C# rows, value projection, result materialization, and runtime comparison evidence are missing.",
			_ => "Value-reader executor evidence summary is defined, but executor implementation, result emission, runtime comparison, and verified parity remain disabled.",
		};
	}
}
