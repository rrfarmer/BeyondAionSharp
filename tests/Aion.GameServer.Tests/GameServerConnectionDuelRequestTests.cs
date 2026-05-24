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

public sealed class GameServerConnectionDuelRequestTests
{
	[Fact]
	public void ClientPacketFactory_ParsesDuelRequestPacket()
	{
		var packet = Assert.IsType<CmDuelRequest>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(114, buffer => buffer.WriteD(1002)), GameConnectionState.InGame));

		Assert.Equal(1002, packet.TargetObjectId);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(114, buffer => buffer.WriteD(1002)), GameConnectionState.Authed));
	}

	[Fact]
	public async Task HandleDuelRequestAsync_SendsTargetQuestionAndRequesterWithdrawQuestion()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);

		var result = await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));

		Assert.Equal(DuelRequestStatus.Requested, result.Status);
		Assert.Equal(1, target.ResponseRequester.Count);
		Assert.Equal(1, requester.ResponseRequester.Count);
		Assert.NotNull(target.PendingDuelRequest);
		Assert.NotNull(requester.PendingDuelWithdrawRequest);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				Assert.Equal(SmQuestionWindow.DuelAcceptRequest, Assert.IsType<SmQuestionWindow>(send.Packet).Code);
			},
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1301065, "Requester");
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				Assert.Equal(SmQuestionWindow.DuelWithdrawRequest, Assert.IsType<SmQuestionWindow>(send.Packet).Code);
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1300094, "Target");
			});
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_DuelDenyClosesRequesterQuestionAndRejectsResponder()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);
		await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));
		registry.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(target, CreateQuestionResponse(SmQuestionWindow.DuelAcceptRequest, response: 0));

		Assert.Null(target.PendingDuelRequest);
		Assert.Null(requester.PendingDuelWithdrawRequest);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Equal(0, requester.ResponseRequester.Count);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertCloseQuestionPayload(Assert.IsType<SmCloseQuestionWindow>(send.Packet), 1300097, "Target");
			},
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1301064, "Requester");
			});
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_DuelAcceptRegistersDuelAndSendsStartedPackets()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);
		await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));
		registry.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(target, CreateQuestionResponse(SmQuestionWindow.DuelAcceptRequest, response: 1));

		Assert.True(duelService.IsDueling(requester));
		Assert.Equal(1002, duelService.GetOpponentId(requester));
		Assert.Null(target.PendingDuelRequest);
		Assert.Null(requester.PendingDuelWithdrawRequest);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertCloseQuestionPayload(Assert.IsType<SmCloseQuestionWindow>(send.Packet), 0);
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertDuelStartedPayload(Assert.IsType<SmDuel>(send.Packet), 1002);
			},
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				AssertDuelStartedPayload(Assert.IsType<SmDuel>(send.Packet), 1001);
			});
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_DuelWithdrawCancelsTargetPendingRequest()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);
		await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));
		registry.SentPackets.Clear();

		await pair.Connection.HandleQuestionResponseAsync(requester, CreateQuestionResponse(SmQuestionWindow.DuelWithdrawRequest, response: 1));

		Assert.Null(target.PendingDuelRequest);
		Assert.Null(requester.PendingDuelWithdrawRequest);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Equal(0, requester.ResponseRequester.Count);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				AssertCloseQuestionPayload(Assert.IsType<SmCloseQuestionWindow>(send.Packet), 1300134, "Requester");
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1300135, "Target");
			});
	}

	[Fact]
	public async Task HandleDuelRequestAsync_TargetDenySettingSendsJavaRejectedDuel()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		target.Settings.Deny = PlayerSettings.DenyDuelRequests;
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);

		var result = await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));

		Assert.Equal(DuelRequestStatus.Rejected, result.Status);
		Assert.Equal(0, target.ResponseRequester.Count);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent.Packet), 1390120, "Target");
	}

	[Fact]
	public async Task PlayerDuelRequestService_LoseDuelSendsLostWonResultsAndRemovesDuel()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);
		await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));
		await pair.Connection.HandleQuestionResponseAsync(target, CreateQuestionResponse(SmQuestionWindow.DuelAcceptRequest, response: 1));

		var plan = duelService.LoseDuel(target, Resolve);

		Assert.Equal(DuelEndStatus.Ended, plan.Status);
		Assert.False(duelService.IsDueling(requester));
		Assert.False(duelService.IsDueling(target));
		Assert.Collection(
			plan.PacketIntents,
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				AssertDuelResultPayload(Assert.IsType<SmDuel>(intent.Packet), resultId: 0, messageId: 1300099, playerName: "Requester");
			},
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertDuelResultPayload(Assert.IsType<SmDuel>(intent.Packet), resultId: 2, messageId: 1300098, playerName: "Target");
			});

		Player? Resolve(int objectId)
		{
			return objectId == requester.ObjectId ? requester : objectId == target.ObjectId ? target : null;
		}
	}

	[Fact]
	public async Task PlayerDuelRequestService_DrawDuelSendsDrawResultsAndRemovesDuel()
	{
		var registry = new CapturingConnectionRegistry();
		var duelService = new PlayerDuelRequestService();
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(1002, "Target");
		registry.OnlinePlayers.AddRange([requester, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, duelService);
		await pair.Connection.HandleDuelRequestAsync(requester, CreateDuelPacket(1002));
		await pair.Connection.HandleQuestionResponseAsync(target, CreateQuestionResponse(SmQuestionWindow.DuelAcceptRequest, response: 1));

		var plan = duelService.DrawDuel(requester, Resolve);

		Assert.Equal(DuelEndStatus.Ended, plan.Status);
		Assert.False(duelService.IsDueling(requester));
		Assert.False(duelService.IsDueling(target));
		Assert.Collection(
			plan.PacketIntents,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertDuelResultPayload(Assert.IsType<SmDuel>(intent.Packet), resultId: 1, messageId: 1300100, playerName: "Target");
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				AssertDuelResultPayload(Assert.IsType<SmDuel>(intent.Packet), resultId: 1, messageId: 1300100, playerName: "Requester");
			});

		Player? Resolve(int objectId)
		{
			return objectId == requester.ObjectId ? requester : objectId == target.ObjectId ? target : null;
		}
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			IsOnline = true,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}

	private static CmDuelRequest CreateDuelPacket(int targetObjectId)
	{
		return Assert.IsType<CmDuelRequest>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(114, buffer => buffer.WriteD(targetObjectId)), GameConnectionState.InGame));
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

	private static void AssertCloseQuestionPayload(
		SmCloseQuestionWindow packet,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		for (var index = 0; index < 3; index++)
			Assert.Equal(index < expectedParameters.Length ? expectedParameters[index] : string.Empty, reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertDuelStartedPayload(SmDuel packet, int expectedOpponentObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(expectedOpponentObjectId, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertDuelResultPayload(
		SmDuel packet,
		byte resultId,
		int messageId,
		string playerName)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(resultId, (byte)reader.ReadC());
		Assert.Equal(messageId, reader.ReadD());
		Assert.Equal(playerName, reader.ReadS());
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
			PlayerDuelRequestService duelService)
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
					"duel-request-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerDuelRequestService: duelService,
					crypt: crypt);
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
