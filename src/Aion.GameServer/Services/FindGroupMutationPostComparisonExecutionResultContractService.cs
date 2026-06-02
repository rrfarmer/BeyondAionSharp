namespace Aion.GameServer.Services;

public enum FindGroupMutationPostComparisonExecutionResultContractStatus
{
	BlockedMissingTraceRows,
	BlockedMissingPreflightReadiness,
	ReadyForComparisonExecution,
}

public enum FindGroupMutationPostComparisonDifferenceKind
{
	CompatibilityGateMismatch,
	RowIdentityMismatch,
	MutationStateMismatch,
	DirectPacketMismatch,
	RegistryObservationMismatch,
	SideEffectGuardMismatch,
	RuntimeOnlyIgnored,
}

public enum FindGroupMutationPostComparisonDifferenceFieldStatus
{
	RequiredForDifferenceReport,
	IgnoredForEquality,
}

public sealed record FindGroupMutationPostComparisonExecutionResultFieldContract(
	int Order,
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string FieldName,
	FindGroupMutationPostComparisonKeyFieldRole ProjectionRole,
	FindGroupMutationPostComparisonDifferenceKind DifferenceKind,
	FindGroupMutationPostComparisonDifferenceFieldStatus Status,
	string DifferenceReportRule,
	string JavaSource,
	string Notes);

public sealed record FindGroupMutationPostComparisonExecutionResultActionContract(
	int Action,
	FindGroupDirectPacketMutationPostTraceMutationKind MutationKind,
	string JavaMethod,
	int ExpectedPostedSystemMessageId,
	int ExpectedRefreshedListAction,
	string RowIdentityFields,
	string Notes);

