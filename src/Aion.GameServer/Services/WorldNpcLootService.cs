using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLootService
{
	private readonly WorldNpcDropRegistrationService _dropRegistrationService;
	private readonly WorldNpcSpawnService? _worldNpcSpawnService;
	private readonly ThreadPoolManager? _threadPoolManager;

	public WorldNpcLootService(
		WorldNpcDropRegistrationService dropRegistrationService,
		WorldNpcSpawnService? worldNpcSpawnService = null,
		ThreadPoolManager? threadPoolManager = null)
	{
		_dropRegistrationService = dropRegistrationService;
		_worldNpcSpawnService = worldNpcSpawnService;
		_threadPoolManager = threadPoolManager;
	}

	public WorldNpcLootResult RequestDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.requestDropList.
		if (player == null || !_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.UnknownDrop);

		var packets = new List<AionServerPacket>();
		var visiblePackets = new List<AionServerPacket>();
		if (player.IsLooting())
		{
			var closeResult = CloseDropList(player, player.GetLootingNpcOid());
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
		player.SetLootingNpcOid(npcObjectId);
		visiblePackets.Add(new SmEmotion(player, EmotionType.START_LOOT, 0, npcObjectId));

		return new WorldNpcLootResult(WorldNpcLootStatus.Opened, packets, visiblePackets);
	}

	public WorldNpcLootResult CloseDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.closeDropList.
		if (player == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.NoPlayer);

		var wasLootingThisNpc = player.IsLooting() && player.GetLootingNpcOid() == npcObjectId;
		player.SetLootingNpcOid(0);
		var visiblePackets = new List<AionServerPacket>
		{
			new SmEmotion(player, EmotionType.END_LOOT, 0, npcObjectId),
		};

		if (!_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return new WorldNpcLootResult(WorldNpcLootStatus.ClosedMissingRegistration, Array.Empty<AionServerPacket>(), visiblePackets);

		if (!wasLootingThisNpc || !registration.ClearLootingPlayer(player.ObjectId))
			return new WorldNpcLootResult(WorldNpcLootStatus.CloseRejected, Array.Empty<AionServerPacket>(), visiblePackets);

		ResumeDecayAfterClose(npcObjectId, registration);
		return new WorldNpcLootResult(WorldNpcLootStatus.Closed, Array.Empty<AionServerPacket>(), visiblePackets);
	}

	public WorldNpcLootResult RequestDropItem(
		Player? player,
		int npcObjectId,
		int itemIndex,
		ItemTemplateTable? itemTemplates,
		Func<int>? nextObjectId,
		ItemRestrictionCleanupTable? itemRestrictionCleanups = null)
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

		if (player.IsInTeam())
			return WorldNpcLootResult.None(WorldNpcLootStatus.TeamDistributionPending);

		var template = itemTemplates.GetItemTemplate(requestedItem.ItemId);
		if (template == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.MissingItemTemplate);

		if (PlayerAlreadyOwnsLimitOneItem(player, template))
		{
			return new WorldNpcLootResult(
				WorldNpcLootStatus.LimitOneAlreadyOwned,
				[SmSystemMessage.CannotGetLoreItem(template.GetClientName() ?? template.Name)],
				Array.Empty<AionServerPacket>());
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
				? new AionServerPacket[] { SmSystemMessage.FullInventory() }
				: Array.Empty<AionServerPacket>();
			return new WorldNpcLootResult(WorldNpcLootStatus.InventoryFull, failurePackets, Array.Empty<AionServerPacket>());
		}

		ApplyInventoryPlan(player, addPlan);
		var playerPackets = CreateInventoryCollectPackets(addPlan, template, itemRestrictionCleanups).ToList();
		var remainingDrops = _dropRegistrationService.ApplyCollectedCount(npcObjectId, itemIndex, addPlan.RemainingCount);
		var visiblePackets = new List<AionServerPacket>();
		if (remainingDrops.Count == 0)
		{
			playerPackets.Add(new SmLootStatus(npcObjectId, SmLootStatusType.CloseDropList));
			player.SetLootingNpcOid(0);
			visiblePackets.Add(new SmEmotion(player, EmotionType.END_LOOT, 0, npcObjectId));
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

	public WorldNpcInitialLootEnableResult CreateInitialLootEnableStatus(int npcObjectId)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop sends SM_LOOT_STATUS.LOOT_ENABLE to every allowed looter after registration.
		if (!_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return WorldNpcInitialLootEnableResult.MissingRegistration();

		var looterObjectIds = registration.AllowedLooters.ToArray();
		if (looterObjectIds.Length == 0)
			return WorldNpcInitialLootEnableResult.NoAllowedLooters();

		return WorldNpcInitialLootEnableResult.Created(looterObjectIds, CreateLootEnableStatus(npcObjectId));
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

	public WorldNpcFreeForAllResult StartFreeForAll(int npcObjectId, IWorldNpcObject? npc = null)
	{
		// Java parity: services/drop/DropService.scheduleFreeForAll delayed body calls DropNpc.startFreeForAll and broadcasts LOOT_ENABLE.
		if (!_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return WorldNpcFreeForAllResult.MissingRegistration();

		registration.StartFreeForAll();
		return WorldNpcFreeForAllResult.Started(CreateLootEnableStatus(npcObjectId), npc);
	}

	public ScheduledTask? ScheduleFreeForAll(
		int npcObjectId,
		IWorldNpcObject? npc = null,
		TimeSpan? delay = null,
		Func<WorldNpcFreeForAllResult, ValueTask>? onStarted = null)
	{
		// Java parity: services/drop/DropService.scheduleFreeForAll uses a 240000 ms delay after drop registration.
		if (_threadPoolManager == null)
			return null;

		return _threadPoolManager.Schedule(
			async cancellationToken =>
			{
				var result = StartFreeForAll(npcObjectId, npc);
				if (!cancellationToken.IsCancellationRequested && onStarted != null)
					await onStarted(result);
			},
			delay ?? TimeSpan.FromMinutes(4));
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

	private static IEnumerable<AionServerPacket> CreateInventoryCollectPackets(
		InventoryAddPlan addPlan,
		ItemTemplateSummary template,
		ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			var updateType = template.TemplateId == InventoryItemFactory.KinahItemId
				? SmInventoryUpdateItem.IncreaseKinahCollect
				: SmInventoryUpdateItem.IncreaseItemCollect;
			yield return new SmInventoryUpdateItem(
				updatedItem,
				template,
				updateType,
				GetGeneralInfoWarehouseRestrictionFlag(updatedItem.ItemId, itemRestrictionCleanups));
		}

		foreach (var addedItem in addPlan.AddedItems)
			yield return SmInventoryAddItem.CreateItemCollect(
				addedItem,
				template,
				GetGeneralInfoWarehouseRestrictionFlag(addedItem.ItemId, itemRestrictionCleanups));
	}

	private static int GetGeneralInfoWarehouseRestrictionFlag(int itemId, ItemRestrictionCleanupTable? itemRestrictionCleanups)
	{
		// Java parity: network/aion/iteminfo/GeneralInfoBlobEntry uses DataManager.ITEM_CLEAN_UP.hasAccountOrLegionWhStorabilityDisabled.
		return itemRestrictionCleanups?.HasAccountOrLegionWarehouseStorabilityDisabled(itemId) == true ? 3 : 0;
	}

	private static bool PlayerAlreadyOwnsLimitOneItem(Player player, ItemTemplateSummary template)
	{
		// Java parity: DropService.requestDropItem checks ItemTemplate.hasLimitOne against inventory and regular warehouse.
		return template.IsLimitOne
			&& (player.GetInventory().GetItems().Any(item => item.GetItemTemplate().GetTemplateId() == template.TemplateId)
				|| player.GetWarehouse().GetItems().Any(item => item.GetItemTemplate().GetTemplateId() == template.TemplateId));
	}
}

public sealed record WorldNpcLootResult(
	WorldNpcLootStatus Status,
	IReadOnlyList<AionServerPacket> PlayerPackets,
	IReadOnlyList<AionServerPacket> VisiblePlayerPackets)
{
	public static WorldNpcLootResult None(WorldNpcLootStatus status)
	{
		return new WorldNpcLootResult(status, Array.Empty<AionServerPacket>(), Array.Empty<AionServerPacket>());
	}
}

public sealed record WorldNpcInitialLootEnableResult(
	WorldNpcInitialLootEnableStatus Status,
	IReadOnlyList<int> LooterObjectIds,
	SmLootStatus? LootStatus)
{
	public static WorldNpcInitialLootEnableResult Created(
		IReadOnlyList<int> looterObjectIds,
		SmLootStatus lootStatus)
	{
		return new WorldNpcInitialLootEnableResult(
			WorldNpcInitialLootEnableStatus.Created,
			looterObjectIds,
			lootStatus);
	}

	public static WorldNpcInitialLootEnableResult MissingRegistration()
	{
		return new WorldNpcInitialLootEnableResult(
			WorldNpcInitialLootEnableStatus.MissingRegistration,
			Array.Empty<int>(),
			null);
	}

	public static WorldNpcInitialLootEnableResult NoAllowedLooters()
	{
		return new WorldNpcInitialLootEnableResult(
			WorldNpcInitialLootEnableStatus.NoAllowedLooters,
			Array.Empty<int>(),
			null);
	}
}

public enum WorldNpcInitialLootEnableStatus
{
	MissingRegistration,
	NoAllowedLooters,
	Created,
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

public sealed record WorldNpcFreeForAllResult(
	WorldNpcFreeForAllStatus Status,
	SmLootStatus? LootStatus,
	IWorldNpcObject? Npc)
{
	public bool CanBroadcastTo(Player player)
	{
		// Java parity: DropService.scheduleFreeForAll skips same-race players when the lootable NPC is Elyos/Asmodian.
		if (Npc == null)
			return true;

		var npcRace = NormalizeRace(Npc.Template.Race);
		return npcRace switch
		{
			"ELYOS" or "ASMODIANS" => !string.Equals(npcRace, NormalizeRace(player.Race.ToString()), StringComparison.Ordinal),
			_ => true,
		};
	}

	public static WorldNpcFreeForAllResult Started(SmLootStatus lootStatus, IWorldNpcObject? npc)
	{
		return new WorldNpcFreeForAllResult(WorldNpcFreeForAllStatus.Started, lootStatus, npc);
	}

	public static WorldNpcFreeForAllResult MissingRegistration()
	{
		return new WorldNpcFreeForAllResult(WorldNpcFreeForAllStatus.MissingRegistration, null, null);
	}

	private static string NormalizeRace(string race)
	{
		return string.Equals(race, "ASMODIAN", StringComparison.OrdinalIgnoreCase)
			? "ASMODIANS"
			: race.ToUpperInvariant();
	}
}

public enum WorldNpcFreeForAllStatus
{
	MissingRegistration,
	Started,
}
