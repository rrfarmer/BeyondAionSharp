using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskAdapterServiceTests
{
	[Fact]
	public void Apply_DisabledExposesStartPlanWithoutVisualMutation()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned, result.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, result.Plan.Status);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.True(result.FanoutPlan.ShouldBroadcast);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientTraceStatus.Projected, result.SightedRecipientTrace.Status);
		Assert.Equal([PlayerObjectId], result.SightedRecipientTrace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned, result.Report.Status);
		Assert.Contains(result.Report.Rows, row => row.JavaOperation == "broadcastToSightedPlayers(player, packet, true)" && !row.IsLive);
		Assert.False(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.ExposesPlanForObservation);
		Assert.False(result.IsLive);
		Assert.False(player.IsProtectionActive());
	}

	[Fact]
	public void Apply_LiveStartSetsBlinkingButLeavesSchedulerAndPacketsPlanned()
	{
		var player = new Player { ObjectId = PlayerObjectId };

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted, result.Status);
		Assert.True(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.IsLive);
		Assert.True(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldScheduleTask);
		Assert.True(result.Plan.ShouldBroadcastPlayerState);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState, result.FanoutPlan.VisualMutationStep);
		Assert.Equal([PlayerObjectId], result.SightedRecipientTrace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Contains(result.Report.Rows, row => row.JavaOperation == "setVisualState(CreatureVisualState.BLINKING)" && row.IsLive);
		Assert.Contains(result.Report.Rows, row => row.JavaOperation == "schedule(this::stopProtectionActiveTask, 60000)" && !row.IsLive);
		Assert.Contains("setVisualState(BLINKING)", result.JavaSource);
	}

	[Fact]
	public void Apply_LiveStartAlreadyProtectedDoesNotMutate()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected, result.Status);
		Assert.False(result.MutatedVisualState);
		Assert.True(player.IsProtectionActive());
		Assert.False(result.Plan.ShouldScheduleTask);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedAlreadyProtectedStart, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.ShouldBroadcast);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientTraceStatus.NoBroadcast, result.SightedRecipientTrace.Status);
		Assert.Empty(result.SightedRecipientTrace.Recipients);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected, result.Report.Status);
		Assert.Equal(PlayerProtectionActiveTaskReportRowKind.SkippedBranch, result.Report.Rows[^1].Kind);
	}

	[Fact]
	public void Apply_LiveStopClearsBlinkingButLeavesSchedulerAndPacketsPlanned()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped, result.Status);
		Assert.True(result.MutatedVisualState);
		Assert.False(result.MutatedScheduler);
		Assert.False(result.SentPackets);
		Assert.True(result.IsLive);
		Assert.False(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldCancelTask);
		Assert.True(result.Plan.ShouldBroadcastPlayerState);
		Assert.True(result.Plan.ShouldNotifyAiOnMove);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.BroadcastPlanned, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState, result.FanoutPlan.VisualMutationStep);
		Assert.Equal([PlayerObjectId], result.SightedRecipientTrace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.Contains(result.Report.Rows, row => row.JavaOperation == "unsetVisualState(CreatureVisualState.BLINKING)" && row.IsLive);
		Assert.Contains(result.Report.Rows, row => row.JavaOperation == "notifyAIOnMove()" && !row.IsLive);
		Assert.Contains("unsetVisualState(BLINKING)", result.JavaSource);
	}

	[Fact]
	public void Apply_LiveStopUnspawnedDoesNotClearBlinking()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: false));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned, result.Status);
		Assert.False(result.MutatedVisualState);
		Assert.True(player.IsProtectionActive());
		Assert.True(result.Plan.ShouldCancelTask);
		Assert.False(result.Plan.ShouldBroadcastPlayerState);
		Assert.Equal(PlayerProtectionActiveTaskFanoutStatus.SkippedUnspawnedStop, result.FanoutPlan.Status);
		Assert.False(result.FanoutPlan.ShouldBroadcast);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientTraceStatus.NoBroadcast, result.SightedRecipientTrace.Status);
		Assert.Empty(result.SightedRecipientTrace.Recipients);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned, result.Report.Status);
		Assert.Equal(PlayerProtectionActiveTaskReportRowKind.SkippedBranch, result.Report.Rows[^1].Kind);
	}

	[Fact]
	public void Apply_DisabledStartProjectsSuppliedSightedRecipientsWithoutSendingPackets()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var membershipService = new PlayerKnownListMembershipService();
		var membership = membershipService.UpsertKnownPlayers(
			PlayerObjectId,
			[
				new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true),
				new PlayerKnownListMembershipCandidate(NotSightedPlayerObjectId, IsVisibleToOwner: true),
			]);

		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			SourceKnownListSnapshot: membership,
			RecipientVisibilityFacts:
			[
				new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true),
				new PlayerProtectionActiveTaskRecipientVisibilityFact(NotSightedPlayerObjectId, RecipientSeesSource: false),
			]));

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.DisabledPlanned, result.Status);
		Assert.False(result.SentPackets);
		Assert.False(result.SightedRecipientTrace.IsLive);
		Assert.Equal(
			[PlayerObjectId, SightedPlayerObjectId],
			result.SightedRecipientTrace.Recipients.Select(recipient => recipient.PlayerObjectId));
		Assert.True(result.SightedRecipientTrace.UsesRecipientKnownListSeesFilter);
	}

	private const int PlayerObjectId = 1001;
	private const int SightedPlayerObjectId = 1002;
	private const int NotSightedPlayerObjectId = 1003;
}
