namespace Aion.GameServer.Services;

public enum FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus
{
	BlockedValueProjectionHandoffNotReady,
	BlockedRuntimeEvidenceMissing,
	ReadyForRuntimeRowsValueReadersBlocked,
}

public enum FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage
{
	ValueProjectionHandoff,
	JavaRuntimeArtifactRows,
	CSharpAcceptedTraceRows,
	TypedValueReaders,
	RuntimeValueReadExecution,
}

public enum FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus
{
	Blocked,
	Deferred,
	ReadyForRuntimeInput,
}

public sealed record FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
	int Order,
	FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage Stage,
	int? Action,
	FindGroupDirectPacketMutationPostTraceMutationKind? MutationKind,
	FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus Status,
	bool HasExpectedShape,
	bool HasRuntimeEvidence,
	bool BlocksValueReaders,
	string RequiredEvidence,
	string CurrentEvidence,
	string Notes);

public sealed record FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate(
	FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus Status,
	IReadOnlyList<FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow> Rows,
	bool HasValueProjectionHandoff,
	bool HasRuntimeEvidenceChecklist,
	bool HasTypedValueReaderPreflight,
	bool HasJavaRuntimeArtifactRows,
	bool HasAcceptedCSharpTraceRows,
	bool HasRuntimeRowValues,
	int RequiredEqualityReaderFieldCount,
	int IgnoredRuntimeContextFieldCount,
	bool CanReadJavaValues,
	bool CanReadCSharpValues,
	bool CanCompareValues,
	bool CanEmitResults,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live intake gate for CM_FIND_GROUP action 2/6
/// runtime row values. It names the Java/C# rows required before typed readers
/// can read equality fields, but it never reads or compares values.
/// </summary>
public static class FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService
{
	public static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate Create(
		FindGroupMutationPostValueProjectionHandoffGate? valueProjectionHandoff = null,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist? runtimeEvidenceChecklist = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract? valueReaderPreflight = null)
	{
		valueProjectionHandoff ??= FindGroupMutationPostValueProjectionHandoffGateService.Create();
		runtimeEvidenceChecklist ??= FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create();
		valueReaderPreflight ??= FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create();

		var valueProjectionReady = valueProjectionHandoff.Status == FindGroupMutationPostValueProjectionHandoffGateStatus.ReadyForRuntimeValuesProjectionBlocked;
		var hasJavaRuntimeRows = RuntimeRow(runtimeEvidenceChecklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact)?.HasRuntimeEvidence == true;
		var hasCSharpAcceptedRows = RuntimeRow(runtimeEvidenceChecklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow)?.HasRuntimeEvidence == true;
		var hasRuntimeRowValues = RuntimeRow(runtimeEvidenceChecklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection)?.HasRuntimeEvidence == true;
		var requiredReaderFields = valueReaderPreflight.Fields.Count(field => field.RequiresJavaReader && field.RequiresCSharpReader);
		var ignoredRuntimeFields = valueReaderPreflight.Fields.Count(field => field.ReadMode == FindGroupMutationPostProjectedRowComparisonValueReadMode.IgnoredRuntimeContext);
		var status = DetermineStatus(valueProjectionReady, hasJavaRuntimeRows, hasCSharpAcceptedRows, hasRuntimeRowValues);
		var rows = new[]
		{
			ValueProjectionHandoffRow(valueProjectionHandoff, valueProjectionReady),
			JavaRuntimeRow(2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, runtimeEvidenceChecklist, hasJavaRuntimeRows),
			JavaRuntimeRow(6, FindGroupDirectPacketMutationPostTraceMutationKind.Application, runtimeEvidenceChecklist, hasJavaRuntimeRows),
			CSharpRuntimeRow(2, FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment, runtimeEvidenceChecklist, hasCSharpAcceptedRows),
			CSharpRuntimeRow(6, FindGroupDirectPacketMutationPostTraceMutationKind.Application, runtimeEvidenceChecklist, hasCSharpAcceptedRows),
			TypedReaderRow(valueReaderPreflight, requiredReaderFields, ignoredRuntimeFields, valueProjectionReady),
			RuntimeValueExecutionRow(runtimeEvidenceChecklist, hasRuntimeRowValues),
		};

		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGate(
			status,
			rows,
			HasValueProjectionHandoff: valueProjectionHandoff.Rows.Count > 0,
			HasRuntimeEvidenceChecklist: runtimeEvidenceChecklist.Rows.Count > 0,
			HasTypedValueReaderPreflight: valueReaderPreflight.Fields.Count > 0,
			HasJavaRuntimeArtifactRows: hasJavaRuntimeRows,
			HasAcceptedCSharpTraceRows: hasCSharpAcceptedRows,
			HasRuntimeRowValues: hasRuntimeRowValues,
			requiredReaderFields,
			ignoredRuntimeFields,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			valueProjectionHandoff.TraceName,
			valueProjectionHandoff.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus DetermineStatus(
		bool valueProjectionReady,
		bool hasJavaRuntimeRows,
		bool hasCSharpAcceptedRows,
		bool hasRuntimeRowValues)
	{
		if (!valueProjectionReady)
			return FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.BlockedValueProjectionHandoffNotReady;

		if (!hasJavaRuntimeRows || !hasCSharpAcceptedRows || !hasRuntimeRowValues)
			return FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.BlockedRuntimeEvidenceMissing;

		return FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.ReadyForRuntimeRowsValueReadersBlocked;
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow ValueProjectionHandoffRow(
		FindGroupMutationPostValueProjectionHandoffGate handoff,
		bool ready)
	{
		var handoffRowEvidence = handoff.Rows.Count == 0
			? "none"
			: string.Join(" | ", handoff.Rows.Select(row => $"{row.Stage}={row.Evidence}"));
		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
			1,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.ValueProjectionHandoff,
			Action: null,
			MutationKind: null,
			ready ? FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.ReadyForRuntimeInput : FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
			HasExpectedShape: handoff.Rows.Count > 0,
			HasRuntimeEvidence: handoff.HasRuntimeRowValues,
			BlocksValueReaders: !ready,
			"Value-projection handoff must reach runtime-row-value evidence intake after paired Java/C# rows, value mappings, and value-reader readiness are complete.",
			$"status={handoff.Status}; hasRuntimeRowValues={handoff.HasRuntimeRowValues}; canStartValueProjection={handoff.CanStartValueProjection}; valueProjectionHandoffRows={handoffRowEvidence}",
			"Handoff metadata preserves row-pairing and accepted-boundary-row evidence, but cannot read Java JSON or C# trace-export values.");
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow JavaRuntimeRow(
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist checklist,
		bool hasRuntimeRows)
	{
		var checklistRow = RuntimeRow(checklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact);
		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
			action == 2 ? 2 : 3,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.JavaRuntimeArtifactRows,
			action,
			mutationKind,
			hasRuntimeRows ? FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.ReadyForRuntimeInput : FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
			HasExpectedShape: checklistRow?.HasExistingProvider == true,
			HasRuntimeEvidence: hasRuntimeRows,
			BlocksValueReaders: !hasRuntimeRows,
			JavaRequirementFor(action),
			ChecklistEvidence(checklistRow),
			"Shape-valid Java artifacts are not enough; the row must come from runtime-backed Java capture before typed readers can read it.");
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow CSharpRuntimeRow(
		int action,
		FindGroupDirectPacketMutationPostTraceMutationKind mutationKind,
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist checklist,
		bool hasAcceptedRows)
	{
		var checklistRow = RuntimeRow(checklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow);
		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
			action == 2 ? 4 : 5,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.CSharpAcceptedTraceRows,
			action,
			mutationKind,
			hasAcceptedRows ? FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.ReadyForRuntimeInput : FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
			HasExpectedShape: checklistRow?.HasExistingProvider == true,
			HasRuntimeEvidence: hasAcceptedRows,
			BlocksValueReaders: !hasAcceptedRows,
			CSharpRequirementFor(action),
			ChecklistEvidence(checklistRow),
			"Disabled projections and synthetic fixtures are not accepted C# boundary rows.");
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow TypedReaderRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContract preflight,
		int requiredReaderFields,
		int ignoredRuntimeFields,
		bool handoffReady)
	{
		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
			6,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.TypedValueReaders,
			Action: null,
			MutationKind: null,
			handoffReady ? FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Deferred : FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
			HasExpectedShape: preflight.HasRequiredTypedReaders,
			HasRuntimeEvidence: false,
			BlocksValueReaders: true,
			"Implement typed readers for all required equality fields only after runtime Java artifacts and accepted C# trace rows provide values.",
			$"status={preflight.Status}; requiredEqualityReaderFields={requiredReaderFields}; ignoredRuntimeContextFields={ignoredRuntimeFields}; canReadJavaValues={preflight.CanReadJavaValues}; canReadCSharpValues={preflight.CanReadCSharpValues}",
			"Preflight names field readers and reader kinds, but deliberately does not read values.");
	}

	private static FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow RuntimeValueExecutionRow(
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist checklist,
		bool hasRuntimeRowValues)
	{
		var checklistRow = RuntimeRow(checklist, FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection);
		return new FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateRow(
			7,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.RuntimeValueReadExecution,
			Action: null,
			MutationKind: null,
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked,
			HasExpectedShape: checklistRow?.HasExistingProvider == true,
			HasRuntimeEvidence: hasRuntimeRowValues,
			BlocksValueReaders: true,
			"Run value readers against paired runtime Java/C# rows and emit comparison-ready projected values.",
			ChecklistEvidence(checklistRow),
			"Value read execution remains blocked; this gate emits no comparison rows and proves no parity.");
	}

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow? RuntimeRow(
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist checklist,
		FindGroupMutationPostProjectedRowComparisonLiveInputRequirement requirement) =>
		checklist.Rows.FirstOrDefault(row => row.Requirement == requirement);

	private static string ChecklistEvidence(FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow? row) =>
		row is null
			? "checklistRow=missing"
			: $"providerStatus={row.ProviderStatus}; hasRuntimeEvidence={row.HasRuntimeEvidence}; evidence={row.Evidence}";

	private static string JavaRequirementFor(int action) =>
		action == 2
			? "Runtime-backed explicit-root Java artifact row for action 2 Recruitment from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addRecruitment, including posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP action 0."
			: "Runtime-backed explicit-root Java artifact row for action 6 Application from CM_FIND_GROUP.readImpl/runImpl and FindGroupService.addApplication, including posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP action 4.";

	private static string CSharpRequirementFor(int action) =>
		action == 2
			? "Accepted live C# ProcessPacketAsync boundary trace row for action 2 Recruitment with executor invocation, registry observation, row values, and Java action 2 pairing identity."
			: "Accepted live C# ProcessPacketAsync boundary trace row for action 6 Application with executor invocation, registry observation, row values, and Java action 6 pairing identity.";

	private static string DecisionFor(FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.BlockedValueProjectionHandoffNotReady => "Runtime row value intake is blocked until the value-projection handoff reaches runtime-row-value evidence readiness.",
			FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.ReadyForRuntimeRowsValueReadersBlocked => "Runtime rows are present, but typed value-reader execution is still blocked and no parity is verified.",
			_ => "Runtime row value intake is blocked until runtime-backed Java artifact rows, accepted C# boundary rows, and projected row values exist for actions 2 and 6.",
		};
	}
}
