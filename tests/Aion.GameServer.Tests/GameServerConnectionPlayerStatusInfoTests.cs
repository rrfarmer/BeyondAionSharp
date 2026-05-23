using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionPlayerStatusInfoTests
{
	[Fact]
	public async Task HandlePlayerStatusInfoAsync_StartReadyCheckBroadcastsJavaStatuses()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var plan = await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(PlayerAllianceReadyCheckCommand.Start, selectedObjectId: member.ObjectId));

		Assert.NotNull(plan);
		Assert.Equal(PlayerAllianceReadyCheckCommand.Start, plan.Command);
		Assert.Equal(1, plan.ReadyStatusAfter);
		Assert.Equal([1001, 1001, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceReadyCheck>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_NonReadyCommandAndMissingAllianceNoopLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var player = new Player { ObjectId = 1001, Name = "Solo" };
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime());

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			player,
			CreatePacket(commandCode: 27, selectedObjectId: 1002)));
		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			player,
			CreatePacket(PlayerAllianceReadyCheckCommand.Start, selectedObjectId: 1002)));
		Assert.Empty(registry.SentPackets);
	}

	private static CmPlayerStatusInfo CreatePacket(
		PlayerAllianceReadyCheckCommand command,
		int selectedObjectId)
	{
		return CreatePacket((int)command, selectedObjectId);
	}

	private static CmPlayerStatusInfo CreatePacket(int commandCode, int selectedObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteC(commandCode);
		writer.WriteD(selectedObjectId);
		writer.WriteD(0);
		writer.WriteD(0);
		var packet = new CmPlayerStatusInfo(96, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			_connection = connection;
		}

		public GameServerConnection Connection => _connection;

		public static async Task<TestConnectionPair> CreateAsync(
			IGameClientConnectionRegistry registry,
			PlayerAllianceRuntime playerAllianceRuntime)
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
					"player-status-info-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerAllianceRuntime: playerAllianceRuntime);
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacketRecord> SentPackets { get; } = [];

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
			SentPackets.Add(new SentPacketRecord(playerObjectId, packet));
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
			return Task.FromResult(0);
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

	private sealed record SentPacketRecord(int PlayerObjectId, GameServerPacket Packet);
}
