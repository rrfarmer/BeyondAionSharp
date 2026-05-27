using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionItemRemodelTests
{
	[Fact]
	public async Task CompleteItemRemodelAsync_WritesCleanupSealFlagForRestrictedExtractAndTargetFullUpdates()
	{
		await using var fixture = await ItemRemodelFixture.CreateAsync();
		var kinahUpdate = CreateItem(objectId: 4001, itemId: KinahItemId, count: 9000);
		var extractUpdate = CreateItem(objectId: 5001, itemId: ExtractItemId, count: 1);
		var targetUpdate = new InventoryItem
		{
			ObjectId = 6001,
			ItemId = TargetItemId,
			Count = 1,
			Location = 0,
			Slot = 65535,
			ItemSkin = ExtractItemId,
			Color = 0x334455,
		};
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 4001, itemId: KinahItemId, count: 10000),
				CreateItem(objectId: 5001, itemId: ExtractItemId, count: 2),
				CreateItem(objectId: 6001, itemId: TargetItemId, count: 1),
			],
		};
		var inventoryItems = player.InventoryItems.ToList();
		var plan = ItemRemodelPlan.Success(
			targetUpdate,
			kinahUpdate,
			extractUpdate,
			deletedExtractItemObjectId: null,
			CreateTemplate(TargetItemId, "Restricted Remodel Target", RemodelableMask),
			remodelPrice: 1000);
		var cleanups = new ItemRestrictionCleanupTable(
		[
			new ItemRestrictionCleanupSummary(ExtractItemId, AccountWarehouse: 0, LegionWarehouse: 1),
			new ItemRestrictionCleanupSummary(TargetItemId, AccountWarehouse: 1, LegionWarehouse: 0),
		]);

		await InvokeCompleteItemRemodelAsync(
			fixture.Connection,
			player,
			inventoryItems,
			plan,
			CreateTemplate(TargetItemId, "Restricted Remodel Target", RemodelableMask),
			CreateTemplate(ExtractItemId, "Restricted Remodel Extract", RemodelableMask),
			CreateTemplate(KinahItemId, "Kinah", 1),
			cleanups);

		Assert.Equal([4001, 5001, 6001], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(1, player.InventoryItems.Single(item => item.ObjectId == 5001).Count);
		Assert.Equal(ExtractItemId, player.InventoryItems.Single(item => item.ObjectId == 6001).ItemSkin);
		Assert.Equal(0x334455, player.InventoryItems.Single(item => item.ObjectId == 6001).Color);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 4001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseKinahBuy),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedItemMask: RemodelableMask,
				expectedCleanupSealFlag: 3),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedItemMask: RemodelableMask,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet));
	}

	private static async Task InvokeCompleteItemRemodelAsync(
		GameServerConnection connection,
		Player player,
		List<InventoryItem> inventoryItems,
		ItemRemodelPlan plan,
		ItemTemplateSummary keepTemplate,
		ItemTemplateSummary extractTemplate,
		ItemTemplateSummary kinahTemplate,
		ItemRestrictionCleanupTable cleanups)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"CompleteItemRemodelAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[player, inventoryItems, plan, keepTemplate, extractTemplate, kinahTemplate, cleanups]));
		await task;
	}

	private static void AssertInventoryUpdatePayload(
		SmInventoryUpdateItem packet,
		int expectedObjectId,
		int expectedUpdateType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		reader.ReadB(blobSize);
		Assert.Equal(expectedUpdateType, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertInventoryUpdatePayloadWithCleanupSealFlag(
		SmInventoryUpdateItem packet,
		int expectedObjectId,
		int expectedUpdateType,
		int expectedItemMask,
		int expectedCleanupSealFlag)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		var blob = reader.ReadB(blobSize);
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask, expectedCleanupSealFlag);
		Assert.Equal(expectedUpdateType, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertGeneralInfoCleanupSealFlag(byte[] blob, int expectedItemMask, int expectedFlag)
	{
		using var reader = new PacketBuffer(blob);
		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(expectedItemMask, reader.ReadH());
		Assert.Equal(1, reader.ReadQ());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		reader.ReadD();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(expectedFlag, reader.ReadH());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static InventoryItem CreateItem(int objectId, int itemId, long count)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			Slot = 65535,
		};
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, string name, int mask)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
			0,
			mask,
			1,
			"SWORD",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			100,
			0,
			0);
	}

	private const int KinahItemId = 182400001;
	private const int ExtractItemId = 100000002;
	private const int TargetItemId = 100000001;
	private const int RemodelableMask = 1 << 12;

	private sealed class ItemRemodelFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ItemRemodelFixture(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<ItemRemodelFixture> CreateAsync()
		{
			var sentPackets = new List<GameServerPacket>();
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
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"item-remodel-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new ItemRemodelFixture(client, connection, sentPackets);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			_client.Dispose();
			await Connection.CloseAsync();
		}
	}
}
