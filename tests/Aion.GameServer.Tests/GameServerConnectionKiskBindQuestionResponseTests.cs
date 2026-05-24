using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionKiskBindQuestionResponseTests
{
	[Fact]
	public async Task HandleQuestionResponseAsync_KiskBindDenyConsumesResponseRequester()
	{
		var player = CreatePlayer();
		SeedPendingKiskBind(player, kiskObjectId: 9001);
		await using var pair = await TestConnectionPair.CreateAsync();

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.RegisterBindstone, response: 0, senderObjectId: 9001));

		Assert.Null(player.PendingKiskBindRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_KiskBindWrongQuestionLeavesRegistryRequest()
	{
		var player = CreatePlayer();
		var pending = SeedPendingKiskBind(player, kiskObjectId: 9001);
		await using var pair = await TestConnectionPair.CreateAsync();

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.BuddyListAddBuddyRequest, response: 0, senderObjectId: 9001));

		Assert.Same(pending, player.PendingKiskBindRequest);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	private static PendingKiskBindRequest SeedPendingKiskBind(Player player, int kiskObjectId)
	{
		var pending = new PendingKiskBindRequest(kiskObjectId, SmQuestionWindow.RegisterBindstone);
		player.PendingKiskBindRequest = pending;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.RegisterBindstone,
			new QuestionResponseRequest(kiskObjectId, QuestionResponseRequestKind.KiskBind, pending)));
		return pending;
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1002,
			Name = "Responder",
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0)
		};
	}

	private static CmQuestionResponse CreateQuestionResponse(int questionId, byte response, int senderObjectId)
	{
		return Assert.IsType<CmQuestionResponse>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(50, buffer =>
			{
				buffer.WriteD(questionId);
				buffer.WriteC(response);
				buffer.WriteC(0);
				buffer.WriteH(0);
				buffer.WriteD(senderObjectId);
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

		public static async Task<TestConnectionPair> CreateAsync()
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
					"kisk-bind-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
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
}
