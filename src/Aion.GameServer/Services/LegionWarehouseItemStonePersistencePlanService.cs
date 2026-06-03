namespace Aion.GameServer.Services;

public static class LegionWarehouseItemStonePersistencePlanService
{
	public static LegionWarehouseItemStonePersistencePlan CreateDisabledPlan(
		int itemCount,
		LegionWarehouseItemStonePersistencePrerequisites prerequisites = default)
	{
		if (itemCount <= 0)
		{
			return new LegionWarehouseItemStonePersistencePlan(
				LegionWarehouseItemStonePersistenceStatus.SkippedBlankItems,
				itemCount,
				IsBlankItemListNoOp: true,
				ShouldPersistItemStones: false,
				Categories: Array.Empty<LegionWarehouseItemStoneCategoryPlan>(),
				MissingCriteria: Array.Empty<LegionWarehouseItemStonePersistenceCriterion>(),
				ReadyForRepositoryWiring: false,
				IsLive: false,
				"ItemStoneListDAO.save(List<Item>) returns immediately when GenericValidator.isBlankOrNull(items) is true.");
		}

		var missing = new List<LegionWarehouseItemStonePersistenceCriterion>();
		if (!prerequisites.RepositoryMethodAvailable)
			missing.Add(LegionWarehouseItemStonePersistenceCriterion.RepositoryMethodAvailable);
		if (!prerequisites.SqlInsertUpdateDeleteContractsAvailable)
			missing.Add(LegionWarehouseItemStonePersistenceCriterion.SqlInsertUpdateDeleteContractsAvailable);
		if (!prerequisites.PersistentStateMutationAvailable)
			missing.Add(LegionWarehouseItemStonePersistenceCriterion.PersistentStateMutationAvailable);

		var ready = missing.Count == 0;
		return new LegionWarehouseItemStonePersistencePlan(
			ready
				? LegionWarehouseItemStonePersistenceStatus.ReadyForRepositoryWiring
				: LegionWarehouseItemStonePersistenceStatus.DisabledMissingContracts,
			itemCount,
			IsBlankItemListNoOp: false,
			ShouldPersistItemStones: true,
			Categories: CreateCategories(),
			missing,
			ReadyForRepositoryWiring: ready,
			IsLive: false,
			"ItemStoneListDAO.save extracts item stones from each item, stores MANASTONE, FUSIONSTONE, GODSTONE, then IDIANSTONE sets, and marks every provided stone UPDATED after store attempts.");
	}

	private static IReadOnlyList<LegionWarehouseItemStoneCategoryPlan> CreateCategories()
	{
		return
		[
			new LegionWarehouseItemStoneCategoryPlan(
				"MANASTONE",
				JavaOrdinal: 0,
				JavaSaveOrder: 1,
				"item.getItemStones()",
				UsesPolishFields: false,
				UsesGodstoneProcCount: false),
			new LegionWarehouseItemStoneCategoryPlan(
				"FUSIONSTONE",
				JavaOrdinal: 2,
				JavaSaveOrder: 2,
				"item.getFusionStones()",
				UsesPolishFields: false,
				UsesGodstoneProcCount: false),
			new LegionWarehouseItemStoneCategoryPlan(
				"GODSTONE",
				JavaOrdinal: 1,
				JavaSaveOrder: 3,
				"item.getGodStone()",
				UsesPolishFields: false,
				UsesGodstoneProcCount: true),
			new LegionWarehouseItemStoneCategoryPlan(
				"IDIANSTONE",
				JavaOrdinal: 3,
				JavaSaveOrder: 4,
				"item.getIdianStone()",
				UsesPolishFields: true,
				UsesGodstoneProcCount: false),
		];
	}
}

public readonly record struct LegionWarehouseItemStonePersistencePrerequisites(
	bool RepositoryMethodAvailable = false,
	bool SqlInsertUpdateDeleteContractsAvailable = false,
	bool PersistentStateMutationAvailable = false);

public sealed record LegionWarehouseItemStonePersistencePlan(
	LegionWarehouseItemStonePersistenceStatus Status,
	int ItemCount,
	bool IsBlankItemListNoOp,
	bool ShouldPersistItemStones,
	IReadOnlyList<LegionWarehouseItemStoneCategoryPlan> Categories,
	IReadOnlyList<LegionWarehouseItemStonePersistenceCriterion> MissingCriteria,
	bool ReadyForRepositoryWiring,
	bool IsLive,
	string JavaSource);

public sealed record LegionWarehouseItemStoneCategoryPlan(
	string JavaName,
	int JavaOrdinal,
	int JavaSaveOrder,
	string JavaCollectionSource,
	bool UsesPolishFields,
	bool UsesGodstoneProcCount);

public enum LegionWarehouseItemStonePersistenceStatus
{
	SkippedBlankItems,
	DisabledMissingContracts,
	ReadyForRepositoryWiring,
}

public enum LegionWarehouseItemStonePersistenceCriterion
{
	RepositoryMethodAvailable,
	SqlInsertUpdateDeleteContractsAvailable,
	PersistentStateMutationAvailable,
}
