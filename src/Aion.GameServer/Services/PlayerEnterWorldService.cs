using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class PlayerEnterWorldService
{
	private readonly GameServerOptions _options;
	private readonly IPlayerEnterWorldRepository _repository;
	private readonly GameWorld _world;
	private readonly WorldNpcResourceStatsService? _resourceStats;
	private readonly CreaturePvpZoneCounterService? _creaturePvpZoneCounterService;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly Action<RepurchaseStateRemovePlan>? _repurchaseStateRemovePlanObserver;
	private readonly FindGroupRecruitmentPlanService? _findGroupService;
	private readonly Action<FindGroupLogoutCleanupPlan>? _findGroupLogoutCleanupPlanObserver;
	private readonly PlayerGroupRuntime? _playerGroupRuntime;
	private readonly PlayerAllianceRuntime? _playerAllianceRuntime;
	private readonly PlayerLeagueRuntime? _playerLeagueRuntime;
	private readonly ConcurrentDictionary<int, byte> _enteringWorld = new();
	private readonly ILogger<PlayerEnterWorldService> _logger;

	public PlayerEnterWorldService(
		GameServerOptions options,
		IPlayerEnterWorldRepository repository,
		GameWorld world,
		ILogger<PlayerEnterWorldService> logger,
		WorldNpcResourceStatsService? resourceStats = null,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null,
		IGameClientConnectionRegistry? connectionRegistry = null,
		GameServerRuntimeContext? runtimeContext = null,
		Action<RepurchaseStateRemovePlan>? repurchaseStateRemovePlanObserver = null,
		FindGroupRecruitmentPlanService? findGroupService = null,
		Action<FindGroupLogoutCleanupPlan>? findGroupLogoutCleanupPlanObserver = null,
		PlayerGroupRuntime? playerGroupRuntime = null,
		PlayerAllianceRuntime? playerAllianceRuntime = null,
		PlayerLeagueRuntime? playerLeagueRuntime = null)
	{
		_options = options;
		_repository = repository;
		_world = world;
		_resourceStats = resourceStats;
		_creaturePvpZoneCounterService = creaturePvpZoneCounterService;
		_connectionRegistry = connectionRegistry;
		_runtimeContext = runtimeContext;
		_repurchaseStateRemovePlanObserver = repurchaseStateRemovePlanObserver;
		_findGroupService = findGroupService;
		_findGroupLogoutCleanupPlanObserver = findGroupLogoutCleanupPlanObserver;
		_playerGroupRuntime = playerGroupRuntime;
		_playerAllianceRuntime = playerAllianceRuntime;
		_playerLeagueRuntime = playerLeagueRuntime;
		_logger = logger;
	}

	public async Task<PlayerEnterWorldResult> EnterWorldAsync(
		int accountId,
		int playerObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerEnterWorldService.enterWorld(AionConnection, int).
		if (accountId == 0)
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);

		var player = await _repository.LoadPlayerAsync(accountId, playerObjectId, cancellationToken);
		if (player == null)
		{
			_logger.LogWarning("Player enterWorld fail: character obj ID {PlayerObjectId} was not found on account ID {AccountId}", playerObjectId, accountId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}

		if (player.IsOnline)
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		if (IsInsideReentryWindow(player.LastOnline))
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		if (_world.TryGetObject(playerObjectId, out _))
		{
			_logger.LogWarning("Player enterWorld fail: duplicate character obj ID {PlayerObjectId} found in world", playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}

		if (!_enteringWorld.TryAdd(playerObjectId, 0))
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ReentryTime);

		try
		{
			player.InventoryItems = await _repository.LoadPlayerItemsAsync(playerObjectId, cancellationToken);
			player.WarehouseItems = await _repository.LoadPlayerWarehouseItemsAsync(playerObjectId, cancellationToken);
			player.AccountWarehouseItems = await _repository.LoadAccountWarehouseItemsAsync(player.AccountId, cancellationToken);
			player.Skills = await _repository.LoadPlayerSkillsAsync(playerObjectId, cancellationToken);
			player.SkillCooldowns = await _repository.LoadPlayerSkillCooldownsAsync(playerObjectId, cancellationToken);
			player.ItemCooldowns = await _repository.LoadPlayerItemCooldownsAsync(playerObjectId, cancellationToken);
			player.Quests = await _repository.LoadPlayerQuestsAsync(playerObjectId, cancellationToken);
			if (_runtimeContext?.DataManager?.StaticData.NpcFactions is { } npcFactions)
			{
				player.NpcFactions = await _repository.LoadPlayerNpcFactionsAsync(
					playerObjectId,
					npcFactions,
					CurrentEpochSeconds(),
					cancellationToken);
			}

			player.Titles = await _repository.LoadPlayerTitlesAsync(playerObjectId, cancellationToken);
			player.Motions = await _repository.LoadPlayerMotionsAsync(playerObjectId, cancellationToken);
			player.Emotions = await _repository.LoadPlayerEmotionsAsync(playerObjectId, cancellationToken);
			player.Recipes = await _repository.LoadPlayerRecipesAsync(playerObjectId, cancellationToken);
			player.Macros = await _repository.LoadPlayerMacrosAsync(playerObjectId, cancellationToken);
			player.Mailbox = await _repository.LoadPlayerMailboxAsync(playerObjectId, cancellationToken);
			player.BrokerSettlements = await _repository.LoadBrokerSettlementsAsync(playerObjectId, player.Race, cancellationToken);
			player.Houses = await _repository.LoadPlayerHousesAsync(playerObjectId, cancellationToken);
			player.CraftCooldowns = await _repository.LoadPlayerCraftCooldownsAsync(playerObjectId, cancellationToken);
			player.HouseObjectCooldowns = await _repository.LoadPlayerHouseObjectCooldownsAsync(playerObjectId, cancellationToken);
			player.PortalCooldowns = await _repository.LoadPlayerPortalCooldownsAsync(playerObjectId, cancellationToken);
			player.LifeStats = await _repository.LoadPlayerLifeStatsAsync(playerObjectId, cancellationToken);
			player.Friends = await _repository.LoadPlayerFriendsAsync(playerObjectId, cancellationToken);
			player.BlockedUsers = await _repository.LoadPlayerBlockedUsersAsync(playerObjectId, cancellationToken);
			player.AbyssRank = await _repository.LoadPlayerAbyssRankAsync(playerObjectId, cancellationToken);
			player.Settings = await _repository.LoadPlayerSettingsAsync(playerObjectId, cancellationToken);
			player.BindPoint = await _repository.LoadPlayerBindPointAsync(playerObjectId, cancellationToken);
			if (!_world.TryAddObject(playerObjectId, player))
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);

			var previousLastOnline = player.LastOnline;
			var now = DateTime.Now;
			if (!await _repository.MarkPlayerOnlineAsync(playerObjectId, now, cancellationToken))
			{
				if (_world.TryRemoveObject(playerObjectId, out _))
					ClearPlayerCreaturePvpZones(playerObjectId);
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
			}

			player.IsOnline = true;
			await ApplyOfflineDpResetAsync(player, previousLastOnline, now);
			player.LastOnline = now;
			_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) logged on", player.Name, playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.Ok, player);
		}
		catch (Exception ex)
		{
			if (_world.TryRemoveObject(playerObjectId, out _))
				ClearPlayerCreaturePvpZones(playerObjectId);
			_logger.LogError(ex, "Error during enter-world of player {PlayerObjectId}", playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
		}
		finally
		{
			_enteringWorld.TryRemove(playerObjectId, out _);
		}
	}

	public async Task<bool> SaveMacroAsync(
		Player player,
		int macroId,
		string macroXml,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerService.addMacro.
		if (macroId is < 1 or > 12)
			return false;

		var macro = new PlayerMacro(macroId, macroXml);
		player.Macros = player.Macros
			.Where(existing => existing.Id != macroId)
			.Append(macro)
			.OrderBy(existing => existing.Id)
			.ToArray();
		return await _repository.SavePlayerMacroAsync(player.ObjectId, macro, cancellationToken);
	}

	public async Task<bool> DeleteMacroAsync(
		Player player,
		int macroId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerService.removeMacro.
		if (macroId is < 1 or > 12)
			return false;

		var beforeCount = player.Macros.Count;
		player.Macros = player.Macros
			.Where(existing => existing.Id != macroId)
			.OrderBy(existing => existing.Id)
			.ToArray();
		if (player.Macros.Count == beforeCount)
			return true;

		return await _repository.DeletePlayerMacroAsync(player.ObjectId, macroId, cancellationToken);
	}

	public Task<bool> SavePortalCooldownsAsync(
		Player player,
		long? nowMillis = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PortalCooldownsDAO.storePortalCooldowns called immediately by PortalCooldownList.addPortalCooldown.
		return _repository.SavePlayerPortalCooldownsAsync(player.ObjectId, player.PortalCooldowns, nowMillis, cancellationToken);
	}

	public async Task<bool> DeleteRecipeAsync(
		Player player,
		int recipeId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/gameobjects/player/RecipeList.deleteRecipe.
		if (!player.Recipes.Contains(recipeId))
			return false;

		if (!await _repository.DeletePlayerRecipeAsync(player.ObjectId, recipeId, cancellationToken))
			return false;

		player.Recipes = player.Recipes
			.Where(existing => existing != recipeId)
			.Order()
			.ToArray();
		return true;
	}

	public async Task<bool> DeleteInventoryItemAsync(
		Player player,
		int itemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: Storage.delete(item, ItemDeleteType.DISCARD) -> InventoryDAO.store removes the item row.
		return await _repository.DeleteInventoryItemAsync(player.ObjectId, itemObjectId, cancellationToken);
	}

	public Task<bool> SaveInventoryItemSlotAsync(
		Player player,
		int itemObjectId,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemMoveService.moveInSameStorage -> item.setEquipmentSlot -> InventoryDAO stores updated slot.
		return _repository.SaveInventoryItemSlotAsync(player.ObjectId, itemObjectId, newSlot, cancellationToken);
	}

	public Task<bool> SaveInventoryItemPackCountAsync(
		Player player,
		int itemObjectId,
		int newPackCount,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_UNWRAP_ITEM.runImpl -> item.setPackCount(-packCount) -> InventoryDAO stores updated pack_count.
		return _repository.SaveInventoryItemPackCountAsync(player.ObjectId, itemObjectId, newPackCount, cancellationToken);
	}

	public Task<bool> SaveItemSplitMutationAsync(
		Player player,
		InventoryItem sourceItem,
		InventoryItem newItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemSplitService.splitItem decreases source count and inserts new split item.
		return _repository.SaveItemSplitMutationAsync(player.ObjectId, sourceItem, newItem, cancellationToken);
	}

	public Task<bool> SaveItemMergeMutationAsync(
		Player player,
		InventoryItem sourceItem,
		InventoryItem targetItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemSplitService.mergeStacks decreases source and increases target count atomically.
		return _repository.SaveItemMergeMutationAsync(player.ObjectId, sourceItem, targetItem, cancellationToken);
	}

	public Task<bool> SaveItemCrossStorageMoveMutationAsync(
		Player player,
		int itemObjectId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemMoveService.moveItem cross-storage — item row item_location and slot updated.
		return _repository.SaveItemCrossStorageMoveMutationAsync(player.ObjectId, itemObjectId, newLocation, newSlot, cancellationToken);
	}

	public Task<bool> SaveItemUseSourceMutationAsync(
		Player player,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: Storage.decreaseByObjectId for item actions whose remaining side effects are runtime-only.
		return _repository.SaveItemUseSourceMutationAsync(
			player.ObjectId,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveCraftLearnActionMutationAsync(
		Player player,
		int recipeId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveCraftLearnActionMutationAsync(
			player.ObjectId,
			recipeId,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveEmotionLearnActionMutationAsync(
		Player player,
		PlayerEmotion emotion,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveEmotionLearnActionMutationAsync(
			player.ObjectId,
			emotion,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveTitleAddActionMutationAsync(
		Player player,
		PlayerTitle title,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveTitleAddActionMutationAsync(
			player.ObjectId,
			title,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveSkillLearnActionMutationAsync(
		Player player,
		IReadOnlyList<PlayerSkill> skills,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveSkillLearnActionMutationAsync(
			player.ObjectId,
			skills,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveInventoryExpansionMutationAsync(
		Player player,
		int itemExpands,
		int warehouseBonusExpands,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveInventoryExpansionMutationAsync(
			player.ObjectId,
			itemExpands,
			warehouseBonusExpands,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveDyeItemActionMutationAsync(
		Player player,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveDyeItemActionMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveAnimationAddActionMutationAsync(
		Player player,
		IReadOnlyList<PlayerMotion> motions,
		IReadOnlyList<int> deactivatedMotionIds,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveAnimationAddActionMutationAsync(
			player.ObjectId,
			motions,
			deactivatedMotionIds,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveCosmeticItemActionMutationAsync(
		Player player,
		Model.Account.CharacterAppearance appearance,
		int deletedItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return _repository.SaveCosmeticItemActionMutationAsync(
			player.ObjectId,
			appearance,
			deletedItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveDecomposeActionMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: DecomposeAction/CM_SELECT_DECOMPOSABLE inventory mutation.
		return _repository.SaveDecomposeActionMutationAsync(
			player.ObjectId,
			updatedItems,
			addedItems,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveAssemblyItemActionMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> updatedPartItems,
		IReadOnlyList<int> deletedPartObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: AssemblyItemAction item-id part consumption plus ItemService.addItem reward.
		return _repository.SaveAssemblyItemActionMutationAsync(
			player.ObjectId,
			updatedPartItems,
			deletedPartObjectIds,
			updatedRewardItems,
			addedRewardItems,
			cancellationToken);
	}

	public Task<bool> SaveExpExtractActionMutationAsync(
		Player player,
		long newExp,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExpExtractAction PlayerCommonData.exp/source consume/reward mutation.
		return _repository.SaveExpExtractActionMutationAsync(
			player.ObjectId,
			newExp,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			updatedRewardItems,
			addedRewardItems,
			cancellationToken);
	}

	public Task<bool> SaveCompositeStoneActionMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CM_COMPOSITE_STONES tool/stone consumption plus CompositionAction ItemService.addItem reward.
		return _repository.SaveAssemblyItemActionMutationAsync(
			player.ObjectId,
			updatedConsumedItems,
			deletedConsumedObjectIds,
			updatedRewardItems,
			addedRewardItems,
			cancellationToken);
	}

	public Task<bool> SavePortalRequirementConsumptionMutationAsync(
		Player player,
		PortalRequirementConsumptionApplication application,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PortalService.checkAndRemoveRequiredItems Storage.decreaseByItemId/decreaseKinah persistence side effects.
		if (!application.Applied)
			return Task.FromResult(false);
		if (application.UpdatedItems.Count == 0 && application.DeletedObjectIds.Count == 0)
			return Task.FromResult(true);

		return _repository.SaveAssemblyItemActionMutationAsync(
			player.ObjectId,
			application.UpdatedItems,
			application.DeletedObjectIds,
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			cancellationToken);
	}

	public Task<bool> SaveBreakItemActionMutationAsync(
		Player player,
		BreakItemPlan plan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExtractAction -> EnchantService.breakItem target delete, tool consume, then ItemService.addItem.
		IReadOnlyList<InventoryItem> updatedConsumedItems = plan.SourceItemUpdate == null ? Array.Empty<InventoryItem>() : [plan.SourceItemUpdate];
		var deletedConsumedObjectIds = plan.DeletedSourceItemObjectId.HasValue
			? new[] { plan.DeletedTargetItemObjectId, plan.DeletedSourceItemObjectId.Value }
			: [plan.DeletedTargetItemObjectId];
		return _repository.SaveAssemblyItemActionMutationAsync(
			player.ObjectId,
			updatedConsumedItems,
			deletedConsumedObjectIds,
			plan.UpdatedRewardItems,
			plan.AddedRewardItems,
			cancellationToken);
	}

	public Task<bool> SaveApExtractActionMutationAsync(
		Player player,
		ApExtractPlan plan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ApExtractAction deletes target, consumes extraction tool, then AbyssPointsService.addAp.
		return plan.AbyssRankUpdate == null
			? Task.FromResult(false)
			: _repository.SaveApExtractActionMutationAsync(
				player.ObjectId,
				plan.AbyssRankUpdate,
				plan.SourceItemUpdate,
				plan.DeletedSourceItemObjectId,
				plan.DeletedTargetItemObjectId,
				cancellationToken);
	}

	public Task<bool> SaveItemRemodelMutationAsync(
		Player player,
		InventoryItem targetItemUpdate,
		InventoryItem kinahItemUpdate,
		InventoryItem? extractItemUpdate,
		int? deletedExtractItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemRemodelService.remodelItem inventory side effects.
		return _repository.SaveItemRemodelMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			kinahItemUpdate,
			extractItemUpdate,
			deletedExtractItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveItemChargeMutationAsync(
		Player player,
		InventoryItem chargedItem,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemChargeService payment + ChargeInfo persistence side effects.
		return _repository.SaveItemChargeMutationAsync(player.ObjectId, chargedItem, kinahItem, abyssRank, cancellationToken);
	}

	public Task<bool> SaveItemChargeAllMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemChargeService.startChargingEquippedItems payment + chargeItems persistence.
		return _repository.SaveItemChargeAllMutationAsync(player.ObjectId, chargedItems, kinahItem, abyssRank, cancellationToken);
	}

	public Task<bool> SaveItemChargeBurnMutationAsync(
		Player player,
		ItemChargeBurnPlan plan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/items/ChargeInfo.updateChargePoints persists observer-driven charge changes on conditioned items.
		var chargedItems = plan.Burns
			.Select(burn => burn.ItemUpdate)
			.ToArray();
		return chargedItems.Length == 0
			? Task.FromResult(true)
			: _repository.SaveItemChargeBurnMutationAsync(player.ObjectId, chargedItems, cancellationToken);
	}

	public Task<bool> SaveIdianPolishMutationAsync(
		Player player,
		InventoryItem? targetItem,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ItemStoneListDAO.storeIdianStones and inventory decreaseByObjectId side effects.
		return _repository.SaveIdianPolishMutationAsync(
			player.ObjectId,
			targetItem,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveIdianPolishBurnMutationAsync(
		Player player,
		IdianPolishBurnPlan plan,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/items/IdianStone.decreasePolishCharge only persists ItemStoneListDAO deletion when charge reaches zero.
		var exhaustedItemUpdates = plan.Burns
			.Where(burn => burn.UpdateKind == IdianPolishBurnUpdateKind.Exhausted)
			.Select(burn => burn.ItemUpdate)
			.ToArray();
		return exhaustedItemUpdates.Length == 0
			? Task.FromResult(true)
			: _repository.SaveIdianPolishBurnMutationAsync(player.ObjectId, exhaustedItemUpdates, cancellationToken);
	}

	public Task<bool> SaveItemChargeActionMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/templates/item/actions/ChargeAction delayed consume + services/item/ItemChargeService.chargeItems.
		return _repository.SaveItemChargeActionMutationAsync(
			player.ObjectId,
			chargedItems,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveStigmaChargeMutationAsync(
		Player player,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/StigmaService.chargeStigma inventory/enchant persistence.
		return _repository.SaveStigmaChargeMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			deletedTargetItemObjectId,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveManastoneRemovalMutationAsync(
		Player player,
		int itemObjectId,
		int slot,
		int category,
		InventoryItem kinahItemUpdate,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemSocketService.removeManastone persistence side effects.
		return _repository.SaveManastoneRemovalMutationAsync(
			player.ObjectId,
			itemObjectId,
			slot,
			category,
			kinahItemUpdate,
			cancellationToken);
	}

	public Task<bool> SaveManastoneSocketMutationAsync(
		Player player,
		InventoryItem targetItemUpdate,
		ItemStoneSocket? addedStone,
		int addedCategory,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.socketManastoneAct supplement consume, source consume, and item_stones insert persistence.
		return _repository.SaveManastoneSocketMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			addedStone,
			addedCategory,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			supplementItemUpdates,
			deletedSupplementItemObjectIds,
			cancellationToken);
	}

	public Task<bool> SaveEnchantItemMutationAsync(
		Player player,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.enchantItemAct source/supplement consume and target enchant persistence.
		return _repository.SaveEnchantItemMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			deletedTargetItemObjectId,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			supplementItemUpdates,
			deletedSupplementItemObjectIds,
			cancellationToken);
	}

	public Task<bool> SaveGodstoneSocketMutationAsync(
		Player player,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemSocketService.socketGodstone completion persistence.
		return _repository.SaveGodstoneSocketMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveItemAmplificationMutationAsync(
		Player player,
		InventoryItem targetItemUpdate,
		InventoryItem? materialItemUpdate,
		int? deletedMaterialItemObjectId,
		InventoryItem? toolItemUpdate,
		int? deletedToolItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.amplifyItem target amplification and source-consume persistence.
		return _repository.SaveItemAmplificationMutationAsync(
			player.ObjectId,
			targetItemUpdate,
			materialItemUpdate,
			deletedMaterialItemObjectId,
			toolItemUpdate,
			deletedToolItemObjectId,
			cancellationToken);
	}

	public Task<bool> SaveEquipmentMutationAsync(
		Player player,
		IReadOnlyList<InventoryItem> items,
		InventoryItem? kinahItem = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/gameobjects/player/Equipment.setPersistentState(UPDATE_REQUIRED) persisted by InventoryDAO.store.
		return _repository.SaveEquipmentMutationAsync(player.ObjectId, items, kinahItem, cancellationToken);
	}

	public Task<bool> SavePowerShardUseMutationAsync(
		Player player,
		IReadOnlyList<PowerShardUseResult> powerShardUses,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/gameobjects/player/Equipment.decreaseEquippedItemCount and usePowerShard persistence.
		return _repository.SavePowerShardUseMutationAsync(
			player.ObjectId,
			powerShardUses.SelectMany(use => use.CountUpdateItems).ToArray(),
			powerShardUses.SelectMany(use => use.EquipUpdateItems).ToArray(),
			powerShardUses.SelectMany(use => use.DeletedItemObjectIds).Distinct().ToArray(),
			cancellationToken);
	}

	public async Task<PortalEntryPreparationResult> PreparePortalEntryAsync(
		Player player,
		PortalPathSummary portalPath,
		PortalLocTable portalLocs,
		InstanceCooltimeTable instanceCooltimes,
		WorldMapRuntimeStateTable worldMaps,
		ItemTemplateTable itemTemplates,
		DateTimeOffset now,
		int npcObjectId = 0,
		bool adminBypassRequirements = false,
		bool bypassLevelRequirement = false,
		bool bypassRaceRequirement = false,
		bool bypassTitleRequirement = false,
		bool bypassQuestRequirement = false,
		bool bypassGroupRequirement = false,
		bool siegeOwnerMatchesPlayerRace = true,
		bool npcIsDialogNpc = true,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/teleport/PortalService.port supported solo/open-world guard + required-item side-effect boundary.
		var effectiveBypassGroupRequirement = bypassGroupRequirement || HasInstanceGroupRequirementBypass(player);
		var entryPlan = PortalEntryValidationService.ValidatePortalEntryPlan(
			player,
			portalPath,
			portalLocs,
			instanceCooltimes,
			worldMaps,
			now,
			npcObjectId,
			adminBypassRequirements,
			bypassLevelRequirement,
			bypassRaceRequirement,
			bypassTitleRequirement,
			bypassQuestRequirement,
			effectiveBypassGroupRequirement,
			siegeOwnerMatchesPlayerRace,
			npcIsDialogNpc);
		if (!entryPlan.CanEnter)
			return entryPlan.TeamPlan == null
				? PortalEntryPreparationResult.Rejected(entryPlan)
				: PortalEntryPreparationResult.UnsupportedTeamPortal(entryPlan);
		if (entryPlan.Reenter)
			return PortalEntryPreparationResult.Ready(entryPlan, null, Array.Empty<GameServerPacket>());

		var consumptionPlan = PortalEntryValidationService.CreateRequiredItemsAndKinahConsumptionPlan(player, portalPath);
		var application = PortalEntryValidationService.CreateRequiredItemsAndKinahApplication(player, consumptionPlan, itemTemplates);
		if (!application.Applied)
			return PortalEntryPreparationResult.ApplicationFailed(entryPlan, application);

		var persisted = await SavePortalRequirementConsumptionMutationAsync(player, application, cancellationToken);
		if (!persisted)
			return PortalEntryPreparationResult.PersistenceFailed(entryPlan, application);

		player.InventoryItems = application.InventoryItems;
		return PortalEntryPreparationResult.Ready(entryPlan, application, application.Packets);
	}

	private bool HasInstanceGroupRequirementBypass(Player player)
	{
		// Java parity: PortalService.port sets instanceGroupReq false when
		// Player.hasAccess(AdminConfig.INSTANCE_ENTER_ALL) or Player.hasPermission(MembershipConfig.INSTANCES_GROUP_REQ).
		return player.AccessLevel >= _options.Administration.InstanceEnterAllAccessLevel
			|| player.AccountMembership >= _options.Membership.InstancesGroupRequirement;
	}

	public async Task LeaveWorldAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerLeaveWorldService.leaveWorld baseline persistence.
		RecordFindGroupLogoutCleanup(player);
		await ClearPendingQuestionResponsesAsync(player);
		RecordLogoutRepurchaseStateRemoval(player);
		var lastOnline = DateTime.Now;
		player.IsOnline = false;
		player.LastOnline = lastOnline;
		if (_world.TryRemoveObject(player.ObjectId, out _))
			ClearPlayerCreaturePvpZones(player.ObjectId);
		var saved = await _repository.SavePlayerLogoutAsync(player, lastOnline, cancellationToken);
		RecordGroupLogoutLastOnline(player, lastOnline);
		await DispatchGroupDisconnectedLogoutAsync(player);
		await DispatchAllianceDisconnectedLogoutAsync(player);
		if (saved)
			_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) logged off", player.Name, player.ObjectId);
		else
			_logger.LogWarning("Player {PlayerName} ({PlayerObjectId}) logout state was not fully persisted", player.Name, player.ObjectId);
	}

	private async Task DispatchGroupDisconnectedLogoutAsync(Player player)
	{
		if (_playerGroupRuntime == null || _connectionRegistry == null)
			return;

		// Java parity: PlayerGroupService.onPlayerLogout fires PlayerDisconnectedEvent after
		// PlayerLeaveWorldService sets the connection to null. The disconnected player is
		// semi-offline, so PacketSendUtility.sendPacket(player, ...) is a no-op.
		var plan = new PlayerGroupDisconnectedPlanner(_playerGroupRuntime).Plan(player);
		if (plan.Status == PlayerGroupDisconnectedPlanStatus.NoOnlineMembersDisband)
		{
			_playerGroupRuntime.DisbandAfterDisconnectedNoOnlineMembers(plan.TeamId);
			return;
		}

		if (!plan.IsPlanned)
			return;

		var membersByObjectId = _playerGroupRuntime
			.GetMemberPlayers(plan.TeamId)
			.ToDictionary(member => member.ObjectId);
		var leaderChangePlan = plan.LeaderChangePlan;
		if (plan.FallbackLeaderObjectId.HasValue)
			leaderChangePlan = _playerGroupRuntime.ChangeLeader(plan.TeamId, plan.FallbackLeaderObjectId.Value) ?? leaderChangePlan;

		if (leaderChangePlan != null)
		{
			foreach (var intent in leaderChangePlan.PacketIntents)
			{
				if (ShouldSkipTeamLogoutRecipient(intent.RecipientObjectId, membersByObjectId, player.ObjectId))
					continue;

				await _connectionRegistry.SendPacketToPlayerAsync(
					intent.RecipientObjectId,
					new SmGroupInfo(intent.GroupInfoPlan));
				await _connectionRegistry.SendPacketToPlayerAsync(
					intent.RecipientObjectId,
					intent.SystemMessage);
			}
		}

		foreach (var intent in plan.PacketIntents)
		{
			if (ShouldSkipTeamLogoutRecipient(intent.RecipientObjectId, membersByObjectId, player.ObjectId))
				continue;

			await _connectionRegistry.SendPacketToPlayerAsync(
				intent.RecipientObjectId,
				intent.CreatePacket());
		}
	}

	private async Task DispatchAllianceDisconnectedLogoutAsync(Player player)
	{
		if (_playerAllianceRuntime == null || _connectionRegistry == null)
			return;

		// Java parity: PlayerAllianceService.onPlayerLogout updates last-online, then
		// PlayerDisconnectedEvent optionally changes leader, fans out offline packets,
		// and only then disbands when no online members remain.
		var snapshot = _playerAllianceRuntime.Resolve(player);
		if (snapshot == null)
			return;

		var allianceId = snapshot.AllianceId;
		var descriptor = _playerAllianceRuntime.GetDescriptor(allianceId);
		var members = _playerAllianceRuntime.GetMemberPlayers(allianceId);
		if (descriptor == null || members.Count == 0 || members.All(member => member.ObjectId != player.ObjectId))
			return;

		var membersByObjectId = members.ToDictionary(member => member.ObjectId);
		var noOnlineMembersRemain = !members.Any(member => member.IsOnline);
		var isInLeague = snapshot.LeagueId != 0;

		if (descriptor.LeaderObjectId == player.ObjectId)
		{
			var fallbackLeaderObjectId = _playerAllianceRuntime.SelectFallbackLeaderObjectId(allianceId, player.ObjectId);
			if (fallbackLeaderObjectId.HasValue)
			{
				var leaderChangePlan = _playerAllianceRuntime.ChangeLeader(
					allianceId,
					fallbackLeaderObjectId.Value,
					eventPlayerWasSpecified: false);
				if (leaderChangePlan != null)
				{
					PlayerLeagueLeaderChangeTimeoutPlan? leagueTimeoutPlan = null;
					if (isInLeague && _playerLeagueRuntime != null)
					{
						var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfo(
							snapshot.LeagueId,
							skippedPlayerObjectId: null,
							_playerAllianceRuntime);
						if (leagueBroadcastPlan != null)
							await DispatchLeagueLogoutPacketsAsync(leagueBroadcastPlan.PacketIntents, player.ObjectId);

						leagueTimeoutPlan = _playerLeagueRuntime.CreateAllianceLeaderChangeTimeoutPlan(
							snapshot.LeagueId,
							allianceId,
							leaderChangePlan.NewLeaderObjectId,
							membersByObjectId.GetValueOrDefault(leaderChangePlan.NewLeaderObjectId)?.Name ?? string.Empty,
							members.Select(member => member.ObjectId).ToArray(),
							_playerAllianceRuntime);
					}

					await DispatchAllianceLeaderChangeAsync(
						leaderChangePlan,
						members,
						membersByObjectId,
						player.ObjectId,
						leagueTimeoutPlan);
				}

				snapshot = _playerAllianceRuntime.GetSnapshot(allianceId) ?? snapshot;
				descriptor = _playerAllianceRuntime.GetDescriptor(allianceId) ?? descriptor;
				members = _playerAllianceRuntime.GetMemberPlayers(allianceId);
				membersByObjectId = members.ToDictionary(member => member.ObjectId);
			}
		}

		var plan = new PlayerAllianceDisconnectedPlanner().CreateDisconnectedPlan(
			allianceId,
			descriptor.LeaderObjectId,
			members,
			snapshot.ViceCaptainObjectIds,
			player.ObjectId,
			descriptor.LootRules,
			descriptor.TeamType,
			isInLeague,
			noOnlineMembersRemain);

		if (plan.Status == PlayerAllianceDisconnectedPlanStatus.Planned)
		{
			var leagueAllianceInfoByRecipient = isInLeague && !noOnlineMembersRemain && _playerLeagueRuntime != null
				? CreateDisconnectedLeagueAllianceInfoByRecipient(plan, snapshot.LeagueId)
				: null;
			foreach (var intent in plan.PacketIntents)
			{
				if (ShouldSkipAllianceLogoutRecipient(intent.RecipientObjectId, membersByObjectId, player.ObjectId))
					continue;

				await _connectionRegistry.SendPacketToPlayerAsync(
					intent.RecipientObjectId,
					CreateAllianceLogoutPacket(intent, leagueAllianceInfoByRecipient));
			}
		}

		if (noOnlineMembersRemain)
		{
			var removedLeaderName = membersByObjectId.GetValueOrDefault(descriptor.LeaderObjectId)?.Name ?? player.Name;
			var disbandPlan = _playerAllianceRuntime.DisbandAfterDisconnectedNoOnlineMembers(allianceId);
			if (disbandPlan is { WouldNotifyLeagueAfterDisband: true } && _playerLeagueRuntime != null)
			{
				var leagueLeavePlan = _playerLeagueRuntime.RemoveAllianceAfterAllianceDisband(
					allianceId,
					removedLeaderName,
					_playerAllianceRuntime);
				if (leagueLeavePlan != null)
					await DispatchLeagueLogoutPacketsAsync(leagueLeavePlan.PacketIntents, player.ObjectId);
			}
		}
		else if (isInLeague && _playerLeagueRuntime != null)
		{
			var leagueBroadcastPlan = _playerLeagueRuntime.BroadcastAllianceInfo(
				snapshot.LeagueId,
				skippedPlayerObjectId: player.ObjectId,
				_playerAllianceRuntime);
			if (leagueBroadcastPlan != null)
				await DispatchLeagueLogoutPacketsAsync(leagueBroadcastPlan.PacketIntents, player.ObjectId);
		}
	}

	private IReadOnlyDictionary<int, GameServerPacket>? CreateDisconnectedLeagueAllianceInfoByRecipient(
		PlayerAllianceDisconnectedPlan plan,
		int leagueId)
	{
		// Java parity: PlayerDisconnectedEvent sends new SM_ALLIANCE_INFO(alliance) to remaining alliance members.
		// If the alliance is in a league, the Java packet constructor expands the real league id and league rows.
		var leagueInfoPlan = _playerLeagueRuntime?.CreateAllianceInfoFanout(
			leagueId,
			plan.AllianceId,
			messageId: 0,
			message: string.Empty,
			_playerAllianceRuntime!);
		return leagueInfoPlan?.PacketIntents
			.Where(intent => intent.Kind == PlayerLeaguePacketIntentKind.AllianceInfo)
			.ToDictionary(intent => intent.RecipientObjectId, intent => intent.CreatePacket());
	}

	private static GameServerPacket CreateAllianceLogoutPacket(
		PlayerAlliancePacketIntent intent,
		IReadOnlyDictionary<int, GameServerPacket>? leagueAllianceInfoByRecipient)
	{
		if (intent.Kind == PlayerAlliancePacketIntentKind.AllianceInfo
			&& leagueAllianceInfoByRecipient != null
			&& leagueAllianceInfoByRecipient.TryGetValue(intent.RecipientObjectId, out var packet))
			return packet;

		return intent.CreatePacket();
	}

	private async Task DispatchLeagueLogoutPacketsAsync(
		IReadOnlyList<PlayerLeaguePacketIntent> packetIntents,
		int disconnectedPlayerObjectId)
	{
		if (_playerAllianceRuntime == null || _connectionRegistry == null)
			return;

		foreach (var intent in packetIntents.OrderBy(intent => intent.Sequence))
		{
			var member = _playerAllianceRuntime.GetMember(intent.AllianceId, intent.RecipientObjectId);
			if (member == null || member.ObjectId == disconnectedPlayerObjectId || !member.IsOnline)
				continue;

			await _connectionRegistry.SendPacketToPlayerAsync(
				intent.RecipientObjectId,
				intent.CreatePacket());
		}
	}

	private async Task DispatchAllianceLeaderChangeAsync(
		PlayerAllianceLeaderChangePlan plan,
		IReadOnlyList<Player> members,
		IReadOnlyDictionary<int, Player> membersByObjectId,
		int disconnectedPlayerObjectId,
		PlayerLeagueLeaderChangeTimeoutPlan? leagueTimeoutPlan = null)
	{
		var allianceInfoIntents = plan.AllianceInfoIntents.ToDictionary(intent => intent.RecipientObjectId);
		var systemMessageIntentsByRecipient = plan.SystemMessageIntents
			.GroupBy(intent => intent.RecipientObjectId)
			.ToDictionary(group => group.Key, group => group.ToArray());
		var leagueTimeoutIntentsByTrigger = leagueTimeoutPlan?.TimeoutIntents
			.GroupBy(intent => intent.TriggeringChangedAllianceMemberObjectId)
			.ToDictionary(group => group.Key, group => group.ToArray())
			?? [];
		foreach (var member in members)
		{
			var skipLocalRecipient = ShouldSkipAllianceLogoutRecipient(member.ObjectId, membersByObjectId, disconnectedPlayerObjectId);
			if (!skipLocalRecipient && allianceInfoIntents.TryGetValue(member.ObjectId, out var allianceInfoIntent))
			{
				await _connectionRegistry!.SendPacketToPlayerAsync(
					allianceInfoIntent.RecipientObjectId,
					allianceInfoIntent.CreatePacket());
			}

			if (!skipLocalRecipient && systemMessageIntentsByRecipient.TryGetValue(member.ObjectId, out var systemMessageIntents))
			{
				foreach (var intent in systemMessageIntents)
				{
					await _connectionRegistry!.SendPacketToPlayerAsync(
						intent.RecipientObjectId,
						intent.Message);
				}
			}

			if (leagueTimeoutIntentsByTrigger.TryGetValue(member.ObjectId, out var leagueTimeoutIntents))
				await DispatchLeagueLogoutPacketsAsync(
					leagueTimeoutIntents.Select(intent => intent.PacketIntent).ToArray(),
					disconnectedPlayerObjectId);
		}
	}

	private static bool ShouldSkipAllianceLogoutRecipient(
		int recipientObjectId,
		IReadOnlyDictionary<int, Player> membersByObjectId,
		int disconnectedPlayerObjectId)
	{
		if (recipientObjectId == disconnectedPlayerObjectId)
			return true;

		return !membersByObjectId.TryGetValue(recipientObjectId, out var recipient) || !recipient.IsOnline;
	}

	private static bool ShouldSkipTeamLogoutRecipient(
		int recipientObjectId,
		IReadOnlyDictionary<int, Player> membersByObjectId,
		int disconnectedPlayerObjectId)
	{
		if (recipientObjectId == disconnectedPlayerObjectId)
			return true;

		return !membersByObjectId.TryGetValue(recipientObjectId, out var recipient) || !recipient.IsOnline;
	}

	private void RecordGroupLogoutLastOnline(Player player, DateTime lastOnline)
	{
		// Java parity: PlayerLeaveWorldService.leaveWorld calls PlayerGroupService.onPlayerLogout
		// after the immediate logout persistence band; PlayerGroupService updates the member's
		// last-online timestamp before PlayerDisconnectedEvent fanout.
		_playerGroupRuntime?.UpdateMemberLastOnlineTime(player, new DateTimeOffset(lastOnline));
		_playerAllianceRuntime?.UpdateMemberLastOnlineTime(player, new DateTimeOffset(lastOnline));
	}

	private void RecordFindGroupLogoutCleanup(Player player)
	{
		if (_findGroupService == null && _findGroupLogoutCleanupPlanObserver == null)
			return;

		// Java parity: PlayerLeaveWorldService.leaveWorld calls FindGroupService.onLogout(player)
		// before ResponseRequester.denyAll and before broader logout side effects. This hook records
		// the disabled cleanup plan only; it does not send packets or enable live CM_FIND_GROUP dispatch.
		var findGroupService = _findGroupService ?? new FindGroupRecruitmentPlanService();
		var plan = findGroupService.OnLogout(player);
		_findGroupLogoutCleanupPlanObserver?.Invoke(plan);
	}

	private void RecordLogoutRepurchaseStateRemoval(Player player)
	{
		if (_repurchaseStateRemovePlanObserver == null)
			return;

		// Java parity: PlayerLeaveWorldService.leaveWorld calls
		// RepurchaseService.removeRepurchaseItems(player). Empty supplied player
		// facts cannot distinguish an absent map key from an empty Java set.
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots;
		if (player.RepurchaseItems.Count == 0)
		{
			currentSnapshots = Array.Empty<RepurchaseStateSnapshot>();
		}
		else
		{
			currentSnapshots =
			[
				new RepurchaseStateSnapshot(
					player.ObjectId,
					player.RepurchaseItems,
					"PlayerLeaveWorldService.leaveWorld supplied Player.RepurchaseItems for disabled RepurchaseService.removeRepurchaseItems cleanup"),
			];
		}

		_repurchaseStateRemovePlanObserver(RepurchaseStatePlanService.CreateRemoveDisabledPlan(
			player.ObjectId,
			currentSnapshots));
	}

	private bool IsInsideReentryWindow(DateTime? lastOnline)
	{
		// Java parity: PlayerEnterWorldService lastOnline vs GSConfig.CHARACTER_REENTRY_TIME check.
		return lastOnline.HasValue
			&& DateTime.Now - lastOnline.Value < TimeSpan.FromSeconds(_options.Core.CharacterReentryTimeSeconds);
	}

	private async Task ClearPendingQuestionResponsesAsync(Player player)
	{
		// Java parity: PlayerLeaveWorldService.leaveWorld calls player.getResponseRequester().denyAll().
		// Java ResponseRequester.denyAll invokes each handler with response 0; execute the
		// migrated per-kind denial side effects we can represent before clearing C# bridge slots.
		var deniedRequests = player.ResponseRequester.DenyAll();
		foreach (var dispatch in deniedRequests)
			await SendPendingQuestionDenySideEffectAsync(player, dispatch);

		// Typed adapter slots are C# bridge state for migrated question handlers and must be
		// cleared with the registry on logout.
		player.PendingFriendRequest = null;
		player.PendingChargeAllRequest = null;
		player.PendingSoulBindRequest = null;
		player.PendingRiftPortalRequest = null;
		player.PendingKiskBindRequest = null;
		player.PendingLeagueInviteRequest = null;
		player.PendingAllianceInviteRequest = null;
		player.PendingDuelRequest = null;
		player.PendingDuelWithdrawRequest = null;
		player.PendingExperienceRecoveryRequest = null;
		player.PendingExchangeRequest = null;
		player.PendingRecallInstantRequest = null;
		player.PendingCraftSkillLearnRequest = null;
		player.PendingStorageExpansionRequest = null;
		player.IsTrading = false;
		player.IsExchangeLocked = false;
		player.IsExchangeConfirmed = false;
		player.CurrentExchangePartnerObjectId = 0;
	}

	private async Task SendPendingQuestionDenySideEffectAsync(Player responder, QuestionResponseDispatch dispatch)
	{
		if (_connectionRegistry == null)
			return;

		switch (dispatch.Request.Kind)
		{
			case QuestionResponseRequestKind.FriendInvite:
			{
				var request = dispatch.Request.Payload as PendingFriendRequest ?? responder.PendingFriendRequest;
				var requesterObjectId = request?.RequesterObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						new SmFriendResponse(SmFriendResponse.TargetDenied, responder.Name));
				}
				break;
			}
			case QuestionResponseRequestKind.LeagueInvite:
			{
				var request = dispatch.Request.Payload as PendingLeagueInviteRequest ?? responder.PendingLeagueInviteRequest;
				var requesterObjectId = request?.RequesterObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						SmSystemMessage.PartyAllianceHeRejectInvitation(responder.Name));
				}
				break;
			}
			case QuestionResponseRequestKind.AllianceInvite:
			{
				var request = dispatch.Request.Payload as PendingAllianceInviteRequest ?? responder.PendingAllianceInviteRequest;
				var requesterObjectId = request?.RequesterObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						SmSystemMessage.PartyAllianceHeRejectInvitation(responder.Name));
				}
				break;
			}
			case QuestionResponseRequestKind.DuelRequest:
			{
				var request = dispatch.Request.Payload as PendingDuelRequest ?? responder.PendingDuelRequest;
				var requesterObjectId = request?.RequesterObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						SmCloseQuestionWindow.DuelHeRejectDuel(responder.Name));
				}
				break;
			}
			case QuestionResponseRequestKind.SoulBind:
			{
				var request = dispatch.Request.Payload as PendingSoulBindRequest ?? responder.PendingSoulBindRequest;
				if (request != null)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						responder.ObjectId,
						SmSystemMessage.SoulBoundItemCanceled(request.ItemName));
				}
				break;
			}
			case QuestionResponseRequestKind.ExchangeRequest:
			{
				var request = dispatch.Request.Payload as PendingExchangeRequest ?? responder.PendingExchangeRequest;
				var requesterObjectId = request?.RequesterObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						SmSystemMessage.ExchangeHeRejectedExchange(responder.Name));
				}
				break;
			}
			case QuestionResponseRequestKind.RecallInstant:
			{
				var request = dispatch.Request.Payload as PendingRecallInstantRequest ?? responder.PendingRecallInstantRequest;
				var requesterObjectId = request?.EffectorObjectId ?? dispatch.Request.RequesterObjectId;
				if (requesterObjectId > 0)
				{
					await _connectionRegistry.SendPacketToPlayerAsync(
						requesterObjectId,
						SmSystemMessage.RecallRejectedEffect(responder.Name));
				}
				break;
			}
		}
	}

	private void ClearPlayerCreaturePvpZones(int playerObjectId)
	{
		// Java parity: PlayerEnterWorldService failure/logout paths delete the player controller and leave map-region zone memberships.
		_creaturePvpZoneCounterService?.ClearCounters(playerObjectId);
	}

	private static int CurrentEpochSeconds()
	{
		var now = DateTimeOffset.Now.ToUnixTimeSeconds();
		return now > int.MaxValue ? int.MaxValue : (int)now;
	}

	private async ValueTask ApplyOfflineDpResetAsync(Player player, DateTime? previousLastOnline, DateTime now)
	{
		// Java parity: services/player/PlayerEnterWorldService.enterWorld -> PlayerCommonData.setDp(0) after >5 minutes offline.
		if (!previousLastOnline.HasValue
			|| now - previousLastOnline.Value <= TimeSpan.FromMinutes(5)
			|| IsStartingClass(player.PlayerClass))
			return;

		if (_resourceStats == null)
		{
			player.Dp = 0;
			return;
		}

		await _resourceStats.AddPlayerDpAsync(player, -player.Dp, maxDp: Math.Max(0, player.Dp));
	}

	private static bool IsStartingClass(string playerClass)
	{
		return string.Equals(playerClass, "WARRIOR", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(playerClass, "SCOUT", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(playerClass, "MAGE", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(playerClass, "PRIEST", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(playerClass, "TECHNIST", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(playerClass, "MUSE", StringComparison.OrdinalIgnoreCase);
	}
}

public sealed record PortalEntryPreparationResult(
	bool CanEnter,
	PortalEntryPreparationStatus Status,
	PortalEntryPlanResult EntryPlan,
	PortalRequirementConsumptionApplication? RequirementApplication,
	IReadOnlyList<GameServerPacket> Packets)
{
	public static PortalEntryPreparationResult Ready(
		PortalEntryPlanResult entryPlan,
		PortalRequirementConsumptionApplication? requirementApplication,
		IReadOnlyList<GameServerPacket> packets)
	{
		return new PortalEntryPreparationResult(
			true,
			PortalEntryPreparationStatus.Ready,
			entryPlan,
			requirementApplication,
			packets);
	}

	public static PortalEntryPreparationResult Rejected(PortalEntryPlanResult entryPlan)
	{
		return new PortalEntryPreparationResult(
			false,
			PortalEntryPreparationStatus.ValidationRejected,
			entryPlan,
			null,
			Array.Empty<GameServerPacket>());
	}

	public static PortalEntryPreparationResult UnsupportedTeamPortal(PortalEntryPlanResult entryPlan)
	{
		return new PortalEntryPreparationResult(
			false,
			PortalEntryPreparationStatus.UnsupportedTeamPortal,
			entryPlan,
			null,
			Array.Empty<GameServerPacket>());
	}

	public static PortalEntryPreparationResult ApplicationFailed(
		PortalEntryPlanResult entryPlan,
		PortalRequirementConsumptionApplication application)
	{
		return new PortalEntryPreparationResult(
			false,
			PortalEntryPreparationStatus.RequirementApplicationFailed,
			entryPlan,
			application,
			Array.Empty<GameServerPacket>());
	}

	public static PortalEntryPreparationResult PersistenceFailed(
		PortalEntryPlanResult entryPlan,
		PortalRequirementConsumptionApplication application)
	{
		return new PortalEntryPreparationResult(
			false,
			PortalEntryPreparationStatus.RequirementPersistenceFailed,
			entryPlan,
			application,
			Array.Empty<GameServerPacket>());
	}
}

public enum PortalEntryPreparationStatus
{
	Ready,
	ValidationRejected,
	UnsupportedTeamPortal,
	RequirementApplicationFailed,
	RequirementPersistenceFailed,
}

public static class PlayerLogoutCraftCooldownSavePlanService
{
	public static PlayerLogoutCraftCooldownSavePlan CreateDisabledPlan(Player? player, long currentTimeMillis)
	{
		if (player == null)
			return PlayerLogoutCraftCooldownSavePlan.PlayerMissing(currentTimeMillis);

		var persistencePlan = CraftCooldownPersistencePlanService.CreateDisabledPlan(
			player.ObjectId,
			player.CraftCooldowns,
			currentTimeMillis);
		var adapterPlan = CraftCooldownPersistenceAdapterPlanService.CreateDisabledPlan(persistencePlan);
		return PlayerLogoutCraftCooldownSavePlan.Create(player.ObjectId, currentTimeMillis, persistencePlan, adapterPlan);
	}
}

public sealed record PlayerLogoutCraftCooldownSavePlan(
	PlayerLogoutCraftCooldownSavePlanStatus Status,
	int PlayerObjectId,
	long CurrentTimeMillis,
	CraftCooldownPersistencePlan? PersistencePlan,
	CraftCooldownPersistenceAdapterPlan? AdapterPlan,
	int JavaStoreOrderAfterPortalCooldowns,
	int JavaStoreOrderBeforeHouseObjectCooldowns,
	int JavaConnectionOpenCount,
	bool JavaDeletesBeforeInserts,
	bool JavaSwallowsDeleteSqlExceptions,
	bool JavaSwallowsInsertSqlExceptions,
	bool WouldPersistCraftCooldowns,
	bool DidPersistCraftCooldowns,
	string JavaSource,
	bool IsLive)
{
	public static PlayerLogoutCraftCooldownSavePlan PlayerMissing(long currentTimeMillis)
	{
		return new PlayerLogoutCraftCooldownSavePlan(
			PlayerLogoutCraftCooldownSavePlanStatus.PlayerMissing,
			PlayerObjectId: 0,
			currentTimeMillis,
			PersistencePlan: null,
			AdapterPlan: null,
			JavaStoreOrderAfterPortalCooldowns: 0,
			JavaStoreOrderBeforeHouseObjectCooldowns: 0,
			JavaConnectionOpenCount: 0,
			JavaDeletesBeforeInserts: false,
			JavaSwallowsDeleteSqlExceptions: false,
			JavaSwallowsInsertSqlExceptions: false,
			WouldPersistCraftCooldowns: false,
			DidPersistCraftCooldowns: false,
			"PlayerService.storePlayer -> CraftCooldownsDAO.storeCraftCooldowns skipped because player is missing",
			IsLive: false);
	}

	public static PlayerLogoutCraftCooldownSavePlan Create(
		int playerObjectId,
		long currentTimeMillis,
		CraftCooldownPersistencePlan persistencePlan,
		CraftCooldownPersistenceAdapterPlan adapterPlan)
	{
		var status = persistencePlan.Status == CraftCooldownPersistencePlanStatus.DisabledNoWrite
			&& adapterPlan.Status == CraftCooldownPersistenceAdapterStatus.DisabledNoWrite
				? PlayerLogoutCraftCooldownSavePlanStatus.DisabledNoWrite
				: PlayerLogoutCraftCooldownSavePlanStatus.PersistencePlanNotReady;
		return new PlayerLogoutCraftCooldownSavePlan(
			status,
			playerObjectId,
			currentTimeMillis,
			persistencePlan,
			adapterPlan,
			JavaStoreOrderAfterPortalCooldowns: 1,
			JavaStoreOrderBeforeHouseObjectCooldowns: 1,
			JavaConnectionOpenCount: persistencePlan.SqlDescriptors.Count,
			JavaDeletesBeforeInserts: persistencePlan.DeleteDescriptorCount > 0,
			JavaSwallowsDeleteSqlExceptions: true,
			JavaSwallowsInsertSqlExceptions: true,
			WouldPersistCraftCooldowns: adapterPlan.WouldExecuteSql,
			DidPersistCraftCooldowns: false,
			"PlayerService.storePlayer calls CraftCooldownsDAO.storeCraftCooldowns after PortalCooldownsDAO.storePortalCooldowns and before HouseObjectCooldownsDAO.storeHouseObjectCooldowns; CraftCooldownsDAO deletes first, then opens one connection per active insert and logs SQLException without propagating",
			IsLive: false);
	}
}

public enum PlayerLogoutCraftCooldownSavePlanStatus
{
	DisabledNoWrite,
	PlayerMissing,
	PersistencePlanNotReady,
}

public static class PlayerLogoutCraftCooldownLiveReadinessPlanService
{
	public static PlayerLogoutCraftCooldownLiveReadinessPlan CreatePlan(
		PlayerLogoutCraftCooldownSavePlan? savePlan,
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision,
		bool repositoryMethodAvailable,
		bool logoutSaveHookAvailable,
		bool databaseIntegrationTestAvailable)
	{
		if (savePlan == null)
			return PlayerLogoutCraftCooldownLiveReadinessPlan.NotReady(
				savePlan,
				connectionDecision,
				errorDecision,
				repositoryMethodAvailable,
				logoutSaveHookAvailable,
				databaseIntegrationTestAvailable,
				new[] { PlayerLogoutCraftCooldownLiveReadinessCriterion.SavePlanAvailable });

		var missingCriteria = new List<PlayerLogoutCraftCooldownLiveReadinessCriterion>();
		if (savePlan.Status != PlayerLogoutCraftCooldownSavePlanStatus.DisabledNoWrite)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.SavePlanReady);
		if (connectionDecision == PlayerLogoutCraftCooldownConnectionDecision.Unspecified)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.ConnectionBehaviorDecided);
		if (errorDecision == PlayerLogoutCraftCooldownErrorDecision.Unspecified)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.ErrorBehaviorDecided);
		if (!repositoryMethodAvailable)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.RepositoryMethodAvailable);
		if (!logoutSaveHookAvailable)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.LogoutSaveHookAvailable);
		if (!databaseIntegrationTestAvailable)
			missingCriteria.Add(PlayerLogoutCraftCooldownLiveReadinessCriterion.DatabaseIntegrationTestAvailable);

		return missingCriteria.Count == 0
			? PlayerLogoutCraftCooldownLiveReadinessPlan.Ready(
				savePlan,
				connectionDecision,
				errorDecision,
				repositoryMethodAvailable,
				logoutSaveHookAvailable,
				databaseIntegrationTestAvailable)
			: PlayerLogoutCraftCooldownLiveReadinessPlan.NotReady(
				savePlan,
				connectionDecision,
				errorDecision,
				repositoryMethodAvailable,
				logoutSaveHookAvailable,
				databaseIntegrationTestAvailable,
				missingCriteria);
	}
}

public sealed record PlayerLogoutCraftCooldownLiveReadinessPlan(
	PlayerLogoutCraftCooldownLiveReadinessStatus Status,
	PlayerLogoutCraftCooldownSavePlan? SavePlan,
	PlayerLogoutCraftCooldownConnectionDecision ConnectionDecision,
	PlayerLogoutCraftCooldownErrorDecision ErrorDecision,
	IReadOnlyList<PlayerLogoutCraftCooldownLiveReadinessCriterion> MissingCriteria,
	bool RepositoryMethodAvailable,
	bool LogoutSaveHookAvailable,
	bool DatabaseIntegrationTestAvailable,
	bool ReadyForLiveRepositoryWiring,
	string JavaSource,
	bool IsLive)
{
	public static PlayerLogoutCraftCooldownLiveReadinessPlan Ready(
		PlayerLogoutCraftCooldownSavePlan savePlan,
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision,
		bool repositoryMethodAvailable,
		bool logoutSaveHookAvailable,
		bool databaseIntegrationTestAvailable)
	{
		return new PlayerLogoutCraftCooldownLiveReadinessPlan(
			PlayerLogoutCraftCooldownLiveReadinessStatus.ReadyForLiveRepositoryWiring,
			savePlan,
			connectionDecision,
			errorDecision,
			MissingCriteria: Array.Empty<PlayerLogoutCraftCooldownLiveReadinessCriterion>(),
			repositoryMethodAvailable,
			logoutSaveHookAvailable,
			databaseIntegrationTestAvailable,
			ReadyForLiveRepositoryWiring: true,
			"CraftCooldownsDAO.storeCraftCooldowns live wiring is gated on explicit connection/error behavior decisions, repository support, logout hook, and database integration coverage",
			IsLive: false);
	}

	public static PlayerLogoutCraftCooldownLiveReadinessPlan NotReady(
		PlayerLogoutCraftCooldownSavePlan? savePlan,
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision,
		bool repositoryMethodAvailable,
		bool logoutSaveHookAvailable,
		bool databaseIntegrationTestAvailable,
		IReadOnlyList<PlayerLogoutCraftCooldownLiveReadinessCriterion> missingCriteria)
	{
		return new PlayerLogoutCraftCooldownLiveReadinessPlan(
			PlayerLogoutCraftCooldownLiveReadinessStatus.NotReady,
			savePlan,
			connectionDecision,
			errorDecision,
			missingCriteria.ToArray(),
			repositoryMethodAvailable,
			logoutSaveHookAvailable,
			databaseIntegrationTestAvailable,
			ReadyForLiveRepositoryWiring: false,
			"CraftCooldownsDAO.storeCraftCooldowns live wiring remains gated until Java connection/error behavior and C# repository/logout/test coverage are explicitly ready",
			IsLive: false);
	}
}

public enum PlayerLogoutCraftCooldownLiveReadinessStatus
{
	NotReady,
	ReadyForLiveRepositoryWiring,
}

public enum PlayerLogoutCraftCooldownConnectionDecision
{
	Unspecified,
	PreserveJavaSeparateConnections,
	IntentionalDifferenceReuseLogoutConnectionDocumented,
}

public enum PlayerLogoutCraftCooldownErrorDecision
{
	Unspecified,
	PreserveJavaSwallowSqlExceptionsPerOperation,
	IntentionalDifferenceAggregateRepositoryFailureDocumented,
}

public enum PlayerLogoutCraftCooldownLiveReadinessCriterion
{
	SavePlanAvailable,
	SavePlanReady,
	ConnectionBehaviorDecided,
	ErrorBehaviorDecided,
	RepositoryMethodAvailable,
	LogoutSaveHookAvailable,
	DatabaseIntegrationTestAvailable,
}

public static class PlayerLogoutCraftCooldownRepositoryContractPlanService
{
	public const string RepositoryMethodSignature =
		"Task<bool> SavePlayerCraftCooldownsAsync(int playerObjectId, IReadOnlyDictionary<int, long> cooldowns, long? nowMillis = null, CancellationToken cancellationToken = default)";
	public const string RepositoryInterfaceName = "IPlayerEnterWorldRepository";
	public const string RepositoryImplementationName = "MySqlPlayerEnterWorldRepository";
	public const string FakeRepositoryCaptureProperty = "SavedCraftCooldowns";
	public const string DatabaseIntegrationTestName = "SavePlayerCraftCooldownsAsync_ReplacesRowsAndKeepsOnlyActiveCooldownsAgainstJavaSchema_WhenEnabled";

	public static PlayerLogoutCraftCooldownRepositoryContractPlan CreateDisabledPlan(
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision)
	{
		if (connectionDecision == PlayerLogoutCraftCooldownConnectionDecision.Unspecified
			|| errorDecision == PlayerLogoutCraftCooldownErrorDecision.Unspecified)
		{
			return PlayerLogoutCraftCooldownRepositoryContractPlan.MissingBehaviorDecision(connectionDecision, errorDecision);
		}

		return PlayerLogoutCraftCooldownRepositoryContractPlan.DisabledContractPlanned(connectionDecision, errorDecision);
	}
}

public sealed record PlayerLogoutCraftCooldownRepositoryContractPlan(
	PlayerLogoutCraftCooldownRepositoryContractPlanStatus Status,
	PlayerLogoutCraftCooldownConnectionDecision ConnectionDecision,
	PlayerLogoutCraftCooldownErrorDecision ErrorDecision,
	string RepositoryInterfaceName,
	string RepositoryImplementationName,
	string MethodSignature,
	string DeleteSql,
	string InsertSql,
	string FakeRepositoryCaptureProperty,
	string DatabaseIntegrationTestName,
	bool ShouldAddInterfaceMethod,
	bool DidAddInterfaceMethod,
	bool ShouldAddFakeRepositoryCapture,
	bool DidAddFakeRepositoryCapture,
	bool ShouldAddDatabaseIntegrationTest,
	bool DidAddDatabaseIntegrationTest,
	bool RequiresSeparateConnectionPerSqlOperation,
	bool RequiresIntentionalConnectionDifferenceDocumentation,
	bool RequiresPerOperationSqlExceptionSwallowing,
	bool RequiresIntentionalErrorDifferenceDocumentation,
	string JavaSource,
	bool IsLive)
{
	public static PlayerLogoutCraftCooldownRepositoryContractPlan MissingBehaviorDecision(
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision)
	{
		return new PlayerLogoutCraftCooldownRepositoryContractPlan(
			PlayerLogoutCraftCooldownRepositoryContractPlanStatus.MissingBehaviorDecision,
			connectionDecision,
			errorDecision,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryInterfaceName,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryImplementationName,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryMethodSignature,
			CraftCooldownPersistencePlanService.JavaCraftCooldownDeleteSql,
			CraftCooldownPersistencePlanService.JavaCraftCooldownInsertSql,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.FakeRepositoryCaptureProperty,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.DatabaseIntegrationTestName,
			ShouldAddInterfaceMethod: false,
			DidAddInterfaceMethod: false,
			ShouldAddFakeRepositoryCapture: false,
			DidAddFakeRepositoryCapture: false,
			ShouldAddDatabaseIntegrationTest: false,
			DidAddDatabaseIntegrationTest: false,
			RequiresSeparateConnectionPerSqlOperation: false,
			RequiresIntentionalConnectionDifferenceDocumentation: false,
			RequiresPerOperationSqlExceptionSwallowing: false,
			RequiresIntentionalErrorDifferenceDocumentation: false,
			"CraftCooldownsDAO.storeCraftCooldowns repository contract planning is blocked until connection and SQL error behavior decisions are explicit",
			IsLive: false);
	}

	public static PlayerLogoutCraftCooldownRepositoryContractPlan DisabledContractPlanned(
		PlayerLogoutCraftCooldownConnectionDecision connectionDecision,
		PlayerLogoutCraftCooldownErrorDecision errorDecision)
	{
		var preservesConnections = connectionDecision == PlayerLogoutCraftCooldownConnectionDecision.PreserveJavaSeparateConnections;
		var preservesErrors = errorDecision == PlayerLogoutCraftCooldownErrorDecision.PreserveJavaSwallowSqlExceptionsPerOperation;
		return new PlayerLogoutCraftCooldownRepositoryContractPlan(
			PlayerLogoutCraftCooldownRepositoryContractPlanStatus.DisabledContractPlanned,
			connectionDecision,
			errorDecision,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryInterfaceName,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryImplementationName,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.RepositoryMethodSignature,
			CraftCooldownPersistencePlanService.JavaCraftCooldownDeleteSql,
			CraftCooldownPersistencePlanService.JavaCraftCooldownInsertSql,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.FakeRepositoryCaptureProperty,
			PlayerLogoutCraftCooldownRepositoryContractPlanService.DatabaseIntegrationTestName,
			ShouldAddInterfaceMethod: true,
			DidAddInterfaceMethod: false,
			ShouldAddFakeRepositoryCapture: true,
			DidAddFakeRepositoryCapture: false,
			ShouldAddDatabaseIntegrationTest: true,
			DidAddDatabaseIntegrationTest: false,
			RequiresSeparateConnectionPerSqlOperation: preservesConnections,
			RequiresIntentionalConnectionDifferenceDocumentation: !preservesConnections,
			RequiresPerOperationSqlExceptionSwallowing: preservesErrors,
			RequiresIntentionalErrorDifferenceDocumentation: !preservesErrors,
			"CraftCooldownsDAO.storeCraftCooldowns repository contract should expose delete-first/active-insert SQL using the Java craft_cooldowns schema before live logout wiring",
			IsLive: false);
	}
}

public enum PlayerLogoutCraftCooldownRepositoryContractPlanStatus
{
	MissingBehaviorDecision,
	DisabledContractPlanned,
}
