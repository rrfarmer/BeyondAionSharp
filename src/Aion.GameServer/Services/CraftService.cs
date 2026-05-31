using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class CraftService
{
	private readonly WorldNpcResourceStatsService _resourceStats;
	private const int CubeStorageId = 0;
	private const int KinahItemId = 182400001;
	private readonly ItemTemplateTable? _itemTemplates;
	private readonly SkillTemplateTable? _skillTemplates;

	public CraftService(
		WorldNpcResourceStatsService resourceStats,
		ItemTemplateTable? itemTemplates = null,
		SkillTemplateTable? skillTemplates = null)
	{
		_resourceStats = resourceStats;
		_itemTemplates = itemTemplates;
		_skillTemplates = skillTemplates;
	}

	public async ValueTask<CraftStartDpCostResult> SpendRecipeDpForCraftStartAsync(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		int? maxDp = null)
	{
		// Java parity: services/craft/CraftService.checkCraft + startCrafting recipe DP branch.
		if (player == null)
			return CraftStartDpCostResult.MissingPlayer(recipeTemplate?.RecipeId ?? 0);
		if (recipeTemplate == null)
			return CraftStartDpCostResult.MissingRecipe(player.ObjectId, player.Dp);

		var requiredDp = recipeTemplate.Dp;
		if (player.Dp < requiredDp)
			return CraftStartDpCostResult.NotEnoughDp(player.ObjectId, recipeTemplate.RecipeId, requiredDp, player.Dp);

		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, -requiredDp, maxDp);
		return CraftStartDpCostResult.FromDpChange(change, recipeTemplate.RecipeId, requiredDp, previousDp);
	}

	public CraftStartValidationPlan CreateStartCraftingValidationPlan(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		ItemTemplateSummary? productTemplate,
		IWorldNpcObject? target,
		bool targetIsStaticObject,
		bool targetIsWithinToolRange,
		bool hasCraftingTaskInProgress,
		IReadOnlyDictionary<int, long>? selectedMaterialData = null,
		int craftType = 0)
	{
		// Java parity: services/craft/CraftService.startCrafting + early checkCraft guards.
		if (player == null)
			return CraftStartValidationPlan.MissingPlayer(recipeTemplate?.RecipeId ?? 0);
		if (recipeTemplate == null)
			return CraftStartValidationPlan.MissingRecipe(player.ObjectId);
		if (productTemplate == null)
			return CraftStartValidationPlan.MissingProductTemplate(player.ObjectId, recipeTemplate);
		if (hasCraftingTaskInProgress)
			return CraftStartValidationPlan.AlreadyCrafting(player.ObjectId, recipeTemplate, productTemplate);

		var isMorphRecipe = recipeTemplate.SkillId == CraftStartValidationPlan.MorphSubstancesSkillId;
		if (!isMorphRecipe)
		{
			if (target == null || !targetIsStaticObject)
				return CraftStartValidationPlan.InvalidNonMorphTarget(player.ObjectId, recipeTemplate, productTemplate);
			if (!targetIsWithinToolRange)
				return CraftStartValidationPlan.TooFarFromTool(player.ObjectId, recipeTemplate, productTemplate, target.TemplateId);
		}

		if (recipeTemplate.Dp > 0 && player.Dp < recipeTemplate.Dp)
			return CraftStartValidationPlan.NotEnoughDp(player.ObjectId, player.Dp, recipeTemplate, productTemplate, target?.ObjectId ?? 0);

		if (player.IsInRideMode || player.IsInAnyHide())
			return CraftStartValidationPlan.InvalidCurrentStance(player.ObjectId, player.Dp, recipeTemplate, productTemplate, target?.ObjectId ?? 0);

		if (InventoryCapacity.GetFreeCubeSlots(player) <= 0)
			return CraftStartValidationPlan.InventoryFull(player.ObjectId, player.Dp, recipeTemplate, productTemplate, target?.ObjectId ?? 0);

		if (!player.Recipes.Contains(recipeTemplate.RecipeId))
			return CraftStartValidationPlan.MissingKnownRecipe(player.ObjectId, player.Dp, recipeTemplate, productTemplate, target?.ObjectId ?? 0);

		if (recipeTemplate.CraftDelayId.HasValue && player.CraftCooldowns.ContainsKey(recipeTemplate.CraftDelayId.Value))
			return CraftStartValidationPlan.CraftCooldownActive(player.ObjectId, player.Dp, recipeTemplate, productTemplate, target?.ObjectId ?? 0);

		var playerSkill = player.Skills.FirstOrDefault(skill => skill.SkillId == recipeTemplate.SkillId);
		var skillName = GetSkillClientName(recipeTemplate.SkillId);
		if (playerSkill == null)
			return CraftStartValidationPlan.MissingCraftSkill(
				player.ObjectId,
				player.Dp,
				recipeTemplate,
				productTemplate,
				target?.ObjectId ?? 0,
				skillName);

		if (playerSkill.SkillLevel < recipeTemplate.SkillPoint)
			return CraftStartValidationPlan.CraftSkillTooLow(
				player.ObjectId,
				player.Dp,
				playerSkill.SkillLevel,
				recipeTemplate,
				productTemplate,
				target?.ObjectId ?? 0,
				skillName);

		var missingComponent = GetMissingComponent(player, recipeTemplate, selectedMaterialData ?? new Dictionary<int, long>());
		if (missingComponent != null)
		{
			var itemName = GetItemClientName(missingComponent.ItemId);
			return CraftStartValidationPlan.MissingComponentItem(
				player.ObjectId,
				player.Dp,
				playerSkill.SkillLevel,
				recipeTemplate,
				productTemplate,
				target?.ObjectId ?? 0,
				missingComponent.ItemId,
				missingComponent.RequiredQuantity,
				missingComponent.AvailableCount,
				itemName);
		}

		var bonusItemId = GetBonusRequiredItemId(recipeTemplate.SkillId);
		if (craftType == 1 && GetCubeItemCountByItemId(player.InventoryItems, bonusItemId) < 1)
		{
			var itemName = GetItemClientName(bonusItemId);
			return CraftStartValidationPlan.MissingBonusItem(
				player.ObjectId,
				player.Dp,
				playerSkill.SkillLevel,
				recipeTemplate,
				productTemplate,
				target?.ObjectId ?? 0,
				bonusItemId,
				itemName);
		}

		return CraftStartValidationPlan.ReadyForNextValidation(
			player.ObjectId,
			player.Dp,
			playerSkill.SkillLevel,
			recipeTemplate,
			productTemplate,
			target?.ObjectId ?? 0,
			isMorphRecipe);
	}

	public CraftStartCancelPacketPlan CreateStartCancelPacketPlan(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		ItemTemplateSummary? productTemplate,
		int targetObjectId)
	{
		// Java parity: services/craft/CraftService.sendCancelCraft.
		if (player == null)
			return CraftStartCancelPacketPlan.NotPlanned("CraftService.sendCancelCraft requires player object id");
		if (recipeTemplate == null)
			return CraftStartCancelPacketPlan.NotPlanned("CraftService.sendCancelCraft requires skill id from recipe template");
		if (productTemplate == null)
			return CraftStartCancelPacketPlan.NotPlanned("CraftService.sendCancelCraft requires item template for SM_CRAFT_UPDATE");

		return CraftStartCancelPacketPlan.Planned(
			new SmCraftUpdate(
				recipeTemplate.SkillId,
				productTemplate,
				success: 0,
				failure: 0,
				CraftingTaskPacketPlanService.CancelAction,
				executionSpeed: 0,
				delay: 0),
			new SmCraftAnimation(
				player.ObjectId,
				targetObjectId,
				skillId: 0,
				CraftingTaskPacketPlanService.AnimationCompleteAction));
	}

	public CraftStartFailureOrchestrationPlan CreateStartFailureOrchestrationPlan(
		CraftStartValidationPlan? validationPlan,
		CraftStartCancelPacketPlan? cancelPlan)
	{
		// Java parity: CraftService.startCrafting -> checkCraft may send a system message,
		// then startCrafting calls sendCancelCraft when checkCraft returns false.
		if (validationPlan == null)
			return CraftStartFailureOrchestrationPlan.NotPlanned("CraftService.startCrafting failure orchestration requires a validation plan");
		if (validationPlan.IsReadyForNextValidation)
			return CraftStartFailureOrchestrationPlan.NotPlanned("CraftService.startCrafting -> checkCraft returned true; no failure packets planned");
		if (!validationPlan.ShouldSendCancelCraft)
			return CraftStartFailureOrchestrationPlan.NotPlanned("CraftService.startCrafting failure did not request sendCancelCraft");

		var orderedPackets = new List<GameServerPacket>();
		if (validationPlan.FailurePacket != null)
			orderedPackets.Add(validationPlan.FailurePacket);

		if (cancelPlan?.Status == CraftStartCancelPacketPlanStatus.Planned)
		{
			if (cancelPlan.SelfPacket != null)
				orderedPackets.Add(cancelPlan.SelfPacket);
			if (cancelPlan.BroadcastPacket != null)
				orderedPackets.Add(cancelPlan.BroadcastPacket);

			return CraftStartFailureOrchestrationPlan.Planned(
				validationPlan,
				cancelPlan,
				orderedPackets);
		}

		return CraftStartFailureOrchestrationPlan.CancelNotPlanned(
			validationPlan,
			cancelPlan,
			orderedPackets);
	}

	public CraftStartConsumptionPlan CreateStartConsumptionPlan(
		CraftStartValidationPlan? validationPlan,
		RecipeTemplateSummary? recipeTemplate,
		IReadOnlyDictionary<int, long>? selectedMaterialData = null,
		int craftType = 0)
	{
		// Java parity: CraftService.checkCraft successful tail consumes the bonus item first,
		// then decreases each component in the selected components_data group.
		if (validationPlan == null)
			return CraftStartConsumptionPlan.NotPlanned("CraftService.checkCraft consumption planning requires validation evidence");
		if (!validationPlan.IsReadyForNextValidation)
			return CraftStartConsumptionPlan.NotPlanned("CraftService.checkCraft returned false; consumption is not planned");
		if (recipeTemplate == null)
			return CraftStartConsumptionPlan.NotPlanned("CraftService.checkCraft consumption planning requires recipe template");

		var decreases = new List<CraftStartConsumedItemPlan>();
		if (craftType == 1)
			decreases.Add(CraftStartConsumedItemPlan.Bonus(GetBonusRequiredItemId(recipeTemplate.SkillId), quantity: 1));

		var selectedComponents = GetSelectedComponentGroup(recipeTemplate, selectedMaterialData ?? new Dictionary<int, long>());
		if (selectedComponents != null)
		{
			foreach (var component in selectedComponents.Components)
				decreases.Add(CraftStartConsumedItemPlan.Component(component.ItemId, component.Quantity));
		}

		return CraftStartConsumptionPlan.Planned(validationPlan, recipeTemplate.RecipeId, decreases);
	}

	public CraftStartTaskPlan CreateStartTaskPlan(
		CraftStartValidationPlan? validationPlan,
		ItemTemplateSummary? productTemplate,
		int craftType = 0)
	{
		// Java parity: CraftService.startCrafting computes skillLvlDiff, CraftingTask bonus,
		// quality-based interval cap, morph interval, and non-morph interval before task start.
		if (validationPlan == null)
			return CraftStartTaskPlan.NotPlanned("CraftService.startCrafting task planning requires validation evidence");
		if (!validationPlan.IsReadyForNextValidation)
			return CraftStartTaskPlan.NotPlanned("CraftService.startCrafting task is not planned when checkCraft returned false");
		if (productTemplate == null)
			return CraftStartTaskPlan.NotPlanned("CraftService.startCrafting task planning requires item template");

		var intervalCap = GetCraftIntervalCap(productTemplate.Quality);
		var skillLevelDiff = validationPlan.CurrentSkillLevel - validationPlan.RequiredSkillPoint;
		var interval = validationPlan.SkillId == CraftStartValidationPlan.MorphSubstancesSkillId
			? 200
			: Math.Max(intervalCap, 2500 - (skillLevelDiff * 60));
		var bonusCritModifier = craftType == 1 ? 15 : 0;

		return CraftStartTaskPlan.Planned(
			validationPlan,
			productTemplate.TemplateId,
			productTemplate.Quality,
			skillLevelDiff,
			intervalCap,
			interval,
			bonusCritModifier);
	}

	public CraftStartInventoryMutationPlan CreateStartInventoryMutationPlan(
		CraftStartConsumptionPlan? consumptionPlan,
		IReadOnlyList<InventoryItem>? inventoryItems)
	{
		// Java parity: Storage.decreaseByItemId walks matching item stacks, decreases each
		// with ItemUpdateType.DEC_ITEM_USE, and deletes non-kinah stacks that reach zero.
		if (consumptionPlan == null)
			return CraftStartInventoryMutationPlan.NotPlanned("CraftService.checkCraft inventory mutation planning requires consumption evidence");
		if (!consumptionPlan.IsPlanned)
			return CraftStartInventoryMutationPlan.NotPlanned("CraftService.checkCraft consumption was not planned");
		if (inventoryItems == null)
			return CraftStartInventoryMutationPlan.MissingInventory(consumptionPlan);

		var workingItems = inventoryItems
			.Select(item => new CraftInventoryWorkingItem(item, item.Count))
			.ToList();
		var updatedItems = new List<InventoryItem>();
		var deletedObjectIds = new List<int>();
		var operations = new List<CraftStartInventoryMutationOperation>();

		foreach (var decrease in consumptionPlan.Decreases)
		{
			var remaining = decrease.Quantity;
			foreach (var item in workingItems.Where(item =>
				item.Item.ItemId == decrease.ItemId
				&& item.Item.Location == CubeStorageId
				&& !item.Item.IsEquipped
				&& item.Count > 0))
			{
				if (remaining <= 0)
					break;

				var removed = Math.Min(remaining, item.Count);
				remaining -= removed;
				item.Count -= removed;
				if (item.Count <= 0)
				{
					deletedObjectIds.Add(item.Item.ObjectId);
					operations.Add(CraftStartInventoryMutationOperation.Deleted(decrease, item.Item));
				}
				else
				{
					var updatedItem = CopyInventoryItem(item.Item, item.Count);
					updatedItems.Add(updatedItem);
					operations.Add(CraftStartInventoryMutationOperation.Updated(decrease, updatedItem));
				}
			}

			if (remaining > 0)
			{
				return CraftStartInventoryMutationPlan.InsufficientInventory(
					consumptionPlan,
					decrease,
					decrease.Quantity - remaining,
					updatedItems,
					deletedObjectIds,
					operations);
			}
		}

		return CraftStartInventoryMutationPlan.Planned(consumptionPlan, updatedItems, deletedObjectIds, operations);
	}

	public CraftStartInventoryPacketPlan CreateStartInventoryPacketPlan(
		CraftStartInventoryMutationPlan? mutationPlan,
		Player? player = null,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: Storage.decreaseItemCount sends SM_INVENTORY_UPDATE_ITEM with
		// ItemUpdateType.DEC_ITEM_USE for remaining stacks, or SM_DELETE_ITEM with
		// ItemDeleteType.USE followed by SM_CUBE_UPDATE for non-kinah stacks deleted at zero.
		if (mutationPlan == null)
			return CraftStartInventoryPacketPlan.NotPlanned("CraftService.checkCraft packet planning requires inventory mutation evidence");
		if (!mutationPlan.IsPlanned)
			return CraftStartInventoryPacketPlan.NotPlanned("CraftService.checkCraft inventory mutation was not planned");
		if (_itemTemplates == null)
			return CraftStartInventoryPacketPlan.MissingItemTemplates(mutationPlan);
		if (mutationPlan.OrderedOperations.Any(operation => operation.Kind == CraftStartInventoryMutationOperationKind.Deleted) && player == null)
			return CraftStartInventoryPacketPlan.MissingCubeSizeSnapshot(mutationPlan, packets: Array.Empty<GameServerPacket>());

		var packets = new List<GameServerPacket>();
		var projectedCubeCount = player?.InventoryItems.Count(item => item.Location == CubeStorageId && item.ItemId != KinahItemId) ?? 0;
		foreach (var operation in mutationPlan.OrderedOperations)
		{
			if (operation.UpdatedItem != null)
			{
				var template = _itemTemplates.GetItemTemplate(operation.UpdatedItem.ItemId);
				if (template == null)
					return CraftStartInventoryPacketPlan.MissingUpdatedItemTemplate(mutationPlan, operation.UpdatedItem.ItemId, packets);

				packets.Add(new SmInventoryUpdateItem(
					operation.UpdatedItem,
					template,
					SmInventoryUpdateItem.DecreaseItemUse,
					GetGeneralInfoWarehouseRestrictionFlag(operation.UpdatedItem.ItemId, itemRestrictionCleanups)));
			}
			else if (operation.DeletedObjectId.HasValue)
			{
				packets.Add(new SmDeleteItem(operation.DeletedObjectId.Value, SmDeleteItem.UseDeleteType));
				projectedCubeCount--;
				packets.Add(SmCubeUpdate.CubeSizeSnapshot(
					projectedCubeCount,
					player!.NpcExpands,
					player.QuestExpands,
					player.ItemExpands));
			}
		}

		return CraftStartInventoryPacketPlan.Planned(mutationPlan, packets);
	}

	public CraftStartInventoryPersistencePlan CreateStartInventoryPersistencePlan(CraftStartInventoryMutationPlan? mutationPlan)
	{
		// Java parity: Storage.decreaseItemCount marks changed/deleted items dirty,
		// Storage.delete adds deleted items to storage.deletedItems, and InventoryDAO.store
		// later batches DELETE rows before UPDATE rows. This method plans only; it does
		// not mutate storage, write the database, or release object ids.
		if (mutationPlan == null)
			return CraftStartInventoryPersistencePlan.NotPlanned("CraftService.checkCraft persistence planning requires inventory mutation evidence");
		if (!mutationPlan.IsPlanned)
			return CraftStartInventoryPersistencePlan.MutationNotPlanned(mutationPlan);

		var operations = new List<CraftStartInventoryPersistenceOperation>();
		foreach (var operation in mutationPlan.OrderedOperations)
		{
			if (operation.UpdatedItem != null)
			{
				operations.Add(CraftStartInventoryPersistenceOperation.UpdateItem(
					operation.Decrease,
					CopyInventoryItem(
						operation.UpdatedItem,
						operation.UpdatedItem.Count,
						InventoryItemPersistentState.UpdateRequired)));
			}
			else if (operation.DeletedObjectId.HasValue)
			{
				var deletedState = operation.DeletedItem == null
					? InventoryItemPersistentState.Deleted
					: InventoryItem.TransitionPersistentState(operation.DeletedItem.PersistentState, InventoryItemPersistentState.Deleted);
				operations.Add(CraftStartInventoryPersistenceOperation.DeleteItem(
					operation.Decrease,
					operation.DeletedObjectId.Value,
					deletedState));
			}
		}

		return CraftStartInventoryPersistencePlan.Planned(mutationPlan, operations);
	}

	public CraftFinishProductPlan CreateFinishProductPlan(Player? player, RecipeTemplateSummary? recipeTemplate, int critCount)
	{
		// Java parity: services/craft/CraftService.finishCrafting product-selection branch.
		if (player == null)
			return CraftFinishProductPlan.MissingPlayer(recipeTemplate?.RecipeId ?? 0, critCount);
		if (recipeTemplate == null)
			return CraftFinishProductPlan.MissingRecipe(player.ObjectId, critCount);

		var usesComboProduct = critCount > 0;
		var productItemId = usesComboProduct
			? recipeTemplate.GetComboProduct(critCount)
			: recipeTemplate.ProductId;
		if (!productItemId.HasValue)
			return CraftFinishProductPlan.MissingComboProduct(player.ObjectId, recipeTemplate.RecipeId, critCount, recipeTemplate.Quantity);

		var productTemplate = _itemTemplates?.GetItemTemplate(productItemId.Value);
		var marksCreatorOnEquipment = productTemplate is { IsWeapon: true } or { IsArmor: true };
		return CraftFinishProductPlan.Planned(
			player.ObjectId,
			recipeTemplate.RecipeId,
			critCount,
			productItemId.Value,
			recipeTemplate.Quantity,
			usesComboProduct,
			marksCreatorOnEquipment ? player.Name : null,
			marksCreatorOnEquipment);
	}

	public CraftFinishCooldownPlan CreateFinishCooldownPlan(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		long currentTimeMillis)
	{
		// Java parity: CraftService.finishCrafting -> if (craftDelayId != null)
		// put(craftDelayId, System.currentTimeMillis() + craftDelayTime * 1000).
		if (player == null)
			return CraftFinishCooldownPlan.MissingPlayer(recipeTemplate?.RecipeId ?? 0);
		if (recipeTemplate == null)
			return CraftFinishCooldownPlan.MissingRecipe(player.ObjectId);
		if (!recipeTemplate.CraftDelayId.HasValue)
			return CraftFinishCooldownPlan.NoCooldown(player.ObjectId, recipeTemplate.RecipeId);
		if (!recipeTemplate.CraftDelayTime.HasValue)
			return CraftFinishCooldownPlan.MissingDelayTime(player.ObjectId, recipeTemplate);

		var reuseTimeMillis = currentTimeMillis + (recipeTemplate.CraftDelayTime.Value * 1000L);
		return CraftFinishCooldownPlan.Planned(
			player.ObjectId,
			recipeTemplate.RecipeId,
			recipeTemplate.CraftDelayId.Value,
			recipeTemplate.CraftDelayTime.Value,
			reuseTimeMillis);
	}

	public CraftFinishRewardPlan CreateFinishRewardPlan(
		Player? player,
		IReadOnlyList<InventoryItem>? inventoryItems,
		RecipeTemplateSummary? recipeTemplate,
		int critCount,
		Func<int> nextObjectId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
	{
		// Java parity: services/craft/CraftService.finishCrafting -> ItemService.addItem(..., ItemAddType.CRAFTED_ITEM, ItemUpdateType.INC_ITEM_COLLECT).
		var productPlan = CreateFinishProductPlan(player, recipeTemplate, critCount);
		if (productPlan.Status != CraftFinishProductStatus.Planned)
			return CraftFinishRewardPlan.FromProductFailure(productPlan);
		if (player == null)
			return CraftFinishRewardPlan.FromProductFailure(productPlan);

		var itemTemplate = _itemTemplates?.GetItemTemplate(productPlan.ProductItemId);
		if (itemTemplate == null)
			return CraftFinishRewardPlan.MissingItemTemplate(productPlan);

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			inventoryItems ?? Array.Empty<InventoryItem>(),
			itemTemplate,
			productPlan.Quantity,
			nextObjectId,
			allowInventoryOverflow: false,
			itemTemplates: _itemTemplates);

		var addedItems = productPlan.MarksCreatorOnEquipment && !string.IsNullOrEmpty(productPlan.CreatorName)
			? addPlan.AddedItems.Select(item => CopyInventoryItem(item, productPlan.CreatorName!)).ToArray()
			: addPlan.AddedItems;

		var packets = new List<GameServerPacket>();
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			packets.Add(new SmInventoryUpdateItem(
				updatedItem,
				itemTemplate,
				SmInventoryUpdateItem.IncreaseItemCollect,
				GetGeneralInfoWarehouseRestrictionFlag(updatedItem.ItemId, itemRestrictionCleanups)));
		}

		foreach (var addedItem in addedItems)
		{
			packets.Add(SmInventoryAddItem.CreateCraftedItem(
				addedItem,
				itemTemplate,
				GetGeneralInfoWarehouseRestrictionFlag(addedItem.ItemId, itemRestrictionCleanups)));
		}

		return CraftFinishRewardPlan.Success(
			productPlan,
			itemTemplate,
			addPlan.UpdatedItems,
			addedItems,
			addPlan.RemainingCount,
			addPlan.InventoryFull,
			packets,
			shouldSendInventoryFullMessage: addPlan.RemainingCount > 0 && addPlan.InventoryFull);
	}

	private static int GetGeneralInfoWarehouseRestrictionFlag(int itemId, ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		return itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(itemId) == true ? 3 : 0;
	}

	private string GetSkillClientName(int skillId)
	{
		return _skillTemplates?.GetSkillTemplate(skillId)?.GetClientName() ?? string.Empty;
	}

	private string GetItemClientName(int itemId)
	{
		return _itemTemplates?.GetItemTemplate(itemId)?.GetClientName() ?? string.Empty;
	}

	private static MissingCraftComponent? GetMissingComponent(
		Player player,
		RecipeTemplateSummary recipeTemplate,
		IReadOnlyDictionary<int, long> selectedMaterialData)
	{
		var selectedComponents = GetSelectedComponentGroup(recipeTemplate, selectedMaterialData);
		if (selectedComponents == null)
			return null;

		foreach (var component in selectedComponents.Components)
		{
			var availableCount = GetCubeItemCountByItemId(player.InventoryItems, component.ItemId);
			if (availableCount < component.Quantity)
				return new MissingCraftComponent(component.ItemId, component.Quantity, availableCount);
		}

		return null;
	}

	private static RecipeComponentDataSummary? GetSelectedComponentGroup(
		RecipeTemplateSummary recipeTemplate,
		IReadOnlyDictionary<int, long> selectedMaterialData)
	{
		foreach (var componentGroup in recipeTemplate.ComponentGroups)
		{
			var firstComponent = componentGroup.FirstComponent;
			if (firstComponent == null || !selectedMaterialData.ContainsKey(firstComponent.ItemId))
				continue;

			return componentGroup;
		}

		return null;
	}

	private static long GetCubeItemCountByItemId(IReadOnlyList<InventoryItem> inventoryItems, int itemId)
	{
		return inventoryItems
			.Where(item => item.ItemId == itemId && item.Location == CubeStorageId && !item.IsEquipped)
			.Sum(item => item.Count);
	}

	private static int GetBonusRequiredItemId(int skillId)
	{
		// Java parity: services/craft/CraftService.getBonusReqItem.
		return skillId switch
		{
			40001 => 169401081,
			40002 => 169401076,
			40003 => 169401077,
			40004 => 169401078,
			40007 => 169401080,
			40008 => 169401079,
			40010 => 169401082,
			_ => 0,
		};
	}

	private static int GetCraftIntervalCap(string itemQuality)
	{
		// Java parity: CraftService.startCrafting switch(itemTemplate.getItemQuality()).
		return itemQuality switch
		{
			"UNIQUE" or "EPIC" => 1500,
			"MYTHIC" => 1700,
			_ => 1200,
		};
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, string creatorName)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = creatorName,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = item.PendingTuneResult,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item)
	{
		return CopyInventoryItem(item, item.Count);
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		long count,
		InventoryItemPersistentState? requestedPersistentState = null)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = item.PendingTuneResult,
			PersistentState = requestedPersistentState.HasValue
				? InventoryItem.TransitionPersistentState(item.PersistentState, requestedPersistentState.Value)
				: item.PersistentState,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}

	private sealed class CraftInventoryWorkingItem
	{
		public CraftInventoryWorkingItem(InventoryItem item, long count)
		{
			Item = item;
			Count = count;
		}

		public InventoryItem Item { get; }

		public long Count { get; set; }
	}
}

