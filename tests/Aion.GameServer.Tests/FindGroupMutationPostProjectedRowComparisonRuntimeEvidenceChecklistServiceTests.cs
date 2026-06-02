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
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderImplementationReadinessChecklistService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderImplementationRunbookContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostTypedValueReaderImplementationReadinessGateService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostValueReaderFunctionExecutionPreflightService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("implementation runbook", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("typed-reader implementation gate", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("function execution preflight", StringComparison.Ordinal)
			&& row.Notes.Contains("reads no values", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
			&& row.ExistingProvider.Contains("FindGroupMutationPostTraceCaptureTest", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostJavaTraceArtifactDirectoryReportService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostJavaArtifactRootValidationCommandReportService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureCommandConsistencyReportService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostExplicitRootJavaCaptureDryRunCommandReportService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostExplicitRootJavaPostCaptureValidatorSummaryService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("capture-enabled Java", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("artifact-root validation command report", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("capture-command consistency report", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("explicit-root dry-run command report", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("post-capture validator summary", StringComparison.Ordinal)
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
			&& row.ExistingProvider.Contains("FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("guarded live C# CM_FIND_GROUP boundary", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("Java artifact pairing identity", StringComparison.Ordinal)
			&& row.Notes.Contains("intake preflight remains blocked", StringComparison.Ordinal)
			&& !row.HasRuntimeEvidence);
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold
			&& row.ExistingProvider.Contains("FindGroupMutationPostJavaCSharpRowPairingReadinessReportService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonDryRunContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("action/mutation pairing readiness", StringComparison.Ordinal)
			&& row.Notes.Contains("row pairing readiness", StringComparison.Ordinal)
			&& !row.HasRuntimeEvidence);
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation
			&& row.ExistingProvider.Contains("FindGroupMutationPostRegistryObservationTraceContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("posted system-message and refreshed-list registry sends", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection
			&& row.ExistingProvider.Contains("FindGroupMutationPostValueProjectionHandoffGateService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("runtime-row-value evidence intake gate", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("accepted C# boundary rows", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("runtime row values", StringComparison.Ordinal)
			&& row.Notes.Contains("required rows", StringComparison.Ordinal)
			&& row.Notes.Contains("deliberately do not read values", StringComparison.Ordinal));
		Assert.Contains(checklist.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ResultEmission
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationReadinessAuditService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContractService", StringComparison.Ordinal)
			&& row.ExistingProvider.Contains("FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("blocked-output preview", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("runtime-evidence intake", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("materialization preflight", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("result-emission gate", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("evidence summary", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("implementation readiness audit", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("runtime comparison handoff", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("live-capture preflight runbook", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("capture acceptance matrix", StringComparison.Ordinal)
			&& row.RequiredNextEvidence.Contains("capture execution blocker summary", StringComparison.Ordinal)
			&& row.Notes.Contains("cannot materialize", StringComparison.Ordinal));
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
