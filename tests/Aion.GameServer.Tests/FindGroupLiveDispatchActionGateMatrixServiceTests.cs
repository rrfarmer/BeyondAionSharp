using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupLiveDispatchActionGateMatrixServiceTests
{
	[Fact]
	public void CreateMatrix_CoversEveryParsedJavaActionAndExcludesServerPacketOnlyCodes()
	{
		var matrix = FindGroupLiveDispatchActionGateMatrixService.CreateMatrix();

		Assert.Equal(
			[0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 17, 20, 25],
			matrix.Entries.Select(entry => entry.Action));
		Assert.DoesNotContain(matrix.Entries, entry => entry.Action is 14 or 16);
		Assert.Contains("CM_FIND_GROUP.java", matrix.JavaClientPacketSource, StringComparison.Ordinal);
		Assert.Contains("FindGroupService.java", matrix.JavaServiceSource, StringComparison.Ordinal);
		Assert.Contains("server-packet action codes", matrix.BoundaryNote, StringComparison.Ordinal);
		Assert.False(matrix.IsReadyForLiveDispatch);
	}

	[Fact]
	public void CreateMatrix_MapsExecutableBranchesToRemainingLiveEvidenceGates()
	{
		var matrix = FindGroupLiveDispatchActionGateMatrixService.CreateMatrix();

		var worldBroadcastActions = ActionsForGate(matrix, FindGroupLiveDispatchGoNoGoChecklistItemKind.WorldBroadcastDispatch);
		Assert.Equal([1, 5], worldBroadcastActions);

		var singletonOnlyActions = matrix.Entries
			.Where(entry => entry.MissingLiveGates.SequenceEqual([FindGroupLiveDispatchGoNoGoChecklistItemKind.SharedSingletonLifecycle]))
			.Select(entry => entry.Action);
		Assert.Equal([3, 7], singletonOnlyActions);

		var directActions = ActionsForGate(matrix, FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch);
		Assert.Equal([0, 2, 4, 6, 8, 9, 10, 11, 12, 13, 15, 17], directActions);

		Assert.All(
			matrix.Entries.Where(entry => entry.RuntimeShape == FindGroupLiveDispatchActionRuntimeShape.ExecutableRunImplBranch),
			entry =>
			{
				Assert.Equal(FindGroupLiveDispatchActionGateStatus.BlockedPendingLiveEvidence, entry.Status);
				Assert.Contains("ProcessPacketAsync dispatch remains deferred", entry.Evidence, StringComparison.Ordinal);
			});
	}

	[Fact]
	public void CreateMatrix_RecordsActionTwelveAndParsedOnlyActionsAsSpecialCases()
	{
		var matrix = FindGroupLiveDispatchActionGateMatrixService.CreateMatrix();

		var actionTwelve = Assert.Single(matrix.Entries, entry => entry.Action == 12);
		Assert.Equal("sendInstanceApplicationResult", actionTwelve.JavaRunImplTarget);
		Assert.Equal(
			[
				FindGroupLiveDispatchGoNoGoChecklistItemKind.ActionTwelveInviteDispatch,
				FindGroupLiveDispatchGoNoGoChecklistItemKind.DirectPacketDispatch,
			],
			actionTwelve.MissingLiveGates);
		Assert.Equal(FindGroupLiveDispatchActionGateStatus.BlockedPendingLiveEvidence, actionTwelve.Status);

		var parsedOnly = matrix.Entries
			.Where(entry => entry.RuntimeShape == FindGroupLiveDispatchActionRuntimeShape.ParsedOnlyNoRunBranch)
			.ToArray();
		Assert.Equal([20, 25], parsedOnly.Select(entry => entry.Action));
		Assert.All(
			parsedOnly,
			entry =>
			{
				Assert.Equal(FindGroupLiveDispatchActionGateStatus.ReadyParsedOnlyNoOp, entry.Status);
				Assert.Equal([FindGroupLiveDispatchGoNoGoChecklistItemKind.ParsedOnlyNoRunActions], entry.MissingLiveGates);
				Assert.Contains("runImpl has no branch", entry.Evidence, StringComparison.Ordinal);
			});
	}

	private static int[] ActionsForGate(
		FindGroupLiveDispatchActionGateMatrix matrix,
		FindGroupLiveDispatchGoNoGoChecklistItemKind gate)
	{
		return matrix.Entries
			.Where(entry => entry.MissingLiveGates.Contains(gate))
			.Select(entry => entry.Action)
			.ToArray();
	}
}
