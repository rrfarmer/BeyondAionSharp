using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionItemPurificationTests
{
	[Fact]
	public async Task HandleItemPurificationAsync_UsesActivePlayerBaseItemAndIgnoresPacketMaterialObjectIdsWithoutMutation()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Workflow.Succeeded);
		Assert.Equal(ItemPurificationApStatus.Allowed, handlerPlan.Workflow.Validation?.Status);
		Assert.Equal([20, 10], handlerPlan.Workflow.MaterialMutation?.DeletedObjectIds);
		Assert.Equal(1_200, handlerPlan.Workflow.MaterialMutation?.AbyssPointsToSpend);
		Assert.Equal(0, handlerPlan.Workflow.Inheritance?.TargetItem?.ObjectId);
		Assert.Equal(100000002, handlerPlan.Workflow.Inheritance?.TargetItem?.ItemId);
		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation, handlerPlan.Application.Status);
		Assert.Equal(ItemPurificationPacketPlanStatus.NeedsRuntimeInputs, handlerPlan.PacketPlan.Status);
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(2, material.Count);
		Assert.Equal(10_000, kinah.Count);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_ReturnsMissingBaseItemPlanWithoutThrowing()
	{
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 }],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: player.ObjectId,
			baseItemObjectId: 999,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [0, 0, 0, 0, 0]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(handlerPlan);
		Assert.Equal(ItemPurificationWorkflowStatus.MissingBaseItem, handlerPlan.Workflow.Status);
		Assert.Null(handlerPlan.Workflow.Validation);
		Assert.Null(handlerPlan.Workflow.MaterialMutation);
		Assert.Null(handlerPlan.Workflow.Inheritance);
		Assert.Equal(ItemPurificationApplicationPlanStatus.WorkflowNotPlanned, handlerPlan.Application.Status);
		Assert.Equal(ItemPurificationPacketPlanStatus.ApplicationPlanUnavailable, handlerPlan.PacketPlan.Status);
	}

	[Fact]
	public async Task HandleItemPurificationAsync_ComposesApplicationAndPacketPlansWhenTargetObjectIdProvided()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);

		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates(),
			targetObjectId: 9001);

		Assert.NotNull(handlerPlan);
		Assert.True(handlerPlan.Workflow.Succeeded);
		Assert.True(handlerPlan.Application.Succeeded);
		Assert.Equal(9001, handlerPlan.Application.TargetItem?.ObjectId);
		Assert.Equal(20, handlerPlan.Application.TargetItem?.Enchant);
		Assert.Equal(
			[
				ItemPurificationApplicationOperationType.DeleteMaterialItem,
				ItemPurificationApplicationOperationType.SpendAbyssPoints,
				ItemPurificationApplicationOperationType.PreserveKinahNoOp,
				ItemPurificationApplicationOperationType.DeleteBaseItem,
				ItemPurificationApplicationOperationType.AddTargetItem,
			],
			handlerPlan.Application.Operations.Select(operation => operation.Type).ToArray());
		Assert.True(handlerPlan.PacketPlan.Succeeded);
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.InventoryAddItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			handlerPlan.PacketPlan.Operations.Select(operation => operation.Type).ToArray());
		Assert.Equal(["item-100000001", "item-100000002"], handlerPlan.PacketPlan.Operations[0].Parameters);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public async Task ItemPurificationHandlerPacketBridge_ComposesConcretePacketsFromPostMutationSnapshots()
	{
		var baseItem = new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = 25,
			TuneCount = 2,
			RandomBonus = 7,
		};
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = 5_000 },
			InventoryItems = [baseItem, material, kinah],
		};
		await using var pair = await TestConnectionPair.CreateAsync();
		var itemTemplates = CreateItemTemplates();
		var packet = CreatePacket(
			playerObjectId: 9999,
			baseItemObjectId: baseItem.ObjectId,
			resultItemId: 100000002,
			requiredMaterialObjectIds: [9001, 9002, 9003, 9004, 9005]);
		var handlerPlan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			itemTemplates,
			targetObjectId: 9001);
		var postMutationItems = new[]
		{
			kinah,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
		};

		var bridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlan(
			handlerPlan,
			postMutationItems,
			itemTemplates,
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 1, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 1, QuestExpands: 0, ItemExpands: 1),
			});

		Assert.True(bridge.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.Ready, bridge.PacketInputs?.Status);
		Assert.NotNull(bridge.ConcretePacketPlan);
		var concretePlan = bridge.ConcretePacketPlan;
		Assert.True(concretePlan.Succeeded);
		Assert.Equal(["item-100000001", "item-100000002"], concretePlan.Operations[0].Parameters);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			concretePlan.Operations
				.Where(operation => operation.ConcretePacket != null)
				.Select(operation => operation.ConcretePacket!.GetType())
				.ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			concretePlan.Operations
				.Where(operation => operation.ConcretePacket == null)
				.Select(operation => operation.Type)
				.ToArray());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	private static CmItemPurification CreatePacket(
		int playerObjectId,
		int baseItemObjectId,
		int resultItemId,
		IReadOnlyList<int> requiredMaterialObjectIds)
	{
		using var body = new PacketBuffer();
		body.WriteD(playerObjectId);
		body.WriteD(baseItemObjectId);
		body.WriteD(resultItemId);
		foreach (var objectId in requiredMaterialObjectIds)
			body.WriteD(objectId);

		var packet = new CmItemPurification(247, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(body.ToArray(), strictReads: false);
		packet.ReadFrom(reader);
		return packet;
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

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<TestConnectionPair> CreateAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"item-purification-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask));
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
