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

public sealed class GameServerConnectionManastoneRemovalTests
{
	[Fact]
	public async Task CompleteRemoveManastoneAsync_WritesCleanupSealFlagForRestrictedTargetFullUpdate()
	{
		await using var fixture = await ManastoneRemovalFixture.CreateAsync();
		var kinahUpdate = CreateItem(objectId: 5001, itemId: KinahItemId, count: 9350);
		var itemUpdate = CreateItem(objectId: 6001, itemId: TargetItemId, count: 1);
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 5001, itemId: KinahItemId, count: 10000),
				CreateItem(objectId: 6001, itemId: TargetItemId, count: 1),
			],
		};
		var plan = new ManastoneRemovalPlan(
			ManastoneRemovalFailure.None,
			ItemName: "Restricted Removal Target",
			InventoryItems: [kinahUpdate, itemUpdate],
			ItemUpdate: itemUpdate,
			KinahItemUpdate: kinahUpdate,
			RemovedSlot: 0,
			RemovedCategory: 0,
			RemovalPrice: 650);
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(KinahItemId, "Kinah"),
			CreateTemplate(TargetItemId, "Restricted Removal Target"),
		]);
		var cleanups = new ItemRestrictionCleanupTable(
		[
			new ItemRestrictionCleanupSummary(TargetItemId, AccountWarehouse: 0, LegionWarehouse: 1),
		]);

		await InvokeCompleteRemoveManastoneAsync(fixture.Connection, player, plan, itemTemplates, cleanups);

		Assert.Same(plan.InventoryItems, player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayload(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseKinahBuy),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3));
	}

	private static async Task InvokeCompleteRemoveManastoneAsync(
		GameServerConnection connection,
		Player player,
		ManastoneRemovalPlan plan,
		ItemTemplateTable itemTemplates,
		ItemRestrictionCleanupTable cleanups)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"CompleteRemoveManastoneAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[player, plan, itemTemplates, cleanups]));
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
		int expectedCleanupSealFlag)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		var blob = reader.ReadB(blobSize);
		AssertGeneralInfoCleanupSealFlag(blob, expectedItemMask: 1, expectedFlag: expectedCleanupSealFlag);
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

	private static ItemTemplateSummary CreateTemplate(int itemId, string name)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
			0,
			1,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			100,
			0,
			0);
	}

	private const int KinahItemId = 182400001;
	private const int TargetItemId = 100000001;

	private sealed class ManastoneRemovalFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ManastoneRemovalFixture(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<ManastoneRemovalFixture> CreateAsync()
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
					"manastone-removal-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new ManastoneRemovalFixture(client, connection, sentPackets);
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
