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

public sealed class GameServerConnectionChargeAllQuestionResponseTests
{
	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllDenyConsumesResponseRequester()
	{
		var player = CreatePlayer();
		SeedPendingChargeAll(player, chargeWay: 1);
		await using var pair = await TestConnectionPair.CreateAsync();

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.ItemChargeAllConfirm, response: 0));

		Assert.Null(player.PendingChargeAllRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_ChargeAllWrongChargeQuestionLeavesRegistryRequest()
	{
		var player = CreatePlayer();
		var pending = SeedPendingChargeAll(player, chargeWay: 1);
		await using var pair = await TestConnectionPair.CreateAsync();

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.ItemCharge2AllConfirm, response: 1));

		Assert.Same(pending, player.PendingChargeAllRequest);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	private static PendingChargeAllRequest SeedPendingChargeAll(Player player, int chargeWay)
	{
		var pending = new PendingChargeAllRequest(
			SenderObjectId: 7001,
			ChargeWay: chargeWay,
			PaymentAmount: 0,
			Items:
			[
				new PendingChargeAllItem(
					ObjectId: 9001,
					ItemId: 100,
					PreviousCharge: 0,
					TargetCharge: 10000,
					Level: 2)
			]);
		player.PendingChargeAllRequest = pending;
		Assert.True(player.ResponseRequester.PutRequest(
			chargeWay == 1 ? SmQuestionWindow.ItemChargeAllConfirm : SmQuestionWindow.ItemCharge2AllConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.ChargeAll, pending)));
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
					"charge-all-question-response-test",
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
