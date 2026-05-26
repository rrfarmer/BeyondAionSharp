using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahPersistenceDecisionBridgeServiceTests
{
	[Fact]
	public void CreateDecision_NotEnoughKinahStopsBeforePersistencePacketCooldownFanoutAndMovement()
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 500, requiredPrice: 1_000);

		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult: null);

		Assert.Equal(BindPointTeleportKinahPersistenceDecisionStatus.StoppedNotEnoughKinah, decision.Status);
		Assert.False(decision.IsLive);
		Assert.True(decision.ShouldSendNotEnoughFee);
		Assert.False(decision.ShouldEmitKinahInventoryUpdatePacket);
		Assert.False(decision.ShouldRollbackInMemoryMutation);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
		Assert.Null(decision.PersistenceResult);
		Assert.Null(decision.KinahItemUpdate);
		Assert.Null(decision.KinahInventoryUpdateType);
	}

	[Fact]
	public void CreateDecision_MissingPersistenceResultStopsAndRequiresRollback()
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 2_000, requiredPrice: 1_000);

		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult: null);

		Assert.Equal(BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingPersistenceResult, decision.Status);
		Assert.True(decision.ShouldRollbackInMemoryMutation);
		Assert.False(decision.ShouldEmitKinahInventoryUpdatePacket);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
		Assert.Null(decision.KinahItemUpdate);
		Assert.Null(decision.KinahInventoryUpdateType);
	}

	[Theory]
	[InlineData(BindPointTeleportKinahPersistenceStatus.MissingRow, BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingRow)]
	[InlineData(BindPointTeleportKinahPersistenceStatus.Failed, BindPointTeleportKinahPersistenceDecisionStatus.StoppedFailed)]
	public void CreateDecision_PersistenceFailureStopsBeforePacketCooldownFanoutAndMovement(
		BindPointTeleportKinahPersistenceStatus persistenceStatus,
		BindPointTeleportKinahPersistenceDecisionStatus expectedStatus)
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 2_000, requiredPrice: 1_000);
		var persistenceResult = CreatePersistenceResult(persistenceStatus, callbackPlan.KinahItemUpdate!);

		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);

		Assert.Equal(expectedStatus, decision.Status);
		Assert.Same(persistenceResult, decision.PersistenceResult);
		Assert.True(decision.ShouldRollbackInMemoryMutation);
		Assert.False(decision.ShouldEmitKinahInventoryUpdatePacket);
		Assert.False(decision.ShouldContinueToCooldownFanout);
		Assert.False(decision.ShouldScheduleFinalTeleport);
		Assert.False(decision.ShouldTeleport);
		Assert.Null(decision.KinahItemUpdate);
		Assert.Null(decision.KinahInventoryUpdateType);
	}

	[Fact]
	public void CreateDecision_SavedPersistenceAllowsPacketMetadataAndCooldownFanoutToContinue()
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 2_000, requiredPrice: 1_000);
		var persistenceResult = CreatePersistenceResult(
			BindPointTeleportKinahPersistenceStatus.Saved,
			callbackPlan.KinahItemUpdate!);

		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);

		Assert.Equal(BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence, decision.Status);
		Assert.Same(persistenceResult, decision.PersistenceResult);
		Assert.Same(callbackPlan.KinahItemUpdate, decision.KinahItemUpdate);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, decision.KinahInventoryUpdateType);
		Assert.True(decision.ShouldEmitKinahInventoryUpdatePacket);
		Assert.False(decision.ShouldRollbackInMemoryMutation);
		Assert.True(decision.ShouldContinueToCooldownFanout);
		Assert.True(decision.ShouldScheduleFinalTeleport);
		Assert.True(decision.ShouldTeleport);
		Assert.Equal(1_000, decision.KinahItemUpdate?.Count);
	}

	[Fact]
	public void CreateDecision_NonPositivePriceContinuesWithoutPersistenceOrPacket()
	{
		var callbackPlan = CreateCallbackPlan(currentKinah: 2_000, requiredPrice: 0);

		var decision = BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult: null);

		Assert.Equal(BindPointTeleportKinahPersistenceDecisionStatus.ContinueWithoutPersistence, decision.Status);
		Assert.False(decision.ShouldEmitKinahInventoryUpdatePacket);
		Assert.False(decision.ShouldRollbackInMemoryMutation);
		Assert.True(decision.ShouldContinueToCooldownFanout);
		Assert.True(decision.ShouldScheduleFinalTeleport);
		Assert.True(decision.ShouldTeleport);
		Assert.Null(decision.KinahItemUpdate);
		Assert.Null(decision.KinahInventoryUpdateType);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(long currentKinah, long requiredPrice)
	{
		var playerObjectId = 7001;
		var locId = 6001;
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice,
			currentKinah);
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
			playerIsAboutToDie: false);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			kinahMutationPlan: mutationPlan);
	}

	private static BindPointTeleportKinahPersistenceResult CreatePersistenceResult(
		BindPointTeleportKinahPersistenceStatus status,
		InventoryItem kinahItem)
	{
		return new BindPointTeleportKinahPersistenceResult(
			status,
			kinahItem.OwnerId,
			kinahItem.ObjectId,
			kinahItem.Count,
			ShouldRollbackInMemoryMutation: status != BindPointTeleportKinahPersistenceStatus.Saved,
			"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
			IsLive: false);
	}
}
