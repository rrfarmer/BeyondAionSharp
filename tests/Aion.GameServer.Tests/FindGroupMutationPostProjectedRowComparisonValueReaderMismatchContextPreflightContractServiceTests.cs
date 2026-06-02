using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractServiceTests
{
	[Fact]
	public void Create_DefaultContextPreflightBlocksBeforeTypedReaderPreflightReadiness()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedPreflightNotReady, contract.Status);
		Assert.False(contract.IsLive);
		Assert.True(contract.HasValueReaderPreflight);
		Assert.True(contract.HasRuntimeContextFields);
		Assert.False(contract.CanReadContextValues);
		Assert.False(contract.CanAttachContext);
		Assert.False(contract.CanEmitComparisonResult);
		Assert.Equal(4, contract.Fields.Count);
		Assert.Equal(["traceSource", "serverEpochSeconds"], contract.RuntimeContextFieldNames);
		Assert.Contains("typed-reader preflight reaches implementation-readiness", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", contract.TraceName);
		Assert.Contains("addRecruitment/addApplication", contract.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_AllowsOnlyMissingRowsAndFieldMismatchAsContextTriggers()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.FieldMismatch,
			],
			contract.AllowedTriggers);
		Assert.All(contract.Fields, field =>
		{
			Assert.False(field.IsEqualityInput);
			Assert.False(field.CanReadContextValues);
			Assert.False(field.CanAttachContext);
			Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingJavaRow, field.AllowedTriggers);
			Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.MissingCSharpRow, field.AllowedTriggers);
			Assert.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextTrigger.FieldMismatch, field.AllowedTriggers);
			Assert.DoesNotContain("Matched", field.AttachmentRule, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_MapsTraceSourceAndServerEpochSecondsAsRuntimeOnlyContext()
	{
		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create();

		Assert.Contains(contract.Fields, field =>
			field.Action == 2
			&& field.FieldName == "traceSource"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			&& field.JavaJsonPath == "$.traces[*].traceSource"
			&& field.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.TraceSource"
			&& field.AttachmentRule.Contains("only after a real MissingJavaRow, MissingCSharpRow, or FieldMismatch result exists", StringComparison.Ordinal));
		Assert.Contains(contract.Fields, field =>
			field.Action == 6
			&& field.FieldName == "serverEpochSeconds"
			&& field.ReaderKind == FindGroupMutationPostProjectedRowComparisonValueReaderKind.IgnoredRuntimeContext
			&& field.JavaJsonPath == "$.traces[*].serverEpochSeconds"
			&& field.CSharpAccessor == "FindGroupDirectPacketMutationPostBoundaryTraceExport.ServerEpochSeconds"
			&& field.Notes.Contains("must not affect equality", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceReadyPreflightStillDefersContextAttachment()
	{
		var design = ReadyDesignContract();
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderPreflightContractService.Create(design);

		var contract = FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightContractService.Create(preflight);

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextPreflightStatus.BlockedContextAttachmentDeferred, contract.Status);
		Assert.Contains("ignored runtime fields can only attach", contract.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(contract.Fields, field =>
			field.FieldName == "traceSource"
			&& field.Status == FindGroupMutationPostProjectedRowComparisonValueReaderMismatchContextFieldStatus.BlockedContextAttachmentDeferred
			&& field.Blocker.Contains("real missing-row or FieldMismatch result", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderDesignContract ReadyDesignContract()
	{
		var gate = new FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateReport(
			FindGroupMutationPostProjectedRowComparisonExecutionReadinessGateStatus.BlockedComparatorNotAllowed,
			[],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasRuntimeEvidence: true,
			CanImplementComparator: false,
			CanExecuteComparator: false,
			CanClaimVerifiedParity: false,
			CanEnableLiveDispatch: false,
			"Runtime evidence exists, but comparator implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

		return FindGroupMutationPostProjectedRowComparisonValueReaderDesignContractService.Create(gate);
	}
}
