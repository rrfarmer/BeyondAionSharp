using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLootService
{
	private readonly WorldNpcDropRegistrationService _dropRegistrationService;
	private readonly WorldNpcSpawnService? _worldNpcSpawnService;

	public WorldNpcLootService(WorldNpcDropRegistrationService dropRegistrationService, WorldNpcSpawnService? worldNpcSpawnService = null)
	{
		_dropRegistrationService = dropRegistrationService;
		_worldNpcSpawnService = worldNpcSpawnService;
	}

	public WorldNpcLootResult RequestDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.requestDropList.
		if (player == null || !_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.UnknownDrop);

		var packets = new List<GameServerPacket>();
		var visiblePackets = new List<GameServerPacket>();
		if (player.IsLooting)
		{
			var closeResult = CloseDropList(player, player.LootingNpcObjectId);
			packets.AddRange(closeResult.PlayerPackets);
			visiblePackets.AddRange(closeResult.VisiblePlayerPackets);
		}

		if (!registration.IsAllowedToLoot(player.ObjectId))
		{
			packets.Add(SmSystemMessage.LootNoRight());
			return new WorldNpcLootResult(WorldNpcLootStatus.NoRight, packets, visiblePackets);
		}

		if (!registration.TryBeginLooting(player.ObjectId, out _))
		{
			packets.Add(SmSystemMessage.LootFailOnLooting());
			return new WorldNpcLootResult(WorldNpcLootStatus.AlreadyLooted, packets, visiblePackets);
		}

		var remainingDecayTime = _worldNpcSpawnService?.CancelDecay(npcObjectId);
		if (remainingDecayTime != null)
			registration.RemainingDecayTimeMillis = (long)remainingDecayTime.Value.TotalMilliseconds;

		var dropItems = _dropRegistrationService.GetCurrentDrops(npcObjectId);
		packets.Add(new SmLootItemList(npcObjectId, dropItems, player));
		packets.Add(new SmLootStatus(npcObjectId, SmLootStatusType.OpenDropList));
		player.StartLooting(npcObjectId);
		visiblePackets.Add(new SmEmotion(player, EmotionType.StartLoot, 0, npcObjectId));

		return new WorldNpcLootResult(WorldNpcLootStatus.Opened, packets, visiblePackets);
	}

	public WorldNpcLootResult CloseDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.closeDropList.
		if (player == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.NoPlayer);

		var wasLootingThisNpc = player.IsLooting && player.LootingNpcObjectId == npcObjectId;
		player.StopLooting();
		var visiblePackets = new List<GameServerPacket>
		{
			new SmEmotion(player, EmotionType.EndLoot, 0, npcObjectId),
		};

		if (!_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return new WorldNpcLootResult(WorldNpcLootStatus.ClosedMissingRegistration, Array.Empty<GameServerPacket>(), visiblePackets);

		if (!wasLootingThisNpc || !registration.ClearLootingPlayer(player.ObjectId))
			return new WorldNpcLootResult(WorldNpcLootStatus.CloseRejected, Array.Empty<GameServerPacket>(), visiblePackets);

		ResumeDecayAfterClose(npcObjectId, registration);
		return new WorldNpcLootResult(WorldNpcLootStatus.Closed, Array.Empty<GameServerPacket>(), visiblePackets);
	}

	public WorldNpcLootResult RequestDropItem(
		Player? player,
		int npcObjectId,
		int itemIndex,
		ItemTemplateTable? itemTemplates,
		Func<int>? nextObjectId)
	{
		// Java parity: services/drop/DropService.requestDropItem, narrowed to direct solo item collection.
		if (player == null
			|| itemTemplates == null
			|| nextObjectId == null
			|| !_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration)
			|| registration == null
			|| !_dropRegistrationService.TryGetCurrentDrop(npcObjectId, itemIndex, out var requestedItem)
			|| requestedItem == null)
		{
			return WorldNpcLootResult.None(WorldNpcLootStatus.ItemNotFound);
		}

		if (!registration.IsAllowedToLoot(player.ObjectId))
			return WorldNpcLootResult.None(WorldNpcLootStatus.NoRight);

		if (player.IsInTeam)
			return WorldNpcLootResult.None(WorldNpcLootStatus.TeamDistributionPending);

		var template = itemTemplates.GetItemTemplate(requestedItem.ItemId);
		if (template == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.MissingItemTemplate);

		if (PlayerAlreadyOwnsLimitOneItem(player, template))
		{
			return new WorldNpcLootResult(
				WorldNpcLootStatus.LimitOneAlreadyOwned,
				[SmSystemMessage.CannotGetLoreItem(template.GetClientName() ?? template.Name)],
				Array.Empty<GameServerPacket>());
		}

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			player.InventoryItems,
			template,
			requestedItem.Count,
			nextObjectId,
			itemTemplates: itemTemplates);
		if (!addPlan.Succeeded && addPlan.AddedItems.Count == 0 && addPlan.UpdatedItems.Count == 0)
		{
			var failurePackets = addPlan.InventoryFull
				? new GameServerPacket[] { SmSystemMessage.FullInventory() }
				: Array.Empty<GameServerPacket>();
			return new WorldNpcLootResult(WorldNpcLootStatus.InventoryFull, failurePackets, Array.Empty<GameServerPacket>());
		}

		ApplyInventoryPlan(player, addPlan);
		var playerPackets = CreateInventoryCollectPackets(addPlan, template).ToList();
		var remainingDrops = _dropRegistrationService.ApplyCollectedCount(npcObjectId, itemIndex, addPlan.RemainingCount);
		var visiblePackets = new List<GameServerPacket>();
		if (remainingDrops.Count == 0)
		{
			playerPackets.Add(new SmLootStatus(npcObjectId, SmLootStatusType.CloseDropList));
			player.StopLooting();
			visiblePackets.Add(new SmEmotion(player, EmotionType.EndLoot, 0, npcObjectId));
			registration.ClearLootingPlayer(player.ObjectId);
			DeleteEmptyDropCorpse(npcObjectId);
		}
		else if (registration.LootingPlayerObjectId == player.ObjectId)
		{
			playerPackets.Add(new SmLootItemList(npcObjectId, remainingDrops, player));
		}

		return new WorldNpcLootResult(WorldNpcLootStatus.ItemCollected, playerPackets, visiblePackets);
	}

	public SmLootStatus CreateLootEnableStatus(int npcObjectId)
	{
		// Java parity: SM_LOOT_STATUS(Status.LOOT_ENABLE) chooses the first non-zero DropItem loot effect.
		var lootEffectId = _dropRegistrationService.GetCurrentDrops(npcObjectId)
			.Select(drop => drop.LootEffectId)
			.FirstOrDefault(effectId => effectId != 0);
		return new SmLootStatus(npcObjectId, SmLootStatusType.LootEnable, lootEffectId);
	}

	public SmLootStatus? CreateLootEnableStatusForSeenNpc(Player? player, IWorldNpcObject? npc)
	{
		// Java parity: services/drop/DropService.see sends LOOT_ENABLE when a player sees a dead NPC they can loot.
		if (player == null
			|| npc == null
			|| !_dropRegistrationService.TryGetRegistration(npc.ObjectId, out var registration)
			|| registration == null
			|| !registration.IsAllowedToLoot(player.ObjectId))
		{
			return null;
		}

		return CreateLootEnableStatus(npc.ObjectId);
	}

	private void ResumeDecayAfterClose(int npcObjectId, WorldNpcDropRegistration registration)
	{
		// Java parity: DropService.closeDropList resumes RespawnService.scheduleDecayTask with the remaining delay.
		if (_worldNpcSpawnService == null
			|| registration.RemainingDecayTimeMillis <= 0
			|| _dropRegistrationService.GetCurrentDrops(npcObjectId).Count == 0)
		{
			return;
		}

		_worldNpcSpawnService.TryScheduleWorldNpcDecayTask(
			npcObjectId,
			hasRegisteredDrops: true,
			TimeSpan.FromMilliseconds(registration.RemainingDecayTimeMillis));
	}

	private void DeleteEmptyDropCorpse(int npcObjectId)
	{
		// Java parity: DropService.resendDropList deletes the NPC when the current drop set becomes empty; NpcController.onDespawn unregisters drops.
		if (_worldNpcSpawnService?.TryDespawnWorldNpc(npcObjectId) == true)
			_dropRegistrationService.UnregisterDrop(npcObjectId);
	}

	private static void ApplyInventoryPlan(Player player, InventoryAddPlan addPlan)
	{
		var inventory = player.InventoryItems.ToList();
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			var index = inventory.FindIndex(item => item.ObjectId == updatedItem.ObjectId);
			if (index >= 0)
				inventory[index] = updatedItem;
			else
				inventory.Add(updatedItem);
		}
		inventory.AddRange(addPlan.AddedItems);
		player.InventoryItems = inventory;
	}

	private static IEnumerable<GameServerPacket> CreateInventoryCollectPackets(InventoryAddPlan addPlan, ItemTemplateSummary template)
	{
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			var updateType = template.TemplateId == InventoryItemFactory.KinahItemId
				? SmInventoryUpdateItem.IncreaseKinahCollect
				: SmInventoryUpdateItem.IncreaseItemCollect;
			yield return new SmInventoryUpdateItem(updatedItem, template, updateType);
		}

		foreach (var addedItem in addPlan.AddedItems)
			yield return SmInventoryAddItem.CreateItemCollect(addedItem, template);
	}

	private static bool PlayerAlreadyOwnsLimitOneItem(Player player, ItemTemplateSummary template)
	{
		// Java parity: DropService.requestDropItem checks ItemTemplate.hasLimitOne against inventory and regular warehouse.
		return template.IsLimitOne
			&& (player.InventoryItems.Any(item => item.ItemId == template.TemplateId)
				|| player.WarehouseItems.Any(item => item.ItemId == template.TemplateId));
	}
}

public sealed record WorldNpcLootResult(
	WorldNpcLootStatus Status,
	IReadOnlyList<GameServerPacket> PlayerPackets,
	IReadOnlyList<GameServerPacket> VisiblePlayerPackets)
{
	public static WorldNpcLootResult None(WorldNpcLootStatus status)
	{
		return new WorldNpcLootResult(status, Array.Empty<GameServerPacket>(), Array.Empty<GameServerPacket>());
	}
}

public enum WorldNpcLootStatus
{
	NoPlayer,
	UnknownDrop,
	NoRight,
	AlreadyLooted,
	Opened,
	Closed,
	ClosedMissingRegistration,
	CloseRejected,
	ItemNotFound,
	MissingItemTemplate,
	InventoryFull,
	TeamDistributionPending,
	LimitOneAlreadyOwned,
	ItemCollected,
}
