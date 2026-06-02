using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractServiceTests
{
	[Fact]
	public void Create_DefaultGateBlocksUntilMaterializationPreflightIsReady()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedMaterializationPreflightNotReady, gate.Status);
		Assert.False(gate.IsLive);
		Assert.True(gate.HasMaterializationPreflight);
		Assert.False(gate.HasAnyRuntimeEvidence);
		Assert.Equal(0, gate.EmittableOutputCount);
		Assert.False(gate.CanEmitMatched);
		Assert.False(gate.CanEmitMissingJavaRow);
		Assert.False(gate.CanEmitMissingCSharpRow);
		Assert.False(gate.CanEmitFieldMismatch);
		Assert.False(gate.CanEmitIgnoredRuntimeContext);
		Assert.False(gate.CanEmitAnyResult);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", gate.TraceName);
		Assert.Contains("addRecruitment/addApplication", gate.JavaSource, StringComparison.Ordinal);
		Assert.Contains("materialization preflight is ready", gate.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultGateListsEveryResultOutputAsNonEmittable()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			gate.Rows.Select(row => row.OutputKind));
		Assert.All(gate.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMaterializationPreflightNotReady, row.Status);
			Assert.True(row.HasMaterializationPreflightRow);
			Assert.True(row.RequiresRuntimeComparison);
			Assert.False(row.CanEmitResult);
			Assert.Contains("canMaterializeOutput=False", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_RuntimeMissingGateMapsOutputSpecificBlockers()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create(
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing, gate.Status);
		Assert.Contains("runtime evidence, row decisions, value projection, and context attachment are missing", gate.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(gate.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("All projected equality values compare equal", StringComparison.Ordinal)));
		Assert.Contains(gate.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedMissingRowDecisionUnavailable
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("runtime-backed Java row has no matching accepted live C# row", StringComparison.Ordinal)));
		Assert.Contains(gate.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedContextAttachmentUnavailable
			&& row.RequiresParentResult);
	}

	[Fact]
	public void Create_MatchedEmissionRequiresEqualityAndNoRuntimeContext()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create(
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(gate.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.RequiresMaterializedOutput
			&& !row.RequiresParentResult
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("Every schema equality field", StringComparison.Ordinal))
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("No ignored runtime context", StringComparison.Ordinal))
			&& row.Notes.Contains("equality comparison", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_IgnoredRuntimeContextRequiresParentResultAndIsNotStandalone()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create(
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing));

		Assert.Contains(gate.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& !row.RequiresMaterializedOutput
			&& row.RequiresParentResult
			&& row.RequiredSchemaFields.SequenceEqual(["traceSource", "serverEpochSeconds"])
			&& row.RequiredEmissionConditions.Any(condition => condition.Contains("not emitted as an independent result row", StringComparison.Ordinal))
			&& row.Notes.Contains("never creates a standalone result", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_PreflightReadyStillKeepsResultEmissionDisabled()
	{
		var gate = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContractService.Create(
			MaterializationPreflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedOutputPreviewNotMaterializable));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred, gate.Status);
		Assert.False(gate.HasAnyRuntimeEvidence);
		Assert.False(gate.CanEmitAnyResult);
		Assert.False(gate.CanClaimVerifiedParity);
		Assert.All(gate.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedResultEmissionDisabled, row.Status);
			Assert.False(row.CanEmitResult);
			Assert.Contains("gateStatus=BlockedResultEmissionDeferred", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract MaterializationPreflight(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus status) =>
		new(
			status,
			[
				PreflightRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, ["matchedFields", "matchedFieldCount"]),
				PreflightRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, ["csharpRowReference", "runtimeContext"]),
				PreflightRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, ["javaRowReference", "runtimeContext"]),
				PreflightRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, ["fieldName", "javaValue", "csharpValue", "runtimeContext"]),
				PreflightRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, ["traceSource", "serverEpochSeconds"]),
			],
			OutputKindCount: 5,
			MaterializableOutputCount: 0,
			EmittableOutputCount: 0,
			HasRuntimeEvidenceIntake: true,
			HasBlockedOutputPreview: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Materialization preflight remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRow PreflightRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		IReadOnlyList<string> schemaFields) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedRuntimeEvidenceMissing,
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			],
			schemaFields,
			HasPreviewRow: true,
			HasRuntimeEvidenceIntake: true,
			HasAllRequiredIntakeRows: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"test blocking evidence",
			"test required evidence",
			"test notes");
}
