using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcLootServiceTests
{
	[Fact]
	public void RequestDropList_OpensVisibleDropsAndMarksPlayerLooting()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var visibleDrop = new WorldNpcDropItem(1, 182400001, 25, new HashSet<int> { 1001 });
		var hiddenDrop = new WorldNpcDropItem(2, 166020000, 1, new HashSet<int> { 1002 });
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [visibleDrop, hiddenDrop]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.Opened, result.Status);
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsLooting);
		Assert.Equal(5001, player.LootingNpcObjectId);
		Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
		Assert.Equal(1001, registration!.LootingPlayerObjectId);
		var itemList = Assert.IsType<SmLootItemList>(Assert.Single(result.PlayerPackets.OfType<SmLootItemList>()));
		Assert.Equal([visibleDrop], itemList.DropItems);
		var status = Assert.IsType<SmLootStatus>(Assert.Single(result.PlayerPackets.OfType<SmLootStatus>()));
		Assert.Equal(SmLootStatusType.OpenDropList, status.Status);
		var emotion = Assert.IsType<SmEmotion>(Assert.Single(result.VisiblePlayerPackets));
		Assert.Equal(37, emotion.OpCode);
	}

	[Fact]
	public void RequestDropList_RejectsPlayerWithoutLootRights()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1002);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.NoRight, result.Status);
		Assert.False(player.IsLooting);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(result.PlayerPackets));
		Assert.Equal(901338, message.MessageId);
	}

	[Fact]
	public void RequestDropList_RejectsAlreadyLootedCorpse()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		dropRegistration.TryGetRegistration(5001, out var registration);
		Assert.True(registration!.TryBeginLooting(1002, out _));
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);

		var result = service.RequestDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.AlreadyLooted, result.Status);
		Assert.False(player.IsLooting);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(result.PlayerPackets));
		Assert.Equal(1300829, message.MessageId);
		Assert.Equal(1002, registration.LootingPlayerObjectId);
	}

	[Fact]
	public void CloseDropList_ClearsLootingStateAndRegistration()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400001, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		Assert.Equal(WorldNpcLootStatus.Opened, service.RequestDropList(player, 5001).Status);

		var result = service.CloseDropList(player, 5001);

		Assert.Equal(WorldNpcLootStatus.Closed, result.Status);
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsLooting);
		Assert.Equal(0, player.LootingNpcObjectId);
		Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
		Assert.Null(registration!.LootingPlayerObjectId);
		var emotion = Assert.IsType<SmEmotion>(Assert.Single(result.VisiblePlayerPackets));
		Assert.Equal(37, emotion.OpCode);
	}

	[Fact]
	public void CreateLootEnableStatus_UsesFirstDropLootEffect()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops:
			[
				new WorldNpcDropItem(1, 182400001, 1),
				new WorldNpcDropItem(2, 188053547, 1),
				new WorldNpcDropItem(3, 166020000, 1),
			]);
		var service = new WorldNpcLootService(dropRegistration);

		var status = service.CreateLootEnableStatus(5001);

		Assert.Equal(SmLootStatusType.LootEnable, status.Status);
		Assert.Equal(1002, status.LootEffectId);
	}

	[Fact]
	public void CreateLootEnableStatusForSeenNpc_SendsStatusWhenPlayerCanLoot()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		var npc = CreateNpc(5001);

		var status = service.CreateLootEnableStatusForSeenNpc(player, npc);

		Assert.NotNull(status);
		Assert.Equal(SmLootStatusType.LootEnable, status.Status);
		Assert.Equal(1003, status.LootEffectId);
	}

	[Fact]
	public void CreateLootEnableStatusForSeenNpc_SkipsPlayersWithoutLootRights()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(
			5001,
			looterObjectId: 1001,
			drops: [new WorldNpcDropItem(1, 166020000, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1002);
		var npc = CreateNpc(5001);

		var status = service.CreateLootEnableStatusForSeenNpc(player, npc);

		Assert.Null(status);
	}

	[Fact]
	public void RequestDropItem_AddsSoloItemAndRefreshesRemainingDropList()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var collectedDrop = new WorldNpcDropItem(1, 182400002, 2);
		var remainingDrop = new WorldNpcDropItem(2, 182400003, 1);
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [collectedDrop, remainingDrop]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		Assert.Equal(WorldNpcLootStatus.Opened, service.RequestDropList(player, 5001).Status);

		var result = service.RequestDropItem(player, 5001, itemIndex: 1, CreateItemTemplates(), () => 9001);

		Assert.Equal(WorldNpcLootStatus.ItemCollected, result.Status);
		var item = Assert.Single(player.InventoryItems);
		Assert.Equal(9001, item.ObjectId);
		Assert.Equal(182400002, item.ItemId);
		Assert.Equal(2, item.Count);
		Assert.Contains(result.PlayerPackets, packet => packet is SmInventoryAddItem);
		var itemList = Assert.IsType<SmLootItemList>(result.PlayerPackets.Last());
		Assert.Equal([remainingDrop], itemList.DropItems);
		Assert.Equal([remainingDrop], dropRegistration.GetCurrentDrops(5001));
		Assert.True(player.IsLooting);
		Assert.Empty(result.VisiblePlayerPackets);
	}

	[Fact]
	public void RequestDropItem_ClosesLootListWhenLastDropIsCollected()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400002, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		Assert.Equal(WorldNpcLootStatus.Opened, service.RequestDropList(player, 5001).Status);

		var result = service.RequestDropItem(player, 5001, itemIndex: 1, CreateItemTemplates(), () => 9001);

		Assert.Equal(WorldNpcLootStatus.ItemCollected, result.Status);
		var closeStatus = Assert.IsType<SmLootStatus>(result.PlayerPackets.Last());
		Assert.Equal(SmLootStatusType.CloseDropList, closeStatus.Status);
		Assert.False(player.IsLooting);
		Assert.Equal(0, player.LootingNpcObjectId);
		Assert.Empty(dropRegistration.GetCurrentDrops(5001));
		Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
		Assert.Null(registration!.LootingPlayerObjectId);
		Assert.Single(result.VisiblePlayerPackets);
	}

	[Fact]
	public void RequestDropItem_DefersTeamDistribution()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400002, 1)]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		player.TeamMembership = PlayerTeamMembership.Group;

		var result = service.RequestDropItem(player, 5001, itemIndex: 1, CreateItemTemplates(), () => 9001);

		Assert.Equal(WorldNpcLootStatus.TeamDistributionPending, result.Status);
		Assert.Empty(player.InventoryItems);
		Assert.Single(dropRegistration.GetCurrentDrops(5001));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void RequestDropItem_RejectsLimitOneItemAlreadyOwned(bool ownedInInventory)
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		var drop = new WorldNpcDropItem(1, 182400004, 1);
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [drop]);
		var service = new WorldNpcLootService(dropRegistration);
		var player = CreatePlayer(1001);
		if (ownedInInventory)
		{
			player.InventoryItems = [new InventoryItem { ObjectId = 8001, ItemId = 182400004, Count = 1, Location = 0 }];
		}
		else
		{
			player.WarehouseItems = [new InventoryItem { ObjectId = 8002, ItemId = 182400004, Count = 1, Location = 1 }];
		}

		var result = service.RequestDropItem(player, 5001, itemIndex: 1, CreateItemTemplates(), () => 9001);

		Assert.Equal(WorldNpcLootStatus.LimitOneAlreadyOwned, result.Status);
		var message = Assert.IsType<SmSystemMessage>(Assert.Single(result.PlayerPackets));
		Assert.Equal(1300422, message.MessageId);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 9001);
		Assert.Equal([drop], dropRegistration.GetCurrentDrops(5001));
	}

	[Fact]
	public async Task RequestDropList_CancelsDecayAndCloseDropListResumesRemainingDecay()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400002, 1)]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var spawnService = new WorldNpcSpawnService(
				new GameServerRuntimeContext(),
				world,
				new IDFactory(),
				gameTimeService: null,
				threadPoolManager: threadPoolManager,
				connectionRegistry: null,
				staticPlaceables: null,
				walkerSpawnPlans: null,
				walkerPlacementApplication: null,
				logger: NullLogger<WorldNpcSpawnService>.Instance);
			var npc = new WorldNpc(
				5001,
				203001,
				new NpcTemplateSummary(203001, "loot_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NON_ATTACKABLE"),
				new WorldPosition(210010000, 1, 2, 3, 0));
			Assert.True(world.TryAddObject(npc.ObjectId, npc));
			Assert.True(spawnService.TryScheduleWorldNpcDecayTask(5001, hasRegisteredDrops: true, TimeSpan.FromMilliseconds(500)));
			var service = new WorldNpcLootService(dropRegistration, spawnService);
			var player = CreatePlayer(1001);

			var open = service.RequestDropList(player, 5001);

			Assert.Equal(WorldNpcLootStatus.Opened, open.Status);
			Assert.False(spawnService.HasDecayTask(5001));
			Assert.True(dropRegistration.TryGetRegistration(5001, out var registration));
			Assert.True(registration!.RemainingDecayTimeMillis > 0);

			var close = service.CloseDropList(player, 5001);

			Assert.Equal(WorldNpcLootStatus.Closed, close.Status);
			Assert.True(spawnService.HasDecayTask(5001));
			Assert.Equal(1, spawnService.PendingDecayCount);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public void RequestDropItem_DeletesWorldCorpseAndUnregistersDropsWhenLastItemIsCollected()
	{
		var dropRegistration = new WorldNpcDropRegistrationService();
		dropRegistration.RegisterDrop(5001, looterObjectId: 1001, drops: [new WorldNpcDropItem(1, 182400002, 1)]);
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var spawnService = new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager: null,
			connectionRegistry: null,
			staticPlaceables: null,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			logger: NullLogger<WorldNpcSpawnService>.Instance);
		var npc = new WorldNpc(
			5001,
			203001,
			new NpcTemplateSummary(203001, "loot_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NON_ATTACKABLE"),
			new WorldPosition(210010000, 1, 2, 3, 0));
		Assert.True(world.TryAddObject(npc.ObjectId, npc));
		var service = new WorldNpcLootService(dropRegistration, spawnService);
		var player = CreatePlayer(1001);
		Assert.Equal(WorldNpcLootStatus.Opened, service.RequestDropList(player, 5001).Status);

		var result = service.RequestDropItem(player, 5001, itemIndex: 1, CreateItemTemplates(), () => 9001);

		Assert.Equal(WorldNpcLootStatus.ItemCollected, result.Status);
		Assert.False(world.TryGetObject(5001, out _));
		Assert.False(dropRegistration.TryGetRegistration(5001, out _));
		Assert.Empty(dropRegistration.GetCurrentDrops(5001));
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			CreatureState = PlayerCreatureState.Active,
		};
	}

	private static WorldNpc CreateNpc(int objectId)
	{
		return new WorldNpc(
			objectId,
			203001,
			new NpcTemplateSummary(203001, "loot_npc", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NON_ATTACKABLE"),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				TemplateId: 182400002,
				Name: "drop_item",
				DescriptionId: 0,
				Mask: 0,
				Level: 1,
				ItemGroup: "NORMAL",
				ItemType: "NORMAL",
				Quality: "COMMON",
				Race: "ALL",
				MaxStackCount: 100,
				Price: 0,
				ValidEquipmentSlots: 0),
			new ItemTemplateSummary(
				TemplateId: 182400004,
				Name: "limit_one_item",
				DescriptionId: 0,
				Mask: 1,
				Level: 1,
				ItemGroup: "NORMAL",
				ItemType: "NORMAL",
				Quality: "COMMON",
				Race: "ALL",
				MaxStackCount: 1,
				Price: 0,
				ValidEquipmentSlots: 0),
			new ItemTemplateSummary(
				TemplateId: 182400003,
				Name: "remaining_item",
				DescriptionId: 0,
				Mask: 0,
				Level: 1,
				ItemGroup: "NORMAL",
				ItemType: "NORMAL",
				Quality: "COMMON",
				Race: "ALL",
				MaxStackCount: 100,
				Price: 0,
				ValidEquipmentSlots: 0),
		]);
	}
}
