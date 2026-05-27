namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea
{
	JavaArtifacts,
	CSharpRuntimeTraceRows,
	ScenarioAlignment,
	RowCountAlignment,
	ComparisonExecution,
}

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingJavaArtifact,
	BlockedInvalidJavaArtifact,
	BlockedMissingCSharpRuntimeTrace,
	BlockedScenarioMismatch,
	BlockedRowCountMismatch,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea Area,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus Status,
	bool BlocksComparison,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> Rows,
	bool HasShapeValidJavaArtifacts,
	bool HasValidCSharpTraceRows,
	bool HasScenarioAlignment,
	bool HasRowCountAlignment,
	bool NeedsJavaArtifacts,
	bool NeedsCSharpTraceRows,
	bool NeedsScenarioAlignment,
	bool NeedsRowCountAlignment,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live preflight for future Java/C# protection stop-trigger trace comparison.
/// This checks only artifact/trace shape alignment and never executes runtime comparison.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReportService
{
	public static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport Create(
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow>();
		var javaScenarioNames = GetJavaScenarioNames(javaArtifacts);
		var javaTraceRowCount = GetJavaTraceRowCount(javaArtifacts);

		AddJavaArtifacts(rows, javaArtifacts, javaScenarioNames);
		AddCSharpTraceRows(rows, csharpTrace);
		AddScenarioAlignment(rows, javaArtifacts, csharpTrace, javaScenarioNames);
		AddRowCountAlignment(rows, javaArtifacts, csharpTrace, javaTraceRowCount);
		AddComparisonExecution(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightReport(
			rowArray,
			HasShapeValidJavaArtifacts: javaArtifacts?.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid,
			HasValidCSharpTraceRows: csharpTrace is { HasLivePacketHooks: true, ValidationIssues.Count: 0 } && csharpTrace.TraceRows.Count > 0,
			HasScenarioAlignment: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ScenarioAlignment && !row.BlocksComparison),
			HasRowCountAlignment: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.RowCountAlignment && !row.BlocksComparison),
			NeedsJavaArtifacts: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts && row.BlocksComparison),
			NeedsCSharpTraceRows: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.CSharpRuntimeTraceRows && row.BlocksComparison),
			NeedsScenarioAlignment: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ScenarioAlignment && row.BlocksComparison),
			NeedsRowCountAlignment: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.RowCountAlignment && row.BlocksComparison),
			NeedsComparisonExecution: rowArray.Any(row => row.Area == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ComparisonExecution && row.BlocksComparison),
			ReadyForRuntimeComparison: false,
			"Java generated protection stop-trigger artifacts and future C# runtime trace rows",
			IsLive: false);
	}

	private static void AddJavaArtifacts(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		IReadOnlyList<string> javaScenarioNames)
	{
		if (javaArtifacts == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingJavaArtifact,
				blocks: true,
				"no Java artifact directory report supplied",
				"Generated Java trace artifacts are required before any preflight can compare C# rows.");
			return;
		}

		if (javaArtifacts.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.SatisfiedByNonLiveMetadata,
				blocks: false,
				$"status={javaArtifacts.Status}; files={javaArtifacts.Files.Count}; scenarios={javaScenarioNames.Count}; traceRows={GetJavaTraceRowCount(javaArtifacts)}",
				"Java artifact files are schema-valid only; preflight still does not execute runtime comparison.");
			return;
		}

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.JavaArtifacts,
			javaArtifacts.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.InvalidArtifacts
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedInvalidJavaArtifact
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingJavaArtifact,
			blocks: true,
			$"status={javaArtifacts.Status}; files={javaArtifacts.Files.Count}",
			javaArtifacts.Notes);
	}

	private static void AddCSharpTraceRows(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace)
	{
		if (csharpTrace == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.CSharpRuntimeTraceRows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingCSharpRuntimeTrace,
				blocks: true,
				"no C# runtime trace report supplied",
				"C# trace rows are required before preflight can align them with Java artifacts.");
			return;
		}

		var valid = csharpTrace.HasLivePacketHooks && csharpTrace.TraceRows.Count > 0 && csharpTrace.ValidationIssues.Count == 0;
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.CSharpRuntimeTraceRows,
			valid
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedMissingCSharpRuntimeTrace,
			blocks: !valid,
			$"scenarios={csharpTrace.Scenarios.Count}; rows={csharpTrace.TraceRows.Count}; validationIssues={csharpTrace.ValidationIssues.Count}; hasLivePacketHooks={csharpTrace.HasLivePacketHooks}",
			valid ? "C# trace rows are synthetically valid only." : "C# trace rows are missing, invalid, or not backed by live hook metadata.");
	}

	private static void AddScenarioAlignment(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace,
		IReadOnlyList<string> javaScenarioNames)
	{
		if (javaArtifacts?.Status != PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid || csharpTrace == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ScenarioAlignment,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedScenarioMismatch,
				blocks: true,
				"scenario alignment prerequisites missing",
				"Need shape-valid Java artifacts and C# trace rows before scenario names can be aligned.");
			return;
		}

		var csharpScenarios = csharpTrace.Scenarios.OrderBy(scenario => scenario, StringComparer.Ordinal).ToArray();
		var matches = javaScenarioNames.SequenceEqual(csharpScenarios, StringComparer.Ordinal);
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ScenarioAlignment,
			matches
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedScenarioMismatch,
			blocks: !matches,
			$"java=[{string.Join(",", javaScenarioNames)}]; csharp=[{string.Join(",", csharpScenarios)}]",
			matches
				? "Scenario names align by parsed Java artifact metadata and C# trace scenario."
				: "Scenario names differ; comparison must not execute.");
	}

	private static void AddRowCountAlignment(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace,
		int javaTraceRowCount)
	{
		if (javaArtifacts?.Status != PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid || csharpTrace == null)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.RowCountAlignment,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedRowCountMismatch,
				blocks: true,
				"row-count alignment prerequisites missing",
				"Need shape-valid Java artifacts and C# trace rows before row counts can be aligned.");
			return;
		}

		var matches = javaTraceRowCount == csharpTrace.TraceRows.Count;
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.RowCountAlignment,
			matches
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedRowCountMismatch,
			blocks: !matches,
			$"javaTraceRows={javaTraceRowCount}; csharpRows={csharpTrace.TraceRows.Count}",
			matches
				? "Trace row counts align by parsed Java artifact metadata and C# trace rows."
				: "Synthetic preflight counts differ; comparison must not execute.");
	}

	private static void AddComparisonExecution(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea.ComparisonExecution,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus.BlockedComparisonNotExecuted,
			blocks: true,
			"preflight only; no Java/C# trace comparison executed",
			"Verified parity cannot be claimed from preflight alignment.");
	}

	private static IReadOnlyList<string> GetJavaScenarioNames(PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts)
	{
		if (javaArtifacts == null)
			return [];

		return javaArtifacts.Files
			.Select(file => file.ValidationReport.Metadata?.Scenario ?? Path.GetFileNameWithoutExtension(file.Path))
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();
	}

	private static int GetJavaTraceRowCount(PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts)
	{
		if (javaArtifacts == null)
			return 0;

		return javaArtifacts.Files.Sum(file => file.ValidationReport.Metadata?.TraceRows.Count ?? 1);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightArea area,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightStatus status,
		bool blocks,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonPreflightRow(
			rows.Count + 1,
			area,
			status,
			blocks,
			evidence,
			notes));
	}
}
