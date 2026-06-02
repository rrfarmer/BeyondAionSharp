namespace Aion.GameServer.Services;

public enum FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus
{
	ReadyForIntentionalExplicitRootCapture,
	BlockedMissingExplicitRoot,
	BlockedRepositoryArtifactRoot,
	BlockedInconsistentCommand,
}

public enum FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate
{
	ExplicitArtifactRoot,
	FocusedJavaFixtureMethod,
	CaptureFlag,
	DeterministicTimestamp,
	ExpectedArtifactTargets,
	CSharpArtifactValidation,
	RuntimeComparisonBlocked,
}

public sealed record FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGateRow(
	int Order,
	FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate Gate,
	bool Passed,
	string Evidence,
	string Notes);

public sealed record FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReport(
	FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus Status,
	IReadOnlyList<FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGateRow> Gates,
	string ArtifactRoot,
	string JavaTestSelector,
	string JavaCaptureCommand,
	string CSharpValidatorCommand,
	IReadOnlyList<string> ExpectedArtifactPaths,
	bool UsesExplicitRoot,
	bool UsesRepositoryArtifactRoot,
	bool HasCaptureFlag,
	bool HasDeterministicTimestamp,
	bool HasArtifactRootProperty,
	bool CanRunIntentionalCaptureCommand,
	bool CanRunRuntimeComparison,
	bool CanClaimVerifiedParity,
	string ExecutionDecision,
	string TraceName,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live dry-run report for the guarded explicit-root
/// Java capture command that exercises FindGroupMutationPostTraceCaptureTest's
/// command-supplied artifact-root path. It names the command and acceptance
/// gates only; it does not execute Maven or compare Java/C# runtime rows.
/// </summary>
public static class FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService
{
	public const string JavaFixtureMethod = "commandSuppliedArtifactRootPropertyWritesGuardedArtifacts";
	public const string JavaTestSelector =
		$"{FindGroupMutationPostJavaArtifactCaptureRunbookService.FixtureClassName}#{JavaFixtureMethod}";

	public static FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReport Create(
		string artifactRoot,
		FindGroupMutationPostJavaArtifactRootValidationCommandReport? rootValidationReport = null,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReport? consistencyReport = null)
	{
		rootValidationReport ??= FindGroupMutationPostJavaArtifactRootValidationCommandReportService.Create(artifactRoot);
		consistencyReport ??= FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService.Create(artifactRoot);

		var command = JavaCaptureCommand(artifactRoot);
		var usesExplicitRoot = !string.IsNullOrWhiteSpace(artifactRoot);
		var usesRepositoryArtifactRoot = string.Equals(
			artifactRoot,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			StringComparison.Ordinal);
		var hasCaptureFlag = command.Contains(
			$"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.CaptureFlag}=true",
			StringComparison.Ordinal);
		var hasDeterministicTimestamp = command.Contains(
			$"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.ServerEpochSecondsProperty}={FindGroupMutationPostJavaArtifactCaptureRunbookService.DeterministicServerEpochSeconds}",
			StringComparison.Ordinal);
		var hasArtifactRootProperty = command.Contains(
			$"-D{FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.JavaArtifactRootProperty}={artifactRoot}",
			StringComparison.Ordinal);
		var expectedArtifactPaths = rootValidationReport.Rows
			.Select(row => row.ArtifactPath)
			.ToArray();
		var gates = new[]
		{
			GateRow(
				1,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.ExplicitArtifactRoot,
				usesExplicitRoot && !usesRepositoryArtifactRoot,
				artifactRoot,
				"Intentional capture must supply a non-repository artifact root so normal repository artifact output remains untouched."),
			GateRow(
				2,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.FocusedJavaFixtureMethod,
				command.Contains($"-Dtest={JavaTestSelector}", StringComparison.Ordinal),
				JavaTestSelector,
				"Use the guarded Java fixture method that requires the artifact-root system property before writing files."),
			GateRow(
				3,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.CaptureFlag,
				hasCaptureFlag,
				FindGroupMutationPostJavaArtifactCaptureRunbookService.CaptureFlag,
				"Capture must remain gated by the Java capture flag."),
			GateRow(
				4,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.DeterministicTimestamp,
				hasDeterministicTimestamp && consistencyReport.AllProvidersConsistent,
				consistencyReport.ExpectedTimestampCommandFragment,
				"Generated rows must use the deterministic fixture timestamp expected by the command-provider consistency report."),
			GateRow(
				5,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.ExpectedArtifactTargets,
				hasArtifactRootProperty && expectedArtifactPaths.Length == 2 && expectedArtifactPaths.All(path => path.StartsWith(artifactRoot, StringComparison.Ordinal)),
				string.Join("; ", expectedArtifactPaths),
				"The explicit root must receive only the stable action 2 and action 6 mutation-post artifact files."),
			GateRow(
				6,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.CSharpArtifactValidation,
				!string.IsNullOrWhiteSpace(rootValidationReport.CSharpValidatorCommand),
				rootValidationReport.CSharpValidatorCommand,
				"After Java capture, run the focused C# artifact directory/validator tests against the same root."),
			GateRow(
				7,
				FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.RuntimeComparisonBlocked,
				!rootValidationReport.ReadyForRuntimeComparison,
				rootValidationReport.ExecutionDecision,
				"Shape-valid generated files still are not live C# boundary rows or Java/C# runtime comparison evidence."),
		};
		var status = StatusFor(usesExplicitRoot, usesRepositoryArtifactRoot, gates);
		var canRunIntentionalCaptureCommand = status == FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.ReadyForIntentionalExplicitRootCapture;

		return new FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReport(
			status,
			gates,
			artifactRoot,
			JavaTestSelector,
			command,
			rootValidationReport.CSharpValidatorCommand,
			expectedArtifactPaths,
			usesExplicitRoot,
			usesRepositoryArtifactRoot,
			hasCaptureFlag,
			hasDeterministicTimestamp,
			hasArtifactRootProperty,
			canRunIntentionalCaptureCommand,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			DecisionFor(status),
			rootValidationReport.TraceName,
			"Java sources reviewed: FindGroupMutationPostTraceCaptureTest.commandSuppliedArtifactRootPropertyWritesGuardedArtifacts; FindGroupMutationPostTraceCaptureInMemoryArtifactBridge; FindGroupMutationPostTraceCaptureArtifactWriter; FindGroupMutationPostTraceCaptureArtifactValidator.",
			IsLive: false);
	}

	public static string JavaCaptureCommand(string artifactRoot) =>
		$"mvn -pl game-server -am test \"-Dtest={JavaTestSelector}\" \"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.CaptureFlag}=true\" \"-D{FindGroupMutationPostJavaArtifactCaptureRunbookService.ServerEpochSecondsProperty}={FindGroupMutationPostJavaArtifactCaptureRunbookService.DeterministicServerEpochSeconds}\" \"-D{FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.JavaArtifactRootProperty}={artifactRoot}\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"";

	private static FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGateRow GateRow(
		int order,
		FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate gate,
		bool passed,
		string evidence,
		string notes)
	{
		return new FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGateRow(
			order,
			gate,
			passed,
			evidence,
			notes);
	}

	private static FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus StatusFor(
		bool usesExplicitRoot,
		bool usesRepositoryArtifactRoot,
		IReadOnlyList<FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGateRow> gates)
	{
		if (!usesExplicitRoot)
			return FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedMissingExplicitRoot;
		if (usesRepositoryArtifactRoot)
			return FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedRepositoryArtifactRoot;
		return gates.All(gate => gate.Passed)
			? FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.ReadyForIntentionalExplicitRootCapture
			: FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedInconsistentCommand;
	}

	private static string DecisionFor(
		FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus status)
	{
		return status switch
		{
			FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.ReadyForIntentionalExplicitRootCapture => "Explicit-root Java capture command is well-formed for an intentional temporary-root artifact generation run; runtime comparison and verified parity remain blocked.",
			FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedMissingExplicitRoot => "Explicit-root Java capture command is blocked because no artifact root was supplied.",
			FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedRepositoryArtifactRoot => "Explicit-root Java capture command is blocked because it points at the repository artifact root instead of an isolated temporary root.",
			_ => "Explicit-root Java capture command is blocked because at least one command, timestamp, artifact-target, or validation gate is inconsistent.",
		};
	}
}