public sealed record CraftStartDpCostResult(
	CraftStartDpCostStatus Status,
	int ObjectId,
	int RecipeId,
	int RequiredDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static CraftStartDpCostResult MissingPlayer(int recipeId)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingPlayer,
			0,
			recipeId,
			0,
			0,
			0);
	}

	public static CraftStartDpCostResult MissingRecipe(int objectId, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingRecipe,
			objectId,
			0,
			0,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult NotEnoughDp(int objectId, int recipeId, int requiredDp, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.NotEnoughDp,
			objectId,
			recipeId,
			requiredDp,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int recipeId,
		int requiredDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? CraftStartDpCostStatus.DpBoundarySkipped
			: CraftStartDpCostStatus.Applied;
		return new CraftStartDpCostResult(
			status,
			change.ObjectId,
			recipeId,
			requiredDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum CraftStartDpCostStatus
{
	Applied,
	MissingPlayer,
	MissingRecipe,
	NotEnoughDp,
	DpBoundarySkipped,
}

public sealed record CraftStartValidationPlan(
	CraftStartValidationStatus Status,
	int ObjectId,
	int RecipeId,
	int SkillId,
	int ProductItemId,
	int TargetObjectId,
	int TargetTemplateId,
	int RequiredDp,
	int CurrentDp,
	int RequiredSkillPoint,
	int CurrentSkillLevel,
	int MissingComponentItemId,
	long MissingComponentRequiredCount,
	long MissingComponentAvailableCount,
	bool IsMorphRecipe,
	bool ShouldSendCancelCraft,
	bool IsReadyForNextValidation,
	GameServerPacket? FailurePacket,
	string JavaSource)
{
	public const int MorphSubstancesSkillId = 40009;

	public static CraftStartValidationPlan MissingPlayer(int recipeId)
	{
		return new CraftStartValidationPlan(
			CraftStartValidationStatus.MissingPlayer,
			0,
			recipeId,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			IsMorphRecipe: false,
			ShouldSendCancelCraft: true,
			IsReadyForNextValidation: false,
			FailurePacket: null,
			"CraftService.startCrafting/checkCraft -> missing player cannot continue");
	}

	public static CraftStartValidationPlan MissingRecipe(int objectId)
	{
		return new CraftStartValidationPlan(
			CraftStartValidationStatus.MissingRecipe,
			objectId,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			IsMorphRecipe: false,
			ShouldSendCancelCraft: true,
			IsReadyForNextValidation: false,
			FailurePacket: null,
			"CraftService.startCrafting -> DataManager.RECIPE_DATA.getRecipeTemplateById(recipeId); checkCraft recipeTemplate == null");
	}

	public static CraftStartValidationPlan MissingProductTemplate(int objectId, RecipeTemplateSummary recipeTemplate)
	{
		return FromRecipe(
			CraftStartValidationStatus.MissingProductTemplate,
			objectId,
			recipeTemplate,
			productItemId: recipeTemplate.ProductId,
			targetObjectId: 0,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp: 0,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: null,
			"CraftService.startCrafting -> DataManager.ITEM_DATA.getItemTemplate(productId); checkCraft itemTemplate == null");
	}

	public static CraftStartValidationPlan AlreadyCrafting(
		int objectId,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate)
	{
		return FromRecipe(
			CraftStartValidationStatus.AlreadyCrafting,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId: 0,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp: 0,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: null,
			"CraftService.checkCraft -> player.getCraftingTask() != null && isInProgress()");
	}

	public static CraftStartValidationPlan InvalidNonMorphTarget(
		int objectId,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate)
	{
		return FromRecipe(
			CraftStartValidationStatus.InvalidNonMorphTarget,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId: 0,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp: 0,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: null,
			"CraftService.checkCraft -> skillId != 40009 && (target == null || !(target instanceof StaticObject))");
	}

	public static CraftStartValidationPlan TooFarFromTool(
		int objectId,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetTemplateId)
	{
		return FromRecipe(
			CraftStartValidationStatus.TooFarFromTool,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId: 0,
			targetTemplateId,
			requiredDp: recipeTemplate.Dp,
			currentDp: 0,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: null,
			"CraftService.checkCraft -> !PositionUtil.isInRange(player, target, 5, false)");
	}

	public static CraftStartValidationPlan NotEnoughDp(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId)
	{
		return FromRecipe(
			CraftStartValidationStatus.NotEnoughDp,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: null,
			"CraftService.checkCraft -> recipeTemplate.getDp() != null && player.getCommonData().getDp() < recipeTemplate.getDp()");
	}

	public static CraftStartValidationPlan InvalidCurrentStance(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId)
	{
		return FromRecipe(
			CraftStartValidationStatus.InvalidCurrentStance,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CraftCannotCombineWhileInCurrentStance(),
			"CraftService.checkCraft -> player.isInPlayerMode(PlayerMode.RIDE) || player.isInAnyHide()");
	}

	public static CraftStartValidationPlan InventoryFull(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId)
	{
		return FromRecipe(
			CraftStartValidationStatus.InventoryFull,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CombineInventoryFull(),
			"CraftService.checkCraft -> player.getInventory().isFull()");
	}

	public static CraftStartValidationPlan MissingKnownRecipe(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId)
	{
		return FromRecipe(
			CraftStartValidationStatus.MissingKnownRecipe,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CombineCannotFindRecipe(),
			"CraftService.checkCraft -> !player.getRecipeList().isRecipePresent(recipeTemplate.getId())");
	}

	public static CraftStartValidationPlan CraftCooldownActive(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId)
	{
		return FromRecipe(
			CraftStartValidationStatus.CraftCooldownActive,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.ItemCantUseUntilDelayTime(),
			"CraftService.checkCraft -> recipeTemplate.getCraftDelayId() != null && player.getCraftCooldowns().hasCooldown(recipeTemplate.getCraftDelayId())");
	}

	public static CraftStartValidationPlan MissingCraftSkill(
		int objectId,
		int currentDp,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId,
		string skillName)
	{
		return FromRecipe(
			CraftStartValidationStatus.MissingCraftSkill,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel: 0,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CombineCantUse(skillName),
			"CraftService.checkCraft -> !player.getSkillList().isSkillPresent(skillId)");
	}

	public static CraftStartValidationPlan CraftSkillTooLow(
		int objectId,
		int currentDp,
		int currentSkillLevel,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId,
		string skillName)
	{
		return FromRecipe(
			CraftStartValidationStatus.CraftSkillTooLow,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CombineOutOfSkillPoint(skillName),
			"CraftService.checkCraft -> player.getSkillList().getSkillLevel(skillId) < recipeTemplate.getSkillpoint()");
	}

	public static CraftStartValidationPlan MissingComponentItem(
		int objectId,
		int currentDp,
		int currentSkillLevel,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId,
		int componentItemId,
		long requiredQuantity,
		long availableCount,
		string itemName)
	{
		return FromRecipe(
			CraftStartValidationStatus.MissingComponentItem,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel,
			missingComponentItemId: componentItemId,
			missingComponentRequiredCount: requiredQuantity,
			missingComponentAvailableCount: availableCount,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: requiredQuantity == 1
				? SmSystemMessage.CombineNoComponentItemSingle(itemName)
				: SmSystemMessage.CombineNoComponentItemMultiple(requiredQuantity, itemName),
			"CraftService.checkCraft -> selected recipe component group inventory count is below component quantity");
	}

	public static CraftStartValidationPlan MissingBonusItem(
		int objectId,
		int currentDp,
		int currentSkillLevel,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId,
		int bonusItemId,
		string itemName)
	{
		return FromRecipe(
			CraftStartValidationStatus.MissingBonusItem,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel,
			missingComponentItemId: bonusItemId,
			missingComponentRequiredCount: 1,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: true,
			isReadyForNextValidation: false,
			failurePacket: SmSystemMessage.CombineNoComponentItemSingle(itemName),
			"CraftService.checkCraft -> craftType == 1 && !inventory.decreaseByItemId(getBonusReqItem(skillId), 1)");
	}

	public static CraftStartValidationPlan ReadyForNextValidation(
		int objectId,
		int currentDp,
		int currentSkillLevel,
		RecipeTemplateSummary recipeTemplate,
		ItemTemplateSummary productTemplate,
		int targetObjectId,
		bool isMorphRecipe)
	{
		return FromRecipe(
			CraftStartValidationStatus.ReadyForNextValidation,
			objectId,
			recipeTemplate,
			productTemplate.TemplateId,
			targetObjectId,
			targetTemplateId: 0,
			requiredDp: recipeTemplate.Dp,
			currentDp,
			requiredSkillPoint: recipeTemplate.SkillPoint,
			currentSkillLevel,
			missingComponentItemId: 0,
			missingComponentRequiredCount: 0,
			missingComponentAvailableCount: 0,
			shouldSendCancelCraft: false,
			isReadyForNextValidation: true,
			failurePacket: null,
			isMorphRecipe
				? "CraftService.checkCraft -> morphing does not need static object/npc to use; skill/material/bonus guards passed"
				: "CraftService.checkCraft -> static target/DP/stance/inventory/recipe/cooldown/skill/material/bonus guards passed; continue to material consumption");
	}

	private static CraftStartValidationPlan FromRecipe(
		CraftStartValidationStatus status,
		int objectId,
		RecipeTemplateSummary recipeTemplate,
		int productItemId,
		int targetObjectId,
		int targetTemplateId,
		int requiredDp,
		int currentDp,
		int requiredSkillPoint,
		int currentSkillLevel,
		int missingComponentItemId,
		long missingComponentRequiredCount,
		long missingComponentAvailableCount,
		bool shouldSendCancelCraft,
		bool isReadyForNextValidation,
		GameServerPacket? failurePacket,
		string javaSource)
	{
		return new CraftStartValidationPlan(
			status,
			objectId,
			recipeTemplate.RecipeId,
			recipeTemplate.SkillId,
			productItemId,
			targetObjectId,
			targetTemplateId,
			requiredDp,
			currentDp,
			requiredSkillPoint,
			currentSkillLevel,
			missingComponentItemId,
			missingComponentRequiredCount,
			missingComponentAvailableCount,
			recipeTemplate.SkillId == MorphSubstancesSkillId,
			shouldSendCancelCraft,
			isReadyForNextValidation,
			failurePacket,
			javaSource);
	}
}

public enum CraftStartValidationStatus
{
	MissingPlayer,
	MissingRecipe,
	MissingProductTemplate,
	AlreadyCrafting,
	InvalidNonMorphTarget,
	TooFarFromTool,
	NotEnoughDp,
	InvalidCurrentStance,
	InventoryFull,
	MissingKnownRecipe,
	CraftCooldownActive,
	MissingCraftSkill,
	CraftSkillTooLow,
	MissingComponentItem,
	MissingBonusItem,
	ReadyForNextValidation,
}

public sealed record MissingCraftComponent(int ItemId, long RequiredQuantity, long AvailableCount);

public sealed record CraftStartCancelPacketPlan(
	CraftStartCancelPacketPlanStatus Status,
	GameServerPacket? SelfPacket,
	GameServerPacket? BroadcastPacket,
	string JavaSource,
	bool IsLive)
{
	public static CraftStartCancelPacketPlan NotPlanned(string javaSource)
	{
		return new CraftStartCancelPacketPlan(
			CraftStartCancelPacketPlanStatus.NotPlanned,
			SelfPacket: null,
			BroadcastPacket: null,
			javaSource,
			IsLive: false);
	}

	public static CraftStartCancelPacketPlan Planned(GameServerPacket selfPacket, GameServerPacket broadcastPacket)
	{
		return new CraftStartCancelPacketPlan(
			CraftStartCancelPacketPlanStatus.Planned,
			selfPacket,
			broadcastPacket,
			"CraftService.sendCancelCraft -> send SM_CRAFT_UPDATE(action=4), broadcast SM_CRAFT_ANIMATION(skill=0, action=2)",
			IsLive: false);
	}
}

public enum CraftStartCancelPacketPlanStatus
{
	NotPlanned,
	Planned,
}

public sealed record CraftStartFailureOrchestrationPlan(
	CraftStartFailureOrchestrationStatus Status,
	CraftStartValidationPlan? ValidationPlan,
	CraftStartCancelPacketPlan? CancelPlan,
	IReadOnlyList<GameServerPacket> OrderedPackets,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartFailureOrchestrationStatus.Planned;

	public GameServerPacket? FailurePacket => ValidationPlan?.FailurePacket;

	public GameServerPacket? SelfCancelPacket => CancelPlan?.SelfPacket;

	public GameServerPacket? BroadcastCancelPacket => CancelPlan?.BroadcastPacket;

	public static CraftStartFailureOrchestrationPlan NotPlanned(string javaSource)
	{
		return new CraftStartFailureOrchestrationPlan(
			CraftStartFailureOrchestrationStatus.NotPlanned,
			ValidationPlan: null,
			CancelPlan: null,
			OrderedPackets: Array.Empty<GameServerPacket>(),
			javaSource,
			IsLive: false);
	}

	public static CraftStartFailureOrchestrationPlan Planned(
		CraftStartValidationPlan validationPlan,
		CraftStartCancelPacketPlan cancelPlan,
		IReadOnlyList<GameServerPacket> orderedPackets)
	{
		return new CraftStartFailureOrchestrationPlan(
			CraftStartFailureOrchestrationStatus.Planned,
			validationPlan,
			cancelPlan,
			orderedPackets,
			"CraftService.startCrafting -> checkCraft failure packet, then sendCancelCraft update/animation",
			IsLive: false);
	}

	public static CraftStartFailureOrchestrationPlan CancelNotPlanned(
		CraftStartValidationPlan validationPlan,
		CraftStartCancelPacketPlan? cancelPlan,
		IReadOnlyList<GameServerPacket> orderedPackets)
	{
		return new CraftStartFailureOrchestrationPlan(
			CraftStartFailureOrchestrationStatus.CancelNotPlanned,
			validationPlan,
			cancelPlan,
			orderedPackets,
			"CraftService.startCrafting -> checkCraft returned false, but sendCancelCraft packet prerequisites were unavailable",
			IsLive: false);
	}
}

public enum CraftStartFailureOrchestrationStatus
{
	NotPlanned,
	Planned,
	CancelNotPlanned,
}

public sealed record CraftStartConsumptionPlan(
	CraftStartConsumptionStatus Status,
	CraftStartValidationPlan? ValidationPlan,
	int RecipeId,
	IReadOnlyList<CraftStartConsumedItemPlan> Decreases,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartConsumptionStatus.Planned;

	public static CraftStartConsumptionPlan NotPlanned(string javaSource)
	{
		return new CraftStartConsumptionPlan(
			CraftStartConsumptionStatus.NotPlanned,
			ValidationPlan: null,
			RecipeId: 0,
			Decreases: Array.Empty<CraftStartConsumedItemPlan>(),
			javaSource,
			IsLive: false);
	}

	public static CraftStartConsumptionPlan Planned(
		CraftStartValidationPlan validationPlan,
		int recipeId,
		IReadOnlyList<CraftStartConsumedItemPlan> decreases)
	{
		return new CraftStartConsumptionPlan(
			CraftStartConsumptionStatus.Planned,
			validationPlan,
			recipeId,
			decreases,
			"CraftService.checkCraft -> bonus decrease first, selected component group decreases second",
			IsLive: false);
	}
}

public enum CraftStartConsumptionStatus
{
	NotPlanned,
	Planned,
}

public sealed record CraftStartConsumedItemPlan(
	int ItemId,
	long Quantity,
	CraftStartConsumedItemKind Kind,
	string JavaSource)
{
	public static CraftStartConsumedItemPlan Bonus(int itemId, long quantity)
	{
		return new CraftStartConsumedItemPlan(
			itemId,
			quantity,
			CraftStartConsumedItemKind.BonusItem,
			"CraftService.checkCraft -> inventory.decreaseByItemId(getBonusReqItem(skillId), 1)");
	}

	public static CraftStartConsumedItemPlan Component(int itemId, long quantity)
	{
		return new CraftStartConsumedItemPlan(
			itemId,
			quantity,
			CraftStartConsumedItemKind.Component,
			"CraftService.checkCraft -> inventory.decreaseByItemId(component.getItemId(), component.getQuantity())");
	}
}

public enum CraftStartConsumedItemKind
{
	BonusItem,
	Component,
}

public sealed record CraftStartInventoryMutationPlan(
	CraftStartInventoryMutationStatus Status,
	CraftStartConsumptionPlan? ConsumptionPlan,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<int> DeletedObjectIds,
	IReadOnlyList<CraftStartInventoryMutationOperation> OrderedOperations,
	CraftStartConsumedItemPlan? FailedDecrease,
	long AvailableCount,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartInventoryMutationStatus.Planned;

	public static CraftStartInventoryMutationPlan NotPlanned(string javaSource)
	{
		return new CraftStartInventoryMutationPlan(
			CraftStartInventoryMutationStatus.NotPlanned,
			ConsumptionPlan: null,
			UpdatedItems: Array.Empty<InventoryItem>(),
			DeletedObjectIds: Array.Empty<int>(),
			OrderedOperations: Array.Empty<CraftStartInventoryMutationOperation>(),
			FailedDecrease: null,
			AvailableCount: 0,
			javaSource,
			IsLive: false);
	}

	public static CraftStartInventoryMutationPlan MissingInventory(CraftStartConsumptionPlan consumptionPlan)
	{
		return new CraftStartInventoryMutationPlan(
			CraftStartInventoryMutationStatus.MissingInventory,
			consumptionPlan,
			UpdatedItems: Array.Empty<InventoryItem>(),
			DeletedObjectIds: Array.Empty<int>(),
			OrderedOperations: Array.Empty<CraftStartInventoryMutationOperation>(),
			FailedDecrease: null,
			AvailableCount: 0,
			"CraftService.checkCraft inventory mutation planning requires player inventory",
			IsLive: false);
	}

	public static CraftStartInventoryMutationPlan InsufficientInventory(
		CraftStartConsumptionPlan consumptionPlan,
		CraftStartConsumedItemPlan failedDecrease,
		long availableCount,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<int> deletedObjectIds,
		IReadOnlyList<CraftStartInventoryMutationOperation> orderedOperations)
	{
		return new CraftStartInventoryMutationPlan(
			CraftStartInventoryMutationStatus.InsufficientInventory,
			consumptionPlan,
			updatedItems.ToArray(),
			deletedObjectIds.ToArray(),
			orderedOperations.ToArray(),
			failedDecrease,
			availableCount,
			"Storage.decreaseByItemId -> matching stacks could not satisfy requested count",
			IsLive: false);
	}

	public static CraftStartInventoryMutationPlan Planned(
		CraftStartConsumptionPlan consumptionPlan,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<int> deletedObjectIds,
		IReadOnlyList<CraftStartInventoryMutationOperation> orderedOperations)
	{
		return new CraftStartInventoryMutationPlan(
			CraftStartInventoryMutationStatus.Planned,
			consumptionPlan,
			updatedItems.ToArray(),
			deletedObjectIds.ToArray(),
			orderedOperations.ToArray(),
			FailedDecrease: null,
			AvailableCount: 0,
			"Storage.decreaseByItemId -> ordered decreaseItemCount(..., ItemUpdateType.DEC_ITEM_USE), delete stacks at zero",
			IsLive: false);
	}
}

public enum CraftStartInventoryMutationStatus
{
	NotPlanned,
	MissingInventory,
	InsufficientInventory,
	Planned,
}

public sealed record CraftStartInventoryMutationOperation(
	CraftStartInventoryMutationOperationKind Kind,
	CraftStartConsumedItemPlan Decrease,
	InventoryItem? UpdatedItem,
	int? DeletedObjectId,
	InventoryItem? DeletedItem,
	string JavaSource)
{
	public static CraftStartInventoryMutationOperation Updated(CraftStartConsumedItemPlan decrease, InventoryItem updatedItem)
	{
		return new CraftStartInventoryMutationOperation(
			CraftStartInventoryMutationOperationKind.Updated,
			decrease,
			updatedItem,
			DeletedObjectId: null,
			DeletedItem: null,
			"Storage.decreaseItemCount -> ItemPacketService.sendItemPacket with DEC_ITEM_USE for remaining stack");
	}

	public static CraftStartInventoryMutationOperation Deleted(CraftStartConsumedItemPlan decrease, int deletedObjectId)
	{
		return Deleted(
			decrease,
			new InventoryItem
			{
				ObjectId = deletedObjectId,
				PersistentState = InventoryItemPersistentState.Updated,
			});
	}

	public static CraftStartInventoryMutationOperation Deleted(CraftStartConsumedItemPlan decrease, InventoryItem deletedItem)
	{
		return new CraftStartInventoryMutationOperation(
			CraftStartInventoryMutationOperationKind.Deleted,
			decrease,
			UpdatedItem: null,
			deletedItem.ObjectId,
			deletedItem,
			"Storage.decreaseItemCount -> delete(..., ItemDeleteType.USE) for zero stack");
	}
}

public enum CraftStartInventoryMutationOperationKind
{
	Updated,
	Deleted,
}

public sealed record CraftStartInventoryPersistencePlan(
	CraftStartInventoryPersistenceStatus Status,
	CraftStartInventoryMutationPlan? MutationPlan,
	IReadOnlyList<CraftStartInventoryPersistenceOperation> Operations,
	IReadOnlyList<CraftStartInventoryPersistenceSqlDescriptor> SqlDescriptors,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<int> DeletedObjectIds,
	IReadOnlyList<int> NoActionDeletedObjectIds,
	IReadOnlyList<int> ObjectIdsPendingRelease,
	bool WouldReleaseObjectIdsAfterSuccessfulDelete,
	bool DidReleaseObjectIds,
	bool ShouldWriteLiveState,
	string JavaSource,
	bool IsLive)
{
	public const string JavaInventoryDeleteSql = "DELETE FROM inventory WHERE item_unique_id=?";
	public const string JavaInventoryUpdateSql = "UPDATE inventory SET item_count=?, item_color=?, color_expires=?, item_creator=?, expire_time=?, activation_count=?, item_owner=?, is_equipped=?, is_soul_bound=?, slot=?, item_location=?, enchant=?, enchant_bonus=?, item_skin=?, fusioned_item=?, optional_socket=?, optional_fusion_socket=?, charge=?, tune_count=?, rnd_bonus=?, fusion_rnd_bonus=?, tempering=?, pack_count=?, is_amplified=?, buff_skill=?, rnd_plume_bonus=? WHERE item_unique_id=?";

	public bool IsPlanned => Status == CraftStartInventoryPersistenceStatus.Planned;

	public static CraftStartInventoryPersistencePlan NotPlanned(string javaSource)
	{
		return new CraftStartInventoryPersistencePlan(
			CraftStartInventoryPersistenceStatus.NotPlanned,
			MutationPlan: null,
			Operations: Array.Empty<CraftStartInventoryPersistenceOperation>(),
			SqlDescriptors: Array.Empty<CraftStartInventoryPersistenceSqlDescriptor>(),
			UpdatedItems: Array.Empty<InventoryItem>(),
			DeletedObjectIds: Array.Empty<int>(),
			NoActionDeletedObjectIds: Array.Empty<int>(),
			ObjectIdsPendingRelease: Array.Empty<int>(),
			WouldReleaseObjectIdsAfterSuccessfulDelete: false,
			DidReleaseObjectIds: false,
			ShouldWriteLiveState: false,
			javaSource,
			IsLive: false);
	}

	public static CraftStartInventoryPersistencePlan MutationNotPlanned(CraftStartInventoryMutationPlan mutationPlan)
	{
		return new CraftStartInventoryPersistencePlan(
			CraftStartInventoryPersistenceStatus.MutationNotPlanned,
			mutationPlan,
			Operations: Array.Empty<CraftStartInventoryPersistenceOperation>(),
			SqlDescriptors: Array.Empty<CraftStartInventoryPersistenceSqlDescriptor>(),
			UpdatedItems: Array.Empty<InventoryItem>(),
			DeletedObjectIds: Array.Empty<int>(),
			NoActionDeletedObjectIds: Array.Empty<int>(),
			ObjectIdsPendingRelease: Array.Empty<int>(),
			WouldReleaseObjectIdsAfterSuccessfulDelete: false,
			DidReleaseObjectIds: false,
			ShouldWriteLiveState: false,
			"InventoryDAO.store is not planned when CraftService.checkCraft inventory mutation was not planned",
			IsLive: false);
	}

	public static CraftStartInventoryPersistencePlan Planned(
		CraftStartInventoryMutationPlan mutationPlan,
		IReadOnlyList<CraftStartInventoryPersistenceOperation> operations)
	{
		var operationSnapshot = operations.ToArray();
		var deletedObjectIds = operationSnapshot
			.Where(operation => operation.Kind == CraftStartInventoryPersistenceOperationKind.DeleteItem)
			.Select(operation => operation.DeletedObjectId!.Value)
			.ToArray();

		return new CraftStartInventoryPersistencePlan(
			CraftStartInventoryPersistenceStatus.Planned,
			mutationPlan,
			operationSnapshot,
			CreateSqlDescriptors(operationSnapshot),
			operationSnapshot
				.Where(operation => operation.Kind == CraftStartInventoryPersistenceOperationKind.UpdateItem)
				.Select(operation => operation.UpdatedItem!)
				.ToArray(),
			deletedObjectIds,
			operationSnapshot
				.Where(operation => operation.Kind == CraftStartInventoryPersistenceOperationKind.NoAction)
				.Select(operation => operation.DeletedObjectId!.Value)
				.ToArray(),
			ObjectIdsPendingRelease: deletedObjectIds,
			WouldReleaseObjectIdsAfterSuccessfulDelete: deletedObjectIds.Length > 0,
			DidReleaseObjectIds: false,
			ShouldWriteLiveState: false,
			"Storage.decreaseItemCount dirty states -> InventoryDAO.store deleteItems before updateItems",
			IsLive: false);
	}

	private static IReadOnlyList<CraftStartInventoryPersistenceSqlDescriptor> CreateSqlDescriptors(
		IReadOnlyList<CraftStartInventoryPersistenceOperation> operations)
	{
		var descriptors = new List<CraftStartInventoryPersistenceSqlDescriptor>();
		foreach (var operation in operations.Where(operation => operation.Kind == CraftStartInventoryPersistenceOperationKind.DeleteItem))
			descriptors.Add(CraftStartInventoryPersistenceSqlDescriptor.DeleteInventoryRow(operation));

		foreach (var operation in operations.Where(operation => operation.Kind == CraftStartInventoryPersistenceOperationKind.UpdateItem))
			descriptors.Add(CraftStartInventoryPersistenceSqlDescriptor.UpdateInventoryRow(operation));

		return descriptors;
	}
}

public sealed record CraftStartInventoryPersistenceSqlDescriptor(
	CraftStartInventoryPersistenceSqlOperationKind Kind,
	CraftStartInventoryPersistenceOperation Operation,
	InventoryItem? UpdatedItem,
	int? DeletedObjectId,
	string Sql,
	string JavaDaoMethod,
	string JavaParameterSource,
	bool WouldExecuteSql,
	bool DidExecuteSql)
{
	public static CraftStartInventoryPersistenceSqlDescriptor DeleteInventoryRow(
		CraftStartInventoryPersistenceOperation operation)
	{
		return new CraftStartInventoryPersistenceSqlDescriptor(
			CraftStartInventoryPersistenceSqlOperationKind.DeleteInventoryRow,
			operation,
			UpdatedItem: null,
			operation.DeletedObjectId,
			CraftStartInventoryPersistencePlan.JavaInventoryDeleteSql,
			"InventoryDAO.deleteItems",
			"stmt.setInt(1, item.getObjectId())",
			WouldExecuteSql: true,
			DidExecuteSql: false);
	}

	public static CraftStartInventoryPersistenceSqlDescriptor UpdateInventoryRow(
		CraftStartInventoryPersistenceOperation operation)
	{
		return new CraftStartInventoryPersistenceSqlDescriptor(
			CraftStartInventoryPersistenceSqlOperationKind.UpdateInventoryRow,
			operation,
			operation.UpdatedItem,
			DeletedObjectId: null,
			CraftStartInventoryPersistencePlan.JavaInventoryUpdateSql,
			"InventoryDAO.updateItems",
			"stmt.setLong(1, item.getItemCount()) ... stmt.setInt(27, item.getObjectId())",
			WouldExecuteSql: true,
			DidExecuteSql: false);
	}
}

public sealed record CraftStartInventoryPersistenceOperation(
	CraftStartInventoryPersistenceOperationKind Kind,
	CraftStartConsumedItemPlan Decrease,
	InventoryItem? UpdatedItem,
	int? DeletedObjectId,
	InventoryItemPersistentState PersistentState,
	bool ShouldWrite,
	string JavaDaoMethod)
{
	public static CraftStartInventoryPersistenceOperation UpdateItem(
		CraftStartConsumedItemPlan decrease,
		InventoryItem updatedItem)
	{
		return new CraftStartInventoryPersistenceOperation(
			CraftStartInventoryPersistenceOperationKind.UpdateItem,
			decrease,
			updatedItem,
			DeletedObjectId: null,
			updatedItem.PersistentState,
			ShouldWrite: true,
			"InventoryDAO.updateItems");
	}

	public static CraftStartInventoryPersistenceOperation DeleteItem(
		CraftStartConsumedItemPlan decrease,
		int deletedObjectId,
		InventoryItemPersistentState persistentState)
	{
		var shouldDelete = persistentState == InventoryItemPersistentState.Deleted;
		return new CraftStartInventoryPersistenceOperation(
			shouldDelete
				? CraftStartInventoryPersistenceOperationKind.DeleteItem
				: CraftStartInventoryPersistenceOperationKind.NoAction,
			decrease,
			UpdatedItem: null,
			deletedObjectId,
			persistentState,
			shouldDelete,
			shouldDelete ? "InventoryDAO.deleteItems" : "Item.setPersistentState(NEW -> NOACTION)");
	}
}

public enum CraftStartInventoryPersistenceOperationKind
{
	UpdateItem,
	DeleteItem,
	NoAction,
}

public enum CraftStartInventoryPersistenceSqlOperationKind
{
	DeleteInventoryRow,
	UpdateInventoryRow,
}

public static class CraftStartInventoryPersistenceAdapterPlanService
{
	public static CraftStartInventoryPersistenceAdapterPlan CreateDisabledPlan(
		CraftStartInventoryPersistencePlan? persistencePlan)
	{
		// Java parity: InventoryDAO.store opens a connection, disables autocommit,
		// executes delete/insert/update batches, then releases deleted object ids after a successful delete batch.
		if (persistencePlan == null)
			return CraftStartInventoryPersistenceAdapterPlan.PersistencePlanMissing();
		if (!persistencePlan.IsPlanned)
			return CraftStartInventoryPersistenceAdapterPlan.PersistencePlanNotReady(persistencePlan);
		if (persistencePlan.SqlDescriptors.Count == 0)
			return CraftStartInventoryPersistenceAdapterPlan.NoSqlRequired(persistencePlan);

		var operations = persistencePlan.SqlDescriptors
			.Select(CraftStartInventoryPersistenceAdapterOperation.Disabled)
			.ToArray();
		return CraftStartInventoryPersistenceAdapterPlan.DisabledNoWrite(persistencePlan, operations);
	}
}

public sealed record CraftStartInventoryPersistenceAdapterPlan(
	CraftStartInventoryPersistenceAdapterStatus Status,
	CraftStartInventoryPersistencePlan? PersistencePlan,
	IReadOnlyList<CraftStartInventoryPersistenceAdapterOperation> Operations,
	bool WouldOpenConnection,
	bool DidOpenConnection,
	bool WouldBeginTransaction,
	bool DidBeginTransaction,
	bool WouldExecuteSql,
	bool DidExecuteSql,
	bool WouldCommitBatches,
	bool DidCommitBatches,
	bool WouldReleaseObjectIdsAfterSuccessfulDelete,
	bool DidReleaseObjectIds,
	int WouldExecuteSqlCount,
	int ExecutedSqlCount,
	string JavaSource,
	bool IsLive)
{
	public static CraftStartInventoryPersistenceAdapterPlan PersistencePlanMissing()
	{
		return new CraftStartInventoryPersistenceAdapterPlan(
			CraftStartInventoryPersistenceAdapterStatus.PersistencePlanMissing,
			PersistencePlan: null,
			Operations: Array.Empty<CraftStartInventoryPersistenceAdapterOperation>(),
			WouldOpenConnection: false,
			DidOpenConnection: false,
			WouldBeginTransaction: false,
			DidBeginTransaction: false,
			WouldExecuteSql: false,
			DidExecuteSql: false,
			WouldCommitBatches: false,
			DidCommitBatches: false,
			WouldReleaseObjectIdsAfterSuccessfulDelete: false,
			DidReleaseObjectIds: false,
			WouldExecuteSqlCount: 0,
			ExecutedSqlCount: 0,
			"InventoryDAO.store boundary skipped because craft-start persistence plan is missing",
			IsLive: false);
	}

	public static CraftStartInventoryPersistenceAdapterPlan PersistencePlanNotReady(
		CraftStartInventoryPersistencePlan persistencePlan)
	{
		return new CraftStartInventoryPersistenceAdapterPlan(
			CraftStartInventoryPersistenceAdapterStatus.PersistencePlanNotReady,
			persistencePlan,
			Operations: Array.Empty<CraftStartInventoryPersistenceAdapterOperation>(),
			WouldOpenConnection: false,
			DidOpenConnection: false,
			WouldBeginTransaction: false,
			DidBeginTransaction: false,
			WouldExecuteSql: false,
			DidExecuteSql: false,
			WouldCommitBatches: false,
			DidCommitBatches: false,
			WouldReleaseObjectIdsAfterSuccessfulDelete: false,
			DidReleaseObjectIds: false,
			WouldExecuteSqlCount: 0,
			ExecutedSqlCount: 0,
			"InventoryDAO.store boundary skipped because craft-start persistence plan is not planned",
			IsLive: false);
	}

	public static CraftStartInventoryPersistenceAdapterPlan NoSqlRequired(
		CraftStartInventoryPersistencePlan persistencePlan)
	{
		return new CraftStartInventoryPersistenceAdapterPlan(
			CraftStartInventoryPersistenceAdapterStatus.NoSqlRequired,
			persistencePlan,
			Operations: Array.Empty<CraftStartInventoryPersistenceAdapterOperation>(),
			WouldOpenConnection: false,
			DidOpenConnection: false,
			WouldBeginTransaction: false,
			DidBeginTransaction: false,
			WouldExecuteSql: false,
			DidExecuteSql: false,
			WouldCommitBatches: false,
			DidCommitBatches: false,
			WouldReleaseObjectIdsAfterSuccessfulDelete: false,
			DidReleaseObjectIds: false,
			WouldExecuteSqlCount: 0,
			ExecutedSqlCount: 0,
			"InventoryDAO.store boundary skipped because craft-start persistence descriptors contain no SQL writes",
			IsLive: false);
	}

	public static CraftStartInventoryPersistenceAdapterPlan DisabledNoWrite(
		CraftStartInventoryPersistencePlan persistencePlan,
		IReadOnlyList<CraftStartInventoryPersistenceAdapterOperation> operations)
	{
		return new CraftStartInventoryPersistenceAdapterPlan(
			CraftStartInventoryPersistenceAdapterStatus.DisabledNoWrite,
			persistencePlan,
			operations.ToArray(),
			WouldOpenConnection: true,
			DidOpenConnection: false,
			WouldBeginTransaction: true,
			DidBeginTransaction: false,
			WouldExecuteSql: operations.Count > 0,
			DidExecuteSql: false,
			WouldCommitBatches: operations.Count > 0,
			DidCommitBatches: false,
			persistencePlan.WouldReleaseObjectIdsAfterSuccessfulDelete,
			DidReleaseObjectIds: false,
			WouldExecuteSqlCount: operations.Count,
			ExecutedSqlCount: 0,
			"InventoryDAO.store delete/update SQL boundary identified, but live C# database execution remains disabled",
			IsLive: false);
	}
}

public sealed record CraftStartInventoryPersistenceAdapterOperation(
	CraftStartInventoryPersistenceSqlDescriptor Descriptor,
	CraftStartInventoryPersistenceSqlOperationKind Kind,
	string Sql,
	string JavaDaoMethod,
	bool WouldExecuteSql,
	bool DidExecuteSql)
{
	public static CraftStartInventoryPersistenceAdapterOperation Disabled(
		CraftStartInventoryPersistenceSqlDescriptor descriptor)
	{
		return new CraftStartInventoryPersistenceAdapterOperation(
			descriptor,
			descriptor.Kind,
			descriptor.Sql,
			descriptor.JavaDaoMethod,
			WouldExecuteSql: descriptor.WouldExecuteSql,
			DidExecuteSql: false);
	}
}

public enum CraftStartInventoryPersistenceAdapterStatus
{
	PersistencePlanMissing,
	PersistencePlanNotReady,
	NoSqlRequired,
	DisabledNoWrite,
}

public enum CraftStartInventoryPersistenceStatus
{
	NotPlanned,
	MutationNotPlanned,
	Planned,
}

public sealed record CraftStartInventoryPacketPlan(
	CraftStartInventoryPacketStatus Status,
	CraftStartInventoryMutationPlan? MutationPlan,
	IReadOnlyList<GameServerPacket> Packets,
	int MissingItemTemplateId,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartInventoryPacketStatus.Planned;

	public static CraftStartInventoryPacketPlan NotPlanned(string javaSource)
	{
		return new CraftStartInventoryPacketPlan(
			CraftStartInventoryPacketStatus.NotPlanned,
			MutationPlan: null,
			Packets: Array.Empty<GameServerPacket>(),
			MissingItemTemplateId: 0,
			javaSource,
			IsLive: false);
	}

	public static CraftStartInventoryPacketPlan MissingItemTemplates(CraftStartInventoryMutationPlan mutationPlan)
	{
		return new CraftStartInventoryPacketPlan(
			CraftStartInventoryPacketStatus.MissingItemTemplates,
			mutationPlan,
			Packets: Array.Empty<GameServerPacket>(),
			MissingItemTemplateId: 0,
			"SM_INVENTORY_UPDATE_ITEM packet planning requires item templates",
			IsLive: false);
	}

	public static CraftStartInventoryPacketPlan MissingUpdatedItemTemplate(
		CraftStartInventoryMutationPlan mutationPlan,
		int missingItemTemplateId,
		IReadOnlyList<GameServerPacket> packets)
	{
		return new CraftStartInventoryPacketPlan(
			CraftStartInventoryPacketStatus.MissingUpdatedItemTemplate,
			mutationPlan,
			packets.ToArray(),
			missingItemTemplateId,
			"SM_INVENTORY_UPDATE_ITEM packet planning requires the updated stack template",
			IsLive: false);
	}

	public static CraftStartInventoryPacketPlan MissingCubeSizeSnapshot(
		CraftStartInventoryMutationPlan mutationPlan,
		IReadOnlyList<GameServerPacket> packets)
	{
		return new CraftStartInventoryPacketPlan(
			CraftStartInventoryPacketStatus.MissingCubeSizeSnapshot,
			mutationPlan,
			packets.ToArray(),
			MissingItemTemplateId: 0,
			"ItemPacketService.sendItemDeletePacket -> SM_CUBE_UPDATE.cubeSize requires player cube expand/count snapshot",
			IsLive: false);
	}

	public static CraftStartInventoryPacketPlan Planned(
		CraftStartInventoryMutationPlan mutationPlan,
		IReadOnlyList<GameServerPacket> packets)
	{
		return new CraftStartInventoryPacketPlan(
			CraftStartInventoryPacketStatus.Planned,
			mutationPlan,
			packets.ToArray(),
			MissingItemTemplateId: 0,
			"Storage.decreaseItemCount -> ItemPacketService.sendItemPacket with DEC_ITEM_USE or SM_DELETE_ITEM with USE followed by SM_CUBE_UPDATE",
			IsLive: false);
	}
}

public enum CraftStartInventoryPacketStatus
{
	NotPlanned,
	MissingItemTemplates,
	MissingUpdatedItemTemplate,
	MissingCubeSizeSnapshot,
	Planned,
}

public static class CraftStartInventoryPacketSendAdapterPlanService
{
	public static CraftStartInventoryPacketSendAdapterPlan CreateDisabledPlan(
		CraftStartInventoryPacketPlan? packetPlan,
		int playerObjectId)
	{
		// Java parity: Storage.decreaseItemCount/delete dispatch through ItemPacketService,
		// which reaches PacketSendUtility.sendPacket. This adapter records that boundary only.
		if (packetPlan == null)
			return CraftStartInventoryPacketSendAdapterPlan.PacketPlanMissing(playerObjectId);
		if (!packetPlan.IsPlanned || packetPlan.Packets.Count == 0)
			return CraftStartInventoryPacketSendAdapterPlan.PacketPlanNotReady(packetPlan, playerObjectId);

		var operations = packetPlan.Packets
			.Select((packet, index) => CraftStartInventoryPacketSendOperation.Disabled(packet, index))
			.ToArray();
		return CraftStartInventoryPacketSendAdapterPlan.DisabledNoSend(packetPlan, playerObjectId, operations);
	}
}

public sealed record CraftStartInventoryPacketSendAdapterPlan(
	CraftStartInventoryPacketSendAdapterStatus Status,
	CraftStartInventoryPacketPlan? PacketPlan,
	int PlayerObjectId,
	IReadOnlyList<CraftStartInventoryPacketSendOperation> Operations,
	bool WouldCallSendPacketAsync,
	bool DidCallSendPacketAsync,
	int WouldSendPacketCount,
	int SentPacketCount,
	string JavaSource,
	bool IsLive)
{
	public static CraftStartInventoryPacketSendAdapterPlan PacketPlanMissing(int playerObjectId)
	{
		return new CraftStartInventoryPacketSendAdapterPlan(
			CraftStartInventoryPacketSendAdapterStatus.PacketPlanMissing,
			PacketPlan: null,
			playerObjectId,
			Operations: Array.Empty<CraftStartInventoryPacketSendOperation>(),
			WouldCallSendPacketAsync: false,
			DidCallSendPacketAsync: false,
			WouldSendPacketCount: 0,
			SentPacketCount: 0,
			"ItemPacketService packet-send boundary skipped because craft-start inventory packet intent is missing",
			IsLive: false);
	}

	public static CraftStartInventoryPacketSendAdapterPlan PacketPlanNotReady(
		CraftStartInventoryPacketPlan packetPlan,
		int playerObjectId)
	{
		return new CraftStartInventoryPacketSendAdapterPlan(
			CraftStartInventoryPacketSendAdapterStatus.PacketPlanNotReady,
			packetPlan,
			playerObjectId,
			Operations: Array.Empty<CraftStartInventoryPacketSendOperation>(),
			WouldCallSendPacketAsync: false,
			DidCallSendPacketAsync: false,
			WouldSendPacketCount: 0,
			SentPacketCount: 0,
			"ItemPacketService packet-send boundary skipped because craft-start inventory packet intent is not planned",
			IsLive: false);
	}

	public static CraftStartInventoryPacketSendAdapterPlan DisabledNoSend(
		CraftStartInventoryPacketPlan packetPlan,
		int playerObjectId,
		IReadOnlyList<CraftStartInventoryPacketSendOperation> operations)
	{
		return new CraftStartInventoryPacketSendAdapterPlan(
			CraftStartInventoryPacketSendAdapterStatus.DisabledNoSend,
			packetPlan,
			playerObjectId,
			operations.ToArray(),
			WouldCallSendPacketAsync: operations.Count > 0,
			DidCallSendPacketAsync: false,
			WouldSendPacketCount: operations.Count,
			SentPacketCount: 0,
			"ItemPacketService.sendItemPacket/sendItemDeletePacket boundary identified, but live C# SendPacketAsync remains disabled",
			IsLive: false);
	}
}

public sealed record CraftStartInventoryPacketSendOperation(
	int PacketIndex,
	GameServerPacket Packet,
	string PacketTypeName,
	string JavaUtilityMethod,
	bool WouldCallSendPacketAsync,
	bool DidCallSendPacketAsync)
{
	public static CraftStartInventoryPacketSendOperation Disabled(GameServerPacket packet, int packetIndex)
	{
		return new CraftStartInventoryPacketSendOperation(
			packetIndex,
			packet,
			packet.GetType().Name,
			"ItemPacketService -> PacketSendUtility.sendPacket",
			WouldCallSendPacketAsync: true,
			DidCallSendPacketAsync: false);
	}
}

public enum CraftStartInventoryPacketSendAdapterStatus
{
	PacketPlanMissing,
	PacketPlanNotReady,
	DisabledNoSend,
}

public sealed record CraftStartTaskPlan(
	CraftStartTaskPlanStatus Status,
	CraftStartValidationPlan? ValidationPlan,
	int ProductItemId,
	string ProductQuality,
	int SkillLevelDiff,
	int IntervalCap,
	int Interval,
	int BonusCritModifier,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftStartTaskPlanStatus.Planned;

	public static CraftStartTaskPlan NotPlanned(string javaSource)
	{
		return new CraftStartTaskPlan(
			CraftStartTaskPlanStatus.NotPlanned,
			ValidationPlan: null,
			ProductItemId: 0,
			ProductQuality: string.Empty,
			SkillLevelDiff: 0,
			IntervalCap: 0,
			Interval: 0,
			BonusCritModifier: 0,
			javaSource,
			IsLive: false);
	}

	public static CraftStartTaskPlan Planned(
		CraftStartValidationPlan validationPlan,
		int productItemId,
		string productQuality,
		int skillLevelDiff,
		int intervalCap,
		int interval,
		int bonusCritModifier)
	{
		return new CraftStartTaskPlan(
			CraftStartTaskPlanStatus.Planned,
			validationPlan,
			productItemId,
			productQuality,
			skillLevelDiff,
			intervalCap,
			interval,
			bonusCritModifier,
			"CraftService.startCrafting -> set CraftingTask, setInterval, then start",
			IsLive: false);
	}
}

public enum CraftStartTaskPlanStatus
{
	NotPlanned,
	Planned,
}

public sealed record CraftFinishProductPlan(
	CraftFinishProductStatus Status,
	int ObjectId,
	int RecipeId,
	int CritCount,
	int ProductItemId,
	int Quantity,
	bool UsesComboProduct,
	string? CreatorName,
	bool MarksCreatorOnEquipment)
{
	public static CraftFinishProductPlan MissingPlayer(int recipeId, int critCount)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingPlayer,
			0,
			recipeId,
			critCount,
			0,
			0,
			false,
			null,
			false);
	}

	public static CraftFinishProductPlan MissingRecipe(int objectId, int critCount)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingRecipe,
			objectId,
			0,
			critCount,
			0,
			0,
			false,
			null,
			false);
	}

	public static CraftFinishProductPlan MissingComboProduct(int objectId, int recipeId, int critCount, int quantity)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.MissingComboProduct,
			objectId,
			recipeId,
			critCount,
			0,
			quantity,
			true,
			null,
			false);
	}

	public static CraftFinishProductPlan Planned(
		int objectId,
		int recipeId,
		int critCount,
		int productItemId,
		int quantity,
		bool usesComboProduct,
		string? creatorName,
		bool marksCreatorOnEquipment)
	{
		return new CraftFinishProductPlan(
			CraftFinishProductStatus.Planned,
			objectId,
			recipeId,
			critCount,
			productItemId,
			quantity,
			usesComboProduct,
			creatorName,
			marksCreatorOnEquipment);
	}
}

