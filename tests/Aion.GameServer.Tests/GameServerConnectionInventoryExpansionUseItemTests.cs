using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionInventoryExpansionUseItemTests
{
	[Fact]
	public async Task HandleUseItemAsync_CubeExpansionTicketConsumesItemAndRefreshesCubeSize()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169630000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.ItemExpands);
		Assert.Equal(36, InventoryCapacity.GetCubeLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_WarehouseExpansionTicketConsumesItemAndRefreshesWarehouseInfo()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169640000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, player.WarehouseBonusExpands);
		Assert.Equal(32, InventoryCapacity.GetWarehouseLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_WarehouseExpansionTicketAllowsQuestOffsetLikeJava()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync();
		var player = CreatePlayer(itemId: 169640000);
		player.WarehouseBonusExpands = 1;
		player.Quests = [new PlayerQuestState(1987, "COMPLETE", 0, 0, 0)];

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(2, player.WarehouseBonusExpands);
		Assert.Equal(40, InventoryCapacity.GetWarehouseLimit(player));
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(1, sourceItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => Assert.IsType<SmItemUsageAnimation>(packet),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet),
			packet => Assert.IsType<SmWarehouseInfo>(packet));
	}

	[Theory]
	[InlineData(169630000)]
	[InlineData(169640000)]
	public async Task HandleUseItemAsync_InventoryExpansionPersistenceFailureDoesNotMutateRuntimeState(int itemId)
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveInventoryExpansionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository);
		var player = CreatePlayer(itemId);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(1, repository.SaveInventoryExpansionMutationCalls);
		Assert.Equal(0, player.ItemExpands);
		Assert.Equal(0, player.WarehouseBonusExpands);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleUseItemAsync_AnimationAddSchedulesPositiveTimeUseAndClearsUsingItem()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 188500000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		var startAnimation = Assert.Single(fixture.SentPackets.OfType<SmItemUsageAnimation>());
		using (var reader = new PacketBuffer(SerializeUnencryptedPayload(startAnimation)))
		{
			Assert.Equal(1001, reader.ReadD());
			Assert.Equal(1001, reader.ReadD());
			Assert.Equal(5001, reader.ReadD());
			Assert.Equal(188500000, reader.ReadD());
			Assert.Equal(1000, reader.ReadD());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(0, (int)reader.ReadC());
			Assert.Equal(1, (int)reader.ReadC());
			Assert.Equal(0, reader.ReadD());
			Assert.Equal(0, reader.Remaining);
		}

		await WaitUntilAsync(() => player.UsingItemObjectId == 0);
	}

	[Fact]
	public async Task HandleEmotionAsync_AnimationAddPendingUseCancelsAndSendsEndState()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 188500000);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.SelectTarget));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedTime: 1000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedTime: 0, expectedEnd: 3),
			packet => Assert.IsType<SmSystemMessage>(packet));
		await Task.Delay(1100);
		Assert.Equal(3, fixture.SentPackets.Count);
	}

	[Fact]
	public async Task HandleEmotionAsync_DecomposePendingUseCancelsAndSendsEndState()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 100);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));
		await InvokeHandleEmotionAsync(fixture.Connection, player, CreateEmotion(EmotionType.SelectTarget));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 2),
			packet => Assert.IsType<SmSystemMessage>(packet));
		await Task.Delay(3100);
		Assert.Equal(3, fixture.SentPackets.Count);
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeCompletesAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		await WaitUntilAsync(() => fixture.SentPackets.Count >= 5, TimeSpan.FromSeconds(5));
		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(100, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(200, item.ItemId);
				Assert.Equal(1, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeDeletesLastSourceAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(
			includeThreadPoolManager: true,
			idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100, count: 1);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(5001, player.UsingItemObjectId);
		await WaitUntilAsync(() => fixture.SentPackets.Count >= 5, TimeSpan.FromSeconds(5));
		Assert.Equal(0, player.UsingItemObjectId);
		var reward = Assert.Single(player.InventoryItems);
		Assert.Equal(200, reward.ItemId);
		Assert.Equal(1, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 3000, expectedEnd: 0),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 5001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 100, expectedTime: 0, expectedEnd: 1),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeInventoryFullDoesNotScheduleOrMutate()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 100);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 26).Select(index => new InventoryItem
			{
				ObjectId = 6000 + index,
				ItemId = 201,
				Count = 1,
				Location = 0,
			}))
			.ToArray();
		var originalInventory = player.InventoryItems
			.OrderBy(item => item.ObjectId)
			.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
			.ToArray();

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(
			originalInventory,
			player.InventoryItems
				.OrderBy(item => item.ObjectId)
				.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
				.ToArray());
		var packet = Assert.Single(fixture.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300447);
	}

	[Fact]
	public async Task HandleUseItemAsync_DecomposeSpecialCubeFullDoesNotScheduleOrMutate()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 102);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 102).Select(index => new InventoryItem
			{
				ObjectId = 7000 + index,
				ItemId = 205,
				Count = 1,
				Location = 0,
			}))
			.ToArray();
		var originalInventory = player.InventoryItems
			.OrderBy(item => item.ObjectId)
			.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
			.ToArray();

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(
			originalInventory,
			player.InventoryItems
				.OrderBy(item => item.ObjectId)
				.Select(item => (item.ObjectId, item.ItemId, item.Count, item.Location, item.IsEquipped))
				.ToArray());
		var packet = Assert.Single(fixture.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(packet), expectedMessageId: 1300447);
	}

	[Fact]
	public async Task HandleUseItemAsync_SelectableDecomposeShowsChoicesWithoutSchedulingUse()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(itemId: 101);

		await fixture.Connection.HandleUseItemAsync(player, CreateUseItem(sourceItemObjectId: 5001));

		Assert.Equal(0, player.UsingItemObjectId);
		var packet = Assert.Single(fixture.SentPackets);
		AssertFirstShowDecomposablePayload(Assert.IsType<SmFirstShowDecomposable>(packet));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardConsumesSourceAndAddsReward()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_FullCubeStillAddsSelectableRewardLikeJavaOverflow()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(Enumerable.Range(0, 26).Select(index => new InventoryItem
			{
				ObjectId = 6000 + index,
				ItemId = 200,
				Count = 1,
				Location = 0,
			}))
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Equal(1, repository.SaveDecomposeActionMutationCalls);
		Assert.Equal(28, InventoryCapacity.GetUsedCubeSlots(player));
		Assert.Contains(player.InventoryItems, item => item is { ObjectId: 5001, ItemId: 101, Count: 1 });
		var reward = Assert.Single(player.InventoryItems, item => item.ItemId == 202);
		Assert.Equal(3, reward.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(99)]
	public async Task HandleSelectDecomposableAsync_InvalidSelectableRewardIndexDoesNotMutateInventory(int index)
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index));

		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardDeletesSingleCountSource()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, count: 1);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		var rewardItem = Assert.Single(player.InventoryItems);
		Assert.Equal(201, rewardItem.ItemId);
		Assert.Equal(2, rewardItem.Count);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmDeleteItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_SelectableRewardMergesExistingStack()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001, 6001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 4,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(201, item.ItemId);
				Assert.Equal(6, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 6001, expectedUpdateType: SmInventoryUpdateItem.IncreaseItemCollect));
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_PersistenceFailureDoesNotMutateRuntimeInventory()
	{
		var repository = new EmptyPlayerEnterWorldRepository { SaveDecomposeActionMutationResult = false };
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001, 6001]));
		var player = CreatePlayer(itemId: 101);
		player.InventoryItems = player.InventoryItems
			.Concat(
			[
				new InventoryItem
				{
					ObjectId = 6001,
					ItemId = 201,
					Count = 4,
					Location = 0,
				},
			])
			.ToArray();

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(1, repository.SaveDecomposeActionMutationCalls);
		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ObjectId),
			item =>
			{
				Assert.Equal(5001, item.ObjectId);
				Assert.Equal(101, item.ItemId);
				Assert.Equal(2, item.Count);
			},
			item =>
			{
				Assert.Equal(6001, item.ObjectId);
				Assert.Equal(201, item.ItemId);
				Assert.Equal(4, item.Count);
			});
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_MissingSourceDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 9999, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_NonSelectableSourceDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 100);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(100, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task HandleSelectDecomposableAsync_MissingRewardTemplateDoesNotCallPersistenceOrSendPackets()
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, race: "ASMODIANS", playerClass: "GLADIATOR");

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 1));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Empty(fixture.SentPackets);
	}

	[Theory]
	[InlineData(true, 0)]
	[InlineData(false, 1)]
	public async Task HandleSelectDecomposableAsync_NonCubeOrEquippedSourceDoesNotCallPersistenceOrSendPackets(bool isEquipped, int location)
	{
		var repository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(repository, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101, isEquipped: isEquipped, location: location);

		await InvokeHandleSelectDecomposableAsync(fixture.Connection, player, CreateSelectDecomposable(sourceItemObjectId: 5001, index: 0));

		Assert.Equal(0, repository.SaveDecomposeActionMutationCalls);
		var sourceItem = Assert.Single(player.InventoryItems);
		Assert.Equal(101, sourceItem.ItemId);
		Assert.Equal(2, sourceItem.Count);
		Assert.Equal(isEquipped, sourceItem.IsEquipped);
		Assert.Equal(location, sourceItem.Location);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_SelectDecomposableDispatchesSelection()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(236, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteD(0);
				buffer.WriteC(1);
			}));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => Assert.IsType<SmInventoryAddItem>(packet));
	}

	[Fact]
	public async Task RunAsync_EncryptedSelectDecomposableFrameDispatchesSelection()
	{
		await using var fixture = await InventoryExpansionUseItemFixture.CreateAsync(enableCryptKeyBeforeRun: false, idFactory: new IDFactory([5001]));
		var player = CreatePlayer(itemId: 101);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var runTask = Task.Run(() => fixture.Connection.RunAsync());

		await fixture.ReadServerFrameAsync();
		await fixture.WriteClientFrameAsync(
			CreateEncryptedClientFrame(
				CreateClientPayload(236, buffer =>
				{
					buffer.WriteD(5001);
					buffer.WriteD(0);
					buffer.WriteC(1);
				})));

		await WaitUntilAsync(() => player.InventoryItems.Any(item => item.ItemId == 202));

		Assert.Collection(
			player.InventoryItems.OrderBy(item => item.ItemId),
			item =>
			{
				Assert.Equal(101, item.ItemId);
				Assert.Equal(1, item.Count);
			},
			item =>
			{
				Assert.Equal(202, item.ItemId);
				Assert.Equal(3, item.Count);
			});
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmKey>(packet),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemId: 101, expectedTime: 0, expectedEnd: 1, expectedUnknown3: 1),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => Assert.IsType<SmInventoryUpdateItem>(packet),
			packet => AssertSecondaryShowDecomposablePayload(Assert.IsType<SmSecondaryShowDecomposable>(packet)),
			packet => Assert.IsType<SmInventoryAddItem>(packet));

		await fixture.Connection.CloseAsync();
		await AssertCompletesAsync(runTask);
	}

	private static Player CreatePlayer(
		int itemId,
		long count = 2,
		string race = "ELYOS",
		string playerClass = "RANGER",
		bool isEquipped = false,
		int location = 0)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = race,
			PlayerClass = playerClass,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = itemId,
					Count = count,
					Location = location,
					IsEquipped = isEquipped,
				},
			],
		};
	}

	private static CmUseItem CreateUseItem(int sourceItemObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteC(0);
		var packet = new CmUseItem(37, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmEmotion CreateEmotion(EmotionType emotionType)
	{
		using var writer = new PacketBuffer();
		writer.WriteC((byte)emotionType);
		var packet = new CmEmotion(43, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static CmSelectDecomposable CreateSelectDecomposable(int sourceItemObjectId, int index)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(sourceItemObjectId);
		writer.WriteD(0);
		writer.WriteC(index);
		var packet = new CmSelectDecomposable(236, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] CreateEncryptedClientFrame(byte[] payload)
	{
		var encryptedPayload = payload.ToArray();
		EncryptClientPayload(encryptedPayload, 0x01020304);
		return GamePacketFrameCodec.CreateFrame(encryptedPayload);
	}

	private static void EncryptClientPayload(Span<byte> data, int baseKey)
	{
		var staticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");
		Span<byte> clientKey =
		[
			(byte)(baseKey & 0xff),
			(byte)((baseKey >> 8) & 0xff),
			(byte)((baseKey >> 16) & 0xff),
			(byte)((baseKey >> 24) & 0xff),
			0xa1,
			0x6c,
			0x54,
			0x87,
		];

		if (data.Length == 0)
			return;

		data[0] ^= clientKey[0];
		var previous = data[0];

		for (var i = 1; i < data.Length; i++)
		{
			data[i] ^= (byte)(staticKey[i & 63] ^ clientKey[i & 7] ^ previous);
			previous = data[i];
		}

		UpdateClientKey(clientKey, data.Length);
	}

	private static void UpdateClientKey(Span<byte> key, int packetSize)
	{
		var oldKey = BinaryPrimitives.ReadUInt64LittleEndian(key);
		oldKey += (uint)packetSize;
		BinaryPrimitives.WriteUInt64LittleEndian(key, oldKey);
	}

	private static async Task InvokeHandleEmotionAsync(GameServerConnection connection, Player player, CmEmotion packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleEmotionAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeHandleSelectDecomposableAsync(GameServerConnection connection, Player player, CmSelectDecomposable packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleSelectDecomposableAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"ProcessPacketAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	private static void SetActivePlayerForPacketDispatch(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		Assert.NotNull(stateField);
		activePlayerField.SetValue(connection, player);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private static void AssertItemUsagePayload(
		SmItemUsageAnimation packet,
		int expectedItemId = 188500000,
		int expectedTime = 0,
		int expectedEnd = 0,
		int expectedUnknown3 = 0)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		Assert.Equal(expectedTime, reader.ReadD());
		Assert.Equal(expectedEnd, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(expectedUnknown3, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertInventoryUpdatePayload(SmInventoryUpdateItem packet, int expectedObjectId, int expectedUpdateType)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(expectedObjectId, reader.ReadD());
		var actualUpdateType = payload[^2] | (payload[^1] << 8);
		Assert.Equal(expectedUpdateType, actualUpdateType);
	}

	private static void AssertDeleteItemPayload(SmDeleteItem packet, int expectedObjectId, int expectedDeleteType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedDeleteType, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSystemMessagePayload(SmSystemMessage packet, int expectedMessageId, params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertFirstShowDecomposablePayload(SmFirstShowDecomposable packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(201, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(202, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSecondaryShowDecomposablePayload(SmSecondaryShowDecomposable packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(5001, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan? timeout = null)
	{
		var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(3));
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (predicate())
				return;

			await Task.Delay(25);
		}

		Assert.True(predicate());
	}

	private static async Task AssertCompletesAsync(Task task)
	{
		var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(task, completed);
		await task;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class InventoryExpansionUseItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;
		private readonly ThreadPoolManager? _threadPoolManager;
		private readonly string _tempRoot;

		private InventoryExpansionUseItemFixture(
			TcpClient client,
			GameServerConnection connection,
			ThreadPoolManager? threadPoolManager,
			List<GameServerPacket> sentPackets,
			string tempRoot)
		{
			_client = client;
			_connection = connection;
			_threadPoolManager = threadPoolManager;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection => _connection;

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<InventoryExpansionUseItemFixture> CreateAsync(
			EmptyPlayerEnterWorldRepository? repository = null,
			bool includeThreadPoolManager = false,
			IDFactory? idFactory = null,
			bool enableCryptKeyBeforeRun = true)
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-inventory-expansion-use-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<player_experience_table>
						<exp>0</exp>
						<exp>100</exp>
					</player_experience_table>
					<item_templates>
						<item_template id="169630000" name="[Expand Card] Expand Cube Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="CUBE" />
							</actions>
						</item_template>
						<item_template id="169640000" name="[Expand Card] Expand Warehouse Ticket (lvl 1)" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<expandinventory level="1" storage="WAREHOUSE" />
							</actions>
						</item_template>
						<item_template id="188500000" name="[Motion Card] Test Motion" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<animation idle="1" run="2" jump="3" rest="4" minutes="60" />
							</actions>
						</item_template>
						<item_template id="100" name="Test Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="101" name="Test Selectable Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="102" name="Test Special Decompose Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<actions>
								<decompose/>
							</actions>
						</item_template>
						<item_template id="200" name="Test Decompose Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="201" name="Test Selectable Reward 1" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="202" name="Test Selectable Reward 2" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
						<item_template id="204" name="Test Special Decompose Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<inventory id="2"/>
						</item_template>
						<item_template id="205" name="Test Special Cube Filler" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1">
							<inventory id="2"/>
						</item_template>
					</item_templates>
					<decomposable_items>
						<decomposable item_id="100">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="200" min_count="1" max_count="1"/>
							</items>
						</decomposable>
						<decomposable item_id="102">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="204" min_count="1" max_count="1"/>
							</items>
						</decomposable>
						<decomposable item_id="101" selectable="true">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="201" min_count="2" max_count="2" race="ELYOS" player_classes="RANGER"/>
								<item id="202" min_count="3" max_count="3"/>
								<item id="203" min_count="1" max_count="1" race="ASMODIANS"/>
							</items>
						</decomposable>
					</decomposable_items>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var sentPackets = new List<GameServerPacket>();
			var world = new Aion.GameServer.World.World(NullLogger<Aion.GameServer.World.World>.Instance);
			world.Initialize();
			var playerEnterWorldService = repository == null
				? null
				: new PlayerEnterWorldService(
					new GameServerOptions(),
					repository,
					world,
					NullLogger<PlayerEnterWorldService>.Instance);
			var threadPoolManager = includeThreadPoolManager
				? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance)
				: null;

			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				if (enableCryptKeyBeforeRun)
					crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"inventory-expansion-use-item-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					playerEnterWorldService: playerEnterWorldService,
					threadPoolManager: threadPoolManager,
					idFactory: idFactory,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new InventoryExpansionUseItemFixture(client, connection, threadPoolManager, sentPackets, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async Task<byte[]> ReadServerFrameAsync()
		{
			var stream = _client.GetStream();
			var header = await ReadExactAsync(stream, 2);
			var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
			var payload = await ReadExactAsync(stream, length - 2);
			var frame = new byte[length];
			header.CopyTo(frame, 0);
			payload.CopyTo(frame.AsSpan(2));
			return frame;
		}

		public async Task WriteClientFrameAsync(byte[] frame)
		{
			var stream = _client.GetStream();
			await stream.WriteAsync(frame);
			await stream.FlushAsync();
		}

		private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var buffer = new byte[length];
			var offset = 0;
			while (offset < length)
			{
				var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cts.Token);
				if (read == 0)
					throw new EndOfStreamException("Socket closed before the expected frame was read.");
				offset += read;
			}

			return buffer;
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			if (_threadPoolManager != null)
				await _threadPoolManager.DisposeAsync();
			_client.Dispose();
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
