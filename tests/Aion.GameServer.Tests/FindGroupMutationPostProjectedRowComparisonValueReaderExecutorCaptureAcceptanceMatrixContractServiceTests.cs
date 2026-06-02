using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractServiceTests
{
	[Fact]
	public void Create_DefaultMatrixBlocksUntilLiveCapturePreflightIsReady()
	{
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create();

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedLiveCapturePreflightNotReady, matrix.Status);
		Assert.False(matrix.IsLive);
		Assert.True(matrix.HasLiveCapturePreflight);
		Assert.False(matrix.HasAnyRuntimeEvidence);
		Assert.False(matrix.AllAcceptanceGatesPassed);
		Assert.False(matrix.CanRunRuntimeComparison);
		Assert.False(matrix.CanStartExecutableImplementation);
		Assert.False(matrix.CanClaimVerifiedParity);
		Assert.Equal(10, matrix.RequiredEvidenceFieldCount);
		Assert.Equal(11, matrix.MissingEvidenceFieldCount);
		Assert.Equal(0, matrix.RuntimeComparisonBlockerCount);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", matrix.TraceName);
		Assert.Contains("addRecruitment/addApplication", matrix.JavaSource, StringComparison.Ordinal);
		Assert.Contains("live-capture preflight metadata is ready", matrix.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DefaultMatrixListsEveryAcceptanceFieldAsBlocked()
	{
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create();

		Assert.Equal(Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField>(), matrix.Rows.Select(row => row.Field));
		Assert.All(matrix.Rows, row =>
		{
			Assert.True(row.HasLiveCapturePreflight);
			Assert.True(row.HasProvider);
			Assert.False(row.HasRuntimeEvidence);
			Assert.False(row.AcceptancePassed);
			Assert.True(row.BlocksExecutableImplementation);
			Assert.True(row.BlocksVerifiedParity);
			Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.BlockedLiveCapturePreflightNotReady, row.Status);
		});
	}

	[Fact]
	public void Create_RuntimeMissingMatrixNamesCaptureEvidenceFieldsAndBlockers()
	{
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(
			Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedAcceptanceEvidenceMissing, matrix.Status);
		Assert.Equal(10, matrix.RuntimeComparisonBlockerCount);
		Assert.Contains("Java artifact, C# boundary, value projection, materialization, emission, and runtime comparison evidence fields are missing", matrix.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.JavaArtifactRows
			&& row.EvidenceField == "javaArtifactRowsPresent"
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingJavaArtifactEvidence
			&& row.RuntimeComparisonBlocker.Contains("Java source-of-truth rows", StringComparison.Ordinal)
			&& row.BlocksRuntimeComparison);
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.CSharpBoundaryRows
			&& row.EvidenceField == "csharpBoundaryRowsAccepted"
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingCSharpBoundaryEvidence
			&& row.RuntimeComparisonBlocker.Contains("disabled C# projections", StringComparison.Ordinal));
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RegistrySendObservation
			&& row.EvidenceField == "registrySendsObservedInOrder"
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRegistryObservation
			&& row.RequiredEvidence.Contains("Posted SmSystemMessage", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RuntimeMissingMatrixSeparatesProjectionMaterializationEmissionAndComparison()
	{
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(
			Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedCaptureEvidenceMissing));

		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RowIdentityMatching
			&& row.SourceStep == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingValueProjection
			&& row.RequiredEvidence.Contains("paired by action", StringComparison.Ordinal));
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ValueProjection
			&& row.EvidenceField == "projectedEqualityValuesRead"
			&& row.RequiredEvidence.Contains("visibleEntryObjectIdsAfterMutation", StringComparison.Ordinal));
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultMaterialization
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingMaterializedResults
			&& row.RuntimeComparisonBlocker.Contains("materialization", StringComparison.Ordinal));
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ResultEmission
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingResultEmission
			&& row.RequiredEvidence.Contains("ignored runtime context", StringComparison.Ordinal));
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.RuntimeComparisonExecution
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.MissingRuntimeComparison
			&& row.RuntimeComparisonBlocker.Contains("Verified parity is impossible", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyShapedRunbookStillBlocksRuntimeComparisonAndExecutableImplementation()
	{
		var matrix = FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixContractService.Create(
			Runbook(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus.BlockedExecutableImplementationDeferred));

		Assert.Equal(FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixStatus.BlockedRuntimeComparisonExecution, matrix.Status);
		Assert.False(matrix.CanRunRuntimeComparison);
		Assert.False(matrix.CanStartExecutableImplementation);
		Assert.False(matrix.CanClaimVerifiedParity);
		Assert.Contains("runtime comparison execution and executable implementation remain blocked", matrix.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains(matrix.Rows, row =>
			row.Field == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixField.ExecutableImplementationGate
			&& row.Status == FindGroupMutationPostProjectedRowComparisonValueReaderExecutorCaptureAcceptanceMatrixFieldStatus.ExecutableImplementationDeferred
			&& !row.BlocksRuntimeComparison
			&& row.RuntimeComparisonBlocker.Contains("must stay blocked", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract Runbook(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStatus status)
	{
		var rows = Enum.GetValues<FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep>()
			.Select((step, index) => RunbookRow(index + 1, step))
			.ToArray();

		return new FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookContract(
			status,
			rows,
			"parity-artifacts/find-group/mutation-post/java",
			"mvn -pl game-server -am test \"-Dtest=FindGroupMutationPostTraceCaptureTest\"",
			"GameServerConnectionFindGroupMutationPostGuardedLiveBoundaryFixture",
			"FindGroupRuntimeComparisonPreflightContractService",
			HasRuntimeComparisonHandoff: true,
			HasJavaArtifactCaptureRunbook: true,
			HasGuardedBoundarySkeleton: true,
			HasAnyRuntimeEvidence: false,
			CanRunJavaCapture: false,
			CanRunCSharpCapture: false,
			CanRunRuntimeComparison: false,
			CanStartExecutableImplementation: false,
			CanClaimVerifiedParity: false,
			"Live capture preflight remains blocked.",
			"cm-find-group-direct-mutation-post-boundary",
			"FindGroupService.addRecruitment/addApplication",
			IsLive: false);
	}

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookRow RunbookRow(
		int order,
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step) =>
		new(
			order,
			step,
			RequirementFor(step),
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStepStatus.BlockedMissingRuntimeComparison,
			HasRuntimeComparisonHandoff: true,
			HasExistingProvider: true,
			HasRuntimeEvidence: false,
			CanRunCommand: false,
			CanAcceptEvidence: false,
			"test provider",
			"test command",
			"test artifact root",
			$"test acceptance gate for {step}",
			"test current evidence",
			"test notes");

	private static FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement RequirementFor(
		FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep step) =>
		step switch
		{
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.JavaArtifactValidation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.JavaArtifactRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.CSharpGuardedBoundaryCapture => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.CSharpBoundaryRows,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.BoundaryExecutorObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.BoundaryExecutorObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RegistrySendObservation => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RegistrySendObservation,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RowIdentityAndValueProjection => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ValueProjection,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultMaterialization => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.Materialization,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.ResultEmission => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ResultEmission,
			FindGroupMutationPostProjectedRowComparisonValueReaderExecutorLiveCapturePreflightRunbookStep.RuntimeComparisonExecution => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.RuntimeComparison,
			_ => FindGroupMutationPostProjectedRowComparisonValueReaderExecutorRuntimeComparisonHandoffRequirement.ExecutableImplementation,
		};
}
