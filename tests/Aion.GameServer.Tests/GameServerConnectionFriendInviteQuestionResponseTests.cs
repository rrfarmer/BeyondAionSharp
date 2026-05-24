using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionFriendInviteQuestionResponseTests
{
	[Fact]
	public async Task HandleFriendAddAsync_RegistersFriendInviteThroughResponseRequesterBeforeQuestionWindow()
	{
		var registry = new CapturingConnectionRegistry();
		var requester = CreatePlayer(1001, "Requester");
		var responder = CreatePlayer(2001, "Responder");
		registry.OnlinePlayers.AddRange([requester, responder]);
		await using var pair = await TestConnectionPair.CreateAsync(registry);

		await pair.Connection.HandleFriendAddAsync(requester, CreateFriendAdd("Responder", "hello"));

		Assert.NotNull(responder.PendingFriendRequest);
		Assert.Equal(1, responder.ResponseRequester.Count);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(2001, sent.PlayerObjectId);
		Assert.IsType<SmQuestionWindow>(sent.Packet);
	}

	[Fact]
	public async Task HandleFriendAddAsync_DuplicateFriendInviteUsesJavaBusyResponseRequesterSemantics()
	{
		var registry = new CapturingConnectionRegistry();
		var directPackets = new List<GameServerPacket>();
		var firstRequester = CreatePlayer(1001, "Requester");
		var secondRequester = CreatePlayer(1002, "Second");
		var responder = CreatePlayer(2001, "Responder");
		registry.OnlinePlayers.AddRange([firstRequester, secondRequester, responder]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, sentPacketObserver: directPackets.Add);
		await pair.Connection.HandleFriendAddAsync(firstRequester, CreateFriendAdd("Responder", "first"));

		await pair.Connection.HandleFriendAddAsync(secondRequester, CreateFriendAdd("Responder", "second"));

		Assert.Equal(1, responder.ResponseRequester.Count);
		Assert.Equal(1001, responder.PendingFriendRequest?.RequesterObjectId);
		Assert.Single(registry.SentPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(Assert.Single(directPackets)), 900847);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_FriendInviteDenyConsumesRegistryAndNotifiesRequester()
	{
		var registry = new CapturingConnectionRegistry();
		var requester = CreatePlayer(1001, "Requester");
		var responder = CreatePlayer(2001, "Responder");
		registry.OnlinePlayers.AddRange([requester, responder]);
		SeedPendingFriendInvite(requester, responder);
		await using var pair = await TestConnectionPair.CreateAsync(registry);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.BuddyListAddBuddyRequest, response: 0));

		Assert.Null(responder.PendingFriendRequest);
		Assert.Equal(0, responder.ResponseRequester.Count);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertFriendResponsePayload(Assert.IsType<SmFriendResponse>(sent.Packet), SmFriendResponse.TargetDenied, "Responder");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_FriendInviteAcceptConsumesRegistryAndPersistsFriendship()
	{
		var registry = new CapturingConnectionRegistry();
		var repository = new CapturingSocialRepository { AddFriendsResult = true };
		var requester = CreatePlayer(1001, "Requester");
		var responder = CreatePlayer(2001, "Responder");
		registry.OnlinePlayers.AddRange([requester, responder]);
		SeedPendingFriendInvite(requester, responder);
		await using var pair = await TestConnectionPair.CreateAsync(registry, repository);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.BuddyListAddBuddyRequest, response: 1));

		Assert.Null(responder.PendingFriendRequest);
		Assert.Equal(0, responder.ResponseRequester.Count);
		Assert.Equal([(1001, 2001)], repository.AddFriendsCalls);
		Assert.Contains(requester.Friends, friend => friend.ObjectId == 2001 && friend.Name == "Responder");
		Assert.Contains(responder.Friends, friend => friend.ObjectId == 1001 && friend.Name == "Requester");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_FriendInviteWrongQuestionLeavesRegistryRequest()
	{
		var registry = new CapturingConnectionRegistry();
		var requester = CreatePlayer(1001, "Requester");
		var responder = CreatePlayer(2001, "Responder");
		registry.OnlinePlayers.AddRange([requester, responder]);
		var pending = SeedPendingFriendInvite(requester, responder);
		await using var pair = await TestConnectionPair.CreateAsync(registry);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.UnionInviteMe, response: 1));

		Assert.Same(pending, responder.PendingFriendRequest);
		Assert.Equal(1, responder.ResponseRequester.Count);
		Assert.Empty(registry.SentPackets);
	}

	private static PendingFriendRequest SeedPendingFriendInvite(Player requester, Player responder)
	{
		var pending = new PendingFriendRequest(requester.ObjectId, requester.Name);
		responder.PendingFriendRequest = pending;
		Assert.True(responder.ResponseRequester.PutRequest(
			SmQuestionWindow.BuddyListAddBuddyRequest,
			new QuestionResponseRequest(requester.ObjectId, QuestionResponseRequestKind.FriendInvite, pending)));
		return pending;
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "WARRIOR",
			Gender = "MALE",
			IsOnline = true,
			Position = new WorldPosition(210010000, 1, 2, 3, 0)
		};
	}

	private static CmFriendAdd CreateFriendAdd(string targetName, string message)
	{
		return Assert.IsType<CmFriendAdd>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(111, buffer =>
			{
				buffer.WriteS(targetName);
				buffer.WriteS(message);
			}), GameConnectionState.InGame));
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

	private static void AssertFriendResponsePayload(SmFriendResponse packet, byte expectedCode, string expectedName)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedName, reader.ReadS());
		Assert.Equal(expectedCode, reader.ReadC());
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
			ISocialRepository? socialRepository = null,
			Action<GameServerPacket>? sentPacketObserver = null)
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
					"friend-invite-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					socialRepository: socialRepository,
					connectionRegistry: registry,
					sentPacketObserver: sentPacketObserver,
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

	private sealed class CapturingSocialRepository : ISocialRepository
	{
		public bool AddFriendsResult { get; init; }
		public List<(int PlayerObjectId, int FriendObjectId)> AddFriendsCalls { get; } = [];

		public Task<SocialPlayerInfo?> LoadPlayerByNameAsync(string playerName, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<SocialPlayerInfo?>(null);
		}

		public Task<bool> AddBlockedUserAsync(
			int playerObjectId,
			int blockedPlayerObjectId,
			string reason,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> AddFriendsAsync(int playerObjectId, int friendObjectId, CancellationToken cancellationToken = default)
		{
			AddFriendsCalls.Add((playerObjectId, friendObjectId));
			return Task.FromResult(AddFriendsResult);
		}

		public Task<bool> DeleteFriendsAsync(int playerObjectId, int friendObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> SetFriendMemoAsync(int playerObjectId, int friendObjectId, string memo, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> DeleteBlockedUserAsync(int playerObjectId, int blockedPlayerObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}

		public Task<bool> SetBlockedReasonAsync(int playerObjectId, int blockedPlayerObjectId, string reason, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record SentPacketRecord(int PlayerObjectId, GameServerPacket Packet);
}
