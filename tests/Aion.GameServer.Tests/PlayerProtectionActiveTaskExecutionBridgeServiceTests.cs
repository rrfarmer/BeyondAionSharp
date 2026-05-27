using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskExecutionBridgeServiceTests
{
	[Fact]
	public async Task ExecuteAsync_LiveStartBuildsPlayerStatePacketAndDisabledExecutorDoesNotSend()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var registry = new RecordingConnectionRegistry();
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService(registry);

		var result = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true,
			SourceKnownListSnapshot: CreateKnownListSnapshot(),
			RecipientVisibilityFacts:
			[
				new PlayerProtectionActiveTaskRecipientVisibilityFact(SightedPlayerObjectId, RecipientSeesSource: true),
			]));

		Assert.Equal(PlayerProtectionActiveTaskExecutionBridgeStatus.LiveVisualMutationNoSend, result.Status);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted, result.AdapterResult.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StartProtection, result.SideEffectOperationPlan.PlanStatus);
		Assert.True(result.SideEffectOperationPlan.SchedulesDelayedStop);
		Assert.True(result.SideEffectOperationPlan.CancelsKnownCreatureCasts);
		Assert.True(result.SideEffectOperationPlan.ClearsKnownPlayerTargets);
		Assert.Contains(result.SideEffectOperationPlan.Rows, row => row.Operation == PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState);
		Assert.Empty(result.AttackUtilRecipientPlan.CastCancellationObjectIds);
		Assert.Empty(result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds);
		Assert.True(result.TaskOperationPlan.SchedulesDelayedStop);
		Assert.True(result.TaskOperationPlan.StoresTask);
		Assert.False(result.TaskOperationPlan.ReplacesExistingTask);
		Assert.False(result.TaskOperationPlan.IsLive);
		Assert.True(player.IsProtectionActive());
		Assert.True(result.ConstructedPacket);
		Assert.IsType<SmPlayerState>(result.Packet);
		Assert.False(result.SentPackets);
		Assert.True(result.UsesDisabledSocketExecutorByDefault);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend, result.SocketExecutorResult.Status);
		Assert.Equal(
			[PlayerObjectId, SightedPlayerObjectId],
			result.SocketExecutorResult.Recipients.Select(recipient => recipient.Recipient.PlayerObjectId));
		Assert.All(result.SocketExecutorResult.Recipients, recipient =>
			Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketRecipientStatus.NotAttemptedDisabled, recipient.Status));
		Assert.Empty(registry.SendAttempts);
	}

	[Fact]
	public async Task ExecuteAsync_LiveSpawnedStopBuildsPlayerStatePacketAfterClearingBlinking()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var result = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		Assert.Equal(PlayerProtectionActiveTaskExecutionBridgeStatus.LiveVisualMutationNoSend, result.Status);
		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped, result.AdapterResult.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtection, result.SideEffectOperationPlan.PlanStatus);
		Assert.True(result.SideEffectOperationPlan.CancelsExistingStopTask);
		Assert.True(result.SideEffectOperationPlan.NotifiesAiOnMove);
		Assert.Empty(result.AttackUtilRecipientPlan.CastCancellationObjectIds);
		Assert.Empty(result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds);
		Assert.True(result.TaskOperationPlan.RemovesMissingTaskAsNoOp);
		Assert.False(result.TaskOperationPlan.CancelsExistingTask);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask,
				PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned,
				PlayerProtectionActiveTaskSideEffectOperation.UnsetBlinkingVisualState,
				PlayerProtectionActiveTaskSideEffectOperation.BroadcastPlayerState,
				PlayerProtectionActiveTaskSideEffectOperation.NotifyAiOnMove,
			],
			result.SideEffectOperationPlan.Rows.Select(row => row.Operation));
		Assert.False(player.IsProtectionActive());
		Assert.True(result.ConstructedPacket);
		Assert.IsType<SmPlayerState>(result.Packet);
		Assert.False(result.SentPackets);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.DisabledNoSend, result.SocketExecutorResult.Status);
		Assert.Equal([PlayerObjectId], result.SocketExecutorResult.Recipients.Select(recipient => recipient.Recipient.PlayerObjectId));
	}

	[Fact]
	public async Task ExecuteAsync_SkippedBranchesDoNotConstructPacketOrSend()
	{
		var alreadyProtectedPlayer = new Player { ObjectId = PlayerObjectId };
		alreadyProtectedPlayer.SetVisualState(PlayerVisualStates.Blinking);
		var unspawnedPlayer = new Player { ObjectId = PlayerObjectId };
		unspawnedPlayer.SetVisualState(PlayerVisualStates.Blinking);
		var registry = new RecordingConnectionRegistry();
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService(registry);

		var alreadyProtected = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			alreadyProtectedPlayer,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));
		var unspawnedStop = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskAdapterRequest(
			unspawnedPlayer,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: false));

		Assert.Equal(PlayerProtectionActiveTaskExecutionBridgeStatus.NoBroadcast, alreadyProtected.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.AlreadyProtected, alreadyProtected.SideEffectOperationPlan.PlanStatus);
		Assert.Single(alreadyProtected.SideEffectOperationPlan.Rows);
		Assert.Empty(alreadyProtected.AttackUtilRecipientPlan.CastCancellationObjectIds);
		Assert.Empty(alreadyProtected.AttackUtilRecipientPlan.TargetClearPlayerObjectIds);
		Assert.False(alreadyProtected.TaskOperationPlan.SchedulesDelayedStop);
		Assert.False(alreadyProtected.TaskOperationPlan.StoresTask);
		Assert.Equal(PlayerProtectionActiveTaskTaskOperation.NoTaskOperation, alreadyProtected.TaskOperationPlan.Rows.Single().Operation);
		Assert.Null(alreadyProtected.Packet);
		Assert.False(alreadyProtected.ConstructedPacket);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.NoPacket, alreadyProtected.SocketExecutorResult.Status);
		Assert.Empty(alreadyProtected.SocketExecutorResult.Recipients);

		Assert.Equal(PlayerProtectionActiveTaskExecutionBridgeStatus.NoBroadcast, unspawnedStop.Status);
		Assert.Equal(PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned, unspawnedStop.SideEffectOperationPlan.PlanStatus);
		Assert.Empty(unspawnedStop.AttackUtilRecipientPlan.CastCancellationObjectIds);
		Assert.Empty(unspawnedStop.AttackUtilRecipientPlan.TargetClearPlayerObjectIds);
		Assert.True(unspawnedStop.TaskOperationPlan.RemovesMissingTaskAsNoOp);
		Assert.False(unspawnedStop.TaskOperationPlan.CancelsExistingTask);
		Assert.Equal(
			[
				PlayerProtectionActiveTaskSideEffectOperation.CancelProtectionTask,
				PlayerProtectionActiveTaskSideEffectOperation.CheckSpawned,
			],
			unspawnedStop.SideEffectOperationPlan.Rows.Select(row => row.Operation));
		Assert.Null(unspawnedStop.Packet);
		Assert.False(unspawnedStop.ConstructedPacket);
		Assert.Equal(PlayerProtectionActiveTaskSightedRecipientSocketExecutorStatus.NoPacket, unspawnedStop.SocketExecutorResult.Status);
		Assert.Empty(unspawnedStop.SocketExecutorResult.Recipients);
		Assert.Empty(registry.SendAttempts);
	}

	[Fact]
	public async Task ExecuteAsync_ComposesTaskOperationPlanWithExistingTaskFact()
	{
		var startPlayer = new Player { ObjectId = PlayerObjectId };
		var stopPlayer = new Player { ObjectId = PlayerObjectId };
		stopPlayer.SetVisualState(PlayerVisualStates.Blinking);
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService();

		var start = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				startPlayer,
				PlayerProtectionActiveTaskAdapterAction.Start,
				ExecuteLiveVisualMutation: true),
			ExistingProtectionTaskPresent: true));
		var stop = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				stopPlayer,
				PlayerProtectionActiveTaskAdapterAction.Stop,
				ExecuteLiveVisualMutation: true,
				HasProtectionActiveTask: true,
				IsSpawned: true),
			ExistingProtectionTaskPresent: true));

		Assert.True(start.TaskOperationPlan.ReplacesExistingTask);
		Assert.True(start.TaskOperationPlan.StoresTask);
		Assert.False(start.TaskOperationPlan.CancelsExistingTask);
		Assert.Contains(start.TaskOperationPlan.Rows, row =>
			row.Operation == PlayerProtectionActiveTaskTaskOperation.AddTaskAndMaybeReplaceExisting
			&& row.Status == PlayerProtectionActiveTaskTaskOperationStatus.WouldReplaceExistingTask
			&& row.WouldCancelExistingTask);

		Assert.True(stop.TaskOperationPlan.CancelsExistingTask);
		Assert.False(stop.TaskOperationPlan.RemovesMissingTaskAsNoOp);
		Assert.Single(stop.TaskOperationPlan.Rows, row =>
			row.Operation == PlayerProtectionActiveTaskTaskOperation.CancelTask
			&& row.Status == PlayerProtectionActiveTaskTaskOperationStatus.WouldCancelExistingTask
			&& row.WouldCancelExistingTask);
		Assert.False(start.TaskOperationPlan.IsLive);
		Assert.False(stop.TaskOperationPlan.IsLive);
	}

	[Fact]
	public async Task ExecuteAsync_ComposesAttackUtilRecipientPlanFromKnownObjectFacts()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var registry = new RecordingConnectionRegistry();
		var bridge = new PlayerProtectionActiveTaskExecutionBridgeService(registry);

		var result = await bridge.ExecuteAsync(new PlayerProtectionActiveTaskExecutionBridgeRequest(
			new PlayerProtectionActiveTaskAdapterRequest(
				player,
				PlayerProtectionActiveTaskAdapterAction.Start,
				ExecuteLiveVisualMutation: true),
			KnownObjectFacts:
			[
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: CastingCreatureObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Creature,
					TargetObjectId: PlayerObjectId,
					IsCasting: true,
					CastingSkillFirstTargetObjectId: PlayerObjectId),
				new PlayerProtectionAttackUtilKnownObjectFact(
					KnownObjectId: TargetingPlayerObjectId,
					PlayerProtectionAttackUtilKnownObjectKind.Player,
					TargetObjectId: PlayerObjectId,
					IsCasting: false,
					CastingSkillFirstTargetObjectId: null,
					CanSeeProtectedTarget: true),
			]));

		Assert.Equal(PlayerProtectionActiveTaskExecutionBridgeStatus.LiveVisualMutationNoSend, result.Status);
		Assert.Equal([CastingCreatureObjectId], result.AttackUtilRecipientPlan.CastCancellationObjectIds);
		Assert.Equal([TargetingPlayerObjectId], result.AttackUtilRecipientPlan.TargetClearPlayerObjectIds);
		Assert.False(result.AttackUtilRecipientPlan.ValidateSeeForTargetRemoval);
		Assert.All(result.AttackUtilRecipientPlan.CastCancellationProjections.Where(projection => projection.WouldCancelCast), projection =>
			Assert.False(projection.IsLive));
		Assert.All(result.AttackUtilRecipientPlan.TargetClearProjections.Where(projection => projection.WouldClearTarget), projection =>
			Assert.False(projection.IsLive));
		Assert.Empty(registry.SendAttempts);
	}

	private static PlayerKnownListMembershipSnapshot CreateKnownListSnapshot()
	{
		var membershipService = new PlayerKnownListMembershipService();
		return membershipService.UpsertKnownPlayers(
			PlayerObjectId,
			[new PlayerKnownListMembershipCandidate(SightedPlayerObjectId, IsVisibleToOwner: true)]);
	}

	private const int PlayerObjectId = 1001;
	private const int SightedPlayerObjectId = 1002;
	private const int CastingCreatureObjectId = 1003;
	private const int TargetingPlayerObjectId = 1004;

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<(int PlayerObjectId, GameServerPacket Packet)> SendAttempts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SendAttempts.Add((playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates) =>
			Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail) =>
			Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah) =>
			Task.FromResult(false);
	}
}
