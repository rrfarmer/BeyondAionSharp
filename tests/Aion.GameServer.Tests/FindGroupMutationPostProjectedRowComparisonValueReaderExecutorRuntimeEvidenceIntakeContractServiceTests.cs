using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractServiceTests
{
	[Fact]
	public void Create_DefaultIntakeBlocksBeforeLiveInputHandoffReadiness()
	{
		var intake = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedLiveInputHandoffNotReady, intake.Status);
		Assert.False(intake.IsLive);
		Assert.True(intake.HasLiveInputHandoff);
		Assert.True(intake.HasRuntimeEvidenceChecklist);
		Assert.True(intake.HasBlockedOutputPreview);
		Assert.False(intake.HasAnyRuntimeEvidence);
		Assert.False(intake.CanStartValueProjection);
		Assert.False(intake.CanMaterializeOutputs);
		Assert.False(intake.CanEmitResults);
		Assert.False(intake.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", intake.TraceName);
		Assert.Contains("addRecruitment/addApplication", intake.JavaSource, StringComparison.Ordinal);
		Assert.Contains("live-input handoff", intake.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultIntakeListsEveryRuntimeEvidenceRequirement()
	{
		var intake = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.BoundaryExecutorObservation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RegistrySendObservation,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RowIdentityMatching,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ValueProjection,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison,
			],
			intake.Rows.Select(row => row.Requirement));
		Assert.All(intake.Rows, row =>
		{
			Assert.False(row.HasRuntimeEvidence);
			Assert.True(row.RequiredForVerifiedParity);
			Assert.False(row.CanMaterializeOutput);
		});
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison
			&& !row.RequiredForMaterialization);
	}

	[Fact]
	public void Create_RuntimeReadyHandoffStillBlocksRuntimeEvidenceMissing()
	{
		var liveInputHandoff = RuntimeArtifactReadyHandoff();
		var checklist = FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistService.Create(liveInputHandoff);
		var intake = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create(
			liveInputHandoff,
			checklist,
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedRuntimeEvidenceMissing, intake.Status);
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedRuntimeEvidenceMissing
			&& row.CurrentEvidence.Contains("JavaRuntimeTraceArtifact", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("Runtime-backed Java action 2 and action 6", StringComparison.Ordinal));
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.CSharpBoundaryRows
			&& row.RequiredEvidence.Contains("Accepted live C# boundary rows", StringComparison.Ordinal));
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RegistrySendObservation
			&& row.RequiredEvidence.Contains("posted SM_SYSTEM_MESSAGE before refreshed SM_FIND_GROUP", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_OutputPrerequisitesRowNamesEveryBlockedOutput()
	{
		var intake = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create(
			RuntimeArtifactReadyHandoff(),
			null,
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedRuntimeRowsMissing));

		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.ResultOutputPrerequisites
			&& row.RequiredEvidence.Contains("Matched", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("MissingJavaRow", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("MissingCSharpRow", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("FieldMismatch", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("IgnoredRuntimeContext", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("outputKinds=5", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("materializableOutputs=0", StringComparison.Ordinal)
			&& row.CurrentEvidence.Contains("emittableOutputs=0", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeEvidenceFlagsStillBlockOutputMaterialization()
	{
		var intake = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeContractService.Create(
			RuntimeArtifactReadyHandoff(),
			RuntimeEvidencePresentChecklist(),
			OutputPreview(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus.BlockedOutputEmissionDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeStatus.BlockedOutputPreviewNotMaterializable, intake.Status);
		Assert.False(intake.HasAnyRuntimeEvidence);
		Assert.False(intake.CanStartValueProjection);
		Assert.False(intake.CanMaterializeOutputs);
		Assert.False(intake.CanEmitResults);
		Assert.False(intake.CanClaimVerifiedParity);
		Assert.All(intake.Rows, row =>
		{
			Assert.False(row.HasRuntimeEvidence);
			Assert.False(row.CanMaterializeOutput);
		});
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.JavaArtifactRows
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedOutputPreviewNotMaterializable);
		Assert.Contains(intake.Rows, row =>
			row.Requirement == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRequirement.RuntimeComparison
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeEvidenceIntakeRowStatus.BlockedRuntimeComparisonMissing);
		Assert.Contains("blocked-output preview rows still cannot materialize", intake.ExecutionDecision, StringComparison.Ordinal);
	}

	private static FindGroupMutationPostProjectedRowComparisonLiveInputHandoffContract RuntimeArtifactReadyHandoff() =>
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

	private static FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist RuntimeEvidencePresentChecklist()
	{
		var liveInputHandoff = RuntimeArtifactReadyHandoff();
		var rows = liveInputHandoff.Requirements
			.Select(requirement => new FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistRow(
				requirement.Order,
				requirement.Requirement,
				requirement.Requirement == FindGroupMutationPostProjectedRowComparisonLiveInputRequirement.RuntimeSocketComparison
					? FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ComparisonNotExecuted
					: FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceProviderStatus.ExistingNonLiveScaffold,
				HasExistingProvider: true,
				HasRuntimeEvidence: true,
				BlocksVerifiedParity: true,
				"test provider",
				"test required runtime evidence",
				$"sourceRequirement={requirement.Requirement}; test runtime evidence present",
				"test notes"))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklist(
			FindGroupMutationPostProjectedRowComparisonRuntimeEvidenceChecklistStatus.BlockedRuntimeEvidenceMissing,
			rows,
			HasLiveInputHandoff: true,
			HasExistingNonLiveProviders: true,
			HasAnyRuntimeEvidence: true,
			CanStartProjectedComparison: false,
			CanClaimVerifiedParity: false,
			"Runtime evidence is represented for intake tests, but comparison remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewContract OutputPreview(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorBlockedOutputPreviewStatus status) =>
		new(
			status,
			[
				OutputRow(1, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched),
				OutputRow(2, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow),
				OutputRow(3, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow),
				OutputRow(4, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch),
				OutputRow(5, FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext),
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
}
