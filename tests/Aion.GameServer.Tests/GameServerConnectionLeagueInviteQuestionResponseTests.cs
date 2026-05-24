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
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionLeagueInviteQuestionResponseTests
{
	[Fact]
	public async Task HandleQuestionResponseAsync_LeagueInviteDenyClearsPendingAndSendsRejectToRequester()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		registry.OnlinePlayers.AddRange([inviter, invitedLeader]);
		SeedPendingInvite(inviter, invitedLeader, alliances);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, leagues, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.UnionInviteMe, response: 0));

		Assert.Null(invitedLeader.PendingLeagueInviteRequest);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent.Packet), 1300190, "InvitedLeader");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LeagueInviteAcceptCreatesLeagueAndFansOutAllianceInfo()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var idFactory = new IDFactory();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		registry.OnlinePlayers.AddRange([inviter, invitedLeader]);
		SeedPendingInvite(inviter, invitedLeader, alliances);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, leagues, idFactory);

		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.UnionInviteMe, response: 1));

		Assert.Null(invitedLeader.PendingLeagueInviteRequest);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(1));
		Assert.Equal(2, registry.SentPackets.Count);
		Assert.Equal([1001, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceInfo>(send.Packet));
		Assert.Equal(2, idFactory.GetUsedCount());
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_AcceptWhileTradingClearsRepresentedTradeStateLikeJavaCancelExchange()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true, IsTrading = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		registry.OnlinePlayers.AddRange([inviter, invitedLeader]);
		SeedPendingInvite(inviter, invitedLeader, alliances);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, leagues, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.UnionInviteMe, response: 1));

		Assert.False(invitedLeader.IsTrading);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_DenyWhileTradingDoesNotCancelRepresentedTradeStateLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true, IsTrading = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		registry.OnlinePlayers.AddRange([inviter, invitedLeader]);
		SeedPendingInvite(inviter, invitedLeader, alliances);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, leagues, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.UnionInviteMe, response: 0));

		Assert.True(invitedLeader.IsTrading);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_LeagueInviteWrongQuestionLeavesPendingRequest()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		registry.OnlinePlayers.AddRange([inviter, invitedLeader]);
		var pending = SeedPendingInvite(inviter, invitedLeader, alliances);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, leagues, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.BuddyListAddBuddyRequest, response: 1));

		Assert.Same(pending, invitedLeader.PendingLeagueInviteRequest);
		Assert.Empty(registry.SentPackets);
	}

	private static PendingLeagueInviteRequest SeedPendingInvite(
		Player inviter,
		Player invitedLeader,
		PlayerAllianceRuntime alliances)
	{
		var planner = new PlayerLeagueInvitePlanner();
		var setupPlan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);
		return planner.TryPutPendingRequest(invitedLeader, setupPlan).PendingRequest;
	}

	private static CmQuestionResponse CreateQuestionResponse(int questionId, byte response)
	{
		return Assert.IsType<CmQuestionResponse>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(50, buffer =>
			{
				buffer.WriteD(questionId);
				buffer.WriteC(response);
				buffer.WriteC(0);
				buffer.WriteH(0);
				buffer.WriteD(0);
				buffer.WriteD(0);
				buffer.WriteH(0);
			}), GameConnectionState.InGame));
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

	private static void AssertSystemMessagePayload(
		SmSystemMessage packet,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
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
			PlayerAllianceRuntime alliances,
			PlayerLeagueRuntime leagues,
			IDFactory idFactory)
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
					"league-invite-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					idFactory: idFactory,
					playerAllianceRuntime: alliances,
					playerLeagueRuntime: leagues);
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
		public List<Player> OnlinePlayers { get; } = [];
		public List<SentPacketRecord> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = OnlinePlayers.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in OnlinePlayers)
				action(player);
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
