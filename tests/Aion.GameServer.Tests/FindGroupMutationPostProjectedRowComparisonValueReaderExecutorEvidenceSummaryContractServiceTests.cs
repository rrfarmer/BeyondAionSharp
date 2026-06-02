using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractServiceTests
{
	[Fact]
	public void Create_DefaultSummaryBlocksUntilUpstreamMetadataIsReady()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedUpstreamMetadataNotReady, summary.Status);
		Assert.False(summary.IsLive);
		Assert.True(summary.HasBlockedOutputPreview);
		Assert.True(summary.HasRuntimeEvidenceIntake);
		Assert.True(summary.HasMaterializationPreflight);
		Assert.True(summary.HasResultEmissionGate);
		Assert.False(summary.HasAnyRuntimeEvidence);
		Assert.False(summary.CanImplementExecutor);
		Assert.False(summary.CanExecuteExecutor);
		Assert.False(summary.CanMaterializeOutputs);
		Assert.False(summary.CanEmitResults);
		Assert.False(summary.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", summary.TraceName);
		Assert.Contains("addRecruitment/addApplication", summary.JavaSource, StringComparison.Ordinal);
		Assert.Contains("upstream output-preview, intake, materialization, and emission-gate metadata are ready", summary.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultSummaryListsEveryEvidenceRequirement()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.BlockedOutputPreview,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeEvidenceIntake,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.MaterializationPreflight,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.ResultEmissionGate,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison,
			],
			summary.Rows.Select(row => row.Requirement));
		Assert.All(summary.Rows, row =>
		{
			Assert.True(row.HasProvider);
			Assert.False(row.HasRuntimeEvidence);
			Assert.True(row.BlocksExecutorImplementation);
			Assert.True(row.BlocksVerifiedParity);
		});
		Assert.Contains(summary.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedRuntimeComparisonMissing);
	}

	[Fact]
	public void Create_RuntimeMissingSummaryNamesEveryMissingRuntimeInput()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create(
			Preview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing),
			Intake(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing),
			Preflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedRuntimeEvidenceMissing),
			Gate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedRuntimeEvidenceMissing, summary.Status);
		Assert.Contains("runtime-backed Java rows, accepted C# rows, value projection, result materialization, and runtime comparison evidence are missing", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeEvidenceIntake
			&& row.ProviderStatus == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing.ToString()
			&& row.RequiredEvidence.Contains("Java artifact rows", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("C# boundary rows", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("registry observation", StringComparison.Ordinal));
		Assert.Contains(summary.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.RuntimeComparison
			&& row.CurrentEvidence.Contains("intakeRuntimeComparisonRows=1", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("gateRowsRequireRuntimeComparison=5", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyShapedMetadataStillBlocksImplementationAndEmission()
	{
		var summary = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryContractService.Create(
			Preview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred),
			Intake(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable),
			Preflight(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus.BlockedOutputPreviewNotMaterializable),
			Gate(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedResultEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryStatus.BlockedResultEmissionDeferred, summary.Status);
		Assert.False(summary.HasAnyRuntimeEvidence);
		Assert.False(summary.CanImplementExecutor);
		Assert.False(summary.CanExecuteExecutor);
		Assert.False(summary.CanMaterializeOutputs);
		Assert.False(summary.CanEmitResults);
		Assert.False(summary.CanClaimVerifiedParity);
		Assert.Contains("executor implementation, result emission, runtime comparison, and verified parity remain disabled", summary.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(summary.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRequirement.ResultEmissionGate
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorEvidenceSummaryRowStatus.BlockedResultEmissionDisabled
			&& row.CurrentEvidence.Contains("emittableOutputs=0", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract Preview(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus status) =>
		new(
			status,
			[
				PreviewRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
				PreviewRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
				PreviewRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
				PreviewRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
				PreviewRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRow PreviewRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewRowStatus.BlockedResultEmissionDeferred,
			["testField"],
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContract Intake(
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

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightContract Preflight(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightStatus status) =>
		new(
			status,
			[
				PreflightRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
				PreflightRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
				PreflightRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
				PreflightRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
				PreflightRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
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
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorMaterializationPreflightRowStatus.BlockedRuntimeEvidenceMissing,
			[FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison],
			["testField"],
			HasPreviewRow: true,
			HasRuntimeEvidenceIntake: true,
			HasAllRequiredIntakeRows: true,
			CanMaterializeOutput: false,
			CanEmitResult: false,
			"test blocking evidence",
			"test required evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateContract Gate(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus status) =>
		new(
			status,
			[
				GateRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
				GateRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
				GateRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
				GateRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
				GateRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
			],
			OutputKindCount: 5,
			EmittableOutputCount: 0,
			HasMaterializationPreflight: true,
			HasAnyRuntimeEvidence: status != FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateStatus.BlockedRuntimeEvidenceMissing,
			CanEmitMatched: false,
			CanEmitMissingJavaRow: false,
			CanEmitMissingCSharpRow: false,
			CanEmitFieldMismatch: false,
			CanEmitIgnoredRuntimeContext: false,
			CanEmitAnyResult: false,
			CanClaimVerifiedParity: false,
			"Result emission gate remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRow GateRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonDryRunOutputKind outputKind) =>
		new(
			order,
			outputKind,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorResultEmissionGateRowStatus.BlockedRuntimeEvidenceMissing,
			["Runtime comparison evidence exists for action 2 and action 6."],
			["testField"],
			HasMaterializationPreflightRow: true,
			RequiresMaterializedOutput: outputKind != FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			RequiresRuntimeComparison: true,
			RequiresParentResult: outputKind == FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext,
			CanEmitResult: false,
			"test blocking evidence",
			"test notes");
}
