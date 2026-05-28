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
	bool NeedsProtectionArtifactSerializer,
	bool NeedsJavaObserverImplementation,
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
		var needsProtectionArtifactSerializer = javaHookDetail?.NeedsProtectionArtifactSerializer == true;
		var needsJavaObserverImplementation = javaHookDetail?.NeedsJavaObserverImplementation == true;
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
				javaHookDetail?.Rows.Count ?? 0,
				needsProtectionArtifactSerializer,
				needsJavaObserverImplementation),
			blockers,
			dashboard.Rows.Count,
			blockers.Length,
			hasJavaToolingBlocker,
			hasJavaArtifactBlocker,
			hasCSharpEmitterBlocker,
			hasRuntimeEvidenceBlocker,
			hasComparisonExecutionBlocker,
			dashboard.HasKeyProjectionEvidence,
			javaHookDetail != null,
			javaHookDetail?.Rows.Count ?? 0,
			needsProtectionArtifactSerializer,
			needsJavaObserverImplementation,
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
		bool needsProtectionArtifactSerializer,
		bool needsJavaObserverImplementation)
	{
		if (status == PlayerProtectionActiveTaskStopTriggerDashboardSummaryExportStatus.ReadyForRuntimeComparison)
		{
			return $"Ready for runtime comparison: dashboardRows={dashboardRowCount}; blockingRows=0; javaHookRows={javaHookDetailRowCount}.";
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

		var blockerSummary = blockerNames.Count == 0
			? "unspecified blockers"
			: string.Join(", ", blockerNames);

		return $"Blocked: dashboardRows={dashboardRowCount}; blockingRows={blockingRowCount}; javaHookRows={javaHookDetailRowCount}; blockers={blockerSummary}.";
	}
}
