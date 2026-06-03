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
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionAutoGroupTests
{
	[Fact]
	public async Task ProcessPacketAsync_AutoGroupDisabledSendsJavaTextMessageAndStopsWindowDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions { AutoGroup = new GameServerAutoGroupOptions { Enabled = false } },
			sentPackets.Add);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1001,
				Name = "DisabledTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(104);
					buffer.WriteC(0);
				}));

		var message = Assert.IsType<SmMessage>(Assert.Single(sentPackets));
		Assert.Equal("Auto Group is disabled", ReadMessage(message));
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupWindow105IsJavaNoOp()
	{
		var sentPackets = new List<GameServerPacket>();
		await using var fixture = await ConnectionFixture.CreateAsync(new GameServerOptions(), sentPackets.Add);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1002,
				Name = "NoOpTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(105);
					buffer.WriteC(0);
				}));

		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupDuplicateStartLookingSendsJavaAlreadyRegisteredMessage()
	{
		var sentPackets = new List<GameServerPacket>();
		var autoGroupRegistrations = new AutoGroupLookingPartyRegistrationService();
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(107, 300110000));
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			autoGroupRegistrations);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1003,
				Name = "DuplicateTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(100);
					buffer.WriteC(2);
				}));
		sentPackets.Clear();
		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(100);
					buffer.WriteC(2);
				}));

		var message = Assert.IsType<SmSystemMessage>(Assert.Single(sentPackets));
		Assert.Equal(1400181, message.MessageId);
		Assert.Equal(["300110000"], message.Parameters);
		Assert.Equal(1, autoGroupRegistrations.GetLookingPartyCount(107));
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupSuccessfulRegistrationSendsJavaFanoutPackets()
	{
		var sentPackets = new List<GameServerPacket>();
		var autoGroupRegistrations = new AutoGroupLookingPartyRegistrationService();
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(107, 300110000));
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			autoGroupRegistrations);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(
			fixture.Connection,
			new Player
			{
				ObjectId = 1004,
				Name = "LeaderTester",
				Race = "ELYOS",
				Level = 50,
			});

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(100);
					buffer.WriteC(2);
				}));

		Assert.Collection(
			sentPackets,
			packet =>
			{
				var autoGroup = Assert.IsType<SmAutoGroup>(packet);
				Assert.Equal(107, autoGroup.MaskId);
				Assert.Equal(SmAutoGroup.EntryIconWindowId, autoGroup.WindowId);
				Assert.True(autoGroup.IsClosed);
			},
			packet =>
			{
				var message = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1400194, message.MessageId);
			},
			packet =>
			{
				var autoGroup = Assert.IsType<SmAutoGroup>(packet);
				Assert.Equal(107, autoGroup.MaskId);
				Assert.Equal(1, autoGroup.WindowId);
			});
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var field = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		field.SetValue(connection, player);
	}

	private static void SetConnectionState(GameServerConnection connection, GameConnectionState state)
	{
		var field = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		field.SetValue(connection, state);
	}

	private static string ReadMessage(SmMessage packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		return reader.ReadS();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static AutoGroupSummary CreateAutoGroup(int maskId, int worldId)
	{
		return new AutoGroupSummary(
			maskId,
			worldId,
			NameId: 140000 + maskId,
			TitleId: 150000 + maskId,
			MinLevel: 46,
			MaxLevel: 65,
			RegisterQuick: true,
			RegisterGroup: true,
			RegisterNew: true,
			NpcIds: []);
	}

	private static GameServerRuntimeContext CreateAutoGroupRuntimeContext(params AutoGroupSummary[] autoGroups)
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(CreateDataManagerForTest(CreateStaticDataForAutoGroups(autoGroups)));
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

	private static StaticData CreateStaticDataForAutoGroups(IReadOnlyList<AutoGroupSummary> autoGroups)
	{
		var emptySkillTemplates = new SkillTemplateTable(Array.Empty<SkillTemplateSummary>());
		var constructor = typeof(StaticData).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
		return (StaticData)constructor.Invoke(
		[
			string.Empty,
			Array.Empty<string>(),
			new Dictionary<string, int>(),
			Array.Empty<string>(),
			Array.Empty<WorldMapSummary>(),
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
			new StaticDoorTable(Array.Empty<StaticDoorSummary>()),
			new NpcRiftSpawnTable(Array.Empty<NpcRiftSpawnSummary>()),
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
			new HousingTemplateTable(Array.Empty<HousingAddressSummary>(), Array.Empty<HousingBuildingSummary>()),
			new HousingObjectTemplateTable(Array.Empty<HousingObjectTemplateSummary>()),
			new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>()),
			new InstanceExitTable(Array.Empty<InstanceExitSummary>()),
			new PortalPathTable(Array.Empty<PortalPathSummary>(), new Dictionary<int, int>(), Array.Empty<PortalPathSummary>(), Array.Empty<PortalPathSummary>()),
			new PortalLocTable(Array.Empty<PortalLocSummary>()),
			new AutoGroupTable(autoGroups),
			new PlayerInitialDataTable(new Dictionary<string, PlayerCreationData>(), new Dictionary<string, PlayerSpawnLocation>()),
			new SkillTreeTable(Array.Empty<SkillLearnSummary>(), emptySkillTemplates),
			new StorageExpansionTemplateTable(Array.Empty<StorageExpansionTemplateSummary>()),
			new StorageExpansionTemplateTable(Array.Empty<StorageExpansionTemplateSummary>()),
			new NearbyQuestTemplateTable(Array.Empty<NearbyQuestTemplateSummary>()),
			new QuestFinishRewardProjectionLookupTable(Array.Empty<(int QuestId, QuestFinishRewardProjectionLookupEntry Entry)>()),
			new QuestBonusItemGroupTable(Array.Empty<QuestBonusItemGroupProjection>()),
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
			GameServerOptions options,
			Action<GameServerPacket> sentPacketObserver,
			GameServerRuntimeContext? runtimeContext = null,
			AutoGroupLookingPartyRegistrationService? autoGroupLookingPartyRegistrations = null)
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var client = new TcpClient();
			var acceptTask = listener.AcceptTcpClientAsync();
			await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
			var serverClient = await acceptTask;
			var processor = new GamePacketProcessor<string>((_, _) => Task.CompletedTask);
			var crypt = new GameCrypt(() => 0x01020304);
			crypt.EnableKey();

			try
			{
				var connection = new GameServerConnection(
					NullLogger<GameServerConnectionAutoGroupTests>.Instance,
					serverClient,
					"autogroup-test",
					processor,
					options,
					runtimeContext: runtimeContext,
					sentPacketObserver: sentPacketObserver,
					autoGroupLookingPartyRegistrations: autoGroupLookingPartyRegistrations,
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
			_client.Dispose();
			await Connection.DisposeAsync();
		}
	}
}
