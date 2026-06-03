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
					buffer.WriteC(0);
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
					buffer.WriteC(1);
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
					buffer.WriteC(0);
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

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupSuccessfulRegistrationFansOutOnlyToOnlineMembersLikeJava()
	{
		var sentPackets = new List<GameServerPacket>();
		var registry = new RecordingConnectionRegistry([1005, 1006]);
		var autoGroupRegistrations = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(107, 300110000));
		var leader = new Player
		{
			ObjectId = 1005,
			Name = "GroupLeader",
			Race = "ELYOS",
			Level = 50,
		};
		var onlineMember = new Player
		{
			ObjectId = 1006,
			Name = "OnlineMember",
			Race = "ELYOS",
			Level = 50,
		};
		var offlineMember = new Player
		{
			ObjectId = 1007,
			Name = "OfflineMember",
			Race = "ELYOS",
			Level = 50,
		};
		groupRuntime.CreateOrUpdateGroup(77, [leader, onlineMember, offlineMember]);

		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			autoGroupRegistrations,
			registry,
			groupRuntime);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(fixture.Connection, leader);

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

		Assert.Empty(sentPackets);
		Assert.Equal([1005, 1005, 1005, 1006, 1006, 1006], registry.SentPackets.Select(delivery => delivery.PlayerObjectId));
		Assert.DoesNotContain(registry.SentPackets, delivery => delivery.PlayerObjectId == 1007);
		AssertFanoutPacketOrder(registry.SentPackets.Take(3).Select(delivery => delivery.Packet).ToArray());
		AssertFanoutPacketOrder(registry.SentPackets.Skip(3).Take(3).Select(delivery => delivery.Packet).ToArray());
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupGroupEntryBroadcastsBattlegroundAnnouncementAfterSuccessLikeJava()
	{
		var sentPackets = new List<GameServerPacket>();
		var leader = new Player
		{
			ObjectId = 1020,
			Name = "BattleLeader",
			Race = "ELYOS",
			Level = 50,
		};
		var member = new Player
		{
			ObjectId = 1021,
			Name = "BattleMember",
			Race = "ELYOS",
			Level = 50,
		};
		var opposingEligible = new Player
		{
			ObjectId = 2020,
			Name = "OpposingEligible",
			Race = "ASMODIANS",
			Level = 50,
		};
		var sameRaceEligible = new Player
		{
			ObjectId = 2021,
			Name = "SameRaceEligible",
			Race = "ELYOS",
			Level = 50,
		};
		var opposingLowLevel = new Player
		{
			ObjectId = 2022,
			Name = "OpposingLowLevel",
			Race = "ASMODIANS",
			Level = 30,
		};
		var registry = new RecordingConnectionRegistry([leader, member, opposingEligible, sameRaceEligible, opposingLowLevel]);
		var autoGroupRegistrations = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		groupRuntime.CreateOrUpdateGroup(77, [leader, member]);
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(107, 300110000));
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions { AutoGroup = new GameServerAutoGroupOptions { AnnounceBattlegroundRegistrations = true } },
			sentPackets.Add,
			runtimeContext,
			autoGroupRegistrations,
			registry,
			groupRuntime);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(fixture.Connection, leader);

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

		Assert.Empty(sentPackets);
		Assert.Equal([1020, 1020, 1020, 1021, 1021, 1021], registry.SentPackets.Select(delivery => delivery.PlayerObjectId));
		var broadcast = Assert.Single(registry.BroadcastPackets);
		Assert.Equal([opposingEligible.ObjectId], broadcast.RecipientObjectIds);
		Assert.Equal(
			$"{ChatUtil.L10n(900240)} have registered for {ChatUtil.L10n(140107)}.",
			ReadMessage(Assert.IsType<SmMessage>(broadcast.Packet), expectedChatType: 36));
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupReadyMatchSendsWindowFourAndRemovesQueuesLikeJava()
	{
		var sentPackets = new List<GameServerPacket>();
		var autoGroupRegistrations = new AutoGroupLookingPartyRegistrationService();
		var baseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
		autoGroupRegistrations.RegisterLookingParty(
			107,
			[1001, 1002],
			"ELYOS",
			AutoGroupEntryRequestType.GroupEntry,
			baseTime);
		var asmoLeader = new Player
		{
			ObjectId = 2001,
			Name = "AsmoLeader",
			Race = "ASMODIANS",
			Level = 50,
		};
		var asmoMember = new Player
		{
			ObjectId = 2002,
			Name = "AsmoMember",
			Race = "ASMODIANS",
			Level = 50,
		};
		var groupRuntime = new PlayerGroupRuntime();
		groupRuntime.CreateOrUpdateGroup(77, [asmoLeader, asmoMember]);
		var registry = new RecordingConnectionRegistry([1001, 1002, 2001, 2002]);
		var runtimeContext = CreateAutoGroupRuntimeContext(
			[CreateAutoGroup(107, 300110000)],
			new InstanceCooltimeTable(
			[
				new InstanceCooltimeSummary(8, 300110000, "PC_ALL", MaxCount: 1, MaxMemberLight: 2, MaxMemberDark: 2),
			]));
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			autoGroupRegistrations,
			registry,
			groupRuntime);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(fixture.Connection, asmoLeader);

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

		Assert.Empty(sentPackets);
		Assert.False(autoGroupRegistrations.IsSearching(1001, 107));
		Assert.False(autoGroupRegistrations.IsSearching(2001, 107));
		Assert.Equal(0, autoGroupRegistrations.GetLookingPartyCount(107));
		Assert.Equal([2001, 2001, 2001, 2002, 2002, 2002, 1001, 1002, 2001, 2002], registry.SentPackets.Select(delivery => delivery.PlayerObjectId));
		AssertFanoutPacketOrder(registry.SentPackets.Take(3).Select(delivery => delivery.Packet).ToArray());
		AssertFanoutPacketOrder(registry.SentPackets.Skip(3).Take(3).Select(delivery => delivery.Packet).ToArray());
		AssertReadyWindow(registry.SentPackets[6], 1001, 107);
		AssertReadyWindow(registry.SentPackets[7], 1002, 107);
		AssertReadyWindow(registry.SentPackets[8], 2001, 107);
		AssertReadyWindow(registry.SentPackets[9], 2002, 107);
		Assert.True(runtimeContext.WorldMapStates.TryGetWorldMapInstance(300110000, 2, out var allocatedInstance));
		Assert.NotNull(allocatedInstance);
		Assert.Equal(4, allocatedInstance!.MaxPlayers);
		Assert.True(allocatedInstance.InstanceCreateNotified);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(102);
					buffer.WriteC(0);
				}));

		var pressEnterWindow = Assert.IsType<SmAutoGroup>(Assert.Single(sentPackets));
		Assert.Equal(107, pressEnterWindow.MaskId);
		Assert.Equal(5, pressEnterWindow.WindowId);
		Assert.Equal(PlayerTeamMembership.None, asmoLeader.TeamMembership);
		Assert.False(groupRuntime.HasMember(77, asmoLeader.ObjectId));
		Assert.True(groupRuntime.HasMember(77, asmoMember.ObjectId));
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupQuickEntryTeamPlayerSendsJavaNotLeaderMessage()
	{
		var sentPackets = new List<GameServerPacket>();
		var groupRuntime = new PlayerGroupRuntime();
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(107, 300110000));
		var leader = new Player
		{
			ObjectId = 1010,
			Name = "QuickLeader",
			Race = "ELYOS",
			Level = 50,
		};
		var member = new Player
		{
			ObjectId = 1011,
			Name = "QuickMember",
			Race = "ELYOS",
			Level = 50,
		};
		groupRuntime.CreateOrUpdateGroup(77, [leader, member]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			playerGroupRuntime: groupRuntime);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(fixture.Connection, leader);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(107);
					buffer.WriteC(100);
					buffer.WriteC(1);
				}));

		var message = Assert.IsType<SmSystemMessage>(Assert.Single(sentPackets));
		Assert.Equal(1400182, message.MessageId);
		Assert.Empty(message.Parameters);
	}

	[Fact]
	public async Task ProcessPacketAsync_AutoGroupHarmonyMissingTicketSendsJavaMemberAndRequesterMessages()
	{
		var sentPackets = new List<GameServerPacket>();
		var registry = new RecordingConnectionRegistry([1013]);
		var groupRuntime = new PlayerGroupRuntime();
		var runtimeContext = CreateAutoGroupRuntimeContext(CreateAutoGroup(33, 300350000));
		var leader = new Player
		{
			ObjectId = 1012,
			Name = "HarmonyLeader",
			Race = "ELYOS",
			Level = 50,
		};
		var member = new Player
		{
			ObjectId = 1013,
			Name = "HarmonyMember",
			Race = "ELYOS",
			Level = 50,
		};
		groupRuntime.CreateOrUpdateGroup(77, [leader, member]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			new GameServerOptions(),
			sentPackets.Add,
			runtimeContext,
			connectionRegistry: registry,
			playerGroupRuntime: groupRuntime);
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);
		SetActivePlayer(fixture.Connection, leader);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				200,
				buffer =>
				{
					buffer.WriteD(33);
					buffer.WriteC(100);
					buffer.WriteC(2);
				}));

		var memberDelivery = Assert.Single(registry.SentPackets);
		Assert.Equal(member.ObjectId, memberDelivery.PlayerObjectId);
		Assert.Equal(1400219, Assert.IsType<SmSystemMessage>(memberDelivery.Packet).MessageId);
		var requesterMessage = Assert.IsType<SmSystemMessage>(Assert.Single(sentPackets));
		Assert.Equal(1400187, requesterMessage.MessageId);
		Assert.Equal(["HarmonyMember"], requesterMessage.Parameters);
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

	private static string ReadMessage(SmMessage packet, int expectedChatType = 25)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(expectedChatType, (int)reader.ReadC());
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

	private static void AssertFanoutPacketOrder(IReadOnlyList<GameServerPacket> packets)
	{
		Assert.Collection(
			packets,
			packet =>
			{
				var autoGroup = Assert.IsType<SmAutoGroup>(packet);
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
				Assert.Equal(1, autoGroup.WindowId);
			});
	}

	private static void AssertReadyWindow(PacketDelivery delivery, int playerObjectId, int maskId)
	{
		Assert.Equal(playerObjectId, delivery.PlayerObjectId);
		var autoGroup = Assert.IsType<SmAutoGroup>(delivery.Packet);
		Assert.Equal(maskId, autoGroup.MaskId);
		Assert.Equal(4, autoGroup.WindowId);
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
		return CreateAutoGroupRuntimeContext(autoGroups, new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>()));
	}

	private static GameServerRuntimeContext CreateAutoGroupRuntimeContext(
		IReadOnlyList<AutoGroupSummary> autoGroups,
		InstanceCooltimeTable instanceCooltimes)
	{
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(CreateDataManagerForTest(CreateStaticDataForAutoGroups(autoGroups, instanceCooltimes)));
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

	private static StaticData CreateStaticDataForAutoGroups(
		IReadOnlyList<AutoGroupSummary> autoGroups,
		InstanceCooltimeTable? instanceCooltimes = null)
	{
		var emptySkillTemplates = new SkillTemplateTable(Array.Empty<SkillTemplateSummary>());
		var constructor = typeof(StaticData).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
		var worldMaps = autoGroups
			.Select(autoGroup => new WorldMapSummary(autoGroup.InstanceMapId, IsInstance: true, TwinCount: 1))
			.GroupBy(worldMap => worldMap.MapId)
			.Select(group => group.Last())
			.ToArray();
		return (StaticData)constructor.Invoke(
		[
			string.Empty,
			Array.Empty<string>(),
			new Dictionary<string, int>(),
			Array.Empty<string>(),
			worldMaps,
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
			instanceCooltimes ?? new InstanceCooltimeTable(Array.Empty<InstanceCooltimeSummary>()),
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
			AutoGroupLookingPartyRegistrationService? autoGroupLookingPartyRegistrations = null,
			IGameClientConnectionRegistry? connectionRegistry = null,
			PlayerGroupRuntime? playerGroupRuntime = null)
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
					connectionRegistry: connectionRegistry,
					sentPacketObserver: sentPacketObserver,
					playerGroupRuntime: playerGroupRuntime,
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

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly IReadOnlyCollection<int> _onlineObjectIds;
		private readonly IReadOnlyList<Player> _onlinePlayers;

		public RecordingConnectionRegistry(IReadOnlyCollection<int> onlineObjectIds)
		{
			_onlineObjectIds = onlineObjectIds;
			_onlinePlayers = Array.Empty<Player>();
		}

		public RecordingConnectionRegistry(IReadOnlyList<Player> onlinePlayers)
		{
			_onlinePlayers = onlinePlayers;
			_onlineObjectIds = onlinePlayers.Select(player => player.ObjectId).ToArray();
		}

		public List<PacketDelivery> SentPackets { get; } = [];

		public List<BroadcastDelivery> BroadcastPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in _onlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (!_onlineObjectIds.Contains(playerObjectId))
				return Task.FromResult(false);

			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var recipients = _onlinePlayers
				.Where(player => filter?.Invoke(player) ?? true)
				.Select(player => player.ObjectId)
				.ToArray();
			BroadcastPackets.Add(new BroadcastDelivery(packet, recipients));
			return Task.FromResult(recipients.Length);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			throw new NotSupportedException();
		}
	}

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);

	private sealed record BroadcastDelivery(GameServerPacket Packet, IReadOnlyList<int> RecipientObjectIds);
}