public enum CraftFinishProductStatus
{
	Planned,
	MissingPlayer,
	MissingRecipe,
	MissingComboProduct,
}

public sealed record CraftFinishCooldownPlan(
	CraftFinishCooldownStatus Status,
	int ObjectId,
	int RecipeId,
	int CraftDelayId,
	int CraftDelayTimeSeconds,
	long ReuseTimeMillis,
	string JavaSource,
	bool IsLive)
{
	public bool ShouldApplyCooldown => Status == CraftFinishCooldownStatus.Planned;

	public static CraftFinishCooldownPlan MissingPlayer(int recipeId)
	{
		return new CraftFinishCooldownPlan(
			CraftFinishCooldownStatus.MissingPlayer,
			ObjectId: 0,
			recipeId,
			CraftDelayId: 0,
			CraftDelayTimeSeconds: 0,
			ReuseTimeMillis: 0,
			"CraftService.finishCrafting requires player cooldowns",
			IsLive: false);
	}

	public static CraftFinishCooldownPlan MissingRecipe(int objectId)
	{
		return new CraftFinishCooldownPlan(
			CraftFinishCooldownStatus.MissingRecipe,
			objectId,
			RecipeId: 0,
			CraftDelayId: 0,
			CraftDelayTimeSeconds: 0,
			ReuseTimeMillis: 0,
			"CraftService.finishCrafting requires recipe template for craft delay",
			IsLive: false);
	}

	public static CraftFinishCooldownPlan NoCooldown(int objectId, int recipeId)
	{
		return new CraftFinishCooldownPlan(
			CraftFinishCooldownStatus.NoCooldown,
			objectId,
			recipeId,
			CraftDelayId: 0,
			CraftDelayTimeSeconds: 0,
			ReuseTimeMillis: 0,
			"CraftService.finishCrafting -> recipeTemplate.getCraftDelayId() == null",
			IsLive: false);
	}

	public static CraftFinishCooldownPlan MissingDelayTime(int objectId, RecipeTemplateSummary recipeTemplate)
	{
		return new CraftFinishCooldownPlan(
			CraftFinishCooldownStatus.MissingDelayTime,
			objectId,
			recipeTemplate.RecipeId,
			recipeTemplate.CraftDelayId ?? 0,
			CraftDelayTimeSeconds: 0,
			ReuseTimeMillis: 0,
			"CraftService.finishCrafting -> craftDelayId exists but craftDelayTime is unavailable",
			IsLive: false);
	}

	public static CraftFinishCooldownPlan Planned(
		int objectId,
		int recipeId,
		int craftDelayId,
		int craftDelayTimeSeconds,
		long reuseTimeMillis)
	{
		return new CraftFinishCooldownPlan(
			CraftFinishCooldownStatus.Planned,
			objectId,
			recipeId,
			craftDelayId,
			craftDelayTimeSeconds,
			reuseTimeMillis,
			"CraftService.finishCrafting -> player.getCraftCooldowns().put(craftDelayId, reuseTimeMillis)",
			IsLive: false);
	}
}

