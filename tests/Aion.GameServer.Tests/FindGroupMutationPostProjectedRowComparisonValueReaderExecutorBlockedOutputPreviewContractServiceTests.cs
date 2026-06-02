using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractServiceTests
{
	[Fact]
	public void Create_DefaultPreviewBlocksBeforeImplementationPlanReadiness()
	{
		var preview = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedImplementationPlanNotReady, preview.Status);
		Assert.False(preview.IsLive);
		Assert.True(preview.HasImplementationPlan);
		Assert.True(preview.HasResultSchema);
		Assert.False(preview.HasRuntimeEvidence);
		Assert.Equal(5, preview.OutputKindCount);
		Assert.Equal(0, preview.MaterializableOutputCount);
		Assert.Equal(0, preview.EmittableOutputCount);
		Assert.False(preview.CanMaterializeMatched);
		Assert.False(preview.CanMaterializeMissingJavaRow);
		Assert.False(preview.CanMaterializeMissingCSharpRow);
		Assert.False(preview.CanMaterializeFieldMismatch);
		Assert.False(preview.CanAttachIgnoredRuntimeContext);
		Assert.False(preview.CanEmitAnyResult);
		Assert.False(preview.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", preview.TraceName);
		Assert.Contains("addRecruitment/addApplication", preview.JavaSource, StringComparison.Ordinal);
		Assert.Contains("implementation-plan and result-schema metadata", preview.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultPreviewListsEveryOutputKindAsUnavailable()
	{
		var preview = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			preview.Rows.Select(row => row.OutputKind));
		Assert.All(preview.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedImplementationPlanNotReady, row.Status);
			Assert.True(row.HasImplementationPlanStep);
			Assert.True(row.HasResultSchemaRow);
			Assert.False(row.CanMaterializeOutput);
			Assert.False(row.CanEmitResult);
		});
	}

	[Fact]
	public void Create_RuntimeMissingPreviewMapsOutputSpecificBlockers()
	{
		var preview = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create(
			ImplementationPlan(hasRuntimeEvidence: false),
			ReadyResultSchema());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing, preview.Status);
		Assert.Contains("runtime-backed Java rows, accepted C# rows, and projected values are missing", preview.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(preview.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiresProjectedValues
			&& row.RequiredExecutorStep == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison.ToString());
		Assert.Contains(preview.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.RequiresMissingRowDecision
			&& row.RequiredExecutorStep == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection.ToString());
		Assert.Contains(preview.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedContextAttachmentUnavailable
			&& row.AllowsRuntimeContextAttachment
			&& row.RequiredExecutorStep == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment.ToString());
	}

	[Fact]
	public void Create_RuntimeEvidenceStillDefersEveryOutputEmission()
	{
		var preview = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create(
			ImplementationPlan(hasRuntimeEvidence: true),
			ReadyResultSchema());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred, preview.Status);
		Assert.True(preview.HasRuntimeEvidence);
		Assert.False(preview.CanEmitAnyResult);
		Assert.All(preview.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedResultEmissionDeferred, row.Status);
			Assert.False(row.CanMaterializeOutput);
			Assert.False(row.CanEmitResult);
			Assert.Contains("BlockedOutputEmissionDeferred", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_IgnoredRuntimeContextIsNotStandaloneOutput()
	{
		var preview = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContractService.Create(
			ImplementationPlan(hasRuntimeEvidence: false),
			ReadyResultSchema());

		Assert.False(preview.CanAttachIgnoredRuntimeContext);
		Assert.Contains(preview.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& !row.RequiresProjectedValues
			&& !row.RequiresMissingRowDecision
			&& row.AllowsRuntimeContextAttachment
			&& row.SchemaFields.SequenceEqual(["traceSource", "serverEpochSeconds"])
			&& row.Notes.Contains("standalone output", StringComparison.Ordinal)
			&& row.Notes.Contains("MissingJavaRow, MissingCSharpRow, or FieldMismatch", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContract ImplementationPlan(
		bool hasRuntimeEvidence) =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorImplementationDeferred,
			[
				PlanStep(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison),
				PlanStep(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection),
				PlanStep(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment),
			],
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			HasExecutorReadinessGate: true,
			HasComparatorPreflight: true,
			HasRuntimeEvidence: hasRuntimeEvidence,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanReadJavaValues: false,
			CanReadCSharpValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Implementation plan remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepRow PlanStep(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep step) =>
		new(
			order,
			step,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedResultEmissionDeferred,
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			OutputKinds: [],
			RequiresAcceptedJavaRows: true,
			RequiresAcceptedCSharpRows: true,
			RequiresJavaValueReader: true,
			RequiresCSharpValueReader: true,
			RequiresProjectedValues: true,
			RequiresResultSchema: true,
			CanImplement: false,
			CanExecute: false,
			CanReadValues: false,
			CanCompareValues: false,
			CanAttachContext: false,
			CanEmitResults: false,
			"test task",
			"test provider",
			"test evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaContract ReadyResultSchema() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaStatus.BlockedResultSchemaDeferred,
			[
				ResultRow(
					1,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
					["action", "mutationKind", "rowIdentity", "matchedFields", "matchedFieldCount"],
					RequiresProjectedValues: true,
					RequiresMissingRowDecision: false,
					AllowsRuntimeContextAttachment: false),
				ResultRow(
					2,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
					["action", "mutationKind", "rowIdentity", "csharpRowReference", "runtimeContext"],
					RequiresProjectedValues: false,
					RequiresMissingRowDecision: true,
					AllowsRuntimeContextAttachment: true),
				ResultRow(
					3,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
					["action", "mutationKind", "rowIdentity", "javaRowReference", "runtimeContext"],
					RequiresProjectedValues: false,
					RequiresMissingRowDecision: true,
					AllowsRuntimeContextAttachment: true),
				ResultRow(
					4,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
					["action", "mutationKind", "rowIdentity", "fieldName", "differenceKind", "javaValue", "csharpValue", "javaSource", "runtimeContext"],
					RequiresProjectedValues: true,
					RequiresMissingRowDecision: false,
					AllowsRuntimeContextAttachment: true),
				ResultRow(
					5,
					FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
					["traceSource", "serverEpochSeconds"],
					RequiresProjectedValues: false,
					RequiresMissingRowDecision: false,
					AllowsRuntimeContextAttachment: true),
			],
			DifferenceKinds: [FindGroupMutationPostComparisonDifferenceKind.DirectPacketMismatch],
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			HasImplementationRunbook: true,
			HasResultContract: true,
			CanProjectValues: false,
			CanAttachRuntimeContext: false,
			CanEmitMatched: false,
			CanEmitFieldMismatch: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitIgnoredRuntimeContext: false,
			"Result schema remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRow ResultRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		IReadOnlyList<string> schemaFields,
		bool RequiresProjectedValues,
		bool RequiresMissingRowDecision,
		bool AllowsRuntimeContextAttachment) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderResultSchemaRowStatus.BlockedValueProjectionDeferred,
			schemaFields,
			RequiresProjectedValues,
			RequiresMissingRowDecision,
			AllowsRuntimeContextAttachment,
			CanEmitResult: false,
			"test producer",
			"test evidence",
			"test notes");
}
