using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionTamperingTests
{
	[Fact]
	public async Task ProcessPacketAsync_TamperingSuccessConsumesSourceUpdatesTargetAndSendsStatsChange()
	{
		await using var fixture = await TamperingFixture.CreateAsync();
		var player = CreatePlayer(
		[
			CreateSourceItem(objectId: 6001, count: 1),
			CreateTargetItem(objectId: 5001, itemId: 110100001, tempering: 0),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(fixture.Connection, CreateUseItemPayload(sourceItemObjectId: 6001, targetItemObjectId: 5001));

		var targetItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, targetItem.ObjectId);
		Assert.Equal(1, targetItem.Tempering);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 5000, 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 6001),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), 5001, SmInventoryUpdateItem.StatsChange),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1402148),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 0, 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_TamperingFailureResetsNonPlumeAndConsumesSource()
	{
		await using var fixture = await TamperingFixture.CreateAsync(
			options: new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					TamperingChances = [0f, 0f],
				},
			});
		var player = CreatePlayer(
		[
			CreateSourceItem(objectId: 6001, count: 2),
			CreateTargetItem(objectId: 5001, itemId: 110100001, tempering: 3),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(fixture.Connection, CreateUseItemPayload(sourceItemObjectId: 6001, targetItemObjectId: 5001));

		Assert.Equal(2, player.InventoryItems.Count);
		var sourceItem = player.InventoryItems.Single(item => item.ObjectId == 6001);
		var targetItem = player.InventoryItems.Single(item => item.ObjectId == 5001);
		Assert.Equal(1, sourceItem.Count);
		Assert.Equal(0, targetItem.Tempering);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 5000, 0),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), 6001, SmInventoryUpdateItem.DecreaseItemUse),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), 5001, SmInventoryUpdateItem.StatsChange),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1402149),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 0, 2));
	}

	[Fact]
	public async Task CancelPendingTamperingUse_SendsAuthorizeCancelAndRemovesCooldown()
	{
		await using var fixture = await TamperingFixture.CreateAsync(includeThreadPoolManager: true);
		var player = CreatePlayer(
		[
			CreateSourceItem(objectId: 6001, count: 1),
			CreateTargetItem(objectId: 5001, itemId: 110100001, tempering: 0),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(fixture.Connection, CreateUseItemPayload(sourceItemObjectId: 6001, targetItemObjectId: 5001));
		Assert.NotEmpty(player.ItemCooldowns);

		var cancelMethod = typeof(GameServerConnection).GetMethod("CancelPendingItemUseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(cancelMethod);
		var cancelTask = Assert.IsAssignableFrom<Task>(cancelMethod.Invoke(fixture.Connection, [player]));
		await cancelTask;

		Assert.Empty(player.ItemCooldowns);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 5000, 0),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 0, 3),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1402147));
	}

	[Fact]
	public async Task ProcessPacketAsync_TamperingSuccessOnEquippedAccessorySendsStatsInfoAndRaceAnnouncementAtTen()
	{
		var registry = new CapturingConnectionRegistry(
		[
			new Player { ObjectId = 2001, Name = "SameRacePeer", Race = "ELYOS" },
			new Player { ObjectId = 2002, Name = "OtherRacePeer", Race = "ASMODIANS" },
		]);
		await using var fixture = await TamperingFixture.CreateAsync(
			options: new GameServerOptions
			{
				Custom = new GameServerCustomOptions
				{
					EnableEnchantAnnounce = true,
				},
				Rates = new GameServerRateOptions
				{
					TamperingChances = [100f, 100f],
				},
			},
			connectionRegistry: registry);
		var player = CreatePlayer(
		[
			CreateSourceItem(objectId: 6001, count: 1),
			CreateTargetItem(objectId: 5001, itemId: 120001486, tempering: 9, isEquipped: true, slot: 1),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var baselineMaxHp = ReadCurrentMaxHp(CreateStatsInfoPacket(player, fixture.StaticData));

		await InvokeProcessPacketAsync(fixture.Connection, CreateUseItemPayload(sourceItemObjectId: 6001, targetItemObjectId: 5001));

		var targetItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, targetItem.ObjectId);
		Assert.Equal(10, targetItem.Tempering);
		Assert.Equal(StoragePersistentState.UpdateRequired, player.EquipmentPersistentState);
		Assert.Collection(
			registry.VisibleBroadcasts,
			broadcast => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(broadcast.Packet), 6001, 166030005, 5000, 0),
			broadcast => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(broadcast.Packet), 6001, 166030005, 0, 1));
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 6001),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), 5001, SmInventoryUpdateItem.StatsChange),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1402148),
			packet =>
			{
				var statsInfo = Assert.IsType<SmStatsInfo>(packet);
				Assert.Equal(baselineMaxHp + 100, ReadCurrentMaxHp(statsInfo));
			});
		var worldBroadcast = Assert.Single(registry.WorldBroadcasts);
		AssertSystemMessageId(Assert.IsType<SmSystemMessage>(worldBroadcast.Packet), 1402154);
		Assert.Collection(
			worldBroadcast.RecipientObjectIds,
			recipientObjectId => Assert.Equal(2001, recipientObjectId));
	}

	[Fact]
	public async Task ProcessPacketAsync_TamperingFailureOnEquippedAccessoryResetsTemperingAndSendsStatsInfo()
	{
		await using var fixture = await TamperingFixture.CreateAsync(
			options: new GameServerOptions
			{
				Rates = new GameServerRateOptions
				{
					TamperingChances = [0f, 0f],
				},
			});
		var player = CreatePlayer(
		[
			CreateSourceItem(objectId: 6001, count: 1),
			CreateTargetItem(objectId: 5001, itemId: 120001486, tempering: 5, isEquipped: true, slot: 1),
		]);
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		var baselineMaxHp = ReadCurrentMaxHp(CreateStatsInfoPacket(player, fixture.StaticData));

		await InvokeProcessPacketAsync(fixture.Connection, CreateUseItemPayload(sourceItemObjectId: 6001, targetItemObjectId: 5001));

		var targetItem = Assert.Single(player.InventoryItems);
		Assert.Equal(5001, targetItem.ObjectId);
		Assert.Equal(0, targetItem.Tempering);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 5000, 0),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 6001),
			packet => Assert.IsType<SmCubeUpdate>(packet),
			packet => AssertInventoryUpdatePayload(Assert.IsType<SmInventoryUpdateItem>(packet), 5001, SmInventoryUpdateItem.StatsChange),
			packet => AssertSystemMessageId(Assert.IsType<SmSystemMessage>(packet), 1402149),
			packet => AssertItemUsagePayload(Assert.IsType<SmItemUsageAnimation>(packet), 6001, 166030005, 0, 2),
			packet =>
			{
				var statsInfo = Assert.IsType<SmStatsInfo>(packet);
				Assert.Equal(baselineMaxHp - 500, ReadCurrentMaxHp(statsInfo));
			});
	}

	private static Player CreatePlayer(InventoryItem[] inventoryItems) =>
		new()
		{
			ObjectId = 1001,
			Name = "TemperTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems = inventoryItems,
		};

	private static InventoryItem CreateSourceItem(int objectId, long count) =>
		new()
		{
			ObjectId = objectId,
			ItemId = 166030005,
			Count = count,
			Location = 0,
			OwnerId = 1001,
		};

	private static InventoryItem CreateTargetItem(
		int objectId,
		int itemId,
		int tempering,
		int randomPlumeBonus = 0,
		bool isEquipped = false,
		long slot = 0) =>
		new()
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			OwnerId = 1001,
			IsEquipped = isEquipped,
			Slot = slot,
			Tempering = tempering,
			RandomPlumeBonus = randomPlumeBonus,
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

	private static byte[] CreateUseItemPayload(int sourceItemObjectId, int targetItemObjectId)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(37);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteD(sourceItemObjectId);
		buffer.WriteC(2);
		buffer.WriteD(targetItemObjectId);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static void AssertItemUsagePayload(SmItemUsageAnimation packet, int expectedItemObjectId, int expectedItemId, int expectedTime, int expectedEnd)
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
		Assert.Equal(expectedUpdateType, payload[^2] | (payload[^1] << 8));
	}

	private static void AssertDeleteItemPayload(SmDeleteItem packet, int expectedObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
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

	private static SmStatsInfo CreateStatsInfoPacket(Player player, StaticData staticData) =>
		new(
			player,
			staticData.PlayerExperienceTable,
			gameMinutes: 0,
			staticData.ItemTemplates,
			staticData.ItemRandomBonuses,
			staticData.ItemSets,
			staticData.EnchantTemplates,
			staticData.TemperingTemplates,
			staticData.SkillTemplates,
			staticData.TitleTemplates);

	private static int ReadCurrentMaxHp(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadD();
		reader.ReadD();
		reader.ReadB(12 + 12 + 8 + 24 + 4);
		return reader.ReadD();
	}

	private sealed class TamperingFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly ThreadPoolManager? _threadPoolManager;
		private readonly string _tempRoot;

		private TamperingFixture(
			TcpClient client,
			GameServerConnection connection,
			List<GameServerPacket> sentPackets,
			ThreadPoolManager? threadPoolManager,
			string tempRoot,
			StaticData staticData)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
			_threadPoolManager = threadPoolManager;
			_tempRoot = tempRoot;
			StaticData = staticData;
		}

		public GameServerConnection Connection { get; }

		public List<GameServerPacket> SentPackets { get; }

		public StaticData StaticData { get; }

		public static async Task<TamperingFixture> CreateAsync(
			GameServerOptions? options = null,
			bool includeThreadPoolManager = false,
			IGameClientConnectionRegistry? connectionRegistry = null)
		{
			var tempRoot = Path.Combine(Path.GetTempPath(), "aion-tampering-" + Guid.NewGuid().ToString("N"));
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
					<tempering_templates>
						<tempering_list item_group="TEST_1">
							<tempering_data level="5">
								<tempering_stat stat="MAXHP" value="500"/>
							</tempering_data>
							<tempering_data level="9">
								<tempering_stat stat="MAXHP" value="900"/>
							</tempering_data>
							<tempering_data level="10">
								<tempering_stat stat="MAXHP" value="1000"/>
							</tempering_data>
						</tempering_list>
					</tempering_templates>
					<item_templates>
						<item_template id="110100001" name="Test Tamper Sword" level="65" item_group="SWORD" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1" max_tampering="5"/>
						<item_template id="120001486" name="Authorize Test Earring" level="65" item_group="EARRING" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1" tempering_name="TEST_1" max_tampering="10"/>
						<item_template id="166100001" name="Test Plume" level="65" item_group="PLUME" item_type="NORMAL" quality="MYTHIC" race="PC_ALL" max_stack_count="1" tempering_name="TSHIRT_PHYSICAL" max_tampering="10"/>
						<item_template id="166030005" name="Tempering Solution" level="65" item_group="TAMPERING" item_type="NORMAL" quality="MYTHIC" race="PC_ALL" max_stack_count="100">
							<uselimits usedelayid="4101" usedelay="30000"/>
							<actions>
								<tampering/>
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
					"cm-tampering-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: options ?? new GameServerOptions(),
					runtimeContext: runtimeContext,
					connectionRegistry: connectionRegistry,
					threadPoolManager: threadPoolManager,
					idFactory: new IDFactory([9001]),
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TamperingFixture(client, connection, sentPackets, threadPoolManager, tempRoot, dataManager.StaticData);
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

	private sealed class CapturingConnectionRegistry(IReadOnlyList<Player> onlinePlayers) : IGameClientConnectionRegistry
	{
		public List<WorldBroadcastRecord> WorldBroadcasts { get; } = [];

		public List<VisibleBroadcastRecord> VisibleBroadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = onlinePlayers.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.Ordinal));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in onlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			return Task.FromResult(false);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var recipients = onlinePlayers
				.Where(player => filter == null || filter(player))
				.Select(player => player.ObjectId)
				.ToArray();
			WorldBroadcasts.Add(new WorldBroadcastRecord(packet, recipients));
			return Task.FromResult(recipients.Length);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			VisibleBroadcasts.Add(new VisibleBroadcastRecord(packet, includeSourcePlayer));
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

	private sealed record WorldBroadcastRecord(GameServerPacket Packet, IReadOnlyList<int> RecipientObjectIds);

	private sealed record VisibleBroadcastRecord(GameServerPacket Packet, bool IncludeSourcePlayer);
}
