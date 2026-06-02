using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksBeforeValueProjectionHandoff()
	{
		var gate = FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create();

		Assert.Equal(FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.BlockedValueProjectionHandoffNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasValueProjectionHandoff);
		Assert.True(gate.HasRuntimeEvidenceChecklist);
		Assert.True(gate.HasTypedValueReaderPreflight);
		Assert.False(gate.HasJavaRuntimeArtifactRows);
		Assert.False(gate.HasAcceptedCSharpTraceRows);
		Assert.False(gate.HasRuntimeRowValues);
		Assert.False(gate.CanReadJavaValues);
		Assert.False(gate.CanReadCSharpValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanEmitResults);
		Assert.False(gate.CanRunRuntimeComparison);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
		Assert.Equal(Enum.GetValues<FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage>(), gate.Rows.Select(row => row.Stage).Distinct());
		Assert.Contains("value-projection handoff", gate.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_ReadyHandoffStillBlocksOnRuntimeJavaAndCSharpRows()
	{
		var gate = FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create(ReadyValueProjectionHandoff(), RuntimeChecklistMissingEvidence());

		Assert.Equal(FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.BlockedRuntimeEvidenceMissing, gate.Status);
		Assert.False(gate.HasJavaRuntimeArtifactRows);
		Assert.False(gate.HasAcceptedCSharpTraceRows);
		Assert.False(gate.HasRuntimeRowValues);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.ValueProjectionHandoff
			&& row.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.ReadyForRuntimeInput
			&& !row.BlocksValueReaders);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.JavaRuntimeArtifactRows
			&& row.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked
			&& row.BlocksValueReaders);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.CSharpAcceptedTraceRows
			&& row.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked
			&& row.BlocksValueReaders);
	}

	[Fact]
	public void Create_NamesActionTwoAndSixRuntimeRowRequirements()
	{
		var gate = FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create(ReadyValueProjectionHandoff(), RuntimeChecklistMissingEvidence());

		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.JavaRuntimeArtifactRows
			&& row.Action == 2
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Recruitment
			&& row.RequiredEvidence.Contains("FindGroupService.addRecruitment", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("SM_FIND_GROUP action 0", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.JavaRuntimeArtifactRows
			&& row.Action == 6
			&& row.MutationKind == FindGroupDirectPacketMutationPostTraceMutationKind.Application
			&& row.RequiredEvidence.Contains("FindGroupService.addApplication", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("SM_FIND_GROUP action 4", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.CSharpAcceptedTraceRows
			&& row.Action == 2
			&& row.RequiredEvidence.Contains("ProcessPacketAsync", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("Java action 2 pairing identity", StringComparison.Ordinal));
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.CSharpAcceptedTraceRows
			&& row.Action == 6
			&& row.RequiredEvidence.Contains("ProcessPacketAsync", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("Java action 6 pairing identity", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_TypedReaderRowCarriesFieldCountsWithoutReadingValues()
	{
		var gate = FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create(ReadyValueProjectionHandoff(), RuntimeChecklistMissingEvidence());

		Assert.True(gate.RequiredEqualityReaderFieldCount > 0);
		Assert.True(gate.IgnoredRuntimeContextFieldCount > 0);
		Assert.False(gate.CanReadJavaValues);
		Assert.False(gate.CanReadCSharpValues);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.TypedValueReaders
			&& row.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Deferred
			&& row.CurrentEvidence.Contains($"requiredEqualityReaderFields={gate.RequiredEqualityReaderFieldCount}", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains($"ignoredRuntimeContextFields={gate.IgnoredRuntimeContextFieldCount}", StringComparison.Ordinal)
			&& row.Notes.Contains("does not read values", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidencePresentStillBlocksReaderExecutionAndParity()
	{
		var gate = FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateService.Create(ReadyValueProjectionHandoff(), RuntimeChecklistWithEvidence());

		Assert.Equal(FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStatus.ReadyForRuntimeRowsValueReadersBlocked, gate.Status);
		Assert.True(gate.HasJavaRuntimeArtifactRows);
		Assert.True(gate.HasAcceptedCSharpTraceRows);
		Assert.True(gate.HasRuntimeRowValues);
		Assert.False(gate.CanReadJavaValues);
		Assert.False(gate.CanReadCSharpValues);
		Assert.False(gate.CanCompareValues);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Contains(gate.Rows, row =>
			row.Stage == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStage.RuntimeValueReadExecution
			&& row.Status == FindGroupMutationPostRuntimeRowValueEvidenceIntakeGateStageStatus.Blocked
			&& row.Notes.Contains("proves no parity", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostValueProjectionHandoffGate ReadyValueProjectionHandoff() =>
		new(
			FindGroupMutationPostValueProjectionHandoffGateStatus.ReadyForRuntimeValuesProjectionBlocked,
			[
				new FindGroupMutationPostValueProjectionHandoffGateRow(
					1,
					FindGroupMutationPostValueProjectionHandoffGateStage.RuntimeValueEvidence,
					FindGroupMutationPostValueProjectionHandoffGateStageStatus.Blocked,
					HasExpectedShape: true,
					BlocksValueProjection: true,
					"hasRuntimeRowValues=False",
					"runtime values missing"),
			],
			HasRowPairingReadiness: true,
			HasValueContract: true,
			HasValueReaderReadiness: true,
			HasAllActionMutationPairs: true,
			HasAllValueSourceMappings: true,
			HasRuntimeRowValues: false,
			CanStartValueProjection: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanRunRuntimeComparison: false,
			CanClaimVerifiedParity: false,
			"Ready for runtime row values, but no values have been read.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist RuntimeChecklistMissingEvidence() =>
		FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(ReadyRuntimeHandoff());

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist RuntimeChecklistWithEvidence()
	{
		var checklist = RuntimeChecklistMissingEvidence();
		var rows = checklist.Rows
			.Select(row => row with
			{
				HasRuntimeEvidence = true,
				Evidence = $"{row.Evidence}; syntheticTestRuntimeEvidence=True",
			})
			.ToArray();

		return checklist with
		{
			Rows = rows,
			HasAnyRuntimeEvidence = true,
		};
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
