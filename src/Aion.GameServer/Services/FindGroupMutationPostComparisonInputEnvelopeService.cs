namespace Aion.GameServer.Services;

public enum FindGroupMutationPostComparisonInputEnvelopeStatus
{
	BlockedMissingJavaRows,
	BlockedMissingLiveCSharpRows,
	BlockedMissingReadiness,
	ReadyForComparisonExecution,
}

public enum FindGroupMutationPostComparisonInputEnvelopeGate
{
	JavaRows,
	CSharpRows,
	ProjectionMetadata,
	ReadinessAggregate,
	ResultContract,
}

public enum FindGroupMutationPostComparisonInputEnvelopeGateStatus
{
	SatisfiedByShapeValidJavaRows,
	SatisfiedByLiveCSharpRows,
	SatisfiedByNonLiveMetadata,
	SatisfiedByReadyContract,
	BlockedMissingJavaRows,
	BlockedMissingLiveCSharpRows,
	BlockedMissingReadiness,
}

public sealed record FindGroupMutationPostComparisonInputEnvelopeRowReference(
	FindGroupDirectPacketMutationPostTraceSource TraceSource,
	int Action,
	string MutationKind,
	int PostedSystemMessageId,
	int RefreshedListAction,
	bool IsShapeValid,
	bool IsLiveEvidence,
	string Evidence);

