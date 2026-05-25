using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationApService
{
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;

	public static ItemPurificationApPlan CreatePurificationApPlan(
		Player? player,
		InventoryItem? baseItem,
		ItemPurificationResultProjection? purificationResult,
		bool materialsAlreadyDecreased,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/item/ItemPurificationService.isPurificationAllowed runs before
		// decreaseMaterials; decreaseMaterials spends AP only after required materials decrease succeeds.
		var validation = ValidatePurificationAllowed(player, baseItem, purificationResult);
		if (validation.Status != ItemPurificationApStatus.Allowed)
			return ItemPurificationApPlan.FromValidation(validation);

		if (!materialsAlreadyDecreased)
			return ItemPurificationApPlan.FromValidation(validation with
			{
				Status = ItemPurificationApStatus.AllowedPendingMaterialDecrease,
			});

		if (purificationResult!.NecessaryAbyssPoints <= 0)
			return ItemPurificationApPlan.FromValidation(validation with
			{
				Status = ItemPurificationApStatus.AppliedNoAbyssPointsRequired,
			});

		var plan = AbyssPointsService.AddAp(player!, -purificationResult.NecessaryAbyssPoints, abyssPointsOptions);
		return new ItemPurificationApPlan(
			ItemPurificationApStatus.Applied,
			purificationResult.NecessaryAbyssPoints,
			purificationResult.NecessaryKinah,
			Array.Empty<ItemPurificationMaterialRequirement>(),
			plan);
	}

	public static ItemPurificationApPlan ValidatePurificationAllowed(
		Player? player,
		InventoryItem? baseItem,
		ItemPurificationResultProjection? purificationResult)
	{
		// Java parity: ItemPurificationService.isPurificationAllowed checks identified, min enchant,
		// AP, Kinah, and required materials before the later material decrease/AP spend step.
		if (player == null)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.MissingPlayer);
		if (baseItem == null)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.MissingBaseItem);
		if (purificationResult == null)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.InvalidResultItem);
		if (!baseItem.IsIdentified)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.NotIdentified, purificationResult);
		if (baseItem.Enchant < purificationResult.MinEnchantCount)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.EnchantTooLow, purificationResult);
		if (player.AbyssRank.Ap < purificationResult.NecessaryAbyssPoints)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.NotEnoughAbyssPoints, purificationResult);
		if (GetInventoryCount(player.InventoryItems, KinahItemId) < purificationResult.NecessaryKinah)
			return ItemPurificationApPlan.Failed(ItemPurificationApStatus.NotEnoughKinah, purificationResult);

		var missingMaterials = purificationResult.RequiredMaterials
			.Where(material => GetInventoryCount(player.InventoryItems, material.ItemId) < material.ItemCount)
			.ToArray();
		if (missingMaterials.Length > 0)
		{
			return new ItemPurificationApPlan(
				ItemPurificationApStatus.MissingRequiredMaterial,
				purificationResult.NecessaryAbyssPoints,
				purificationResult.NecessaryKinah,
				missingMaterials,
				AbyssPointsPlan: null);
		}

		return new ItemPurificationApPlan(
			ItemPurificationApStatus.Allowed,
			purificationResult.NecessaryAbyssPoints,
			purificationResult.NecessaryKinah,
			Array.Empty<ItemPurificationMaterialRequirement>(),
			AbyssPointsPlan: null);
	}

	private static long GetInventoryCount(IReadOnlyList<InventoryItem> items, int itemId)
	{
		// Java Storage.getItemCountByItemId is represented with current in-memory inventory projections.
		// Kinah is cube-bound in existing C# inventory services.
		return items
			.Where(item => item.ItemId == itemId && (itemId != KinahItemId || item.Location == CubeStorageId))
			.Sum(item => item.Count);
	}
}

public sealed record ItemPurificationResultProjection(
	int ResultItemId,
	int MinEnchantCount,
	int NecessaryAbyssPoints,
	long NecessaryKinah,
	IReadOnlyList<ItemPurificationMaterialRequirement> RequiredMaterials);

public sealed record ItemPurificationMaterialRequirement(int ItemId, long ItemCount);

public sealed record ItemPurificationApPlan(
	ItemPurificationApStatus Status,
	int NecessaryAbyssPoints,
	long NecessaryKinah,
	IReadOnlyList<ItemPurificationMaterialRequirement> MissingMaterials,
	AbyssPointsAddPlan? AbyssPointsPlan)
{
	public bool Allowed => Status is ItemPurificationApStatus.Allowed
		or ItemPurificationApStatus.AllowedPendingMaterialDecrease
		or ItemPurificationApStatus.Applied
		or ItemPurificationApStatus.AppliedNoAbyssPointsRequired;

	public static ItemPurificationApPlan Failed(
		ItemPurificationApStatus status,
		ItemPurificationResultProjection? purificationResult = null)
	{
		return new ItemPurificationApPlan(
			status,
			purificationResult?.NecessaryAbyssPoints ?? 0,
			purificationResult?.NecessaryKinah ?? 0,
			Array.Empty<ItemPurificationMaterialRequirement>(),
			AbyssPointsPlan: null);
	}

	public static ItemPurificationApPlan FromValidation(ItemPurificationApPlan validation)
	{
		return new ItemPurificationApPlan(
			validation.Status,
			validation.NecessaryAbyssPoints,
			validation.NecessaryKinah,
			validation.MissingMaterials,
			AbyssPointsPlan: null);
	}
}

public enum ItemPurificationApStatus
{
	Allowed,
	AllowedPendingMaterialDecrease,
	Applied,
	AppliedNoAbyssPointsRequired,
	MissingPlayer,
	MissingBaseItem,
	InvalidResultItem,
	NotIdentified,
	EnchantTooLow,
	NotEnoughAbyssPoints,
	NotEnoughKinah,
	MissingRequiredMaterial,
}
