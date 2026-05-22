using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class PlayerEnterWorldService
{
	private readonly GameServerOptions _options;
	private readonly IPlayerEnterWorldRepository _repository;
	private readonly GameWorld _world;
	private readonly ConcurrentDictionary<int, byte> _enteringWorld = new();
	private readonly ILogger<PlayerEnterWorldService> _logger;

	public PlayerEnterWorldService(
		GameServerOptions options,
		IPlayerEnterWorldRepository repository,
		GameWorld world,
		ILogger<PlayerEnterWorldService> logger)
	{
		_options = options;
		_repository = repository;
		_world = world;
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
			player.Titles = await _repository.LoadPlayerTitlesAsync(playerObjectId, cancellationToken);
			player.Motions = await _repository.LoadPlayerMotionsAsync(playerObjectId, cancellationToken);
			player.Emotions = await _repository.LoadPlayerEmotionsAsync(playerObjectId, cancellationToken);
			player.Recipes = await _repository.LoadPlayerRecipesAsync(playerObjectId, cancellationToken);
			player.Macros = await _repository.LoadPlayerMacrosAsync(playerObjectId, cancellationToken);
			player.Mailbox = await _repository.LoadPlayerMailboxAsync(playerObjectId, cancellationToken);
			player.BrokerSettlements = await _repository.LoadBrokerSettlementsAsync(playerObjectId, player.Race, cancellationToken);
			player.Houses = await _repository.LoadPlayerHousesAsync(playerObjectId, cancellationToken);
			player.CraftCooldowns = await _repository.LoadPlayerCraftCooldownsAsync(playerObjectId, cancellationToken);
			player.PortalCooldowns = await _repository.LoadPlayerPortalCooldownsAsync(playerObjectId, cancellationToken);
			player.LifeStats = await _repository.LoadPlayerLifeStatsAsync(playerObjectId, cancellationToken);
			player.Friends = await _repository.LoadPlayerFriendsAsync(playerObjectId, cancellationToken);
			player.BlockedUsers = await _repository.LoadPlayerBlockedUsersAsync(playerObjectId, cancellationToken);
			player.AbyssRank = await _repository.LoadPlayerAbyssRankAsync(playerObjectId, cancellationToken);
			player.Settings = await _repository.LoadPlayerSettingsAsync(playerObjectId, cancellationToken);
			player.BindPoint = await _repository.LoadPlayerBindPointAsync(playerObjectId, cancellationToken);
			if (!_world.TryAddObject(playerObjectId, player))
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);

			var now = DateTime.Now;
			if (!await _repository.MarkPlayerOnlineAsync(playerObjectId, now, cancellationToken))
			{
				_world.TryRemoveObject(playerObjectId, out _);
				return new PlayerEnterWorldResult(EnterWorldCheckMessage.ConnectionError);
			}

			player.IsOnline = true;
			player.LastOnline = now;
			_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) logged on", player.Name, playerObjectId);
			return new PlayerEnterWorldResult(EnterWorldCheckMessage.Ok, player);
		}
		catch (Exception ex)
		{
			_world.TryRemoveObject(playerObjectId, out _);
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

	public async Task LeaveWorldAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerLeaveWorldService.leaveWorld baseline persistence.
		var lastOnline = DateTime.Now;
		player.IsOnline = false;
		player.LastOnline = lastOnline;
		_world.TryRemoveObject(player.ObjectId, out _);
		var saved = await _repository.SavePlayerLogoutAsync(player, lastOnline, cancellationToken);
		if (saved)
			_logger.LogInformation("Player {PlayerName} ({PlayerObjectId}) logged off", player.Name, player.ObjectId);
		else
			_logger.LogWarning("Player {PlayerName} ({PlayerObjectId}) logout state was not fully persisted", player.Name, player.ObjectId);
	}

	private bool IsInsideReentryWindow(DateTime? lastOnline)
	{
		// Java parity: PlayerEnterWorldService lastOnline vs GSConfig.CHARACTER_REENTRY_TIME check.
		return lastOnline.HasValue
			&& DateTime.Now - lastOnline.Value < TimeSpan.FromSeconds(_options.Core.CharacterReentryTimeSeconds);
	}
}
