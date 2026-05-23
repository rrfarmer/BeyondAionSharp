using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionFlightZoneFanoutTests
{
	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_BroadcastsStatsSpeedThenStopFlyWhenFlyingGliderLeavesFlyArea()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(7201);
		player.SetFlyState(PlayerFlyState.Gliding);
		player.SetCreatureState(PlayerCreatureState.Gliding, enabled: true);
		player.IsFpRestoreActive = true;

		var result = await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.True(result.LeftValidFlyArea);
		Assert.False(player.IsInFlyingState());
		Assert.True(player.IsInGlidingState());
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.ChangeSpeed),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.StopFly));
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.All(registry.Broadcasts, broadcast =>
		{
			Assert.Equal(player.Position, broadcast.SourcePosition);
			Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
			Assert.True(broadcast.IncludeSourcePlayer);
		});
	}

	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_BroadcastsStatsSpeedThenLandWhenFlyingPlayerLeavesFlyArea()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = CreateFlyingPlayer(7202);
		player.IsFpReduceActive = true;

		var result = await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.True(result.LeftValidFlyArea);
		Assert.False(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.IsType<SmStatsInfo>(packet),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.ChangeSpeed),
			packet => AssertEmotion(packet, player.ObjectId, EmotionType.Land));
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.All(registry.Broadcasts, broadcast =>
		{
			Assert.Equal(player.Position, broadcast.SourcePosition);
			Assert.Equal(player.ObjectId, broadcast.SourceObjectId);
			Assert.True(broadcast.IncludeSourcePlayer);
		});
	}

	[Fact]
	public async Task RevalidatePlayerFlightZonesAsync_FeedsCreaturePvpZoneCountersFromStaticData()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		var zoneCounterService = new CreaturePvpZoneCounterService();
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry, runtimeContext, zoneCounterService);
		var player = new Player
		{
			ObjectId = 7301,
			Name = "pvp-zone-player",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210040000, 2700, 620, 150, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
		};
		Assert.Contains(
			dataManager.StaticData.CreaturePvpZones.GetZonesByMapId(player.Position.WorldId),
			zone => zone.Name == "PVP_87_210040000" && zone.Contains(player.Position));

		await pair.Connection.RevalidatePlayerFlightZonesAsync(player);
		var enteredCounters = zoneCounterService.GetCounters(player.ObjectId);
		player.Position = player.Position with { X = 100, Y = 100, Z = 150 };
		await pair.Connection.RevalidatePlayerFlightZonesAsync(player);

		Assert.Equal(1, enteredCounters.PvpZoneCount);
		Assert.Equal(0, enteredCounters.SiegeZoneCount);
		Assert.Equal(CreaturePvpZoneCounters.Empty, zoneCounterService.GetCounters(player.ObjectId));
		Assert.Empty(registry.PacketOrder);
	}

	private static Player CreateFlyingPlayer(int objectId)
	{
		var player = new Player
		{
			ObjectId = objectId,
			Name = $"flight-{objectId}",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 205, CurrentFp: 55),
			IsInsideFlyZone = true,
		};
		player.SetFlyState(PlayerFlyState.Flying);
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		return player;
	}

	private static void AssertEmotion(GameServerPacket packet, int expectedObjectId, EmotionType expectedEmotion)
	{
		var emotion = Assert.IsType<SmEmotion>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(emotion));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal((int)expectedEmotion, (int)reader.ReadC());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<GameServerPacket> PacketOrder { get; } = [];

		public List<BroadcastRecord> Broadcasts { get; } = [];

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
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			PacketOrder.Add(packet);
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
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

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
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

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			IGameClientConnectionRegistry registry,
			GameServerRuntimeContext? runtimeContext = null,
			CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null)
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
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"flight-zone-fanout-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					connectionRegistry: registry,
					creaturePvpZoneCounterService: creaturePvpZoneCounterService);
				return new TestConnectionPair(client, connection);
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
