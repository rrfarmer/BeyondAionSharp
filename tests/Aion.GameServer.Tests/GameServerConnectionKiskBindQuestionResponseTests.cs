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
using GameWorld = Aion.GameServer.World.World;

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

	[Fact]
	public async Task HandleQuestionResponseAsync_KiskBindDuplicateSendsAlreadyRegisteredAndConsumesRequest()
	{
		var player = CreatePlayer(boundKiskObjectId: 9001);
		SeedPendingKiskBind(player, kiskObjectId: 9001);
		var runtimeContext = new GameServerRuntimeContext();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			useMask: 0);
		runtimeContext.Kisks.RegisterKisk(kisk);
		Assert.True(world.TryAddObject(kisk.ObjectId, CreateKiskNpc(kisk.ObjectId, kisk.NpcId)));
		await using var pair = await TestConnectionPair.CreateAsync(runtimeContext, world);

		await pair.Connection.HandleQuestionResponseAsync(
			player,
			CreateQuestionResponse(SmQuestionWindow.RegisterBindstone, response: 1, senderObjectId: 9001));

		Assert.Null(player.PendingKiskBindRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		var systemMessage = Assert.IsType<SmSystemMessage>(Assert.Single(pair.SentPackets));
		Assert.Equal(1390161, systemMessage.MessageId);
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

	private static Player CreatePlayer(int boundKiskObjectId = 0)
	{
		return new Player
		{
			ObjectId = 1002,
			Name = "Responder",
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			BoundKiskObjectId = boundKiskObjectId
		};
	}

	private static WorldNpc CreateKiskNpc(int objectId, int npcId)
	{
		var template = new NpcTemplateSummary(
			npcId,
			"test_kisk",
			NameId: npcId + 100,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "PC_LIGHT_CASTLE_DOOR",
			Tribe: "KISK",
			Type: "NPC",
			MaxHp: 1000,
			Height: 2.5f,
			BoundRadius: 1.2f,
			State: WorldNpcState.DefaultSpawnState);
		return new WorldNpc(
			objectId,
			npcId,
			template,
			new WorldPosition(210010000, 1, 2, 3, 4));
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

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			_connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection => _connection;

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			GameServerRuntimeContext? runtimeContext = null,
			GameWorld? world = null)
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
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"kisk-bind-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					runtimeContext: runtimeContext,
					world: world,
					sentPacketObserver: sentPackets.Add,
					crypt: crypt);
				return new TestConnectionPair(client, connection, sentPackets);
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