public enum CraftFinishCooldownStatus
{
	Planned,
	MissingPlayer,
	MissingRecipe,
	NoCooldown,
	MissingDelayTime,
}

public static class CraftFinishCooldownApplicationPlanService
{
	public static CraftFinishCooldownApplicationPlan CreateDisabledPlan(
		Player? player,
		CraftFinishCooldownPlan? cooldownPlan,
		long currentTimeMillis)
	{
		// Java parity: Cooldowns.put stores only future reuse times; expired/immediate reuse removes the cooldown id.
		if (cooldownPlan == null)
			return CraftFinishCooldownApplicationPlan.CooldownPlanMissing(player?.ObjectId ?? 0, currentTimeMillis);
		if (player == null)
			return CraftFinishCooldownApplicationPlan.PlayerMissing(cooldownPlan, currentTimeMillis);
		if (cooldownPlan.Status != CraftFinishCooldownStatus.Planned)
			return CraftFinishCooldownApplicationPlan.CooldownPlanNotReady(cooldownPlan, player.CraftCooldowns, currentTimeMillis);

		var existingCooldowns = player.CraftCooldowns ?? new Dictionary<int, long>();
		existingCooldowns.TryGetValue(cooldownPlan.CraftDelayId, out var previousReuseTimeMillis);
		var projectedCooldowns = new Dictionary<int, long>(existingCooldowns);
		var wouldStoreCooldown = cooldownPlan.ReuseTimeMillis > currentTimeMillis;
		if (wouldStoreCooldown)
			projectedCooldowns[cooldownPlan.CraftDelayId] = cooldownPlan.ReuseTimeMillis;
		else
			projectedCooldowns.Remove(cooldownPlan.CraftDelayId);

		return CraftFinishCooldownApplicationPlan.DisabledNoMutation(
			cooldownPlan,
			existingCooldowns,
			projectedCooldowns,
			previousReuseTimeMillis,
			currentTimeMillis,
			wouldStoreCooldown);
	}
}

