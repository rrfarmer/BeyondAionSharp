namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea
{
	JavaTraceArtifacts,
	CSharpRuntimeTraceOutput,
	ComparisonExecution,
}

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingJavaArtifact,
	BlockedInvalidJavaArtifact,
	BlockedMissingCSharpRuntimeTrace,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport(
	IReadOnlyList<string> Scenarios,
	bool HasLivePacketHooks,
	bool ReadyForRuntimeComparison,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea Area,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus Status,
	bool BlocksRuntimeComparison,
	string JavaSource,
	string CSharpTarget,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow> Rows,
	bool HasJavaArtifactDirectoryReport,
	bool HasShapeValidJavaArtifacts,
	bool HasCSharpRuntimeTraceReport,
	bool NeedsJavaArtifacts,
	bool NeedsCSharpRuntimeTrace,
	bool NeedsExecutedComparison,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live contract for the future comparison between generated Java
/// protection stop-trigger artifacts and C# runtime traces. This report only models blockers.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractService
{
	public static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport Create(
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifactDirectoryReport,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpRuntimeTraceReport = null)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow>();

		AddJavaTraceArtifactRow(rows, javaArtifactDirectoryReport);
		AddCSharpRuntimeTraceRow(rows, csharpRuntimeTraceReport);
		AddComparisonExecutionRow(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractReport(
			rowArray,
			HasJavaArtifactDirectoryReport: javaArtifactDirectoryReport != null,
			HasShapeValidJavaArtifacts: javaArtifactDirectoryReport?.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			HasCSharpRuntimeTraceReport: csharpRuntimeTraceReport != null,
			NeedsJavaArtifacts: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts && row.BlocksRuntimeComparison),
			NeedsCSharpRuntimeTrace: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput && row.BlocksRuntimeComparison),
			NeedsExecutedComparison: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.ComparisonExecution && row.BlocksRuntimeComparison),
			ReadyForRuntimeComparison: false,
			"Java generated protection stop-trigger artifacts and future C# runtime traces",
			IsLive: false);
	}

	private static void AddJavaTraceArtifactRow(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifactDirectoryReport)
	{
		if (javaArtifactDirectoryReport == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingJavaArtifact,
				blocks: true,
				"CM_MOVE/CM_MOVE_IN_AIR/action packets; PlayerController; CreatureController; TeleportService",
				"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReportService",
				"no Java artifact directory report supplied",
				"Generated Java trace artifacts must exist and pass schema-v1 validation before C# runtime traces can be compared.");
			return;
		}

		if (javaArtifactDirectoryReport.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.SatisfiedByNonLiveMetadata,
				blocks: false,
				"generated Java protection stop-trigger trace artifacts",
				"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport",
				$"status={javaArtifactDirectoryReport.Status}; files={javaArtifactDirectoryReport.Files.Count}",
				"Java artifact files are shape-valid only; this does not prove that C# runtime output matches them.");
			return;
		}

		var status = javaArtifactDirectoryReport.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts
			? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedInvalidJavaArtifact
			: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingJavaArtifact;

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.JavaTraceArtifacts,
			status,
			blocks: true,
			"generated Java protection stop-trigger trace artifacts",
			"PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport",
			$"status={javaArtifactDirectoryReport.Status}; files={javaArtifactDirectoryReport.Files.Count}",
			javaArtifactDirectoryReport.Notes);
	}

	private static void AddCSharpRuntimeTraceRow(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow> rows,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpRuntimeTraceReport)
	{
		if (csharpRuntimeTraceReport == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace,
				blocks: true,
				"future C# packet/controller stop-trigger execution",
				"future C# protection stop-trigger runtime trace emitter",
				"no C# runtime trace report supplied",
				"Live C# packet hooks and trace emission must exist before Java artifacts can be compared to C# behavior.");
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.CSharpRuntimeTraceOutput,
			csharpRuntimeTraceReport.HasLivePacketHooks
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedMissingCSharpRuntimeTrace,
			blocks: !csharpRuntimeTraceReport.HasLivePacketHooks,
			"future C# packet/controller stop-trigger execution",
			"PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport",
			$"scenarios={csharpRuntimeTraceReport.Scenarios.Count}; hasLivePacketHooks={csharpRuntimeTraceReport.HasLivePacketHooks}",
			csharpRuntimeTraceReport.Notes);
	}

	private static void AddComparisonExecutionRow(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow> rows)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea.ComparisonExecution,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus.BlockedComparisonNotExecuted,
			blocks: true,
			"generated Java artifacts and future C# runtime trace output",
			"future protection stop-trigger runtime comparison suite",
			"no deterministic Java/C# comparison executed",
			"Verified parity cannot be claimed until generated Java trace rows and C# runtime trace rows are compared deterministically.");
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractArea area,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractStatus status,
		bool blocks,
		string javaSource,
		string csharpTarget,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonContractRow(
			rows.Count + 1,
			area,
			status,
			blocks,
			javaSource,
			csharpTarget,
			evidence,
			notes));
	}
}
