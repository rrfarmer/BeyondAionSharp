using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahInventoryOwnerServiceTests
{
	[Fact]
	public void TryApplyScheduledDecrease_MissingKinahStopsWithFee()
	{
		var player = CreatePlayer(currentKinah: null);
		var owner = new BindPointTeleportKinahInventoryOwnerService();

		var result = owner.TryApplyScheduledDecrease(player, requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah, result.Status);
		Assert.Equal(0, result.OriginalKinah);
		Assert.Null(result.RemainingKinah);
		Assert.True(result.ShouldSendNotEnoughFee);
		Assert.False(result.ShouldEmitInventoryUpdatePacket);
		Assert.False(result.ShouldContinueScheduledTeleport);
		Assert.Empty(player.InventoryItems);
	}

	[Fact]
	public void TryApplyScheduledDecrease_InsufficientKinahStopsWithoutMutation()
	{
		var player = CreatePlayer(currentKinah: 500);
		var owner = new BindPointTeleportKinahInventoryOwnerService();

		var result = owner.TryApplyScheduledDecrease(player, requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah, result.Status);
		Assert.Equal(500, result.OriginalKinah);
		Assert.Null(result.RemainingKinah);
		Assert.True(result.ShouldSendNotEnoughFee);
		Assert.Equal(500, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void TryApplyScheduledDecrease_NonPositivePriceContinuesWithoutMutation()
	{
		var player = CreatePlayer(currentKinah: 500);
		var owner = new BindPointTeleportKinahInventoryOwnerService();

		var result = owner.TryApplyScheduledDecrease(player, requiredPrice: 0);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.ContinueWithoutMutation, result.Status);
		Assert.Equal(500, result.OriginalKinah);
		Assert.Equal(500, result.RemainingKinah);
		Assert.False(result.ShouldSendNotEnoughFee);
		Assert.False(result.ShouldEmitInventoryUpdatePacket);
		Assert.True(result.ShouldContinueScheduledTeleport);
		Assert.Equal(500, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void TryApplyScheduledDecrease_ExactPriceKeepsZeroCountKinahItem()
	{
		var player = CreatePlayer(currentKinah: 1_000);
		var owner = new BindPointTeleportKinahInventoryOwnerService();

		var result = owner.TryApplyScheduledDecrease(player, requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerMutationStatus.AppliedMutation, result.Status);
		Assert.Equal(1_000, result.OriginalKinah);
		Assert.Equal(0, result.RemainingKinah);
		Assert.Equal(0, result.UpdatedKinahItem?.Count);
		Assert.True(result.ShouldEmitInventoryUpdatePacket);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, result.InventoryUpdateType);
		Assert.True(result.ShouldContinueScheduledTeleport);
		Assert.Single(player.InventoryItems);
		Assert.Equal(0, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void RollbackScheduledDecrease_RestoresOriginalKinahSnapshot()
	{
		var player = CreatePlayer(currentKinah: 2_000);
		var owner = new BindPointTeleportKinahInventoryOwnerService();
		var mutation = owner.TryApplyScheduledDecrease(player, requiredPrice: 1_000);

		var rollback = owner.RollbackScheduledDecrease(player, mutation);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerRollbackStatus.RestoredOriginalKinah, rollback.Status);
		Assert.True(rollback.RestoredOriginalKinah);
		Assert.Equal(2_000, player.InventoryItems.Single().Count);
		Assert.Equal(2_000, rollback.InventoryAfterRollback.Single().Count);
	}

	[Fact]
	public void RollbackScheduledDecrease_NoMutationIsNoOp()
	{
		var player = CreatePlayer(currentKinah: 500);
		var owner = new BindPointTeleportKinahInventoryOwnerService();
		var mutation = owner.TryApplyScheduledDecrease(player, requiredPrice: 1_000);

		var rollback = owner.RollbackScheduledDecrease(player, mutation);

		Assert.Equal(BindPointTeleportKinahInventoryOwnerRollbackStatus.NoMutationToRollback, rollback.Status);
		Assert.False(rollback.RestoredOriginalKinah);
		Assert.Equal(500, player.InventoryItems.Single().Count);
	}

	[Fact]
	public async Task TryApplyScheduledDecrease_ConcurrentDoubleSpendAllowsOnlyOneMutation()
	{
		var player = CreatePlayer(currentKinah: 1_000);
		var owner = new BindPointTeleportKinahInventoryOwnerService();

		var first = Task.Run(() => owner.TryApplyScheduledDecrease(player, requiredPrice: 700));
		var second = Task.Run(() => owner.TryApplyScheduledDecrease(player, requiredPrice: 700));
		var results = await Task.WhenAll(first, second);

		Assert.Equal(1, results.Count(result => result.Status == BindPointTeleportKinahInventoryOwnerMutationStatus.AppliedMutation));
		Assert.Equal(1, results.Count(result => result.Status == BindPointTeleportKinahInventoryOwnerMutationStatus.NotEnoughKinah));
		Assert.Equal(300, player.InventoryItems.Single().Count);
	}

	private static Player CreatePlayer(long? currentKinah)
	{
		return new Player
		{
			ObjectId = 7001,
			InventoryItems = currentKinah == null
				? []
				:
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = 7001,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah.Value,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
		};
	}
}
