using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightServiceTests
{
	[Fact]
	public void Create_DefaultPreflightBlocksUntilAcceptedBoundaryRowsExist()
	{
		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create();

		Assert.Equal(FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.BlockedMissingAcceptedBoundaryRows, preflight.Status);
		Assert.False(preflight.IsLive);
		Assert.Equal(0, preflight.AcceptedLiveRowCount);
		Assert.False(preflight.HasActionTwoAcceptedRow);
		Assert.False(preflight.HasActionSixAcceptedRow);
		Assert.False(preflight.CanFeedRuntimeComparison);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", preflight.TraceName);
		Assert.Contains("addRecruitment/addApplication", preflight.JavaSource, StringComparison.Ordinal);
		Assert.Equal(Enum.GetValues<FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate>(), preflight.Rows.Select(row => row.Gate));
		Assert.All(preflight.Rows, row =>
		{
			Assert.False(row.Satisfied);
			Assert.True(row.BlocksRuntimeComparison);
		});
		Assert.Contains("accepted action 2 and action 6 rows", preflight.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_DisabledShapeRowsDoNotSatisfyLiveBoundaryIntake()
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(2),
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
			]);

		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);

		Assert.Equal(FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.BlockedMissingAcceptedBoundaryRows, preflight.Status);
		Assert.Equal(0, preflight.AcceptedLiveRowCount);
		Assert.False(preflight.HasBoundaryAcceptance);
		Assert.False(preflight.HasExecutorObservation);
		Assert.False(preflight.HasRegistryObservation);
		Assert.Contains(preflight.Rows, row =>
			row.Gate == FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.BoundaryAccepted
			&& !row.Satisfied
			&& row.Notes.Contains("Disabled plan rows are shape inputs only", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_AcceptedActionTwoAndSixRowsSatisfyIntakeButNotParity()
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveRow(2),
				LiveRow(6),
			]);

		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);

		Assert.Equal(FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked, preflight.Status);
		Assert.Equal(2, preflight.AcceptedLiveRowCount);
		Assert.True(preflight.HasActionTwoAcceptedRow);
		Assert.True(preflight.HasActionSixAcceptedRow);
		Assert.True(preflight.HasBoundaryAcceptance);
		Assert.True(preflight.HasExecutorObservation);
		Assert.True(preflight.HasRegistryObservation);
		Assert.True(preflight.HasPostedBeforeRefreshedOrdering);
		Assert.True(preflight.HasZeroWorldBroadcasts);
		Assert.True(preflight.HasZeroInviteDispatches);
		Assert.True(preflight.HasJavaArtifactPairingIdentity);
		Assert.True(preflight.CanFeedRuntimeComparison);
		Assert.False(preflight.CanClaimVerifiedParity);
		Assert.All(preflight.Rows, row =>
		{
			Assert.True(row.Satisfied);
			Assert.False(row.BlocksRuntimeComparison);
		});
		Assert.Contains("value projection", preflight.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("verified parity remain blocked", preflight.ExecutionDecision, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_SingleAcceptedActionStillBlocksJavaArtifactPairing()
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveRow(2),
			]);

		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);

		Assert.Equal(FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.BlockedMissingAcceptedBoundaryRows, preflight.Status);
		Assert.Equal(1, preflight.AcceptedLiveRowCount);
		Assert.True(preflight.HasActionTwoAcceptedRow);
		Assert.False(preflight.HasActionSixAcceptedRow);
		Assert.False(preflight.CanFeedRuntimeComparison);
		Assert.Contains(preflight.Rows, row =>
			row.Gate == FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.ActionSixAcceptedBoundaryRow
			&& row.Action == 6
			&& !row.Satisfied);
		Assert.Contains(preflight.Rows, row =>
			row.Gate == FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.JavaArtifactPairingIdentity
			&& !row.Satisfied
			&& row.CurrentEvidence.Contains("action6=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_RejectedPacketShapeCannotSatisfyPostedRefreshedOrdering()
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveRow(2) with { PostedSystemMessageId = 1400393 },
				LiveRow(6),
			]);

		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);

		Assert.Equal(FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightStatus.BlockedMissingAcceptedBoundaryRows, preflight.Status);
		Assert.False(preflight.HasActionTwoAcceptedRow);
		Assert.True(preflight.HasActionSixAcceptedRow);
		Assert.False(preflight.HasPostedBeforeRefreshedOrdering);
		Assert.Contains(preflight.Rows, row =>
			row.Gate == FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.PostedSystemMessageBeforeRefreshedList
			&& !row.Satisfied
			&& row.RequiredEvidence.Contains("SmSystemMessage 1400392", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("SmFindGroup action 4", StringComparison.Ordinal));
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport LiveRow(int action) =>
		FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ActivePlayerObjectId = action == 2 ? 1001 : 1002,
			ActivePlayerRace = "ELYOS",
			ServerEpochSeconds = 1700000000,
			MutatedEntryObjectId = action == 2 ? 2001 : 2002,
			StateMutationRecordedBeforeDirectPackets = true,
			PostedSystemMessageRecipientObjectId = action == 2 ? 1001 : 1002,
			RefreshedListRecipientObjectId = action == 2 ? 1001 : 1002,
			VisibleEntryObjectIdsAfterMutation = [action == 2 ? 2001 : 2002],
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
			WorldBroadcastCount = 0,
			InviteDispatchCount = 0,
		};
}
