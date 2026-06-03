using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class LegionWarehousePersistenceContractPlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_LogoutMode_RecordsPlayerAccountAndLegionParameters()
	{
		var plan = LegionWarehousePersistenceContractPlanService.CreateDisabledPlan(
			LegionWarehousePersistenceMode.Logout);

		Assert.Equal(LegionWarehousePersistenceMode.Logout, plan.Mode);
		Assert.Equal("LegionService.LegionWhUpdate(Player)", plan.JavaStoreCall.JavaCaller);
		Assert.Equal("player.getObjectId()", plan.JavaStoreCall.PlayerObjectIdArgument);
		Assert.Equal("player.getAccount().getId()", plan.JavaStoreCall.AccountIdArgument);
		Assert.Equal("legion.getLegionId()", plan.JavaStoreCall.LegionIdArgument);
		Assert.True(plan.JavaStoreCall.PassesPlayerObjectId);
		Assert.True(plan.JavaStoreCall.PassesAccountId);
		Assert.True(plan.JavaStoreCall.PassesLegionId);
		Assert.True(plan.JavaStoreCall.UsesItemsWithKinah);
		Assert.True(plan.JavaStoreCall.UsesDeletedItems);
		Assert.True(plan.JavaStoreCall.CallsItemStoneSaveAfterInventoryStore);
		Assert.True(plan.JavaStoreCall.CallerSwallowsAndLogsExceptions);
	}

	[Fact]
	public void CreateDisabledPlan_PeriodicMode_RecordsNullPlayerAndAccountParameters()
	{
		var plan = LegionWarehousePersistenceContractPlanService.CreateDisabledPlan(
			LegionWarehousePersistenceMode.PeriodicSave);

		Assert.Equal(LegionWarehousePersistenceMode.PeriodicSave, plan.Mode);
		Assert.Equal("PeriodicSaveService.LegionWarehouseSaveTask.run", plan.JavaStoreCall.JavaCaller);
		Assert.Equal("null", plan.JavaStoreCall.PlayerObjectIdArgument);
		Assert.Equal("null", plan.JavaStoreCall.AccountIdArgument);
		Assert.Equal("legion.getLegionId()", plan.JavaStoreCall.LegionIdArgument);
		Assert.False(plan.JavaStoreCall.PassesPlayerObjectId);
		Assert.False(plan.JavaStoreCall.PassesAccountId);
		Assert.True(plan.JavaStoreCall.PassesLegionId);
		Assert.True(plan.JavaStoreCall.UsesItemsWithKinah);
		Assert.True(plan.JavaStoreCall.UsesDeletedItems);
		Assert.True(plan.JavaStoreCall.CallsItemStoneSaveAfterInventoryStore);
		Assert.True(plan.JavaStoreCall.CallerSwallowsAndLogsExceptions);
	}

	[Fact]
	public void CreateDisabledPlan_WithMissingContracts_KeepsRepositoryWiringDisabled()
	{
		var plan = LegionWarehousePersistenceContractPlanService.CreateDisabledPlan(
			LegionWarehousePersistenceMode.Logout,
			new LegionWarehousePersistenceContractPrerequisites(
				InventoryPersistenceContractAvailable: true));

		Assert.Equal(LegionWarehousePersistenceContractStatus.DisabledMissingContracts, plan.Status);
		Assert.False(plan.ShouldAddRepositoryMethod);
		Assert.False(plan.DidAddRepositoryMethod);
		Assert.False(plan.ReadyForRepositoryWiring);
		Assert.False(plan.IsLive);
		Assert.DoesNotContain(LegionWarehousePersistenceContractCriterion.InventoryPersistenceContractAvailable, plan.MissingCriteria);
		Assert.Contains(LegionWarehousePersistenceContractCriterion.ItemStonePersistenceContractAvailable, plan.MissingCriteria);
		Assert.Contains(LegionWarehousePersistenceContractCriterion.RepositoryMethodAvailable, plan.MissingCriteria);
		Assert.Contains("ItemStoneListDAO.save", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("catch Exception", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateDisabledPlan_AllPrerequisitesReady_MarksReadyButDoesNotAddRepositoryMethod()
	{
		var plan = LegionWarehousePersistenceContractPlanService.CreateDisabledPlan(
			LegionWarehousePersistenceMode.PeriodicSave,
			new LegionWarehousePersistenceContractPrerequisites(
				InventoryPersistenceContractAvailable: true,
				ItemStonePersistenceContractAvailable: true,
				RepositoryMethodAvailable: true));

		Assert.Equal(LegionWarehousePersistenceContractStatus.ReadyForRepositoryWiring, plan.Status);
		Assert.Empty(plan.MissingCriteria);
		Assert.True(plan.ShouldAddRepositoryMethod);
		Assert.False(plan.DidAddRepositoryMethod);
		Assert.True(plan.ReadyForRepositoryWiring);
		Assert.False(plan.IsLive);
		Assert.Contains("SaveLegionWarehouseItemsAsync", plan.FutureRepositoryMethod, StringComparison.Ordinal);
	}
}
