namespace Aion.GameServer.Services;

public enum FindGroupMutationPostArtifactComparisonPreflightStatus
{
	BlockedMissingPrerequisite,
	BlockedMissingJavaArtifacts,
	BlockedMissingLiveCSharpRows,
	BlockedMissingRegistryObservation,
	BlockedComparisonNotExecuted,
	BlockedComparisonNotMatching,
	Ready,
}

public enum FindGroupMutationPostArtifactComparisonPreflightGate
{
	JavaArtifactTargets,
	JavaArtifactReader,
	CSharpLiveTraceRows,
	ComparisonKeyProjection,
	RegistryObservation,
	ComparisonExecution,
}

public enum FindGroupMutationPostArtifactComparisonPreflightGateStatus
{
	SatisfiedByNonLiveMetadata,
	SatisfiedByShapeValidArtifact,
	SatisfiedByLiveEvidence,
	BlockedMissingPrerequisite,
	BlockedMissingJavaArtifact,
	BlockedMissingLiveCSharpRows,
	BlockedMissingRegistryObservation,
	BlockedComparisonNotExecuted,
	BlockedComparisonNotMatching,
}

public sealed record FindGroupMutationPostArtifactComparisonPreflightRow(
	int Order,
	FindGroupMutationPostArtifactComparisonPreflightGate Gate,
	FindGroupMutationPostArtifactComparisonPreflightGateStatus Status,
	bool BlocksRuntimeComparison,
	string Evidence,
	string JavaSource,
	string CSharpTarget,
	string Notes);

