using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class ExpirableTaskServiceTests
{
	[Fact]
	public async Task Tick_RemovesExpiredEmotionTitleAndMotion()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Kahrun",
			Position = new WorldPosition(210010000, 1, 2, 3, 32),
			TitleId = 5,
			BonusTitleId = 6,
			Emotions =
			[
				new PlayerEmotion(64, 999),
				new PlayerEmotion(65, 0),
			],
			Titles =
			[
				new PlayerTitle(5, 999),
				new PlayerTitle(6, 999),
				new PlayerTitle(7, 0),
			],
			Motions =
			[
				new PlayerMotion(11, 999, true),
				new PlayerMotion(12, 0, true),
			],
		};
		var sentPackets = new List<GameServerPacket>();
		var broadcastPackets = new List<GameServerPacket>();
		var titleTemplates = new TitleTemplateTable(
		[
			new TitleTemplateSummary(5, 412994, "display", "PC_ALL", Array.Empty<ItemStatModifier>()),
			new TitleTemplateSummary(6, 412995, "bonus", "PC_ALL", Array.Empty<ItemStatModifier>()),
			new TitleTemplateSummary(7, 412996, "permanent", "PC_ALL", Array.Empty<ItemStatModifier>()),
		]);

		service.RegisterPlayerExpirables(
			player,
			packet =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			packet =>
			{
				broadcastPackets.Add(packet);
				return Task.CompletedTask;
			},
			titleTemplates);
		await service.TickAsync();

		Assert.Equal([65], player.Emotions.Select(emotion => emotion.Id).ToArray());
		Assert.Equal([7], player.Titles.Select(title => title.Id).ToArray());
		Assert.Equal(-1, player.TitleId);
		Assert.Equal(-1, player.BonusTitleId);
		Assert.Equal([12], player.Motions.Select(motion => motion.Id).ToArray());
		Assert.Contains(sentPackets, packet => packet is SmEmotionList);
		Assert.Contains(sentPackets, packet => packet is SmTitleInfo);
		Assert.Contains(sentPackets, packet => packet is SmMotion);
		Assert.Contains(broadcastPackets, packet => packet is SmTitleInfo);
	}

	[Fact]
	public async Task Tick_RemovesExpiredInventoryAndWarehouseItems()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var player = new Player
		{
			ObjectId = 1001,
			AccountId = 77,
			Name = "Kahrun",
			InventoryItems =
			[
				new InventoryItem { ObjectId = 10, ItemId = 100, Count = 1, OwnerId = 1001, Location = 0, ExpireTime = 999 },
				new InventoryItem { ObjectId = 11, ItemId = 101, Count = 1, OwnerId = 1001, Location = 0 },
			],
			WarehouseItems =
			[
				new InventoryItem { ObjectId = 20, ItemId = 102, Count = 1, OwnerId = 1001, Location = 1, ExpireTime = 999 },
			],
			AccountWarehouseItems =
			[
				new InventoryItem { ObjectId = 30, ItemId = 103, Count = 1, OwnerId = 77, Location = 2, ExpireTime = 999 },
			],
		};
		var sentPackets = new List<GameServerPacket>();
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100),
			CreateTemplate(101),
			CreateTemplate(102),
			CreateTemplate(103),
		]);

		service.RegisterPlayerExpirables(
			player,
			packet =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			itemTemplates: itemTemplates);
		await service.TickAsync();

		Assert.Equal([11], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Empty(player.WarehouseItems);
		Assert.Empty(player.AccountWarehouseItems);
		Assert.Contains(sentPackets, packet => packet is SmDeleteItem);
		Assert.Equal(2, sentPackets.Count(packet => packet is SmDeleteWarehouseItem));
		Assert.Contains(sentPackets, packet => packet is SmCubeUpdate);
		Assert.Equal(3, sentPackets.Count(packet => packet is SmSystemMessage));
	}

	[Fact]
	public async Task Tick_SendsItemBeforeExpireNoticeAtJavaMinuteThreshold()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Kahrun",
			InventoryItems =
			[
				new InventoryItem { ObjectId = 10, ItemId = 100, Count = 1, OwnerId = 1001, Location = 0, ExpireTime = 1300 },
			],
		};
		var sentPackets = new List<GameServerPacket>();
		var itemTemplates = new ItemTemplateTable([CreateTemplate(100)]);

		service.RegisterPlayerExpirables(
			player,
			packet =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			itemTemplates: itemTemplates);
		await service.TickAsync();

		Assert.Equal([10], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Single(sentPackets);
		Assert.IsType<SmSystemMessage>(sentPackets[0]);
	}

	[Fact]
	public async Task RegisterInventoryItem_TracksNewExpirableItems()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var player = new Player { ObjectId = 1001, Name = "Kahrun" };
		var addedItem = new InventoryItem { ObjectId = 10, ItemId = 100, Count = 1, OwnerId = 1001, Location = 0, ExpireTime = 999 };
		var sentPackets = new List<GameServerPacket>();

		service.RegisterPlayerExpirables(
			player,
			packet =>
			{
				sentPackets.Add(packet);
				return Task.CompletedTask;
			},
			itemTemplates: new ItemTemplateTable([CreateTemplate(100)]));
		player.InventoryItems = [addedItem];
		service.RegisterInventoryItem(player, addedItem);
		await service.TickAsync();

		Assert.Empty(player.InventoryItems);
		Assert.Contains(sentPackets, packet => packet is SmDeleteItem);
	}

	[Fact]
	public async Task Tick_ExpiresLoadedActiveHouseObjectsAndDefersFinalRewardObjects()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var normalObject = new RegisteredHouseObjectSummary(9001, 3001000, ExpirationSeconds: -1, ExpireTimeSeconds: 999);
		var finalRewardObject = new RegisteredHouseObjectSummary(9002, 3190013, ExpirationSeconds: -1, ExpireTimeSeconds: 999);
		var emblemObject = new RegisteredHouseObjectSummary(9003, 3200001, ExpirationSeconds: -1, ExpireTimeSeconds: 999);
		var registry = new HouseRegistrySummary([normalObject, finalRewardObject, emblemObject], Array.Empty<RegisteredHouseDecorationSummary>());
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Kahrun",
			Houses = [new PlayerHouse(51, 700100, 353000, DateTime.UtcNow, null, IsInactive: false, Registry: registry)],
		};
		var housingObjectTemplates = new HousingObjectTemplateTable(
		[
			new HousingObjectTemplateSummary(3001000, 0, "passive", "INTERIOR", "FLOOR", "NONE", "DECORATION", 1, false),
			new HousingObjectTemplateSummary(3190013, 1, "use_item", "INTERIOR", "STACK", "POT", "DECORATION", 25, false, UseActionFinalRewardId: 188051555),
			new HousingObjectTemplateSummary(3200001, 11, "emblem", "INTERIOR", "FLOOR", "NONE", "DECORATION", 1, false),
		]);
		var expiredObjects = new List<int>();

		service.RegisterPlayerExpirables(
			player,
			_ => Task.CompletedTask,
			housingObjectTemplates: housingObjectTemplates,
			expireHouseObjectAsync: (house, houseObject, _) =>
			{
				expiredObjects.Add(houseObject.ObjectId);
				player.Houses = player.Houses
					.Select(existingHouse => existingHouse.ObjectId == house.ObjectId
						? existingHouse with { Registry = existingHouse.Registry!.WithoutObject(houseObject.ObjectId) }
						: existingHouse)
					.ToArray();
				return Task.CompletedTask;
			});
		await service.TickAsync();

		Assert.Equal([9001], expiredObjects);
		var remainingObjects = Assert.Single(player.Houses).Registry!.Objects.Select(obj => obj.ObjectId).Order().ToArray();
		Assert.Equal([9002, 9003], remainingObjects);
	}

	[Fact]
	public async Task Tick_KeepsExactExpiryAndUnregisteredPlayers()
	{
		await using var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		var now = DateTimeOffset.FromUnixTimeSeconds(1000);
		var service = new ExpirableTaskService(
			threadPoolManager,
			new EmptyPlayerEnterWorldRepository(),
			NullLogger<ExpirableTaskService>.Instance,
			() => now);
		var exactExpiryPlayer = new Player
		{
			ObjectId = 1001,
			Name = "Exact",
			Emotions = [new PlayerEmotion(64, 1000)],
		};
		var unregisteredPlayer = new Player
		{
			ObjectId = 1002,
			Name = "Gone",
			Emotions = [new PlayerEmotion(65, 999)],
		};

		service.RegisterPlayerExpirables(exactExpiryPlayer, _ => Task.CompletedTask);
		service.RegisterPlayerExpirables(unregisteredPlayer, _ => Task.CompletedTask);
		service.UnregisterPlayer(unregisteredPlayer);
		await service.TickAsync();

		Assert.Equal([64], exactExpiryPlayer.Emotions.Select(emotion => emotion.Id).ToArray());
		Assert.Equal([65], unregisteredPlayer.Emotions.Select(emotion => emotion.Id).ToArray());
	}

	private static ItemTemplateSummary CreateTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			"Reward",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NONE",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0);
	}
}