public sealed record FindGroupMutationPostComparisonExecutionResultContract(
	FindGroupMutationPostComparisonExecutionResultContractStatus Status,
	IReadOnlyList<FindGroupMutationPostComparisonExecutionResultActionContract> Actions,
	IReadOnlyList<FindGroupMutationPostComparisonExecutionResultFieldContract> Fields,
	IReadOnlyList<FindGroupMutationPostComparisonDifferenceKind> DifferenceKinds,
	IReadOnlyList<string> EqualityProjectionFields,
	IReadOnlyList<string> IgnoredRuntimeFields,
	bool RequiresGeneratedJavaTraceRows,
	bool RequiresLiveCSharpTraceRows,
	bool RequiresRegistryObservation,
	bool RequiresPreflightReady,
	bool ReadyForComparisonExecution,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: result contract for future CM_FIND_GROUP action 2/6
/// mutation-post row comparison. This defines how differences must be reported;
/// it does not execute a Java/C# row comparison.
/// </summary>
public static class FindGroupMutationPostComparisonExecutionResultContractService
{
	public static FindGroupMutationPostComparisonExecutionResultContract Create(
		FindGroupMutationPostComparisonKeyProjectionMetadata? keyProjection = null,
		FindGroupMutationPostTraceRowReadinessAggregate? readiness = null)
	{
		keyProjection ??= FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		readiness ??= FindGroupMutationPostTraceRowReadinessAggregateService.Create();

		var schema = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSchema();
		var fieldRows = keyProjection.Fields
			.Select((field, index) => CreateFieldContract(index + 1, field))
			.ToArray();
		var actionRows = schema.SupportedActions
			.Select(action => new FindGroupMutationPostComparisonExecutionResultActionContract(
				action.Action,
				action.MutationKind,
				action.JavaMethod,
				action.PostedSystemMessageId,
				action.RefreshedShowListAction,
				string.Join("/", keyProjection.RowIdentityFields),
				"Future comparison must report one Java row and one C# row per action identity before evaluating equality fields."))
			.ToArray();

		var status = DetermineStatus(readiness);

		return new FindGroupMutationPostComparisonExecutionResultContract(
			status,
			actionRows,
			fieldRows,
			fieldRows.Select(row => row.DifferenceKind).Distinct().ToArray(),
			keyProjection.EqualityProjectionFields,
			keyProjection.IgnoredRuntimeFields,
			RequiresGeneratedJavaTraceRows: readiness.NeedsGeneratedJavaArtifacts || keyProjection.RequiresGeneratedJavaTraceRows,
			RequiresLiveCSharpTraceRows: readiness.NeedsCSharpLiveRows || keyProjection.RequiresLiveCSharpTraceRows,
			RequiresRegistryObservation: readiness.NeedsRegistryObservation || keyProjection.RequiresRegistryObservation,
			RequiresPreflightReady: !readiness.ReadyForRuntimeComparison,
			ReadyForComparisonExecution: status == FindGroupMutationPostComparisonExecutionResultContractStatus.ReadyForComparisonExecution,
			keyProjection.TraceName,
			keyProjection.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostComparisonExecutionResultContractStatus DetermineStatus(
		FindGroupMutationPostTraceRowReadinessAggregate readiness)
	{
		if (readiness.NeedsGeneratedJavaArtifacts || readiness.NeedsCSharpLiveRows)
			return FindGroupMutationPostComparisonExecutionResultContractStatus.BlockedMissingTraceRows;

		if (!readiness.ReadyForRuntimeComparison)
			return FindGroupMutationPostComparisonExecutionResultContractStatus.BlockedMissingPreflightReadiness;

		return FindGroupMutationPostComparisonExecutionResultContractStatus.ReadyForComparisonExecution;
	}

	private static FindGroupMutationPostComparisonExecutionResultFieldContract CreateFieldContract(
		int order,
		FindGroupMutationPostComparisonKeyProjectionFieldRow field)
	{
		var differenceKind = field.Role switch
		{
			FindGroupMutationPostComparisonKeyFieldRole.CompatibilityGate => FindGroupMutationPostComparisonDifferenceKind.CompatibilityGateMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.RowIdentity => FindGroupMutationPostComparisonDifferenceKind.RowIdentityMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.MutationState => FindGroupMutationPostComparisonDifferenceKind.MutationStateMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.DirectPacketShape => FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.RegistryObservation => FindGroupMutationPostComparisonDifferenceKind.RegistryObservationMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.SideEffectGuard => FindGroupMutationPostComparisonDifferenceKind.SideEffectGuardMismatch,
			FindGroupMutationPostComparisonKeyFieldRole.RuntimeOnly => FindGroupMutationPostComparisonDifferenceKind.RuntimeOnlyIgnored,
			_ => FindGroupMutationPostComparisonDifferenceKind.RowIdentityMismatch,
		};
		var status = field.Status == FindGroupMutationPostComparisonKeyFieldStatus.IgnoredForEquality
			? FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality
			: FindGroupMutationPostComparisonDifferenceFieldStatus.RequiredForDifferenceReport;

		return new FindGroupMutationPostComparisonExecutionResultFieldContract(
			order,
			field.Action,
			field.MutationKind,
			field.FieldName,
			field.Role,
			differenceKind,
			status,
			CreateDifferenceRule(field, differenceKind, status),
			field.JavaSource,
			field.Notes);
	}

	private static string CreateDifferenceRule(
		FindGroupMutationPostComparisonKeyProjectionFieldRow field,
		FindGroupMutationPostComparisonDifferenceKind differenceKind,
		FindGroupMutationPostComparisonDifferenceFieldStatus status)
	{
		if (status == FindGroupMutationPostComparisonDifferenceFieldStatus.IgnoredForEquality)
			return $"Do not compare {field.FieldName} for equality; include it only as runtime context if a mismatch report is emitted.";

		return $"When projected Java and C# {field.FieldName} differ, emit {differenceKind} with action={field.Action}, mutationKind={field.MutationKind}, fieldName={field.FieldName}, javaValue, csharpValue, and Java source evidence.";
	}
}
