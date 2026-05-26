using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahPersistenceOperationPlanServiceTests
{
	[Fact]
	public void CreatePlan_NotEnoughKinahDoesNotCreateSql()
	{
		var mutationPlan = CreateMutationPlan(currentKinah: 500, requiredPrice: 1_000);

		var plan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);

		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.NotEnoughKinah, plan.Status);
		Assert.False(plan.ShouldExecuteSql);
		Assert.Null(plan.Sql);
		Assert.Empty(plan.Parameters);
		Assert.False(plan.ShouldDeleteWhenZero);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NonPositivePriceDoesNotCreateSql()
	{
		var mutationPlan = CreateMutationPlan(currentKinah: 500, requiredPrice: 0);

		var plan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);

		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.NoMutationRequired, plan.Status);
		Assert.False(plan.ShouldExecuteSql);
		Assert.Null(plan.Sql);
		Assert.Empty(plan.Parameters);
		Assert.False(plan.ShouldDeleteWhenZero);
	}

	[Theory]
	[InlineData(1_000, 1_000, 0)]
	[InlineData(1_500, 1_000, 500)]
	public void CreatePlan_DecrementReadyCreatesOwnerCheckedCountUpdate(
		long currentKinah,
		long requiredPrice,
		long expectedRemaining)
	{
		var mutationPlan = CreateMutationPlan(currentKinah, requiredPrice);

		var plan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(mutationPlan);

		Assert.Equal(BindPointTeleportKinahPersistenceOperationStatus.UpdateReady, plan.Status);
		Assert.True(plan.ShouldExecuteSql);
		Assert.Equal(BindPointTeleportKinahPersistenceOperationPlanService.OwnerCheckedCountUpdateSql, plan.Sql);
		Assert.Equal(PlayerObjectId, plan.PlayerObjectId);
		Assert.Equal(KinahObjectId, plan.KinahObjectId);
		Assert.Equal(expectedRemaining, plan.KinahCount);
		Assert.False(plan.ShouldDeleteWhenZero);
		Assert.Collection(
			plan.Parameters,
			parameter =>
			{
				Assert.Equal("item_count", parameter.Name);
				Assert.Equal(expectedRemaining, parameter.Value);
			},
			parameter =>
			{
				Assert.Equal("item_unique_id", parameter.Name);
				Assert.Equal(KinahObjectId, parameter.Value);
			},
			parameter =>
			{
				Assert.Equal("item_owner", parameter.Name);
				Assert.Equal(PlayerObjectId, parameter.Value);
			});
	}

	[Fact]
	public void CreateResult_OneAffectedRowSavesWithoutRollback()
	{
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(
			CreateMutationPlan(currentKinah: 1_500, requiredPrice: 1_000));

		var result = BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(
			operationPlan,
			affectedRows: 1);

		Assert.Equal(BindPointTeleportKinahPersistenceStatus.Saved, result.Status);
		Assert.Equal(PlayerObjectId, result.PlayerObjectId);
		Assert.Equal(KinahObjectId, result.KinahObjectId);
		Assert.Equal(500, result.KinahCount);
		Assert.False(result.ShouldRollbackInMemoryMutation);
		Assert.False(result.IsLive);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void CreateResult_NonSingleAffectedRowsRequireMissingRowRollback(int affectedRows)
	{
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(
			CreateMutationPlan(currentKinah: 1_500, requiredPrice: 1_000));

		var result = BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(
			operationPlan,
			affectedRows);

		Assert.Equal(BindPointTeleportKinahPersistenceStatus.MissingRow, result.Status);
		Assert.True(result.ShouldRollbackInMemoryMutation);
	}

	[Fact]
	public void CreateResult_ExceptionRequiresFailedRollback()
	{
		var operationPlan = BindPointTeleportKinahPersistenceOperationPlanService.CreatePlan(
			CreateMutationPlan(currentKinah: 1_500, requiredPrice: 1_000));

		var result = BindPointTeleportKinahPersistenceOperationPlanService.CreateResult(
			operationPlan,
			affectedRows: 0,
			exception: new InvalidOperationException("simulated write failure"));

		Assert.Equal(BindPointTeleportKinahPersistenceStatus.Failed, result.Status);
		Assert.True(result.ShouldRollbackInMemoryMutation);
	}

	private const int PlayerObjectId = 7001;
	private const int KinahObjectId = 1824;

	private static BindPointTeleportScheduledKinahMutationPlan CreateMutationPlan(
		long currentKinah,
		long requiredPrice)
	{
		return BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = PlayerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = KinahObjectId,
						OwnerId = PlayerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
	}
}
