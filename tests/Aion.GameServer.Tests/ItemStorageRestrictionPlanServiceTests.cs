using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemStorageRestrictionPlanServiceTests
{
	// --- isItemRestrictedFrom ---

	[Fact]
	public void CreateIsItemRestrictedFromPlan_LegionWarehouseWithRightIsAllowed()
	{
		var plan = ItemStorageRestrictionPlanService.CreateIsItemRestrictedFromPlan(
			"LEGION_WAREHOUSE", isLegionWarehouseEnabled: true, isLegionMember: true, hasWHWithdrawalRight: true);

		Assert.Equal(ItemStorageRestrictionPlanStatus.Allowed, plan.Status);
		Assert.False(plan.IsRestricted);
		Assert.Null(plan.DenialMessage);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(false, true, true)]  // !enabled
	[InlineData(true, false, true)]  // !member
	[InlineData(true, true, false)]  // !withdrawal
	public void CreateIsItemRestrictedFromPlan_LegionWarehouseBlocksWithNoRight(bool enabled, bool member, bool withdrawal)
	{
		var plan = ItemStorageRestrictionPlanService.CreateIsItemRestrictedFromPlan(
			"LEGION_WAREHOUSE", enabled, member, withdrawal);

		Assert.Equal(ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseNoWithdrawalRight, plan.Status);
		Assert.True(plan.IsRestricted);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300322, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateIsItemRestrictedFromPlan_OtherStorageTypesAreAllowed()
	{
		var plan = ItemStorageRestrictionPlanService.CreateIsItemRestrictedFromPlan(
			"CUBE", isLegionWarehouseEnabled: false, isLegionMember: false, hasWHWithdrawalRight: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.Allowed, plan.Status);
		Assert.False(plan.IsRestricted);
	}

	// --- isItemRestrictedTo ---

	[Fact]
	public void CreateIsItemRestrictedToPlan_RegularWarehouseBlocksNonStorable()
	{
		var plan = CreateDefaultRestrictedToPlan("REGULAR_WAREHOUSE", itemIsStorableInWarehouse: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.BlockedRegularWarehouseItemRestricted, plan.Status);
		Assert.Equal(1300418, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateIsItemRestrictedToPlan_RegularWarehouseAllowsStorable()
	{
		var plan = CreateDefaultRestrictedToPlan("REGULAR_WAREHOUSE", itemIsStorableInWarehouse: true);

		Assert.Equal(ItemStorageRestrictionPlanStatus.Allowed, plan.Status);
	}

	[Fact]
	public void CreateIsItemRestrictedToPlan_AccountWarehouseBlocksNonStorable()
	{
		var plan = CreateDefaultRestrictedToPlan("ACCOUNT_WAREHOUSE", itemIsStorableInAccWarehouse: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.BlockedAccountWarehouseItemRestricted, plan.Status);
		Assert.Equal(1400356, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateIsItemRestrictedToPlan_LegionWarehouseBlocksNonStorableItem()
	{
		var plan = CreateDefaultRestrictedToPlan("LEGION_WAREHOUSE", itemIsStorableInLegWarehouse: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseItemRestricted, plan.Status);
		Assert.Equal(1400355, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateIsItemRestrictedToPlan_LegionWarehouseBlocksNonMember()
	{
		var plan = ItemStorageRestrictionPlanService.CreateIsItemRestrictedToPlan(
			"LEGION_WAREHOUSE",
			itemIsStorableInWarehouse: true, itemIsStorableInAccWarehouse: true, itemIsStorableInLegWarehouse: true,
			isLegionWarehouseEnabled: true, isLegionMember: false,
			hasWHDepositRight: false, hasWHWithdrawalRight: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.BlockedLegionWarehouseNoDepositRight, plan.Status);
		Assert.Equal(1300322, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreateIsItemRestrictedToPlan_LegionWarehouseAllowsWithDepositRight()
	{
		var plan = ItemStorageRestrictionPlanService.CreateIsItemRestrictedToPlan(
			"LEGION_WAREHOUSE",
			itemIsStorableInWarehouse: true, itemIsStorableInAccWarehouse: true, itemIsStorableInLegWarehouse: true,
			isLegionWarehouseEnabled: true, isLegionMember: true,
			hasWHDepositRight: true, hasWHWithdrawalRight: false);

		Assert.Equal(ItemStorageRestrictionPlanStatus.Allowed, plan.Status);
	}

	private static ItemStorageRestrictionPlan CreateDefaultRestrictedToPlan(
		string storageType,
		bool itemIsStorableInWarehouse = true,
		bool itemIsStorableInAccWarehouse = true,
		bool itemIsStorableInLegWarehouse = true)
	{
		return ItemStorageRestrictionPlanService.CreateIsItemRestrictedToPlan(
			storageType,
			itemIsStorableInWarehouse, itemIsStorableInAccWarehouse, itemIsStorableInLegWarehouse,
			isLegionWarehouseEnabled: true, isLegionMember: true,
			hasWHDepositRight: true, hasWHWithdrawalRight: true);
	}
}
