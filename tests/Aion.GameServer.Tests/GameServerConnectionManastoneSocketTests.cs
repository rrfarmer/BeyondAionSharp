using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionManastoneSocketTests
{
	[Fact]
	public async Task CompleteSocketManastoneAsync_WritesCleanupSealFlagForRestrictedFullUpdates()
	{
		await using var fixture = await ManastoneSocketFixture.CreateAsync();
		var sourceUpdate = CreateItem(objectId: 5001, itemId: SourceItemId, count: 1);
		var targetUpdate = CreateItem(objectId: 6001, itemId: TargetItemId, count: 1);
		var player = new Player
		{
			ObjectId = 1001,
			InventoryItems =
			[
				CreateItem(objectId: 5001, itemId: SourceItemId, count: 2),
				CreateItem(objectId: 6001, itemId: TargetItemId, count: 1),
			],
		};
		var plan = new ManastoneSocketPlan(
			ManastoneSocketFailure.None,
			ItemName: "Restricted Socket Target",
			InventoryItems: [sourceUpdate, targetUpdate],
			TargetItemUpdate: targetUpdate,
			SourceItemUpdate: sourceUpdate,
			DeletedSourceItemObjectId: null,
			SupplementItemUpdates: [],
			DeletedSupplementItemObjectIds: [],
			AddedStone: new ItemStoneSocket(SourceItemId, 0),
			AddedCategory: 0,
			SocketSucceeded: true,
			RefreshStats: false);

		await InvokeCompleteSocketManastoneAsync(
			fixture.Connection,
			player,
			new CmManastone(74, new HashSet<GameConnectionState> { GameConnectionState.InGame }),
			plan,
			CreateTemplate(SourceItemId, "Restricted Manastone"),
			CreateTemplate(TargetItemId, "Restricted Socket Target"),
			fixture.StaticData);

		Assert.Same(plan.InventoryItems, player.InventoryItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 5001,
				expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmSystemMessage>(packet),
			packet => AssertInventoryUpdatePayloadWithCleanupSealFlag(
				Assert.IsType<SmInventoryUpdateItem>(packet),
				expectedObjectId: 6001,
				expectedUpdateType: 0,
				expectedCleanupSealFlag: 3),
			packet => Assert.IsType<SmItemUsageAnimation>(packet));
	}

	private static async Task InvokeCompleteSocketManastoneAsync(
		GameServerConnection connection,
		Player player,
		CmManastone packet,
		ManastoneSocketPlan plan,
		ItemTemplateSummary sourceTemplate,
		ItemTemplateSummary targetTemplate,
		StaticData staticData)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"CompleteSocketManastoneAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(
			connection,
			[player, packet, plan, sourceTemplate, targetTemplate, staticData, CancellationToken.None]));
		await task;
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

	private const int SourceItemId = 167000001;
	private const int TargetItemId = 100000001;

	private sealed class ManastoneSocketFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly string _tempRoot;

		private ManastoneSocketFixture(TcpClient client, GameServerConnection connection, StaticData staticData, List<GameServerPacket> sentPackets, string tempRoot)
		{
			_client = client;
			Connection = connection;
			StaticData = staticData;
			SentPackets = sentPackets;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection { get; }

		public StaticData StaticData { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<ManastoneSocketFixture> CreateAsync()
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-manastone-socket-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path.Combine(tempRoot, "game-server", "data", "static_data"));
			await File.WriteAllTextAsync(
				Path.Combine(tempRoot, "game-server", "data", "static_data", "static_data.xml"),
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<static_data>
					<item_restriction_cleanups>
						<cleanup id="167000001" awh="0" lwh="1"/>
						<cleanup id="100000001" awh="1" lwh="0"/>
					</item_restriction_cleanups>
				</static_data>
				""");
			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
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
					"manastone-socket-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new ManastoneSocketFixture(client, connection, dataManager.StaticData, sentPackets, tempRoot);
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
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
