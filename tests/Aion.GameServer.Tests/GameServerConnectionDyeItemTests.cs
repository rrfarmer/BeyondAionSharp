using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionDyeItemTests
{
	[Fact]
	public async Task HandleDyeUseItemAsync_WritesCleanupSealFlagForRestrictedSourceAndTargetFullUpdates()
	{
		await using var fixture = await DyeItemFixture.CreateAsync();
		var sourceItem = CreateItem(objectId: 5001, itemId: SourceItemId, count: 2);
		var targetItem = CreateItem(objectId: 6001, itemId: TargetItemId, count: 1);
		var inventoryItems = new List<InventoryItem> { sourceItem, targetItem };
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems = inventoryItems.ToArray(),
		};
		var sourceTemplate = CreateTemplate(
			SourceItemId,
			"Restricted Dye Source",
			mask: 1,
			dyeAction: new ItemDyeActionInfo(0xc22626, 0, false));
		var targetTemplate = CreateTemplate(TargetItemId, "Restricted Dye Target", mask: DyeableMask);
		var itemTemplates = new ItemTemplateTable([sourceTemplate, targetTemplate]);
		var cleanups = new ItemRestrictionCleanupTable(
		[
			new ItemRestrictionCleanupSummary(SourceItemId, AccountWarehouse: 0, LegionWarehouse: 1),
			new ItemRestrictionCleanupSummary(TargetItemId, AccountWarehouse: 1, LegionWarehouse: 0),
		]);

		await InvokeHandleDyeUseItemAsync(
			fixture.Connection,
			player,
			inventoryItems,
			sourceItem,
			sourceTemplate,
			targetItem.ObjectId,
			itemTemplates,
			cleanups);

		Assert.Equal([5001, 6001], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(1, player.InventoryItems.Single(item => item.ObjectId == 5001).Count);
		Assert.Equal(0xc22626, player.InventoryItems.Single(item => item.ObjectId == 6001).Color);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedItemMask: 1,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedItemMask: DyeableMask,
				expectedCleanupSealFlag: 3));
	}

	private static async Task InvokeHandleDyeUseItemAsync(
		GameServerConnection connection,
		Player player,
		List<InventoryItem> inventoryItems,
		InventoryItem sourceItem,
		ItemTemplateSummary sourceTemplate,
		int targetItemObjectId,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable cleanups)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleDyeUseItemAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[player, inventoryItems, sourceItem, sourceTemplate, targetItemObjectId, itemTemplates, cleanups]));
		await task;
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

	private static ItemTemplateSummary CreateTemplate(
		int itemId,
		string name,
		int mask,
		ItemDyeActionInfo? dyeAction = null)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
			0,
			mask,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			100,
			0,
			0,
			DyeAction: dyeAction);
	}

	private const int SourceItemId = 169120000;
	private const int TargetItemId = 100000001;
	private const int DyeableMask = 1 << 15;

	private sealed class DyeItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private DyeItemFixture(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<DyeItemFixture> CreateAsync()
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
					"dye-item-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new DyeItemFixture(client, connection, sentPackets);
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
