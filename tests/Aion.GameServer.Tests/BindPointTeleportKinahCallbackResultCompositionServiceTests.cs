using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahCallbackResultCompositionServiceTests
{
	[Fact]
	public void CreateComposition_StoppedPersistenceDecisionBlocksPacketCooldownFanoutAndMovement()
	{
		var (decision, packetPlan, runtimeResult) = CreateInputs(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.MissingRow,
			includeRuntimeResult: true);

		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			decision,
			packetPlan,
			runtimeResult);

		Assert.Equal(BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforePersistence, composition.Status);
		Assert.False(composition.IsLive);
		Assert.Null(composition.InventoryUpdatePacket);
		Assert.False(composition.ShouldSendInventoryUpdatePacket);
		Assert.False(composition.ShouldStoreCooldown);
		Assert.False(composition.ShouldBroadcastCooldown);
		Assert.False(composition.ShouldScheduleFinalTeleport);
		Assert.False(composition.ShouldTeleport);
		Assert.Equal(
			[BindPointTeleportKinahCallbackCompositionStep.CheckPersistenceDecision],
			composition.Steps);
	}

	[Fact]
	public void CreateComposition_MissingPacketPlanBlocksRuntimeMetadata()
	{
		var (decision, packetPlan, runtimeResult) = CreateInputs(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeRuntimeResult: true,
			includeKinahTemplate: false);

		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			decision,
			packetPlan,
			runtimeResult);

		Assert.Equal(BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforePacket, composition.Status);
		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.MissingTemplate, composition.PacketPlan.Status);
		Assert.False(composition.ShouldSendInventoryUpdatePacket);
		Assert.False(composition.ShouldStoreCooldown);
		Assert.False(composition.ShouldBroadcastCooldown);
		Assert.False(composition.ShouldScheduleFinalTeleport);
	}

	[Fact]
	public void CreateComposition_MissingRuntimeResultKeepsPacketButBlocksCooldownFanoutAndMovement()
	{
		var (decision, packetPlan, _) = CreateInputs(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeRuntimeResult: false);

		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			decision,
			packetPlan,
			runtimeResult: null);

		Assert.Equal(BindPointTeleportKinahCallbackCompositionStatus.StoppedBeforeRuntimeCallback, composition.Status);
		Assert.Same(packetPlan.Packet, composition.InventoryUpdatePacket);
		Assert.True(composition.ShouldSendInventoryUpdatePacket);
		Assert.False(composition.ShouldStoreCooldown);
		Assert.False(composition.ShouldBroadcastCooldown);
		Assert.False(composition.ShouldScheduleFinalTeleport);
		Assert.Equal(
			[
				BindPointTeleportKinahCallbackCompositionStep.CheckPersistenceDecision,
				BindPointTeleportKinahCallbackCompositionStep.CreateInventoryUpdatePacketIntent,
			],
			composition.Steps);
	}

	[Fact]
	public void CreateComposition_SavedPacketAndRuntimeMetadataComposeJavaOrderWithMovement()
	{
		var (decision, packetPlan, runtimeResult) = CreateInputs(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeRuntimeResult: true);

		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			decision,
			packetPlan,
			runtimeResult);

		Assert.Equal(BindPointTeleportKinahCallbackCompositionStatus.ReadyWithRuntimeCallback, composition.Status);
		Assert.Same(packetPlan.Packet, composition.InventoryUpdatePacket);
		Assert.True(composition.ShouldSendInventoryUpdatePacket);
		Assert.True(composition.ShouldStoreCooldown);
		Assert.True(composition.ShouldBroadcastCooldown);
		Assert.True(composition.ShouldScheduleFinalTeleport);
		Assert.True(composition.ShouldTeleport);
		Assert.Equal(
			[
				BindPointTeleportKinahCallbackCompositionStep.CheckPersistenceDecision,
				BindPointTeleportKinahCallbackCompositionStep.CreateInventoryUpdatePacketIntent,
				BindPointTeleportKinahCallbackCompositionStep.StoreCooldown,
				BindPointTeleportKinahCallbackCompositionStep.BroadcastCooldown,
				BindPointTeleportKinahCallbackCompositionStep.ScheduleFinalTeleport,
				BindPointTeleportKinahCallbackCompositionStep.CreateFinalMovementIntent,
			],
			composition.Steps);
	}

	[Fact]
	public void CreateComposition_RuntimeMetadataWithoutMovementKeepsFinalMovementBlocked()
	{
		var (decision, packetPlan, runtimeResult) = CreateInputs(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved,
			includeRuntimeResult: true,
			shouldTeleport: false);

		var composition = BindPointTeleportKinahCallbackResultCompositionService.CreateComposition(
			decision,
			packetPlan,
			runtimeResult);

		Assert.Equal(BindPointTeleportKinahCallbackCompositionStatus.ReadyWithRuntimeCallback, composition.Status);
		Assert.True(composition.ShouldSendInventoryUpdatePacket);
		Assert.True(composition.ShouldStoreCooldown);
		Assert.True(composition.ShouldBroadcastCooldown);
		Assert.True(composition.ShouldScheduleFinalTeleport);
		Assert.False(composition.ShouldTeleport);
		Assert.DoesNotContain(BindPointTeleportKinahCallbackCompositionStep.CreateFinalMovementIntent, composition.Steps);
	}

	private static (
		BindPointTeleportKinahPersistenceDecision Decision,
		BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
		BindPointTeleportRuntimeCallbackExecutionResult? RuntimeResult) CreateInputs(
			long currentKinah,
			long requiredPrice,
			BindPointTeleportKinahPersistenceStatus? persistenceStatus,
			bool includeRuntimeResult,
			bool includeKinahTemplate = true,
			bool shouldTeleport = true)
	{
		var callbackPlan = CreateCallbackPlan(currentKinah, requiredPrice, shouldTeleport);
		var persistenceResult = persistenceStatus == null || callbackPlan.KinahItemUpdate == null
			? null
			: new BindPointTeleportKinahPersistenceResult(
				persistenceStatus.Value,
				callbackPlan.KinahItemUpdate.OwnerId,
				callbackPlan.KinahItemUpdate.ObjectId,
				callbackPlan.KinahItemUpdate.Count,
				ShouldRollbackInMemoryMutation: persistenceStatus != BindPointTeleportKinahPersistenceStatus.Saved,
				"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
				IsLive: false);
		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(callbackPlan, persistenceResult);
		var packetPlan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			decision,
			includeKinahTemplate ? CreateKinahTemplate() : null);
		var runtimeResult = includeRuntimeResult
			? CreateRuntimeResult(callbackPlan, shouldTeleport)
			: null;
		return (decision, packetPlan, runtimeResult);
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
		var cooldown = new BindPointTeleportCooldownFact(7001, 6001, CooldownEndMillis: 601_000);
		var fanout = new BindPointTeleportRuntimeFanoutResult(
			BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf,
			callbackPlan.CooldownFanoutPlan,
			SentCount: 1,
			SentPacket: true,
			"PacketSendUtility.broadcastPacket(player, action 3, true)",
			IsLive: false);
		return new BindPointTeleportRuntimeCallbackExecutionResult(
			BindPointTeleportRuntimeCallbackExecutionStatus.StoredCooldownAndBroadcast,
			callbackPlan,
			cooldown,
			fanout,
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
