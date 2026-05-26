using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportScheduledKinahMutationPlanServiceTests
{
	[Fact]
	public void CreatePlan_MissingKinahSendsFeeAndDoesNotPrepareMutation()
	{
		var player = CreatePlayer([CreateItem(5001, 100100001, 1)]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_500);

		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah, plan.Status);
		Assert.Equal(0, plan.CurrentKinah);
		Assert.Null(plan.RemainingKinah);
		Assert.Null(plan.KinahItemUpdate);
		Assert.True(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldEmitInventoryUpdatePacket);
		Assert.Null(plan.InventoryUpdateType);
		Assert.Equal([5001], plan.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal([5001], player.InventoryItems.Select(item => item.ObjectId).ToArray());
	}

	[Fact]
	public void CreatePlan_InsufficientKinahSendsFeeAndPreservesSnapshot()
	{
		var kinah = CreateKinah(count: 1_499);
		var player = CreatePlayer([kinah]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_500);

		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah, plan.Status);
		Assert.Equal(1_499, plan.CurrentKinah);
		Assert.Null(plan.KinahItemUpdate);
		Assert.True(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldEmitInventoryUpdatePacket);
		Assert.Equal(1_499, plan.InventoryItems.Single().Count);
		Assert.Same(kinah, plan.InventoryItems.Single());
		Assert.Equal(1_499, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_ExactKinahSucceedsAndKeepsZeroCountKinahItem()
	{
		var player = CreatePlayer([CreateKinah(count: 1_500)]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_500);

		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady, plan.Status);
		Assert.Equal(1_500, plan.CurrentKinah);
		Assert.Equal(0, plan.RemainingKinah);
		Assert.NotNull(plan.KinahItemUpdate);
		Assert.Equal(0, plan.KinahItemUpdate.Count);
		Assert.Contains(plan.InventoryItems, item => item.ItemId == BindPointTeleportScheduledKinahMutationPlanService.KinahItemId && item.Count == 0);
		Assert.False(plan.ShouldSendNotEnoughFee);
		Assert.True(plan.ShouldEmitInventoryUpdatePacket);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, plan.InventoryUpdateType);
		Assert.Equal(1_500, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_PositiveDecrementPreparesKinahUpdateAndPacketIntent()
	{
		var player = CreatePlayer([CreateKinah(count: 2_000)]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_500);

		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady, plan.Status);
		Assert.Equal(500, plan.RemainingKinah);
		Assert.Equal(500, plan.KinahItemUpdate?.Count);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, plan.InventoryUpdateType);
		Assert.Equal(
			[
				BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
				BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
				BindPointTeleportScheduledKinahMutationPlanStep.PrepareKinahItemUpdate,
				BindPointTeleportScheduledKinahMutationPlanStep.PrepareInventoryUpdatePacket,
			],
			plan.Steps);
		Assert.Equal(2_000, player.InventoryItems.Single().Count);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreatePlan_NonPositivePriceContinuesWithoutMutationLikeJava(long requiredPrice)
	{
		var player = CreatePlayer([CreateKinah(count: 2_000)]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice);

		Assert.Equal(BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady, plan.Status);
		Assert.Equal(2_000, plan.CurrentKinah);
		Assert.Equal(2_000, plan.RemainingKinah);
		Assert.Null(plan.KinahItemUpdate);
		Assert.False(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldEmitInventoryUpdatePacket);
		Assert.Null(plan.InventoryUpdateType);
		Assert.Contains(BindPointTeleportScheduledKinahMutationPlanStep.ContinueWithoutMutation, plan.Steps);
		Assert.Equal(2_000, player.InventoryItems.Single().Count);
	}

	[Fact]
	public void CreatePlan_PreservesUnrelatedInventoryAndCopiesKinahMetadata()
	{
		var kinah = CreateKinah(count: 2_000, slot: 4, color: 123);
		var unrelated = CreateItem(5001, 100100001, 1);
		var player = CreatePlayer([unrelated, kinah]);

		var plan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(player, requiredPrice: 1_500);

		Assert.Equal([5001, 1824], plan.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Same(unrelated, plan.InventoryItems[0]);
		Assert.NotSame(kinah, plan.KinahItemUpdate);
		Assert.Equal(4, plan.KinahItemUpdate?.Slot);
		Assert.Equal(123, plan.KinahItemUpdate?.Color);
		Assert.Equal(500, plan.InventoryItems.Single(item => item.ObjectId == 1824).Count);
		Assert.Equal(2_000, kinah.Count);
	}

	private static Player CreatePlayer(IReadOnlyList<InventoryItem> inventoryItems)
	{
		return new Player { ObjectId = 7001, InventoryItems = inventoryItems };
	}

	private static InventoryItem CreateKinah(long count, long slot = 0, int color = 0)
	{
		return CreateItem(1824, BindPointTeleportScheduledKinahMutationPlanService.KinahItemId, count, slot, color);
	}

	private static InventoryItem CreateItem(int objectId, int itemId, long count, long slot = 0, int color = 0)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			OwnerId = 7001,
			ItemId = itemId,
			Count = count,
			Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
			Slot = slot,
			Color = color,
		};
	}
}
