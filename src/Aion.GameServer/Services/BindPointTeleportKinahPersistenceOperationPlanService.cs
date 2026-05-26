namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahPersistenceOperationStatus
{
	NotEnoughKinah,
	NoMutationRequired,
	UpdateReady,
}

public sealed record BindPointTeleportKinahPersistenceOperationParameter(
	string Name,
	object Value);

public sealed record BindPointTeleportKinahPersistenceOperationPlan(
	BindPointTeleportKinahPersistenceOperationStatus Status,
	BindPointTeleportScheduledKinahMutationPlan MutationPlan,
	string? Sql,
	IReadOnlyList<BindPointTeleportKinahPersistenceOperationParameter> Parameters,
	int? PlayerObjectId,
	int? KinahObjectId,
	long? KinahCount,
	bool ShouldExecuteSql,
	bool ShouldDeleteWhenZero,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahPersistenceOperationPlanService
{
	public const string OwnerCheckedCountUpdateSql =
		"UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?";

	public static BindPointTeleportKinahPersistenceOperationPlan CreatePlan(
		BindPointTeleportScheduledKinahMutationPlan mutationPlan)
	{
		// Java parity: InventoryDAO.store(player) eventually writes dirty Kinah item rows after
		// Storage.decreaseItemCount. This C# contract narrows the future live write to an owner-checked
		// count update and never deletes zero-count Kinah.
		if (mutationPlan.Status == BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah)
		{
			return NoSql(
				BindPointTeleportKinahPersistenceOperationStatus.NotEnoughKinah,
				mutationPlan,
				"BindPointTeleportService scheduled callback stops after failed tryDecreaseKinah; no Kinah persistence update is created");
		}

		if (!mutationPlan.ShouldEmitInventoryUpdatePacket || mutationPlan.KinahItemUpdate == null)
		{
			return NoSql(
				BindPointTeleportKinahPersistenceOperationStatus.NoMutationRequired,
				mutationPlan,
				"Storage.decreaseKinah amount > 0 guard means no Kinah row update is required for this staged callback");
		}

		var kinahItem = mutationPlan.KinahItemUpdate;
		return new BindPointTeleportKinahPersistenceOperationPlan(
			BindPointTeleportKinahPersistenceOperationStatus.UpdateReady,
			mutationPlan,
			OwnerCheckedCountUpdateSql,
			[
				new BindPointTeleportKinahPersistenceOperationParameter("item_count", kinahItem.Count),
				new BindPointTeleportKinahPersistenceOperationParameter("item_unique_id", kinahItem.ObjectId),
				new BindPointTeleportKinahPersistenceOperationParameter("item_owner", kinahItem.OwnerId),
			],
			kinahItem.OwnerId,
			kinahItem.ObjectId,
			kinahItem.Count,
			ShouldExecuteSql: true,
			ShouldDeleteWhenZero: false,
			"InventoryDAO.updateItems dirty Kinah persistence modeled as owner-checked C# count update: UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?",
			IsLive: false);
	}

	public static BindPointTeleportKinahPersistenceResult CreateResult(
		BindPointTeleportKinahPersistenceOperationPlan operationPlan,
		int affectedRows,
		Exception? exception = null)
	{
		if (!operationPlan.ShouldExecuteSql)
		{
			return new BindPointTeleportKinahPersistenceResult(
				BindPointTeleportKinahPersistenceStatus.Saved,
				operationPlan.PlayerObjectId ?? 0,
				operationPlan.KinahObjectId ?? 0,
				operationPlan.KinahCount ?? operationPlan.MutationPlan.CurrentKinah,
				ShouldRollbackInMemoryMutation: false,
				"Scheduled bind-point Kinah persistence operation had no SQL work; C# treats the no-mutation branch as already satisfied",
				IsLive: false);
		}

		var status = exception != null
			? BindPointTeleportKinahPersistenceStatus.Failed
			: affectedRows == 1
				? BindPointTeleportKinahPersistenceStatus.Saved
				: BindPointTeleportKinahPersistenceStatus.MissingRow;

		return new BindPointTeleportKinahPersistenceResult(
			status,
			operationPlan.PlayerObjectId!.Value,
			operationPlan.KinahObjectId!.Value,
			operationPlan.KinahCount!.Value,
			ShouldRollbackInMemoryMutation: status != BindPointTeleportKinahPersistenceStatus.Saved,
			status switch
			{
				BindPointTeleportKinahPersistenceStatus.Saved =>
					"Owner-checked scheduled bind-point Kinah count update affected exactly one row",
				BindPointTeleportKinahPersistenceStatus.MissingRow =>
					"Owner-checked scheduled bind-point Kinah count update affected no rows; rollback is required before packet send",
				_ =>
					"Owner-checked scheduled bind-point Kinah count update failed; rollback is required before packet send",
			},
			IsLive: false);
	}

	private static BindPointTeleportKinahPersistenceOperationPlan NoSql(
		BindPointTeleportKinahPersistenceOperationStatus status,
		BindPointTeleportScheduledKinahMutationPlan mutationPlan,
		string javaSource)
	{
		return new BindPointTeleportKinahPersistenceOperationPlan(
			status,
			mutationPlan,
			Sql: null,
			Array.Empty<BindPointTeleportKinahPersistenceOperationParameter>(),
			PlayerObjectId: null,
			KinahObjectId: null,
			KinahCount: null,
			ShouldExecuteSql: false,
			ShouldDeleteWhenZero: false,
			javaSource,
			IsLive: false);
	}
}
