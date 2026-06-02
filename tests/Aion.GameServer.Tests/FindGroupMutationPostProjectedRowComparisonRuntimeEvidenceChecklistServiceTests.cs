using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistServiceTests
{
	[Fact]
	public void Create_DefaultChecklistBlocksBeforeRuntimeEvidenceReadiness()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedSummaryNotReady, checklist.Status);
		Assert.False(checklist.IsLive);
		Assert.True(checklist.HasLiveInputHandoff);
		Assert.True(checklist.HasExistingNonLiveProviders);
		Assert.False(checklist.HasAnyRuntimeEvidence);
		Assert.False(checklist.CanStartProjectedComparison);
		Assert.False(checklist.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", checklist.TraceName);
		Assert.Contains("addRecruitment/addApplication", checklist.JavaSource, StringComparison.Ordinal);
		Assert.All(checklist.Rows, row =>
		{
			Assert.False(row.HasRuntimeEvidence);
			Assert.True(row.BlocksVerifiedParity);
		});
	}

	[Fact]
	public void Create_MapsEveryLiveInputRequirementToAProvider()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create();

		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonLiveInputRequirement>(), checklist.Rows.Select(row => row.Requirement));
		Assert.All(checklist.Rows, row =>
		{
			Assert.True(row.HasExistingProvider);
			Assert.False(string.IsNullOrWhiteSpace(row.ExistingProvider));
			Assert.False(string.IsNullOrWhiteSpace(row.RequiredNextEvidence));
		});
	}

	[Fact]
	public void Create_JavaRuntimeArtifactRowNamesJavaCaptureAndCSharpReader()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create();

		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveMetadata
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderReadinessSummaryService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("value-reader readiness summary, typed-reader preflight, and mismatch-context preflight", StringComparison.Ordinal)
			&& row.Notes.Contains("reads no values", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
			&& row.ExistingProvider.Contains("FindGroupMutationPostTraceCaptureTest", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostJavaTraceArtifactDirectoryReportService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("capture-enabled Java", StringComparison.Ordinal)
			&& row.Notes.Contains("shape evidence only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_CSharpBoundaryAndRegistryRowsStayNonLive()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(ReadyRuntimeHandoff());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedRuntimeEvidenceMissing, checklist.Status);
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
			&& row.ExistingProvider.Contains("FindGroupMutationPostGuardedFixtureResultContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("guarded live C# CM_FIND_GROUP boundary", StringComparison.Ordinal)
			&& !row.HasRuntimeEvidence);
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation
			&& row.ExistingProvider.Contains("FindGroupMutationPostRegistryObservationTraceContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("posted system-message and refreshed-list registry sends", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DispatchAndRuntimeComparisonRowsRemainHardBlocked()
	{
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(ReadyRuntimeHandoff());

		Assert.False(checklist.CanClaimVerifiedParity);
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.LiveDispatchDisabled
			&& row.ExistingProvider.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("broad-validation trigger", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ComparisonNotExecuted
			&& row.ExistingProvider.Contains("FindGroupRuntimeComparisonPreflightContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("runtime or socket comparison", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract ReadyRuntimeHandoff() =>
		FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary());

	private static FindGroupMutationPostProjectedRowComparisonReadinessSummary ReadyForRuntimeInputSummary() =>
		new(
			FindGroupMutationPostProjectedRowComparisonReadinessSummaryStatus.BlockedValueProjectionDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
					1,
					FindGroupMutationPostProjectedRowComparisonReadinessStage.DryRunContract,
					FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.ReadyForFutureInput,
					HasExpectedShape: true,
					BlocksComparison: false,
					"status=ReadyForFutureExecutor",
					"ready"),
			],
			HasDryRunContract: true,
			HasExecutorSkeleton: true,
			HasValueContract: true,
			HasBlockedResultReport: true,
			HasAllPairedInputs: true,
			CanCompareRows: false,
			CanProjectValues: false,
			CanEmitResults: false,
			"Projected-row comparison remains blocked because value projection is still deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
