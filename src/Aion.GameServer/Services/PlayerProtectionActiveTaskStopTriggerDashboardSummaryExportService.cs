namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus
{
	Blocked,
	ReadyForRuntimeComparison,
}

public sealed record PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportBlockerRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardArea Area,
	PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus Status,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportReport(
	PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus Status,
	string Summary,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportBlockerRow> Blockers,
	int DashboardRowCount,
	int BlockingRowCount,
	bool HasJavaToolingBlocker,
	bool HasJavaArtifactBlocker,
	bool HasCSharpEmitterBlocker,
	bool HasRuntimeEvidenceBlocker,
	bool HasComparisonExecutionBlocker,
	bool HasKeyProjectionEvidence,
	bool HasJavaHookDetailEvidence,
	int JavaHookDetailRowCount,
	bool HasSerializerFieldContract,
	int SerializerFieldContractRowCount,
	bool HasSerializerTimestampNonParityPolicy,
	bool HasSerializerNestedPayloadPlaceholders,
	bool HasSerializerActionBranchNameTraceContract,
	bool HasSerializerEmotionPayloadContract,
	bool HasSerializerActionPayloadContract,
	bool HasSerializerCallerOriginPayloadContract,
	bool HasSerializerImplementationDesign,
	int SerializerImplementationDesignRowCount,
	bool HasSerializerTopLevelWriterPlan,
	bool HasSerializerRuntimeFactsWriterPlan,
	bool HasSerializerTraceRowCoreWriterPlan,
	bool HasSerializerPlayerSnapshotWriterPlan,
	bool HasSerializerNestedPayloadWriterPlan,
	bool HasSerializerTimestampPolicyWriterPlan,
	bool HasSerializerSourceBreadcrumbWriterPlan,
	bool HasSerializerArtifactFileWriterPlan,
	bool HasSerializerActionBranchNameWriterPlan,
	bool HasSerializerEmotionPayloadWriterPlan,
	bool HasSerializerActionPayloadWriterPlan,
	bool HasSerializerCallerOriginPayloadWriterPlan,
	bool NeedsProtectionArtifactSerializer,
	bool NeedsJavaObserverImplementation,
	bool NeedsJavaSerializerImplementation,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live handoff export for protection stop-trigger prerequisite status.
/// Composes dashboard metadata only; it does not execute Java instrumentation or C# runtime tracing.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportService
{
	public static PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportReport Create(
		PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardReport dashboard,
		PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport? javaHookDetail = null)
	{
		var blockers = dashboard.Rows
			.Where(row => row.BlocksRuntimeComparison)
			.Select((row, index) => new PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportBlockerRow(
				index + 1,
				row.Area,
				row.Status,
				row.Evidence,
				row.Notes))
			.ToArray();

		var hasJavaToolingBlocker = dashboard.HasJavaToolingBlocker
			|| blockers.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaTooling);
		var hasJavaArtifactBlocker = dashboard.NeedsJavaArtifacts
			|| blockers.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingJavaArtifacts);
		var hasCSharpEmitterBlocker = dashboard.NeedsCSharpEmitter
			|| blockers.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingCSharpEmitter);
		var hasRuntimeEvidenceBlocker = dashboard.NeedsRuntimeEvidence
			|| blockers.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedMissingRuntimeEvidence);
		var hasComparisonExecutionBlocker = dashboard.NeedsComparisonExecution
			|| blockers.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerPrerequisiteDashboardStatus.BlockedComparisonNotExecuted);
		var hasJavaHookDetailEvidence = dashboard.HasJavaHookDetailEvidence || javaHookDetail != null;
		var javaHookDetailRowCount = javaHookDetail?.Rows.Count ?? dashboard.JavaHookDetailRowCount;
		var needsProtectionArtifactSerializer = dashboard.NeedsProtectionArtifactSerializer
			|| javaHookDetail?.NeedsProtectionArtifactSerializer == true;
		var needsJavaObserverImplementation = dashboard.NeedsJavaObserverImplementation
			|| javaHookDetail?.NeedsJavaObserverImplementation == true;
		var needsJavaSerializerImplementation = dashboard.NeedsJavaSerializerImplementation;
		var readyForRuntimeComparison = dashboard.ReadyForRuntimeComparison && blockers.Length == 0;
		var status = readyForRuntimeComparison
			? PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus.ReadyForRuntimeComparison
			: PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus.Blocked;

		return new PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportReport(
			status,
			CreateSummary(
				status,
				dashboard.Rows.Count,
				blockers.Length,
				hasJavaToolingBlocker,
				hasJavaArtifactBlocker,
				hasCSharpEmitterBlocker,
				hasRuntimeEvidenceBlocker,
				hasComparisonExecutionBlocker,
				javaHookDetailRowCount,
				dashboard.SerializerFieldContractRowCount,
				needsProtectionArtifactSerializer,
				needsJavaObserverImplementation,
				needsJavaSerializerImplementation),
			blockers,
			dashboard.Rows.Count,
			blockers.Length,
			hasJavaToolingBlocker,
			hasJavaArtifactBlocker,
			hasCSharpEmitterBlocker,
			hasRuntimeEvidenceBlocker,
			hasComparisonExecutionBlocker,
			dashboard.HasKeyProjectionEvidence,
			hasJavaHookDetailEvidence,
			javaHookDetailRowCount,
			dashboard.HasSerializerFieldContract,
			dashboard.SerializerFieldContractRowCount,
			dashboard.HasSerializerTimestampNonParityPolicy,
			dashboard.HasSerializerNestedPayloadPlaceholders,
			dashboard.HasSerializerActionBranchNameTraceContract,
			dashboard.HasSerializerEmotionPayloadContract,
			dashboard.HasSerializerActionPayloadContract,
			dashboard.HasSerializerCallerOriginPayloadContract,
			dashboard.HasSerializerImplementationDesign,
			dashboard.SerializerImplementationDesignRowCount,
			dashboard.HasSerializerTopLevelWriterPlan,
			dashboard.HasSerializerRuntimeFactsWriterPlan,
			dashboard.HasSerializerTraceRowCoreWriterPlan,
			dashboard.HasSerializerPlayerSnapshotWriterPlan,
			dashboard.HasSerializerNestedPayloadWriterPlan,
			dashboard.HasSerializerTimestampPolicyWriterPlan,
			dashboard.HasSerializerSourceBreadcrumbWriterPlan,
			dashboard.HasSerializerArtifactFileWriterPlan,
			dashboard.HasSerializerActionBranchNameWriterPlan,
			dashboard.HasSerializerEmotionPayloadWriterPlan,
			dashboard.HasSerializerActionPayloadWriterPlan,
			dashboard.HasSerializerCallerOriginPayloadWriterPlan,
			needsProtectionArtifactSerializer,
			needsJavaObserverImplementation,
			needsJavaSerializerImplementation,
			readyForRuntimeComparison,
			dashboard.JavaSource,
			IsLive: false);
	}

	private static string CreateSummary(
		PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus status,
		int dashboardRowCount,
		int blockingRowCount,
		bool hasJavaToolingBlocker,
		bool hasJavaArtifactBlocker,
		bool hasCSharpEmitterBlocker,
		bool hasRuntimeEvidenceBlocker,
		bool hasComparisonExecutionBlocker,
		int javaHookDetailRowCount,
		int serializerFieldContractRowCount,
		bool needsProtectionArtifactSerializer,
		bool needsJavaObserverImplementation,
		bool needsJavaSerializerImplementation)
	{
		if (status == PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus.ReadyForRuntimeComparison)
		{
			return $"Ready for runtime comparison: dashboardRows={dashboardRowCount}; blockingRows=0; javaHookRows={javaHookDetailRowCount}; serializerRows={serializerFieldContractRowCount}.";
		}

		var blockerNames = new List<string>();
		if (hasJavaToolingBlocker)
		{
			blockerNames.Add("Java tooling");
		}

		if (hasJavaArtifactBlocker)
		{
			blockerNames.Add("Java artifacts");
		}

		if (hasCSharpEmitterBlocker)
		{
			blockerNames.Add("C# emitter");
		}

		if (hasRuntimeEvidenceBlocker)
		{
			blockerNames.Add("runtime evidence");
		}

		if (hasComparisonExecutionBlocker)
		{
			blockerNames.Add("comparison execution");
		}

		if (needsProtectionArtifactSerializer)
		{
			blockerNames.Add("protection artifact serializer");
		}

		if (needsJavaObserverImplementation)
		{
			blockerNames.Add("Java observer implementation");
		}

		if (needsJavaSerializerImplementation)
		{
			blockerNames.Add("Java serializer implementation");
		}

		var blockerSummary = blockerNames.Count == 0
			? "unspecified blockers"
			: string.Join(", ", blockerNames);

		return $"Blocked: dashboardRows={dashboardRowCount}; blockingRows={blockingRowCount}; javaHookRows={javaHookDetailRowCount}; serializerRows={serializerFieldContractRowCount}; blockers={blockerSummary}.";
	}
}
