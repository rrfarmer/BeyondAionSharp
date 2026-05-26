using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahInventoryOwnerCallbackBridgeServiceTests
{
	[Fact]
	public void CreatePlan_NotEnoughOwnerResultStopsBeforePersistence()
	{
		var ownerResult = CreateOwnerResult(currentKinah: 500, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahInventoryOwnerCallbackBridgeService.CreatePlan(ownerResult);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.StoppedNotEnoughKinah, plan.Status);
		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah, plan.MutationPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.NotEnoughKinah, plan.PersistenceOperationPlan.Status);
		Assert.True(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldCreatePersistenceDecision);
		Assert.False(plan.ShouldCreateInventoryPacketIntent);
		Assert.False(plan.ShouldContinueScheduledTeleport);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NonPositiveOwnerResultContinuesWithoutPersistence()
	{
		var ownerResult = CreateOwnerResult(currentKinah: 500, requiredPrice: 0);

		var plan = BindPointTeleportKinahInventoryOwnerCallbackBridgeService.CreatePlan(ownerResult);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.ContinueWithoutMutation, plan.Status);
		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady, plan.MutationPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.NoMutationRequired, plan.PersistenceOperationPlan.Status);
		Assert.False(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldCreatePersistenceDecision);
		Assert.False(plan.ShouldCreateInventoryPacketIntent);
		Assert.True(plan.ShouldContinueScheduledTeleport);
		Assert.Null(plan.MutationPlan.KinahItemUpdate);
	}

	[Fact]
	public void CreatePlan_AppliedOwnerResultCreatesPersistenceOperationMetadata()
	{
		var ownerResult = CreateOwnerResult(currentKinah: 2_000, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahInventoryOwnerCallbackBridgeService.CreatePlan(ownerResult);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerCallbackBridgeStatus.MutationReady, plan.Status);
		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady, plan.MutationPlan.Status);
		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.UpdateReady, plan.PersistenceOperationPlan.Status);
		Assert.True(plan.ShouldCreatePersistenceDecision);
		Assert.False(plan.ShouldCreateInventoryPacketIntent);
		Assert.True(plan.ShouldContinueScheduledTeleport);
		Assert.Equal(1_000, plan.MutationPlan.KinahItemUpdate?.Count);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, plan.MutationPlan.InventoryUpdateType);
		Assert.Equal(1_000, plan.PersistenceOperationPlan.KinahCount);
	}

	private static BindPointTeleportKinahInventoryOwnerMutationResult CreateOwnerResult(
		long currentKinah,
		long requiredPrice)
	{
		var owner = new BindPointTeleportKinahInventoryOwnerService();
		return owner.TryApplyScheduledDecrease(
			new Player
			{
				ObjectId = 7001,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = 7001,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
	}
}
