using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksUntilRuntimeEvidenceIntakeIsReady()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedIntakeNotReady, preflight.Status);
		Assert.False(preflight.IsLive);
		Assert.True(preflight.HasRuntimeEvidenceIntake);
		Assert.True(preflight.HasBlockedOutputPreview);
		Assert.False(preflight.HasAnyRuntimeEvidence);
		Assert.Equal(0, preflight.MaterializableOutputCount);
		Assert.Equal(0, preflight.EmittableOutputCount);
		Assert.False(preflight.CanMaterializeMatched);
		Assert.False(preflight.CanMaterializeMissingJavaRow);
		Assert.False(preflight.CanMaterializeMissingCSharpRow);
		Assert.False(preflight.CanMaterializeFieldMismatch);
		Assert.False(preflight.CanAttachIgnoredRuntimeContext);
		Assert.False(preflight.CanEmitAnyResult);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", preflight.TraceName);
		Assert.Contains("addRecruitment/addApplication", preflight.JavaSource, StringComparison.Ordinal);
		Assert.Contains("runtime-evidence intake is ready", preflight.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultPreflightListsEveryBlockedPreviewOutput()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			],
			preflight.Rows.Select(row => row.OutputKind));
		Assert.All(preflight.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedIntakeNotReady, row.Status);
			Assert.True(row.HasPreviewRow);
			Assert.True(row.HasRuntimeEvidenceIntake);
			Assert.True(row.HasAllRequiredIntakeRows);
			Assert.False(row.CanMaterializeOutput);
			Assert.False(row.CanEmitResult);
		});
	}

	[Fact]
	public void Create_RuntimeMissingPreflightMapsOutputSpecificBlockers()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create(
			RuntimeEvidenceIntake(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing),
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing, preflight.Status);
		Assert.Contains("Java/C# runtime evidence and output prerequisites are missing", preflight.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(preflight.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedValueProjectionUnavailable
			&& row.RequiredIntakeRequirements.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection)
			&& row.RequiredEvidence.Contains("all equality fields match", StringComparison.Ordinal));
		Assert.Contains(preflight.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedMissingRowDecisionUnavailable
			&& !row.RequiredIntakeRequirements.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection)
			&& row.RequiredEvidence.Contains("no matching runtime-backed Java row", StringComparison.Ordinal));
		Assert.Contains(preflight.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedContextAttachmentUnavailable
			&& row.RequiredEvidence.Contains("cannot materialize as a standalone output", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_FieldMismatchRequiresValueProjectionAndRuntimeComparison()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create(
			RuntimeEvidenceIntake(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing),
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing));

		Assert.Contains(preflight.Rows, row =>
			row.OutputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch
			&& row.RequiredIntakeRequirements.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection)
			&& row.RequiredIntakeRequirements.Contains(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison)
			&& row.RequiredEvidence.Contains("projected Java/C# equality values", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("diagnostic context attachment", StringComparison.Ordinal)
			&& row.Notes.Contains("field-specific", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_OutputPreviewReadyStillBlocksRuntimeComparisonAndEmission()
	{
		var preflight = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContractService.Create(
			RuntimeEvidenceIntake(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable),
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedOutputPreviewNotMaterializable, preflight.Status);
		Assert.False(preflight.HasAnyRuntimeEvidence);
		Assert.False(preflight.CanEmitAnyResult);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.All(preflight.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedRuntimeComparisonMissing, row.Status);
			Assert.False(row.CanMaterializeOutput);
			Assert.False(row.CanEmitResult);
			Assert.Contains("canMaterializePreviewRow=False", row.BlockingEvidence, StringComparison.Ordinal);
		});
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract RuntimeEvidenceIntake(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus status) =>
		new(
			status,
			[
				IntakeRow(1, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows),
				IntakeRow(2, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows),
				IntakeRow(3, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.BoundaryExecutorObservation),
				IntakeRow(4, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RegistrySendObservation),
				IntakeRow(5, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching),
				IntakeRow(6, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection),
				IntakeRow(7, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites),
				IntakeRow(8, FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison),
			],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasBlockedOutputPreview: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing,
			CanStartValueProjection: false,
			CanMaterializeOutputs: false,
			CanEmitResults: false,
			CanClaimVerifiedParity: false,
			"Runtime evidence intake remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRow IntakeRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement requirement) =>
		new(
			order,
			requirement,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedRuntimeEvidenceMissing,
			HasExistingProvider: true,
			HasRuntimeEvidence: false,
			RequiredForMaterialization: requirement != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			RequiredForVerifiedParity: true,
			CanMaterializeOutput: false,
			"test required evidence",
			"test current evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract OutputPreview(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus status) =>
		new(
			status,
			[
				OutputRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched, ["matchedFields"]),
				OutputRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow, ["csharpRowReference", "runtimeContext"]),
				OutputRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow, ["javaRowReference", "runtimeContext"]),
				OutputRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch, ["fieldName", "javaValue", "csharpValue", "runtimeContext"]),
				OutputRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext, ["traceSource", "serverEpochSeconds"]),
			],
			OutputKindCount: 5,
			MaterializableOutputCount: 0,
			EmittableOutputCount: 0,
			HasImplementationPlan: true,
			HasResultSchema: true,
			HasRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing,
			CanMaterializeMatched: false,
			CanMaterializeMissingJavaRow: false,
			CanMaterializeMissingCSharpRow: false,
			CanMaterializeFieldMismatch: false,
			CanAttachIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Blocked-output preview remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow OutputRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind,
		IReadOnlyList<string> schemaFields) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedResultEmissionDeferred,
			schemaFields,
			RequiresProjectedValues: outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			RequiresMissingRowDecision: outputKind is FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow or FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow,
			AllowsRuntimeContextAttachment: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
			HasImplementationPlanStep: true,
			HasResultSchemaRow: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"test step",
			"test blocking evidence",
			"test notes");
}
