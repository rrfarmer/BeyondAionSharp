using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PowerShardDamageService
{
	private const int CubeStorageId = 0;
	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;

	public static PowerShardDamageResult GetPowerShardDamage(
		Player player,
		ItemTemplateTable itemTemplates,
		bool mainHand,
		bool removePowerShards)
	{
		// Java parity: model/stats/container/PlayerGameStats.getPowerShardDamage.
		if (!player.IsInState(PlayerCreatureState.Powershard))
			return PowerShardDamageResult.NoDamage();

		var weapon = GetEquippedItemBySlot(player.InventoryItems, mainHand ? MainHand : SubHand);
		var weaponTemplate = weapon == null ? null : itemTemplates.GetItemTemplate(weapon.ItemId);
		if (weaponTemplate is not { IsWeapon: true } || weaponTemplate.IsShield)
			return PowerShardDamageResult.NoDamage();

		var damage = 0;
		var inventoryItems = player.InventoryItems;
		var useResults = new List<PowerShardUseResult>();
		var workingPlayer = new Player
		{
			ObjectId = player.ObjectId,
			CreatureState = player.CreatureState,
			InventoryItems = inventoryItems,
		};

		void ApplyShard(InventoryItem? shard)
		{
			if (shard == null)
				return;

			var shardTemplate = itemTemplates.GetItemTemplate(shard.ItemId);
			if (shardTemplate == null)
				return;

			damage += shardTemplate.WeaponBoost;
			if (!removePowerShards)
				return;

			var useResult = EquipmentService.UsePowerShard(workingPlayer, shard.ObjectId, count: 1, itemTemplates);
			if (!useResult.Changed)
				return;

			useResults.Add(useResult);
			inventoryItems = useResult.InventoryItems;
			workingPlayer.InventoryItems = inventoryItems;
		}

		var firstShard = GetEquippedPowerShardBySlot(inventoryItems, PowerShardRight, itemTemplates);
		var secondShard = GetEquippedPowerShardBySlot(inventoryItems, PowerShardLeft, itemTemplates);
		if (mainHand)
		{
			ApplyShard(firstShard);
			if (weaponTemplate.IsTwoHandWeapon)
				ApplyShard(secondShard);
		}
		else
		{
			ApplyShard(secondShard);
		}

		return damage == 0 && useResults.Count == 0
			? PowerShardDamageResult.NoDamage()
			: new PowerShardDamageResult(damage, inventoryItems, useResults);
	}

	private static InventoryItem? GetEquippedPowerShardBySlot(
		IReadOnlyList<InventoryItem> inventoryItems,
		long slot,
		ItemTemplateTable itemTemplates)
	{
		var item = GetEquippedItemBySlot(inventoryItems, slot);
		var template = item == null ? null : itemTemplates.GetItemTemplate(item.ItemId);
		return template != null && string.Equals(template.ItemGroup, "POWER_SHARDS", StringComparison.Ordinal)
			? item
			: null;
	}

	private static InventoryItem? GetEquippedItemBySlot(IReadOnlyList<InventoryItem> inventoryItems, long slot)
	{
		return inventoryItems.FirstOrDefault(item =>
			item.Location == CubeStorageId
			&& item.IsEquipped
			&& (item.Slot & slot) == slot);
	}
}

public sealed record PowerShardDamageResult(
	int Damage,
	IReadOnlyList<InventoryItem> InventoryItems,
	IReadOnlyList<PowerShardUseResult> PowerShardUses)
{
	public static PowerShardDamageResult NoDamage()
	{
		return new PowerShardDamageResult(0, Array.Empty<InventoryItem>(), Array.Empty<PowerShardUseResult>());
	}
}