public sealed record CraftFinishCooldownApplicationPlan(
	CraftFinishCooldownApplicationStatus Status,
	CraftFinishCooldownPlan? CooldownPlan,
	int ObjectId,
	int RecipeId,
	int CraftDelayId,
	long PreviousReuseTimeMillis,
	long ReuseTimeMillis,
	long CurrentTimeMillis,
	IReadOnlyDictionary<int, long> ExistingCooldowns,
	IReadOnlyDictionary<int, long> ProjectedCooldowns,
	bool WouldStoreCooldown,
	bool DidStoreCooldown,
	bool WouldRemoveCooldown,
	bool DidRemoveCooldown,
	string JavaSource,
	bool IsLive)
{
	public static CraftFinishCooldownApplicationPlan CooldownPlanMissing(int objectId, long currentTimeMillis)
	{
		return new CraftFinishCooldownApplicationPlan(
			CraftFinishCooldownApplicationStatus.CooldownPlanMissing,
			CooldownPlan: null,
			objectId,
			RecipeId: 0,
			CraftDelayId: 0,
			PreviousReuseTimeMillis: 0,
			ReuseTimeMillis: 0,
			currentTimeMillis,
			ExistingCooldowns: new Dictionary<int, long>(),
			ProjectedCooldowns: new Dictionary<int, long>(),
			WouldStoreCooldown: false,
			DidStoreCooldown: false,
			WouldRemoveCooldown: false,
			DidRemoveCooldown: false,
			"Cooldowns.put boundary skipped because craft-finish cooldown plan is missing",
			IsLive: false);
	}

	public static CraftFinishCooldownApplicationPlan PlayerMissing(
		CraftFinishCooldownPlan cooldownPlan,
		long currentTimeMillis)
	{
		return new CraftFinishCooldownApplicationPlan(
			CraftFinishCooldownApplicationStatus.PlayerMissing,
			cooldownPlan,
			cooldownPlan.ObjectId,
			cooldownPlan.RecipeId,
			cooldownPlan.CraftDelayId,
			PreviousReuseTimeMillis: 0,
			cooldownPlan.ReuseTimeMillis,
			currentTimeMillis,
			ExistingCooldowns: new Dictionary<int, long>(),
			ProjectedCooldowns: new Dictionary<int, long>(),
			WouldStoreCooldown: false,
			DidStoreCooldown: false,
			WouldRemoveCooldown: false,
			DidRemoveCooldown: false,
			"Cooldowns.put boundary skipped because player cooldown map is unavailable",
			IsLive: false);
	}

	public static CraftFinishCooldownApplicationPlan CooldownPlanNotReady(
		CraftFinishCooldownPlan cooldownPlan,
		IReadOnlyDictionary<int, long> existingCooldowns,
		long currentTimeMillis)
	{
		return new CraftFinishCooldownApplicationPlan(
			CraftFinishCooldownApplicationStatus.CooldownPlanNotReady,
			cooldownPlan,
			cooldownPlan.ObjectId,
			cooldownPlan.RecipeId,
			cooldownPlan.CraftDelayId,
			PreviousReuseTimeMillis: 0,
			cooldownPlan.ReuseTimeMillis,
			currentTimeMillis,
			new Dictionary<int, long>(existingCooldowns),
			new Dictionary<int, long>(existingCooldowns),
			WouldStoreCooldown: false,
			DidStoreCooldown: false,
			WouldRemoveCooldown: false,
			DidRemoveCooldown: false,
			"Cooldowns.put boundary skipped because craft-finish cooldown plan is not planned",
			IsLive: false);
	}

	public static CraftFinishCooldownApplicationPlan DisabledNoMutation(
		CraftFinishCooldownPlan cooldownPlan,
		IReadOnlyDictionary<int, long> existingCooldowns,
		IReadOnlyDictionary<int, long> projectedCooldowns,
		long previousReuseTimeMillis,
		long currentTimeMillis,
		bool wouldStoreCooldown)
	{
		return new CraftFinishCooldownApplicationPlan(
			CraftFinishCooldownApplicationStatus.DisabledNoMutation,
			cooldownPlan,
			cooldownPlan.ObjectId,
			cooldownPlan.RecipeId,
			cooldownPlan.CraftDelayId,
			previousReuseTimeMillis,
			cooldownPlan.ReuseTimeMillis,
			currentTimeMillis,
			new Dictionary<int, long>(existingCooldowns),
			new Dictionary<int, long>(projectedCooldowns),
			wouldStoreCooldown,
			DidStoreCooldown: false,
			WouldRemoveCooldown: !wouldStoreCooldown,
			DidRemoveCooldown: false,
			"CraftService.finishCrafting -> Cooldowns.put(craftDelayId, reuseTimeMillis) identified, but live cooldown mutation remains disabled",
			IsLive: false);
	}
}

