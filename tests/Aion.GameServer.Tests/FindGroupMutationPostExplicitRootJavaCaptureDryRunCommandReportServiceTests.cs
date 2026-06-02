using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportServiceTests
{
	[Fact]
	public void Create_TemporaryRootNamesFocusedJavaCommandAndAcceptanceGates()
	{
		var artifactRoot = Path.Combine(Path.GetTempPath(), $"find-group-explicit-root-{Guid.NewGuid():N}");

		var report = FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService.Create(artifactRoot);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.ReadyForIntentionalExplicitRootCapture, report.Status);
		Assert.False(report.IsLive);
		Assert.True(report.UsesExplicitRoot);
		Assert.False(report.UsesRepositoryArtifactRoot);
		Assert.True(report.CanRunIntentionalCaptureCommand);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal("FindGroupMutationPostTraceCaptureTest#commandSuppliedArtifactRootPropertyWritesGuardedArtifacts", report.JavaTestSelector);
		Assert.Contains("-Dtest=FindGroupMutationPostTraceCaptureTest#commandSuppliedArtifactRootPropertyWritesGuardedArtifacts", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("-Daion.findGroupMutationPost.capture=true", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("-Daion.findGroupMutationPost.serverEpochSeconds=1700000000", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains($"-Daion.findGroupMutationPost.artifactRoot={artifactRoot}", report.JavaCaptureCommand, StringComparison.Ordinal);
		Assert.Contains("FindGroupMutationPostJavaTraceArtifactDirectoryReportServiceTests", report.CSharpValidatorCommand, StringComparison.Ordinal);
		Assert.Contains("FindGroupMutationPostJavaTraceArtifactValidatorServiceTests", report.CSharpValidatorCommand, StringComparison.Ordinal);
		Assert.Contains("allProvidersConsistent=True", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("selectedKind=ExecutorConsistencyAudit", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("selectedEvidenceField=executorConsistencyAuditAccepted", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("commandDecisionRowsEvidence=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("captureExecutionBlockerSummaryRows=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("captureAcceptanceMatrixRows=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("liveCapturePreflightRows=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("runtimeComparisonHandoffRows=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("consistencyAuditRowEvidence=", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("ExecutorEvidenceBridge", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("ResultEmissionBlocker", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("JavaArtifactCaptureRunbook=consistent:True", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Contains("JavaArtifactRootValidationCommandReport=consistent:True", report.CommandConsistencyEvidence, StringComparison.Ordinal);
		Assert.Equal(2, report.ExpectedArtifactPaths.Count);
		Assert.Contains(report.ExpectedArtifactPaths, path => path.EndsWith("cm-find-group-direct-mutation-post-boundary-action-2-java.json", StringComparison.Ordinal));
		Assert.Contains(report.ExpectedArtifactPaths, path => path.EndsWith("cm-find-group-direct-mutation-post-boundary-action-6-java.json", StringComparison.Ordinal));
		Assert.All(report.ExpectedArtifactPaths, path => Assert.StartsWith(artifactRoot, path, StringComparison.Ordinal));
		Assert.Equal(Enum.GetValues<FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate>(), report.Gates.Select(row => row.Gate));
		Assert.All(report.Gates, gate => Assert.True(gate.Passed));
		Assert.Contains("temporary-root artifact generation", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("verified parity remain blocked", report.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultRepositoryRootBlocksIntentionalCaptureCommand()
	{
		var report = FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService.Create(
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedRepositoryArtifactRoot, report.Status);
		Assert.True(report.UsesExplicitRoot);
		Assert.True(report.UsesRepositoryArtifactRoot);
		Assert.False(report.CanRunIntentionalCaptureCommand);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.Contains("repository artifact root", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Gates, gate =>
			gate.Gate == FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.ExplicitArtifactRoot
			&& !gate.Passed
			&& gate.Notes.Contains("non-repository artifact root", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_MissingRootBlocksBeforeCommandCanBeUsed()
	{
		var report = FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService.Create(string.Empty);

		Assert.Equal(FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportStatus.BlockedMissingExplicitRoot, report.Status);
		Assert.False(report.UsesExplicitRoot);
		Assert.False(report.CanRunIntentionalCaptureCommand);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.Contains("no artifact root was supplied", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(report.Gates, gate =>
			gate.Gate == FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandGate.ExplicitArtifactRoot
			&& !gate.Passed);
	}
}
