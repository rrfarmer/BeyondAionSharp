using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TuneResultApplicationPlanServiceTests
{
	[Fact]
	public void CreatePlan_AuditsWhenPendingTuneResultIsMissing()
	{
		var targetItem = CreateItem();

		var plan = TuneResultApplicationPlanService.CreatePlan(targetItem);

		Assert.Equal(TuneResultApplicationPlanStatus.MissingPendingResultAudited, plan.Status);
		Assert.Same(targetItem, plan.ResultingTargetItem);
		Assert.False(plan.TargetItemMutated);
		Assert.False(plan.TargetItemPersistentUpdateRequired);
		Assert.False(plan.InventoryPersistentUpdateRequired);
		Assert.Equal("attempted to apply a tune result without tuning the item beforehand.", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_AppliesPendingTuneResultToInventoryItem()
	{
		var pendingResult = new PendingTuneResult(OptionalSockets: 5, EnchantBonus: 7, StatBonusId: 9, IsAttributeOnly: false);
		var targetItem = CreateItem(optionalSockets: 1, enchantBonus: 2, randomBonus: 3, pendingTuneResult: pendingResult);

		var plan = TuneResultApplicationPlanService.CreatePlan(targetItem);

		Assert.Equal(TuneResultApplicationPlanStatus.Applied, plan.Status);
		Assert.NotSame(targetItem, plan.ResultingTargetItem);
		Assert.True(plan.TargetItemMutated);
		Assert.True(plan.TargetItemPersistentUpdateRequired);
		Assert.True(plan.InventoryPersistentUpdateRequired);
		Assert.Equal(5, plan.ResultingTargetItem.OptionalSocket);
		Assert.Equal(7, plan.ResultingTargetItem.EnchantBonus);
		Assert.Equal(9, plan.ResultingTargetItem.RandomBonus);
		Assert.Equal(targetItem.TuneCount, plan.ResultingTargetItem.TuneCount);
		Assert.Null(plan.ResultingTargetItem.PendingTuneResult);
		Assert.Null(plan.AuditMessage);
		Assert.Contains("UPDATE_REQUIRED", plan.JavaSource, StringComparison.Ordinal);
	}

	private static InventoryItem CreateItem(
		int optionalSockets = 0,
		int enchantBonus = 0,
		int randomBonus = 0,
		PendingTuneResult? pendingTuneResult = null) =>
		new()
		{
			ObjectId = 1001,
			ItemId = 110100001,
			OwnerId = 9001,
			Location = 0,
			Slot = 1,
			Count = 1,
			TuneCount = 3,
			OptionalSocket = optionalSockets,
			EnchantBonus = enchantBonus,
			RandomBonus = randomBonus,
			PendingTuneResult = pendingTuneResult,
		};
}
