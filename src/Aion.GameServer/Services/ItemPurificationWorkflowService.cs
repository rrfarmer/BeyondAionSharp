using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationWorkflowService
{
	public static ItemPurificationWorkflowPlan CreateWorkflowPlan(
		Player? player,
		InventoryItem? baseItem,
		ItemPurificationTable itemPurifications,
		ItemTemplateTable itemTemplates,
		int resultItemId,
		int targetObjectId,
		int? rerolledRandomBonusId = null,
		ItemRandomBonusTable? itemRandomBonuses = null,
		Func<double>? randomBonusRoll = null)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_PURIFICATION validates the selected result
		// through ItemPurificationService, then decreases materials, then calls upgradeItem.
		if (baseItem == null)
			return ItemPurificationWorkflowPlan.Failed(ItemPurificationWorkflowStatus.MissingBaseItem);

		var purificationTemplate = itemPurifications.GetItemPurificationTemplate(baseItem.ItemId);
		if (purificationTemplate == null)
			return ItemPurificationWorkflowPlan.Failed(ItemPurificationWorkflowStatus.MissingTemplate);

		var result = itemPurifications.GetResultItem(baseItem.ItemId, resultItemId);
		if (result == null)
			return ItemPurificationWorkflowPlan.Failed(ItemPurificationWorkflowStatus.InvalidResultItem);

		var projection = ToProjection(result);
		var validation = ItemPurificationApService.ValidatePurificationAllowed(player, baseItem, projection);
		if (validation.Status != ItemPurificationApStatus.Allowed)
			return new ItemPurificationWorkflowPlan(
				ItemPurificationWorkflowStatus.ValidationFailed,
				validation,
				MaterialMutation: null,
				Inheritance: null);

		var materialMutation = ItemPurificationMaterialMutationService.CreateDecreaseMaterialsPlan(
			player,
			baseItem,
			projection.RequiredMaterials,
			projection.NecessaryAbyssPoints,
			projection.NecessaryKinah);
		if (!materialMutation.Succeeded)
		{
			return new ItemPurificationWorkflowPlan(
				ItemPurificationWorkflowStatus.MaterialMutationFailed,
				validation,
				materialMutation,
				Inheritance: null);
		}

		var sourceTemplate = itemTemplates.GetItemTemplate(baseItem.ItemId);
		var targetTemplate = itemTemplates.GetItemTemplate(result.ResultItemId);
		var inheritance = ItemPurificationInheritanceService.CreateTargetItemPlan(
			baseItem,
			sourceTemplate,
			targetTemplate,
			targetObjectId,
			rerolledRandomBonusId,
			itemRandomBonuses,
			randomBonusRoll);
		if (!inheritance.Succeeded)
		{
			return new ItemPurificationWorkflowPlan(
				ItemPurificationWorkflowStatus.TargetInheritanceFailed,
				validation,
				materialMutation,
				inheritance);
		}

		return new ItemPurificationWorkflowPlan(
			ItemPurificationWorkflowStatus.Planned,
			validation,
			materialMutation,
			inheritance);
	}

	private static ItemPurificationResultProjection ToProjection(ItemPurificationResultSummary result)
	{
		return new ItemPurificationResultProjection(
			result.ResultItemId,
			result.MinEnchantCount,
			result.NecessaryAbyssPoints,
			result.NecessaryKinah,
			result.RequiredMaterials
				.Select(material => new ItemPurificationMaterialRequirement(material.ItemId, material.ItemCount))
				.ToArray());
	}
}

public sealed record ItemPurificationWorkflowPlan(
	ItemPurificationWorkflowStatus Status,
	ItemPurificationApPlan? Validation,
	ItemPurificationMaterialMutationPlan? MaterialMutation,
	ItemPurificationInheritancePlan? Inheritance)
{
	public bool Succeeded => Status == ItemPurificationWorkflowStatus.Planned;

	public static ItemPurificationWorkflowPlan Failed(ItemPurificationWorkflowStatus status)
	{
		return new ItemPurificationWorkflowPlan(
			status,
			Validation: null,
			MaterialMutation: null,
			Inheritance: null);
	}
}

public enum ItemPurificationWorkflowStatus
{
	Planned,
	MissingBaseItem,
	MissingTemplate,
	InvalidResultItem,
	ValidationFailed,
	MaterialMutationFailed,
	TargetInheritanceFailed,
}
