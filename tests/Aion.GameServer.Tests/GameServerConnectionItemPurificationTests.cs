using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
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

		var plan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(plan);
		Assert.True(plan.Succeeded);
		Assert.Equal(ItemPurificationApStatus.Allowed, plan.Validation?.Status);
		Assert.Equal([20, 10], plan.MaterialMutation?.DeletedObjectIds);
		Assert.Equal(1_200, plan.MaterialMutation?.AbyssPointsToSpend);
		Assert.Equal(0, plan.Inheritance?.TargetItem?.ObjectId);
		Assert.Equal(100000002, plan.Inheritance?.TargetItem?.ItemId);
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

		var plan = await pair.Connection.HandleItemPurificationAsync(
			player,
			packet,
			CreatePurificationTable(),
			CreateItemTemplates());

		Assert.NotNull(plan);
		Assert.Equal(ItemPurificationWorkflowStatus.MissingBaseItem, plan.Status);
		Assert.Null(plan.Validation);
		Assert.Null(plan.MaterialMutation);
		Assert.Null(plan.Inheritance);
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
