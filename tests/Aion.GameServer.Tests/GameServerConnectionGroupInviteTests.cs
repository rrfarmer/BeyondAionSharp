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

public sealed class GameServerConnectionGroupInviteTests
{
	[Fact]
	public async Task HandleInviteToGroupAsync_GroupInviteSendsInviterMessageAndQuestion()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Invited"));

		Assert.Equal(GroupInviteRequestStatus.Requested, result?.Status);
		Assert.Equal(1, invited.ResponseRequester.Count);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1300173, "Invited");
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1002, sent.PlayerObjectId);
		Assert.Equal(SmQuestionWindow.PartyInvite, Assert.IsType<SmQuestionWindow>(sent.Packet).Code);
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_NoSuchUserSendsJavaFailure()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Missing"));

		Assert.Null(result);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1300627, "Missing");
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_DeniedGroupRequestsSendsRejectedInvite()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		invited.Settings.Deny = PlayerSettings.DenyGroupRequests;
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Invited"));

		Assert.Null(result);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1390116, "Invited");
		Assert.Empty(registry.SentPackets);
		Assert.Equal(0, invited.ResponseRequester.Count);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_GroupInviteDenyClearsRequestAndRejectsInviter()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerGroupInviteRequestService().SendInvite(inviter, invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.PartyInvite, response: 0));

		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.Null(inviter.CurrentGroupSnapshot);
		Assert.Null(invited.CurrentGroupSnapshot);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent.Packet), 1300161, "Invited");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_GroupInviteAcceptCreatesGroupAndFansOutEnteredPackets()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerGroupInviteRequestService().SendInvite(inviter, invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.PartyInvite, response: 1));

		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.NotNull(inviter.CurrentGroupSnapshot);
		Assert.Same(inviter.CurrentGroupSnapshot, invited.CurrentGroupSnapshot);
		Assert.Equal([1001, 1002], invited.CurrentGroupSnapshot?.MemberObjectIds);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1002 && send.Packet is SmGroupInfo);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1001 && send.Packet is SmSystemMessage);
		Assert.Contains(registry.SentPackets, send => send.Packet is SmGroupMemberInfo);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}

	private static CmInviteToGroup CreateInvitePacket(byte inviteType, string playerName)
	{
		return Assert.IsType<CmInviteToGroup>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(97, buffer =>
			{
				buffer.WriteC(inviteType);
				buffer.WriteS(playerName);
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
			PlayerGroupRuntime groups,
			Action<GameServerPacket>? sentPacketObserver = null,
			IDFactory? idFactory = null)
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
					"group-invite-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					idFactory: idFactory,
					sentPacketObserver: sentPacketObserver,
					playerGroupRuntime: groups,
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