public enum CraftFinishCooldownApplicationStatus
{
	DisabledNoMutation,
	CooldownPlanMissing,
	PlayerMissing,
	CooldownPlanNotReady,
}

public static class CraftCooldownPersistencePlanService
{
	public const string JavaCraftCooldownInsertSql = "INSERT INTO `craft_cooldowns` (`player_id`, `delay_id`, `reuse_time`) VALUES (?,?,?)";
	public const string JavaCraftCooldownDeleteSql = "DELETE FROM `craft_cooldowns` WHERE `player_id`=?";

	public static CraftCooldownPersistencePlan CreateDisabledPlan(
		int playerObjectId,
		IReadOnlyDictionary<int, long>? craftCooldowns,
		long currentTimeMillis)
	{
		// Java parity: CraftCooldownsDAO.storeCraftCooldowns deletes all rows first,
		// then inserts entries whose reuse time is not before System.currentTimeMillis().
		if (playerObjectId <= 0)
			return CraftCooldownPersistencePlan.PlayerMissing(currentTimeMillis);
		if (craftCooldowns == null)
			return CraftCooldownPersistencePlan.CooldownsMissing(playerObjectId, currentTimeMillis);

		var descriptors = new List<CraftCooldownPersistenceSqlDescriptor>
		{
			CraftCooldownPersistenceSqlDescriptor.DeleteAllForPlayer(playerObjectId),
		};
		var skippedExpired = 0;
		foreach (var (delayId, reuseTimeMillis) in craftCooldowns)
		{
			if (reuseTimeMillis < currentTimeMillis)
			{
				skippedExpired++;
				continue;
			}

			descriptors.Add(CraftCooldownPersistenceSqlDescriptor.InsertActiveCooldown(
				playerObjectId,
				delayId,
				reuseTimeMillis));
		}

		return CraftCooldownPersistencePlan.DisabledNoWrite(
			playerObjectId,
			currentTimeMillis,
			descriptors,
			skippedExpired);
	}
}

