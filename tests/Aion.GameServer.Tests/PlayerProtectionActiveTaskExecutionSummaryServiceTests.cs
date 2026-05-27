using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskExecutionSummaryServiceTests
{
	[Fact]
	public async Task Create_StartSummarizesJavaOrderWithAttackUtilAndTaskRows()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Start,
				ExecuteLiveVisualMutation: true),
			KnownObjectFacts:
			[
				new PlayerProtectionAttackUtilKnownObjectFact(
					CastingCreatureObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Creature,
					TargetObjectId: PlayerObjectId,
					IsCasting: true,
					CastingSkillFirstTargetObjectId: PlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(
					TargetingPlayerObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: PlayerObjectId,
					IsCasting: false,
					CastingSkillFirstTargetObjectId: null),
			],
			ExistingProtectionTaskPresent: true));

		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Start, summary.Action);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted, summary.AdapterStatus);
		Assert.True(summary.HasLiveVisualMutationOnly);
		Assert.True(summary.HasAttackUtilProjections);
		Assert.True(summary.HasTaskOperations);
		Assert.True(summary.HasPacketFanout);
		Assert.False(summary.SentPackets);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilCastCancellation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilTargetClear,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation,
			],
			summary.Rows.Select(row => row.Kind).ToArray());
		Assert.Single(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilCastCancellation
			&& row.RelatedObjectIds.SequenceEqual([CastingCreatureObjectId])
			&& row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive);
		Assert.Single(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.AttackUtilTargetClear
			&& row.RelatedObjectIds.SequenceEqual([TargetingPlayerObjectId])
			&& row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive);
		Assert.Contains(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation
			&& row.JavaOperation == "addTask(TaskId.PROTECTION_ACTIVE, scheduledFuture)"
			&& row.Notes.Contains("cancel the previous future", StringComparison.Ordinal));
		Assert.Contains(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout
			&& row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.DisabledNoSend);
		Assert.True(summary.Rows.Single(row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation).MutatedState);
	}

	[Fact]
	public async Task Create_AlreadyProtectedStartReportsOnlyObservedSkippedTaskBranch()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Start,
				ExecuteLiveVisualMutation: true),
			ExistingProtectionTaskPresent: true));

		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Start, summary.Action);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected, summary.AdapterStatus);
		Assert.False(summary.HasLiveVisualMutationOnly);
		Assert.False(summary.HasAttackUtilProjections);
		Assert.False(summary.HasPacketFanout);
		Assert.False(summary.SentPackets);
		Assert.Equal(
			[PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition],
			summary.Rows.Select(row => row.Kind).ToArray());
		Assert.DoesNotContain(summary.Rows, row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction);
		Assert.DoesNotContain(summary.Rows, row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation);
	}

	[Fact]
	public async Task Create_SpawnedStopSummarizesCancelBeforeVisualFanoutAndAi()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Stop,
				ExecuteLiveVisualMutation: true,
				HasProtectionActiveTask: true,
				IsSpawned: true),
			ExistingProtectionTaskPresent: true));

		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Stop, summary.Action);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped, summary.AdapterStatus);
		Assert.True(summary.HasAiMoveNotification);
		Assert.True(summary.HasTaskOperations);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.VisualMutation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketFanout,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.AiMoveNotification,
			],
			summary.Rows.Select(row => row.Kind).ToArray());
		Assert.True(summary.Rows.First().JavaOperation == "cancelTask(TaskId.PROTECTION_ACTIVE)");
		Assert.True(summary.Rows.First().Notes.Contains("remove and cancel", StringComparison.Ordinal));
		Assert.Contains(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.AiMoveNotification
			&& row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.PlannedNotLive
			&& !row.IsLive);
	}

	[Fact]
	public async Task Create_UnspawnedStopKeepsCancelAndSpawnedGuardOnly()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: false));

		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		Assert.Equal(PlayerProtectionActiveTaskAdapterAction.Stop, summary.Action);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned, summary.AdapterStatus);
		Assert.False(summary.HasPacketFanout);
		Assert.False(summary.HasAiMoveNotification);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation,
				PlayerProtectionActiveTaskExecutionSummaryRowKind.ObservedCondition,
			],
			summary.Rows.Select(row => row.Kind).ToArray());
		Assert.Contains(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.TaskOperation
			&& row.Notes.Contains("missing task removal returns null", StringComparison.Ordinal));
		Assert.DoesNotContain(summary.Rows, row => row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.PacketConstruction);
	}

	[Fact]
	public async Task Create_FlightPathStopReportsSkippedAiMoveNotification()
	{
		var player = new Player { ObjectId = PlayerObjectId, FlightPathType = PlayerFlightPathType.Windstream };
		player.SetVisualState(PlayerVisualStates.Blinking);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		var summary = PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);

		Assert.False(summary.HasAiMoveNotification);
		Assert.Contains(summary.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskExecutionSummaryRowKind.SkippedBranch
			&& row.Status == PlayerProtectionActiveTaskExecutionSummaryRowStatus.SkippedBranch
			&& row.JavaOperation == "notifyAIOnMove()");
	}

	private const int PlayerObjectId = 1001;
	private const int CastingCreatureObjectId = 1002;
	private const int TargetingPlayerObjectId = 1003;
}
