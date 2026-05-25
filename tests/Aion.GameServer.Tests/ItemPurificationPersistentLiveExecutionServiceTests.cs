using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationPersistentLiveExecutionServiceTests
{
	[Fact]
	public async Task ExecuteAsync_PersistsPayloadAfterReadyLiveExecution()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		var registry = new RecordingConnectionRegistry();
		var repository = new EmptyPlayerEnterWorldRepository();

		var result = await ItemPurificationPersistentLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1,
			registry,
			repository);

		Assert.True(result.Succeeded);
		Assert.Equal(ItemPurificationPersistentLiveExecutionStatus.Ready, result.Status);
		Assert.NotNull(result.LiveExecution);
		Assert.True(result.LiveExecution.Succeeded);
		Assert.NotNull(result.PersistencePlan);
		Assert.True(result.PersistencePlan.Succeeded);
		Assert.True(result.PersistenceSaved);
		Assert.Equal(1, repository.SaveItemPurificationMutationCalls);
		var materialUpdate = Assert.Single(repository.ItemPurificationMaterialItemUpdates);
		Assert.Equal(material.ObjectId, materialUpdate.ObjectId);
		Assert.Equal(1, materialUpdate.Count);
		Assert.Empty(repository.ItemPurificationDeletedMaterialItemObjectIds);
		Assert.Null(repository.ItemPurificationBaseItemUpdate);
		Assert.Equal(baseItem.ObjectId, repository.ItemPurificationDeletedBaseItemObjectId);
		var targetAdd = Assert.Single(repository.ItemPurificationAddedTargetItems);
		Assert.Equal(9001, targetAdd.ObjectId);
		Assert.Equal(100000002, targetAdd.ItemId);
		Assert.Equal(20, targetAdd.Enchant);
		Assert.Empty(repository.ItemPurificationUpdatedTargetItems);
		Assert.NotNull(repository.ItemPurificationAbyssRank);
		Assert.Equal(3_800, repository.ItemPurificationAbyssRank.Ap);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			registry.SentPackets.Select(packet => packet.GetType()).ToArray());
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotPersistWhenLiveExecutionIsNotReady()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		player.InventoryItems = [baseItem, kinah];
		var repository = new EmptyPlayerEnterWorldRepository();

		var result = await ItemPurificationPersistentLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0,
			new RecordingConnectionRegistry(),
			repository);

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPersistentLiveExecutionStatus.LiveExecutionNotReady, result.Status);
		Assert.NotNull(result.LiveExecution);
		Assert.Equal(ItemPurificationLiveExecutionStatus.HandlerBridgeNotReady, result.LiveExecution.Status);
		Assert.Null(result.PersistencePlan);
		Assert.False(result.PersistenceSaved);
		Assert.Equal(0, repository.SaveItemPurificationMutationCalls);
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Equal([10, 30], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
	}

	[Fact]
	public async Task ExecuteAsync_ReportsPersistenceSaveFailureAfterReadyExecution()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		var repository = new EmptyPlayerEnterWorldRepository { SaveItemPurificationMutationResult = false };

		var result = await ItemPurificationPersistentLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1,
			new RecordingConnectionRegistry(),
			repository);

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPersistentLiveExecutionStatus.PersistenceSaveFailed, result.Status);
		Assert.NotNull(result.LiveExecution);
		Assert.True(result.LiveExecution.Succeeded);
		Assert.NotNull(result.PersistencePlan);
		Assert.True(result.PersistencePlan.Succeeded);
		Assert.False(result.PersistenceSaved);
		Assert.Equal(1, repository.SaveItemPurificationMutationCalls);
		Assert.Equal(3_800, player.AbyssRank.Ap);
		Assert.Equal([20, 30, 9001], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
	}

	private static ItemPurificationHandlerPlan CreateHandlerPlan(
		Player player,
		InventoryItem baseItem,
		ItemTemplateTable itemTemplates,
		int targetObjectId)
	{
		var workflow = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			itemTemplates,
			resultItemId: 100000002,
			targetObjectId);
		var application = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);
		var packetPlan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"item-100000001",
			"item-100000002");
		return new ItemPurificationHandlerPlan(workflow, application, packetPlan);
	}

	private static Player CreatePlayer(int abyssPoints, params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = abyssPoints },
			InventoryItems = items,
		};
	}

	private static InventoryItem CreateBaseItem(int enchant)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = enchant,
			TuneCount = 2,
			RandomBonus = 7,
		};
	}

	private static ItemPurificationTable CreatePurificationTable()
	{
		return new ItemPurificationTable(
		[
			new ItemPurificationSummary(
				100000001,
				[
					new ItemPurificationResultSummary(
						ResultItemId: 100000002,
						MinEnchantCount: 10,
						NecessaryAbyssPoints: 1_200,
						NecessaryKinah: 1_000,
						RequiredMaterials: [new ItemPurificationMaterialSummary(186000001, 2)]),
				]),
		]);
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100000001, statBonusSetId: 1, maxTuneCount: 5, maxEnchantLevel: 15),
			CreateTemplate(100000002, statBonusSetId: 1, maxTuneCount: 1, maxEnchantLevel: 20),
			CreateTemplate(186000001, statBonusSetId: 0, maxTuneCount: 0, maxEnchantLevel: 0),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int statBonusSetId,
		int maxTuneCount,
		int maxEnchantLevel)
	{
		return new ItemTemplateSummary(
			TemplateId: templateId,
			Name: $"item-{templateId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 65,
			ItemGroup: "SWORD",
			ItemType: "normal",
			Quality: "MYTHIC",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0,
			StatBonusSetId: statBonusSetId,
			MaxTuneCount: maxTuneCount,
			MaxEnchantLevel: maxEnchantLevel);
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<GameServerPacket> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add(packet);
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(IReadOnlyList<WorldHouse> houses, HousingTemplateTable? housingTemplates, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}
}