public sealed record CraftCooldownPersistencePlan(
	CraftCooldownPersistencePlanStatus Status,
	int PlayerObjectId,
	long CurrentTimeMillis,
	IReadOnlyList<CraftCooldownPersistenceSqlDescriptor> SqlDescriptors,
	int DeleteDescriptorCount,
	int InsertDescriptorCount,
	int SkippedExpiredCooldownCount,
	bool WouldDeleteExistingRows,
	bool DidDeleteExistingRows,
	bool WouldInsertActiveCooldowns,
	bool DidInsertActiveCooldowns,
	string JavaSource,
	bool IsLive)
{
	public bool IsPlanned => Status == CraftCooldownPersistencePlanStatus.DisabledNoWrite;

	public static CraftCooldownPersistencePlan PlayerMissing(long currentTimeMillis)
	{
		return new CraftCooldownPersistencePlan(
			CraftCooldownPersistencePlanStatus.PlayerMissing,
			PlayerObjectId: 0,
			currentTimeMillis,
			SqlDescriptors: Array.Empty<CraftCooldownPersistenceSqlDescriptor>(),
			DeleteDescriptorCount: 0,
			InsertDescriptorCount: 0,
			SkippedExpiredCooldownCount: 0,
			WouldDeleteExistingRows: false,
			DidDeleteExistingRows: false,
			WouldInsertActiveCooldowns: false,
			DidInsertActiveCooldowns: false,
			"CraftCooldownsDAO.storeCraftCooldowns skipped because player id is unavailable",
			IsLive: false);
	}

	public static CraftCooldownPersistencePlan CooldownsMissing(int playerObjectId, long currentTimeMillis)
	{
		return new CraftCooldownPersistencePlan(
			CraftCooldownPersistencePlanStatus.CooldownsMissing,
			playerObjectId,
			currentTimeMillis,
			SqlDescriptors: Array.Empty<CraftCooldownPersistenceSqlDescriptor>(),
			DeleteDescriptorCount: 0,
			InsertDescriptorCount: 0,
			SkippedExpiredCooldownCount: 0,
			WouldDeleteExistingRows: false,
			DidDeleteExistingRows: false,
			WouldInsertActiveCooldowns: false,
			DidInsertActiveCooldowns: false,
			"CraftCooldownsDAO.storeCraftCooldowns skipped because craft cooldowns are unavailable",
			IsLive: false);
	}

	public static CraftCooldownPersistencePlan DisabledNoWrite(
		int playerObjectId,
		long currentTimeMillis,
		IReadOnlyList<CraftCooldownPersistenceSqlDescriptor> descriptors,
		int skippedExpiredCooldownCount)
	{
		var insertCount = descriptors.Count(descriptor => descriptor.Kind == CraftCooldownPersistenceSqlOperationKind.InsertActiveCooldown);
		return new CraftCooldownPersistencePlan(
			CraftCooldownPersistencePlanStatus.DisabledNoWrite,
			playerObjectId,
			currentTimeMillis,
			descriptors.ToArray(),
			DeleteDescriptorCount: descriptors.Count(descriptor => descriptor.Kind == CraftCooldownPersistenceSqlOperationKind.DeleteAllForPlayer),
			InsertDescriptorCount: insertCount,
			skippedExpiredCooldownCount,
			WouldDeleteExistingRows: true,
			DidDeleteExistingRows: false,
			WouldInsertActiveCooldowns: insertCount > 0,
			DidInsertActiveCooldowns: false,
			"CraftCooldownsDAO.storeCraftCooldowns delete-all/insert-active SQL boundary identified, but live C# database execution remains disabled",
			IsLive: false);
	}
}

