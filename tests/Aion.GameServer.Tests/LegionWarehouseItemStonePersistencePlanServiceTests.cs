using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class LegionWarehouseItemStonePersistencePlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_BlankItems_SkipsLikeJava()
	{
		var plan = LegionWarehouseItemStonePersistencePlanService.CreateDisabledPlan(0);

		Assert.Equal(LegionWarehouseItemStonePersistenceStatus.SkippedBlankItems, plan.Status);
		Assert.True(plan.IsBlankItemListNoOp);
		Assert.False(plan.ShouldPersistItemStones);
		Assert.False(plan.ReadyForRepositoryWiring);
		Assert.False(plan.IsLive);
		Assert.Empty(plan.Categories);
		Assert.Empty(plan.MissingCriteria);
		Assert.Contains("GenericValidator.isBlankOrNull(items)", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateDisabledPlan_NonBlankItems_EnumeratesJavaSaveOrderAndOrdinals()
	{
		var plan = LegionWarehouseItemStonePersistencePlanService.CreateDisabledPlan(3);

		Assert.Equal(LegionWarehouseItemStonePersistenceStatus.DisabledMissingContracts, plan.Status);
		Assert.False(plan.IsBlankItemListNoOp);
		Assert.True(plan.ShouldPersistItemStones);
		Assert.Equal(
			["MANASTONE", "FUSIONSTONE", "GODSTONE", "IDIANSTONE"],
			plan.Categories.Select(category => category.JavaName).ToArray());
		Assert.Equal([0, 2, 1, 3], plan.Categories.Select(category => category.JavaOrdinal).ToArray());
		Assert.Equal([1, 2, 3, 4], plan.Categories.Select(category => category.JavaSaveOrder).ToArray());
		Assert.Contains(plan.Categories, category => category.JavaName == "GODSTONE" && category.UsesGodstoneProcCount);
		Assert.Contains(plan.Categories, category => category.JavaName == "IDIANSTONE" && category.UsesPolishFields);
		Assert.Contains("MANASTONE, FUSIONSTONE, GODSTONE, then IDIANSTONE", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateDisabledPlan_WithMissingContracts_KeepsRepositoryWiringDisabled()
	{
		var plan = LegionWarehouseItemStonePersistencePlanService.CreateDisabledPlan(
			2,
			new LegionWarehouseItemStonePersistencePrerequisites(
				RepositoryMethodAvailable: true));

		Assert.Equal(LegionWarehouseItemStonePersistenceStatus.DisabledMissingContracts, plan.Status);
		Assert.False(plan.ReadyForRepositoryWiring);
		Assert.False(plan.IsLive);
		Assert.DoesNotContain(LegionWarehouseItemStonePersistenceCriterion.RepositoryMethodAvailable, plan.MissingCriteria);
		Assert.Contains(LegionWarehouseItemStonePersistenceCriterion.SqlInsertUpdateDeleteContractsAvailable, plan.MissingCriteria);
		Assert.Contains(LegionWarehouseItemStonePersistenceCriterion.PersistentStateMutationAvailable, plan.MissingCriteria);
	}

	[Fact]
	public void CreateDisabledPlan_AllPrerequisitesReady_MarksReadyButStaysNonLive()
	{
		var plan = LegionWarehouseItemStonePersistencePlanService.CreateDisabledPlan(
			2,
			new LegionWarehouseItemStonePersistencePrerequisites(
				RepositoryMethodAvailable: true,
				SqlInsertUpdateDeleteContractsAvailable: true,
				PersistentStateMutationAvailable: true));

		Assert.Equal(LegionWarehouseItemStonePersistenceStatus.ReadyForRepositoryWiring, plan.Status);
		Assert.Empty(plan.MissingCriteria);
		Assert.True(plan.ReadyForRepositoryWiring);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldPersistItemStones);
	}
}
