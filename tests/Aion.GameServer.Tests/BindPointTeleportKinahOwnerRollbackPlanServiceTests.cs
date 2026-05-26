using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahOwnerRollbackPlanServiceTests
{
	[Fact]
	public void CreatePlan_NotEnoughKinahDoesNotApplyOrRollbackMutation()
	{
		var player = CreatePlayer(currentKinah: 500);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(player, mutationPlan);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.StoppedNotEnoughKinah, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldApplyInMemoryMutation);
		Assert.False(plan.ShouldRollbackInMemoryMutation);
		Assert.False(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(500, plan.OriginalKinahItem?.Count);
		Assert.Null(plan.UpdatedKinahItem);
	}

	[Fact]
	public void CreatePlan_NonPositivePriceContinuesWithoutMutationOrRollback()
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 0);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(player, mutationPlan);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.ContinueWithoutMutation, plan.Status);
		Assert.False(plan.ShouldApplyInMemoryMutation);
		Assert.False(plan.ShouldRollbackInMemoryMutation);
		Assert.True(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, plan.OriginalKinahItem?.Count);
		Assert.Null(plan.UpdatedKinahItem);
	}

	[Fact]
	public void CreatePlan_MutationAwaitingResultsRecordsRollbackRequirement()
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(player, mutationPlan);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.AwaitingPersistenceOrSendResult, plan.Status);
		Assert.True(plan.ShouldApplyInMemoryMutation);
		Assert.True(plan.ShouldRollbackInMemoryMutation);
		Assert.False(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, plan.OriginalKinahItem?.Count);
		Assert.Equal(1_000, plan.UpdatedKinahItem?.Count);
		Assert.Equal(1_000, plan.InventoryAfterMutation.Single(item => item.ObjectId == 1824).Count);
		Assert.Equal(2_000, plan.InventoryAfterRollback.Single(item => item.ObjectId == 1824).Count);
	}

	[Theory]
	[InlineData(BindPointTeleportKinahPersistenceStatus.MissingRow)]
	[InlineData(BindPointTeleportKinahPersistenceStatus.Failed)]
	public void CreatePlan_PersistenceFailureRequiresRollback(BindPointTeleportKinahPersistenceStatus status)
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_000);
		var persistenceDecision = CreatePersistenceDecision(mutationPlan, status);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(
			player,
			mutationPlan,
			persistenceDecision,
			sendDecision: null);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.AwaitingPersistenceOrSendResult, plan.Status);
		Assert.True(plan.ShouldRollbackInMemoryMutation);
		Assert.False(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, plan.InventoryAfterRollback.Single(item => item.ObjectId == 1824).Count);
	}

	[Fact]
	public void CreatePlan_SendFailureRequiresRollback()
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_000);
		var persistenceDecision = CreatePersistenceDecision(mutationPlan, BindPointTeleportKinahPersistenceStatus.Saved);
		var sendDecision = CreateSendDecision(persistenceDecision, sendStatus: BindPointTeleportKinahInventorySendStatus.Failed);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(
			player,
			mutationPlan,
			persistenceDecision,
			sendDecision);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.RollbackRequired, plan.Status);
		Assert.True(plan.ShouldApplyInMemoryMutation);
		Assert.True(plan.ShouldRollbackInMemoryMutation);
		Assert.False(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, plan.InventoryAfterRollback.Single(item => item.ObjectId == 1824).Count);
		Assert.Contains(BindPointTeleportKinahOwnerRollbackPlanStep.RollbackToOriginalKinah, plan.Steps);
	}

	[Fact]
	public void CreatePlan_SavedAndSentCommitsUpdatedKinahAndContinues()
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_000);
		var persistenceDecision = CreatePersistenceDecision(mutationPlan, BindPointTeleportKinahPersistenceStatus.Saved);
		var sendDecision = CreateSendDecision(persistenceDecision, sendStatus: BindPointTeleportKinahInventorySendStatus.Sent);

		var plan = BindPointTeleportKinahOwnerRollbackPlanService.CreatePlan(
			player,
			mutationPlan,
			persistenceDecision,
			sendDecision);

		Assert.Equal(BindPointTeleportKinahOwnerRollbackPlanStatus.CommitReady, plan.Status);
		Assert.True(plan.ShouldApplyInMemoryMutation);
		Assert.False(plan.ShouldRollbackInMemoryMutation);
		Assert.True(plan.ShouldContinueToCooldownFanout);
		Assert.Equal(2_000, plan.OriginalKinahItem?.Count);
		Assert.Equal(1_000, plan.UpdatedKinahItem?.Count);
		Assert.Equal(1_000, plan.InventoryAfterMutation.Single(item => item.ObjectId == 1824).Count);
		Assert.Contains(BindPointTeleportKinahOwnerRollbackPlanStep.CommitUpdatedKinah, plan.Steps);
	}

	private static Player CreatePlayer(long currentKinah)
	{
		return new Player
		{
			ObjectId = 7001,
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					OwnerId = 7001,
					ItemId = 100100001,
					Count = 1,
					Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
				},
				new InventoryItem
				{
					ObjectId = 1824,
					OwnerId = 7001,
					ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
					Count = currentKinah,
					Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
				},
			],
		};
	}

	private static BindPointTeleportKinahPersistenceDecision CreatePersistenceDecision(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		BindPointTeleportKinahPersistenceStatus status)
	{
		var callbackPlan = CreateCallbackPlan(mutationPlan);
		var persistenceResult = mutationPlan.KinahItemUpdate == null
			? null
			: new BindPointTeleportKinahPersistenceResult(
				status,
				mutationPlan.KinahItemUpdate.OwnerId,
				mutationPlan.KinahItemUpdate.ObjectId,
				mutationPlan.KinahItemUpdate.Count,
				ShouldRollbackInMemoryMutation: status != BindPointTeleportKinahPersistenceStatus.Saved,
				"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
				IsLive: false);
		return BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(callbackPlan, persistenceResult);
	}

	private static BindPointTeleportKinahInventorySendDecision CreateSendDecision(
		BindPointTeleportKinahPersistenceDecision persistenceDecision,
		BindPointTeleportKinahInventorySendStatus sendStatus)
	{
		var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			persistenceDecision,
			CreateKinahTemplate());
		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			persistenceDecision,
			packetPlan,
			CreateRuntimeResult(persistenceDecision.CallbackPlan));
		return BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			new BindPointTeleportKinahInventorySendResult(
				sendStatus,
				PlayerObjectId: 7001,
				SentPacket: sendStatus == BindPointTeleportKinahInventorySendStatus.Sent,
				"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM)",
				IsLive: false));
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan)
	{
		var playerObjectId = 7001;
		var locId = 6001;
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			mutationPlan.RequiredPrice,
			mutationPlan.CurrentKinah);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId,
			locId,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			playerObjectId,
			SmBindPointTeleport.Cooldown(playerObjectId, locId, cooldownSeconds: 600));
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
			playerIsDead: false,
			playerIsAboutToDie: false);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			kinahMutationPlan: mutationPlan);
	}

	private static BindPointTeleportRuntimeCallbackExecutionResult CreateRuntimeResult(
		BindPointTeleportScheduledCallbackPlan callbackPlan)
	{
		return new BindPointTeleportRuntimeCallbackExecutionResult(
			BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast,
			callbackPlan,
			new BindPointTeleportCooldownFact(7001, 6001, CooldownEndMillis: 601_000),
			new BindPointTeleportRuntimeFanoutResult(
				BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf,
				callbackPlan.CooldownFanoutPlan,
				SentCount: 1,
				SentPacket: true,
				"PacketSendUtility.broadcastPacket(player, action 3, true)",
				IsLive: false),
			callbackPlan.KinahItemUpdate,
			callbackPlan.KinahInventoryUpdateType,
			ShouldSendNotEnoughFee: false,
			callbackPlan.ShouldEmitKinahInventoryUpdatePacket,
			StoredCooldownFact: true,
			BroadcastCooldown: true,
			ShouldScheduleFinalTeleport: true,
			ShouldTeleport: true,
			"BindPointTeleportService.teleport scheduled task -> addCooldown -> broadcast action 3 -> schedule final teleport",
			IsLive: false);
	}

	private static ItemTemplateSummary CreateKinahTemplate()
	{
		return new ItemTemplateSummary(
			BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
			"Kinah",
			0,
			0,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0);
	}
}
