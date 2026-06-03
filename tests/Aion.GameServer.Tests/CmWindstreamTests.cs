using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CmWindstreamTests
{
	// Java parity: CM_WINDSTREAM registered at opcode 70 in GameClientPacketFactory (InGame only).
	[Fact]
	public void TryCreatePacket_RegistersJavaWindstreamOpcodeAsInGameOnly()
	{
		Assert.IsType<CmWindstream>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(70, buffer =>
				{
					buffer.WriteD(1001); // teleportId
					buffer.WriteD(500);  // distance
					buffer.WriteD(1);    // state
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(70, buffer =>
			{
				buffer.WriteD(1001);
				buffer.WriteD(500);
				buffer.WriteD(1);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ParsesTeleportIdDistanceAndState()
	{
		// Java parity: CM_WINDSTREAM.readImpl: readD() + readD() + readD().
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(2001); // teleportId
		buffer.WriteD(800);  // distance
		buffer.WriteD(1);    // state = entering windstream

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(2001, packet.TeleportId);
		Assert.Equal(800, packet.Distance);
		Assert.Equal(1, packet.State);
	}

	[Fact]
	public async Task HandleWindstreamAsync_State0_ClearsRideMode()
	{
		// Java parity: CM_WINDSTREAM.runImpl state 0 -> player.unsetPlayerMode(PlayerMode.RIDE).
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8001);
		player.IsInRideMode = true;

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 0));

		Assert.False(player.IsInRideMode);
		// state 0 still sends SmWindstream(0, 1)
		Assert.Single(pair.SentPackets, packet => packet is SmWindstream);
	}

	[Fact]
	public async Task HandleWindstreamAsync_State1EnteringWindstream_SetsFlightPathAndBroadcastsEmotion()
	{
		// Java parity: CM_WINDSTREAM.runImpl state 1 -> FlightPath.Type.WINDSTREAM + SM_EMOTION(WINDSTREAM).
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8002);
		// Player must be in fly state (isFlying == true) and no active flight path
		player.SetFlyState(PlayerFlyState.Flying);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 1, teleportId: 5001, distance: 300));

		Assert.Equal(PlayerFlightPathType.Windstream, player.FlightPathType);
		Assert.True(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsFpRestoreActive);
		// State 1 must NOT send SmWindstream
		Assert.Empty(pair.SentPackets);
		// Emotion broadcast to visible players
		var broadcast = Assert.Single(registry.Broadcasts);
		Assert.True(broadcast.IncludeSourcePlayer);
		Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
		var emotion = Assert.IsType<SmEmotion>(broadcast.Packet);
		using var emotionBuffer = new PacketBuffer(SerializeUnencryptedPayload(emotion));
		Assert.Equal(player.ObjectId, emotionBuffer.ReadD());
		Assert.Equal((byte)EmotionType.Windstream, emotionBuffer.ReadC());
	}

	[Fact]
	public async Task HandleWindstreamAsync_State1_GuardRejectsWhenAlreadyUsingFlightPath()
	{
		// Java parity: isUsingFlightTransporterOrWindstream() guard returns early.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8003);
		player.SetFlyState(PlayerFlyState.Flying);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		player.FlightPathType = PlayerFlightPathType.Windstream; // already in windstream

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 1));

		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task HandleWindstreamAsync_State1_GuardRejectsWhenNotFlying()
	{
		// Java parity: !player.isFlying() guard returns early.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8004);
		// No fly state set — not flying
		player.FlyState = PlayerFlyState.None;

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 1));

		Assert.Null(player.FlightPathType);
		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task HandleWindstreamAsync_State2_LeavingWindstreamToGliding_SetsGlidingAndSendsWindstreamEnd()
	{
		// Java parity: state 2 -> unset FLYING states, switchToGliding, SM_EMOTION(WINDSTREAM_END) + SM_WINDSTREAM.
		// UpdatePlayerStatsAndSpeedVisuallyAsync also broadcasts (ChangeSpeed emotion), so expect 2 broadcasts total.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateWindstreamingPlayer(8005);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 2));

		Assert.Null(player.FlightPathType);
		Assert.False(player.IsInFlyingState());
		Assert.True(player.IsInGlidingState()); // switchToGliding sets GLIDING
		Assert.True(player.IsInState(PlayerCreatureState.Gliding));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		// SmWindstream(2, 1) sent to player
		var windstreamPacket = Assert.Single(pair.SentPackets, p => p is SmWindstream);
		using var wsBuffer = new PacketBuffer(SerializeUnencryptedPayload(windstreamPacket));
		Assert.Equal(2, wsBuffer.ReadD());
		Assert.Equal(1, wsBuffer.ReadC());
		// 2 broadcasts: updateStatsAndSpeedVisually (ChangeSpeed) + WindstreamEnd emotion
		Assert.Equal(2, registry.Broadcasts.Count);
		var windstreamEndBroadcast = registry.Broadcasts[1];
		var windstreamEndEmotion = Assert.IsType<SmEmotion>(windstreamEndBroadcast.Packet);
		using var eBuffer = new PacketBuffer(SerializeUnencryptedPayload(windstreamEndEmotion));
		eBuffer.ReadD(); // objectId
		Assert.Equal((byte)EmotionType.WindstreamEnd, eBuffer.ReadC());
	}

	[Fact]
	public async Task HandleWindstreamAsync_State3_LeavingWindstream_SetsActiveAndSendsWindstreamExit()
	{
		// Java parity: state 3 -> unset FLYING states, updateStatsAndSpeedVisually, SM_EMOTION(WINDSTREAM_EXIT) + SM_WINDSTREAM.
		// UpdatePlayerStatsAndSpeedVisuallyAsync also broadcasts (ChangeSpeed emotion), so expect 2 broadcasts total.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateWindstreamingPlayer(8006);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 3));

		Assert.Null(player.FlightPathType);
		Assert.False(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		// SmWindstream(3, 1) sent to player
		var windstreamPacket = Assert.Single(pair.SentPackets, p => p is SmWindstream);
		using var wsBuffer = new PacketBuffer(SerializeUnencryptedPayload(windstreamPacket));
		Assert.Equal(3, wsBuffer.ReadD());
		// 2 broadcasts: updateStatsAndSpeedVisually (ChangeSpeed) + WindstreamExit emotion
		Assert.Equal(2, registry.Broadcasts.Count);
		var windstreamExitBroadcast = registry.Broadcasts[1];
		var windstreamExitEmotion = Assert.IsType<SmEmotion>(windstreamExitBroadcast.Packet);
		using var eBuffer = new PacketBuffer(SerializeUnencryptedPayload(windstreamExitEmotion));
		eBuffer.ReadD(); // objectId
		Assert.Equal((byte)EmotionType.WindstreamExit, eBuffer.ReadC());
	}

	[Fact]
	public async Task HandleWindstreamAsync_State2Or3_GuardRejectsWhenNotInWindstream()
	{
		// Java parity: !isUsingFlightPath(WINDSTREAM) guard returns early.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8007);
		// Player is flying but not in windstream flight path

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 2));

		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public async Task HandleWindstreamAsync_State7_StartBoost_BroadcastsWindstreamStartBoostEmotion()
	{
		// Java parity: state 7 -> SM_EMOTION(WINDSTREAM_START_BOOST) + SM_WINDSTREAM.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateWindstreamingPlayer(8008);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 7));

		Assert.Single(pair.SentPackets, p => p is SmWindstream);
		var broadcast = Assert.Single(registry.Broadcasts);
		var emotion = Assert.IsType<SmEmotion>(broadcast.Packet);
		using var buf = new PacketBuffer(SerializeUnencryptedPayload(emotion));
		buf.ReadD();
		Assert.Equal((byte)EmotionType.WindstreamStartBoost, buf.ReadC());
	}

	[Fact]
	public async Task HandleWindstreamAsync_State8_EndBoost_BroadcastsWindstreamEndBoostEmotion()
	{
		// Java parity: state 8 -> SM_EMOTION(WINDSTREAM_END_BOOST) + SM_WINDSTREAM.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateWindstreamingPlayer(8009);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 8));

		Assert.Single(pair.SentPackets, p => p is SmWindstream);
		var broadcast = Assert.Single(registry.Broadcasts);
		var emotion = Assert.IsType<SmEmotion>(broadcast.Packet);
		using var buf = new PacketBuffer(SerializeUnencryptedPayload(emotion));
		buf.ReadD();
		Assert.Equal((byte)EmotionType.WindstreamEndBoost, buf.ReadC());
	}

	[Fact]
	public async Task HandleWindstreamAsync_UnknownState_ReturnsWithoutPacket()
	{
		// Java parity: default case logs warning and returns without sending SM_WINDSTREAM.
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(8010);

		await InvokeHandleWindstreamAsync(pair.Connection, player, CreatePacket(state: 99));

		Assert.Empty(pair.SentPackets);
		Assert.Empty(registry.Broadcasts);
	}

	[Fact]
	public void SmWindstream_WritesStateAndResult()
	{
		// Java parity: SM_WINDSTREAM(state, 1) writes writeD(state) + writeC(1).
		var packet = new SmWindstream(3, 1);
		using var buf = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(3, buf.ReadD());
		Assert.Equal(1, buf.ReadC());
		Assert.Equal(0, buf.Remaining);
	}

	[Fact]
	public void SmWindstream_OpcodeIs163()
	{
		// Java parity: ServerPacketsOpcodes line 181 -> addPacketOpcode(163, SM_WINDSTREAM.class).
		Assert.Equal(163, SmWindstream.PacketOpCode);
	}

	[Fact]
	public void SmWindstreamAnnounce_WritesAllFields()
	{
		// Java parity: SM_WINDSTREAM_ANNOUNCE.writeImpl: writeD(bidirectional) writeD(mapId) writeD(streamId) writeC(state).
		// bidirectional=1 (ONE_WAY), mapId=210050000, streamId=77, state=1.
		var packet = new SmWindstreamAnnounce(flyPathId: 1, mapId: 210050000, streamId: 77, state: 1);
		using var buf = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(1, buf.ReadD()); // flyPathId (ONE_WAY)
		Assert.Equal(210050000, buf.ReadD()); // mapId
		Assert.Equal(77, buf.ReadD()); // streamId
		Assert.Equal(1, buf.ReadC()); // state
		Assert.Equal(0, buf.Remaining);
	}

	[Fact]
	public void SmWindstreamAnnounce_OpcodeIs164()
	{
		// Java parity: ServerPacketsOpcodes line 182 -> addPacketOpcode(164, SM_WINDSTREAM_ANNOUNCE.class).
		Assert.Equal(164, SmWindstreamAnnounce.PacketOpCode);
	}

	[Fact]
	public void WindstreamTable_GetByMapId_ReturnsLocationsForMap()
	{
		// Java parity: WindstreamData.getStreamTemplate(mapId) returns WindstreamTemplate with its locations.
		var locations = new[]
		{
			new WindstreamLocationSummary(FlyPathId: 1, MapId: 210050000, StreamId: 77, State: 1),
			new WindstreamLocationSummary(FlyPathId: 0, MapId: 210050000, StreamId: 100, State: 1),
			new WindstreamLocationSummary(FlyPathId: 2, MapId: 220070000, StreamId: 1, State: 1),
		};
		var table = new WindstreamTable(locations);

		var forMap1 = table.GetByMapId(210050000);
		var forMap2 = table.GetByMapId(220070000);
		var forMissing = table.GetByMapId(999999999);

		Assert.Equal(2, forMap1.Count);
		Assert.Single(forMap2);
		Assert.Empty(forMissing);
		Assert.Contains(forMap1, loc => loc.StreamId == 77 && loc.FlyPathId == 1);
		Assert.Contains(forMap1, loc => loc.StreamId == 100 && loc.FlyPathId == 0);
	}

	[Fact]
	public void WindstreamTable_Count_ReflectsTotalLocations()
	{
		var table = new WindstreamTable([
			new WindstreamLocationSummary(1, 210050000, 77, 1),
			new WindstreamLocationSummary(0, 210050000, 100, 1),
		]);

		Assert.Equal(2, table.Count);
	}

	[Fact]
	public void SmInstanceCountInfo_WritesMapIdInstanceIdAndSoloFlag()
	{
		// Java parity: SM_INSTANCE_COUNT_INFO.writeImpl: writeD(mapId) writeD(instanceId) writeD(1).
		var packet = new SmInstanceCountInfo(mapId: 300030000, instanceId: 2);
		using var buf = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(300030000, buf.ReadD()); // mapId
		Assert.Equal(2, buf.ReadD()); // instanceId
		Assert.Equal(1, buf.ReadD()); // solo flag hardcoded
		Assert.Equal(0, buf.Remaining);
	}

	[Fact]
	public void SmInstanceCountInfo_OpcodeIs147()
	{
		// Java parity: ServerPacketsOpcodes line 165 -> addPacketOpcode(147, SM_INSTANCE_COUNT_INFO.class).
		Assert.Equal(147, SmInstanceCountInfo.PacketOpCode);
	}

	private static CmWindstream CreatePacket(int state = 0, int teleportId = 0, int distance = 0)
	{
		var packet = new CmWindstream(70, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(teleportId);
		buffer.WriteD(distance);
		buffer.WriteD(state);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static Player CreateFlyingPlayer(int objectId)
	{
		var player = new Player
		{
			ObjectId = objectId,
			Name = $"windstream-{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 200, CurrentMp: 200, CurrentFp: 100),
			IsInsideFlyZone = true,
		};
		return player;
	}

	private static Player CreateWindstreamingPlayer(int objectId)
	{
		var player = CreateFlyingPlayer(objectId);
		player.SetFlyState(PlayerFlyState.Flying);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		player.FlightPathType = PlayerFlightPathType.Windstream;
		return player;
	}

	private static async Task InvokeHandleWindstreamAsync(GameServerConnection connection, Player player, CmWindstream packet)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"HandleWindstreamAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [player, packet]));
		await task;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		// Java parity: encoded opcode format from GameClientPacketFactory.TryCreatePacket.
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		// Uses first-packet-unencrypted behavior: the crypt's first EncryptServerPayload call
		// sets IsEnabled=true but does not encrypt, yielding plain payload bytes.
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..]; // skip 2-byte length + 2-byte opcode + 1-byte static code + 2-byte ~opcode
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<GameServerPacket> PacketOrder { get; } = [];
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }
		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection) { }

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action) { }

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			PacketOrder.Add(packet);
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			PacketOrder.Add(packet);
			return Task.FromResult(1);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
			=> Task.FromResult(0);

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
			=> Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
			=> Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
			=> Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
			=> Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
			=> Task.FromResult(false);
	}

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			Connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection { get; }
		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(IGameClientConnectionRegistry? registry)
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
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"windstream-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TestConnectionPair(client, connection, sentPackets);
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
