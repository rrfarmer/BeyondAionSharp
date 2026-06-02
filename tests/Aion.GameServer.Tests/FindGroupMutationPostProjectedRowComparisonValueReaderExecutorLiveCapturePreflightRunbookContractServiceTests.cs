using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractServiceTests
{
	[Fact]
	public void Create_DefaultRunbookBlocksUntilRuntimeComparisonHandoffIsReady()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedRuntimeComparisonHandoffNotReady, runbook.Status);
		Assert.False(runbook.IsLive);
		Assert.True(runbook.HasRuntimeComparisonHandoff);
		Assert.True(runbook.HasJavaArtifactCaptureRunbook);
		Assert.True(runbook.HasGuardedBoundarySkeleton);
		Assert.False(runbook.HasAnyRuntimeEvidence);
		Assert.False(runbook.CanRunJavaCapture);
		Assert.False(runbook.CanRunCSharpCapture);
		Assert.False(runbook.CanRunRuntimeComparison);
		Assert.False(runbook.CanStartExecutableImplementation);
		Assert.False(runbook.CanClaimVerifiedParity);
		Assert.Equal("parity-artifacts/find-group/mutation-post/java", runbook.JavaArtifactRoot);
		Assert.Equal("GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture", runbook.CSharpBoundaryFixtureName);
		Assert.Contains("runtime comparison handoff metadata is ready", runbook.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultRunbookListsEveryPreflightStepAsBlocked()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create();

		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep>(), runbook.Rows.Select(row => row.Step));
		Assert.All(runbook.Rows, row =>
		{
			Assert.True(row.HasRuntimeComparisonHandoff);
			Assert.True(row.HasExistingProvider);
			Assert.False(row.HasRuntimeEvidence);
			Assert.False(row.CanRunCommand);
			Assert.False(row.CanAcceptEvidence);
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedRuntimeComparisonHandoffNotReady, row.Status);
		});
	}

	[Fact]
	public void Create_RuntimeMissingRunbookNamesConcreteJavaCaptureCommandAndArtifactRoot()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create(
			Handoff(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing, runbook.Status);
		Assert.Equal(
			"mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\" \"-Daion.findGroupMutationPost.capture=true\" \"-Daion.findGroupMutationPost.artifactRoot=parity-artifacts/find-group/mutation-post/java\" \"-Dmaven.test.skip=false\" \"-Dsurefire.failIfNoSpecifiedTests=false\"",
			runbook.JavaCaptureCommand);
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture
			&& row.SourceRequirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingJavaArtifacts
			&& row.Provider.Contains("FindGroupMutationPostTraceCaptureTest", StringComparison.Ordinal)
			&& row.Command.Contains("-Daion.findGroupMutationPost.artifactRoot=parity-artifacts/find-group/mutation-post/java", StringComparison.Ordinal)
			&& row.AcceptanceGate.Contains("Action 2 and action 6 Java artifact files", StringComparison.Ordinal));
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactValidation
			&& row.Provider.Contains("FindGroupMutationPostJavaTraceArtifactDirectoryReportService", StringComparison.Ordinal)
			&& row.Command.Contains("FindGroupMutationPostJavaTraceArtifactValidatorServiceTests", StringComparison.Ordinal)
			&& row.ArtifactRoot == "parity-artifacts/find-group/mutation-post/java");
	}

	[Fact]
	public void Create_RuntimeMissingRunbookNamesCSharpBoundaryExecutorAndRegistryGates()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create(
			Handoff(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingCSharpLiveRows
			&& row.Provider.Contains("GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture", StringComparison.Ordinal)
			&& row.AcceptanceGate.Contains("boundaryAccepted=true", StringComparison.Ordinal)
			&& row.Notes.Contains("must not enable production", StringComparison.Ordinal));
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.BoundaryExecutorObservation
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingExecutorObservation
			&& row.AcceptanceGate.Contains("executorInvokedFromBoundary", StringComparison.Ordinal));
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RegistrySendObservation
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRegistryObservation
			&& row.AcceptanceGate.Contains("Posted SmSystemMessage", StringComparison.Ordinal)
			&& row.AcceptanceGate.Contains("refreshed SmFindGroup", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyShapedHandoffStillBlocksComparisonAndExecutableImplementation()
	{
		var runbook = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService.Create(
			Handoff(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus.BlockedExecutableImplementationDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedExecutableImplementationDeferred, runbook.Status);
		Assert.False(runbook.CanRunRuntimeComparison);
		Assert.False(runbook.CanStartExecutableImplementation);
		Assert.False(runbook.CanClaimVerifiedParity);
		Assert.Contains("executable implementation remains deferred", runbook.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RuntimeComparisonExecution
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRuntimeComparison
			&& row.Provider.Contains("FindGroupRuntimeComparisonPreflightContractService", StringComparison.Ordinal)
			&& row.AcceptanceGate.Contains("deterministically compare action 2/6", StringComparison.Ordinal));
		Assert.Contains(runbook.Rows, row =>
			row.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ExecutableImplementationGate
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedExecutableImplementationDeferred
			&& row.Command.Contains("no executable implementation command", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContract Handoff(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffStatus status) =>
		new(
			status,
			[
				HandoffRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows),
				HandoffRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows),
				HandoffRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation),
				HandoffRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation),
				HandoffRow(5, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RowIdentityMatching),
				HandoffRow(6, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection),
				HandoffRow(7, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization),
				HandoffRow(8, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission),
				HandoffRow(9, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison),
				HandoffRow(10, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation),
			],
			HasImplementationReadinessAudit: true,
			HasAnyRuntimeEvidence: false,
			CanStartExecutableImplementation: false,
			CanStartRuntimeComparison: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Runtime comparison handoff remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRow HandoffRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement requirement) =>
		new(
			order,
			requirement,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRowStatus.BlockedRuntimeEvidenceMissing,
			HasImplementationReadinessAudit: true,
			HasRuntimeEvidence: false,
			RequiredBeforeExecutableImplementation: true,
			RequiredBeforeRuntimeComparison: requirement is not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization
				and not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission
				and not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison
				and not FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
			RequiredBeforeVerifiedParity: true,
			CanStartExecutableImplementation: false,
			"test required evidence",
			"test current evidence",
			"test notes");
}
