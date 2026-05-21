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
