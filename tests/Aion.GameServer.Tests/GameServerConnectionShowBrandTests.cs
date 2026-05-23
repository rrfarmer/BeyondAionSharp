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

public sealed class GameServerConnectionShowBrandTests
{
	[Fact]
	public async Task HandleShowBrandCommandAsync_SoloEchoSendsShowBrandLikeJavaCmShowBrand()
	{
		var registry = new CapturingConnectionRegistry();
		await using var pair = await TestConnectionPair.CreateAsync(registry);
		var player = new Player { ObjectId = 1001, Name = "Solo" };

		var plan = await pair.Connection.HandleShowBrandCommandAsync(
			player,
			CreatePacket(action: 99, brandId: 7, targetObjectId: 9001));

		Assert.Equal(PlayerShowBrandCommandPlanStatus.SoloEcho, plan.Status);
		var send = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, send.PlayerObjectId);
		Assert.IsType<SmShowBrand>(send.Packet);
	}

	[Fact]
	public async Task HandleShowBrandCommandAsync_GroupLeaderBroadcastsUpdateBrandWithRegistry()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader" };
		var member = new Player { ObjectId = 1002, Name = "Member" };
		groups.CreateOrUpdateGroup(70001, [leader, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, playerGroupRuntime: groups);

		var plan = await pair.Connection.HandleShowBrandCommandAsync(
			leader,
			CreatePacket(action: 1, brandId: 3, targetObjectId: 8001));

		Assert.Equal(PlayerShowBrandCommandPlanStatus.GroupUpdated, plan.Status);
		Assert.Equal([1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmShowBrand>(send.Packet));
	}

	[Fact]
	public async Task HandleShowBrandCommandAsync_AllianceViceCaptainBroadcastsUpdateBrandWithRegistry()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader" };
		var viceCaptain = new Player { ObjectId = 1002, Name = "Vice" };
		var member = new Player { ObjectId = 1003, Name = "Member" };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, viceCaptain);
		alliances.AddMember(88001, member);
		alliances.SetViceCaptains(88001, [viceCaptain.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, playerAllianceRuntime: alliances);

		var plan = await pair.Connection.HandleShowBrandCommandAsync(
			viceCaptain,
			CreatePacket(action: 1, brandId: 5, targetObjectId: 8005));

		Assert.Equal(PlayerShowBrandCommandPlanStatus.AllianceUpdated, plan.Status);
		Assert.Equal([1001, 1002, 1003], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmShowBrand>(send.Packet));
	}

	private static CmShowBrand CreatePacket(int action, int brandId, int targetObjectId)
	{
		using var writer = new PacketBuffer();
		writer.WriteD(action);
		writer.WriteD(brandId);
		writer.WriteD(targetObjectId);
		var packet = new CmShowBrand(181, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
			PlayerGroupRuntime? playerGroupRuntime = null,
			PlayerAllianceRuntime? playerAllianceRuntime = null)
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
					"show-brand-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerGroupRuntime: playerGroupRuntime,
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
