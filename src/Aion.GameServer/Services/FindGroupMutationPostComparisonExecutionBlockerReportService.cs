namespace Aion.GameServer.Services;

public enum FindGroupMutationPostComparisonExecutionBlockerReportStatus
{
	BlockedMissingJavaRows,
	BlockedMissingLiveCSharpRows,
	BlockedMissingReadiness,
	ReadyForExecutor,
}

public enum FindGroupMutationPostComparisonExecutionBlockerReason
{
	MissingJavaRows,
	MissingLiveCSharpRows,
	MissingGuardedFixtureResultContract,
	MissingProjectionMetadata,
	MissingReadinessAggregate,
	MissingResultContract,
	ReadyNoBlocker,
}

public sealed record FindGroupMutationPostComparisonExecutionBlockerRow(
	int Order,
	FindGroupMutationPostComparisonInputEnvelopeGate Gate,
	FindGroupMutationPostComparisonInputEnvelopeGateStatus GateStatus,
	FindGroupMutationPostComparisonExecutionBlockerReason Reason,
	bool BlocksExecution,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostComparisonExecutionBlockerReport(
	FindGroupMutationPostComparisonExecutionBlockerReportStatus Status,
	IReadOnlyList<FindGroupMutationPostComparisonExecutionBlockerRow> Rows,
	bool HasJavaRows,
	bool HasLiveCSharpRows,
	bool HasProjectionMetadata,
	bool HasReadinessAggregate,
	bool HasResultContract,
	bool ShouldExecuteComparison,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: execution blocker report for future CM_FIND_GROUP action 2/6
/// mutation-post comparison. This explains whether comparison may be invoked; it does
/// not compare Java and C# rows.
/// </summary>
public static class FindGroupMutationPostComparisonExecutionBlockerReportService
{
	public static FindGroupMutationPostComparisonExecutionBlockerReport Create(
		FindGroupMutationPostComparisonInputEnvelope? envelope = null)
	{
		envelope ??= FindGroupMutationPostComparisonInputEnvelopeService.Create();

		var rows = envelope.Gates
			.Select((gate, index) => CreateRow(index + 1, gate))
			.ToArray();
		var status = DetermineStatus(envelope);
		var shouldExecute = status == FindGroupMutationPostComparisonExecutionBlockerReportStatus.ReadyForExecutor;

		return new FindGroupMutationPostComparisonExecutionBlockerReport(
			status,
			rows,
			HasJavaRows: envelope.HasActionTwoJavaRow && envelope.HasActionSixJavaRow,
			HasLiveCSharpRows: envelope.HasActionTwoLiveCSharpRow && envelope.HasActionSixLiveCSharpRow,
			envelope.HasProjectionMetadata,
			envelope.HasReadinessAggregate,
			envelope.HasResultContract,
			ShouldExecuteComparison: shouldExecute,
			shouldExecute
				? "Envelope gates are ready; a future executor may compare projected rows, but this report did not execute comparison."
				: "Comparison not executed because one or more envelope gates are blocked.",
			envelope.TraceName,
			envelope.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostComparisonExecutionBlockerReportStatus DetermineStatus(
		FindGroupMutationPostComparisonInputEnvelope envelope)
	{
		return envelope.Status switch
		{
			FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingJavaRows => FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingJavaRows,
			FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows => FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingLiveCSharpRows,
			FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingReadiness => FindGroupMutationPostComparisonExecutionBlockerReportStatus.BlockedMissingReadiness,
			_ => FindGroupMutationPostComparisonExecutionBlockerReportStatus.ReadyForExecutor,
		};
	}

	private static FindGroupMutationPostComparisonExecutionBlockerRow CreateRow(
		int order,
		FindGroupMutationPostComparisonInputEnvelopeGateRow gate)
	{
		var reason = gate.Status switch
		{
			FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingJavaRows => FindGroupMutationPostComparisonExecutionBlockerReason.MissingJavaRows,
			FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows =>
				gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.GuardedFixtureResultContract
					? FindGroupMutationPostComparisonExecutionBlockerReason.MissingGuardedFixtureResultContract
					: FindGroupMutationPostComparisonExecutionBlockerReason.MissingLiveCSharpRows,
			FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness => ReasonForReadinessGate(gate.Gate),
			_ => FindGroupMutationPostComparisonExecutionBlockerReason.ReadyNoBlocker,
		};

		return new FindGroupMutationPostComparisonExecutionBlockerRow(
			order,
			gate.Gate,
			gate.Status,
			reason,
			gate.BlocksComparisonExecution,
			gate.Evidence,
			gate.BlocksComparisonExecution
				? gate.Notes
				: "Gate is satisfied; no execution blocker from this envelope row.");
	}

	private static FindGroupMutationPostComparisonExecutionBlockerReason ReasonForReadinessGate(
		FindGroupMutationPostComparisonInputEnvelopeGate gate)
	{
		return gate switch
		{
			FindGroupMutationPostComparisonInputEnvelopeGate.GuardedFixtureResultContract => FindGroupMutationPostComparisonExecutionBlockerReason.MissingGuardedFixtureResultContract,
			FindGroupMutationPostComparisonInputEnvelopeGate.ProjectionMetadata => FindGroupMutationPostComparisonExecutionBlockerReason.MissingProjectionMetadata,
			FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract => FindGroupMutationPostComparisonExecutionBlockerReason.MissingResultContract,
			_ => FindGroupMutationPostComparisonExecutionBlockerReason.MissingReadinessAggregate,
		};
	}
}