public sealed record FindGroupMutationPostComparisonInputEnvelopeGateRow(
	int Order,
	FindGroupMutationPostComparisonInputEnvelopeGate Gate,
	FindGroupMutationPostComparisonInputEnvelopeGateStatus Status,
	bool BlocksComparisonExecution,
	string Evidence,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record FindGroupMutationPostComparisonInputEnvelope(
	FindGroupMutationPostComparisonInputEnvelopeStatus Status,
	IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeGateRow> Gates,
	IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeRowReference> JavaRows,
	IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeRowReference> CSharpRows,
	bool HasActionTwoJavaRow,
	bool HasActionSixJavaRow,
	bool HasActionTwoLiveCSharpRow,
	bool HasActionSixLiveCSharpRow,
	bool HasProjectionMetadata,
	bool HasReadinessAggregate,
	bool HasResultContract,
	bool ReadyForComparisonExecution,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: guarded input envelope for future CM_FIND_GROUP action 2/6
/// mutation-post Java/C# row comparison. It collects row references and prerequisite
/// contracts, but does not compare rows.
/// </summary>
public static class FindGroupMutationPostComparisonInputEnvelopeService
{
	public static FindGroupMutationPostComparisonInputEnvelope Create(
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifacts = null,
		IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport>? csharpRows = null,
		FindGroupMutationPostComparisonKeyProjectionMetadata? keyProjection = null,
		FindGroupMutationPostTraceRowReadinessAggregate? readiness = null,
		FindGroupMutationPostComparisonExecutionResultContract? resultContract = null)
	{
		javaArtifacts ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create();
		csharpRows ??= [];
		keyProjection ??= FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		readiness ??= FindGroupMutationPostTraceRowReadinessAggregateService.Create();
		resultContract ??= FindGroupMutationPostComparisonExecutionResultContractService.Create(keyProjection, readiness);

		var javaRowRefs = CreateJavaRowReferences(javaArtifacts);
		var csharpRowRefs = csharpRows.Select(CreateCSharpRowReference).ToArray();
		var gates = new List<FindGroupMutationPostComparisonInputEnvelopeGateRow>();
		AddJavaRows(gates, javaArtifacts, javaRowRefs);
		AddCSharpRows(gates, csharpRowRefs);
		AddProjectionMetadata(gates, keyProjection);
		AddReadiness(gates, readiness);
		AddResultContract(gates, resultContract);

		var gateArray = gates.ToArray();
		var status = DetermineStatus(gateArray);

		return new FindGroupMutationPostComparisonInputEnvelope(
			status,
			gateArray,
			javaRowRefs,
			csharpRowRefs,
			HasActionTwoJavaRow: javaRowRefs.Any(row => row.Action == 2 && row.IsShapeValid),
			HasActionSixJavaRow: javaRowRefs.Any(row => row.Action == 6 && row.IsShapeValid),
			HasActionTwoLiveCSharpRow: csharpRowRefs.Any(row => row.Action == 2 && row.IsLiveEvidence),
			HasActionSixLiveCSharpRow: csharpRowRefs.Any(row => row.Action == 6 && row.IsLiveEvidence),
			HasProjectionMetadata: keyProjection.Fields.Count > 0,
			HasReadinessAggregate: readiness.Rows.Count > 0 || readiness.ReadyForRuntimeComparison,
			HasResultContract: resultContract.Fields.Count > 0,
			ReadyForComparisonExecution: status == FindGroupMutationPostComparisonInputEnvelopeStatus.ReadyForComparisonExecution,
			keyProjection.TraceName,
			keyProjection.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostComparisonInputEnvelopeStatus DetermineStatus(
		IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates)
	{
		if (gates.Any(gate => gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingJavaRows))
			return FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingJavaRows;
		if (gates.Any(gate => gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows))
			return FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows;
		if (gates.Any(gate => gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness))
			return FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingReadiness;

		return FindGroupMutationPostComparisonInputEnvelopeStatus.ReadyForComparisonExecution;
	}

	private static IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeRowReference> CreateJavaRowReferences(
		FindGroupMutationPostJavaTraceArtifactDirectoryReport javaArtifacts)
	{
		return javaArtifacts.Files
			.Where(file => file.ValidationReport?.Metadata != null)
			.SelectMany(file => file.ValidationReport!.Metadata!.TraceRows.Select(row => new FindGroupMutationPostComparisonInputEnvelopeRowReference(
				FindGroupDirectPacketMutationPostTraceSource.Java,
				row.Action,
				row.MutationKind,
				row.PostedSystemMessageId,
				row.RefreshedListAction,
				IsShapeValid: file.Status == FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
				IsLiveEvidence: false,
				$"path={file.Path}; status={file.Status}; source={row.TraceSource}")))
			.ToArray();
	}

	private static FindGroupMutationPostComparisonInputEnvelopeRowReference CreateCSharpRowReference(
		FindGroupDirectPacketMutationPostBoundaryTraceExport row)
	{
		var isLiveEvidence = row.TraceSource == FindGroupDirectPacketMutationPostTraceSource.CSharp
			&& row.BoundaryAccepted
			&& row.ExecutorInvokedFromBoundary
			&& row.RegistrySendsObservedInOrder;

		return new FindGroupMutationPostComparisonInputEnvelopeRowReference(
			row.TraceSource,
			row.Action,
			row.MutationKind.ToString(),
			row.PostedSystemMessageId,
			row.RefreshedListAction,
			IsShapeValid: true,
			isLiveEvidence,
			$"boundaryAccepted={row.BoundaryAccepted}; executor={row.ExecutorInvokedFromBoundary}; registry={row.RegistrySendsObservedInOrder}; source={row.TraceSource}");
	}

	private static void AddJavaRows(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport javaArtifacts,
		IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeRowReference> javaRows)
	{
		var hasExpectedRows = javaRows.Any(row => row.Action == 2 && row.IsShapeValid)
			&& javaRows.Any(row => row.Action == 6 && row.IsShapeValid);

		Add(gates,
			FindGroupMutationPostComparisonInputEnvelopeGate.JavaRows,
			hasExpectedRows
				? FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByShapeValidJavaRows
				: FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingJavaRows,
			blocks: !hasExpectedRows,
			$"directoryStatus={javaArtifacts.Status}; rowActions={string.Join("/", javaRows.Select(row => row.Action).Distinct().Order())}; shapeValidRows={javaRows.Count(row => row.IsShapeValid)}",
			"FindGroupService.addRecruitment/addApplication generated trace artifacts",
			"FindGroupMutationPostJavaTraceArtifactDirectoryReport",
			"Envelope requires shape-valid Java rows for actions 2 and 6 before comparison inputs can be complete.");
	}

	private static void AddCSharpRows(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		IReadOnlyList<FindGroupMutationPostComparisonInputEnvelopeRowReference> csharpRows)
	{
		var hasExpectedRows = csharpRows.Any(row => row.Action == 2 && row.IsLiveEvidence)
			&& csharpRows.Any(row => row.Action == 6 && row.IsLiveEvidence);

		Add(gates,
			FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows,
			hasExpectedRows
				? FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByLiveCSharpRows
				: FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows,
			blocks: !hasExpectedRows,
			$"rowActions={string.Join("/", csharpRows.Select(row => row.Action).Distinct().Order())}; liveRows={csharpRows.Count(row => row.IsLiveEvidence)}",
			"CM_FIND_GROUP.runImpl action 2/6 live boundary execution",
			"GameServerConnection.ProcessPacketAsync live CmFindGroup trace rows",
			"C# rows must be live boundary evidence with executor and registry observation, not disabled sample projections.");
	}

	private static void AddProjectionMetadata(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		FindGroupMutationPostComparisonKeyProjectionMetadata keyProjection)
	{
		var hasMetadata = keyProjection.Fields.Count > 0 && keyProjection.Actions.SequenceEqual([2, 6]);
		Add(gates,
			FindGroupMutationPostComparisonInputEnvelopeGate.ProjectionMetadata,
			hasMetadata
				? FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness,
			blocks: !hasMetadata,
			$"fields={keyProjection.Fields.Count}; equalityFields={keyProjection.EqualityProjectionFields.Count}; ignoredRuntimeFields={string.Join("/", keyProjection.IgnoredRuntimeFields)}",
			keyProjection.JavaSource,
			"FindGroupMutationPostComparisonKeyProjectionMetadata",
			"Projection metadata defines the row keys but does not compare rows.");
	}

	private static void AddReadiness(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		FindGroupMutationPostTraceRowReadinessAggregate readiness)
	{
		Add(gates,
			FindGroupMutationPostComparisonInputEnvelopeGate.ReadinessAggregate,
			readiness.ReadyForRuntimeComparison
				? FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness,
			blocks: !readiness.ReadyForRuntimeComparison,
			$"status={readiness.Status}; ready={readiness.ReadyForRuntimeComparison}; needsJava={readiness.NeedsGeneratedJavaArtifacts}; needsCSharp={readiness.NeedsCSharpLiveRows}; needsRegistry={readiness.NeedsRegistryObservation}",
			readiness.JavaSource,
			"FindGroupMutationPostTraceRowReadinessAggregate",
			"Readiness must be green before the envelope can be handed to a comparison executor.");
	}

	private static void AddResultContract(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		FindGroupMutationPostComparisonExecutionResultContract resultContract)
	{
		Add(gates,
			FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract,
			resultContract.ReadyForComparisonExecution
				? FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByReadyContract
				: FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness,
			blocks: !resultContract.ReadyForComparisonExecution,
			$"status={resultContract.Status}; fields={resultContract.Fields.Count}; differenceKinds={string.Join("/", resultContract.DifferenceKinds)}",
			resultContract.JavaSource,
			"FindGroupMutationPostComparisonExecutionResultContract",
			"Result contract must be ready before comparison can emit mismatch reports.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostComparisonInputEnvelopeGateRow> gates,
		FindGroupMutationPostComparisonInputEnvelopeGate gate,
		FindGroupMutationPostComparisonInputEnvelopeGateStatus status,
		bool blocks,
		string evidence,
		string javaSource,
		string csharpTarget,
		string notes)
	{
		gates.Add(new FindGroupMutationPostComparisonInputEnvelopeGateRow(
			gates.Count + 1,
			gate,
			status,
			blocks,
			evidence,
			javaSource,
			csharpTarget,
			notes));
	}
}
