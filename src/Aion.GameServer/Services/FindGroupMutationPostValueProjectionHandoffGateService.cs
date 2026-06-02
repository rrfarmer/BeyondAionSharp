namespace Aion.GameServer.Services;

public enum FindGroupMutationPostValueProjectionHandoffGateStatus
{
	BlockedRowPairingNotReady,
	BlockedValueContractNotReady,
	BlockedValueReaderNotReady,
	ReadyForRuntimeValuesProjectionBlocked,
}

public enum FindGroupMutationPostValueProjectionHandoffGateStage
{
	RowPairingReadiness,
	ValueContract,
	ValueReaderReadiness,
	RuntimeValueEvidence,
}

public enum FindGroupMutationPostValueProjectionHandoffGateStageStatus
{
	Blocked,
	Deferred,
	ReadyForRuntimeInput,
}

public sealed record FindGroupMutationPostValueProjectionHandoffGateRow(
	int Order,
	FindGroupMutationPostValueProjectionHandoffGateStage Stage,
	FindGroupMutationPostValueProjectionHandoffGateStageStatus Status,
	bool HasExpectedShape,
	bool BlocksValueProjection,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostValueProjectionHandoffGate(
	FindGroupMutationPostValueProjectionHandoffGateStatus Status,
	IReadOnlyList<FindGroupMutationPostValueProjectionHandoffGateRow> Rows,
	bool HasRowPairingReadiness,
	bool HasValueContract,
	bool HasValueReaderReadiness,
	bool HasAllActionMutationPairs,
	bool HasAllValueSourceMappings,
	bool HasRuntimeRowValues,
	bool CanStartValueProjection,
	bool CanReadValues,
	bool CanCompareValues,
	bool CanEmitResults,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live handoff gate between CM_FIND_GROUP action
/// 2/6 Java/C# row pairing and future value projection. It records when row
/// identity can feed projection planning, but it never reads Java JSON or C#
/// trace values and never compares rows.
/// </summary>
public static class FindGroupMutationPostValueProjectionHandoffGateService
{
	public static FindGroupMutationPostValueProjectionHandoffGate Create(
		FindGroupMutationPostJavaCSharpRowPairingReadinessReport? rowPairing = null,
		FindGroupMutationPostProjectedRowComparisonValueContract? valueContract = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary? valueReaderReadiness = null)
	{
		rowPairing ??= FindGroupMutationPostJavaCSharpRowPairingReadinessReportService.Create();
		valueContract ??= FindGroupMutationPostProjectedRowComparisonValueContractService.Create();
		valueReaderReadiness ??= FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService.Create();

		var status = DetermineStatus(rowPairing, valueContract, valueReaderReadiness);
		var rows = new[]
		{
			RowPairingRow(rowPairing),
			ValueContractRow(valueContract),
			ValueReaderRow(valueReaderReadiness),
			RuntimeValuesRow(),
		};

		return new FindGroupMutationPostValueProjectionHandoffGate(
			status,
			rows,
			HasRowPairingReadiness: rowPairing.Rows.Count > 0,
			HasValueContract: valueContract.Fields.Count > 0,
			HasValueReaderReadiness: valueReaderReadiness.Stages.Count > 0,
			rowPairing.HasAllActionMutationPairs,
			HasAllValueSourceMappings: valueContract.HasResultContract && valueContract.Fields.Count > 0 && valueContract.EqualityProjectionFields.Count > 0,
			HasRuntimeRowValues: false,
			CanStartValueProjection: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			rowPairing.TraceName,
			rowPairing.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostValueProjectionHandoffGateStatus DetermineStatus(
		FindGroupMutationPostJavaCSharpRowPairingReadinessReport rowPairing,
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract,
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary valueReaderReadiness)
	{
		if (!rowPairing.CanFeedValueProjection)
			return FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedRowPairingNotReady;

		if (valueContract.Status != FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred)
			return FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueContractNotReady;

		if (valueReaderReadiness.Status != FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedReaderImplementationDeferred)
			return FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueReaderNotReady;

		return FindGroupMutationPostValueProjectionHandoffGateStatus.ReadyForRuntimeValuesProjectionBlocked;
	}

	private static FindGroupMutationPostValueProjectionHandoffGateRow RowPairingRow(
		FindGroupMutationPostJavaCSharpRowPairingReadinessReport rowPairing)
	{
		var ready = rowPairing.CanFeedValueProjection;
		var rowEvidence = rowPairing.Rows.Count == 0
			? "none"
			: string.Join(" | ", rowPairing.Rows.Select(row => $"action{row.Action}={row.CurrentEvidence}"));
		return new FindGroupMutationPostValueProjectionHandoffGateRow(
			1,
			FindGroupMutationPostValueProjectionHandoffGateStage.RowPairingReadiness,
			ready ? FindGroupMutationPostValueProjectionHandoffGateStageStatus.ReadyForRuntimeInput : FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked,
			HasExpectedShape: rowPairing.Rows.Count == 2,
			BlocksValueProjection: !ready,
			$"status={rowPairing.Status}; action2Pair={rowPairing.HasActionTwoPair}; action6Pair={rowPairing.HasActionSixPair}; canFeedValueProjection={rowPairing.CanFeedValueProjection}; rowPairingEvidence={rowEvidence}",
			ready
				? "Action 2/Recruitment and action 6/Application row identities can feed future value projection planning through the accepted-boundary-row handoff."
				: "Value projection cannot start until both Java/C# action-mutation row pairs are ready through the accepted-boundary-row handoff.");
	}

	private static FindGroupMutationPostValueProjectionHandoffGateRow ValueContractRow(
		FindGroupMutationPostProjectedRowComparisonValueContract valueContract)
	{
		var ready = valueContract.Status == FindGroupMutationPostProjectedRowComparisonValueContractStatus.ReadyForFutureValueProjectionButDeferred;
		return new FindGroupMutationPostValueProjectionHandoffGateRow(
			2,
			FindGroupMutationPostValueProjectionHandoffGateStage.ValueContract,
			ready ? FindGroupMutationPostValueProjectionHandoffGateStageStatus.Deferred : FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked,
			HasExpectedShape: valueContract.Fields.Count > 0 && valueContract.EqualityProjectionFields.Count > 0,
			BlocksValueProjection: !ready,
			$"status={valueContract.Status}; fields={valueContract.Fields.Count}; equalityFields={valueContract.EqualityProjectionFields.Count}; hasAllPairedInputs={valueContract.HasAllPairedInputs}; canProjectValues={valueContract.CanProjectValues}",
			ready
				? "Value source mappings exist for paired rows, but this contract still does not project field values."
				: "Value source mapping remains blocked until executor skeleton paired inputs are ready.");
	}

	private static FindGroupMutationPostValueProjectionHandoffGateRow ValueReaderRow(
		FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummary valueReaderReadiness)
	{
		var ready = valueReaderReadiness.Status == FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryStatus.BlockedReaderImplementationDeferred;
		return new FindGroupMutationPostValueProjectionHandoffGateRow(
			3,
			FindGroupMutationPostValueProjectionHandoffGateStage.ValueReaderReadiness,
			ready ? FindGroupMutationPostValueProjectionHandoffGateStageStatus.Deferred : FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked,
			HasExpectedShape: valueReaderReadiness.HasRequiredFieldMappings && valueReaderReadiness.Stages.Count > 0,
			BlocksValueProjection: true,
			$"status={valueReaderReadiness.Status}; hasAllPairedRows={valueReaderReadiness.HasAllPairedRows}; canReadValues={valueReaderReadiness.CanReadValues}; canCompareValues={valueReaderReadiness.CanCompareValues}; canEmitComparisonResult={valueReaderReadiness.CanEmitComparisonResult}",
			ready
				? "Typed reader stages are shaped, but implementation is intentionally deferred and cannot read Java/C# values."
				: "Value reader readiness is blocked before typed reader implementation can be considered.");
	}

	private static FindGroupMutationPostValueProjectionHandoffGateRow RuntimeValuesRow()
	{
		return new FindGroupMutationPostValueProjectionHandoffGateRow(
			4,
			FindGroupMutationPostValueProjectionHandoffGateStage.RuntimeValueEvidence,
			FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked,
			HasExpectedShape: false,
			BlocksValueProjection: true,
			"hasRuntimeRowValues=False; canReadValues=False; canCompareValues=False; canEmitResults=False",
			"Runtime-backed Java/C# row values are still missing; this gate does not read values or execute comparison.");
	}

	private static string DecisionFor(FindGroupMutationPostValueProjectionHandoffGateStatus status)
	{
		return status switch
		{
			FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedRowPairingNotReady => "Value projection handoff is blocked until Java/C# action 2/6 row pairing readiness is complete.",
			FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueContractNotReady => "Value projection handoff is blocked until value-source mappings are ready for paired rows.",
			FindGroupMutationPostValueProjectionHandoffGateStatus.BlockedValueReaderNotReady => "Value projection handoff is blocked until value-reader readiness reaches the deferred implementation stage.",
			_ => "Row pairing and value-source metadata can feed future runtime value projection, but runtime row values, typed readers, comparison, result emission, and verified parity remain blocked.",
		};
	}
}
