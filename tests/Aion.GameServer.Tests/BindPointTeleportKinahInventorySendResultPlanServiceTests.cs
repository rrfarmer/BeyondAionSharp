using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahInventorySendResultPlanServiceTests
{
	[Fact]
	public void CreateDecision_CompositionWithoutPacketIntentStopsBeforeSend()
	{
		var composition = CreateComposition(
			persistenceStatus: BindPointTeleportKinahPersistenceStatus.MissingRow,
			includePacketTemplate: true,
			includeRuntimeResult: true);

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			sendResult: null);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.StoppedBeforePacketIntent, decision.Status);
		Assert.False(decision.IsLive);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldStoreCooldown);
		Assert.False(decision.ShouldBroadcastCooldown);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
		Assert.Null(decision.SendResult);
	}

	[Fact]
	public void CreateDecision_MissingConnectionStopsBeforeCooldownFanoutAndMovement()
	{
		var composition = CreateComposition(
			persistenceStatus: BindPointTeleportKinahPersistenceStatus.Saved,
			includePacketTemplate: true,
			includeRuntimeResult: true);
		var sendResult = CreateSendResult(BindPointTeleportKinahInventorySendStatus.MissingConnection);

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			sendResult);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.StoppedMissingConnection, decision.Status);
		Assert.Same(sendResult, decision.SendResult);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldStoreCooldown);
		Assert.False(decision.ShouldBroadcastCooldown);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
	}

	[Theory]
	[InlineData(BindPointTeleportKinahInventorySendStatus.Failed, false)]
	[InlineData(BindPointTeleportKinahInventorySendStatus.Sent, false)]
	public void CreateDecision_FailedSendStopsBeforeCooldownFanoutAndMovement(
		BindPointTeleportKinahInventorySendStatus status,
		bool sentPacket)
	{
		var composition = CreateComposition(
			persistenceStatus: BindPointTeleportKinahPersistenceStatus.Saved,
			includePacketTemplate: true,
			includeRuntimeResult: true);
		var sendResult = CreateSendResult(status, sentPacket);

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			sendResult);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.StoppedSendFailed, decision.Status);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldStoreCooldown);
		Assert.False(decision.ShouldBroadcastCooldown);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
	}

	[Fact]
	public void CreateDecision_SentPacketAllowsCooldownFanoutAndMovementMetadata()
	{
		var composition = CreateComposition(
			persistenceStatus: BindPointTeleportKinahPersistenceStatus.Saved,
			includePacketTemplate: true,
			includeRuntimeResult: true);
		var sendResult = CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent);

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			sendResult);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout, decision.Status);
		Assert.False(decision.IsLive);
		Assert.True(decision.ShouldContinueToCooldownFanout);
		Assert.True(decision.ShouldStoreCooldown);
		Assert.True(decision.ShouldBroadcastCooldown);
		Assert.True(decision.ShouldScheduleFinalTeleport);
		Assert.True(decision.ShouldTeleport);
	}

	[Fact]
	public void CreateDecision_SentPacketKeepsBlockedMovementBlocked()
	{
		var composition = CreateComposition(
			persistenceStatus: BindPointTeleportKinahPersistenceStatus.Saved,
			includePacketTemplate: true,
			includeRuntimeResult: true,
			shouldTeleport: false);
		var sendResult = CreateSendResult(BindPointTeleportKinahInventorySendStatus.Sent);

		var decision = BindPointTeleportKinahInventorySendResultPlanService.CreateDecision(
			composition,
			sendResult);

		Assert.Equal(BindPointTeleportKinahInventorySendDecisionStatus.ReadyForCooldownFanout, decision.Status);
		Assert.True(decision.ShouldContinueToCooldownFanout);
		Assert.True(decision.ShouldStoreCooldown);
		Assert.True(decision.ShouldBroadcastCooldown);
		Assert.True(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
	}

	private static BindPointTeleportKinahCallbackComposition CreateComposition(
		BindPointTeleportKinahPersistenceStatus persistenceStatus,
		bool includePacketTemplate,
		bool includeRuntimeResult,
		bool shouldTeleport = true)
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 2_000, requiredPrice: 1_000, shouldTeleport);
		var persistenceResult = callbackPlan.KinahItemUpdate == null
			? null
			: new BindPointTeleportKinahPersistenceResult(
				persistenceStatus,
				callbackPlan.KinahItemUpdate.OwnerId,
				callbackPlan.KinahItemUpdate.ObjectId,
				callbackPlan.KinahItemUpdate.Count,
				ShouldRollbackInMemoryMutation: persistenceStatus != BindPointTeleportKinahPersistenceStatus.Saved,
				"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
				IsLive: false);
		var persistenceDecision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);
		var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			persistenceDecision,
			includePacketTemplate ? CreateKinahTemplate() : null);
		return BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			persistenceDecision,
			packetPlan,
			includeRuntimeResult ? CreateRuntimeResult(callbackPlan, shouldTeleport) : null);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(
		long currentKinah,
		long requiredPrice,
		bool shouldTeleport)
	{
		var playerObjectId = 7001;
		var locId = 6001;
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(requiredPrice, currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = playerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = playerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
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
			playerIsAboutToDie: !shouldTeleport);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			kinahMutationPlan: mutationPlan);
	}

	private static BindPointTeleportRuntimeCallbackExecutionResult CreateRuntimeResult(
		BindPointTeleportScheduledCallbackPlan callbackPlan,
		bool shouldTeleport)
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
			shouldTeleport,
			"BindPointTeleportService.teleport scheduled task -> addCooldown -> broadcast action 3 -> schedule final teleport",
			IsLive: false);
	}

	private static BindPointTeleportKinahInventorySendResult CreateSendResult(
		BindPointTeleportKinahInventorySendStatus status,
		bool sentPacket = true)
	{
		return new BindPointTeleportKinahInventorySendResult(
			status,
			PlayerObjectId: 7001,
			SentPacket: status == BindPointTeleportKinahInventorySendStatus.Sent && sentPacket,
			"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM)",
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
