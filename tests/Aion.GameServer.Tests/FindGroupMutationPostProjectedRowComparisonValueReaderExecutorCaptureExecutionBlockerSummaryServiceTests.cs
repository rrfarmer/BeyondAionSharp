using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryServiceTests
{
	[Fact]
	public void Create_DefaultSummaryBlocksRuntimeComparisonAndNamesJavaCaptureCommand()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedLiveCapturePreflightNotReady, summary.Status);
		Assert.False(summary.IsLive);
		Assert.False(summary.ShouldRunRuntimeComparison);
		Assert.False(summary.CanStartExecutableImplementation);
		Assert.False(summary.CanClaimVerifiedParity);
		Assert.Equal(0, summary.BlockingRuntimeComparisonCount);
		Assert.Equal(11, summary.BlockingExecutableImplementationCount);
		Assert.Equal(11, summary.BlockingVerifiedParityCount);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows, summary.PrimaryBlockingField);
		Assert.Equal("javaArtifactRowsPresent", summary.PrimaryBlockingEvidenceField);
		Assert.Contains("FindGroupMutationPostTraceCaptureTest", summary.SmallestNextEvidenceCommand, StringComparison.Ordinal);
		Assert.Contains("aion.findGroupMutationPost.capture=true", summary.SmallestNextEvidenceCommand, StringComparison.Ordinal);
		Assert.Contains("must not start", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("smallest next evidence command", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", summary.TraceName);
		Assert.Contains("addRecruitment/addApplication", summary.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_RuntimeMissingSummaryUsesRuntimeBlockersFromAcceptanceMatrix()
	{
		var runbook = Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing);
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(runbook);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create(matrix, runbook);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryStatus.BlockedAcceptanceEvidenceMissing, summary.Status);
		Assert.Equal(10, summary.BlockingRuntimeComparisonCount);
		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows, summary.PrimaryBlockingField);
		Assert.Contains("java capture command", summary.SmallestNextEvidenceCommand, StringComparison.Ordinal);
		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows
			&& row.BlocksRuntimeComparison
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingCSharpBoundaryEvidence
			&& row.NextEvidenceCommand.Contains("csharp boundary command", StringComparison.Ordinal));
		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation
			&& row.BlocksRuntimeComparison
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingRegistryObservation
			&& row.RequiredEvidence.Contains("Posted SmSystemMessage", StringComparison.Ordinal));
		Assert.Contains("first runtime blocker is javaArtifactRowsPresent", summary.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ExecutableImplementationGateDoesNotBlockRuntimeStartButBlocksParity()
	{
		var runbook = Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedExecutableImplementationDeferred);
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(runbook);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create(matrix, runbook);

		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutableImplementationGate
			&& !row.BlocksRuntimeComparison
			&& row.BlocksExecutableImplementation
			&& row.BlocksVerifiedParity
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.ExecutableImplementationDeferred
			&& row.Notes.Contains("does not block runtime comparison start", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_SummaryKeepsResultStagesSeparated()
	{
		var runbook = Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing);
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(runbook);

		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerSummaryService.Create(matrix, runbook);

		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ValueProjection
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingValueProjection);
		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultMaterialization
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingMaterializedResults);
		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultEmission
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingResultEmission);
		Assert.Contains(summary.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution
			&& row.Reason == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureExecutionBlockerReason.MissingRuntimeComparison);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract Runbook(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus status)
	{
		var rows = Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep>()
			.Select((step, index) => RunbookRow(index + 1, step))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract(
			status,
			rows,
			"parity-artifacts/find-group/mutation-post/java",
			"java capture command",
			"GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture",
			"FindGroupRuntimeComparisonPreflightContractService",
			HasRuntimeComparisonHandoff: true,
			HasJavaArtifactCaptureRunbook: true,
			HasGuardedBoundarySkeleton: true,
			HasAnyRuntimeEvidence: false,
			CanRunJavaCapture: false,
			CanRunCSharpCapture: false,
			CanRunRuntimeComparison: false,
			CanStartExecutableImplementation: false,
			CanClaimVerifiedParity: false,
			"Live capture preflight remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow RunbookRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step) =>
		new(
			order,
			step,
			RequirementFor(step),
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRuntimeComparison,
			HasRuntimeComparisonHandoff: true,
			HasExistingProvider: true,
			HasRuntimeEvidence: false,
			CanRunCommand: false,
			CanAcceptEvidence: false,
			"test provider",
			CommandFor(step),
			"test artifact root",
			$"test acceptance gate for {step}",
			"test current evidence",
			"test notes");

	private static string CommandFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step) =>
		step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture => "java capture command",
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture => "csharp boundary command",
			_ => $"command for {step}",
		};

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement RequirementFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step) =>
		step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactValidation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.BoundaryExecutorObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RegistrySendObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultMaterialization => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultEmission => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RuntimeComparisonExecution => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
		};
}
