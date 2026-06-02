using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractServiceTests
{
	[Fact]
	public void Create_DefaultContractBlocksBeforeSummaryReadiness()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedSummaryNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasReadinessSummary);
		Assert.False(contract.CanStartLiveComparison);
		Assert.False(contract.CanEnableLiveDispatch);
		Assert.False(contract.HasRequiredRuntimeEvidence);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
		Assert.Contains("readiness summary", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.All(contract.Requirements, row =>
		{
			Assert.False(row.IsRuntimeEvidence);
			if (row.Status != FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata)
				Assert.True(row.BlocksLiveComparison);
		});
	}

	[Fact]
	public void Create_DefaultContractListsExpectedRuntimeRequirements()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ProjectedRowReadinessSummary,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.CSharpLiveBoundaryRow,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.BoundaryExecutorInvocation,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ResultEmission,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard,
				FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison,
			],
			contract.Requirements.Select(row => row.Requirement));
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.JavaRuntimeTraceArtifact
			&& row.RequiredArtifact.Contains("CM_FIND_GROUP.readImpl/runImpl", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("FindGroupService.addRecruitment/addApplication", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RegistrySendObservation
			&& row.RequiredArtifact.Contains("posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("zero broadcasts", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueReaderReadinessSummary
			&& row.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata
			&& row.RequiredArtifact.Contains("value-reader readiness summary", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("typed-reader preflight", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("mismatch-context preflight", StringComparison.Ordinal)
			&& row.Evidence.Contains("stages=5", StringComparison.Ordinal)
			&& row.Evidence.Contains("canReadValues=False", StringComparison.Ordinal)
			&& row.Notes.Contains("reads no Java/C# values", StringComparison.Ordinal));
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison
			&& row.Notes.Contains("verified parity", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_SummaryReadyForRuntimeInputsStillBlocksMissingRuntimeArtifacts()
	{
		var summary = ReadyForRuntimeInputSummary();

		var contract = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(summary);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonLiveInputHandoffStatus.BlockedMissingRuntimeArtifacts, contract.Status);
		Assert.True(contract.HasNonLiveMetadata);
		Assert.False(contract.HasRequiredRuntimeEvidence);
		Assert.False(contract.CanStartLiveComparison);
		Assert.Contains("runtime artifacts", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ProjectedRowReadinessSummary
			&& row.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.SatisfiedByNonLiveMetadata
			&& !row.IsRuntimeEvidence);
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.ValueProjection
			&& row.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedMissingRuntimeEvidence
			&& row.Evidence.Contains("canProjectValues=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_LiveDispatchGuardNeverEnablesDispatch()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary());

		Assert.False(contract.CanEnableLiveDispatch);
		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.LiveDispatchGuard
			&& row.Status == FindGroupMutationPostProjectedRowComparisonLiveInputRequirementStatus.BlockedLiveDispatchDisabled
			&& row.RequiredArtifact.Contains("GameServerConnection.ProcessPacketAsync", StringComparison.Ordinal)
			&& row.Evidence.Contains("canEnableLiveDispatch=false", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RowIdentityRequirementNamesActionMutationAndObjectIds()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContractService.Create(ReadyForRuntimeInputSummary());

		Assert.Contains(contract.Requirements, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RowIdentityMatching
			&& row.RequiredArtifact.Contains("action", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("mutationKind", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("activePlayerObjectId", StringComparison.Ordinal)
			&& row.RequiredArtifact.Contains("mutatedEntryObjectId", StringComparison.Ordinal));
	}

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
				new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
					2,
					FindGroupMutationPostProjectedRowComparisonReadinessStage.ExecutorSkeleton,
					FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Deferred,
					HasExpectedShape: true,
					BlocksComparison: false,
					"hasAllPairedInputs=True",
					"deferred"),
				new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
					3,
					FindGroupMutationPostProjectedRowComparisonReadinessStage.ValueContract,
					FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Deferred,
					HasExpectedShape: true,
					BlocksComparison: true,
					"canProjectValues=False",
					"deferred"),
				new FindGroupMutationPostProjectedRowComparisonReadinessSummaryStageRow(
					4,
					FindGroupMutationPostProjectedRowComparisonReadinessStage.BlockedResultReport,
					FindGroupMutationPostProjectedRowComparisonReadinessStageStatus.Blocked,
					HasExpectedShape: true,
					BlocksComparison: true,
					"canEmitMatched=False",
					"blocked"),
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
