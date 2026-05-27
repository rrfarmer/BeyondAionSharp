using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskLiveReadinessServiceTests
{
	[Fact]
	public async Task Create_StartBlocksAdditionalLiveSideEffects()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Start,
			player =>
			{
			},
			knownObjectFacts:
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
			existingTask: true);

		var report = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);

		Assert.False(report.CanEnableAdditionalLiveSideEffects);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation, report.BlockedCapabilities);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear, report.BlockedCapabilities);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout, report.BlockedCapabilities);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap, report.BlockedCapabilities);
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.VisualMutation
			&& row.Status == PlayerProtectionActiveTaskLiveReadinessStatus.LiveOnlyAllowed);
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation
			&& row.BlockedReasons.Any(reason => reason.Contains("forEachObject", StringComparison.Ordinal)));
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear
			&& row.BlockedReasons.Any(reason => reason.Contains("setTarget(null)", StringComparison.Ordinal)));
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout
			&& row.BlockedReasons.Any(reason => reason.Contains("socket executor gate", StringComparison.Ordinal)));
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap
			&& row.BlockedReasons.Any(reason => reason.Contains("Future.cancel(false)", StringComparison.Ordinal)));
	}

	[Fact]
	public async Task Create_AlreadyProtectedStartHasNoAdditionalBlockedCapabilities()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Start,
			player => player.SetVisualState(PlayerVisualStates.Blinking));

		var report = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);

		Assert.True(report.CanEnableAdditionalLiveSideEffects);
		Assert.Empty(report.BlockedCapabilities);
		Assert.Single(report.Rows);
		Assert.Equal(PlayerProtectionActiveTaskLiveReadinessCapability.BranchObservation, report.Rows.Single().Capability);
		Assert.Equal(PlayerProtectionActiveTaskLiveReadinessStatus.Ready, report.Rows.Single().Status);
	}

	[Fact]
	public async Task Create_SpawnedStopBlocksSchedulerPacketAndAiMove()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Stop,
			player => player.SetVisualState(PlayerVisualStates.Blinking),
			existingTask: true);

		var report = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);

		Assert.False(report.CanEnableAdditionalLiveSideEffects);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap, report.BlockedCapabilities);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout, report.BlockedCapabilities);
		Assert.Contains(PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification, report.BlockedCapabilities);
		Assert.DoesNotContain(PlayerProtectionActiveTaskLiveReadinessCapability.CastCancellation, report.BlockedCapabilities);
		Assert.DoesNotContain(PlayerProtectionActiveTaskLiveReadinessCapability.TargetClear, report.BlockedCapabilities);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap,
				PlayerProtectionActiveTaskLiveReadinessCapability.BranchObservation,
				PlayerProtectionActiveTaskLiveReadinessCapability.VisualMutation,
				PlayerProtectionActiveTaskLiveReadinessCapability.PacketConstruction,
				PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout,
				PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification,
			],
			report.Rows.Select(row => row.Capability).ToArray());
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification
			&& row.BlockedReasons.Any(reason => reason.Contains("MovementNotifyTask.add", StringComparison.Ordinal)));
	}

	[Fact]
	public async Task Create_UnspawnedStopBlocksOnlyReachedTaskCancel()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Stop,
			player => player.SetVisualState(PlayerVisualStates.Blinking),
			isSpawned: false);

		var report = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);

		Assert.False(report.CanEnableAdditionalLiveSideEffects);
		Assert.Equal([PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap], report.BlockedCapabilities);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskLiveReadinessCapability.SchedulerTaskMap,
				PlayerProtectionActiveTaskLiveReadinessCapability.BranchObservation,
			],
			report.Rows.Select(row => row.Capability).ToArray());
		Assert.DoesNotContain(report.Rows, row => row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.PacketFanout);
		Assert.DoesNotContain(report.Rows, row => row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification);
	}

	[Fact]
	public async Task Create_FlightPathStopTreatsAiMoveAsSkippedNotBlocked()
	{
		var summary = await CreateSummaryAsync(
			PlayerProtectionActiveTaskAdapterAction.Stop,
			player =>
			{
				player.SetVisualState(PlayerVisualStates.Blinking);
				player.FlightPathType = PlayerFlightPathType.Windstream;
				player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
			});

		var report = PlayerProtectionActiveTaskLiveReadinessService.Create(summary);

		Assert.DoesNotContain(PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification, report.BlockedCapabilities);
		Assert.Contains(report.Rows, row =>
			row.Capability == PlayerProtectionActiveTaskLiveReadinessCapability.AiMoveNotification
			&& row.Status == PlayerProtectionActiveTaskLiveReadinessStatus.SkippedBranch
			&& row.BlockedReasons.Count == 0);
	}

	private static async Task<PlayerProtectionActiveTaskExecutionSummary> CreateSummaryAsync(
		PlayerProtectionActiveTaskAdapterAction action,
		Action<Player> configurePlayer,
		IReadOnlyList<PlayerProtectionAttackUtilKnownObjectFact>? knownObjectFacts = null,
		bool existingTask = false,
		bool isSpawned = true)
	{
		var player = new Player { ObjectId = PlayerObjectId };
		configurePlayer(player);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();
		var bridgeResult = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				action,
				ExecuteLiveVisualMutation: true,
				HasProtectionActiveTask: action == PlayerProtectionActiveTaskAdapterAction.Stop,
				IsSpawned: isSpawned),
			knownObjectFacts,
			ExistingProtectionTaskPresent: existingTask));

		return PlayerProtectionActiveTaskExecutionSummaryService.Create(bridgeResult);
	}

	private const int PlayerObjectId = 1001;
	private const int CastingCreatureObjectId = 1002;
	private const int TargetingPlayerObjectId = 1003;
}