public sealed record FindGroupMutationPostArtifactComparisonPreflightReport(
	FindGroupMutationPostArtifactComparisonPreflightStatus Status,
	IReadOnlyList<FindGroupMutationPostArtifactComparisonPreflightRow> Rows,
	bool HasJavaArtifactTargets,
	bool HasShapeValidJavaArtifacts,
	bool HasComparisonKeyProjection,
	bool HasLiveCSharpTraceRows,
	bool HasRegistryObservation,
	bool HasComparisonExecution,
	bool HasMatchingComparisonResult,
	bool NeedsGeneratedJavaArtifacts,
	bool NeedsLiveCSharpTraceRows,
	bool NeedsRegistryObservation,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string ArtifactRoot,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: guarded preflight for future CM_FIND_GROUP action 2/6
/// mutation-post artifact comparison. This ties Java artifacts, C# live rows, key
/// projection metadata, and comparison execution gates together without comparing rows.
/// </summary>
public static class FindGroupMutationPostArtifactComparisonPreflightService
{
	public static FindGroupMutationPostArtifactComparisonPreflightReport Create(
		FindGroupMutationPostJavaTraceArtifactFileReport? fileTargets = null,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifacts = null,
		FindGroupMutationPostComparisonKeyProjectionMetadata? keyProjection = null,
		FindGroupMutationPostRegistryObservationTraceContract? registryContract = null,
		bool hasLiveCSharpTraceRows = false,
		bool hasRegistryObservation = false,
		bool comparisonExecuted = false,
		bool hasMatchingComparisonResult = false)
	{
		fileTargets ??= FindGroupMutationPostJavaTraceArtifactFileReportService.Create();
		javaArtifacts ??= FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(fileTargets.ArtifactRoot);
		keyProjection ??= FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		registryContract ??= FindGroupMutationPostRegistryObservationTraceContractService.Create();

		var rows = new List<FindGroupMutationPostArtifactComparisonPreflightRow>();
		AddJavaArtifactTargets(rows, fileTargets);
		AddJavaArtifactReader(rows, javaArtifacts);
		AddCSharpLiveRows(rows, hasLiveCSharpTraceRows);
		AddComparisonKeyProjection(rows, keyProjection);
		AddRegistryObservation(rows, registryContract, hasRegistryObservation);
		AddComparisonExecution(rows, javaArtifacts, keyProjection, hasLiveCSharpTraceRows, hasRegistryObservation, comparisonExecuted, hasMatchingComparisonResult);

		var rowArray = rows.ToArray();
		var status = DetermineStatus(rowArray);

		return new FindGroupMutationPostArtifactComparisonPreflightReport(
			status,
			rowArray,
			HasJavaArtifactTargets: fileTargets.HasActionTwoTarget && fileTargets.HasActionSixTarget && fileTargets.UsesStableTraceName,
			HasShapeValidJavaArtifacts: javaArtifacts.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			HasComparisonKeyProjection: keyProjection.Fields.Count > 0 && keyProjection.Actions.SequenceEqual([2, 6]),
			HasLiveCSharpTraceRows: hasLiveCSharpTraceRows,
			HasRegistryObservation: hasRegistryObservation,
			HasComparisonExecution: comparisonExecuted,
			HasMatchingComparisonResult: hasMatchingComparisonResult,
			NeedsGeneratedJavaArtifacts: rowArray.Any(row => row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactReader && row.BlocksRuntimeComparison),
			NeedsLiveCSharpTraceRows: rowArray.Any(row => row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.CSharpLiveTraceRows && row.BlocksRuntimeComparison),
			NeedsRegistryObservation: rowArray.Any(row => row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.RegistryObservation && row.BlocksRuntimeComparison),
			NeedsComparisonExecution: rowArray.Any(row => row.Gate == FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonExecution && row.BlocksRuntimeComparison),
			ReadyForRuntimeComparison: status == FindGroupMutationPostArtifactComparisonPreflightStatus.Ready,
			fileTargets.ArtifactRoot,
			fileTargets.Files.FirstOrDefault()?.ExpectedTraceName ?? "cm-find-group-direct-mutation-post-boundary",
			fileTargets.JavaSource,
			IsLive: false);
	}

	private static FindGroupMutationPostArtifactComparisonPreflightStatus DetermineStatus(
		IReadOnlyList<FindGroupMutationPostArtifactComparisonPreflightRow> rows)
	{
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingPrerequisite;
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingJavaArtifact))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingJavaArtifacts;
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingLiveCSharpRows))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingLiveCSharpRows;
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingRegistryObservation))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedMissingRegistryObservation;
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedComparisonNotExecuted))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedComparisonNotExecuted;
		if (rows.Any(row => row.Status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedComparisonNotMatching))
			return FindGroupMutationPostArtifactComparisonPreflightStatus.BlockedComparisonNotMatching;

		return FindGroupMutationPostArtifactComparisonPreflightStatus.Ready;
	}

	private static void AddJavaArtifactTargets(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostJavaTraceArtifactFileReport? fileTargets)
	{
		if (fileTargets == null)
		{
			Add(rows,
				FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactTargets,
				FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
				blocks: true,
				"missing Java artifact file target report",
				"CM_FIND_GROUP action 2/6 generated Java trace artifacts",
				"FindGroupMutationPostJavaTraceArtifactFileReportService",
				"Expected Java artifact paths must be known before comparison preflight can proceed.");
			return;
		}

		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactTargets,
			fileTargets.HasActionTwoTarget && fileTargets.HasActionSixTarget && fileTargets.UsesStableTraceName
				? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
			blocks: !(fileTargets.HasActionTwoTarget && fileTargets.HasActionSixTarget && fileTargets.UsesStableTraceName),
			$"artifactRoot={fileTargets.ArtifactRoot}; pattern={fileTargets.FileNamePattern}; actions={string.Join("/", fileTargets.Files.Select(file => file.Action))}; traceNameStable={fileTargets.UsesStableTraceName}",
			fileTargets.JavaSource,
			"FindGroupMutationPostJavaTraceArtifactFileReport",
			"Action 2 and 6 Java artifact file targets exist as non-live metadata only.");
	}

	private static void AddJavaArtifactReader(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport? javaArtifacts)
	{
		if (javaArtifacts == null)
		{
			Add(rows,
				FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactReader,
				FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
				blocks: true,
				"missing Java artifact reader report",
				"generated Java action 2/6 mutation-post trace rows",
				"FindGroupMutationPostJavaTraceArtifactDirectoryReportService",
				"Generated Java artifact reader status must be known before comparison preflight can proceed.");
			return;
		}

		var status = javaArtifacts.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid
			? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByShapeValidArtifact
			: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingJavaArtifact;

		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.JavaArtifactReader,
			status,
			blocks: status != FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByShapeValidArtifact,
			$"status={javaArtifacts.Status}; files={javaArtifacts.Files.Count}; generated={javaArtifacts.HasGeneratedJavaArtifacts}; allExpected={javaArtifacts.HasAllExpectedFiles}; shapeValid={javaArtifacts.HasOnlyShapeValidArtifacts}",
			"FindGroupService.addRecruitment/addApplication generated trace artifacts",
			"FindGroupMutationPostJavaTraceArtifactDirectoryReport",
			status == FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByShapeValidArtifact
				? "Java artifacts are shape-valid only; live C# rows and comparison execution are still required."
				: "Generated Java mutation-post artifacts are missing or invalid.");
	}

	private static void AddCSharpLiveRows(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		bool hasLiveCSharpTraceRows)
	{
		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.CSharpLiveTraceRows,
			hasLiveCSharpTraceRows
				? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByLiveEvidence
				: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingLiveCSharpRows,
			blocks: !hasLiveCSharpTraceRows,
			$"hasLiveCSharpTraceRows={hasLiveCSharpTraceRows}",
			"CM_FIND_GROUP.runImpl action 2/6 live boundary execution",
			"GameServerConnection.ProcessPacketAsync live CmFindGroup trace rows",
			"Live C# rows must come from the real boundary, not disabled boundary-plan projection.");
	}

	private static void AddComparisonKeyProjection(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostComparisonKeyProjectionMetadata? keyProjection)
	{
		if (keyProjection == null)
		{
			Add(rows,
				FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonKeyProjection,
				FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
				blocks: true,
				"missing comparison key projection metadata",
				"CM_FIND_GROUP action 2/6 mutation-post comparison keys",
				"FindGroupMutationPostComparisonKeyProjectionMetadataService",
				"Comparison projection keys must be defined before Java/C# rows can be compared deterministically.");
			return;
		}

		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonKeyProjection,
			keyProjection.Fields.Count > 0 && keyProjection.Actions.SequenceEqual([2, 6])
				? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByNonLiveMetadata
				: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
			blocks: !(keyProjection.Fields.Count > 0 && keyProjection.Actions.SequenceEqual([2, 6])),
			$"fields={keyProjection.Fields.Count}; equalityFields={keyProjection.EqualityProjectionFields.Count}; ignoredRuntimeFields={string.Join("/", keyProjection.IgnoredRuntimeFields)}",
			keyProjection.JavaSource,
			"FindGroupMutationPostComparisonKeyProjectionMetadata",
			"Projection metadata exists, but it does not compare rows or prove parity.");
	}

	private static void AddRegistryObservation(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostRegistryObservationTraceContract? registryContract,
		bool hasRegistryObservation)
	{
		if (registryContract == null)
		{
			Add(rows,
				FindGroupMutationPostArtifactComparisonPreflightGate.RegistryObservation,
				FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingPrerequisite,
				blocks: true,
				"missing registry-observation trace contract",
				"PacketSendUtility.sendPacket order in FindGroupService.addRecruitment/addApplication",
				"FindGroupMutationPostRegistryObservationTraceContractService",
				"Registry observation requirements must be known before live rows can be trusted.");
			return;
		}

		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.RegistryObservation,
			hasRegistryObservation
				? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByLiveEvidence
				: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedMissingRegistryObservation,
			blocks: !hasRegistryObservation,
			$"hasRegistryObservation={hasRegistryObservation}; requirements={registryContract.Requirements.Count}; requiresOrderedSends={registryContract.RequiresRegistrySendsObservedInOrder}",
			registryContract.JavaSource,
			"FindGroupMutationPostRegistryObservationTraceContract",
			"Live registry observation must prove posted system message before refreshed list for actions 2 and 6.");
	}

	private static void AddComparisonExecution(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostJavaTraceArtifactDirectoryReport javaArtifacts,
		FindGroupMutationPostComparisonKeyProjectionMetadata keyProjection,
		bool hasLiveCSharpTraceRows,
		bool hasRegistryObservation,
		bool comparisonExecuted,
		bool hasMatchingComparisonResult)
	{
		var prerequisitesReady = javaArtifacts.Status == FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid
			&& keyProjection.Fields.Count > 0
			&& hasLiveCSharpTraceRows
			&& hasRegistryObservation;
		var status = !comparisonExecuted
			? FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedComparisonNotExecuted
			: hasMatchingComparisonResult
				? FindGroupMutationPostArtifactComparisonPreflightGateStatus.SatisfiedByLiveEvidence
				: FindGroupMutationPostArtifactComparisonPreflightGateStatus.BlockedComparisonNotMatching;

		Add(rows,
			FindGroupMutationPostArtifactComparisonPreflightGate.ComparisonExecution,
			status,
			blocks: !comparisonExecuted || !hasMatchingComparisonResult,
			$"prerequisitesReady={prerequisitesReady}; comparisonExecuted={comparisonExecuted}; matchingProjectedRows={hasMatchingComparisonResult}",
			"generated Java mutation-post artifacts",
			"future deterministic FindGroup mutation-post artifact comparison",
			"Verified parity cannot be claimed until projected Java/C# rows are compared and match.");
	}

	private static void Add(
		ICollection<FindGroupMutationPostArtifactComparisonPreflightRow> rows,
		FindGroupMutationPostArtifactComparisonPreflightGate gate,
		FindGroupMutationPostArtifactComparisonPreflightGateStatus status,
		bool blocks,
		string evidence,
		string javaSource,
		string csharpTarget,
		string notes)
	{
		rows.Add(new FindGroupMutationPostArtifactComparisonPreflightRow(
			rows.Count + 1,
			gate,
			status,
			blocks,
			evidence,
			javaSource,
			csharpTarget,
			notes));
	}
}