public sealed record CraftCooldownPersistenceSqlDescriptor(
	CraftCooldownPersistenceSqlOperationKind Kind,
	int PlayerObjectId,
	int DelayId,
	long ReuseTimeMillis,
	string Sql,
	string JavaDaoMethod,
	bool WouldExecuteSql,
	bool DidExecuteSql,
	bool IsLive)
{
	public static CraftCooldownPersistenceSqlDescriptor DeleteAllForPlayer(int playerObjectId)
	{
		return new CraftCooldownPersistenceSqlDescriptor(
			CraftCooldownPersistenceSqlOperationKind.DeleteAllForPlayer,
			playerObjectId,
			DelayId: 0,
			ReuseTimeMillis: 0,
			CraftCooldownPersistencePlanService.JavaCraftCooldownDeleteSql,
			"CraftCooldownsDAO.deleteCraftCoolDowns",
			WouldExecuteSql: true,
			DidExecuteSql: false,
			IsLive: false);
	}

	public static CraftCooldownPersistenceSqlDescriptor InsertActiveCooldown(
		int playerObjectId,
		int delayId,
		long reuseTimeMillis)
	{
		return new CraftCooldownPersistenceSqlDescriptor(
			CraftCooldownPersistenceSqlOperationKind.InsertActiveCooldown,
			playerObjectId,
			delayId,
			reuseTimeMillis,
			CraftCooldownPersistencePlanService.JavaCraftCooldownInsertSql,
			"CraftCooldownsDAO.storeCraftCooldowns",
			WouldExecuteSql: true,
			DidExecuteSql: false,
			IsLive: false);
	}
}

public static class CraftCooldownPersistenceAdapterPlanService
{
	public static CraftCooldownPersistenceAdapterPlan CreateDisabledPlan(CraftCooldownPersistencePlan? persistencePlan)
	{
		if (persistencePlan == null)
			return CraftCooldownPersistenceAdapterPlan.PersistencePlanMissing();
		if (!persistencePlan.IsPlanned)
			return CraftCooldownPersistenceAdapterPlan.PersistencePlanNotReady(persistencePlan);

		var operations = persistencePlan.SqlDescriptors
			.Select(CraftCooldownPersistenceAdapterOperation.Disabled)
			.ToArray();
		return CraftCooldownPersistenceAdapterPlan.DisabledNoWrite(persistencePlan, operations);
	}
}

public sealed record CraftCooldownPersistenceAdapterPlan(
	CraftCooldownPersistenceAdapterStatus Status,
	CraftCooldownPersistencePlan? PersistencePlan,
	IReadOnlyList<CraftCooldownPersistenceAdapterOperation> Operations,
	bool WouldOpenConnection,
	bool DidOpenConnection,
	bool WouldExecuteSql,
	bool DidExecuteSql,
	int WouldExecuteSqlCount,
	int ExecutedSqlCount,
	string JavaSource,
	bool IsLive)
{
	public static CraftCooldownPersistenceAdapterPlan PersistencePlanMissing()
	{
		return new CraftCooldownPersistenceAdapterPlan(
			CraftCooldownPersistenceAdapterStatus.PersistencePlanMissing,
			PersistencePlan: null,
			Operations: Array.Empty<CraftCooldownPersistenceAdapterOperation>(),
			WouldOpenConnection: false,
			DidOpenConnection: false,
			WouldExecuteSql: false,
			DidExecuteSql: false,
			WouldExecuteSqlCount: 0,
			ExecutedSqlCount: 0,
			"CraftCooldownsDAO.storeCraftCooldowns boundary skipped because persistence plan is missing",
			IsLive: false);
	}

	public static CraftCooldownPersistenceAdapterPlan PersistencePlanNotReady(CraftCooldownPersistencePlan persistencePlan)
	{
		return new CraftCooldownPersistenceAdapterPlan(
			CraftCooldownPersistenceAdapterStatus.PersistencePlanNotReady,
			persistencePlan,
			Operations: Array.Empty<CraftCooldownPersistenceAdapterOperation>(),
			WouldOpenConnection: false,
			DidOpenConnection: false,
			WouldExecuteSql: false,
			DidExecuteSql: false,
			WouldExecuteSqlCount: 0,
			ExecutedSqlCount: 0,
			"CraftCooldownsDAO.storeCraftCooldowns boundary skipped because persistence plan is not planned",
			IsLive: false);
	}

	public static CraftCooldownPersistenceAdapterPlan DisabledNoWrite(
		CraftCooldownPersistencePlan persistencePlan,
		IReadOnlyList<CraftCooldownPersistenceAdapterOperation> operations)
	{
		return new CraftCooldownPersistenceAdapterPlan(
			CraftCooldownPersistenceAdapterStatus.DisabledNoWrite,
			persistencePlan,
			operations.ToArray(),
			WouldOpenConnection: operations.Count > 0,
			DidOpenConnection: false,
			WouldExecuteSql: operations.Count > 0,
			DidExecuteSql: false,
			WouldExecuteSqlCount: operations.Count,
			ExecutedSqlCount: 0,
			"CraftCooldownsDAO.storeCraftCooldowns SQL boundary identified, but live C# database execution remains disabled",
			IsLive: false);
	}
}

public sealed record CraftCooldownPersistenceAdapterOperation(
	CraftCooldownPersistenceSqlDescriptor Descriptor,
	CraftCooldownPersistenceSqlOperationKind Kind,
	string Sql,
	string JavaDaoMethod,
	bool WouldExecuteSql,
	bool DidExecuteSql)
{
	public static CraftCooldownPersistenceAdapterOperation Disabled(CraftCooldownPersistenceSqlDescriptor descriptor)
	{
		return new CraftCooldownPersistenceAdapterOperation(
			descriptor,
			descriptor.Kind,
			descriptor.Sql,
			descriptor.JavaDaoMethod,
			descriptor.WouldExecuteSql,
			DidExecuteSql: false);
	}
}

public enum CraftCooldownPersistenceSqlOperationKind
{
	DeleteAllForPlayer,
	InsertActiveCooldown,
}

public enum CraftCooldownPersistencePlanStatus
{
	DisabledNoWrite,
	PlayerMissing,
	CooldownsMissing,
}

public enum CraftCooldownPersistenceAdapterStatus
{
	PersistencePlanMissing,
	PersistencePlanNotReady,
	DisabledNoWrite,
}

public sealed record CraftFinishRewardPlan(
	CraftFinishRewardStatus Status,
	CraftFinishProductPlan ProductPlan,
	ItemTemplateSummary? ItemTemplate,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<InventoryItem> AddedItems,
	long RemainingCount,
	bool InventoryFull,
	IReadOnlyList<GameServerPacket> Packets,
	bool ShouldSendInventoryFullMessage)
{
	public static CraftFinishRewardPlan FromProductFailure(CraftFinishProductPlan productPlan)
	{
		return new CraftFinishRewardPlan(
			MapProductStatus(productPlan.Status),
			productPlan,
			null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			0,
			false,
			Array.Empty<GameServerPacket>(),
			false);
	}

	private static CraftFinishRewardStatus MapProductStatus(CraftFinishProductStatus status)
	{
		return status switch
		{
			CraftFinishProductStatus.MissingPlayer => CraftFinishRewardStatus.MissingPlayer,
			CraftFinishProductStatus.MissingRecipe => CraftFinishRewardStatus.MissingRecipe,
			CraftFinishProductStatus.MissingComboProduct => CraftFinishRewardStatus.MissingComboProduct,
			_ => CraftFinishRewardStatus.Planned,
		};
	}

	public static CraftFinishRewardPlan MissingItemTemplate(CraftFinishProductPlan productPlan)
	{
		return new CraftFinishRewardPlan(
			CraftFinishRewardStatus.MissingItemTemplate,
			productPlan,
			null,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			productPlan.Quantity,
			false,
			Array.Empty<GameServerPacket>(),
			false);
	}

	public static CraftFinishRewardPlan Success(
		CraftFinishProductPlan productPlan,
		ItemTemplateSummary itemTemplate,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		long remainingCount,
		bool inventoryFull,
		IReadOnlyList<GameServerPacket> packets,
		bool shouldSendInventoryFullMessage)
	{
		var status = remainingCount == 0
			? CraftFinishRewardStatus.Planned
			: inventoryFull
				? CraftFinishRewardStatus.InventoryFull
				: CraftFinishRewardStatus.PartialOverflow;
		return new CraftFinishRewardPlan(
			status,
			productPlan,
			itemTemplate,
			updatedItems,
			addedItems,
			remainingCount,
			inventoryFull,
			packets,
			shouldSendInventoryFullMessage);
	}
}

public enum CraftFinishRewardStatus
{
	Planned,
	MissingPlayer,
	MissingRecipe,
	MissingComboProduct,
	MissingItemTemplate,
	InventoryFull,
	PartialOverflow,
}
