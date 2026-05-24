using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionSoulBindQuestionResponseTests
{
	[Fact]
	public async Task HandleQuestionResponseAsync_SoulBindDenyConsumesResponseRequesterAndSendsCancel()
	{
		var directPackets = new List<GameServerPacket>();
		var player = CreatePlayer();
		SeedPendingSoulBind(player);
		await using var pair = await TestConnectionPair.CreateAsync(directPackets.Add);

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.SoulBoundItemConfirm, response: 0));

		Assert.Null(player.PendingSoulBindRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		var packet = Assert.IsType<SmSystemMessage>(Assert.Single(directPackets));
		Assert.Equal(1300487, ReadSystemMessageId(packet));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_SoulBindMissingRegistryClearsAdapterSlot()
	{
		var player = CreatePlayer();
		player.PendingSoulBindRequest = new PendingSoulBindRequest(9001, 1, "Practice Sword");
		await using var pair = await TestConnectionPair.CreateAsync();

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.SoulBoundItemConfirm, response: 0));

		Assert.Null(player.PendingSoulBindRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	private static PendingSoulBindRequest SeedPendingSoulBind(Player player)
	{
		var pending = new PendingSoulBindRequest(9001, 1, "Practice Sword");
		player.PendingSoulBindRequest = pending;
		Assert.True(player.ResponseRequester.PutRequest(
			SmQuestionWindow.SoulBoundItemConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.SoulBind, pending)));
		return pending;
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1002,
			Name = "Responder",
		};
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

	private static int ReadSystemMessageId(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadC();
		reader.ReadC();
		reader.ReadD();
		return reader.ReadD();
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

		public static async Task<TestConnectionPair> CreateAsync(Action<GameServerPacket>? sentPacketObserver = null)
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
					"soulbind-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
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
}
