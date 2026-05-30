using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionTuneTests
{
	[Fact]
	public async Task ProcessPacketAsync_CmTune_IdentifyBranchBroadcastsAndUpdatesIdentifiedItem()
	{
		await using var fixture = await TuneFixture.CreateAsync();
		var player = CreatePlayer([CreateTargetItem(objectId: 5001, itemId: 110100001, tuneCount: -1)]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(235, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteD(0);
			}));

		var targetItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, targetItem.ObjectId);
		Assert.True(targetItem.IsIdentified);
		Assert.Equal(0, targetItem.TuneCount);
		Assert.Null(targetItem.PendingTuneResult);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemObjectId: 5001, expectedItemId: 110100001, expectedTime: 5000, expectedEnd: 9),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemObjectId: 5001, expectedItemId: 110100001, expectedTime: 0, expectedEnd: 10),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), expectedObjectId: 5001, expectedUpdateType: SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1401626));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmTune_GuardDeniedSendsJavaSystemMessage()
	{
		await using var fixture = await TuneFixture.CreateAsync();
		var player = CreatePlayer(
		[
			CreateTargetItem(objectId: 5001, itemId: 111100001, tuneCount: 0),
			CreateScrollItem(objectId: 6001, itemId: 166200001),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(235, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteD(6001);
			}));

		Assert.Equal(2, player.InventoryItems.Count);
		var denial = Assert.Single(fixture.SentPackets);
		AssertSystemMessageId(Assert.IsType<SmSystemMessage>(denial), 1401633);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmTune_ExecutableActionConsumesScrollAndSendsTunePreview()
	{
		await using var fixture = await TuneFixture.CreateAsync();
		var player = CreatePlayer(
		[
			CreateTargetItem(objectId: 5001, itemId: 110100001, tuneCount: 0),
			CreateScrollItem(objectId: 6001, itemId: 166200001),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(235, buffer =>
			{
				buffer.WriteD(5001);
				buffer.WriteD(6001);
			}));

		var targetItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, targetItem.ObjectId);
		Assert.Equal(1, targetItem.TuneCount);
		Assert.NotNull(targetItem.PendingTuneResult);
		Assert.False(targetItem.PendingTuneResult!.IsAttributeOnly);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemObjectId: 6001, expectedItemId: 166200001, expectedTime: 5000, expectedEnd: 12),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), expectedItemObjectId: 6001, expectedItemId: 166200001, expectedTime: 0, expectedEnd: 13),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), expectedObjectId: 6001, expectedDeleteType: SmDeleteItem.UseDeleteType),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => Assert.IsType<SmTuneResult>(packet),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1401639));
	}

	private static Player CreatePlayer(InventoryItem[] inventoryItems) =>
		new()
		{
			ObjectId = 1001,
			Name = "TuneTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems = inventoryItems,
		};

	private static InventoryItem CreateTargetItem(int objectId, int itemId, int tuneCount) =>
		new()
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			OwnerId = 1001,
			TuneCount = tuneCount,
		};

	private static InventoryItem CreateScrollItem(int objectId, int itemId) =>
		new()
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			OwnerId = 1001,
		};

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
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

	private static void AssertItemUsagePayload(
		SmItemUsageAnimation packet,
		int expectedItemObjectId,
		int expectedItemId,
		int expectedTime,
		int expectedEnd)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(expectedItemObjectId, reader.ReadD());
		Assert.Equal(expectedItemId, reader.ReadD());
		Assert.Equal(expectedTime, reader.ReadD());
		Assert.Equal(expectedEnd, (int)reader.ReadC());
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
	}

	private static void AssertSystemMessageId(SmSystemMessage packet, int expectedMessageId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class TuneFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly ThreadPoolManager? _threadPoolManager;
		private readonly string _tempRoot;

		private TuneFixture(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets, ThreadPoolManager? threadPoolManager, string tempRoot)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
			_threadPoolManager = threadPoolManager;
			_tempRoot = tempRoot;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TuneFixture> CreateAsync(bool includeThreadPoolManager = false)
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-tune-" + Guid.NewGuid().ToString("N"));
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
						<item_template id="110100001" name="Test Tunable Sword" level="50" item_group="SWORD" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1" max_enchant_bonus="0" option_slot_bonus="0" rnd_bonus="0" rnd_count="6"/>
						<item_template id="111100001" name="Test Tunable Armor" level="50" item_group="CL_TORSO" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1" max_enchant_bonus="0" option_slot_bonus="0" rnd_bonus="0" rnd_count="6"/>
						<item_template id="166200001" name="Test Weapon Scroll" level="55" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
							<actions>
								<tuning target="WEAPON" no_reduce="false"/>
							</actions>
						</item_template>
					</item_templates>
				</static_data>
				""");

			var dataManager = await DataManager.LoadAsync(
				tempRoot,
				cacheDirectory: Path.Combine(tempRoot, "cache"),
				validateWhenCacheChanges: false);
			var runtimeContext = new GameServerRuntimeContext();
			runtimeContext.SetDataManager(dataManager);
			var sentPackets = new List<GameServerPacket>();
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
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"cm-tune-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					threadPoolManager: threadPoolManager,
					idFactory: new IDFactory([9001]),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TuneFixture(client, connection, sentPackets, threadPoolManager, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			if (_threadPoolManager != null)
				await _threadPoolManager.DisposeAsync();
			_client.Dispose();
			if (Directory.Exists(_tempRoot))
				Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
