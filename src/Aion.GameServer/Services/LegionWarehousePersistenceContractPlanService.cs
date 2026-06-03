namespace Aion.GameServer.Services;

public static class LegionWarehousePersistenceContractPlanService
{
	public const string FutureRepositoryMethod =
		"Task<bool> SaveLegionWarehouseItemsAsync(IReadOnlyList<InventoryItem> items, int? playerObjectId, int? accountId, int legionId, CancellationToken cancellationToken = default)";

	public static LegionWarehousePersistenceContractPlan CreateDisabledPlan(
		LegionWarehousePersistenceMode mode,
		LegionWarehousePersistenceContractPrerequisites prerequisites = default)
	{
		var missing = new List<LegionWarehousePersistenceContractCriterion>();
		if (!prerequisites.InventoryPersistenceContractAvailable)
			missing.Add(LegionWarehousePersistenceContractCriterion.InventoryPersistenceContractAvailable);
		if (!prerequisites.ItemStonePersistenceContractAvailable)
			missing.Add(LegionWarehousePersistenceContractCriterion.ItemStonePersistenceContractAvailable);
		if (!prerequisites.RepositoryMethodAvailable)
			missing.Add(LegionWarehousePersistenceContractCriterion.RepositoryMethodAvailable);

		var ready = missing.Count == 0;
		var descriptor = CreateJavaCallDescriptor(mode);
		return new LegionWarehousePersistenceContractPlan(
			ready
				? LegionWarehousePersistenceContractStatus.ReadyForRepositoryWiring
				: LegionWarehousePersistenceContractStatus.DisabledMissingContracts,
			mode,
			FutureRepositoryMethod,
			descriptor,
			missing,
			ShouldAddRepositoryMethod: ready,
			DidAddRepositoryMethod: false,
			ReadyForRepositoryWiring: ready,
			IsLive: false,
			"Legion warehouse persistence uses InventoryDAO.store(allItems, playerId, accountId, legionId) followed by ItemStoneListDAO.save(allItems); callers catch Exception and log without propagating.");
	}

	private static LegionWarehouseJavaStoreCallDescriptor CreateJavaCallDescriptor(LegionWarehousePersistenceMode mode)
	{
		return mode switch
		{
			LegionWarehousePersistenceMode.Logout => new LegionWarehouseJavaStoreCallDescriptor(
				"LegionService.LegionWhUpdate(Player)",
				"player.getObjectId()",
				"player.getAccount().getId()",
				"legion.getLegionId()",
				PassesPlayerObjectId: true,
				PassesAccountId: true,
				PassesLegionId: true,
				UsesItemsWithKinah: true,
				UsesDeletedItems: true,
				CallsItemStoneSaveAfterInventoryStore: true,
				CallerSwallowsAndLogsExceptions: true),
			LegionWarehousePersistenceMode.PeriodicSave => new LegionWarehouseJavaStoreCallDescriptor(
				"PeriodicSaveService.LegionWarehouseSaveTask.run",
				"null",
				"null",
				"legion.getLegionId()",
				PassesPlayerObjectId: false,
				PassesAccountId: false,
				PassesLegionId: true,
				UsesItemsWithKinah: true,
				UsesDeletedItems: true,
				CallsItemStoneSaveAfterInventoryStore: true,
				CallerSwallowsAndLogsExceptions: true),
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
		};
	}
}

public readonly record struct LegionWarehousePersistenceContractPrerequisites(
	bool InventoryPersistenceContractAvailable = false,
	bool ItemStonePersistenceContractAvailable = false,
	bool RepositoryMethodAvailable = false);

public sealed record LegionWarehousePersistenceContractPlan(
	LegionWarehousePersistenceContractStatus Status,
	LegionWarehousePersistenceMode Mode,
	string FutureRepositoryMethod,
	LegionWarehouseJavaStoreCallDescriptor JavaStoreCall,
	IReadOnlyList<LegionWarehousePersistenceContractCriterion> MissingCriteria,
	bool ShouldAddRepositoryMethod,
	bool DidAddRepositoryMethod,
	bool ReadyForRepositoryWiring,
	bool IsLive,
	string JavaSource);

public sealed record LegionWarehouseJavaStoreCallDescriptor(
	string JavaCaller,
	string PlayerObjectIdArgument,
	string AccountIdArgument,
	string LegionIdArgument,
	bool PassesPlayerObjectId,
	bool PassesAccountId,
	bool PassesLegionId,
	bool UsesItemsWithKinah,
	bool UsesDeletedItems,
	bool CallsItemStoneSaveAfterInventoryStore,
	bool CallerSwallowsAndLogsExceptions);

public enum LegionWarehousePersistenceMode
{
	Logout,
	PeriodicSave,
}

public enum LegionWarehousePersistenceContractStatus
{
	DisabledMissingContracts,
	ReadyForRepositoryWiring,
}

public enum LegionWarehousePersistenceContractCriterion
{
	InventoryPersistenceContractAvailable,
	ItemStonePersistenceContractAvailable,
	RepositoryMethodAvailable,
}
