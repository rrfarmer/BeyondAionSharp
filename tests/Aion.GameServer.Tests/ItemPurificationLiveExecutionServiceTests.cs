using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationLiveExecutionServiceTests
{
	[Fact]
	public async Task ExecuteAsync_SendsSuccessBeforeLiveMutationThenSendsMutationFanout()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		var registry = new RecordingConnectionRegistry(
			packet => new SentPacketRecord(
				player.ObjectId,
				packet,
				player.AbyssRank.Ap,
				player.InventoryItems.Select(item => item.ObjectId).Order().ToArray()));

		var result = await ItemPurificationLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1,
			registry);

		Assert.True(result.Succeeded);
		Assert.Equal(ItemPurificationLiveExecutionStatus.Ready, result.Status);
		Assert.NotNull(result.HandlerBridge);
		Assert.True(result.HandlerBridge.Succeeded);
		Assert.NotNull(result.SuccessMessageSend);
		Assert.Equal(1, result.SuccessMessageSend.SentCount);
		Assert.NotNull(result.LiveMutation);
		Assert.True(result.LiveMutation.Succeeded);
		Assert.NotNull(result.MutationPacketSend);
		Assert.Equal(5, result.MutationPacketSend.SentCount);
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			result.MutationPacketSend.SkippedMetadataOperations.Select(operation => operation.Type).ToArray());
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			registry.SentPackets.Select(packet => packet.Packet.GetType()).ToArray());
		Assert.Equal(5_000, registry.SentPackets[0].AbyssPointsAtSend);
		Assert.Equal([10, 20, 30], registry.SentPackets[0].InventoryObjectIdsAtSend);
		Assert.All(registry.SentPackets.Skip(1), sent =>
		{
			Assert.Equal(3_800, sent.AbyssPointsAtSend);
			Assert.Equal([20, 30, 9001], sent.InventoryObjectIdsAtSend);
		});
		Assert.Equal(3_800, player.AbyssRank.Ap);
		Assert.Equal([20, 30, 9001], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.Equal(1, player.InventoryItems.Single(item => item.ObjectId == material.ObjectId).Count);
		Assert.Equal(10_000, player.InventoryItems.Single(item => item.ObjectId == kinah.ObjectId).Count);
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotMutateOrSendWhenHandlerBridgeIsNotReady()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		player.InventoryItems = [baseItem, kinah];
		var registry = new RecordingConnectionRegistry(
			packet => new SentPacketRecord(
				player.ObjectId,
				packet,
				player.AbyssRank.Ap,
				player.InventoryItems.Select(item => item.ObjectId).Order().ToArray()));

		var result = await ItemPurificationLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0,
			registry);

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationLiveExecutionStatus.HandlerBridgeNotReady, result.Status);
		Assert.NotNull(result.HandlerBridge);
		Assert.Equal(ItemPurificationHandlerMutationBridgeStatus.MutationSnapshotNotReady, result.HandlerBridge.Status);
		Assert.Null(result.SuccessMessageSend);
		Assert.Null(result.LiveMutation);
		Assert.Null(result.MutationPacketSend);
		Assert.Empty(registry.SentPackets);
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Equal([10, 30], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.Equal(3, material.Count);
	}

	[Fact]
	public async Task ExecuteAsync_RankDropKeepsApSpendPacketsModeledButNotSent()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(
			abyssPoints: 1_300,
			baseItem,
			material,
			kinah);
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 1_300, Rank = 2, MaxRank = 2 };
		var itemTemplates = CreateItemTemplates();
		var handlerPlan = CreateHandlerPlan(player, baseItem, itemTemplates, targetObjectId: 9001);
		var registry = new RecordingConnectionRegistry(
			packet => new SentPacketRecord(
				player.ObjectId,
				packet,
				player.AbyssRank.Ap,
				player.InventoryItems.Select(item => item.ObjectId).Order().ToArray()));

		var result = await ItemPurificationLiveExecutionService.ExecuteAsync(
			player.ObjectId,
			player,
			handlerPlan,
			itemTemplates,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1,
			registry);

		Assert.True(result.Succeeded);
		var abyssPointsPlan = Assert.IsType<AbyssPointsAddPlan>(result.LiveMutation?.AbyssPointsPlan);
		Assert.Equal(2, abyssPointsPlan.OldRank);
		Assert.Equal(1, abyssPointsPlan.UpdatedRank?.Rank);
		Assert.Equal(-1_200, abyssPointsPlan.Added);
		Assert.True(abyssPointsPlan.ShouldCheckRankLimitItems);
		Assert.True(abyssPointsPlan.ShouldUpdateAbyssSkills);
		Assert.NotNull(abyssPointsPlan.RankUpdatePacket);
		Assert.Equal(
			[typeof(SmSystemMessage), typeof(SmAbyssRank)],
			abyssPointsPlan.PlayerPackets.Select(packet => packet.GetType()).ToArray());
		Assert.Equal(100, player.AbyssRank.Ap);
		Assert.Equal(1, player.AbyssRank.Rank);

		Assert.Equal(5, result.MutationPacketSend?.SentCount);
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			result.MutationPacketSend?.SkippedMetadataOperations.Select(operation => operation.Type).ToArray());
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			registry.SentPackets.Select(packet => packet.Packet.GetType()).ToArray());
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
		private readonly Func<GameServerPacket, SentPacketRecord> _createRecord;

		public RecordingConnectionRegistry(Func<GameServerPacket, SentPacketRecord> createRecord)
		{
			_createRecord = createRecord;
		}

		public List<SentPacketRecord> SentPackets { get; } = [];

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
			SentPackets.Add(_createRecord(packet));
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

	private sealed record SentPacketRecord(
		int PlayerObjectId,
		GameServerPacket Packet,
		int AbyssPointsAtSend,
		IReadOnlyList<int> InventoryObjectIdsAtSend);
}
