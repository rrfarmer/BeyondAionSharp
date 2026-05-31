using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PowerShardDamageServiceTests
{
	[Fact]
	public void GetPowerShardDamage_ReturnsZeroWhenBoostModeIsOff()
	{
		var player = CreatePlayer(
			state: 0,
			new InventoryItem { ObjectId = 1, ItemId = SwordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand },
			new InventoryItem { ObjectId = 2, ItemId = PowerShardId, Count = 1, Location = 0, IsEquipped = true, Slot = PowerShardRight });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: true, removePowerShards: true);

		Assert.Equal(0, result.Damage);
		Assert.Empty(result.PowerShardUses);
		Assert.Empty(result.InventoryItems);
	}

	[Fact]
	public void GetPowerShardDamage_ReadsMainHandShardWithoutConsumingWhenRequested()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = SwordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand },
			new InventoryItem { ObjectId = 2, ItemId = PowerShardId, Count = 3, Location = 0, IsEquipped = true, Slot = PowerShardRight });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: true, removePowerShards: false);

		Assert.Equal(20, result.Damage);
		Assert.Empty(result.PowerShardUses);
		Assert.Equal(3, result.InventoryItems.First(item => item.ObjectId == 2).Count);
	}

	[Fact]
	public void GetPowerShardDamage_ConsumesMainHandShardWhenRequested()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = SwordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand },
			new InventoryItem { ObjectId = 2, ItemId = PowerShardId, Count = 3, Location = 0, IsEquipped = true, Slot = PowerShardRight });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: true, removePowerShards: true);

		Assert.Equal(20, result.Damage);
		var use = Assert.Single(result.PowerShardUses);
		Assert.True(use.MarksEquipmentPersistentState);
		Assert.Equal(2, Assert.Single(use.CountUpdateItems).Count);
		Assert.Equal(2, result.InventoryItems.First(item => item.ObjectId == 2).Count);
	}

	[Fact]
	public void GetPowerShardDamage_AddsBothShardsForTwoHandMainWeapon()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = GreatswordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand | SubHand },
			new InventoryItem { ObjectId = 2, ItemId = PowerShardId, Count = 3, Location = 0, IsEquipped = true, Slot = PowerShardRight },
			new InventoryItem { ObjectId = 3, ItemId = GreaterPowerShardId, Count = 4, Location = 0, IsEquipped = true, Slot = PowerShardLeft });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: true, removePowerShards: true);

		Assert.Equal(45, result.Damage);
		Assert.Equal([2, 3], result.PowerShardUses.SelectMany(use => use.CountUpdateItems).Select(item => item.ObjectId));
		Assert.Equal(2, result.InventoryItems.First(item => item.ObjectId == 2).Count);
		Assert.Equal(3, result.InventoryItems.First(item => item.ObjectId == 3).Count);
	}

	[Fact]
	public void GetPowerShardDamage_SkipsOffHandWhenSubHandIsSameTwoHandWeapon()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = GreatswordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand | SubHand },
			new InventoryItem { ObjectId = 2, ItemId = PowerShardId, Count = 3, Location = 0, IsEquipped = true, Slot = PowerShardRight },
			new InventoryItem { ObjectId = 3, ItemId = GreaterPowerShardId, Count = 4, Location = 0, IsEquipped = true, Slot = PowerShardLeft });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: false, removePowerShards: true);

		Assert.Equal(0, result.Damage);
		Assert.Empty(result.PowerShardUses);
		Assert.Empty(result.InventoryItems);
	}

	[Fact]
	public void GetPowerShardDamage_UsesLeftShardForOffHandWeapon()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = SwordId, Count = 1, Location = 0, IsEquipped = true, Slot = MainHand },
			new InventoryItem { ObjectId = 2, ItemId = DaggerId, Count = 1, Location = 0, IsEquipped = true, Slot = SubHand },
			new InventoryItem { ObjectId = 3, ItemId = PowerShardId, Count = 2, Location = 0, IsEquipped = true, Slot = PowerShardRight },
			new InventoryItem { ObjectId = 4, ItemId = GreaterPowerShardId, Count = 2, Location = 0, IsEquipped = true, Slot = PowerShardLeft });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: false, removePowerShards: true);

		Assert.Equal(25, result.Damage);
		var use = Assert.Single(result.PowerShardUses);
		Assert.True(use.MarksEquipmentPersistentState);
		Assert.Equal(4, Assert.Single(use.CountUpdateItems).ObjectId);
		Assert.Equal(2, result.InventoryItems.First(item => item.ObjectId == 3).Count);
		Assert.Equal(1, result.InventoryItems.First(item => item.ObjectId == 4).Count);
	}

	[Fact]
	public void GetPowerShardDamage_SkipsShieldOffHand()
	{
		var player = CreatePlayer(
			PlayerCreatureState.Powershard,
			new InventoryItem { ObjectId = 1, ItemId = ShieldId, Count = 1, Location = 0, IsEquipped = true, Slot = SubHand },
			new InventoryItem { ObjectId = 2, ItemId = GreaterPowerShardId, Count = 2, Location = 0, IsEquipped = true, Slot = PowerShardLeft });

		var result = PowerShardDamageService.GetPowerShardDamage(player, CreateItemTemplates(), mainHand: false, removePowerShards: true);

		Assert.Equal(0, result.Damage);
		Assert.Empty(result.PowerShardUses);
	}

	private static Player CreatePlayer(PlayerCreatureState state, params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 100,
			CreatureState = state,
			InventoryItems = items,
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new(SwordId, "Practice Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, MainHand | SubHand),
			new(DaggerId, "Practice Dagger", 0, 1, 1, "DAGGER", "NORMAL", "COMMON", "PC_ALL", 1, 0, MainHand | SubHand),
			new(GreatswordId, "Practice Greatsword", 0, 1, 1, "GREATSWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, MainHand | SubHand),
			new(ShieldId, "Practice Shield", 0, 1, 1, "SHIELD", "NORMAL", "COMMON", "PC_ALL", 1, 0, SubHand),
			new(PowerShardId, "Practice Power Shard", 0, 1, 1, "POWER_SHARDS", "NORMAL", "COMMON", "PC_ALL", 1000, 0, PowerShardRight | PowerShardLeft, WeaponBoost: 20),
			new(GreaterPowerShardId, "Practice Greater Power Shard", 0, 1, 1, "POWER_SHARDS", "NORMAL", "COMMON", "PC_ALL", 1000, 0, PowerShardRight | PowerShardLeft, WeaponBoost: 25),
		]);
	}

	private const int SwordId = 100000001;
	private const int DaggerId = 100200001;
	private const int GreatswordId = 100100001;
	private const int ShieldId = 115000001;
	private const int PowerShardId = 169000005;
	private const int GreaterPowerShardId = 169000007;
	private const long MainHand = 1L;
	private const long SubHand = 1L << 1;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
}
