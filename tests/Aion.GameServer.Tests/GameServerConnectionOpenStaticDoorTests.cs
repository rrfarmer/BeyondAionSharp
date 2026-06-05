using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionOpenStaticDoorTests
{
	private const int WorldId = 210010000;
	private const int InstanceId = 7;
	private const int KeylessDoorId = 33;
	private const int KeyedDoorId = 34;

	[Fact]
	public async Task ProcessPacketAsync_OpenStaticDoorKeylessClosedDoorSetsRuntimeStateAndSendsOpenEmotion()
	{
		var staticPlaceables = new StaticPlaceableStateService();
		staticPlaceables.SetDoorState(WorldId, InstanceId, KeylessDoorId, open: false);
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(
			CreateRuntimeContext(),
			staticPlaceables,
			sentPackets.Add);
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());

		await InvokeProcessPacketAsync(fixture.Connection, CreateOpenStaticDoorPayload(KeylessDoorId));

		Assert.True(staticPlaceables.GetDoorState(WorldId, InstanceId, KeylessDoorId));
		var emotion = Assert.Single(sentPackets);
		AssertOpenDoorEmotion(Assert.IsType<SmEmotion>(emotion), KeylessDoorId);
	}

	[Fact]
	public async Task ProcessPacketAsync_OpenStaticDoorKeyedDoorLeavesDeferredStateUntouched()
	{
		var staticPlaceables = new StaticPlaceableStateService();
		staticPlaceables.SetDoorState(WorldId, InstanceId, KeyedDoorId, open: false);
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(
			CreateRuntimeContext(),
			staticPlaceables,
			sentPackets.Add);
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());

		await InvokeProcessPacketAsync(fixture.Connection, CreateOpenStaticDoorPayload(KeyedDoorId));

		Assert.False(staticPlaceables.GetDoorState(WorldId, InstanceId, KeyedDoorId));
		Assert.Empty(sentPackets);
	}

	private static void AssertOpenDoorEmotion(SmEmotion packet, int expectedDoorId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(expectedDoorId, reader.ReadD());
		Assert.Equal((int)EmotionType.OpenDoor, reader.ReadC());
		Assert.Equal(0x9, reader.ReadH());
		Assert.Equal(0f, reader.ReadF());
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "DoorRunner",
			Race = "ELYOS",
			Position = new WorldPosition(WorldId, 100, 200, 300, 0, InstanceId),
		};
	}

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
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private static byte[] CreateOpenStaticDoorPayload(int doorId)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(23);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteD(doorId);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static GameServerRuntimeContext CreateRuntimeContext()
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(CreateDataManagerForTest(CreateStaticData()));
		return runtimeContext;
	}

	private static DataManager CreateDataManagerForTest(StaticData staticData)
	{
		var constructor = typeof(DataManager).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[typeof(StaticData)],
			modifiers: null);
		Assert.NotNull(constructor);
		return (DataManager)constructor!.Invoke([staticData]);
	}

	private static StaticData CreateStaticData()
	{
		var emptySkillTemplates = new SkillTemplateTable(Array.Empty<SkillTemplateSummary>());
		var constructor = typeof(StaticData).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
		return (StaticData)constructor.Invoke(
		[
			string.Empty,
			Array.Empty<string>(),
			new Dictionary<string, int>(),
			Array.Empty<string>(),
			new[] { new WorldMapSummary(WorldId, IsInstance: false, TwinCount: 1) },
			new FlightZoneTable(Array.Empty<FlightZoneSummary>()),
			new CreaturePvpZoneTable(Array.Empty<CreaturePvpZoneSummary>()),
			new PlayerExperienceTable(Array.Empty<long>()),
			new ItemTemplateTable(Array.Empty<ItemTemplateSummary>()),
			new CosmeticItemTable(Array.Empty<CosmeticItemSummary>()),
			new DecomposableItemTable(Array.Empty<DecomposableItemSummary>()),
			new AssemblyItemTable(Array.Empty<AssemblyItemSummary>()),
			new ItemPurificationTable(Array.Empty<ItemPurificationSummary>()),
			new ItemRestrictionCleanupTable(Array.Empty<ItemRestrictionCleanupSummary>()),
			new RideTable(Array.Empty<RideInfoSummary>()),
			new ItemRandomBonusTable(Array.Empty<ItemRandomBonusSummary>()),
			new ItemSetTable(Array.Empty<ItemSetSummary>()),
			new EnchantTable(Array.Empty<EnchantGroupSummary>()),
			new TemperingTable(Array.Empty<TemperingGroupSummary>()),
			new WalkerTemplateTable(Array.Empty<WalkerTemplateSummary>()),
			new WalkerVersionTable(new Dictionary<string, string>()),
			new RiftLocationTable(Array.Empty<RiftLocationSummary>()),
			new VortexLocationTable(Array.Empty<VortexLocationSummary>()),
			new NpcTemplateTable(Array.Empty<NpcTemplateSummary>()),
			new NpcSpawnTable(Array.Empty<NpcSpawnSummary>()),
			new StaticDoorTable(
			[
				new StaticDoorSummary(WorldId, KeylessDoorId, KeyId: 0, X: 110, Y: 210, Z: 310, State: 0),
				new StaticDoorSummary(WorldId, KeyedDoorId, KeyId: 185000044, X: 120, Y: 220, Z: 320, State: 0),
			]),
			new NpcRiftSpawnTable(Array.Empty<NpcRiftSpawnSummary>()),
			new NpcVortexSpawnTable(Array.Empty<NpcVortexSpawnSummary>()),
			new NpcFactionTable(Array.Empty<NpcFactionSummary>()),
			new TradeListTable(Array.Empty<TradeListTemplateSummary>(), Array.Empty<TradeListTemplateSummary>(), Array.Empty<TradeListTemplateSummary>()),
			new GoodsListTable(Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>()),
			new CustomNpcDropTable(Array.Empty<CustomNpcDropSummary>()),
			new QuestDropTable(Array.Empty<QuestDropSummary>()),
			new QuestUpdateItemTable(Array.Empty<int>()),
			new GlobalDropTable(Array.Empty<GlobalDropRuleSummary>()),
			new EventDropTable(Array.Empty<EventTemplateSummary>()),
			GlobalNpcExclusionTable.Empty,
			emptySkillTemplates,
			new NpcSkillTable(Array.Empty<NpcSkillListSummary>()),
			new PetSkillTable(Array.Empty<PetSkillSummary>()),
			new TitleTemplateTable(Array.Empty<TitleTemplateSummary>()),
			new RecipeTemplateTable(Array.Empty<RecipeTemplateSummary>()),
			new WorkOrderRecipeTable(Array.Empty<WorkOrderRecipeSummary>()),
			new HousingTemplateTable(Array.Empty<HousingAddressSummary>(), Array.Empty<HousingBuildingSummary>()),
			new HousingObjectTemplateTable(Array.Empty<HousingObjectTemplateSummary>()),
			new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>()),
			new InstanceExitTable(Array.Empty<InstanceExitSummary>()),
			new PortalPathTable(Array.Empty<PortalPathSummary>(), new Dictionary<int, int>(), Array.Empty<PortalPathSummary>(), Array.Empty<PortalPathSummary>()),
			new PortalLocTable(Array.Empty<PortalLocSummary>()),
			new AutoGroupTable(Array.Empty<AutoGroupSummary>()),
			new PlayerInitialDataTable(new Dictionary<string, PlayerCreationData>(), new Dictionary<string, PlayerSpawnLocation>()),
			new SkillTreeTable(Array.Empty<SkillLearnSummary>(), emptySkillTemplates),
			new StorageExpansionTemplateTable(Array.Empty<StorageExpansionTemplateSummary>()),
			new StorageExpansionTemplateTable(Array.Empty<StorageExpansionTemplateSummary>()),
			new NearbyQuestTemplateTable(Array.Empty<NearbyQuestTemplateSummary>()),
			QuestHandlerAvailabilityTable.Empty,
			new QuestFinishRewardProjectionLookupTable(Array.Empty<(int QuestId, QuestFinishRewardProjectionLookupEntry Entry)>()),
			new QuestBonusItemGroupTable(Array.Empty<QuestBonusItemGroupProjection>()),
			new WindstreamTable(Array.Empty<WindstreamLocationSummary>()),
			null,
		]);
	}

	private sealed class ConnectionFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ConnectionFixture(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<ConnectionFixture> CreateAsync(
			GameServerRuntimeContext runtimeContext,
			IStaticPlaceableStateService staticPlaceableStateService,
			Action<GameServerPacket> sentPacketObserver)
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
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"open-static-door-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					staticPlaceableStateService: staticPlaceableStateService,
					sentPacketObserver: sentPacketObserver,
					crypt: crypt);
				return new ConnectionFixture(client, connection);
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
