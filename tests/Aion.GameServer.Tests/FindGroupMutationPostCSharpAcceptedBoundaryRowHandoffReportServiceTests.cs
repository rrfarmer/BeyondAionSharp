using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksAndListsRequiredBoundaryFields()
	{
		var report = FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportService.Create();

		Assert.Equal(FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.BlockedMissingAcceptedBoundaryRows, report.Status);
		Assert.False(report.IsLive);
		Assert.Equal(0, report.AcceptedLiveRowCount);
		Assert.False(report.HasActionTwoAcceptedRow);
		Assert.False(report.HasActionSixAcceptedRow);
		Assert.False(report.CanFeedJavaArtifactPairing);
		Assert.False(report.CanRunCSharpCapture);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Contains("action", report.RequiredAcceptedBoundaryRowFields);
		Assert.Contains("boundaryAccepted", report.RequiredAcceptedBoundaryRowFields);
		Assert.Contains("executorInvokedFromBoundary", report.RequiredAcceptedBoundaryRowFields);
		Assert.Contains("registrySendsObservedInOrder", report.RequiredAcceptedBoundaryRowFields);
		Assert.Contains("visibleEntryObjectIdsAfterMutation", report.RequiredAcceptedBoundaryRowFields);
		Assert.Equal(Enum.GetValues<FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate>(), report.Rows.Select(row => row.Gate));
		Assert.All(report.Rows, row =>
		{
			Assert.False(row.Satisfied);
			Assert.True(row.BlocksJavaArtifactPairing);
		});
		Assert.Contains("blocked until action 2 and action 6 rows", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
		Assert.Contains("addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_AcceptedRowsCanFeedPairingButStillCannotRunComparisonOrClaimParity()
	{
		var guardedResult = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows:
			[
				LiveRow(2),
				LiveRow(6),
			]);
		var preflight = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guardedResult);

		var report = FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportService.Create(preflight);

		Assert.Equal(FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked, report.Status);
		Assert.Equal(2, report.AcceptedLiveRowCount);
		Assert.True(report.HasActionTwoAcceptedRow);
		Assert.True(report.HasActionSixAcceptedRow);
		Assert.True(report.CanFeedJavaArtifactPairing);
		Assert.False(report.CanRunCSharpCapture);
		Assert.False(report.CanRunRuntimeComparison);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.All(report.Rows, row =>
		{
			Assert.True(row.Satisfied);
			Assert.False(row.BlocksJavaArtifactPairing);
		});
		Assert.Contains(report.Rows, row =>
			row.Gate == FindGroupMutationPostCSharpLiveBoundaryRowIntakeGate.PostedSystemMessageBeforeRefreshedList
			&& row.RequiredEvidence.Contains("postedSystemMessageId=1400392", StringComparison.Ordinal)
			&& row.RequiredEvidence.Contains("refreshedFindGroupAction=4", StringComparison.Ordinal));
		Assert.Contains("does not execute capture", report.ExecutionDecision, StringComparison.Ordinal);
		Assert.Contains("runtime comparison", report.ExecutionDecision, StringComparison.Ordinal);
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
