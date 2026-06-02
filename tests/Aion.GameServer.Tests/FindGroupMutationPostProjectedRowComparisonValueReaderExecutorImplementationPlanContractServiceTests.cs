using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractServiceTests
{
	[Fact]
	public void Create_DefaultPlanBlocksBeforeExecutorReadinessGate()
	{
		var plan = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorReadinessGateNotReady, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.HasExecutorReadinessGate);
		Assert.True(plan.HasComparatorPreflight);
		Assert.False(plan.HasRuntimeEvidence);
		Assert.Equal(38, plan.EqualityFieldCount);
		Assert.Equal(4, plan.RuntimeContextFieldCount);
		Assert.False(plan.CanImplementExecutor);
		Assert.False(plan.CanExecuteExecutor);
		Assert.False(plan.CanReadJavaValues);
		Assert.False(plan.CanReadCSharpValues);
		Assert.False(plan.CanCompareValues);
		Assert.False(plan.CanAttachRuntimeContext);
		Assert.False(plan.CanEmitResults);
		Assert.False(plan.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", plan.TraceName);
		Assert.Contains("addRecruitment/addApplication", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("executor readiness gate", plan.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultPlanListsImplementationTasksWithoutExecuting()
	{
		var plan = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create();

		Assert.Equal(
			[
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment,
				FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission,
			],
			plan.Steps.Select(step => step.Step));
		Assert.All(plan.Steps, step =>
		{
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedExecutorReadinessGateNotReady, step.Status);
			Assert.False(step.CanImplement);
			Assert.False(step.CanExecute);
			Assert.False(step.CanReadValues);
			Assert.False(step.CanCompareValues);
			Assert.False(step.CanAttachContext);
			Assert.False(step.CanEmitResults);
		});
	}

	[Fact]
	public void Create_ReadyGateStillDefersConcreteExecutorSteps()
	{
		var plan = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create(
			ReadyExecutorGate(),
			ReadyComparatorPreflight());

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStatus.BlockedExecutorImplementationDeferred, plan.Status);
		Assert.True(plan.HasRuntimeEvidence);
		Assert.Contains("row pairing, typed reads, comparison, context attachment, result emission, and verified parity remain intentionally deferred", plan.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.RowIdentityPairing
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedRuntimeRowsMissing);
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.JavaTypedValueRead
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedReaderImplementationDeferred
			&& step.RequiresJavaValueReader
			&& !step.RequiresCSharpValueReader);
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.CSharpTypedValueRead
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedReaderImplementationDeferred
			&& !step.RequiresJavaValueReader
			&& step.RequiresCSharpValueReader);
	}

	[Fact]
	public void Create_ComparisonAndResultSelectionNameAllowedOutputKinds()
	{
		var plan = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create(
			ReadyExecutorGate(),
			ReadyComparatorPreflight());

		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.EqualityComparison
			&& step.RequiresProjectedValues
			&& step.OutputKinds.SequenceEqual(
			[
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched,
				FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.FieldMismatch,
			])
			&& step.Notes.Contains("Matched is allowed only when every equality field matches", StringComparison.Ordinal));
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultSelection
			&& step.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingJavaRow)
			&& step.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.MissingCSharpRow)
			&& step.ImplementationTask.Contains("Select exactly one comparison result", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ContextAttachmentAndResultEmissionRemainBlocked()
	{
		var plan = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanContractService.Create(
			ReadyExecutorGate(),
			ReadyComparatorPreflight());

		Assert.False(plan.CanAttachRuntimeContext);
		Assert.False(plan.CanEmitResults);
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.MismatchContextAttachment
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedContextAttachmentDeferred
			&& step.OutputKinds.SequenceEqual([FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext])
			&& step.Notes.Contains("must never affect equality", StringComparison.Ordinal));
		Assert.Contains(plan.Steps, step =>
			step.Step == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStep.ResultEmission
			&& step.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorImplementationPlanStepStatus.BlockedResultEmissionDeferred
			&& step.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.Matched)
			&& step.OutputKinds.Contains(FindGroupMutationPostProjectedRowComparisonDryRunOutputKind.IgnoredRuntimeContext)
			&& step.Notes.Contains("runtime-backed Java and C# rows", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateReport ReadyExecutorGate() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateStatus.BlockedExecutorImplementationDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRow(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGate.ExecutorImplementation,
					FindGroupMutationPostProjectedRowComparisonValueReaderExecutorReadinessGateRowStatus.BlockedExecutorImplementationDeferred,
					HasPrerequisite: true,
					HasRuntimeEvidence: true,
					BlocksExecutorImplementation: true,
					CanImplementExecutor: false,
					CanExecuteExecutor: false,
					CanEnableLiveDispatch: false,
					"hasRuntimeEvidence=True",
					"executor implementation still deferred",
					"test row"),
			],
			HasLiveInputHandoff: true,
			HasRuntimeEvidenceChecklist: true,
			HasComparatorPreflight: true,
			HasRuntimeEvidence: true,
			CanImplementExecutor: false,
			CanExecuteExecutor: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanEmitResults: false,
			CanEnableLiveDispatch: false,
			CanClaimVerifiedParity: false,
			"Value-reader executor implementation remains intentionally deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);

	private static FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightContract ReadyComparatorPreflight() =>
		new(
			FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStatus.BlockedComparatorImplementationDeferred,
			[
				new FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageRow(
					1,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStage.RowIdentityPairing,
					FindGroupMutationPostProjectedRowComparisonValueReaderComparatorPreflightStageStatus.BlockedRuntimeRowsMissing,
					EqualityFieldCount: 38,
					RuntimeContextFieldCount: 4,
					OutputKinds: [],
					RequiresAcceptedJavaRows: true,
					RequiresAcceptedCSharpRows: true,
					RequiresProjectedValues: false,
					RequiresResultSchema: true,
					CanExecute: false,
					CanProjectValues: false,
					CanCompareValues: false,
					CanEmitResults: false,
					"ready comparator stage",
					"evidence",
					"notes"),
			],
			EqualityFieldCount: 38,
			RuntimeContextFieldCount: 4,
			HasImplementationRunbook: true,
			HasResultSchema: true,
			CanExecuteComparator: false,
			CanProjectValues: false,
			CanCompareValues: false,
			CanAttachRuntimeContext: false,
			CanEmitResults: false,
			"Comparator implementation remains deferred.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
}
