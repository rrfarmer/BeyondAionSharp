using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

	private static Player CreatePlayer(int itemId)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "TicketUser",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = itemId,
					Count = 2,
					Location = 0,
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

	private static async Task InvokeHandleEmotionAsync(GameServerConnection connection, Player player, CmEmotion packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleEmotionAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static void AssertItemUsagePayload(SmItemUsageAnimation packet, int expectedItemId = 188500000, int expectedTime = 0, int expectedEnd = 0)
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
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static async Task WaitUntilAsync(Func<bool> predicate)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (predicate())
				return;

			await Task.Delay(25);
		}

		Assert.True(predicate());
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
			bool includeThreadPoolManager = false)
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
						<item_template id="200" name="Test Decompose Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
					</item_templates>
					<decomposable_items>
						<decomposable item_id="100">
							<items chance="100" minlevel="1" maxlevel="1">
								<item id="200" min_count="1" max_count="1"/>
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
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new InventoryExpansionUseItemFixture(client, connection, threadPoolManager, sentPackets, tempRoot);
			}
			finally
			{
				listener.Stop();
			}
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
