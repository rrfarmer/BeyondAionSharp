using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLootService
{
	private readonly WorldNpcDropRegistrationService _dropRegistrationService;

	public WorldNpcLootService(WorldNpcDropRegistrationService dropRegistrationService)
	{
		_dropRegistrationService = dropRegistrationService;
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
	ItemCollected,
}
